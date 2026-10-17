using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Something the player uses with Interact (E) when standing close to it: a switch, a bag, a clue.
/// Shows a prompt above it; with Hold Seconds above 0, E must be held and a bar fills.
/// Wire what it does in On Used.
/// </summary>
public class Interactable : MonoBehaviour
{
    public string prompt = "Use";
    [Tooltip("Seconds to hold E. 0 = a single press.")]
    public float holdSeconds;
    public float range = 1.2f;
    [Tooltip("Only usable while the player stands in a shadow (e.g. glow paint).")]
    public bool requiresHidden;
    public bool singleUse = true;
    [Tooltip("Optional sound played when the player uses it.")]
    public AudioClip useSound;
    [Range(0f, 1f)] public float useVolume = 1f;
    public UnityEvent onUsed = new();

    /// <summary>True while a menu (like the lock dial) has taken over the controls.</summary>
    public static bool Blocked;
    static Interactable focus;

    // Domain reload is disabled in this project, so statics survive between Play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Blocked = false;
        focus = null;
    }

    Transform player;
    InputAction interact;
    AudioSource audioSource;
    float held;
    bool used;

    void Start()
    {
        PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
        if (p != null)
            player = p.transform;
        interact = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
        interact.Enable();
    }

    void Update()
    {
        bool usable = player != null && !used && !Blocked
            && (!requiresHidden || HidingSpot.IsPlayerHidden)
            && Distance() <= range;
        // The closest usable thing gets the prompt, so neighbouring bags don't fight.
        if (usable && (focus == null || Distance() < focus.Distance()))
            focus = this;
        if (!usable && focus == this)
            focus = null;
        if (focus != this)
        {
            held = 0f;
            return;
        }

        bool done;
        if (holdSeconds <= 0f)
            done = interact.WasPressedThisFrame();
        else
        {
            held = interact.IsPressed() ? held + Time.deltaTime : 0f;
            done = held >= holdSeconds;
        }
        if (!done)
            return;

        held = 0f;
        if (singleUse)
        {
            used = true;
            focus = null;
        }
        PlayUseSound();
        onUsed.Invoke();
    }

    void PlayUseSound()
    {
        if (useSound == null)
            return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D: same loudness wherever the camera is
        }
        audioSource.PlayOneShot(useSound, useVolume);
    }

    float Distance() => Vector2.Distance(player.position, transform.position);

    void OnGUI()
    {
        if (focus != this || Camera.main == null)
            return;
        Vector3 s = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 0.8f);
        string bar = holdSeconds > 0f && held > 0f ? "\n" + new string('|', Mathf.CeilToInt(held / holdSeconds * 20f)) : "";
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 18 };
        GUI.Label(new Rect(s.x - 150f, Screen.height - s.y - 60f, 300f, 60f), $"[E] {prompt}{bar}", style);
    }
}
