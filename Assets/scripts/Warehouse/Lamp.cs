using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// A hanging warehouse lamp (a 2D spot light). It stays off until the switch is used,
/// then flickers on after a short random delay and hums with a slight flicker.
/// SceneAtmosphere finds every Lamp in the scene. Tools > Oogabooga > Place Spot Lights adds them,
/// or put this on any Light2D yourself.
/// </summary>
[RequireComponent(typeof(Light2D))]
public class Lamp : MonoBehaviour
{
    [Tooltip("Brightness when on.")]
    public float intensity = 1.2f;
    [Tooltip("Constant slight flicker while on (0 = steady).")]
    [Range(0f, 0.5f)] public float hum = 0.06f;
    [Tooltip("This lamp sometimes cuts out for a moment.")]
    public bool faulty;

    /// <summary>Scales every lamp at once; the monster's warning flicker drives this.</summary>
    public static float GlobalLevel = 1f;

    // Domain reload is disabled in this project, so statics survive between Play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => GlobalLevel = 1f;

    Light2D light2D;
    bool on;
    float seed;

    void Awake()
    {
        light2D = GetComponent<Light2D>();
        seed = Random.value * 100f;
    }

    /// <summary>Turn the lamp off straight away (before the switch).</summary>
    public void Off()
    {
        StopAllCoroutines();
        on = false;
        light2D.intensity = 0f;
    }

    /// <summary>Flicker on after the delay, like a tube light catching.</summary>
    public void TurnOn(float delay) => StartCoroutine(Catch(delay));

    IEnumerator Catch(float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (float blink in new[] { 0.06f, 0.08f, 0.05f })
        {
            light2D.intensity = intensity * Random.Range(0.4f, 1f);
            yield return new WaitForSeconds(blink);
            light2D.intensity = 0f;
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
        light2D.intensity = intensity * level * GlobalLevel;
    }
}
