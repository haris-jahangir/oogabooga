using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "CODE  3 _ _ _" in the top-left corner of a level with a keypad, filling in as the player finds
/// each clue: the newspaper (1st digit), the red digit painted on the wall (2nd, found by walking
/// up to it), the phone (3rd) and the bill (4th). Shown once the switch is on. Found digits are
/// kept through Try Again and cleared on the way back to the title. Installs itself; nothing to wire.
/// </summary>
public class CodeTracker : MonoBehaviour
{
    // Which digit each clue's picture gives (the clue art marks its own position).
    static readonly (string picture, int digit)[] ClueDigits = { ("Clue_Newspaper", 0), ("Clue_Phone", 2), ("Clue_Bill", 3) };
    const string WallDigitName = "Clue_WallDigit";
    const int WallDigit = 1;
    const float WallDigitRange = 4f;

    static bool[] found = new bool[0];
    static string foundCode = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        found = new bool[0];
        foundCode = "";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.buildIndex == 0)
            ResetStatics(); // back at the title: a new run starts from nothing
        KeypadLock keypad = FindFirstObjectByType<KeypadLock>();
        if (keypad == null)
            return;
        if (foundCode != keypad.Code)
        {
            foundCode = keypad.Code;
            found = new bool[keypad.Code.Length];
        }
        new GameObject("Code Tracker").AddComponent<CodeTracker>();
    }

    Text label;
    Transform player, wallDigit;
    LevelStarter starter;
    int flash = -1;
    float flashUntil;

    void Start()
    {
        PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
        player = p != null ? p.transform : null;
        foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            if (sr.name.StartsWith(WallDigitName))
            {
                wallDigit = sr.transform;
                break;
            }
        starter = FindFirstObjectByType<LevelStarter>();
        ClueViewer.Opened += OnClue;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var rt = new GameObject("Code", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(40f, -30f);
        rt.sizeDelta = new Vector2(700f, 80f);
        label = rt.gameObject.AddComponent<Text>();
        Font font = Resources.Load<Font>("Fonts/SpecialElite-Regular");
        label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 46;
        label.supportRichText = true;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.color = new Color(0.86f, 0.84f, 0.79f);
        rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        Refresh();
    }

    void OnDestroy() => ClueViewer.Opened -= OnClue;

    void OnClue(ClueViewer clue)
    {
        if (clue.Picture == null)
            return;
        foreach (var (picture, digit) in ClueDigits)
            if (clue.Picture.name.StartsWith(picture))
                Reveal(digit);
    }

    void Reveal(int digit)
    {
        if (digit >= found.Length || found[digit])
            return;
        found[digit] = true;
        flash = digit;
        flashUntil = Time.time + 2.5f;
    }

    void Update()
    {
        // The red digit on the wall counts once the player has walked up to it.
        if (wallDigit != null && player != null && Vector2.Distance(player.position, wallDigit.position) <= WallDigitRange)
            Reveal(WallDigit);
        if (flash >= 0 && Time.time > flashUntil)
            flash = -1;
        Refresh();
    }

    void Refresh()
    {
        label.enabled = starter == null || starter.HasStarted;
        var s = new System.Text.StringBuilder("CODE  ");
        for (int i = 0; i < found.Length; i++)
        {
            string d = found[i] ? foundCode[i].ToString() : "_";
            // A newly found digit glows warm for a moment.
            s.Append(i == flash && Mathf.Repeat(Time.time * 4f, 1f) < 0.6f ? $"<color=#ffb347>{d}</color>" : d);
            if (i < found.Length - 1)
                s.Append(' ');
        }
        label.text = s.ToString();
    }
}
