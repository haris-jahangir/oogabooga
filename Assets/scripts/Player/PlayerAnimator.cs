using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the player's Animator from PlayerMovement and flips the sprite to face
/// the direction of travel. Animator parameters (created by Tools > Player > Build Animations):
///   Speed (float)    - absolute horizontal speed
///   Grounded (bool)  - standing on ground
///   VelocityY (float)- vertical speed
///   Scared (bool)    - plays the Scare animation
/// </summary>
[RequireComponent(typeof(Animator), typeof(SpriteRenderer), typeof(PlayerMovement))]
public class PlayerAnimator : MonoBehaviour
{
    static readonly int SpeedHash = Animator.StringToHash("Speed");
    static readonly int GroundedHash = Animator.StringToHash("Grounded");
    static readonly int VelocityYHash = Animator.StringToHash("VelocityY");
    static readonly int ScaredHash = Animator.StringToHash("Scared");

    [Tooltip("Set if the source images face left instead of right.")]
    [SerializeField] bool spritesFaceLeft = false;
    [Tooltip("Stop the player from moving while the Scare animation plays.")]
    [SerializeField] bool freezeWhileScared = true;
    [Tooltip("Press this key in Play mode to toggle the Scare animation, for testing. Set to None to disable.")]
    [SerializeField] Key debugScareKey = Key.F;

    Animator animator;
    SpriteRenderer spriteRenderer;
    PlayerMovement movement;

    /// <summary>True while the Scare animation is active.</summary>
    public bool IsScared { get; private set; }

    void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<PlayerMovement>();
    }

    /// <summary>Start or stop the Scare animation. Call this from enemies, triggers, etc.</summary>
    public void SetScared(bool scared)
    {
        IsScared = scared;
        animator.SetBool(ScaredHash, scared);
        if (freezeWhileScared)
            movement.CanMove = !scared;
    }

    void Update()
    {
        if (debugScareKey != Key.None && Keyboard.current != null && Keyboard.current[debugScareKey].wasPressedThisFrame)
            SetScared(!IsScared);

        Vector2 velocity = movement.Velocity;
        animator.SetFloat(SpeedHash, Mathf.Abs(velocity.x));
        animator.SetBool(GroundedHash, movement.IsGrounded);
        animator.SetFloat(VelocityYHash, velocity.y);

        // Face the direction the player is pushing; keep the last facing when idle.
        float input = movement.MoveInput;
        if (Mathf.Abs(input) > 0.01f)
            spriteRenderer.flipX = (input < 0f) != spritesFaceLeft;
    }
}
