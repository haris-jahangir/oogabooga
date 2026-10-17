using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Oogabooga > Build Scene 2 Climb: turns the open scene's shaft into the escape climb.
///   - Fire rises from the floor (Grow Direction = Up) and starts on its own shortly after load.
///   - One-way ledges zig-zag left, middle, right up to the roof; jump up through them.
///     Two of the middle ledges slide side to side.
///     Some gaps are too wide to jump: those need a dash (Shift) mid-jump. The ledge you dash
///     to is a warmer colour so it reads as "dash here".
///   - Shadow pockets on every other ledge against the wall, to hide from the shadow creature.
///   - The roof at the top is the way out (RoofExit loads the next scene).
///   - Spot lights are re-hung around the new ledges, keeping the shadows dark.
///   - The camera follows the player up the shaft to the roof.
///   - The building shakes now and then and rubble rains from the roof (CollapseFX).
/// Everything it adds goes under "Climb". Run it again to rebuild; Ctrl+Z undoes it.
/// </summary>
public static class Scene2ClimbBuilder
{
    const string RootName = "Climb";
    const string SquareSpriteGuid = "311925a002f4447b3a28927169b83ea6"; // 2D package Square, same sprite the scene uses

    // Layout, in units. The player jumps 2.5 high and runs 6 per second.
    const float RowGap = 2f;            // height between ledges
    const float FirstRowAboveFloor = 4f;
    const int Rows = 10;
    const float LedgeThickness = 0.3f;
    const float SideLedgeWidth = 3f;
    const float MiddleLedgeWidth = 3.2f;
    const float WallThickness = 0.5f;
    // A jump alone covers about 5.5 units sideways while climbing one row; a dash adds ~3.
    const float DashGap = 7f;
    const float DashLedgeWidth = 2.5f;

    // Which ledge each row gets: L(eft), C(entre), R(ight), or D(ash): a small ledge DashGap
    // away from the previous one, on its far side. Shadow pockets on the rows marked true.
    static readonly (char pos, bool shadow)[] Pattern =
    {
        ('C', false), ('L', true), ('D', false), ('R', true), ('C', false),
        ('L', true), ('C', false), ('R', true), ('D', false), ('L', true),
    };
    static readonly Color DashLedgeColor = new(0.42f, 0.3f, 0.16f, 1f);
    // Rows whose ledge slides side to side (MovingPlatform), and how far: toward the next ledge.
    static readonly (int row, float travel)[] MovingRows = { (4, -3f), (6, 3f) };

    // Fire pace: about two minutes to reach the roof.
    const float FireGrowSpeed = 0.12f, FireAcceleration = 0.0015f, FireStartDelay = 4f;

    static readonly Color ShadowColor = new(0.03f, 0.03f, 0.07f, 0.72f);

