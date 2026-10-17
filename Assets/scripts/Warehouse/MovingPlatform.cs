using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slides a platform back and forth (sideways by default) and carries the player standing on it.
/// Art sitting on the platform (sprites with no collider, like scaffolding drawn over it) and
/// lamps hanging right under it are found automatically and move along too.
/// Put it on a ledge with a collider (one-way ledges work).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("How far it travels from its start, in units. (3, 0) = 3 to the right and back.")]
    [SerializeField] Vector2 travel = new(3f, 0f);
    [Tooltip("Seconds for one trip there and back.")]
    [SerializeField] float period = 4f;
    [Tooltip("Start partway through the trip (0..1) so platforms don't move in step.")]
    [Range(0f, 1f)] [SerializeField] float phase;
    [Tooltip("Pause at each end, as a fraction of the trip.")]
    [Range(0f, 0.4f)] [SerializeField] float endPause = 0.15f;

    Rigidbody2D body;
    Collider2D surface;
    Vector2 start;
    float time;
    readonly List<Transform> attached = new();
    Rigidbody2D player;

    void Awake()
    {
        surface = GetComponent<Collider2D>();
        body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        start = body.position;
        time = phase * period;

        PlayerMovement p = FindFirstObjectByType<PlayerMovement>();
        if (p != null)
            player = p.GetComponent<Rigidbody2D>();
        FindAttachedArt();
    }

    void FixedUpdate()
    {
        time += Time.fixedDeltaTime;
        Vector2 next = start + travel * Ease(Mathf.Repeat(time / Mathf.Max(0.1f, period), 1f));
        Vector2 delta = next - body.position;
        body.MovePosition(next);

        foreach (Transform t in attached)
            if (t != null)
                t.position += (Vector3)delta;

        // Carry the player while they stand on top.
        if (player != null && player.IsTouching(surface) && player.position.y > surface.bounds.center.y)
            player.position += delta;
    }

    // 0 -> 1 -> 0 over one trip, easing in and out, with a short pause at each end.
    float Ease(float t)
    {
        float k = t < 0.5f ? t * 2f : (1f - t) * 2f;   // 0..1..0
        k = Mathf.Clamp01((k - endPause) / Mathf.Max(0.01f, 1f - 2f * endPause));
        return Mathf.SmoothStep(0f, 1f, k);
    }

    void FindAttachedArt()
    {
        Bounds b = surface.bounds;
        b.Expand(new Vector3(0.2f, 0.4f, 10f));
        foreach (SpriteRenderer sr in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            Transform t = sr.transform;
            if (t == transform || t.IsChildOf(transform) || sr.GetComponent<Collider2D>() != null
                || sr.GetComponentInParent<Rigidbody2D>() != null)
                continue;
            // Only things that sit on this ledge, not big backgrounds that happen to overlap it.
            if (b.Contains(sr.bounds.center) && sr.bounds.size.x <= b.size.x * 1.5f)
                attached.Add(t);
        }
        foreach (Lamp lamp in FindObjectsByType<Lamp>(FindObjectsSortMode.None))
        {
            Vector3 p = lamp.transform.position;
            if (p.x >= b.min.x && p.x <= b.max.x && p.y <= b.min.y + 0.4f && p.y >= b.min.y - 0.8f)
                attached.Add(lamp.transform);
        }
    }

    void OnDrawGizmosSelected()
    {
        Collider2D c = GetComponent<Collider2D>();
        if (c == null)
            return;
        Vector3 from = Application.isPlaying ? (Vector3)start : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(from, from + (Vector3)travel);
        Gizmos.DrawWireCube(from + (Vector3)travel, c.bounds.size);
    }
}
