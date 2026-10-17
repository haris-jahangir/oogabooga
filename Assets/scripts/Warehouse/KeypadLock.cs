using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// The digital lock on the warehouse floor. Open() shows a keypad: type the 4 digits
/// (number row or numpad), Backspace deletes, Enter tries the code. Esc, an arrow/WASD key
/// or a mouse click steps away.
/// The right code loads the next scene.
///
/// Wire it: on the lock's Interactable, add Open() to On Used (untick Single Use).
/// When the monster's warning starts, the player is pulled off the keypad to go hide.
/// </summary>
public class KeypadLock : MonoBehaviour
{
    [Tooltip("The 4-digit code: one digit each from the newspaper, the wall, the telephone and the bill.")]
    [SerializeField] string code = "3816";
    [Tooltip("Scene to load when unlocked. Leave empty for the next scene in Build Profiles.")]
    [SerializeField] string nextScene = "";
    [Tooltip("Seconds between unlocking and loading the next scene.")]
    [SerializeField] float loadDelay = 1f;

    [Header("Sounds (optional)")]
    [SerializeField] AudioClip keySound;
    [SerializeField] AudioClip wrongSound;
    [SerializeField] AudioClip unlockSound;

    AudioSource audioSource;
    PlayerMovement player;
    string entered = "";
    bool open, solved;
    int openedFrame;
    string message;
    float messageUntil;

    void Start()
    {
        player = FindFirstObjectByType<PlayerMovement>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    /// <summary>Show the keypad. Called by the Interactable.</summary>
    public void Open()
    {
        if (open || solved || player == null || !player.CanMove)
            return;
        open = true;
        entered = "";
        openedFrame = Time.frameCount; // the E that opened it must not count as input
        Interactable.Blocked = true;
        player.CanMove = false;
    }

    void Close()
    {
        open = false;
        Interactable.Blocked = false;
        if (player != null)
            player.CanMove = true;
    }

    void OnDisable()
    {
        if (open)
            Close();
    }

    void Update()
    {
        if (!open || solved || Time.frameCount == openedFrame)
            return;

        // Something else gave movement back: that's the monster's warning. Go hide.
        if (player.CanMove)
        {
            open = false;
            Interactable.Blocked = false;
            Say("Something is coming. Hide!");
            return;
        }

        Keyboard k = Keyboard.current;
        if (k == null)
            return;

        if (MenuInput.DismissPressed())
        {
            Close();
            return;
        }
        if (k.backspaceKey.wasPressedThisFrame && entered.Length > 0)
        {
            entered = entered.Substring(0, entered.Length - 1);
            Play(keySound);
            return;
        }
        if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame)
        {
            TryCode();
            return;
        }

        for (int d = 0; d <= 9; d++)
        {
            // The number row runs 1..9 then 0 in the Key enum; the numpad runs 0..9.
            Key row = d == 0 ? Key.Digit0 : Key.Digit1 + (d - 1);
            bool pressed = k[row].wasPressedThisFrame || k[Key.Numpad0 + d].wasPressedThisFrame;
            if (pressed && entered.Length < code.Length)
            {
                entered += d;
                Play(keySound);
                break;
            }
        }
    }

    void TryCode()
    {
        if (entered != code)
        {
            Play(wrongSound);
            Say("Wrong code.");
            entered = "";
            return;
        }

        solved = true;
        // Nothing can catch the player between unlocking and the next scene.
        foreach (MonsterManager m in FindObjectsByType<MonsterManager>(FindObjectsSortMode.None))
            m.StopHunting();
        foreach (Fire f in FindObjectsByType<Fire>(FindObjectsSortMode.None))
            f.enabled = false;
        Play(unlockSound);
        Say("Unlocked!");
        Invoke(nameof(LoadNext), loadDelay);
    }

    void LoadNext()
    {
        Interactable.Blocked = false;
        if (!string.IsNullOrEmpty(nextScene))
        {
            SceneManager.LoadScene(nextScene);
            return;
        }
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(next);
        else
            Debug.LogWarning("[KeypadLock] No next scene: add it to File > Build Profiles or set Next Scene.");
    }

    void Say(string text)
    {
        message = text;
        messageUntil = Time.time + 2.5f;
    }

    void Play(AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        if (Time.time < messageUntil)
            GUI.Label(new Rect(0f, Screen.height - 90f, Screen.width, 40f), message, style);
        if (!open)
            return;

        var box = new Rect(Screen.width / 2f - 170f, Screen.height / 2f - 90f, 340f, 180f);
        GUI.Box(box, "");
        var big = new GUIStyle(style) { fontSize = 48 };
        string shown = "";
        for (int i = 0; i < code.Length; i++)
            shown += (i < entered.Length ? entered[i].ToString() : "_") + (i < code.Length - 1 ? " " : "");
        GUI.Label(new Rect(box.x, box.y + 25f, box.width, 70f), shown, big);
        var hint = new GUIStyle(style) { fontSize = 14 };
        GUI.Label(new Rect(box.x, box.y + 120f, box.width, 40f), "Type the code   Enter try   Backspace delete   Arrows / Esc / click: leave", hint);
    }
}
