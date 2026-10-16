using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A shadow the player can hide in. Put this on an object with a Collider2D set to Is Trigger.
/// The player is hidden while standing inside any HidingSpot.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HidingSpot : MonoBehaviour
{
    static readonly HashSet<HidingSpot> occupied = new();

    /// <summary>True while the player is inside at least one hiding spot.</summary>
    public static bool IsPlayerHidden => occupied.Count > 0;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other))
            occupied.Add(this);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayer(other))
            occupied.Remove(this);
    }

    // Also covers scene reloads, so a stale "hidden" never carries over.
    void OnDisable() => occupied.Remove(this);

    static bool IsPlayer(Collider2D other) => other.GetComponentInParent<PlayerMovement>() != null;
}
