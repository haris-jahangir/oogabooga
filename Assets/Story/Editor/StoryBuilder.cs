using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Oogabooga > Story: builds the opening prefab, the ending scene, and a test copy of
/// room 1 with the opening in it, all under Assets/Story. Wire Story Into Game then puts the
/// story and the character art into the real levels and links Scene1 -> Scene2 -> Ending.
/// </summary>
public static class StoryBuilder
{
    const string Art = "Assets/Story/Art/";
    const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string OpeningPrefab = "Assets/Story/Prefabs/Opening.prefab";
    // The player's drawing is 1.4 units tall (Player frames at 137 PPU), the shadow 1.5 times that.
    const float PlayerHeight = 1.4f, ShadowHeight = 2.1f;

    /// <summary>Frames 0..3 of one animation, e.g. Frames("Player/Player_Walk").</summary>
    static Sprite[] Frames(string name) =>
        Enumerable.Range(0, 4).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{Art}{name}_{i}.png")).ToArray();

    static Flipbook AddFlipbook(SpriteRenderer sr, Sprite[] frames, float fps)
    {
        sr.sprite = frames[0];
        if (!sr.TryGetComponent(out Flipbook book))
            book = sr.gameObject.AddComponent<Flipbook>();
        book.frames = frames;
        book.framesPerSecond = fps;
        return book;
    }

    [MenuItem("Tools/Oogabooga/Story/Build Opening Prefab")]
    public static void BuildOpening()
    {
        var go = new GameObject("Opening");
        go.AddComponent<WakeUpIntro>();
        var spark = go.AddComponent<SwitchSpark>();
        spark.usedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Switch_On.png");
        PrefabUtility.SaveAsPrefabAsset(go, OpeningPrefab);
        Object.DestroyImmediate(go);
        Debug.Log("STORY opening prefab saved: " + OpeningPrefab);
    }

    [MenuItem("Tools/Oogabooga/Story/Build Ending Scene")]
    public static void BuildEnding()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return; // this replaces the open scene: don't lose unsaved work
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.AddComponent<AudioListener>();

