using UnityEngine;

/// <summary>
/// Shared helpers for the game-feel effects: particle materials, a soft dot texture,
/// a quick particle-system builder, and the default sounds in Resources/SFX.
/// </summary>
public static class Vfx
{
    static Material additive, alpha, unlitSprite;
    static Texture2D softDot;

    // The materials live in Assets/Resources/VFX so their shaders are always included and
    // compatible with URP's 2D Renderer (shaders it can't draw show up pink).

    /// <summary>Glowing particles (fire, sparks): colours add up and stay bright in the dark.</summary>
    public static Material Additive => additive != null ? additive : additive = Particle("VfxAdditive");

    /// <summary>Normal see-through particles (dust, smoke).</summary>
    public static Material Alpha => alpha != null ? alpha : alpha = Particle("VfxAlpha");

    /// <summary>For SpriteRenderers that should glow regardless of the lights (afterimages, the fire body).</summary>
    public static Material UnlitSprite => unlitSprite != null ? unlitSprite : unlitSprite = Load("VfxUnlitSprite");

    static Material Particle(string name)
    {
        Material m = Load(name);
        m.mainTexture = SoftDot;
        return m;
    }

    static Material Load(string name)
    {
        Material source = Resources.Load<Material>("VFX/" + name);
        if (source == null)
            Debug.LogError($"[Vfx] Missing Assets/Resources/VFX/{name}.mat");
        return source != null ? new Material(source) { name = name + " (runtime)" } : new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
    }

    /// <summary>A white dot that fades out softly to its edge.</summary>
    public static Texture2D SoftDot
    {
        get
        {
            if (softDot != null)
                return softDot;
            const int size = 32;
            softDot = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                }
            softDot.SetPixels32(px);
            softDot.Apply();
            return softDot;
        }
    }

    /// <summary>
    /// A particle system with sensible 2D defaults that emits nothing until configured.
    /// It simulates in world space so particles stay behind when the parent moves.
    /// </summary>
    public static ParticleSystem MakeSystem(Transform parent, string name, Material material, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 500;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        shape.rotation = Vector3.zero;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = material;
        r.sortingOrder = sortingOrder;
        r.renderMode = ParticleSystemRenderMode.Billboard;

        // Playing with no emission rate: nothing appears until Emit() or a rate is set,
        // but emitted particles always animate.
        ps.Play();
        return ps;
    }

    /// <summary>A gradient that fades in quickly and out at the end.</summary>
    public static Gradient Fade(Color start, Color end, float startAlpha = 1f)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(startAlpha, 0.1f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    /// <summary>A size curve that shrinks from full size to nothing.</summary>
    public static ParticleSystem.MinMaxCurve Shrink(float from = 1f, float to = 0f) =>
        new(1f, AnimationCurve.Linear(0f, from, 1f, to));

    /// <summary>One of the default sounds in Assets/Resources/SFX, or null.</summary>
    public static AudioClip Sound(string name) => Resources.Load<AudioClip>("SFX/" + name);

    /// <summary>Plays a clip as a 2D sound (same loudness anywhere).</summary>
    public static void Play2D(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
            return;
        var go = new GameObject("SFX " + clip.name);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.spatialBlend = 0f;
        source.Play();
        Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }
}
