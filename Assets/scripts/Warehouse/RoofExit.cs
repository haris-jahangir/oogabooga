using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The way out of a level (the exit gate at the top of the climb). When the player walks into
/// this trigger the monster and the fire stop, a message shows, and the next scene loads.
/// Put it on an object with a Collider2D set to Is Trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RoofExit : MonoBehaviour
{
    [Tooltip("Scene to load. Leave empty for the next scene in Build Profiles.")]
    [SerializeField] string nextScene = "";
    [SerializeField] float loadDelay = 2f;
    [SerializeField] string message = "You escaped!";
    [SerializeField] AudioClip reachedSound;

    static GUIStyle style;
    bool reached;

    void Reset() => GetComponent<Collider2D>().isTrigger = true;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (reached || other.GetComponentInParent<PlayerMovement>() == null)
            return;
        reached = true;

        foreach (MonsterManager m in FindObjectsByType<MonsterManager>(FindObjectsSortMode.None))
            m.StopHunting();
        foreach (Fire f in FindObjectsByType<Fire>(FindObjectsSortMode.None))
            f.enabled = false;
        Vfx.Play2D(reachedSound);
        Invoke(nameof(LoadNext), loadDelay);
    }

    void LoadNext()
    {
        if (!string.IsNullOrEmpty(nextScene))
        {
            SceneManager.LoadScene(nextScene);
            return;
        }
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next > 0 && next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.LogWarning("[RoofExit] No next scene: add it to File > Build Profiles or set Next Scene.");
    }

    void OnGUI()
    {
        if (!reached)
            return;
        // Made once: OnGUI runs several times a frame.
        style ??= new GUIStyle(GUI.skin.label) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 80f), message, style);
    }
}
