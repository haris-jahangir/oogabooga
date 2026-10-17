using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The opening of room 1: first a panel with how to survive (press E), then the player wakes up
/// somewhere dark. The eyelids flutter open, the haze clears, and "Where am I?" fades in and out,
/// worn and quiet. Then the player can move, but an invisible wall keeps them near the switch
/// until it's used. After TRY AGAIN on the Game Over screen the opening is skipped.
/// Put it on any object in the scene. It draws its own screen overlay, so it needs no setup.
/// </summary>
public class WakeUpIntro : MonoBehaviour
{
    [Tooltip("What the player thinks as they wake up.")]
    public string line = "Where am I?";
    [Tooltip("A smaller line under it. Leave empty for none.")]
    public string subtitle = "";
    [Tooltip("The line's font (Special Elite: a worn typewriter). Empty = Unity's default.")]
    public Font font;
    [Tooltip("Optional: breathing or a heartbeat while waking up.")]
    public AudioClip wakeSound;
    [Tooltip("Optional: the voice line, played when the line appears.")]
    public AudioClip voiceLine;
    [Tooltip("The panel shown before waking up. Empty = no panel.")]
    [TextArea(4, 10)]
    public string howToSurvive =
        "You wake up inside a burning warehouse.\n\n" +
        "When the lights flicker, something is coming.\n" +
        "Get into the light: the shadow can't reach you there.\n\n" +
        "Find the code. Outrun the fire. Escape.";
    public string controls = "A / D  move      SPACE  jump      SHIFT  dash      E  use";
    [Tooltip("Until the switch is used, an invisible wall this far to the player's right keeps them near it. 0 = none.")]
    public float holdBackRight = 1f;

    PlayerMovement player;
    Transform cam;
    GameObject overlay;
    RectTransform topLid, bottomLid;
    Image haze;
    Text caption, captionSub;

    IEnumerator Start()
    {
        player = FindFirstObjectByType<PlayerMovement>();
        HoldBack();
        if (GameOverScreen.Retrying)
        {
            // Straight back in after a death: no panel, no waking up again.
            GameOverScreen.Retrying = false;
            enabled = false;
            yield break;
        }
        if (player != null)
            player.CanMove = false;
        if (Camera.main != null)
            cam = Camera.main.transform;
        BuildOverlay();
        SetLids(0f);
        if (!string.IsNullOrEmpty(howToSurvive))
            yield return HowToSurvive();
        Play(wakeSound);

        // Black, then the eyes try to open, close again, and open for real.
        yield return new WaitForSeconds(1f);
        yield return MoveLids(0f, 0.3f, 0.7f);
        yield return MoveLids(0.3f, 0.04f, 0.3f);
        yield return new WaitForSeconds(0.4f);
        yield return MoveLids(0.04f, 0.55f, 0.6f);
        yield return new WaitForSeconds(0.25f);
        yield return MoveLids(0.55f, 1f, 1f);

        // The haze clears and the line comes up slowly, stays a moment, and goes.
        StartCoroutine(Fade(haze, haze.color.a, 0f, 1.5f));
        Play(voiceLine);
        yield return FadeCaption(0f, 1f, 1.4f);
        if (player != null)
            player.CanMove = true;
        yield return new WaitForSeconds(2f);
        yield return FadeCaption(1f, 0f, 1.4f);
        Destroy(overlay);
        enabled = false;
    }

