using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Adds the game-feel effects to every scene as it loads, so nothing has to be wired by hand:
///   Main Camera  -> CameraFX (shake, zoom, screen effects) and TensionFX (warning shake, heartbeat)
///   Player       -> PlayerVFX (dust, landing, dash)
///   Fire         -> FireVFX (flames, embers, smoke, ignition)
///   LevelStarter -> SwitchVFX (sparks when the switch is used)
/// Anything you've already added yourself is left alone, so you can add a component in the
/// Inspector to change its settings.
/// </summary>
public static class VfxBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Ensure<CameraFX>(cam.gameObject);
            if (Object.FindFirstObjectByType<MonsterManager>() != null)
                Ensure<TensionFX>(cam.gameObject);
        }
        foreach (PlayerMovement p in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))
            Ensure<PlayerVFX>(p.gameObject);
        foreach (Fire f in Object.FindObjectsByType<Fire>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Ensure<FireVFX>(f.gameObject);
        foreach (LevelStarter s in Object.FindObjectsByType<LevelStarter>(FindObjectsSortMode.None))
            Ensure<SwitchVFX>(s.gameObject);
    }

    static void Ensure<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() == null)
            go.AddComponent<T>();
    }
}
