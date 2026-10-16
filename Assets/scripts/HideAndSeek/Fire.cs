using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A wall of fire that grows over time. By default its left edge stays put and it spreads
/// to the right (switch Grow Direction to spread left instead). When the fire covers the
/// middle of a HidingSpot, that spot burns and can no longer hide the player.
/// Touching the fire is game over.
///
/// Setup: put a fire sprite where the fire should start, tall enough to cover the play
/// area, and add this script. The sprite repeats (Draw Mode is set to Tiled) instead of
/// stretching; set the image's Mesh Type to Full Rect so the tiling looks right.
/// The fire never scales or moves its children, and needs no collider.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Fire : MonoBehaviour
{
    public enum Direction { Right, Left }

    [Header("Growth")]
    [Tooltip("Which way the fire spreads. The opposite edge stays fixed.")]
    [SerializeField] Direction growDirection = Direction.Right;
    [Tooltip("Seconds before the fire starts spreading.")]
    [SerializeField] float startDelay = 3f;
    [Tooltip("How fast the fire spreads, in units per second.")]
    [SerializeField] float growSpeed = 0.3f;
    [Tooltip("How much faster it spreads every second. 0 = constant speed.")]
    [SerializeField] float acceleration = 0.01f;
    [Tooltip("Widest the fire can get, in units. 0 = no limit.")]
    [SerializeField] float maxWidth = 0f;

    [Header("Burning")]
    [Tooltip("Touching the fire is game over.")]
    [SerializeField] bool killsPlayer = true;
    [Tooltip("Optional effect spawned where a hiding spot burns (smoke, embers...).")]
    [SerializeField] GameObject burnEffect;
    [Tooltip("Optional sound when a hiding spot burns.")]
    [SerializeField] AudioClip burnSound;

    SpriteRenderer spriteRenderer;
    MonsterManager monsterManager;
    Collider2D playerCollider;

    float anchorEdge;   // the edge that stays fixed
    float width;
    float speed;
    float timer;
    bool playerBurned;

    /// <summary>World-space area currently covered by the fire.</summary>
    public Bounds Area => spriteRenderer.bounds;

    /// <summary>Which way the fire is spreading.</summary>
    public Direction GrowDirection => growDirection;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        monsterManager = FindFirstObjectByType<MonsterManager>();
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            playerCollider = player.GetComponent<Collider2D>();

        // Remember the visible size, then switch to Tiled so we can widen it without scaling.
        Bounds start = spriteRenderer.bounds;
        anchorEdge = growDirection == Direction.Right ? start.min.x : start.max.x;
        width = start.size.x;
        float height = start.size.y;
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        SetVisualSize(width, height);

        speed = growSpeed;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= startDelay && (maxWidth <= 0f || width < maxWidth))
        {
            speed += acceleration * Time.deltaTime;
            width += speed * Time.deltaTime;
            if (maxWidth > 0f)
                width = Mathf.Min(width, maxWidth);
            SetVisualSize(width, spriteRenderer.bounds.size.y);
        }

        Bounds fire = spriteRenderer.bounds;
        BurnHidingSpots(fire);
        CheckPlayer(fire);
    }

    void SetVisualSize(float worldWidth, float worldHeight)
    {
        Vector3 scale = transform.lossyScale;
        spriteRenderer.size = new Vector2(
            worldWidth / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            worldHeight / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));

        // Shift so the anchored edge stays put and the fire only grows the chosen way.
        Bounds b = spriteRenderer.bounds;
        float currentEdge = growDirection == Direction.Right ? b.min.x : b.max.x;
        float drift = anchorEdge - currentEdge;
        transform.position += new Vector3(drift, 0f, 0f);
    }

    void BurnHidingSpots(Bounds fire)
    {
        // Walk backwards: burning a spot removes it from the list.
        var spots = HidingSpot.All;
        for (int i = spots.Count - 1; i >= 0; i--)
        {
            HidingSpot spot = spots[i];
            Bounds b = spot.Bounds;
            // Burn only once the fire actually covers the middle of the spot.
            bool reached = b.center.x >= fire.min.x && b.center.x <= fire.max.x;
            bool sameHeight = b.max.y >= fire.min.y && b.min.y <= fire.max.y;
            if (!reached || !sameHeight)
                continue;

            if (burnEffect != null)
                Instantiate(burnEffect, b.center, Quaternion.identity);
            if (burnSound != null)
                AudioSource.PlayClipAtPoint(burnSound, b.center);
            spot.Burn();
        }
    }

    void CheckPlayer(Bounds fire)
    {
        if (!killsPlayer || playerBurned || playerCollider == null)
            return;

        Bounds p = playerCollider.bounds;
        bool touching = p.max.x >= fire.min.x && p.min.x <= fire.max.x
                     && p.max.y >= fire.min.y && p.min.y <= fire.max.y;
        if (!touching)
            return;

        playerBurned = true;
        if (monsterManager != null)
            monsterManager.CatchPlayer();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
