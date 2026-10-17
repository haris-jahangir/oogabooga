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
    InputAction moveAction;
    InputAction jumpAction;
    InputAction dashAction;
    ContactFilter2D groundFilter;
    float baseGravityScale;

    float moveInput;
    float lastGroundedTime = float.NegativeInfinity;
    bool jumpPressed;

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
    public bool CanMove { get; set; } = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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
        if (!CanMove)
        {
            moveInput = 0f;
            jumpPressed = false;
            dashPressed = false;
            return;
        }

        moveInput = moveAction.ReadValue<Vector2>().x;
        // Like Input.GetKeyDown: one tap = one jump. Holding does nothing extra.
        // Latched here and consumed in FixedUpdate so a tap is never missed between physics steps.
        if (jumpAction.WasPressedThisFrame())
            jumpPressed = true;
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

        // Jump on tap when on the ground (or just walked off a ledge). Always the same height.
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool jump = jumpPressed && canJump;
        jumpPressed = false;
        if (jump)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y * baseGravityScale);
            velocity.y = Mathf.Sqrt(2f * gravity * jumpHeight);
            lastGroundedTime = float.NegativeInfinity;
            Jumped?.Invoke();
        }

        // Fall faster than you rise, for a less floaty jump.
        rb.gravityScale = velocity.y < 0f ? baseGravityScale * fallGravityMultiplier : baseGravityScale;

        rb.linearVelocity = velocity;
    }
}
