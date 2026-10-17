using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Left/right movement, jumping and dashing for a 2D platformer character.
/// Reads the "Player/Move", "Player/Jump" and "Player/Sprint" (used as dash: Left Shift)
/// actions from the project-wide Input System actions (Assets/Settings/InputSystem_Actions).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;
    [Tooltip("How quickly the player reaches full speed on the ground.")]
    [SerializeField] float groundAcceleration = 60f;
    [Tooltip("How quickly the player changes speed while in the air.")]
    [SerializeField] float airAcceleration = 30f;

    [Header("Jumping")]
    [SerializeField] float jumpHeight = 2.5f;
    [Tooltip("Gravity multiplier while falling, for a snappier jump.")]
    [SerializeField] float fallGravityMultiplier = 2f;
    [Tooltip("Seconds after leaving a ledge during which a jump still counts.")]
    [SerializeField] float coyoteTime = 0.1f;
    [Tooltip("A jump pressed this many seconds before landing still happens on landing.")]
    [SerializeField] float jumpBuffer = 0.12f;
    [Tooltip("Barely missing the top of a one-way ledge by this much (units) lifts you onto it.")]
    [SerializeField] float ledgeAssist = 0.35f;

    [Header("Dash")]
    [Tooltip("Speed during a dash, in units per second.")]
    [SerializeField] float dashSpeed = 16f;
    [Tooltip("How long a dash lasts, in seconds.")]
    [SerializeField] float dashDuration = 0.15f;
    [Tooltip("Seconds before you can dash again.")]
    [SerializeField] float dashCooldown = 0.6f;
    [Tooltip("Allow one dash in the air before landing again.")]
    [SerializeField] bool allowAirDash = true;

    [Header("Ground Check")]
    [Tooltip("Layers that count as ground. Leave as Everything if unsure.")]
    [SerializeField] LayerMask groundLayers = ~0;

    Rigidbody2D rb;
    Collider2D body;
    readonly Collider2D[] ledgeHits = new Collider2D[8];
    readonly ContactPoint2D[] contacts = new ContactPoint2D[8];
    InputAction moveAction;
    InputAction jumpAction;
    InputAction dashAction;
    ContactFilter2D groundFilter;
    float baseGravityScale;

    float moveInput;
    float moveInputY;
    float lastGroundedTime = float.NegativeInfinity;
    float jumpPressedTime = float.NegativeInfinity;
    bool canMove = true;
    int regainedFrame = -1;

    bool dashPressed;
    float facing = 1f;
    float dashEndTime = float.NegativeInfinity;
    float nextDashTime;
    bool airDashUsed;
    float lastFallSpeed;

    /// <summary>True while standing on ground.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Horizontal input in the range -1..1.</summary>
    public float MoveInput => moveInput;

    /// <summary>Current velocity of the rigidbody.</summary>
    public Vector2 Velocity => rb.linearVelocity;

    /// <summary>Raised the moment a jump starts.</summary>
    public event System.Action Jumped;
    /// <summary>Raised on touching down, with the downward speed at impact.</summary>
    public event System.Action<float> Landed;
    /// <summary>Raised when a dash starts, with its direction (-1 or 1).</summary>
    public event System.Action<float> Dashed;

    /// <summary>Which way the player faces (-1 left, 1 right).</summary>
    public float Facing => facing;

    /// <summary>True during a dash.</summary>
    public bool IsDashing => Time.time < dashEndTime;

    /// <summary>When false, input is ignored (e.g. while the player is scared).</summary>
    public bool CanMove
    {
        get => canMove;
        set
        {
            // The key that closed a menu (Space, Enter) must not also jump or dash on that frame.
            if (value && !canMove)
                regainedFrame = Time.frameCount;
            canMove = value;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        body = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // No friction: pushing into a wall mid-jump would otherwise pin the player to its side.
        // Stopping on the ground comes from groundAcceleration, not friction.
        rb.sharedMaterial = new PhysicsMaterial2D("Player (no friction)") { friction = 0f, bounciness = 0f };
        baseGravityScale = rb.gravityScale;

        // Only contacts whose normal points upwards (i.e. surfaces below us) count as ground.
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayers);
        groundFilter.SetNormalAngle(45f, 135f);

        moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
        jumpAction = InputSystem.actions.FindAction("Player/Jump", throwIfNotFound: true);
        dashAction = InputSystem.actions.FindAction("Player/Sprint", throwIfNotFound: true);
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        dashAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
        dashAction.Disable();
    }

    void Update()
    {
        if (!CanMove || Time.frameCount == regainedFrame)
        {
            moveInput = 0f;
            moveInputY = 0f;
            jumpPressedTime = float.NegativeInfinity;
            dashPressed = false;
            return;
        }

        Vector2 move = moveAction.ReadValue<Vector2>();
        moveInput = move.x;
        moveInputY = move.y;
        // Like Input.GetKeyDown: one tap = one jump. Holding does nothing extra.
        // Latched here and consumed in FixedUpdate so a tap is never missed between physics steps.
        if (jumpAction.WasPressedThisFrame())
            jumpPressedTime = Time.time;
        if (dashAction.WasPressedThisFrame())
            dashPressed = true;

        if (Mathf.Abs(moveInput) > 0.01f)
            facing = Mathf.Sign(moveInput);
    }

    void FixedUpdate()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = rb.IsTouching(groundFilter);
        if (IsGrounded && !wasGrounded)
            Landed?.Invoke(Mathf.Max(0f, -lastFallSpeed));
        lastFallSpeed = rb.linearVelocity.y;
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            airDashUsed = false;
        }

        Vector2 velocity = rb.linearVelocity;

        // Dash: a short, fixed-speed burst in the facing direction with no gravity.
        bool dash = dashPressed && Time.time >= nextDashTime && (IsGrounded || (allowAirDash && !airDashUsed));
        dashPressed = false;
        if (dash)
        {
            dashEndTime = Time.time + dashDuration;
            nextDashTime = Time.time + dashCooldown;
            if (!IsGrounded)
                airDashUsed = true;
            Dashed?.Invoke(facing);
        }
        if (IsDashing)
        {
            rb.gravityScale = 0f;
            rb.linearVelocity = new Vector2(facing * dashSpeed, 0f);
            return;
        }

        // Horizontal movement
        float targetSpeed = moveInput * moveSpeed;
        float accel = IsGrounded ? groundAcceleration : airAcceleration;
        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, accel * Time.fixedDeltaTime);

        if (!IsGrounded && velocity.y <= 1f)
            AssistOntoLedge(ref velocity);

        // Jump on tap when on the ground (or just walked off a ledge), or on landing if it was
        // pressed a moment before. Always the same height.
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool jump = Time.time - jumpPressedTime <= jumpBuffer && canJump;
        // Down + Jump on a one-way ledge drops through it instead (like the classics).
        if (jump && IsGrounded && moveInputY < -0.5f && DropThroughLedge())
        {
            jumpPressedTime = float.NegativeInfinity;
            jump = false;
        }
        if (jump)
        {
            jumpPressedTime = float.NegativeInfinity;
            float gravity = Mathf.Abs(Physics2D.gravity.y * baseGravityScale);
            velocity.y = Mathf.Sqrt(2f * gravity * jumpHeight);
            lastGroundedTime = float.NegativeInfinity;
            Jumped?.Invoke();
        }

        // Fall faster than you rise, for a less floaty jump.
        rb.gravityScale = velocity.y < 0f ? baseGravityScale * fallGravityMultiplier : baseGravityScale;

        rb.linearVelocity = velocity;
    }

    // Lets the player fall through the one-way ledges they stand on for a moment. False if there are none.
    bool DropThroughLedge()
    {
        if (body == null)
            return false;
        bool dropped = false;
        int count = body.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = contacts[i].collider;
            if (c.usedByEffector && c.TryGetComponent(out PlatformEffector2D e) && e.useOneWay && contacts[i].normal.y > 0.5f)
            {
                StartCoroutine(IgnoreFor(c, 0.35f));
                dropped = true;
            }
        }
        return dropped;
    }

    System.Collections.IEnumerator IgnoreFor(Collider2D ledge, float seconds)
    {
        Physics2D.IgnoreCollision(body, ledge, true);
        yield return new WaitForSeconds(seconds);
        if (ledge != null && body != null)
            Physics2D.IgnoreCollision(body, ledge, false);
    }

    // Near the top of a jump, feet just under the top of a one-way ledge you're passing through:
    // step up onto it instead of dropping back down.
    void AssistOntoLedge(ref Vector2 velocity)
    {
        if (body == null || ledgeAssist <= 0f)
            return;
        Bounds b = body.bounds;
        var area = new Vector2(b.size.x * 0.6f, ledgeAssist);
        int count = Physics2D.OverlapBox(new Vector2(b.center.x, b.min.y + ledgeAssist / 2f), area, 0f, ContactFilter2D.noFilter, ledgeHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = ledgeHits[i];
            if (c == body || c.isTrigger || !c.usedByEffector || !c.TryGetComponent(out PlatformEffector2D e) || !e.useOneWay
                || Physics2D.GetIgnoreCollision(body, c))   // the ledge we're dropping through
                continue;
            float lift = c.bounds.max.y - b.min.y;
            if (lift > 0f && lift <= ledgeAssist)
            {
                rb.position += new Vector2(0f, lift + 0.01f);
                velocity.y = 0f;
                return;
            }
        }
    }
}
