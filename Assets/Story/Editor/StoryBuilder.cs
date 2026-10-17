using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Oogabooga > Story: builds the opening prefab, the ending scene, and a test copy of
/// room 1 with the opening in it. Everything is written under Assets/Story; no existing scene
/// or script is changed.
/// </summary>
public static class StoryBuilder
{
    const string Art = "Assets/Story/Art/";
    const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    const string OpeningPrefab = "Assets/Story/Prefabs/Opening.prefab";

    [MenuItem("Tools/Oogabooga/Story/Build Opening Prefab")]
    public static void BuildOpening()
    {
        var go = new GameObject("Opening");
        go.AddComponent<WakeUpIntro>();
        var spark = go.AddComponent<SwitchSpark>();
        spark.sparkMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);
        PrefabUtility.SaveAsPrefabAsset(go, OpeningPrefab);
        Object.DestroyImmediate(go);
        Debug.Log("STORY opening prefab saved: " + OpeningPrefab);
    }

    [MenuItem("Tools/Oogabooga/Story/Build Ending Scene")]
    public static void BuildEnding()
    {
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
        director.shadow = Sprite("Shadow", "Story_Shadow", new Vector3(director.shadowX, floorY, 0f), 4);
        director.player = Sprite("Player", "Story_Player", new Vector3(director.doorX, floorY, 0f), 5);
        director.shadowAppears = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/eeerie sound.mp3");
        director.darkSting = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/sfx/monster sound.mp3");

        EditorSceneManager.SaveScene(scene, "Assets/Story/Scenes/Ending.unity");
        Debug.Log("STORY ending scene saved");
    }

    [MenuItem("Tools/Oogabooga/Story/Build Opening Test Scene")]
    public static void BuildOpeningTest()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPrefab) == null)
            BuildOpening();
        // A copy of room 1 with the opening dropped in, so the real scene stays untouched.
        AssetDatabase.CopyAsset("Assets/Scenes/Scene0.unity", "Assets/Story/Scenes/OpeningTest.unity");
        var scene = EditorSceneManager.OpenScene("Assets/Story/Scenes/OpeningTest.unity");
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(OpeningPrefab), scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("STORY opening test scene saved");
    }

    /// <summary>Command line: build everything.</summary>
    public static void BuildAll()
    {
        BuildOpening();
        BuildEnding();
        BuildOpeningTest();
    }
}
