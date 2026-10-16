using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A shadow the player can hide in. Put this on an object with a Collider2D set to Is Trigger.
/// The player is hidden while standing inside any HidingSpot that hasn't been burned.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HidingSpot : MonoBehaviour
{
    static readonly HashSet<HidingSpot> occupied = new();
    static readonly List<HidingSpot> all = new();

    /// <summary>True while the player is inside at least one hiding spot.</summary>
    public static bool IsPlayerHidden => occupied.Count > 0;

    /// <summary>Every hiding spot in the scene that can still be used.</summary>
    public static IReadOnlyList<HidingSpot> All => all;

    Collider2D spotCollider;

    /// <summary>True once fire has destroyed this spot.</summary>
    public bool IsBurned { get; private set; }

    /// <summary>World-space bounds of this spot's collider.</summary>
    public Bounds Bounds => spotCollider.bounds;

    // Domain reload is disabled in this project, so statics survive between Play sessions.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        occupied.Clear();
        all.Clear();
    }

    void Awake() => spotCollider = GetComponent<Collider2D>();

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnEnable()
    {
        if (!IsBurned)
            all.Add(this);
    }

    void OnDisable()
    {
        occupied.Remove(this);
        all.Remove(this);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsBurned && IsPlayer(other))
            occupied.Add(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
            occupied.Remove(this);
    }

    /// <summary>
    /// Destroys the spot: it no longer hides the player and its sprite disappears.
    /// Only this object's own collider and sprite are turned off; nothing else is touched.
    /// </summary>
    public void Burn()
    {
        if (IsBurned)
            return;
        IsBurned = true;
        occupied.Remove(this);
        all.Remove(this);
        spotCollider.enabled = false;
        if (TryGetComponent(out SpriteRenderer sprite))
            sprite.enabled = false;
    }

    static bool IsPlayer(Collider2D other) => other.GetComponentInParent<PlayerMovement>() != null;
}
