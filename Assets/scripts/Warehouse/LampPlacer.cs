using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hangs spot lights around the level: under each platform and along the ceiling, pointing
/// down, at loosely random spacing. Lamps never shine on a hiding spot, so those stay in
/// natural shadow. Used by Tools > Oogabooga > Place Spot Lights (and by SceneAtmosphere
/// when a scene has no lamps).
/// </summary>
public static class LampPlacer
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Average distance between lamps along a ceiling or platform.")]
        public float spacing = 6f;
        [Tooltip("Random shift of each lamp, as a fraction of the spacing.")]
        [Range(0f, 0.5f)] public float jitter = 0.3f;
        public float intensity = 1.2f;
        public Color color = new(1f, 0.85f, 0.6f);
        [Tooltip("Width of the bright centre of the cone, in degrees.")]
        public float innerAngle = 35f;
        [Tooltip("Full width of the cone, in degrees.")]
        public float outerAngle = 80f;
        [Tooltip("Longest reach of a lamp.")]
        public float maxRange = 8f;
        [Tooltip("Keep lamps at least this far (sideways) from a hiding spot's edge.")]
        public float hidingSpotMargin = 1f;
        [Tooltip("Chance a lamp is faulty and cuts out now and then.")]
        [Range(0f, 1f)] public float faultyChance = 0.2f;
        public bool castShadows = true;
        public int seed = 1;
    }

    /// <summary>Creates the lamps as children of parent and returns them.</summary>
    public static List<Lamp> Place(Transform parent, Settings s)
    {
        var random = new System.Random(s.seed);
        List<Bounds> solids = Solids();
        List<Bounds> spots = Object.FindObjectsByType<HidingSpot>(FindObjectsSortMode.None)
            .Select(h => h.GetComponent<Collider2D>().bounds).ToList();
        var lamps = new List<Lamp>();
        if (solids.Count == 0)
            return lamps;

        float levelTop = solids.Max(b => b.max.y);
        float levelLeft = solids.Min(b => b.min.x), levelRight = solids.Max(b => b.max.x);

        // Places a lamp could hang from: the underside of each wide platform, plus the ceiling.
        var ceilings = new List<(float y, float xMin, float xMax)>();
        foreach (Bounds b in solids)
            if (b.size.x >= 2f && b.size.x > b.size.y * 2f)
                ceilings.Add((b.min.y, b.min.x, b.max.x));
        ceilings.Add((levelTop - 0.3f, levelLeft, levelRight));

        int count = 0;
        foreach (var (y, xMin, xMax) in ceilings)
        {
            for (float x = xMin + s.spacing * 0.5f; x < xMax; x += s.spacing)
            {
                float lx = x + (float)(random.NextDouble() * 2 - 1) * s.spacing * s.jitter;
                lx = Mathf.Clamp(lx, xMin + 0.3f, xMax - 0.3f);
                float ly = y - 0.1f;

                if (!FloorBelow(solids, lx, ly, out float floorY))
                    continue;                       // nothing to light (e.g. under the main floor)
                float drop = ly - floorY;
                if (drop < 1.5f || InsideSolid(solids, lx, ly - 0.2f))
                    continue;                       // too cramped, or inside a wall
                float reach = Mathf.Min(s.maxRange, drop + 1.5f);
                if (LightsHidingSpot(spots, lx, ly, floorY, reach, s))
                    continue;

                var go = new GameObject($"Lamp {++count}");
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(lx, ly, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, 180f); // cone points down

                Light2D light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = s.color;
                light.intensity = s.intensity;
                light.pointLightInnerAngle = s.innerAngle;
                light.pointLightOuterAngle = s.outerAngle;
                light.pointLightInnerRadius = 0.2f;
                light.pointLightOuterRadius = reach;
                light.falloffIntensity = 0.5f;
                light.shadowsEnabled = s.castShadows;
                light.shadowIntensity = 0.9f;

                Lamp lamp = go.AddComponent<Lamp>();
                lamp.intensity = s.intensity;
                lamp.faulty = random.NextDouble() < s.faultyChance;
                lamps.Add(lamp);
            }
        }
        return lamps;
    }

    /// <summary>Bounds of every solid, still object with a sprite: floors, walls, platforms.</summary>
    public static List<Bounds> Solids() =>
        Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None)
            .Where(IsSolid)
            .Select(c => c.bounds)
            .ToList();

    public static bool IsSolid(Collider2D c) =>
        !c.isTrigger && c.attachedRigidbody == null && c.GetComponent<SpriteRenderer>() != null
        && c.GetComponent<HidingSpot>() == null && c.GetComponent<Fire>() == null;

    static bool FloorBelow(List<Bounds> solids, float x, float y, out float floorY)
    {
        floorY = float.NegativeInfinity;
        foreach (Bounds b in solids)
            if (x >= b.min.x && x <= b.max.x && b.max.y < y && b.max.y > floorY)
                floorY = b.max.y;
        return !float.IsNegativeInfinity(floorY);
    }

    static bool InsideSolid(List<Bounds> solids, float x, float y) =>
        solids.Any(b => b.Contains(new Vector3(x, y, b.center.z)));

    // Would the cone reach a hiding spot in the room under this lamp?
    static bool LightsHidingSpot(List<Bounds> spots, float x, float y, float floorY, float reach, Settings s)
    {
        // The cone's well-lit part, halfway between the bright centre and the soft edge.
        float tan = Mathf.Tan((s.innerAngle + s.outerAngle) * 0.25f * Mathf.Deg2Rad);
        foreach (Bounds spot in spots)
        {
            bool sameRoom = spot.max.y > floorY - 0.5f && spot.min.y < y;
            if (!sameRoom)
                continue;
            float down = Mathf.Clamp(y - spot.max.y, 0f, reach); // how far the cone has spread by the spot's top
            float gap = Mathf.Max(spot.min.x - x, x - spot.max.x); // < 0 when directly above
            if (gap < tan * down + s.hidingSpotMargin)
                return true;
        }
        return false;
    }
}
