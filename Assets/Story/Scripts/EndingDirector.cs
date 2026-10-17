using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the ending scene: the player walks out of the burning warehouse, the shadow is waiting,
/// it speaks, and the player chooses.
///   Take its hand        -> the dark ending: we become the shadow, watching the next person wake up.
///   Walk toward the light -> the light ending: past the shadow into the lamp, waking up in hospital.
/// Every line of text is editable in the Inspector. E / Space / Enter advances, A / D picks.
/// </summary>
public class EndingDirector : MonoBehaviour
{
    [Header("Scene")]
    public SpriteRenderer docks;
    public SpriteRenderer hospital;
    public SpriteRenderer shadowView;
    public SpriteRenderer player;
    public SpriteRenderer shadow;
    [Tooltip("x positions on the dock: warehouse door, where the player stops, the shadow, the lamp.")]
    public float doorX = -3.75f, stopX = -1.6f, shadowX = 2.05f, lampX = 7.2f;

    [Header("Words")]
    public string speaker = "???";
    [TextArea(1, 3)]
    public string[] lines =
    {
        "You made it out.",
        "Nobody makes it out.",
        "It's quiet in the dark. No fire. No pain.",
        "I was like you once... I chose to stay.",
        "Stay with me.",
    };
    public string choiceDark = "Take its hand";
    public string choiceLight = "Walk toward the light";
    [TextArea(1, 3)] public string darkCaption1 = "You chose the dark.";
    [TextArea(1, 3)] public string darkCaption2 = "Somewhere inside, someone opens their eyes...";
    public string darkTitle = "Ending 1 of 2: The Next Shadow";
    [TextArea(1, 3)] public string lightCaption1 = "\"...stay with us... there you are.\"";
    [TextArea(1, 3)] public string lightCaption2 = "You were pulled out of the Pier 3 fire. You're alive.";
    public string lightTitle = "Ending 2 of 2: Into the Light";

    [Header("Sound (optional)")]
    public AudioClip shadowAppears;
    public AudioClip darkSting;

    [Header("Framing")]
    [Tooltip("The backdrop's size in units. The camera always shows all of it, whatever the window shape.")]
    public Vector2 pictureSize = new Vector2(1920f / 108f, 10f);

    Image fade, box;
    Text speakerText, lineText, captionText, titleText, subTitleText, hintText;
    Text[] choiceTexts;
    AudioSource sfx;

    IEnumerator Start()
    {
        sfx = gameObject.AddComponent<AudioSource>();
        BuildUI();
        SetAlpha(hospital, 0f);
        SetAlpha(shadowView, 0f);
        SetAlpha(shadow, 0f);
        player.transform.position = new Vector3(doorX, player.transform.position.y, 0f);

        yield return FadeScreen(Color.black, 1f, 0f, 1.5f);
        yield return Walk(stopX, 1.8f);
        yield return new WaitForSeconds(0.6f);

        Play(shadowAppears);
        yield return FadeSprite(shadow, 0f, 1f, 1.4f);
        yield return new WaitForSeconds(0.5f);

        box.gameObject.SetActive(true);
        foreach (string line in lines)
            yield return Say(line);

        int choice = 0;
        yield return Choose(c => choice = c);
        box.gameObject.SetActive(false);

        if (choice == 0)
            yield return DarkEnding();
        else
            yield return LightEnding();
    }

