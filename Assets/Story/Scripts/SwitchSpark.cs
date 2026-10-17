using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// The moment the switch is used: a beat after the lamps catch, the wiring sparks,
/// the screen flashes hot and the camera shakes. Listens for LevelStarter.Started,
/// so it needs no wiring beyond pointing Spark Point at the switch.
/// </summary>
[DefaultExecutionOrder(1000)] // shake is added after CameraFollow has placed the camera
public class SwitchSpark : MonoBehaviour
{
    [Tooltip("Where the sparks fly from: the switch.")]
    public Transform sparkPoint;
    [Tooltip("Optional: an electric crack / pop.")]
    public AudioClip sparkSound;
    [Tooltip("Seconds after the switch before it sparks.")]
    public float delay = 0.6f;
    public int sparkCount = 18;
    public float shakeStrength = 0.3f;
    public float shakeSeconds = 0.6f;
    [Tooltip("The switch's sprite after it's used (Switch_On: lever down, scorched). Leave empty to keep it.")]
    public Sprite usedSprite;
    [Tooltip("An unlit sprite material, so sparks glow in the dark (URP's Sprite-Unlit-Default).")]
    public Material sparkMaterial;

    Transform cam;
    Vector3 shakeOffset;
    float shakeUntil;
    static Sprite dot;

    void Start()
    {
        if (Camera.main != null)
            cam = Camera.main.transform;
        if (sparkPoint == null)
        {
            GameObject sw = GameObject.Find("switch");
            if (sw != null)
                sparkPoint = sw.transform;
        }
        LevelStarter starter = FindFirstObjectByType<LevelStarter>();
        if (starter != null)
            starter.Started += () => StartCoroutine(Spark());
    }

    IEnumerator Spark()
    {
        yield return new WaitForSeconds(delay);
        Vector3 at = sparkPoint != null ? sparkPoint.position : transform.position;
        if (sparkSound != null)
            AudioSource.PlayClipAtPoint(sparkSound, cam != null ? cam.position : at);

        if (usedSprite != null && sparkPoint != null && sparkPoint.TryGetComponent(out SpriteRenderer switchSprite))
            switchSprite.sprite = usedSprite;
        StartCoroutine(FlashLight(at));
        StartCoroutine(ScreenFlash());
        for (int i = 0; i < sparkCount; i++)
            StartCoroutine(Fly(at));
        shakeUntil = Time.time + shakeSeconds;
    }

    // Take the last frame's shake off before CameraFollow moves the camera, so its smoothing never sees it.
    void Update()
    {
        if (cam != null)
            cam.position -= shakeOffset;
        shakeOffset = Vector3.zero;
    }

    void LateUpdate()
    {
        if (cam == null || Time.time >= shakeUntil)
            return;
        float k = (shakeUntil - Time.time) / shakeSeconds;
        shakeOffset = (Vector3)(Random.insideUnitCircle * shakeStrength * k);
        cam.position += shakeOffset;
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

    // One spark: shoots out, falls, fades.
    IEnumerator Fly(Vector3 at)
    {
        var go = new GameObject("Spark");
        go.transform.position = at;
        go.transform.localScale = Vector3.one * Random.Range(0.05f, 0.11f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Dot();
        if (sparkMaterial != null)
            sr.sharedMaterial = sparkMaterial;
        sr.sortingOrder = 200;
        Vector2 v = new Vector2(Random.Range(-3.5f, 3.5f), Random.Range(1.5f, 5f));
        float life = Random.Range(0.35f, 0.8f);
        for (float t = 0f; t < life; t += Time.deltaTime)
        {
            v.y -= 12f * Time.deltaTime;
            go.transform.position += (Vector3)(v * Time.deltaTime);
            sr.color = Color.Lerp(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.35f, 0.05f, 0f), t / life);
            yield return null;
        }
        Destroy(go);
    }

    static Sprite Dot()
    {
        if (dot != null)
            return dot;
        var tex = new Texture2D(8, 8);
        var px = new Color[64];
        for (int i = 0; i < 64; i++)
            px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        dot = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        return dot;
    }
}
