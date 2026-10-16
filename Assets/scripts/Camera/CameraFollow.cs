using UnityEngine;

/// <summary>
/// Smoothly follows the player. Put this on the Main Camera.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("What to follow. Leave empty to find the player automatically.")]
    [SerializeField] Transform target;
    [Tooltip("Camera position relative to the player. Y raises the view; Z is ignored (the camera keeps its own Z).")]
    [SerializeField] Vector2 offset = new Vector2(0f, 1f);
    [Tooltip("Seconds to catch up with the player. 0 = locked to the player.")]
    [SerializeField] float smoothTime = 0.15f;
    [Tooltip("Follow the player up and down too.")]
    [SerializeField] bool followY = true;

    [Header("Level Limits (optional)")]
    [Tooltip("Stop the camera at the level edges.")]
    [SerializeField] bool useLimits = false;
    [SerializeField] Vector2 minPosition = new Vector2(-20f, -10f);
    [SerializeField] Vector2 maxPosition = new Vector2(20f, 10f);

    Vector3 velocity;

    void Start()
    {
        if (target == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
                target = player.transform;
        }
        if (target != null)
            transform.position = Desired(); // start on the player, no swoop in
    }

    // LateUpdate runs after the player has moved this frame, so the camera doesn't jitter.
    void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, Desired(), ref velocity, smoothTime)
            : Desired();
    }

    Vector3 Desired()
    {
        Vector3 pos = transform.position;
        pos.x = target.position.x + offset.x;
        if (followY)
            pos.y = target.position.y + offset.y;

        if (useLimits)
        {
            pos.x = Mathf.Clamp(pos.x, minPosition.x, maxPosition.x);
            pos.y = Mathf.Clamp(pos.y, minPosition.y, maxPosition.y);
        }
        return pos;
    }
}
