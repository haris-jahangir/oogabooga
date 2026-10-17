using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Screen effects on the Main Camera: shake, zoom, and post-processing pulses
/// (vignette, chromatic aberration, lens distortion). Other scripts call the static methods.
/// Works alongside CameraFollow: the shake is removed before CameraFollow moves the camera
/// and added back after, so following stays smooth.
/// </summary>
[DefaultExecutionOrder(1000)] // LateUpdate after CameraFollow
[RequireComponent(typeof(Camera))]
public class CameraFX : MonoBehaviour
{
    [Header("Shake")]
    [Tooltip("Biggest shake offset, in units.")]
    [SerializeField] float maxShake = 0.35f;
    [Tooltip("How fast the camera jitters.")]
    [SerializeField] float shakeFrequency = 22f;
    [Tooltip("How fast a shake dies down (per second).")]
    [SerializeField] float shakeRecovery = 1.6f;

    [Header("Zoom")]
    [Tooltip("How quickly the camera eases to a new zoom.")]
    [SerializeField] float zoomSpeed = 3f;

    [Header("Post-processing")]
    [Tooltip("Dark edges that are always there, for mood.")]
    [Range(0f, 1f)] [SerializeField] float baseVignette = 0.25f;

    static CameraFX instance;

    Camera cam;
    float baseSize;
    float trauma;
    readonly Dictionary<object, float> shakeHolds = new();
    readonly Dictionary<object, float> zoomHolds = new();
    readonly Dictionary<object, float> vignetteHolds = new();
    float zoomPunch, chromaPulse, distortPulse;
    Vector3 appliedOffset;
    float seed;

    Vignette vignette;
    ChromaticAberration chroma;
    LensDistortion distortion;

    // Domain reload is disabled in this project, so statics survive between Play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
        baseSize = cam.orthographicSize;
        seed = Random.value * 100f;
        SetUpPostProcessing();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // ---------- API ----------

    /// <summary>A one-off kick of shake (0..1, adds up and dies down).</summary>
    public static void Shake(float amount)
    {
        if (instance != null)
            instance.trauma = Mathf.Clamp01(instance.trauma + amount);
    }

    /// <summary>Keep the screen shaking at least this much (0..1) until cleared with 0.</summary>
    public static void HoldShake(object key, float amount) => Set(instance != null ? instance.shakeHolds : null, key, amount);

    /// <summary>Zoom in (above 1) or out (below 1) while held; 1 or 0 clears it.</summary>
    public static void HoldZoom(object key, float zoom) => Set(instance != null ? instance.zoomHolds : null, key, zoom == 1f ? 0f : zoom);

    /// <summary>Extra dark edges (0..1) while held; 0 clears it.</summary>
    public static void HoldVignette(object key, float amount) => Set(instance != null ? instance.vignetteHolds : null, key, amount);

    /// <summary>A quick screen "whoosh": zoom kick, colour fringing and lens bend that snap back.</summary>
    public static void Punch(float zoom = 0.06f, float chromatic = 0.8f, float lens = -0.35f)
    {
        if (instance == null)
            return;
        instance.zoomPunch = zoom;
        instance.chromaPulse = Mathf.Max(instance.chromaPulse, chromatic);
        instance.distortPulse = lens;
    }

    static void Set(Dictionary<object, float> holds, object key, float value)
    {
        if (holds == null)
            return;
        if (value <= 0f)
            holds.Remove(key);
        else
            holds[key] = value;
    }

    // ---------- Update ----------

    void Update()
    {
        // Take last frame's shake out so CameraFollow works from the real position.
        transform.position -= appliedOffset;
        appliedOffset = Vector3.zero;
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        trauma = Mathf.Max(0f, trauma - shakeRecovery * dt);
        float shake = trauma;
        foreach (float h in shakeHolds.Values)
            shake = Mathf.Max(shake, h);

        if (shake > 0f)
        {
            float t = Time.unscaledTime * shakeFrequency;
            float power = shake * shake * maxShake; // squared: small values stay subtle
            appliedOffset = new Vector3(
                (Mathf.PerlinNoise(seed, t) * 2f - 1f) * power,
                (Mathf.PerlinNoise(seed + 7f, t) * 2f - 1f) * power, 0f);
            transform.position += appliedOffset;
        }

        // Zoom: held zooms multiply; a punch is a brief zoom-out that springs back.
        float zoom = 1f;
        foreach (float z in zoomHolds.Values)
            zoom *= z;
        zoomPunch = Mathf.MoveTowards(zoomPunch, 0f, dt * 0.4f);
        float targetSize = baseSize / zoom * (1f + zoomPunch);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, 1f - Mathf.Exp(-zoomSpeed * dt));

        chromaPulse = Mathf.MoveTowards(chromaPulse, 0f, dt * 3f);
        distortPulse = Mathf.MoveTowards(distortPulse, 0f, dt * 2f);
        if (chroma != null)
            chroma.intensity.value = chromaPulse;
        if (distortion != null)
            distortion.intensity.value = distortPulse;
        if (vignette != null)
        {
            float extra = 0f;
            foreach (float v in vignetteHolds.Values)
                extra = Mathf.Max(extra, v);
            vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, Mathf.Clamp01(baseVignette + extra), 1f - Mathf.Exp(-4f * dt));
        }
    }

    void SetUpPostProcessing()
    {
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        if (data != null)
            data.renderPostProcessing = true;

        var volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

        vignette = volume.profile.Add<Vignette>(true);
        vignette.intensity.Override(baseVignette);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(Color.black);

        chroma = volume.profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0f);

        distortion = volume.profile.Add<LensDistortion>(true);
        distortion.intensity.Override(0f);
    }
}
