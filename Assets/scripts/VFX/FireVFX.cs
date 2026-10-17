using UnityEngine;

/// <summary>
/// Makes the Fire look like fire instead of a flat block: the body becomes a glowing, pulsing
/// wall of embers, the spreading edge throws up licking flames, sparks drift up, and dark
/// smoke rolls off the top. When the fire first lights (after the switch) there's a burst
/// of flame, a whoomp sound and a camera shake.
/// Added to the Fire automatically (see VfxBootstrap); tweak it in the Inspector.
/// </summary>
[RequireComponent(typeof(Fire))]
public class FireVFX : MonoBehaviour
{
    [Header("Body")]
    [SerializeField] Color bodyColor = new(1f, 0.45f, 0.12f);
    [Tooltip("How much the body brightness throbs.")]
    [Range(0f, 0.5f)] [SerializeField] float bodyPulse = 0.18f;

    [Header("Flames")]
    [Tooltip("Flames per second for each unit of the fire's height.")]
    [SerializeField] float flameRate = 22f;
    [SerializeField] float flameSize = 0.9f;
    [SerializeField] float flameHeight = 2.5f;

    [Header("Embers & Smoke")]
    [SerializeField] float emberRate = 18f;
    [SerializeField] float smokeRate = 5f;
    [SerializeField] Color smokeColor = new(0.08f, 0.07f, 0.07f, 0.55f);

    [Header("Ignition")]
    [SerializeField] AudioClip igniteSound;
    [Range(0f, 1f)] [SerializeField] float igniteVolume = 1f;

    Fire fire;
    SpriteRenderer body;
    Transform holder; // unscaled parent for the particles (the fire itself is stretched)
    ParticleSystem edgeFlames, innerFlames, embers, smoke;
    bool lit;
    float seed;

