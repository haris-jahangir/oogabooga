using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plays the player's frame animations (Idle, Walk, Jump, Scare) by swapping sprites
/// on the SpriteRenderer, and flips the sprite to face the direction of travel.
/// No Animator or controller needed: drag 4 PNG sprites into each array in the Inspector.
///
/// Priority: Scare > Jump (in the air) > Walk (moving) > Idle.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(PlayerMovement))]
public class PlayerAnimator : MonoBehaviour
{
    enum AnimState { Idle, Walk, Jump, Scare }

    [Header("Frames (4 PNGs each, in order)")]
    [SerializeField] Sprite[] idleFrames = new Sprite[4];
    [SerializeField] Sprite[] walkFrames = new Sprite[4];
    [SerializeField] Sprite[] jumpFrames = new Sprite[4];
    [SerializeField] Sprite[] scareFrames = new Sprite[4];

    [Header("Playback")]
    [Tooltip("Animation frames per second.")]
    [SerializeField] float framesPerSecond = 8f;
    [Tooltip("Horizontal speed above which the Walk animation plays.")]
    [SerializeField] float walkSpeedThreshold = 0.1f;

    [Header("Behaviour")]
    [Tooltip("Set if the source images face left instead of right.")]
    [SerializeField] bool spritesFaceLeft = false;
    [Tooltip("Stop the player from moving while the Scare animation plays.")]
    [SerializeField] bool freezeWhileScared = true;
    [Tooltip("Press this key in Play mode to toggle the Scare animation, for testing. Set to None to disable.")]
    [SerializeField] Key debugScareKey = Key.F;

    SpriteRenderer spriteRenderer;
    PlayerMovement movement;

    AnimState state = AnimState.Idle;
    float stateTime;

    /// <summary>True while the Scare animation is active.</summary>
    public bool IsScared { get; private set; }

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        movement = GetComponent<PlayerMovement>();

        // An Animator would overwrite the sprite every frame, so make sure it doesn't fight us.
        if (TryGetComponent(out Animator animator))
            animator.enabled = false;
    }

    /// <summary>Start or stop the Scare animation. Call this from enemies, triggers, etc.</summary>
    public void SetScared(bool scared) => SetScared(scared, freezeWhileScared);

    /// <summary>Start or stop the Scare animation, choosing whether the player can still move.</summary>
    public void SetScared(bool scared, bool freezeMovement)
    {
        IsScared = scared;
        movement.CanMove = !(scared && freezeMovement);
    }

    void Update()
    {
        if (debugScareKey != Key.None && Keyboard.current != null && Keyboard.current[debugScareKey].wasPressedThisFrame)
            SetScared(!IsScared);

        AnimState next = PickState();
        if (next != state)
        {
            state = next;
            stateTime = 0f;
        }
        else
        {
            stateTime += Time.deltaTime;
        }

        Sprite[] frames = FramesFor(state);
        bool loop = state != AnimState.Jump; // Jump plays once and holds its last frame.
        Sprite frame = GetFrame(frames, stateTime, loop);
        if (frame != null)
            spriteRenderer.sprite = frame;

        // Face the direction the player is pushing; keep the last facing when idle.
        float input = movement.MoveInput;
        if (Mathf.Abs(input) > 0.01f)
            spriteRenderer.flipX = (input < 0f) != spritesFaceLeft;
    }

    AnimState PickState()
    {
        if (IsScared)
            return AnimState.Scare;
        if (!movement.IsGrounded)
            return AnimState.Jump;
        if (Mathf.Abs(movement.Velocity.x) > walkSpeedThreshold)
            return AnimState.Walk;
        return AnimState.Idle;
    }

    Sprite[] FramesFor(AnimState s) => s switch
    {
        AnimState.Walk => walkFrames,
        AnimState.Jump => jumpFrames,
        AnimState.Scare => scareFrames,
        _ => idleFrames,
    };

    Sprite GetFrame(Sprite[] frames, float time, bool loop)
    {
        if (frames == null || frames.Length == 0)
            return null;

        int index = Mathf.FloorToInt(time * framesPerSecond);
        index = loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1);
        return frames[index];
    }
}
