using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The opening of room 1: the player wakes up somewhere dark, lying on the floor. The eyelids
/// flutter open, the view sways and clears, the player gets up, a line appears
/// ("Main kahan hoon?"), then the player can move.
/// Put it on any object in the scene. It draws its own screen overlay, so it needs no setup.
/// </summary>
public class WakeUpIntro : MonoBehaviour
{
    [Tooltip("What the player thinks as they wake up.")]
    public string line = "Main kahan hoon?";
    [Tooltip("Translation under the line. Leave empty for none.")]
    public string subtitle = "Where am I?";
    [Tooltip("Optional: breathing or a heartbeat while waking up.")]
    public AudioClip wakeSound;
    [Tooltip("Optional: the voice line, played when the line appears.")]
    public AudioClip voiceLine;
    [Tooltip("How far the view tilts while groggy, in degrees.")]
    public float sway = 3f;
    [Tooltip("Start lying on the floor and get up once the eyes are open.")]
    public bool startLying = true;
    [Tooltip("How far the player sinks when lying down, so the body rests on the floor (units).")]
    public float lieDrop = 0.26f;

    PlayerMovement player;
    Rigidbody2D body;
    Transform cam;
    GameObject overlay;
    RectTransform topLid, bottomLid;
    Image haze;
    Text caption, captionSub;
    float swayLeft;

    IEnumerator Start()
    {
        player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            player.CanMove = false;
            if (startLying)
                LieDown();
        }
        if (Camera.main != null)
            cam = Camera.main.transform;
        BuildOverlay();
        SetLids(0f);
        Play(wakeSound);

        // Black, then the eyes try to open, close again, and open for real.
        yield return new WaitForSeconds(1f);
        swayLeft = 1f;
        yield return MoveLids(0f, 0.3f, 0.7f);
        yield return MoveLids(0.3f, 0.04f, 0.3f);
        yield return new WaitForSeconds(0.4f);
        yield return MoveLids(0.04f, 0.55f, 0.6f);
        yield return new WaitForSeconds(0.25f);
        yield return MoveLids(0.55f, 1f, 1f);

        // The haze clears, the player gets up, and the line appears.
        StartCoroutine(Fade(haze, haze.color.a, 0f, 1.5f));
        if (body != null)
            yield return GetUp();
        Play(voiceLine);
        yield return FadeCaption(0f, 1f, 0.6f);
        if (player != null)
            player.CanMove = true;
        yield return new WaitForSeconds(2.2f);
        yield return FadeCaption(1f, 0f, 0.8f);
        Destroy(overlay);
        if (cam != null)
            cam.rotation = Quaternion.identity;
        enabled = false; // hand the camera's rotation back
    }

    void LateUpdate()
    {
        if (cam == null || swayLeft <= 0f)
            return;
        // A slow, groggy tilt that settles as the player comes round.
        swayLeft = Mathf.MoveTowards(swayLeft, 0f, Time.deltaTime / 4f);
        cam.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.3f) * sway * swayLeft);
    }

    void LieDown()
    {
        // Physics off while lying, or the body would be pushed back out of the floor.
        if (!player.TryGetComponent(out body))
            return;
        body.simulated = false;
        player.transform.SetPositionAndRotation(player.transform.position + Vector3.down * lieDrop, Quaternion.Euler(0f, 0f, 90f));
    }

    IEnumerator GetUp()
    {
        Transform t = player.transform;
        Vector3 lying = t.position, standing = lying + Vector3.up * lieDrop;
        const float seconds = 0.9f;
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            // Pushes up slowly, then swings upright, a little unsteady.
            float e = 1f - Mathf.Pow(1f - k, 3f);
            float wobble = Mathf.Sin(k * Mathf.PI * 3f) * (1f - k) * 6f;
            t.SetPositionAndRotation(Vector3.Lerp(lying, standing, Mathf.Sqrt(k)), Quaternion.Euler(0f, 0f, 90f * (1f - e) + wobble));
            yield return null;
        }
        t.SetPositionAndRotation(standing, Quaternion.identity);
        body.simulated = true;
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

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption = MakeText("Line", line, 54, font, new Vector2(0f, -120f));
        captionSub = MakeText("Subtitle", subtitle, 30, font, new Vector2(0f, -185f));
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
        text.fontStyle = FontStyle.Italic;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.color = new Color(0.92f, 0.9f, 0.85f);
        text.text = value;
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        return text;
    }
}
