using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Oogabooga > Story: builds the opening prefab and the ending scene under Assets/Story. Wire Story Into Game then puts the
/// story and the character art into the real levels and links Scene1 -> Scene2 -> Ending.
/// </summary>
public static class StoryBuilder
{
    const string Art = "Assets/Story/Art/";
    const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string OpeningPrefab = "Assets/Story/Prefabs/Opening.prefab";
    // The player's drawing is 1.4 units tall (Player frames at 137 PPU), the shadow 1.5 times that.
    const float PlayerHeight = 1.4f, ShadowHeight = 2.1f;
    const string StoryFont = "Assets/Story/Resources/Fonts/SpecialElite-Regular.ttf";
    // Free stand-ins for Chiller and Stencil, which only Windows has: the menu looks the same everywhere.
    const string TitleFont = "Assets/Story/Resources/Fonts/Creepster-Regular.ttf", MenuFont = "Assets/Story/Resources/Fonts/StardosStencil-Bold.ttf";

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
        go.AddComponent<WakeUpIntro>().font = AssetDatabase.LoadAssetAtPath<Font>(StoryFont);
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
        director.font = AssetDatabase.LoadAssetAtPath<Font>(StoryFont);
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
        director.lightMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/music/ending_light_piano.ogg");
        director.darkMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/music/ending_dark_piano.ogg");
        director.ambience = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/music/ending_ambience.ogg");

        EditorSceneManager.SaveScene(scene, "Assets/Story/Scenes/Ending.unity");
        Debug.Log("STORY ending scene saved");
    }

    /// <summary>
    /// Puts the new art on the levels' placeholder squares: the player's
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
        foreach (var (field, file) in new[] { ("keySound", "keypad key"), ("wrongSound", "keypad wrong"), ("unlockSound", "keypad unlock") })
            SetAll<KeypadLock>(room1, field, p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/sfx/{file}.wav"));
        // Playtest tuning: slower, more forgiving movement; platforms you can jump up through.
        SetAll<PlayerMovement>(room1, "moveSpeed", p => p.floatValue = 5f);
        SetAll<PlayerMovement>(room1, "coyoteTime", p => p.floatValue = 0.15f);
        MakeThinPlatformsOneWay(room1);
        PlaceWallText(room1);
        EditorSceneManager.SaveScene(room1);

        // Level 2: the art, and the roof exit leads to the ending.
        var level2 = EditorSceneManager.OpenScene("Assets/Scenes/Scene2.unity");
        DressCharacters(level2);
        SetAll<RoofExit>(level2, "nextScene", p => p.stringValue = "Ending");
        // Playtest tuning: slower movement, more time before and while the fire rises.
        SetAll<PlayerMovement>(level2, "moveSpeed", p => p.floatValue = 6f);
        SetAll<PlayerMovement>(level2, "coyoteTime", p => p.floatValue = 0.15f);
        MakeThinPlatformsOneWay(level2);   // the low step left over from Level 1's layout
        SetAll<Fire>(level2, "growSpeed", p => p.floatValue = 0.85f);
        SetAll<Fire>(level2, "startDelay", p => p.floatValue = 5f);
        EditorSceneManager.SaveScene(level2);

        // The menu's fonts travel with the game.
        if (System.IO.File.Exists("Assets/Scenes/MainMenu.unity"))
        {
            var menu = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
            SetAll<MainMenu>(menu, "titleFont", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(TitleFont));
            SetAll<MainMenu>(menu, "menuFont", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(MenuFont));
            EditorSceneManager.SaveScene(menu);
        }

        // The menu (if there is one) first: the ending goes back to build index 0.
        EditorBuildSettings.scenes = new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Scene1.unity", "Assets/Scenes/Scene2.unity", "Assets/Story/Scenes/Ending.unity" }
            .Where(path => System.IO.File.Exists(path))
            .Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        Debug.Log("STORY wired: " + string.Join(" -> ", EditorBuildSettings.scenes.Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))));
    }

    // Thin platforms (scaffolding, walkways) become one-way: jump up through them, land on top.
    // Floors, walls and crates stay solid.
    static void MakeThinPlatformsOneWay(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (BoxCollider2D box in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider2D>(true)))
        {
            Bounds b = box.bounds;
            if (box.isTrigger || box.GetComponentInParent<PlayerMovement>() != null || b.size.y > 0.6f || b.size.x < 1.5f)
                continue;
            box.usedByEffector = true;
            if (!box.TryGetComponent(out PlatformEffector2D effector))
                effector = box.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
            Debug.Log("STORY one-way platform: " + box.name);
        }
    }

    static void PlaceWallText(UnityEngine.SceneManagement.Scene scene)
    {
        const string name = "Wall Text: Stay In The Light";
        GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name) ?? new GameObject(name);
        go.transform.position = WallTextPosition;
        if (!go.TryGetComponent(out SpriteRenderer sr))
            sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "WallText_StayInTheLight.png");
        sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial); // readable in the dark, like old paint
        sr.color = new Color(1f, 1f, 1f, 0.9f);
        sr.sortingOrder = -10; // painted on the back wall, behind everything
    }

    static readonly Vector3 WallTextPosition = new Vector3(-0.4f, 1.1f, 0f);

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
        WireGame();
    }
}
