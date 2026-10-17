using UnityEngine;

/// <summary>A falling concrete chunk from CollapseFX: after a while it breaks into dust and disappears.</summary>
public class SlabCrumble : MonoBehaviour
{
    const float FadeTime = 0.4f;

    CollapseFX owner;
    SpriteRenderer sr;
    float crumbleAt;
    bool crumbling;

    public void Init(CollapseFX source, float lifetime)
    {
        owner = source;
        crumbleAt = Time.time + lifetime;
    }

    void Start() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (!crumbling && Time.time >= crumbleAt)
        {
            crumbling = true;
            if (owner != null)
                owner.Crumble(transform.position);
        }
        if (!crumbling)
            return;

        Color c = sr.color;
        c.a -= Time.deltaTime / FadeTime;
        sr.color = c;
        if (c.a <= 0f)
            Destroy(gameObject);
    }
}
