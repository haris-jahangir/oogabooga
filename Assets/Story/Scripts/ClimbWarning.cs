using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In a level whose fire rises from the floor (Level 2), the moment the fire catches:
/// "THE FIRE IS RISING" / "CLIMB!" flashes on screen with a shake, so players know to go up, fast.
/// Installs itself in every such scene; nothing to wire.
/// </summary>
public class ClimbWarning : MonoBehaviour
{
    Fire fire;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        foreach (Fire f in FindObjectsByType<Fire>(FindObjectsSortMode.None))
            if (f.GrowVector.y > 0.5f)
            {
                new GameObject("Climb Warning").AddComponent<ClimbWarning>().fire = f;
                return;
            }
    }

    IEnumerator Start()
    {
        while (fire != null && !fire.enabled)
            yield return null;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        Text title = Label("THE FIRE IS RISING", Resources.Load<Font>("Fonts/Creepster-Regular"), 110, new Color(0.85f, 0.18f, 0.05f), 0.66f);
        Text climb = Label("CLIMB!", Resources.Load<Font>("Fonts/StardosStencil-Bold"), 80, new Color(1f, 0.85f, 0.55f), 0.54f);
        CameraFX.Shake(0.35f);

        // Flash for 3.5 s, then fade.
        for (float t = 0f; t < 4.5f; t += Time.deltaTime)
        {
            float a = t < 3.5f ? (Mathf.Sin(t * 9f) > -0.3f ? 1f : 0.35f) : 1f - (t - 3.5f);
            SetAlpha(title, a);
            SetAlpha(climb, a);
            yield return null;
        }
        Destroy(gameObject);
    }

    Text Label(string value, Font font, int size, Color color, float y)
    {
        var rt = new GameObject(value, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = new Vector2(0f, y - 0.07f);
        rt.anchorMax = new Vector2(1f, y + 0.07f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = color;
        t.text = value;
        rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
        return t;
    }

    static void SetAlpha(Graphic g, float a)
    {
        Color c = g.color;
        c.a = a;
        g.color = c;
    }
}
