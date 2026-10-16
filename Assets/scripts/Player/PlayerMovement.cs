using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Left/right movement and jumping for a 2D platformer character.
/// Reads the "Player/Move" and "Player/Jump" actions from the project-wide
/// Input System actions (Assets/Settings/InputSystem_Actions).
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

    [Header("Ground Check")]
    [Tooltip("Layers that count as ground. Leave as Everything if unsure.")]
    [SerializeField] LayerMask groundLayers = ~0;

    Rigidbody2D rb;
    InputAction moveAction;
    InputAction jumpAction;
    ContactFilter2D groundFilter;
    float baseGravityScale;

    float moveInput;
    float lastGroundedTime = float.NegativeInfinity;
    bool jumpPressed;

    /// <summary>True while standing on ground.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Horizontal input in the range -1..1.</summary>
    public float MoveInput => moveInput;

    /// <summary>Current velocity of the rigidbody.</summary>
    public Vector2 Velocity => rb.linearVelocity;

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
    }

    void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        jumpAction.Disable();
    }

    void Update()
    {
        if (!CanMove)
        {
            moveInput = 0f;
            jumpPressed = false;
            return;
        }

        moveInput = moveAction.ReadValue<Vector2>().x;
        // Like Input.GetKeyDown: one tap = one jump. Holding does nothing extra.
        // Latched here and consumed in FixedUpdate so a tap is never missed between physics steps.
        if (jumpAction.WasPressedThisFrame())
            jumpPressed = true;
    }

    void FixedUpdate()
    {
        IsGrounded = rb.IsTouching(groundFilter);
        if (IsGrounded)
            lastGroundedTime = Time.time;

        Vector2 velocity = rb.linearVelocity;

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
        }

        // Fall faster than you rise, for a less floaty jump.
        rb.gravityScale = velocity.y < 0f ? baseGravityScale * fallGravityMultiplier : baseGravityScale;

        rb.linearVelocity = velocity;
    }
}
