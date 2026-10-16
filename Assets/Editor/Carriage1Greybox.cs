using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the carriage 1 greybox from the level design blueprint into the open scene:
/// Tools > Oogabooga > Build Carriage 1 Greybox. Run it again to rebuild; Ctrl+Z undoes it.
///
/// Positions are in carriage units: x from the rear wall, y from the floor top.
/// 1 unit = player height. Pockets get a trigger + HidingSpot, racks are one-way platforms.
/// Interactables are coloured markers only until the interact system exists.
/// </summary>
public static class Carriage1Greybox
{
    const string RootName = "Carriage1_Greybox";
    const string SquareSpriteGuid = "311925a002f4447b3a28927169b83ea6"; // 2D package Square, same sprite the scene uses
    const string ScenePath = "Assets/Scenes/Carriage1_Greybox.unity";

    // Where the carriage sits in the world: rear wall x, and floor top y (= top of the existing Ground).
    static readonly Vector2 Origin = new(-8f, -3.58f);
    const float Length = 36f, Height = 7.6f;

    // Fire tuning from the GDD: ~10 minutes to cross the carriage.
    const float FireGrowSpeed = 0.06f;

    enum Kind { Solid, OneWay, Pocket, Interact, Window, Door, Sign }

    static readonly Color[] Colors =
    {
        new(0.55f, 0.58f, 0.62f),        // Solid
        new(0.45f, 0.50f, 0.58f),        // OneWay
        new(0.06f, 0.07f, 0.14f, 0.85f), // Pocket
        new(0.85f, 0.64f, 0.10f),        // Interact
        new(0.60f, 0.76f, 0.90f, 0.45f), // Window
        new(0.12f, 0.12f, 0.14f),        // Door
        Color.clear,                     // Sign (text only)
    };

    static readonly (string id, Kind kind, float x, float y, float w, float h, string label)[] Parts =
    {
        ("SW",  Kind.Interact, 1.1f, 1.0f, 0.5f, 0.6f, "Light switch"),
        ("MSG", Kind.Sign,     5.2f, 3.9f, 4.6f, 0.9f, "USE THE SHADOWS"),
        ("P1",  Kind.Pocket,   5.6f, 0f,   2f,   1.6f, "P1"),
        ("SeatA",         Kind.Solid, 9f,    0f, 2.4f, 1.2f, null),
        ("SeatA_Back",    Kind.Solid, 11.4f, 0f, 0.6f, 2.4f, null),
        ("P2",  Kind.Pocket,   12f,  0f,   1.4f, 1.4f, "P2"),
        ("W1",  Kind.Window,   12.5f, 3.4f, 3f,  2f,   "W1"),
        ("Suitcase",      Kind.Solid, 14.4f, 0f, 1.2f, 0.8f, null),
        ("SeatB",         Kind.Solid, 16.6f, 0f, 2.4f, 1.2f, null),
        ("SeatB_Back",    Kind.Solid, 19f,   0f, 0.6f, 2.4f, null),
        ("Rack",          Kind.OneWay, 18.2f, 3.6f, 9.4f, 0.25f, null),
        ("Clue_Digit2", Kind.Interact, 21.8f, 3.85f, 0.5f, 0.4f, "_ 3 _ _"),
        ("Rack_Suitcases", Kind.Solid, 24.2f, 3.85f, 1f, 1f, null),
        ("P4",  Kind.Pocket,   25.2f, 3.85f, 1.6f, 1.2f, "P4"),
        ("P3",  Kind.Pocket,   19.8f, 0f,   2.6f, 1.6f, "P3"),
        ("Clue_Digit3", Kind.Interact, 20.9f, 0.9f, 0.4f, 0.4f, "_ _ 9 _"),
        ("SuitcaseStack", Kind.Solid, 23.6f, 0f, 1.2f, 1.6f, null),
        ("P5",  Kind.Pocket,   24.8f, 0f,   1.2f, 1.4f, "P5"),
        ("W2",  Kind.Window,   26.5f, 3.4f, 3f,  2f,   "W2"),
        ("Clue_Digit4", Kind.Interact, 27.8f, 4.2f, 0.4f, 0.4f, "_ _ _ 1"),
        ("SeatC",         Kind.Solid, 27.6f, 0f, 2.4f, 1.2f, null),
        ("SeatC_Back",    Kind.Solid, 30f,   0f, 0.6f, 2.4f, null),
        ("P6",  Kind.Pocket,   30.6f, 0f,   1.2f, 1.4f, "P6"),
        ("Bag1", Kind.Interact, 32.2f, 0f, 0.5f, 0.5f, null),
        ("Bag2", Kind.Interact, 32.9f, 0f, 0.5f, 0.5f, null),
        ("Bag3", Kind.Interact, 33.6f, 0f, 0.5f, 0.5f, "7 _ _ _"),
        ("Bag4", Kind.Interact, 34.3f, 0f, 0.5f, 0.5f, null),
        ("Lock", Kind.Interact, 35.1f, 1.2f, 0.4f, 0.5f, "LOCK"),
        ("Door", Kind.Door,     35.6f, 0f,   0.4f, 3.2f, null),
    };