    // An invisible wall to the player's right until the switch is used, so they find it first.
    void HoldBack()
    {
        LevelStarter starter = FindFirstObjectByType<LevelStarter>();
        if (holdBackRight <= 0f || player == null || starter == null || starter.HasStarted)
            return;
        var wall = new GameObject("Hold Back Wall (until the switch)");
        wall.transform.position = player.transform.position + Vector3.right * holdBackRight;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 30f);
        starter.Started += () => Destroy(wall);
    }

    // The game in four lines, on the black before the eyes open. Any of E / Enter / Space / click goes on.
    IEnumerator HowToSurvive()
    {
        Font words = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Font title = Resources.Load<Font>("Fonts/Creepster-Regular");
        var group = new GameObject("How To Survive", typeof(RectTransform)).AddComponent<CanvasGroup>();
        var rt = (RectTransform)group.transform;
        rt.SetParent(overlay.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Text head = MakeText("Title", "HOW TO SURVIVE", 96, title != null ? title : words, new Vector2(0f, 330f));
        head.color = new Color(0.6f, 0.06f, 0.04f);
        Text body = MakeText("Rules", howToSurvive, 40, words, new Vector2(0f, 60f));
        body.rectTransform.sizeDelta = new Vector2(1500f, 420f);
        body.lineSpacing = 1.15f;
        Text keys = MakeText("Controls", controls, 30, words, new Vector2(0f, -230f));
        keys.color = new Color(0.6f, 0.58f, 0.54f);
        Text press = MakeText("Press", "Press E to begin", 34, words, new Vector2(0f, -360f));
        foreach (Text t in new[] { head, body, keys, press })
            t.transform.SetParent(rt, true);

        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f)
        {
            group.alpha = t;
            yield return null;
        }
        group.alpha = 1f;
        while (!Continue())
        {
            press.color = new Color(0.86f, 0.84f, 0.79f, 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 2f)));
            yield return null;
        }
        for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.6f)
        {
            group.alpha = t;
            yield return null;
        }
        Destroy(group.gameObject);
    }

    static bool Continue()
    {
        Keyboard k = Keyboard.current;
        return k != null && (k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
            || Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    IEnumerator MoveLids(float from, float to, float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            SetLids(Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
            yield return null;
        }
        SetLids(to);
    }

    // open = 0: eyes shut (lids meet in the middle). open = 1: fully open (lids off screen).
    void SetLids(float open)
    {
        float cover = (1f - open) * 0.5f;
        topLid.anchorMin = new Vector2(0f, 1f - cover - 0.08f); // a little extra so the soft edge stays off screen
        bottomLid.anchorMax = new Vector2(1f, cover + 0.08f);
    }

    IEnumerator FadeCaption(float from, float to, float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            SetAlpha(caption, Mathf.Lerp(from, to, t));
            SetAlpha(captionSub, Mathf.Lerp(from, to, t) * 0.7f);
            yield return null;
        }
        SetAlpha(caption, to);
        SetAlpha(captionSub, to * 0.7f);
    }

    static IEnumerator Fade(Graphic g, float from, float to, float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
        {
            SetAlpha(g, Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetAlpha(g, to);
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && cam != null)
            AudioSource.PlayClipAtPoint(clip, cam.position);
    }

    void BuildOverlay()
    {
        overlay = new GameObject("Wake Up Overlay");
        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = overlay.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        haze = Panel("Haze", new Color(0.02f, 0.02f, 0.05f, 0.55f), Vector2.zero, Vector2.one).GetComponent<Image>();
        topLid = Panel("Top Eyelid", Color.black, new Vector2(0f, 0.5f), Vector2.one);
        bottomLid = Panel("Bottom Eyelid", Color.black, Vector2.zero, new Vector2(1f, 0.5f));

        Font face = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption = MakeText("Line", line, 52, face, new Vector2(0f, -150f));
        captionSub = MakeText("Subtitle", subtitle, 30, face, new Vector2(0f, -215f));
        SetAlpha(caption, 0f);
        SetAlpha(captionSub, 0f);
    }

    RectTransform Panel(string name, Color color, Vector2 min, Vector2 max)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(overlay.transform, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.gameObject.AddComponent<Image>().color = color;
        return rt;
    }

    Text MakeText(string name, string value, int size, Font font, Vector2 position)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(overlay.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(1600f, size * 1.6f);
        var text = rt.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = new Color(0.86f, 0.84f, 0.79f); // old paper, not bright white
        text.text = value;
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
        return text;
    }
}
