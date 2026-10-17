using System.Collections;
using UnityEngine;

/// <summary>
/// The building is coming down: every so often a deep rumble rolls through, the screen shakes,
/// the lights stutter, rubble and dust rain from above bouncing off ledges, ledges crumble at
/// their undersides, lamps spit sparks and chunks of concrete come crashing down. Between
/// tremors grit keeps trickling from the ceiling. Tremors come more often as time goes on.
/// Starts with the level (LevelStarter), or straight away if there isn't one.
/// Put it on any object in the scene (the Scene 2 climb builder adds it).
/// </summary>
public class CollapseFX : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] float firstTremorDelay = 4f;
    [SerializeField] float minInterval = 7f;
    [SerializeField] float maxInterval = 14f;
    [Tooltip("Seconds until tremors come twice as often.")]
    [SerializeField] float worsensOver = 120f;

    [Header("Tremor")]
    [Tooltip("Strongest jolt of a tremor (0..1).")]
    [Range(0f, 1f)] [SerializeField] float shake = 0.55f;
    [Tooltip("Rolling shake held during a tremor (0..1).")]
    [Range(0f, 1f)] [SerializeField] float rumbleShake = 0.3f;
    [SerializeField] float tremorDuration = 2.2f;

    [Header("Rubble")]
    [SerializeField] int rubblePerBurst = 14;
    [SerializeField] int bursts = 3;
    [SerializeField] Color rubbleColorA = new(0.3f, 0.28f, 0.26f);
    [SerializeField] Color rubbleColorB = new(0.5f, 0.42f, 0.34f);
    [SerializeField] Color dustColor = new(0.55f, 0.5f, 0.45f, 0.35f);

    [Header("Falling Apart")]
    [Tooltip("Big concrete chunks that crash down during each tremor (they never hit the player).")]
    [SerializeField] int slabsPerTremor = 2;
    [SerializeField] Vector2 slabSize = new(0.35f, 0.9f);
    [Tooltip("Ledges on screen shed bits from their undersides during tremors.")]
    [SerializeField] bool crumbleLedges = true;
    [Tooltip("A lamp on screen spits sparks during tremors.")]
    [SerializeField] bool lampSparks = true;
    [Tooltip("Lights stutter while the building shakes.")]
    [SerializeField] bool flickerLights = true;
    [Tooltip("Seconds between little trickles of grit from the ceiling between tremors (0 = off).")]
    [SerializeField] float gritInterval = 1.3f;

    [Header("Sounds")]
    [SerializeField] AudioClip rumbleSound;
    [SerializeField] AudioClip rubbleSound;
    [Range(0f, 1f)] [SerializeField] float volume = 0.8f;

    ParticleSystem rubble, dust, sparks;
    Collider2D roof;
    Collider2D playerCollider;
    float startTime;
    bool running;
    float nextGrit;
    static Sprite slabSprite;

    void Start()
    {
        if (rumbleSound == null) rumbleSound = Vfx.Sound("rumble");
        if (rubbleSound == null) rubbleSound = Vfx.Sound("rubble");

        GameObject roofObject = GameObject.Find("Roof");
        if (roofObject != null)
            roof = roofObject.GetComponent<Collider2D>();
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            playerCollider = player.GetComponent<Collider2D>();
        MakeParticles();

        LevelStarter starter = FindFirstObjectByType<LevelStarter>();
        if (starter != null && !starter.HasStarted)
            starter.Started += Begin;
        else
            Begin();
    }

    void Begin()
    {
        startTime = Time.time;
        running = true;
        StartCoroutine(Loop());
    }

    IEnumerator Loop()
    {
        yield return new WaitForSeconds(firstTremorDelay);
        while (true)
        {
            yield return Tremor();
            float worse = Mathf.Lerp(1f, 0.5f, (Time.time - startTime) / Mathf.Max(1f, worsensOver));
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval) * worse);
        }
    }

    IEnumerator Tremor()
    {
        Vfx.Play2D(rumbleSound, volume, Random.Range(0.9f, 1.1f));
        CameraFX.HoldShake(this, rumbleShake);
        if (flickerLights)
            StartCoroutine(FlickerLamps(tremorDuration));
        if (lampSparks)
            SparkALamp();
        float gap = tremorDuration / Mathf.Max(1, bursts);
        for (int i = 0; i < bursts; i++)
        {
            CameraFX.Shake(shake * Random.Range(0.6f, 1f));
            DropRubble();
            if (crumbleLedges)
                CrumbleALedge();
            if (i < slabsPerTremor)
                DropSlab();
            yield return new WaitForSeconds(gap);
        }
        CameraFX.HoldShake(this, 0f);
    }

    void OnDisable()
    {
        CameraFX.HoldShake(this, 0f);
        Lamp.TremorLevel = 1f;
    }

    void Update()
    {
        // Between tremors the ceiling keeps shedding a little grit.
        if (!running || gritInterval <= 0f || Time.time < nextGrit)
            return;
        nextGrit = Time.time + gritInterval * Random.Range(0.5f, 1.5f);
        if (!TopOfView(out float x, out float y, out float halfW))
            return;
        var p = new ParticleSystem.EmitParams();
        float gx = x + Random.Range(-halfW, halfW);
        for (int i = 0; i < 4; i++)
        {
            p.position = new Vector3(gx + Random.Range(-0.3f, 0.3f), y, 0f);
            p.velocity = new Vector3(0f, Random.Range(-1f, -0.2f), 0f);
            p.startSize = Random.Range(0.03f, 0.07f);
            rubble.Emit(p, 1);
        }
        p.startSize = Random.Range(0.4f, 0.8f);
        p.velocity = new Vector3(0f, -0.3f, 0f);
        dust.Emit(p, 1);
    }

    IEnumerator FlickerLamps(float duration)
    {
        float end = Time.time + duration;
        while (Time.time < end)
        {
            Lamp.TremorLevel = Random.value < 0.3f ? Random.Range(0.05f, 0.4f) : Random.Range(0.7f, 1f);
            yield return new WaitForSeconds(Random.Range(0.04f, 0.14f));
        }
        Lamp.TremorLevel = 1f;
    }

    // The top edge of what the camera sees (or just under the roof once it's in view).
    bool TopOfView(out float x, out float y, out float halfW)
    {
        Camera cam = Camera.main;
        x = y = halfW = 0f;
        if (cam == null)
            return false;
        float halfH = cam.orthographicSize;
        halfW = halfH * cam.aspect * 0.8f;
        x = cam.transform.position.x;
        y = cam.transform.position.y + halfH + 0.5f;
        if (roof != null)
            y = Mathf.Min(y, roof.bounds.min.y - 0.4f);
        return true;
    }

    static bool InView(Camera cam, Vector3 p)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
    }

    // A ledge on screen sheds bits and dust from its underside, as if it's cracking.
    void CrumbleALedge()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        var candidates = new System.Collections.Generic.List<Bounds>();
        foreach (Bounds b in LampPlacer.Solids())
            if (b.size.x > b.size.y * 2f && InView(cam, b.center))
                candidates.Add(b);
        if (candidates.Count == 0)
            return;
        Bounds ledge = candidates[Random.Range(0, candidates.Count)];
        var p = new ParticleSystem.EmitParams();
        float x = Random.Range(ledge.min.x, ledge.max.x);
        for (int i = 0; i < 8; i++)
        {
            p.position = new Vector3(x + Random.Range(-0.5f, 0.5f), ledge.min.y - 0.05f, 0f);
            p.velocity = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-1.5f, -0.2f), 0f);
            p.startSize = Random.Range(0.04f, 0.14f);
            rubble.Emit(p, 1);
        }
        p.startSize = Random.Range(0.5f, 0.9f);
        for (int i = 0; i < 3; i++)
        {
            p.position = new Vector3(x + Random.Range(-0.6f, 0.6f), ledge.min.y, 0f);
            p.velocity = new Vector3(Random.Range(-0.3f, 0.3f), -0.3f, 0f);
            dust.Emit(p, 1);
        }
    }

    void SparkALamp()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        var onScreen = new System.Collections.Generic.List<Lamp>();
        foreach (Lamp lamp in FindObjectsByType<Lamp>(FindObjectsSortMode.None))
            if (InView(cam, lamp.transform.position))
                onScreen.Add(lamp);
        if (onScreen.Count == 0)
            return;
        var p = new ParticleSystem.EmitParams { position = onScreen[Random.Range(0, onScreen.Count)].transform.position };
        for (int i = 0; i < 20; i++)
        {
            p.velocity = new Vector3(Random.Range(-3f, 3f), Random.Range(-2f, 2.5f), 0f);
            sparks.Emit(p, 1);
        }
        Vfx.Play2D(Vfx.Sound("spark"), volume * 0.35f, Random.Range(0.9f, 1.2f));
    }

    // A real chunk of concrete that tumbles down, bounces off ledges, then crumbles away.
    void DropSlab()
    {
        if (!TopOfView(out float x, out float y, out float halfW))
            return;
        if (slabSprite == null)
            slabSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);

        var go = new GameObject("Falling Slab");
        go.transform.position = new Vector3(x + Random.Range(-halfW, halfW), y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        float size = Random.Range(slabSize.x, slabSize.y);
        go.transform.localScale = new Vector3(size, size * Random.Range(0.4f, 0.8f), 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = slabSprite;
        sr.color = Color.Lerp(rubbleColorA, rubbleColorB, Random.value);
        sr.sortingOrder = 1;
        var col = go.AddComponent<BoxCollider2D>();
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1.2f;
        rb.angularVelocity = Random.Range(-200f, 200f);
        if (playerCollider != null)
            Physics2D.IgnoreCollision(col, playerCollider); // scenery, not a hazard
        go.AddComponent<SlabCrumble>().Init(this, 2.5f);
    }

    /// <summary>Called by a slab as it breaks apart: a puff of dust and bits where it lies.</summary>
    public void Crumble(Vector3 at)
    {
        var p = new ParticleSystem.EmitParams { position = at };
        for (int i = 0; i < 6; i++)
        {
            p.velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.2f, 1.5f), 0f);
            p.startSize = Random.Range(0.04f, 0.12f);
            rubble.Emit(p, 1);
        }
        p.startSize = 0.7f;
        p.velocity = new Vector3(0f, 0.2f, 0f);
        dust.Emit(p, 2);
    }

    // Rubble falls in from just above the top of the screen (or just under the roof, near the top).
    void DropRubble()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        TopOfView(out float cx, out float y, out float halfW);
        float x = cx + Random.Range(-halfW, halfW);
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < rubblePerBurst; i++)
        {
            p.position = new Vector3(x + Random.Range(-1.5f, 1.5f), y + Random.Range(0f, 0.5f), 0f);
            p.velocity = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-1f, 0f), 0f);
            rubble.Emit(p, 1);
        }
        for (int i = 0; i < 5; i++)
        {
            p.position = new Vector3(x + Random.Range(-1.5f, 1.5f), y, 0f);
            p.velocity = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.6f, -0.2f), 0f);
            dust.Emit(p, 1);
        }
        Vfx.Play2D(rubbleSound, volume * 0.7f, Random.Range(0.85f, 1.15f));
    }

    void MakeParticles()
    {
        rubble = Vfx.MakeSystem(transform, "Rubble", Vfx.Chunks, 50);
        var main = rubble.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(rubbleColorA, rubbleColorB);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 1.3f;
        var spin = rubble.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var col = rubble.colorOverLifetime;
        col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        // Bounce off floors, ledges and walls.
        var hit = rubble.collision;
        hit.enabled = true;
        hit.type = ParticleSystemCollisionType.World;
        hit.mode = ParticleSystemCollisionMode.Collision2D;
        hit.bounce = 0.3f;
        hit.dampen = 0.4f;
        hit.lifetimeLoss = 0f;
        hit.radiusScale = 0.5f;
        hit.quality = ParticleSystemCollisionQuality.Medium;

        dust = Vfx.MakeSystem(transform, "Falling Dust", Vfx.Alpha, 49);
        var dm = dust.main;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        dm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        dm.startColor = dustColor;
        dm.gravityModifier = 0.08f;
        var dc = dust.colorOverLifetime; dc.enabled = true; dc.color = Vfx.Fade(Color.white, Color.white);
        var ds = dust.sizeOverLifetime; ds.enabled = true; ds.size = Vfx.Shrink(0.6f, 1.6f);
        var dn = dust.noise; dn.enabled = true; dn.strength = 0.3f; dn.frequency = 0.5f;

        sparks = Vfx.MakeSystem(transform, "Lamp Sparks", Vfx.Additive, 60);
        var sm = sparks.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
        sm.startColor = new Color(1f, 0.85f, 0.5f);
        sm.gravityModifier = 1.2f;
        var sc = sparks.colorOverLifetime; sc.enabled = true; sc.color = Vfx.Fade(Color.white, new Color(1f, 0.4f, 0.1f));
        var sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.velocityScale = 0.05f;
        sr.lengthScale = 1.5f;
    }
}