    [MenuItem("Tools/Oogabooga/Build Scene 2 Climb")]
    static void Build()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        // Measure the shaft from what's already in the scene.
        Bounds floor = BoundsOf("Ground", new Bounds(new Vector3(0.12f, -5.58f), new Vector3(16.43f, 4f)));
        Bounds leftWall = BoundsOf("Wall Left", new Bounds(new Vector3(-7.91f, 2.76f), new Vector3(0.5f, 21f)));
        float floorTop = floor.max.y;
        float left = leftWall.max.x;                                   // inner face of the left wall
        float right = floor.max.x - WallThickness;                     // inner face of the new right wall
        float mid = (left + right) / 2f;
        float roofY = floorTop + FirstRowAboveFloor + RowGap * Rows;   // top surface of the roof

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(SquareSpriteGuid));
        Color solidColor = new(0.22f, 0.22f, 0.22f, 1f);
        Material solidMaterial = null;
        GameObject ground = GameObject.Find("Ground");
        if (ground != null && ground.TryGetComponent(out SpriteRenderer groundSprite))
        {
            sprite = groundSprite.sprite != null ? groundSprite.sprite : sprite;
            solidColor = groundSprite.color;
            solidMaterial = groundSprite.sharedMaterial;
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Scene 2 Climb");

        GameObject Box(string name, float x0, float y0, float x1, float y1, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f);
            go.transform.localScale = new Vector3(x1 - x0, y1 - y0, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (solidMaterial != null)
                sr.sharedMaterial = solidMaterial;
            return go;
        }

        GameObject Solid(string name, float x0, float y0, float x1, float y1)
        {
            GameObject go = Box(name, x0, y0, x1, y1, solidColor, 0);
            go.AddComponent<BoxCollider2D>();
            return go;
        }

        GameObject Ledge(string name, float x0, float x1, float top, Color? color = null)
        {
            GameObject go = Box(name, x0, top - LedgeThickness, x1, top, color ?? solidColor, 0);
            go.AddComponent<BoxCollider2D>().usedByEffector = true;   // jump up through, land on top
            go.AddComponent<PlatformEffector2D>();
            return go;
        }

        // Walls all the way up (the scene's own walls stop short).
        Solid("Wall Right", right, floorTop - 1f, right + WallThickness, roofY + 4f);
        Solid("Wall Left Upper", left - WallThickness, leftWall.max.y - 0.5f, left, roofY + 4f);

        // The zig-zag of ledges.
        float prevX0 = mid - MiddleLedgeWidth / 2f, prevX1 = mid + MiddleLedgeWidth / 2f;
        for (int i = 0; i < Rows; i++)
        {
            float top = floorTop + FirstRowAboveFloor + RowGap * i;
            (char pos, bool shadow) = Pattern[i % Pattern.Length];
            bool prevOnLeft = (prevX0 + prevX1) / 2f < mid;
            (float x0, float x1) = pos switch
            {
                'L' => (left, left + SideLedgeWidth),
                'R' => (right - SideLedgeWidth, right),
                'D' => prevOnLeft
                    ? (prevX1 + DashGap, prevX1 + DashGap + DashLedgeWidth)
                    : (prevX0 - DashGap - DashLedgeWidth, prevX0 - DashGap),
                _ => (mid - MiddleLedgeWidth / 2f, mid + MiddleLedgeWidth / 2f),
            };
            x0 = Mathf.Max(x0, left);
            x1 = Mathf.Min(x1, right);
            GameObject ledge = Ledge($"Ledge {i + 1} ({pos})", x0, x1, top, pos == 'D' ? DashLedgeColor : null);
            foreach ((int row, float travel) in MovingRows)
                if (row == i)
                {
                    var so = new SerializedObject(ledge.AddComponent<MovingPlatform>());
                    so.FindProperty("travel").vector2Value = new Vector2(travel, 0f);
                    so.FindProperty("phase").floatValue = i * 0.37f % 1f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            prevX0 = x0;
            prevX1 = x1;

            if (shadow)
            {
                // A shadow pocket tucked against the wall on this ledge.
                float sx0 = pos == 'R' ? right - 2.2f : left, sx1 = pos == 'R' ? right : left + 2.2f;
                GameObject spot = Box($"Shadow {i + 1}", sx0, top, sx1, top + 1.6f, ShadowColor, -1);
                spot.AddComponent<BoxCollider2D>().isTrigger = true;
                spot.AddComponent<HidingSpot>();
            }
        }

        // The roof. If the scene has its own exit (like a gate), that's the way out; otherwise
        // jumping up through the roof is.
        Ledge("Roof", left, right, roofY);
        bool hasOwnExit = false;
        foreach (RoofExit e in Object.FindObjectsByType<RoofExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            hasOwnExit |= !e.transform.IsChildOf(root.transform);
        GameObject exit = new("Roof Exit");
        exit.transform.SetParent(root.transform, false);
        exit.transform.position = new Vector3(mid, roofY + 1.25f, 0f);
        exit.transform.localScale = new Vector3(right - left, 2.5f, 1f);
        exit.AddComponent<BoxCollider2D>().isTrigger = true;
        exit.AddComponent<RoofExit>();
        exit.SetActive(!hasOwnExit);

        // The building is coming down: tremors, screen shake, rubble from the roof.
        root.AddComponent<CollapseFX>();

        SetUpFire(left, right, floorTop);
        SetUpCamera(mid, roofY);
        SetUpStarter();
        WarehouseSpotLights.Place();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Climb] Built {Rows} ledges up to the roof at y={roofY:0.0}. Save the scene to keep it.");
    }

    // Rising fire across the whole floor, starting just under the floor surface.
    static void SetUpFire(float left, float right, float floorTop)
    {
        Fire fire = Object.FindFirstObjectByType<Fire>();
        if (fire == null)
        {
            Debug.LogWarning("[Climb] No Fire in the scene; add one and run this again.");
            return;
        }
        var sr = fire.GetComponent<SpriteRenderer>();
        Undo.RecordObject(sr, "Set up rising fire");
        sr.drawMode = SpriteDrawMode.Simple; // Fire switches to Tiled itself in Play mode
        Undo.RecordObject(fire.transform, "Set up rising fire");
        fire.transform.position = new Vector3((left + right) / 2f, floorTop - 0.55f, 0f);
        fire.transform.localScale = new Vector3(right - left + 0.2f, 0.5f, 1f);

        var so = new SerializedObject(fire);
        so.FindProperty("growDirection").enumValueIndex = (int)Fire.Direction.Up;
        so.FindProperty("growSpeed").floatValue = FireGrowSpeed;
        so.FindProperty("acceleration").floatValue = FireAcceleration;
        so.FindProperty("startDelay").floatValue = FireStartDelay;
        so.FindProperty("maxWidth").floatValue = 0f;
        so.ApplyModifiedProperties();
    }

    // Follow the player up the shaft: centred sideways (the shaft fits on screen), from the
    // floor up to just above the roof so the exit is in view.
    static void SetUpCamera(float mid, float roofY)
    {
        CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>();
        if (follow == null)
            return;
        var so = new SerializedObject(follow);
        so.FindProperty("followY").boolValue = true;
        so.FindProperty("useLimits").boolValue = true;
        Vector2 min = so.FindProperty("minPosition").vector2Value;
        so.FindProperty("minPosition").vector2Value = new Vector2(mid, min.y);
        so.FindProperty("maxPosition").vector2Value = new Vector2(mid, roofY + 2f);
        so.ApplyModifiedProperties();
    }

    // No switch in this level: everything starts on its own, and the fog lifts.
    static void SetUpStarter()
    {
        LevelStarter starter = Object.FindFirstObjectByType<LevelStarter>();
        if (starter == null)
        {
            Debug.LogWarning("[Climb] No LevelStarter in the scene: the fire and monster will start straight away.");
            return;
        }
        var so = new SerializedObject(starter);
        so.FindProperty("startAutomatically").boolValue = true;
        SerializedProperty fog = so.FindProperty("fog");
        GameObject fogObject = GameObject.Find("fog");
        if (fog.arraySize == 0 && fogObject != null && fogObject.TryGetComponent(out SpriteRenderer fogSprite))
        {
            fog.arraySize = 1;
            fog.GetArrayElementAtIndex(0).objectReferenceValue = fogSprite;
        }
        so.ApplyModifiedProperties();
    }

    static Bounds BoundsOf(string name, Bounds fallback)
    {
        GameObject go = GameObject.Find(name);
        return go != null && go.TryGetComponent(out Renderer r) ? r.bounds : fallback;
    }
}
