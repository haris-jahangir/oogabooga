using UnityEngine;

/// <summary>
/// Fear effects around the monster:
///   - Warning: the screen shakes and the edges darken while the warning plays.
///   - Hiding while the monster is near: the camera slowly zooms in, the edges close in and
///     a heartbeat thumps, faster and louder the closer the monster is.
/// Added to the Main Camera automatically (see VfxBootstrap).
/// </summary>
public class TensionFX : MonoBehaviour
{
    [Header("Warning")]
    [Tooltip("Screen shake during the warning (0..1).")]
    [Range(0f, 1f)] [SerializeField] float warningShake = 0.45f;
    [Range(0f, 1f)] [SerializeField] float warningVignette = 0.2f;

    [Header("Hiding")]
    [Tooltip("Distance at which the monster starts to feel close.")]
    [SerializeField] float nearDistance = 7f;
    [Tooltip("Zoom when the monster is right next to you (1 = none, 1.3 = 30% closer).")]
    [SerializeField] float maxZoom = 1.3f;
    [Range(0f, 1f)] [SerializeField] float hidingVignette = 0.4f;

    [Header("Heartbeat")]
    [SerializeField] AudioClip heartbeat;
    [Range(0f, 1f)] [SerializeField] float heartbeatVolume = 0.9f;
    [Tooltip("Heartbeat speed when the monster is far / right next to you.")]
    [SerializeField] float calmPitch = 0.9f;
    [SerializeField] float panicPitch = 1.6f;
    [Tooltip("Heartbeat volume during the warning, before the monster arrives.")]
    [Range(0f, 1f)] [SerializeField] float warningHeartbeat = 0.35f;

    MonsterManager monsters;
    Transform player;
    AudioSource heart;
    float fear; // 0..1, smoothed

    void Start()
    {
        monsters = FindFirstObjectByType<MonsterManager>();
        PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
        if (p != null)
            player = p.transform;

        if (heartbeat == null)
            heartbeat = Vfx.Sound("heartbeat");
        if (heartbeat != null)
        {
            heart = gameObject.AddComponent<AudioSource>();
            heart.clip = heartbeat;
            heart.loop = true;
            heart.spatialBlend = 0f;
            heart.volume = 0f;
        }
    }

    void Update()
    {
        bool warning = monsters != null && monsters.InWarning;
        CameraFX.HoldShake(this, warning ? warningShake : 0f);

        // How close the monster is while the player hides (0 far .. 1 touching).
        float closeness = 0f;
        if (monsters != null && monsters.MonsterPresent && player != null && monsters.Monster != null && HidingSpot.IsPlayerHidden)
        {
            float d = Vector2.Distance(monsters.Monster.position, player.position);
            closeness = 1f - Mathf.Clamp01(d / nearDistance);
            closeness = Mathf.Max(closeness, 0.25f); // it's here at all: some fear
        }

        float target = Mathf.Max(closeness, warning ? 0.15f : 0f);
        fear = Mathf.MoveTowards(fear, target, Time.deltaTime * (target > fear ? 1.5f : 0.6f));

        CameraFX.HoldZoom(this, Mathf.Lerp(1f, maxZoom, closeness > 0f ? fear : 0f));
        CameraFX.HoldVignette(this, Mathf.Max(warning ? warningVignette : 0f, hidingVignette * (closeness > 0f ? fear : 0f)));

        if (heart != null)
        {
            float volume = closeness > 0f ? heartbeatVolume * Mathf.Lerp(0.5f, 1f, fear) : (warning ? warningHeartbeat : 0f);
            heart.volume = Mathf.MoveTowards(heart.volume, volume, Time.deltaTime * 1.5f);
            heart.pitch = Mathf.Lerp(calmPitch, panicPitch, fear);
            if (heart.volume > 0.001f && !heart.isPlaying)
                heart.Play();
            else if (heart.volume <= 0.001f && heart.isPlaying)
                heart.Stop();
        }
    }

    void OnDisable()
    {
        CameraFX.HoldShake(this, 0f);
        CameraFX.HoldZoom(this, 1f);
        CameraFX.HoldVignette(this, 0f);
    }
}
