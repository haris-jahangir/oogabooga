using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Lighting and sound for the warehouse. Put it on any object in the scene.
///
/// Before the switch: the scene is pitch dark. Only the switch and a faint glow around
/// the player are visible.
/// When the switch is used (LevelStarter.Begin): the hanging spot lights (Lamps) flicker on one
/// by one, the room gets only a faint ambient glow, the fire's crackle and the background
/// music fade in and loop, and the fire gets a flickering orange glow. Walls and platforms
/// cast shadows, so anywhere the lamps don't reach is naturally dark: that's where the
/// hiding spots are. Lamps come from Tools > Oogabooga > Place Spot Lights, or are placed
/// automatically when the scene has none.
/// </summary>
public class SceneAtmosphere : MonoBehaviour
{
    [Header("Room Light")]
    [Tooltip("Brightness of the global light before the switch.")]
    [SerializeField] float darkIntensity = 0.02f;
    [Tooltip("Faint glow over the whole room after the switch. The lamps do the real lighting.")]
    [SerializeField] float ambientIntensity = 0.15f;
    [Tooltip("Seconds over which the lamps come on, one after another.")]
    [SerializeField] float lightUpDuration = 1.5f;

    [Header("Spot Lights")]
    [Tooltip("If the scene has no Lamps, hang some automatically with these settings.")]
    [SerializeField] bool autoPlaceLamps = true;
    [SerializeField] LampPlacer.Settings lampSettings = new();

    [Header("Glows")]
    [Tooltip("Small light around the switch so the player can find it in the dark.")]
    [SerializeField] float switchGlow = 1.5f;
    [SerializeField] Color switchGlowColor = new(0.4f, 0.9f, 1f);
    [Tooltip("Faint light around the player before the switch.")]
    [SerializeField] float playerGlow = 0.6f;
    [Tooltip("Orange light at the fire's edge.")]
    [SerializeField] float fireGlow = 2.5f;
    [SerializeField] float fireGlowRadius = 7f;
    [SerializeField] Color fireGlowColor = new(1f, 0.5f, 0.15f);
    [Tooltip("How much the fire glow flickers (0 = steady).")]
    [Range(0f, 1f)] [SerializeField] float fireFlicker = 0.35f;
    [Tooltip("Walls and platforms block the fire's light and cast shadows.")]
    [SerializeField] bool fireCastsShadows = true;

    [Header("Hiding Spots")]
    [Tooltip("Also draw a soft dark patch over each hiding spot. Off = they rely on being out of the lamps' light.")]
    [SerializeField] bool styleHidingSpots = false;
    [SerializeField] Color shadowColor = new(0.01f, 0.01f, 0.03f, 0.92f);
    [Tooltip("How soft the shadow edges are (0 = hard square, 0.5 = very soft).")]
    [Range(0f, 0.5f)] [SerializeField] float shadowSoftness = 0.3f;

    [Header("Sound")]
    [Tooltip("Fire crackle, looped. Gets louder as the fire gets closer to the player.")]
    [SerializeField] AudioClip fireLoop;
    [Range(0f, 1f)] [SerializeField] float fireVolumeFar = 0.25f;
    [Range(0f, 1f)] [SerializeField] float fireVolumeNear = 1f;
    [Tooltip("Distance at which the fire is heard at full volume.")]
    [SerializeField] float fireNearDistance = 3f;
    [Tooltip("Distance beyond which the fire stays at its quietest.")]
    [SerializeField] float fireFarDistance = 20f;
    [Tooltip("Intense background music, looped.")]
    [SerializeField] AudioClip music;
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.6f;
    [Tooltip("Seconds for the fire sound and music to fade in.")]
    [SerializeField] float soundFadeIn = 2f;

    Light2D[] globalLights;
    Lamp[] lamps;
    Light2D playerLight, fireLight;
    Fire fire;
    Transform player;
    AudioSource fireSource, musicSource;
    bool lit;
    float soundLevel; // 0..1 fade-in
    float flickerSeed;

    void Start()
    {
        globalLights = System.Array.FindAll(
            FindObjectsByType<Light2D>(FindObjectsSortMode.None),
            l => l.lightType == Light2D.LightType.Global);
        fire = FindFirstObjectByType<Fire>();
        PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
        if (p != null)
            player = p.transform;

        if (styleHidingSpots)
            StyleHidingSpots();

        lamps = FindObjectsByType<Lamp>(FindObjectsSortMode.None);
        if (lamps.Length == 0 && autoPlaceLamps)
            lamps = LampPlacer.Place(new GameObject("Spot Lights").transform, lampSettings).ToArray();
        foreach (Lamp lamp in lamps)
            lamp.Off();

        LevelStarter starter = FindFirstObjectByType<LevelStarter>();
        if (starter == null || starter.HasStarted)
        {
            // No switch in this scene: start lit.
            SetGlobal(ambientIntensity);
            OnSwitch();
            return;
        }

        SetGlobal(darkIntensity);
        starter.Started += OnSwitch;
        GlowSwitches(starter);
        if (player != null && playerGlow > 0f)
            playerLight = MakePointLight(player, "Player Glow", Color.white, playerGlow, 2.5f, false);
    }

