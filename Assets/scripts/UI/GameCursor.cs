using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The game's custom mouse cursor (Assets/Resources/UI/Cursor.png). It shows in menus and other
/// non-gameplay scenes, and is hidden during gameplay (Scene1 to Scene4) so it doesn't sit in the
/// middle of the screen. Runs on its own; no setup needed.
/// </summary>
public static class GameCursor
{
    /// <summary>Scenes where the cursor is hidden.</summary>
    static readonly string[] GameplayScenes = { "Scene1", "Scene2", "Scene3", "Scene4" };

    // The tip of the arrow, in pixels from the image's top-left corner.
    static readonly Vector2 Hotspot = new(3f, 3f);

    static Texture2D image;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        image = Resources.Load<Texture2D>("UI/Cursor");
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene.name);

    /// <summary>Shows the custom cursor, or hides it in gameplay scenes.</summary>
    public static void Apply(string sceneName)
    {
        bool gameplay = System.Array.IndexOf(GameplayScenes, sceneName) >= 0;
        Cursor.visible = !gameplay;
        Cursor.lockState = CursorLockMode.None;
        if (!gameplay && image != null)
            Cursor.SetCursor(image, Hotspot, CursorMode.Auto);
    }
}
