using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shown when the player dies (caught by the shadow or burned): the screen sinks to dark red,
/// GAME OVER, and TRY AGAIN (restart this level) or MAIN MENU. W/S or arrows to choose,
/// Enter / E / Space or a click to pick. Call GameOverScreen.Show(); it builds itself.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    /// <summary>True right after TRY AGAIN, so the level skips its opening (WakeUpIntro reads it).</summary>
    public static bool Retrying;

    // Domain reload is disabled in this project, so statics survive between Play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Retrying = false;

    static readonly string[] Choices = { "TRY AGAIN", "MAIN MENU" };

    Text[] options;
    int selected;
    bool chosen;

    public static void Show()
    {
        if (FindFirstObjectByType<GameOverScreen>() == null)
            new GameObject("Game Over").AddComponent<GameOverScreen>();
    }

    IEnumerator Start()
    {
        Interactable.Blocked = true;
        Cursor.visible = true;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Font title = Resources.Load<Font>("Fonts/Creepster-Regular");
        Font words = Resources.Load<Font>("Fonts/SpecialElite-Regular");
        Font menu = Resources.Load<Font>("Fonts/StardosStencil-Bold");

        Image dim = Panel(new Color(0.08f, 0f, 0f, 0f));
        Text big = Label("GAME OVER", title, 180, new Color(0.6f, 0.05f, 0.04f, 0f), 0.62f);
        Text sub = Label("You didn't make it out.", words, 40, new Color(0.85f, 0.82f, 0.76f, 0f), 0.48f);
        options = new Text[Choices.Length];
        for (int i = 0; i < Choices.Length; i++)
            options[i] = Label(Choices[i], menu, 52, Color.clear, 0.32f - i * 0.09f);

        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.2f)
        {
            dim.color = new Color(0.08f, 0f, 0f, 0.88f * t);
            SetAlpha(big, t);
            SetAlpha(sub, t);
            yield return null;
        }
        Select(0);
    }

    void Update()
    {
        if (chosen || options == null || options[0].color.a == 0f)
            return;
        Keyboard k = Keyboard.current;
        if (k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame))
            Select(1 - selected);

        int hovered = -1;
        if (Mouse.current != null)
            for (int i = 0; i < options.Length; i++)
                if (RectTransformUtility.RectangleContainsScreenPoint(options[i].rectTransform, Mouse.current.position.ReadValue()))
                    hovered = i;
        if (hovered >= 0 && hovered != selected)
            Select(hovered);

        bool confirm = k != null && (k.enterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                    || hovered >= 0 && Mouse.current.leftButton.wasPressedThisFrame;
        if (!confirm)
            return;
        chosen = true;
        Interactable.Blocked = false;
        Retrying = selected == 0;
        SceneManager.LoadScene(selected == 0 ? SceneManager.GetActiveScene().buildIndex : 0);
    }

    void Select(int i)
    {
        selected = i;
        for (int o = 0; o < options.Length; o++)
        {
            options[o].color = o == i ? new Color(0.92f, 0.82f, 0.66f) : new Color(0.35f, 0.3f, 0.28f);
            options[o].text = o == i ? "> " + Choices[o] + " <" : Choices[o];
        }
    }

    Image Panel(Color color)
    {
        var rt = new GameObject("Dim", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    Text Label(string value, Font font, int size, Color color, float y)
    {
        var rt = new GameObject(value, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(0f, y - 0.06f);
        rt.anchorMax = new Vector2(1f, y + 0.06f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = color;
        t.text = value;
        rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return t;
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
