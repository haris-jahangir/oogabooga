using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tools > Oogabooga > Place Spot Lights: hangs spot lights (Lamps) under the platforms and
/// along the ceiling of the open scene, keeping hiding spots in shadow, and makes walls and
/// platforms cast shadows. The lamps go under a "Spot Lights" object so you can move, delete
/// or tweak them. Run it again for a fresh random layout (Ctrl+Z undoes it).
/// The lamps stay on in the editor; in Play mode SceneAtmosphere keeps them off until the switch.
/// </summary>
public static class WarehouseSpotLights
{
    const string RootName = "Spot Lights";

    [MenuItem("Tools/Oogabooga/Place Spot Lights")]
    static void Place()
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null)
            Undo.DestroyObjectImmediate(old);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Place spot lights");

        // Use the spacing, colour etc. set on SceneAtmosphere if there is one, with a new random layout.
        SceneAtmosphere atmosphere = Object.FindFirstObjectByType<SceneAtmosphere>();
        LampPlacer.Settings settings = atmosphere != null ? GetSettings(new SerializedObject(atmosphere)) : null;
        settings ??= new LampPlacer.Settings();
        settings.seed = Random.Range(1, 100000);

        int count = LampPlacer.Place(root.transform, settings).Count;

        int casters = 0;
        if (settings.castShadows)
            foreach (BoxCollider2D box in Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
                if (LampPlacer.IsSolid(box) && box.GetComponent<ShadowCaster2D>() == null)
                {
                    ShadowCaster2D caster = Undo.AddComponent<ShadowCaster2D>(box.gameObject);
                    caster.selfShadows = false;
                    casters++;
                }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Spot Lights] Placed {count} lamps and {casters} shadow casters. Save the scene to keep them.");
    }

    static LampPlacer.Settings GetSettings(SerializedObject so)
    {
        SerializedProperty p = so.FindProperty("lampSettings");
        return p?.boxedValue as LampPlacer.Settings;
    }
}