    // Fit the whole picture on screen: narrower windows get black bars top and bottom instead of losing the sides.
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
            cam.orthographicSize = Mathf.Max(pictureSize.y / 2f, pictureSize.x / 2f / cam.aspect);
    }

    IEnumerator DarkEnding()
    {
        yield return Walk(shadowX - 0.7f, 1.6f);
        Play(darkSting);
        yield return FadeScreen(Color.black, 0f, 1f, 1.6f);
        yield return Caption(darkCaption1, 2.6f);
        SetAlpha(docks, 0f); SetAlpha(player, 0f); SetAlpha(shadow, 0f);
        SetAlpha(shadowView, 1f);
        StartCoroutine(SlowZoom(shadowView.transform, 1.12f, 9f));
        yield return FadeScreen(Color.black, 1f, 0f, 2f);
        yield return Caption(darkCaption2, 3.5f);
        yield return EndCard(darkTitle);
    }

    IEnumerator LightEnding()
    {
        // Straight past the shadow and into the lamp's light; the shadow can't follow.
        StartCoroutine(ShadowRecoils());
        yield return Walk(lampX, 3.2f);
        yield return FadeScreen(Color.white, 0f, 1f, 1.4f);
        StartCoroutine(HeartMonitor());
        SetAlpha(docks, 0f); SetAlpha(player, 0f); SetAlpha(shadow, 0f);
        SetAlpha(hospital, 1f);
        StartCoroutine(SlowZoom(hospital.transform, 1.06f, 10f));
        yield return FadeScreen(Color.white, 1f, 0f, 2.2f);
        yield return Caption(lightCaption1, 3f);
        yield return Caption(lightCaption2, 3.5f);
        yield return EndCard(lightTitle);
    }

    IEnumerator ShadowRecoils()
    {
        while (player.transform.position.x < shadowX - 1.2f)
            yield return null;
        yield return FadeSprite(shadow, shadow.color.a, 0f, 0.6f);
    }

    IEnumerator EndCard(string ending)
    {
        yield return FadeScreen(Color.black, 0f, 1f, 1.5f);
        titleText.text = "THE END";
        subTitleText.text = ending;
        hintText.text = "Press E to play again";
        yield return FadeTexts(0f, 1f, 1.2f, titleText, subTitleText, hintText);
        yield return WaitForAdvance();
        SceneManager.LoadScene(0);
    }

    // ---- dialogue ----

    IEnumerator Say(string line)
    {
        speakerText.text = speaker;
        lineText.text = "";
        for (int i = 1; i <= line.Length; i++)
        {
            lineText.text = line.Substring(0, i);
            if (Advance())
            {
                lineText.text = line; // pressing during typing shows the whole line
                yield return null;
                break;
            }
            yield return new WaitForSeconds(0.035f);
        }
        yield return WaitForAdvance();
    }

    IEnumerator Choose(System.Action<int> picked)
    {
        lineText.text = "";
        speakerText.text = "";
        int current = 0;
        foreach (Text t in choiceTexts)
            t.gameObject.SetActive(true);
        while (true)
        {
            for (int i = 0; i < 2; i++)
                choiceTexts[i].text = (i == current ? "> " : "   ") + (i == 0 ? choiceDark : choiceLight);
            choiceTexts[0].color = current == 0 ? new Color(1f, 0.85f, 0.5f) : new Color(1f, 1f, 1f, 0.6f);
            choiceTexts[1].color = current == 1 ? new Color(1f, 0.85f, 0.5f) : new Color(1f, 1f, 1f, 0.6f);
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame || k.digit1Key.wasPressedThisFrame) current = 0;
                if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame || k.digit2Key.wasPressedThisFrame) current = 1;
            }
            if (Advance())
                break;
            yield return null;
        }
        foreach (Text t in choiceTexts)
            t.gameObject.SetActive(false);
        picked(current);
    }

    static bool Advance()
    {
        Keyboard k = Keyboard.current;
        return k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame);
    }

    IEnumerator WaitForAdvance()
    {
        yield return null;
        while (!Advance())
            yield return null;
    }

    IEnumerator Caption(string text, float hold)
    {
        captionText.text = text;
        yield return FadeTexts(0f, 1f, 0.8f, captionText);
        yield return new WaitForSeconds(hold);
        yield return FadeTexts(1f, 0f, 0.8f, captionText);
    }

    // ---- movement, fades, sound ----

    IEnumerator Walk(float toX, float seconds)
    {
        Transform t = player.transform;
        float fromX = t.position.x, baseY = t.position.y;
        player.flipX = toX < fromX;
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            float x = Mathf.Lerp(fromX, toX, k);
            t.position = new Vector3(x, baseY + Mathf.Abs(Mathf.Sin(k * seconds * 9f)) * 0.05f, 0f);
            yield return null;
        }
        t.position = new Vector3(toX, baseY, 0f);
    }

    IEnumerator SlowZoom(Transform t, float to, float seconds)
    {
        Vector3 from = t.localScale;
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            t.localScale = from * Mathf.Lerp(1f, to, k);
            yield return null;
        }
    }

    IEnumerator HeartMonitor()
    {
        AudioClip beep = MakeBeep();
        while (true)
        {
            sfx.PlayOneShot(beep, 0.35f);
            yield return new WaitForSeconds(0.85f);
        }
    }

    static AudioClip MakeBeep()
    {
        const int rate = 44100;
        var data = new float[rate / 8];
        for (int i = 0; i < data.Length; i++)
        {
            float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (data.Length - i) / 800f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 880f * i / rate) * env;
        }
        AudioClip clip = AudioClip.Create("Monitor Beep", data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void Play(AudioClip clip)
    {
        if (clip != null)
            sfx.PlayOneShot(clip);
    }

    IEnumerator FadeScreen(Color color, float from, float to, float seconds)
    {
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            fade.color = new Color(color.r, color.g, color.b, Mathf.Lerp(from, to, k));
            yield return null;
        }
        fade.color = new Color(color.r, color.g, color.b, to);
    }

    static IEnumerator FadeSprite(SpriteRenderer sr, float from, float to, float seconds)
    {
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            SetAlpha(sr, Mathf.Lerp(from, to, k));
            yield return null;
        }
        SetAlpha(sr, to);
    }

    static IEnumerator FadeTexts(float from, float to, float seconds, params Text[] texts)
    {
        for (float k = 0f; k < 1f; k += Time.deltaTime / seconds)
        {
            foreach (Text t in texts)
                t.color = new Color(t.color.r, t.color.g, t.color.b, Mathf.Lerp(from, to, k));
            yield return null;
        }
        foreach (Text t in texts)
            t.color = new Color(t.color.r, t.color.g, t.color.b, to);
    }

    static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null)
            return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    // ---- screen UI, built in code so the scene needs no setup ----

    void BuildUI()
    {
        var root = new GameObject("Ending UI");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        box = Rect(root.transform, "Dialogue Box", new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.27f)).gameObject.AddComponent<Image>();
        box.color = new Color(0.02f, 0.02f, 0.05f, 0.88f);
        speakerText = Label(box.transform, "Speaker", font, 30, TextAnchor.UpperLeft, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.94f));
        speakerText.color = new Color(0.62f, 0.75f, 1f);
        lineText = Label(box.transform, "Line", font, 40, TextAnchor.UpperLeft, new Vector2(0.04f, 0.1f), new Vector2(0.96f, 0.68f));
        choiceTexts = new[]
        {
            Label(box.transform, "Choice Dark", font, 40, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.2f), new Vector2(0.5f, 0.8f)),
            Label(box.transform, "Choice Light", font, 40, TextAnchor.MiddleLeft, new Vector2(0.52f, 0.2f), new Vector2(0.96f, 0.8f)),
        };
        foreach (Text t in choiceTexts)
            t.gameObject.SetActive(false);
        box.gameObject.SetActive(false);

        captionText = Label(root.transform, "Caption", font, 44, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.6f));
        captionText.fontStyle = FontStyle.Italic;
        captionText.color = new Color(1f, 1f, 1f, 0f);

        fade = Rect(root.transform, "Fade", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
        fade.color = Color.black;
        fade.raycastTarget = false;
        captionText.transform.SetAsLastSibling();

        titleText = Label(root.transform, "Title", font, 96, TextAnchor.MiddleCenter, new Vector2(0f, 0.52f), new Vector2(1f, 0.7f));
        subTitleText = Label(root.transform, "Ending Name", font, 40, TextAnchor.MiddleCenter, new Vector2(0f, 0.42f), new Vector2(1f, 0.52f));
        hintText = Label(root.transform, "Hint", font, 26, TextAnchor.MiddleCenter, new Vector2(0f, 0.2f), new Vector2(1f, 0.28f));
        foreach (Text t in new[] { titleText, subTitleText, hintText })
            t.color = new Color(1f, 1f, 1f, 0f);
    }

    static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static Text Label(Transform parent, string name, Font font, int size, TextAnchor align, Vector2 min, Vector2 max)
    {
        Text t = Rect(parent, name, min, max).gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
