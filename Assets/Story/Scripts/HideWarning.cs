using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A small "LIGHT!" flashing at the top of the screen while the shadow's warning plays
/// (the lights flickering). Installs itself in every level with a MonsterManager; nothing to wire.
/// </summary>
public class HideWarning : MonoBehaviour
{
    MonsterManager monster;
    Text label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        MonsterManager m = FindFirstObjectByType<MonsterManager>();
        if (m != null)
            new GameObject("Hide Warning").AddComponent<HideWarning>().monster = m;
    }

    void Start()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var rt = new GameObject("Light", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(600f, 80f);
        label = rt.gameObject.AddComponent<Text>();
        Font font = Resources.Load<Font>("Fonts/StardosStencil-Bold");
        label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 52;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.82f, 0.45f);
        label.text = "LIGHT!";   // get into the light: that's where the shadow can't reach
        rt.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        label.enabled = false;
    }

    void Update()
    {
        // Flashes in step with the flickering lights.
        label.enabled = monster != null && monster.InWarning && Mathf.Repeat(Time.time * 3f, 1f) < 0.7f;
    }
}
