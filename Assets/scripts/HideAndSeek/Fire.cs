using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A wall of fire that grows over time. By default its left edge stays put and it spreads
/// to the right; Grow Direction can also be Left, Up (rising from the floor, like a level
/// filling with flames) or Down. When the fire covers the middle of a HidingSpot, that spot
/// burns and can no longer hide the player.
/// Touching the fire is game over.
///
/// Setup: put a fire sprite where the fire should start, covering the play area across
/// (tall for Right/Left, wide for Up/Down), and add this script. The sprite repeats (Draw Mode is set to Tiled) instead of
/// stretching; set the image's Mesh Type to Full Rect so the tiling looks right.
/// The fire never scales or moves its children, and needs no collider.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Fire : MonoBehaviour
{
    // New values go at the end so scenes keep their saved choice.
    public enum Direction { Right, Left, Up, Down }

    [Header("Growth")]
    [Tooltip("Which way the fire spreads. The opposite edge stays fixed.")]
    [SerializeField] Direction growDirection = Direction.Right;
    [Tooltip("Seconds before the fire starts spreading.")]
    [SerializeField] float startDelay = 3f;
    [Tooltip("How fast the fire spreads, in units per second.")]
    [SerializeField] float growSpeed = 0.3f;
    [Tooltip("How much faster it spreads every second. 0 = constant speed.")]
    [SerializeField] float acceleration = 0.01f;
    [Tooltip("Furthest the fire can grow (its width, or height for Up/Down), in units. 0 = no limit.")]
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

    float anchorEdge;   // the edge that stays fixed (an x for Right/Left, a y for Up/Down)
    float length;       // size along the growth direction
    float across;       // size the other way, which never changes
    float speed;
    float timer;
    bool playerBurned;

    /// <summary>World-space area currently covered by the fire.</summary>
    public Bounds Area => spriteRenderer.bounds;

    /// <summary>Which way the fire is spreading.</summary>
    public Direction GrowDirection => growDirection;

    /// <summary>True when the fire grows up or down instead of sideways.</summary>
    public bool IsVertical => growDirection == Direction.Up || growDirection == Direction.Down;

    /// <summary>The direction the fire spreads in, as a unit vector.</summary>
    public Vector2 GrowVector => growDirection switch
    {
        Direction.Left => Vector2.left,
        Direction.Up => Vector2.up,
        Direction.Down => Vector2.down,
        _ => Vector2.right,
    };

    /// <summary>The x (Right/Left) or y (Up/Down) of the edge that is spreading.</summary>
    public float FrontEdge
    {
        get
        {
            Bounds b = Area;
            return growDirection switch
            {
                Direction.Left => b.min.x,
                Direction.Up => b.max.y,
                Direction.Down => b.min.y,
                _ => b.max.x,
            };
        }
    }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        monsterManager = FindFirstObjectByType<MonsterManager>();
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            playerCollider = player.GetComponent<Collider2D>();

        // Remember the visible size, then switch to Tiled so we can widen it without scaling.
        Bounds start = spriteRenderer.bounds;
        anchorEdge = AnchorOf(start);
        length = IsVertical ? start.size.y : start.size.x;
        across = IsVertical ? start.size.x : start.size.y;
        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
        SetVisualSize();

        speed = growSpeed;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= startDelay && (maxWidth <= 0f || length < maxWidth))
        {
            speed += acceleration * Time.deltaTime;
            length += speed * Time.deltaTime;
            if (maxWidth > 0f)
                length = Mathf.Min(length, maxWidth);
            SetVisualSize();
        }

        Bounds fire = spriteRenderer.bounds;
        BurnHidingSpots(fire);
        CheckPlayer(fire);
    }

    // The edge opposite the growth direction, which stays fixed.
    float AnchorOf(Bounds b) => growDirection switch
    {
        Direction.Left => b.max.x,
        Direction.Up => b.min.y,
        Direction.Down => b.max.y,
        _ => b.min.x,
    };

    void SetVisualSize()
    {
        float worldWidth = IsVertical ? across : length;
        float worldHeight = IsVertical ? length : across;
        Vector3 scale = transform.lossyScale;
        spriteRenderer.size = new Vector2(
            worldWidth / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            worldHeight / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));

        // Shift so the anchored edge stays put and the fire only grows the chosen way.
        float drift = anchorEdge - AnchorOf(spriteRenderer.bounds);
        transform.position += IsVertical ? new Vector3(0f, drift, 0f) : new Vector3(drift, 0f, 0f);
    }

    void BurnHidingSpots(Bounds fire)
    {
        // Walk backwards: burning a spot removes it from the list.
        var spots = HidingSpot.All;
        for (int i = spots.Count - 1; i >= 0; i--)
        {
            HidingSpot spot = spots[i];
            Bounds b = spot.Bounds;
            // Burn only once the fire actually covers the middle of the spot
            // (along the growth direction) and overlaps it the other way.
            bool coversX = b.center.x >= fire.min.x && b.center.x <= fire.max.x;
            bool coversY = b.center.y >= fire.min.y && b.center.y <= fire.max.y;
            bool overlapsX = b.max.x >= fire.min.x && b.min.x <= fire.max.x;
            bool overlapsY = b.max.y >= fire.min.y && b.min.y <= fire.max.y;
            bool reached = IsVertical ? coversY && overlapsX : coversX && overlapsY;
            if (!reached)
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