    [MenuItem("Tools/Oogabooga/Build Carriage 1 Greybox")]
    static void Build()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(SquareSpriteGuid));
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Carriage 1 Greybox");
        root.transform.position = Origin;

        // Shell: the player can't leave the carriage.
        Box(root, square, "Floor", Kind.Solid, -0.5f, -1f, Length + 1f, 1f);
        Box(root, square, "Ceiling", Kind.Solid, -0.5f, Height, Length + 1f, 0.5f);
        Box(root, square, "RearWall", Kind.Solid, -0.5f, 0f, 0.5f, Height);
        Box(root, square, "FrontWall", Kind.Solid, Length, 0f, 0.5f, Height);

        foreach (var p in Parts)
        {
            if (p.kind != Kind.Sign)
                Box(root, square, p.id, p.kind, p.x, p.y, p.w, p.h);
            if (p.label != null)
                Label(root, p.label, p.kind == Kind.Sign
                    ? new Vector2(p.x + p.w / 2f, p.y)
                    : new Vector2(p.x + p.w / 2f, p.y + p.h + 0.05f), p.kind == Kind.Sign ? 0.12f : 0.06f);
        }

        PlacePlayerAndFire();
        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
    }

    /// <summary>For the command line: SampleScene + greybox, saved as its own scene.</summary>
    public static void BuildScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        GameObject testSpot = GameObject.Find("hiding spot"); // the old test spot would sit inside the carriage
        if (testSpot != null)
            testSpot.SetActive(false);
        Build();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
    }

    static void Box(GameObject root, Sprite sprite, string name, Kind kind, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(x + w / 2f, y + h / 2f, 0f);
        go.transform.localScale = new Vector3(w, h, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Colors[(int)kind];
        sr.sortingOrder = kind switch { Kind.Window => -10, Kind.Pocket => -5, _ => 0 };

        switch (kind)
        {
            case Kind.Solid:
            case Kind.Door:
                go.AddComponent<BoxCollider2D>();
                break;
            case Kind.OneWay:
                go.AddComponent<BoxCollider2D>().usedByEffector = true;
                go.AddComponent<PlatformEffector2D>();
                break;
            case Kind.Pocket:
                go.AddComponent<BoxCollider2D>().isTrigger = true;
                go.AddComponent<HidingSpot>();
                break;
        }
    }

    static void Label(GameObject root, string text, Vector2 at, float size)
    {
        var go = new GameObject("Label " + text);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = at;
        var tm = go.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        tm.text = text;
        tm.fontSize = 48;
        tm.characterSize = size;
        tm.anchor = TextAnchor.LowerCenter;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 10;
    }

    static void PlacePlayerAndFire()
    {
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            Undo.RecordObject(player.transform, "Place player");
            player.transform.position = Origin + new Vector2(4.5f, 0.5f);
        }

        Fire fire = Object.FindFirstObjectByType<Fire>();
        if (fire == null)
            return;
        // Start the fire just behind the rear wall and slow it to the GDD pace.
        Bounds b = fire.GetComponent<SpriteRenderer>().bounds;
        Undo.RecordObject(fire.transform, "Place fire");
        fire.transform.position += new Vector3(Origin.x - 2f - b.min.x, Origin.y - b.min.y, 0f);
        var so = new SerializedObject(fire);
        so.FindProperty("growSpeed").floatValue = FireGrowSpeed;
        so.FindProperty("acceleration").floatValue = 0f;
        so.ApplyModifiedProperties();
    }
}