    void OnSwitch()
    {
        if (lit)
            return;
        lit = true;
        StartCoroutine(LightUp());

        if (fire != null && fireGlow > 0f)
            fireLight = MakePointLight(transform, "Fire Glow", fireGlowColor, fireGlow, fireGlowRadius, fireCastsShadows);
        if ((fireCastsShadows && fireLight != null) || (lampSettings.castShadows && lamps.Length > 0))
            AddShadowCasters();
        foreach (Lamp lamp in lamps)
            lamp.TurnOn(Random.Range(0f, lightUpDuration));

        fireSource = MakeLoop(fireLoop);
        musicSource = MakeLoop(music);
        flickerSeed = Random.value * 100f;
    }

    IEnumerator LightUp()
    {
        float from = globalLights.Length > 0 ? globalLights[0].intensity : darkIntensity;
        for (float t = 0f; t < lightUpDuration; t += Time.deltaTime)
        {
            float k = t / lightUpDuration;
            SetGlobal(Mathf.Lerp(from, ambientIntensity, k));
            if (playerLight != null)
                playerLight.intensity = playerGlow * (1f - k);
            yield return null;
        }
        SetGlobal(ambientIntensity);
        if (playerLight != null)
            Destroy(playerLight.gameObject);
    }

    void Update()
    {
        if (!lit)
            return;

        if (fireLight != null && fire != null)
        {
            // Sit on the fire's spreading edge, halfway up.
            Bounds b = fire.Area;
            float edge = fire.GrowDirection == Fire.Direction.Right ? b.max.x : b.min.x;
            float y = player != null ? Mathf.Clamp(player.position.y, b.min.y, b.max.y) : b.center.y;
            fireLight.transform.position = new Vector3(edge, y, 0f);
            float n = Mathf.PerlinNoise(flickerSeed, Time.time * 6f);
            fireLight.intensity = fireGlow * (1f - fireFlicker + fireFlicker * 2f * n);
        }

        soundLevel = soundFadeIn > 0f ? Mathf.MoveTowards(soundLevel, 1f, Time.deltaTime / soundFadeIn) : 1f;
        if (musicSource != null)
            musicSource.volume = musicVolume * soundLevel;
        if (fireSource != null)
            fireSource.volume = FireVolume() * soundLevel;
    }

    float FireVolume()
    {
        if (fire == null || player == null)
            return fireVolumeFar;
        Bounds b = fire.Area;
        float distance = Mathf.Max(0f, Mathf.Max(b.min.x - player.position.x, player.position.x - b.max.x));
        float k = Mathf.InverseLerp(fireFarDistance, fireNearDistance, distance);
        return Mathf.Lerp(fireVolumeFar, fireVolumeNear, k);
    }

    void SetGlobal(float intensity)
    {
        foreach (Light2D l in globalLights)
            if (l != null)
                l.intensity = intensity;
    }

    // Any Interactable that calls LevelStarter.Begin gets a small glow so it can be found in the dark.
    void GlowSwitches(LevelStarter starter)
    {
        if (switchGlow <= 0f)
            return;
        foreach (Interactable i in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            for (int e = 0; e < i.onUsed.GetPersistentEventCount(); e++)
                if (i.onUsed.GetPersistentTarget(e) == starter && i.onUsed.GetPersistentMethodName(e) == nameof(LevelStarter.Begin))
                {
                    Light2D glow = MakePointLight(i.transform, "Switch Glow", switchGlowColor, switchGlow, 1.8f, false);
                    starter.Started += () => { if (glow != null) Destroy(glow.gameObject, lightUpDuration); };
                    break;
                }
    }

    Light2D MakePointLight(Transform parent, string name, Color color, float intensity, float radius, bool shadows)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Light2D light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.pointLightInnerRadius = radius * 0.15f;
        light.pointLightOuterRadius = radius;
        light.falloffIntensity = 0.6f;
        light.shadowsEnabled = shadows;
        light.shadowIntensity = 0.85f;
        return light;
    }

    // Solid, still objects (walls, floors, platforms) block light.
    static void AddShadowCasters()
    {
        foreach (BoxCollider2D box in FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
        {
            if (!LampPlacer.IsSolid(box) || box.GetComponent<ShadowCaster2D>() != null)
                continue;
            ShadowCaster2D caster = box.gameObject.AddComponent<ShadowCaster2D>();
            caster.selfShadows = false;
        }
    }

    AudioSource MakeLoop(AudioClip clip)
    {
        if (clip == null)
            return null;
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.Play();
        return source;
    }

    void StyleHidingSpots()
    {
        Sprite shadow = MakeShadowSprite(shadowSoftness);
        foreach (HidingSpot spot in FindObjectsByType<HidingSpot>(FindObjectsSortMode.None))
        {
            if (!spot.TryGetComponent(out SpriteRenderer sr))
                continue;
            // Stretch the shadow over the old sprite's area; the transform (and collider) stay as they are.
            Vector2 oldSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            if (sr.drawMode != SpriteDrawMode.Simple)
                oldSize = sr.size;
            sr.sprite = shadow;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = oldSize;
            sr.color = shadowColor;
        }
    }

    // A white square that fades out towards its edges, with a little blotchiness so it reads as shadow.
    static Sprite MakeShadowSprite(float softness)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float seed = Random.value * 50f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)); // 0 at border, 0.5 at centre
                float a = softness <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, edge / softness);
                a *= 0.85f + 0.15f * Mathf.PerlinNoise(seed + u * 4f, seed + v * 4f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }
}
