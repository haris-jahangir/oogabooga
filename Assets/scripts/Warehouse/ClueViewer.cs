using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A clue the player inspects: a newspaper or bill (shows a picture and/or text on screen)
/// or a telephone (plays a recording with optional subtitles). Each clue holds one digit
/// of the keypad code, drawn into the picture or spoken in the recording.
///
/// Wire it: on the object's Interactable, add Show() to On Used (untick Single Use so it can be re-read).
/// E or Esc puts it away. When the monster's warning starts, it closes so the player can hide.
/// </summary>
public class ClueViewer : MonoBehaviour
{
    static int openCount;

    /// <summary>True while any clue is open (a picture shown or a recording playing).</summary>
    public static bool AnyOpen => openCount > 0;

    // Domain reload is disabled in this project, so statics survive between Play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => openCount = 0;

    [Header("Picture (newspaper, bill)")]
    [Tooltip("Close-up image shown on screen. Put the digit in the art.")]
    [SerializeField] Sprite picture;
    [Tooltip("Optional text under the picture, or on its own if there's no picture.")]
    [TextArea(2, 6)] [SerializeField] string text;

    [Header("Recording (telephone)")]
    [SerializeField] AudioClip recording;
    [Tooltip("Optional subtitle shown while the recording plays.")]
    [TextArea(1, 4)] [SerializeField] string subtitle;
    [Tooltip("Let the player keep moving while the recording plays.")]
    [SerializeField] bool canMoveDuringRecording = true;

    AudioSource audioSource;
    PlayerMovement player;
    bool showing;
    bool frozePlayer;
    int openedFrame;

    bool HasPicture => picture != null || !string.IsNullOrEmpty(text);

    void Start()
    {
        player = FindFirstObjectByType<PlayerMovement>();
        if (recording != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    /// <summary>Open the clue. Called by the Interactable.</summary>
    public void Show()
    {
        if (showing)
            return;
        showing = true;
        openCount++;
        openedFrame = Time.frameCount; // the E that opened it must not also close it

        if (recording != null)
            audioSource.Play();

        frozePlayer = player != null && player.CanMove && (HasPicture || !canMoveDuringRecording);
        if (frozePlayer)
        {
            player.CanMove = false;
            Interactable.Blocked = true;
        }
    }

    void Close()
    {
        if (showing)
            openCount--;
        showing = false;
        if (audioSource != null)
            audioSource.Stop();
        if (frozePlayer)
        {
            Interactable.Blocked = false;
            if (player != null)
                player.CanMove = true;
            frozePlayer = false;
        }
    }

    void OnDisable()
    {
        if (showing)
            Close();
    }

    void Update()
    {
        if (!showing || Time.frameCount == openedFrame)
            return;

        // Something else gave movement back: that's the monster's warning. Let the player hide.
        if (frozePlayer && player != null && player.CanMove)
        {
            frozePlayer = false;
            Interactable.Blocked = false;
            Close();
            return;
        }

        // A recording on its own ends by itself.
        if (!HasPicture && audioSource != null && !audioSource.isPlaying)
        {
            Close();
            return;
        }

        Keyboard k = Keyboard.current;
        if (HasPicture && k != null && (k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame))
            Close();
    }

    void OnGUI()
    {
        if (!showing)
            return;

        var style = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        bool playing = audioSource != null && audioSource.isPlaying;

        if (HasPicture)
        {
            var box = new Rect(Screen.width * 0.2f, Screen.height * 0.1f, Screen.width * 0.6f, Screen.height * 0.75f);
            GUI.Box(box, "");
            float textHeight = string.IsNullOrEmpty(text) ? 0f : 90f;
            var imageArea = new Rect(box.x + 20f, box.y + 20f, box.width - 40f, box.height - 70f - textHeight);
            if (picture != null)
                DrawSprite(picture, imageArea);
            if (textHeight > 0f)
                GUI.Label(new Rect(box.x + 20f, box.yMax - 50f - textHeight, box.width - 40f, textHeight), text, style);
            var hint = new GUIStyle(style) { fontSize = 14 };
            GUI.Label(new Rect(box.x, box.yMax - 40f, box.width, 30f), "E / Esc  put it down", hint);
        }

        if (playing && !string.IsNullOrEmpty(subtitle))
            GUI.Label(new Rect(Screen.width * 0.1f, Screen.height - 140f, Screen.width * 0.8f, 60f), subtitle, style);
    }

    // Draws a sprite (even one cut from a sheet) fitted inside the area, keeping its shape.
    static void DrawSprite(Sprite sprite, Rect area)
    {
        Rect r = sprite.textureRect;
        Texture2D tex = sprite.texture;
        float scale = Mathf.Min(area.width / r.width, area.height / r.height);
        var size = new Vector2(r.width * scale, r.height * scale);
        var dest = new Rect(area.center - size / 2f, size);
        var uv = new Rect(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
        GUI.DrawTextureWithTexCoords(dest, tex, uv);
    }
}