        const float floorY = 5f - 840f / 108f; // the dock's top edge in the backdrop
        SpriteRenderer Sprite(string name, string file, Vector3 pos, int order)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            var sr = g.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file + ".png");
            sr.sharedMaterial = unlit;
            sr.sortingOrder = order;
            if (sr.sprite == null)
                Debug.LogError("STORY missing sprite " + file);
            return sr;
        }

        var director = new GameObject("Ending Director").AddComponent<EndingDirector>();
        director.docks = Sprite("Docks", "Ending_Docks", Vector3.zero, 0);
        director.shadowView = Sprite("Shadow's View", "Ending_ShadowView", Vector3.zero, 1);
        director.hospital = Sprite("Hospital", "Ending_Hospital", Vector3.zero, 2);
        // Both sprites are pivoted at the body's centre.
        director.shadow = Sprite("Shadow", "Shadow/Shadow_0", new Vector3(director.shadowX, floorY + ShadowHeight / 2f, 0f), 4);
        AddFlipbook(director.shadow, Frames("Shadow/Shadow"), 7f);
        director.player = Sprite("Player", "Player/Player_Idle_0", new Vector3(director.doorX, floorY + PlayerHeight / 2f, 0f), 5);
        director.playerIdle = Frames("Player/Player_Idle");
        director.playerWalk = Frames("Player/Player_Walk");
        AddFlipbook(director.player, director.playerIdle, 4f);
        director.shadowAppears = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/eeerie sound.mp3");
        director.darkSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/monster sound.mp3");
        director.fireLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/fire sfx.mp3");
        director.music = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/music/horror musiic.mp3");

        EditorSceneManager.SaveScene(scene, "Assets/Story/Scenes/Ending.unity");
        Debug.Log("STORY ending scene saved");
    }

    [MenuItem("Tools/Oogabooga/Story/Build Opening Test Scene")]
    public static void BuildOpeningTest()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return; // this replaces the open scene: don't lose unsaved work
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPrefab) == null)
            BuildOpening();
        // A copy of room 1 (Scene1) with the opening dropped in, so the real scene stays untouched.
        AssetDatabase.DeleteAsset("Assets/Story/Scenes/OpeningTest.unity");
        AssetDatabase.CopyAsset("Assets/Scenes/Scene1.unity", "Assets/Story/Scenes/OpeningTest.unity");
        var scene = EditorSceneManager.OpenScene("Assets/Story/Scenes/OpeningTest.unity");
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPrefab), scene);
        DressCharacters(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("STORY opening test scene saved");
    }

    /// <summary>
    /// Puts the new art on room 1's placeholder squares (in the test copy only): the player's
    /// frames into PlayerAnimator, the shadow's smoke onto the monster.
    /// </summary>
    static void DressCharacters(UnityEngine.SceneManagement.Scene scene)
    {
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

        var animator = all.Select(t => t.GetComponent<PlayerAnimator>()).FirstOrDefault(a => a != null);
        if (animator != null)
        {
            var so = new SerializedObject(animator);
            foreach (var (field, name) in new[] { ("idleFrames", "Idle"), ("walkFrames", "Walk"), ("jumpFrames", "Jump"), ("scareFrames", "Scare") })
            {
                var prop = so.FindProperty(field);
                Sprite[] frames = Frames("Player/Player_" + name);
                prop.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                    prop.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            animator.GetComponent<SpriteRenderer>().sprite = Frames("Player/Player_Idle")[0];
            // A collider as tall as the drawing and about as wide as the body, so the player doesn't
            // catch on edges before touching them. Raised so the feet stay where they were.
            if (animator.TryGetComponent(out BoxCollider2D box))
            {
                animator.transform.position += Vector3.up * (PlayerHeight - box.size.y * animator.transform.localScale.y) / 2f;
                box.size = new Vector2(0.55f, PlayerHeight);
                box.offset = Vector2.zero;
            }
        }
        else
            Debug.LogWarning("STORY no PlayerAnimator found, player art not set");

        var monster = all.FirstOrDefault(t => t.name == "monster")?.GetComponent<SpriteRenderer>();
        if (monster != null)
        {
            monster.color = Color.white;
            monster.transform.localScale = Vector3.one;
            AddFlipbook(monster, Frames("Shadow/Shadow"), 7f);
        }
        else
            Debug.LogWarning("STORY no 'monster' sprite found, shadow art not set");
    }

    [MenuItem("Tools/Oogabooga/Story/Set Game Icon")]
    public static void SetIcon()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Icon/GameIcon.png");
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        AssetDatabase.SaveAssets();
        Debug.Log("STORY game icon set: " + (icon != null));
    }

    [MenuItem("Tools/Oogabooga/Story/Wire Story Into Game")]
    public static void WireGame()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPrefab) == null)
            BuildOpening();

        // Room 1: the wake-up opening, the art, and the keypad opens the way to level 2.
        var room1 = EditorSceneManager.OpenScene("Assets/Scenes/Scene1.unity");
        if (Object.FindFirstObjectByType<WakeUpIntro>() == null)
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPrefab), room1);
        DressCharacters(room1);
        SetAll<KeypadLock>(room1, "nextScene", p => p.stringValue = "Scene2");
        SetAll<PlayerMovement>(room1, "moveSpeed", p => p.floatValue = 10f); // level 2's speed
        EditorSceneManager.SaveScene(room1);

        // Level 2: the art, and the roof exit leads to the ending.
        var level2 = EditorSceneManager.OpenScene("Assets/Scenes/Scene2.unity");
        DressCharacters(level2);
        SetAll<RoofExit>(level2, "nextScene", p => p.stringValue = "Ending");
        EditorSceneManager.SaveScene(level2);

        // Ending loads build index 0 to play again, so room 1 goes first.
        EditorBuildSettings.scenes = new[] { "Assets/Scenes/Scene1.unity", "Assets/Scenes/Scene2.unity", "Assets/Story/Scenes/Ending.unity" }
            .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        Debug.Log("STORY wired: Scene1 -> Scene2 -> Ending");
    }

    static void SetAll<T>(UnityEngine.SceneManagement.Scene scene, string field, System.Action<SerializedProperty> set) where T : Component
    {
        foreach (T c in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)))
        {
            var so = new SerializedObject(c);
            set(so.FindProperty(field));
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Command line: build everything.</summary>
    public static void BuildAll()
    {
        BuildOpening();
        BuildEnding();
        BuildOpeningTest();
        WireGame();
    }
}
