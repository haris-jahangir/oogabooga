using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The title screen, old-arcade style: a flickering title over a burning skyline, embers drifting
/// up, red eyes blinking in the dark, and a blinking "PRESS ENTER". Enter opens START GAME / EXIT
/// (arrow keys or W/S to choose, Enter to pick, or use the mouse). Esc goes straight to EXIT.
/// Builds everything itself: the scene only needs a camera and this component.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] string title = "OOGABOOGA";
    [SerializeField] string tagline = "THE WAREHOUSE IS BURNING.  SOMETHING WAITS IN THE DARK.";
    [SerializeField] string footer = "© 2026 OOGABOOGA     GAME JAM EDITION";

    [Header("Flow")]
    [Tooltip("Scene loaded by START GAME. Leave empty for the next scene in Build Profiles.")]
    [SerializeField] string firstScene = "Scene1";

    [Header("Look")]
    [Tooltip("Font for the title. Leave empty to use Chiller (a horror font on Windows), else Impact.")]
    [SerializeField] Font titleFont;
    [Tooltip("Font for everything else. Leave empty to use Stencil (warehouse-crate lettering), else Impact.")]
    [SerializeField] Font menuFont;
    [SerializeField] Color titleColor = new(0.55f, 0.05f, 0.04f);
    [SerializeField] Color glowColor = new(0.8f, 0.18f, 0.04f, 0.22f);

    [Header("Sound")]
    [SerializeField] AudioClip music;
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.6f;
    [SerializeField] AudioClip moveSound;
    [SerializeField] AudioClip startSound;

    enum Stage { Title, Menu, Leaving }

    Stage stage = Stage.Title;
    int selected;               // 0 = START GAME, 1 = EXIT
    Font font;      // menu text
    Font titleFace; // the title
    Text titleText, pressText;
    Text[] options;
    RectTransform[] optionRects;
    Image fade;
    Image[] eyes;
    SpriteRenderer glow;
    AudioSource musicSource;
    Vector2 titleHome;
    float nextEyes;

    void Start()
    {
        font = menuFont != null ? menuFont : FontOrFallback("Stencil", "Impact");
        titleFace = titleFont != null ? titleFont : FontOrFallback("Chiller", "Impact");
        if (moveSound == null) moveSound = Vfx.Sound("land");
        if (startSound == null) startSound = Vfx.Sound("ignite");

        BuildWorld();
        BuildUI();

        if (music != null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.clip = music;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.Play();
        }
        StartCoroutine(FadeFromBlack());
    }

    // ---------- Input ----------

    void Update()
    {
        Animate();
        if (stage == Stage.Leaving)
            return;

        Keyboard k = Keyboard.current;
        Gamepad g = Gamepad.current;
        bool confirm = (k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                    || (g != null && (g.startButton.wasPressedThisFrame || g.buttonSouth.wasPressedThisFrame));
        bool back = (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.buttonEast.wasPressedThisFrame);
        bool up = (k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (g != null && g.dpad.up.wasPressedThisFrame);
        bool down = (k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (g != null && g.dpad.down.wasPressedThisFrame);

        if (stage == Stage.Title)
        {
            if (confirm || back || ClickedAnywhere())
                OpenMenu(back ? 1 : 0);
            return;
        }

        // Menu: keyboard, gamepad, or mouse hover + click.
        int hovered = HoveredOption();
        if (hovered >= 0 && hovered != selected)
            Select(hovered);
        if (up || down)
            Select(selected == 0 ? 1 : 0);
        if (confirm || (hovered >= 0 && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame))
            StartCoroutine(selected == 0 ? StartGame() : ExitGame());
        else if (back)
            Select(1);
    }

    static bool ClickedAnywhere() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    int HoveredOption()
    {
        if (Mouse.current == null || optionRects == null)
            return -1;
        Vector2 m = Mouse.current.position.ReadValue();
        for (int i = 0; i < optionRects.Length; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(optionRects[i], m))
                return i;
        return -1;
    }

    void OpenMenu(int choice)
    {
        stage = Stage.Menu;
        pressText.gameObject.SetActive(false);
        foreach (Text t in options)
            t.gameObject.SetActive(true);
        Select(choice, sound: true);
        CameraFX.Shake(0.15f);
    }

    void Select(int index, bool sound = true)
    {
        selected = index;
        for (int i = 0; i < options.Length; i++)
        {
            bool on = i == selected;
            options[i].text = i == 0 ? "START GAME" : "EXIT";
            // The chosen one glows like a dying bulb; the other sinks into the dark.
            options[i].color = on ? new Color(0.85f, 0.78f, 0.65f) : new Color(0.28f, 0.25f, 0.24f);
            options[i].fontSize = on ? 52 : 44;
            if (options[i].TryGetComponent(out Outline glowEdge))
                glowEdge.effectColor = on ? new Color(0.6f, 0.08f, 0.04f, 0.7f) : Color.clear;
        }
        if (sound)
            Vfx.Play2D(moveSound, 0.5f, 1.8f);
    }

    // ---------- Leaving ----------

    IEnumerator StartGame()
    {
        stage = Stage.Leaving;
        Vfx.Play2D(startSound, 0.9f);
        CameraFX.Shake(0.6f);
        CameraFX.Punch(0.05f, 0.6f, 0.2f);
        // The classic arcade "selected!" strobe.
        for (int i = 0; i < 10; i++)
        {
            options[0].enabled = i % 2 == 1;
            yield return new WaitForSeconds(0.06f);
        }
        options[0].enabled = true;
        yield return FadeToBlack(0.8f);

        if (!string.IsNullOrEmpty(firstScene) && Application.CanStreamedLevelBeLoaded(firstScene))
            SceneManager.LoadScene(firstScene);
        else if (SceneManager.GetActiveScene().buildIndex + 1 < SceneManager.sceneCountInBuildSettings)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
        else
            Debug.LogWarning("[MainMenu] Can't find the first level: add it to File > Build Profiles.");
    }

    IEnumerator ExitGame()
    {
        stage = Stage.Leaving;
        Vfx.Play2D(moveSound, 0.6f, 0.8f);
        yield return FadeToBlack(0.6f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    IEnumerator FadeToBlack(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, t / seconds);
            if (musicSource != null)
                musicSource.volume = musicVolume * (1f - t / seconds);
            yield return null;
        }
        fade.color = Color.black;
    }

    IEnumerator FadeFromBlack()
    {
        for (float t = 0f; t < 1.5f; t += Time.deltaTime)
        {
            fade.color = new Color(0f, 0f, 0f, 1f - t / 1.5f);
            if (musicSource != null)
                musicSource.volume = musicVolume * t / 1.5f;
            yield return null;
        }
        fade.color = Color.clear;
    }

    // ---------- Animation ----------

    void Animate()
    {
        float t = Time.time;

        // Title: steady burn with the odd faulty-sign stutter and a tiny tremble.
        float stutter = Mathf.PerlinNoise(3.1f, t * 1.3f) > 0.82f ? Random.Range(0.25f, 0.6f) : 1f;
        float burn = 0.85f + 0.15f * Mathf.PerlinNoise(7f, t * 4f);
        titleText.color = new Color(titleColor.r, titleColor.g, titleColor.b, burn * stutter);
        titleText.rectTransform.anchoredPosition = titleHome + new Vector2(
            (Mathf.PerlinNoise(t * 9f, 1f) - 0.5f) * 3f, (Mathf.PerlinNoise(2f, t * 9f) - 0.5f) * 3f);

        // PRESS ENTER: on 0.6 s, off 0.4 s.
        if (stage == Stage.Title)
            pressText.enabled = t % 1f < 0.6f;

        if (glow != null)
            glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * (0.75f + 0.25f * Mathf.PerlinNoise(t * 2f, 5f)));

        // Something in the dark: a pair of eyes opens now and then, somewhere different.
        if (t >= nextEyes)
            StartCoroutine(Eyes());
    }

    IEnumerator Eyes()
    {
        nextEyes = Time.time + Random.Range(5f, 10f);
        RectTransform area = (RectTransform)eyes[0].canvas.transform;
        float x = Random.Range(0.1f, 0.9f) * area.rect.width, y = Random.Range(0.12f, 0.35f) * area.rect.height;
        if (x > area.rect.width * 0.35f && x < area.rect.width * 0.65f)
            x += area.rect.width * 0.3f; // keep clear of the menu in the middle
        eyes[0].rectTransform.anchoredPosition = new Vector2(x - 14f, y);
        eyes[1].rectTransform.anchoredPosition = new Vector2(x + 14f, y);
        for (float a = 0f; a < 1f; a += Time.deltaTime * 1.5f)
        {
            SetEyes(a);
            yield return null;
        }
        yield return new WaitForSeconds(Random.Range(1f, 2.5f));
        SetEyes(0f);                         // blink
        yield return new WaitForSeconds(0.12f);
        SetEyes(1f);
        yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));
        for (float a = 1f; a > 0f; a -= Time.deltaTime * 2f)
        {
            SetEyes(a);
            yield return null;
        }
        SetEyes(0f);
    }

    void SetEyes(float a)
    {
        foreach (Image e in eyes)
            e.color = new Color(0.85f, 0.05f, 0.03f, a * 0.85f);
    }

    // ---------- Building the screen ----------

    void BuildWorld()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        cam.backgroundColor = Color.black;
        float halfH = cam.orthographicSize, halfW = halfH * cam.aspect + 1f;
        Vector3 c = cam.transform.position;
        float bottom = c.y - halfH;

        // Fire glow along the bottom of the screen.
        var glowGo = new GameObject("Fire Glow");
        glowGo.transform.position = new Vector3(c.x, bottom, 0f);
        glow = glowGo.AddComponent<SpriteRenderer>();
        glow.sprite = GradientSprite();
        glow.sharedMaterial = Vfx.UnlitSprite;
        Vector2 glowSize = glow.sprite.bounds.size;
        glow.transform.localScale = new Vector3(halfW * 2f / glowSize.x, halfH * 1.1f / glowSize.y, 1f);

        // Flames licking up from the bottom edge.
        ParticleSystem flames = Vfx.MakeSystem(transform, "Flames", Vfx.Additive, 10);
        flames.transform.position = new Vector3(c.x, bottom - 0.3f, 0f);
        var fm = flames.main;
        fm.maxParticles = 1500;
        fm.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        fm.startSpeed = 0f;
        fm.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1f);
        fm.gravityModifier = -0.5f;   // low flames, mostly off-screen
        var fs = flames.shape; fs.shapeType = ParticleSystemShapeType.Box; fs.scale = new Vector3(halfW * 2f, 0.3f, 1f);
        var fe = flames.emission; fe.rateOverTime = halfW * 12f;
        var fc = flames.colorOverLifetime; fc.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(0.9f, 0.5f, 0.2f), 0f), new GradientColorKey(new Color(0.7f, 0.18f, 0.04f), 0.35f), new GradientColorKey(new Color(0.25f, 0.02f, 0.01f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.1f), new GradientAlphaKey(0f, 1f) });
        fc.color = g;
        var fz = flames.sizeOverLifetime; fz.enabled = true; fz.size = Vfx.Shrink(1f, 0.1f);
        var fn = flames.noise; fn.enabled = true; fn.strength = 0.8f; fn.frequency = 0.8f; fn.scrollSpeed = 1.2f;

        // Embers drifting up the whole screen.
        ParticleSystem embers = Vfx.MakeSystem(transform, "Embers", Vfx.Additive, 11);
        embers.transform.position = new Vector3(c.x, bottom, 0f);
        var em = embers.main;
        em.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        em.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        em.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        em.startColor = new ParticleSystem.MinMaxGradient(new Color(0.9f, 0.5f, 0.2f), new Color(0.6f, 0.12f, 0.04f));
        em.gravityModifier = -0.05f;
        var es = embers.shape; es.shapeType = ParticleSystemShapeType.Box; es.scale = new Vector3(halfW * 2f, 0.5f, 1f);
        es.rotation = new Vector3(-90f, 0f, 0f); // emit upwards
        var ee = embers.emission; ee.rateOverTime = 7f;
        var ec = embers.colorOverLifetime; ec.enabled = true; ec.color = Vfx.Fade(Color.white, new Color(1f, 0.3f, 0.1f));
        var en = embers.noise; en.enabled = true; en.strength = 0.6f; en.frequency = 0.4f;

        // Smoke rolling over the fire.
        ParticleSystem smoke = Vfx.MakeSystem(transform, "Smoke", Vfx.Alpha, 9);
        smoke.transform.position = new Vector3(c.x, bottom + 1f, 0f);
        var sm = smoke.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        sm.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
        sm.startColor = new Color(0.03f, 0.03f, 0.03f, 0.75f);
        var ss = smoke.shape; ss.shapeType = ParticleSystemShapeType.Box; ss.scale = new Vector3(halfW * 2f, 0.5f, 1f);
        ss.rotation = new Vector3(-90f, 0f, 0f);
        var se = smoke.emission; se.rateOverTime = 4f;
        var sc = smoke.colorOverLifetime; sc.enabled = true; sc.color = Vfx.Fade(Color.white, Color.white, 0.8f);
        var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = Vfx.Shrink(0.6f, 1.5f);

        // Start mid-blaze rather than from nothing.
        foreach (ParticleSystem ps in new[] { flames, embers, smoke })
            ps.Simulate(4f, true, true);
        foreach (ParticleSystem ps in new[] { flames, embers, smoke })
            ps.Play();
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("Menu Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        titleText = MakeText(canvas.transform, title, 200, FontStyle.Normal, new Vector2(0.5f, 0.68f));
        titleText.font = titleFace;
        var outline = titleText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);
        var shadow = titleText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(8f, -10f);
        titleHome = titleText.rectTransform.anchoredPosition;

        Text tag = MakeText(canvas.transform, tagline, 26, FontStyle.Normal, new Vector2(0.5f, 0.55f));
        tag.color = new Color(0.35f, 0.32f, 0.3f);

        pressText = MakeText(canvas.transform, "PRESS ENTER", 50, FontStyle.Normal, new Vector2(0.5f, 0.36f));
        pressText.color = new Color(0.75f, 0.7f, 0.6f);
        pressText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);

        options = new Text[2];
        optionRects = new RectTransform[2];
        for (int i = 0; i < 2; i++)
        {
            options[i] = MakeText(canvas.transform, "", 48, FontStyle.Normal, new Vector2(0.5f, 0.38f - i * 0.08f));
            options[i].gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
            options[i].gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            options[i].gameObject.SetActive(false);
            optionRects[i] = options[i].rectTransform;
        }
        Select(0, sound: false);

        Text foot = MakeText(canvas.transform, footer, 22, FontStyle.Normal, new Vector2(0.5f, 0.05f));
        foot.color = new Color(0.25f, 0.23f, 0.22f);

        Sprite dot = Sprite.Create(Vfx.SoftDot, new Rect(0, 0, Vfx.SoftDot.width, Vfx.SoftDot.height), new Vector2(0.5f, 0.5f));
        eyes = new Image[2];
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject("Eye");
            go.transform.SetParent(canvas.transform, false);
            eyes[i] = go.AddComponent<Image>();
            eyes[i].sprite = dot;
            eyes[i].raycastTarget = false;
            var rt = eyes[i].rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(16f, 10f);
        }
        SetEyes(0f);
        nextEyes = Time.time + 3f;

        var fadeGo = new GameObject("Fade");
        fadeGo.transform.SetParent(canvas.transform, false);
        fade = fadeGo.AddComponent<Image>();
        fade.color = Color.black;
        fade.raycastTarget = false;
        var frt = fade.rectTransform;
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;
    }

    // A font installed on the player's computer, or Unity's built-in one if none of these exist.
    static Font FontOrFallback(params string[] names)
    {
        string[] installed = Font.GetOSInstalledFontNames();
        foreach (string n in names)
            if (System.Array.IndexOf(installed, n) >= 0)
                return Font.CreateDynamicFontFromOSFont(n, 64);
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    Text MakeText(Transform parent, string content, int size, FontStyle style, Vector2 anchor)
    {
        var go = new GameObject(content.Length > 0 ? content : "Option");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(900f, size * 1.4f);
        rt.anchoredPosition = Vector2.zero;
        return text;
    }

    // A glow that's bright along the bottom and fades out upwards.
    static Sprite GradientSprite()
    {
        const int h = 128;
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
        {
            float a = Mathf.Pow(1f - y / (float)(h - 1), 2.2f);
            for (int x = 0; x < 4; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0f), h);
    }
}