    void Start()
    {
        fire = GetComponent<Fire>();
        body = GetComponent<SpriteRenderer>();
        if (igniteSound == null)
            igniteSound = Vfx.Sound("ignite");
        seed = Random.value * 100f;

        // Glowing ember texture, tiled across the fire (Fire uses Tiled draw mode).
        body.sprite = MakeEmberSprite();
        body.sharedMaterial = Vfx.UnlitSprite;
        int order = body.sortingOrder;
        holder = new GameObject(name + " VFX").transform;

        edgeFlames = Flames("Edge Flames", order + 2, flameSize, flameHeight);
        innerFlames = Flames("Inner Flames", order + 1, flameSize * 0.6f, flameHeight * 0.4f);

        embers = Vfx.MakeSystem(holder, "Embers", Vfx.Additive, order + 3);
        var em = embers.main;
        em.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        em.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        em.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        em.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.4f, 0.1f));
        em.gravityModifier = -0.15f;
        Shape(embers);
        var ec = embers.colorOverLifetime; ec.enabled = true; ec.color = Vfx.Fade(Color.white, new Color(1f, 0.3f, 0.1f));
        var en = embers.noise; en.enabled = true; en.strength = 0.8f; en.frequency = 0.6f; en.scrollSpeed = 0.5f;

        smoke = Vfx.MakeSystem(holder, "Smoke", Vfx.Alpha, order + 1);
        var sm = smoke.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1f);
        sm.startSize = new ParticleSystem.MinMaxCurve(1f, 2.2f);
        sm.startColor = smokeColor;
        sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        sm.gravityModifier = -0.04f;
        Shape(smoke);
        var sc = smoke.colorOverLifetime; sc.enabled = true; sc.color = Vfx.Fade(Color.white, Color.white, 0.8f);
        var ss = smoke.sizeOverLifetime; ss.enabled = true; ss.size = Vfx.Shrink(0.6f, 1.8f);
        var sn = smoke.noise; sn.enabled = true; sn.strength = 0.4f; sn.frequency = 0.3f;

        SetLit(fire.enabled);
    }

    ParticleSystem Flames(string name, int order, float size, float height)
    {
        ParticleSystem ps = Vfx.MakeSystem(holder, name, Vfx.Additive, order);
        var main = ps.main;
        main.maxParticles = 1500;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = Color.white;
        main.gravityModifier = -height * 0.3f;   // flames rise
        Shape(ps);
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.15f), 0.3f),
                    new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 0.7f), new GradientColorKey(new Color(0.3f, 0.05f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = Vfx.Shrink(1f, 0.1f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.7f; noise.frequency = 1.2f; noise.scrollSpeed = 1.5f;
        var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(height * 0.4f, height);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        return ps;
    }

    static void Shape(ParticleSystem ps)
    {
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = Vector3.one;
    }

    void OnDestroy()
    {
        if (holder != null)
            Destroy(holder.gameObject);
    }

    void SetLit(bool on)
    {
        lit = on;
        // Hide the body until it lights (by alpha: Fire still needs the renderer's bounds).
        if (!on)
            body.color = Color.clear;
        foreach (ParticleSystem ps in new[] { edgeFlames, innerFlames, embers, smoke })
            if (on) ps.Play(); else ps.Stop();
    }

    void Update()
    {
        if (!lit)
        {
            if (fire.enabled)
                Ignite();
            return;
        }

        Bounds a = fire.Area;
        Vector3 inward = -(Vector3)fire.GrowVector;
        bool vertical = fire.IsVertical;
        // The spreading edge: a line across the fire (a tall strip, or a wide one for Up/Down).
        Vector3 front = vertical ? new Vector3(a.center.x, fire.FrontEdge) : new Vector3(fire.FrontEdge, a.center.y);
        float span = vertical ? a.size.x : a.size.y;
        Vector3 Strip(float thickness) => vertical ? new Vector3(span, thickness, 1f) : new Vector3(thickness, span, 1f);

        // The leading edge: big licking flames.
        Place(edgeFlames, front + inward * 0.2f, Strip(0.6f), flameRate * span);
        // The body: smaller flames scattered across the whole burning area.
        Place(innerFlames, a.center, new Vector3(a.size.x, a.size.y, 1f), Mathf.Min(400f, flameRate * 0.08f * a.size.x * a.size.y));
        Place(embers, front + inward * 0.5f, Strip(1.2f), emberRate * span * 0.15f);
        // Smoke rolls off the top of the fire.
        if (vertical)
            Place(smoke, new Vector3(a.center.x, a.max.y - 0.3f), new Vector3(a.size.x, 1f, 1f), smokeRate * Mathf.Max(1f, a.size.x / 8f));
        else
            Place(smoke, new Vector3(front.x + inward.x * 0.6f, a.max.y - 0.5f), new Vector3(1.5f + a.size.x * 0.5f, 1f, 1f), smokeRate);

        float pulse = 1f - bodyPulse + bodyPulse * 2f * Mathf.PerlinNoise(seed, Time.time * 2.5f);
        Color c = bodyColor * pulse;
        c.a = 1f;
        body.color = c;
    }

    static void Place(ParticleSystem ps, Vector3 position, Vector3 size, float rate)
    {
        ps.transform.position = new Vector3(position.x, position.y, 0f);
        var shape = ps.shape;
        shape.scale = size;
        var emission = ps.emission;
        emission.rateOverTime = rate;
    }

    void Ignite()
    {
        SetLit(true);
        Update();   // position the emitters before the burst
        edgeFlames.Emit(120);
        embers.Emit(60);
        smoke.Emit(10);
        Vfx.Play2D(igniteSound, igniteVolume);
        CameraFX.Shake(0.5f);
        CameraFX.Punch(0.04f, 0.5f, 0.15f);
    }

    // A tileable glowing-coals texture: dark red with brighter orange veins.
    static Sprite MakeEmberSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Tileable noise: sample on a torus so the edges match.
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float n = Mathf.PerlinNoise(10f + Mathf.Cos(u) * 1.5f, 10f + Mathf.Sin(u) * 1.5f + Mathf.Cos(v) * 1.5f)
                        * 0.6f + Mathf.PerlinNoise(30f + Mathf.Sin(v) * 3f, 30f + Mathf.Cos(u) * 3f) * 0.4f;
                float vein = Mathf.SmoothStep(0.45f, 0.7f, n);
                Color c = Color.Lerp(new Color(0.35f, 0.04f, 0.01f), new Color(1f, 0.75f, 0.3f), vein);
                px[y * size + x] = c;
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }
}
