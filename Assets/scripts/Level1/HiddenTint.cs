using UnityEngine;

/// <summary>
/// Darkens the player while they stand in a hiding spot, so "am I safe?" reads at a glance.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class HiddenTint : MonoBehaviour
{
    public Color hiddenColor = new(0.25f, 0.28f, 0.45f);
    [Tooltip("How fast the colour changes.")]
    public float speed = 12f;

    SpriteRenderer sprite;
    Color normalColor;

    void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        normalColor = sprite.color;
    }

    void LateUpdate()
    {
        Color target = HidingSpot.IsPlayerHidden ? hiddenColor : normalColor;
        sprite.color = Color.Lerp(sprite.color, target, speed * Time.deltaTime);
    }
}
