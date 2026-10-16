using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the player's animation clips and Animator Controller from frame images.
///
/// 1. Drop frames into Assets/Art/Player/Walk, Jump, Fear (and optionally Idle).
///    Frames play in file-name order, e.g. walk_1.png, walk_2.png, ...
/// 2. Run Tools > Player > Build Animations.
///
/// Clips are written to Assets/Animations/Player and the controller is assigned
/// to the "player" object in the open scene (adding PlayerMovement/PlayerAnimator if missing).
/// Re-run any time you change the images.
/// </summary>
public static class PlayerAnimationBuilder
{
    const string ArtRoot = "Assets/Art/Player";
    const string OutputRoot = "Assets/Animations/Player";
    const string ControllerPath = OutputRoot + "/Player.controller";
    const string PlayerObjectName = "player";
    const float FramesPerSecond = 8f;
    const float WalkSpeedThreshold = 0.1f;

    [MenuItem("Tools/Player/Create Art Folders")]
    public static void CreateArtFolders()
    {
        foreach (string name in new[] { "Idle", "Walk", "Jump", "Fear" })
            EnsureFolder($"{ArtRoot}/{name}");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(ArtRoot);
    }

    [MenuItem("Tools/Player/Build Animations")]
    public static void Build()
    {
        CreateArtFolders();
        EnsureFolder(OutputRoot);

        Sprite[] walk = LoadFrames("Walk");
        Sprite[] jump = LoadFrames("Jump");
        Sprite[] fear = LoadFrames("Fear");
        Sprite[] idle = LoadFrames("Idle");

        var missing = new List<string>();
        if (walk.Length == 0) missing.Add("Walk");
        if (jump.Length == 0) missing.Add("Jump");
        if (fear.Length == 0) missing.Add("Fear");
        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog("Player Animations",
                $"No images found in: {string.Join(", ", missing.Select(m => $"{ArtRoot}/{m}"))}\n\n" +
                "Add the frame images and run Build Animations again.", "OK");
            return;
        }

        // No dedicated idle frames: stand still on the first walk frame.
        if (idle.Length == 0)
            idle = new[] { walk[0] };

        AnimationClip idleClip = WriteClip("Idle", idle, loop: true);
        AnimationClip walkClip = WriteClip("Walk", walk, loop: true);
        AnimationClip jumpClip = WriteClip("Jump", jump, loop: false);
        AnimationClip fearClip = WriteClip("Fear", fear, loop: true);

        AnimatorController controller = BuildController(idleClip, walkClip, jumpClip, fearClip);
        bool assigned = AssignToPlayer(controller, idle[0]);

        AssetDatabase.SaveAssets();
        Selection.activeObject = controller;
        Debug.Log($"[PlayerAnimationBuilder] Built {ControllerPath} " +
                  $"(Idle {idle.Length}, Walk {walk.Length}, Jump {jump.Length}, Fear {fear.Length} frames)." +
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
        string path = $"{OutputRoot}/{name}.anim";
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

    static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip jump, AnimationClip fear)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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
        AnimatorState fearState = sm.AddState("Fear", new Vector3(50, 200));
        idleState.motion = idle;
        walkState.motion = walk;
        jumpState.motion = jump;
        fearState.motion = fear;
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

        // Fear overrides everything until Scared is cleared.
        AnimatorStateTransition toFear = Instant(sm.AddAnyStateTransition(fearState));
        toFear.canTransitionToSelf = false;
        toFear.AddCondition(AnimatorConditionMode.If, 0, "Scared");
        Instant(fearState.AddTransition(idleState)).AddCondition(AnimatorConditionMode.IfNot, 0, "Scared");

        EditorUtility.SetDirty(controller);
        return controller;
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
