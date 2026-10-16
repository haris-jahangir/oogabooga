using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the player's animation clips and Animator Controller from PNG frames.
///
/// 1. Drop 4 PNGs into each of Assets/Art/Player/Idle, Walk, Jump, Scare.
///    Frames play in file-name order, e.g. walk_1.png, walk_2.png, ...
/// 2. Run Tools > Player > Build Animations.
///
/// Clips are written to Assets/Animations/Player, Assets/Animations/Player.controller is
/// filled in, and it is assigned to the "player" object in the open scene (adding
/// PlayerMovement/PlayerAnimator if missing). Re-run any time you change the images.
/// </summary>
public static class PlayerAnimationBuilder
{
    const string ArtRoot = "Assets/Art/Player";
    const string ClipRoot = "Assets/Animations/Player";
    const string ControllerPath = "Assets/Animations/Player.controller";
    const string PlayerObjectName = "player";
    const float FramesPerSecond = 8f;
    const float WalkSpeedThreshold = 0.1f;
    static readonly string[] AnimationNames = { "Idle", "Walk", "Jump", "Scare" };

    [MenuItem("Tools/Player/Create Art Folders")]
    public static void CreateArtFolders()
    {
        foreach (string name in AnimationNames)
            EnsureFolder($"{ArtRoot}/{name}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ArtRoot);
    }

    [MenuItem("Tools/Player/Build Animations")]
    public static void Build()
    {
        CreateArtFolders();
        EnsureFolder(ClipRoot);

        var frames = AnimationNames.ToDictionary(n => n, LoadFrames);
        string[] missing = AnimationNames.Where(n => frames[n].Length == 0).ToArray();
        if (missing.Length > 0)
        {
            EditorUtility.DisplayDialog("Player Animations",
                $"No PNGs found in: {string.Join(", ", missing.Select(m => $"{ArtRoot}/{m}"))}

" +
                "Add the frame images and run Build Animations again.", "OK");
            return;
        }

        AnimationClip idleClip = WriteClip("Idle", frames["Idle"], loop: true);
        AnimationClip walkClip = WriteClip("Walk", frames["Walk"], loop: true);
        AnimationClip jumpClip = WriteClip("Jump", frames["Jump"], loop: false);
        AnimationClip scareClip = WriteClip("Scare", frames["Scare"], loop: true);

        AnimatorController controller = BuildController(idleClip, walkClip, jumpClip, scareClip);
        bool assigned = AssignToPlayer(controller, frames["Idle"][0]);

        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;
        Debug.Log($"[PlayerAnimationBuilder] Built {ControllerPath} (" +
                  string.Join(", ", AnimationNames.Select(n => $"{n} {frames[n].Length}")) + " frames)." +
                  (assigned ? $" Assigned to '{PlayerObjectName}' - save the scene to keep it." : $" No '{PlayerObjectName}' object found in the open scene."));
    }

    static Sprite[] LoadFrames(string animationName)
    {
        string folder = $"{ArtRoot}/{animationName}";
        if (!AssetDatabase.IsValidFolder(folder))
            return new Sprite[0];

        string[] paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == folder)
            .OrderBy(p => Path.GetFileNameWithoutExtension(p), NaturalComparer.Instance)
            .ToArray();

        var sprites = new List<Sprite>();
        foreach (string path in paths)
        {
            EnsureSpriteImport(path);
            // A file may hold one sprite, or several if it was sliced as a sheet.
            sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => s.name, NaturalComparer.Instance));
        }
        return sprites.ToArray();
    }

    static void EnsureSpriteImport(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            return;
        if (importer.textureType == TextureImporterType.Sprite)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
    }

    static AnimationClip WriteClip(string name, Sprite[] frames, bool loop)
    {
        string path = $"{ClipRoot}/{name}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.frameRate = FramesPerSecond;

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        // One key per frame, plus a final key holding the last frame so it gets its full duration.
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / FramesPerSecond, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / FramesPerSecond, value = frames[^1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip jump, AnimationClip scare)
    {
        // Rebuild the existing controller in place so anything already referencing it keeps working.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (controller.layers.Length == 0)
            controller.AddLayer("Base Layer");
        foreach (AnimatorControllerParameter p in controller.parameters)
            controller.RemoveParameter(p);
        ClearStateMachine(controller.layers[0].stateMachine);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Scared", AnimatorControllerParameterType.Bool);
        controller.parameters = controller.parameters
            .Select(p => { if (p.name == "Grounded") p.defaultBool = true; return p; })
            .ToArray();

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle", new Vector3(300, 0));
        AnimatorState walkState = sm.AddState("Walk", new Vector3(300, 120));
        AnimatorState jumpState = sm.AddState("Jump", new Vector3(550, 60));
        AnimatorState scareState = sm.AddState("Scare", new Vector3(50, 200));
        idleState.motion = idle;
        walkState.motion = walk;
        jumpState.motion = jump;
        scareState.motion = scare;
        sm.defaultState = idleState;

        // Idle <-> Walk
        Instant(idleState.AddTransition(walkState))
            .AddCondition(AnimatorConditionMode.Greater, WalkSpeedThreshold, "Speed");
        AnimatorStateTransition walkToIdle = Instant(walkState.AddTransition(idleState));
        walkToIdle.AddCondition(AnimatorConditionMode.Less, WalkSpeedThreshold, "Speed");

        // Leaving the ground -> Jump; landing -> Idle (which moves on to Walk if still moving).
        Instant(idleState.AddTransition(jumpState)).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
        Instant(walkState.AddTransition(jumpState)).AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
        Instant(jumpState.AddTransition(idleState)).AddCondition(AnimatorConditionMode.If, 0, "Grounded");

        // Scare overrides everything until Scared is cleared.
        AnimatorStateTransition toScare = Instant(sm.AddAnyStateTransition(scareState));
        toScare.canTransitionToSelf = false;
        toScare.AddCondition(AnimatorConditionMode.If, 0, "Scared");
        Instant(scareState.AddTransition(idleState)).AddCondition(AnimatorConditionMode.IfNot, 0, "Scared");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void ClearStateMachine(AnimatorStateMachine sm)
    {
        foreach (AnimatorStateTransition t in sm.anyStateTransitions)
            sm.RemoveAnyStateTransition(t);
        foreach (ChildAnimatorState s in sm.states)
            sm.RemoveState(s.state);
        foreach (ChildAnimatorStateMachine child in sm.stateMachines)
            sm.RemoveStateMachine(child.stateMachine);
    }

    static AnimatorStateTransition Instant(AnimatorStateTransition t)
    {
        t.hasExitTime = false;
        t.duration = 0f;
        return t;
    }

    static bool AssignToPlayer(AnimatorController controller, Sprite firstFrame)
    {
        GameObject player = GameObject.Find(PlayerObjectName);
        if (player == null)
            return false;

        Undo.RecordObject(player, "Set up player animations");
        if (!player.TryGetComponent(out Animator animator))
            animator = Undo.AddComponent<Animator>(player);
        if (!player.TryGetComponent(out PlayerMovement _))
            Undo.AddComponent<PlayerMovement>(player);
        if (!player.TryGetComponent(out PlayerAnimator _))
            Undo.AddComponent<PlayerAnimator>(player);

        Undo.RecordObject(animator, "Assign player controller");
        animator.runtimeAnimatorController = controller;

        var spriteRenderer = player.GetComponent<SpriteRenderer>();
        Undo.RecordObject(spriteRenderer, "Set player sprite");
        spriteRenderer.sprite = firstFrame;

        EditorSceneManager.MarkSceneDirty(player.scene);
        return true;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    /// <summary>Sorts "walk2" before "walk10".</summary>
    class NaturalComparer : IComparer<string>
    {
        public static readonly NaturalComparer Instance = new();
        public int Compare(string a, string b) => EditorUtility.NaturalCompare(a, b);
    }
}
