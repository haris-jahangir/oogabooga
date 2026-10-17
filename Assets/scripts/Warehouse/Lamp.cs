using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A hanging warehouse lamp (a 2D spot light). It stays off until the switch is used,
/// then flickers on after a short random delay and hums with a slight flicker.
/// In Play mode it also draws a soft beam of light, so you can see where the light comes from.
/// SceneAtmosphere finds every Lamp in the scene. Tools > Oogabooga > Place Spot Lights adds them,
/// or put this on any Light2D yourself.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class Lamp : MonoBehaviour
{
    [Tooltip("Brightness when on (before SceneAtmosphere's Lamp Brightness).")]
    public float intensity = 1.2f;
    [Tooltip("Constant slight flicker while on (0 = steady).")]
    [Range(0f, 0.5f)] public float hum = 0.06f;
    [Tooltip("This lamp sometimes cuts out for a moment.")]
    public bool faulty;

    /// <summary>Scales every lamp at once; the monster's warning flicker drives this.</summary>
    public static float GlobalLevel = 1f;
    /// <summary>Extra flicker from the building shaking (CollapseFX drives this).</summary>
    public static float TremorLevel = 1f;
    /// <summary>Overall lamp strength, set by SceneAtmosphere.</summary>
    public static float Brightness = 1f;
    /// <summary>How visible the light beams are (0 = no beam), set by SceneAtmosphere.</summary>
    public static float BeamOpacity = 0.16f;

    // Domain reload is disabled in this project, so statics survive between Play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        GlobalLevel = 1f;
        TremorLevel = 1f;
        Brightness = 1f;
        BeamOpacity = 0.16f;
    }

    static Sprite beamSprite;

    Light2D light2D;
    SpriteRenderer beam;
    bool on;
    float seed;

    void Awake()
    {
        light2D = GetComponent<Light2D>();
        seed = Random.value * 100f;
    }

    void Start() => MakeVisuals();

    /// <summary>Turn the lamp off straight away (before the switch).</summary>
    public void Off()
    {
        StopAllCoroutines();
        on = false;
        Show(0f);
    }

    /// <summary>Flicker on after the delay, like a tube light catching.</summary>
    public void TurnOn(float delay) => StartCoroutine(Catch(delay));

    IEnumerator Catch(float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (float blink in new[] { 0.06f, 0.08f, 0.05f })
        {
            Show(Random.Range(0.4f, 1f));
            yield return new WaitForSeconds(blink);
            Show(0f);
            yield return new WaitForSeconds(blink * 1.5f);
        }
        on = true;
    }

    void Update()
    {
        if (!on)
            return;
        float n = Mathf.PerlinNoise(seed, Time.time * 8f);
        float level = 1f - hum + hum * 2f * n;
        // A faulty lamp drops out briefly now and then.
        if (faulty && Mathf.PerlinNoise(seed + 10f, Time.time * 0.7f) > 0.78f)
            level *= 0.1f;
        Show(level * GlobalLevel * TremorLevel);
    }

    // Sets the light and its beam together (level 0 = off, 1 = full).
    void Show(float level)
    {
        light2D.intensity = intensity * Brightness * level;
        ShowVisuals(Mathf.Clamp01(level));
    }

    void ShowVisuals(float a)
    {
        if (beam != null)
            beam.color = new Color(light2D.color.r, light2D.color.g, light2D.color.b, BeamOpacity * a);
    }

    void MakeVisuals()
    {
        if (BeamOpacity <= 0f)
            return;

        // The beam: a soft cone the same size and angle as the light, pointing the way the light does.
        float range = light2D.pointLightOuterRadius;
        float angle = Mathf.Clamp(light2D.pointLightOuterAngle, 10f, 170f);
        float width = 2f * Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad) * range;
        var beamGo = new GameObject("Beam");
        beamGo.transform.SetParent(transform, false);
        beam = beamGo.AddComponent<SpriteRenderer>();
        beam.sprite = beamSprite != null ? beamSprite : beamSprite = MakeBeamSprite();
        Vector2 spriteSize = beam.sprite.bounds.size;
        beamGo.transform.localScale = new Vector3(width / spriteSize.x, range / spriteSize.y, 1f);
        beam.sharedMaterial = Vfx.UnlitSprite;
        beam.sortingOrder = -3;

        // Match whatever the light is doing right now (SceneAtmosphere may already have switched it off).
        ShowVisuals(light2D.intensity > 0f ? 1f : 0f);
    }

    // A cone that starts at the bottom middle and widens upwards (the lamp's local up is the
    // direction it shines), brightest a little way out and in the middle, fading towards the
    // edges and the far end. It fades in right at the lamp so there's no bright blob there.
    static Sprite MakeBeamSprite()
    {
        const int w = 64, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = (y + 0.5f) / h;                         // 0 at the lamp .. 1 at the far end
                float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);    // 0 centre .. 1 edge of the texture
                float edge = u / Mathf.Max(v, 0.02f);              // 0 centre .. 1 edge of the cone
                float a = Mathf.Clamp01(1f - edge);
                a = a * a * (1f - v) * (1f - v) * Mathf.SmoothStep(0f, 1f, v / 0.2f);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), h, 0, SpriteMeshType.FullRect);
    }
}
