using System.Collections;
using UnityEngine;

/// <summary>
/// Movement effects for the player: dust while running, puffs on jump and landing
/// (with a camera bump on hard landings), and the dash: pale smoky afterimages, white
/// streaks, a trail of smoke, a whoosh sound and a screen punch.
/// Added to the player automatically (see VfxBootstrap); tweak it in the Inspector.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerVFX : MonoBehaviour
{
    [Header("Dust")]
    [SerializeField] Color dustColor = new(0.55f, 0.5f, 0.45f, 0.5f);
    [Tooltip("Seconds between dust puffs while running.")]
    [SerializeField] float runDustInterval = 0.08f;

    [Header("Landing")]
    [Tooltip("Falling speed above which a landing bumps the camera.")]
    [SerializeField] float hardLandingSpeed = 9f;
    [SerializeField] AudioClip landSound;

    [Header("Dash")]
    [SerializeField] Color afterimageColor = new(0.92f, 0.92f, 0.95f, 0.4f);
    [Tooltip("Smoke left behind along the dash.")]
    [SerializeField] Color dashSmokeColor = new(0.75f, 0.75f, 0.78f, 0.45f);
    [Tooltip("Seconds between afterimages during a dash.")]
    [SerializeField] float afterimageInterval = 0.025f;
    [SerializeField] float afterimageFade = 0.25f;
    [SerializeField] AudioClip dashSound;
    [Range(0f, 1f)] [SerializeField] float dashVolume = 0.8f;

    PlayerMovement movement;
    SpriteRenderer sprite;
    Collider2D body;
    ParticleSystem dust, streaks;
    float nextDust;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        sprite = GetComponent<SpriteRenderer>();
        body = GetComponent<Collider2D>();
        if (landSound == null) landSound = Vfx.Sound("land");
        if (dashSound == null) dashSound = Vfx.Sound("dash");

        int order = sprite != null ? sprite.sortingOrder : 0;
        dust = Vfx.MakeSystem(transform, "Dust", Vfx.Alpha, order - 1);
        var main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = dustColor;
        main.gravityModifier = -0.05f;
        var col = dust.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(Color.white, Color.white);
        var size = dust.sizeOverLifetime; size.enabled = true; size.size = Vfx.Shrink(0.6f, 1.4f);
        var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.15f; shape.arc = 180f;

        streaks = Vfx.MakeSystem(transform, "Dash Streaks", Vfx.Additive, order + 1);
        var sm = streaks.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        sm.startColor = afterimageColor;
        var sc = streaks.colorOverLifetime; sc.enabled = true; sc.color = Vfx.Fade(Color.white, new Color(0.7f, 0.7f, 0.75f));
        var r = streaks.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.08f;
        r.lengthScale = 2f;
    }

    void OnEnable()
    {
        movement.Jumped += OnJump;
        movement.Landed += OnLand;
        movement.Dashed += OnDash;
    }

    void OnDisable()
    {
        movement.Jumped -= OnJump;
        movement.Landed -= OnLand;
        movement.Dashed -= OnDash;
    }

    Vector3 Feet => body != null
        ? new Vector3(body.bounds.center.x, body.bounds.min.y + 0.05f, 0f)
        : transform.position;

    void Update()
    {
        if (movement.IsGrounded && !movement.IsDashing && Mathf.Abs(movement.Velocity.x) > 2f && Time.time >= nextDust)
        {
            nextDust = Time.time + runDustInterval;
            Puff(1, -Mathf.Sign(movement.Velocity.x));
        }
    }

    // Emits dust at the feet, kicked sideways (direction -1/1) or both ways (0).
    void Puff(int count, float direction)
    {
        var p = new ParticleSystem.EmitParams { applyShapeToPosition = true };
        for (int i = 0; i < count; i++)
        {
            float dir = direction != 0f ? direction : (i % 2 == 0 ? -1f : 1f);
            p.position = Feet;
            p.velocity = new Vector3(dir * Random.Range(0.5f, 2f), Random.Range(0.1f, 0.8f), 0f);
            dust.Emit(p, 1);
        }
    }

    void OnJump() => Puff(6, 0f);

    void OnLand(float impactSpeed)
    {
        if (impactSpeed < 3f)
            return;
        Puff(Mathf.Clamp(Mathf.RoundToInt(impactSpeed), 4, 14), 0f);
        if (impactSpeed >= hardLandingSpeed)
        {
            CameraFX.Shake(0.25f);
            Vfx.Play2D(landSound, 0.7f);
        }
    }

    void OnDash(float direction)
    {
        Vfx.Play2D(dashSound, dashVolume, Random.Range(0.95f, 1.05f));
        CameraFX.Punch();
        CameraFX.Shake(0.15f);

        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < 18; i++)
        {
            p.position = body != null ? body.bounds.center + (Vector3)Random.insideUnitCircle * 0.3f : transform.position;
            p.velocity = new Vector3(-direction * Random.Range(2f, 7f), Random.Range(-0.6f, 0.6f), 0f);
            streaks.Emit(p, 1);
        }
        if (movement.IsGrounded)
            Puff(8, -direction);
        StartCoroutine(Afterimages());
    }

    IEnumerator Afterimages()
    {
        // Wait for the dash to actually begin in the next physics step.
        yield return new WaitForFixedUpdate();
        while (movement.IsDashing)
        {
            SpawnAfterimage();
            DashSmoke();
            yield return new WaitForSeconds(afterimageInterval);
        }
    }

    // Soft puffs of smoke that hang in the air where the player just was.
    void DashSmoke()
    {
        var p = new ParticleSystem.EmitParams
        {
            startColor = dashSmokeColor,
            startSize = Random.Range(0.35f, 0.7f),
            startLifetime = Random.Range(0.5f, 0.9f),
        };
        for (int i = 0; i < 2; i++)
        {
            p.position = (body != null ? body.bounds.center : transform.position) + (Vector3)(Random.insideUnitCircle * 0.25f);
            p.velocity = new Vector3(-movement.Facing * Random.Range(0.2f, 0.8f), Random.Range(0f, 0.4f), 0f);
            dust.Emit(p, 1);
        }
    }

    void SpawnAfterimage()
    {
        if (sprite == null || sprite.sprite == null)
            return;
        var go = new GameObject("Afterimage");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite.sprite;
        sr.flipX = sprite.flipX;
        sr.flipY = sprite.flipY;
        sr.sharedMaterial = Vfx.UnlitSprite;
        sr.sortingLayerID = sprite.sortingLayerID;
        sr.sortingOrder = sprite.sortingOrder - 1;
        sr.color = afterimageColor;
        go.AddComponent<FadeAndDie>().duration = afterimageFade;
    }
}
