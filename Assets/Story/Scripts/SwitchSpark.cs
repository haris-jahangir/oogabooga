using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// The moment the switch is used: the switch flips to its scorched "on" picture and the room
/// flashes hot. The sparks, smoke and camera shake come from SwitchVFX. Listens for
/// LevelStarter.Started, so it needs no wiring beyond pointing Spark Point at the switch.
/// </summary>
public class SwitchSpark : MonoBehaviour
{
    [Tooltip("The switch.")]
    public Transform sparkPoint;
    [Tooltip("Seconds after the switch before it flashes.")]
    public float delay = 0.6f;
    [Tooltip("The switch's sprite after it's used (Switch_On: lever down, scorched). Leave empty to keep it.")]
    public Sprite usedSprite;

    void Start()
    {
        if (sparkPoint == null)
        {
            GameObject sw = GameObject.Find("switch");
            if (sw != null)
                sparkPoint = sw.transform;
        }
        LevelStarter starter = FindFirstObjectByType<LevelStarter>();
        if (starter != null)
            starter.Started += () => StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        yield return new WaitForSeconds(delay);
        Vector3 at = sparkPoint != null ? sparkPoint.position : transform.position;
        if (usedSprite != null && sparkPoint != null && sparkPoint.TryGetComponent(out SpriteRenderer switchSprite))
            switchSprite.sprite = usedSprite;
        StartCoroutine(FlashLight(at));
        StartCoroutine(ScreenFlash());
    }

    IEnumerator FlashLight(Vector3 at)
    {
        var go = new GameObject("Spark Light");
        go.transform.position = at;
        Light2D light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(1f, 0.75f, 0.35f);
        light.pointLightOuterRadius = 5f;
        light.pointLightInnerRadius = 0.5f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f)
        {
            light.intensity = Mathf.Lerp(4f, 0f, t) * Random.Range(0.6f, 1f);
            yield return null;
        }
        Destroy(go);
    }

    IEnumerator ScreenFlash()
    {
        var go = new GameObject("Spark Flash");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var rt = new GameObject("Flash", typeof(RectTransform)).GetComponent<RectTransform>();
        rt.SetParent(go.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        Image img = rt.gameObject.AddComponent<Image>();
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
        {
            img.color = new Color(1f, 0.85f, 0.6f, Mathf.Lerp(0.45f, 0f, t));
            yield return null;
        }
        Destroy(go);
    }
}
