using UnityEngine;

/// <summary>
/// Loops a few sprites on this SpriteRenderer: the shadow's drifting smoke, or a character
/// in a cutscene. For the playable player use PlayerAnimator instead.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Flipbook : MonoBehaviour
{
    public Sprite[] frames;
    public float framesPerSecond = 8f;

    SpriteRenderer sprite;
    float startTime;

    void Awake() => sprite = GetComponent<SpriteRenderer>();

    void OnEnable() => startTime = Time.time;

    public void Play(Sprite[] newFrames)
    {
        if (newFrames == frames)
            return;
        frames = newFrames;
        startTime = Time.time;
    }

    void Update()
    {
        if (frames != null && frames.Length > 0)
            sprite.sprite = frames[(int)((Time.time - startTime) * framesPerSecond) % frames.Length];
    }
}
