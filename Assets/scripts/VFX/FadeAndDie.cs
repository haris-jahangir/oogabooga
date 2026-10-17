using UnityEngine;

/// <summary>Fades a SpriteRenderer out, then destroys its object.</summary>
public class FadeAndDie : MonoBehaviour
{
    public float duration = 0.25f;
    SpriteRenderer sr;
    float startAlpha, t;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        startAlpha = sr.color.a;
    }

    void Update()
    {
        t += Time.deltaTime;
        Color c = sr.color;
        c.a = startAlpha * (1f - t / duration);
        sr.color = c;
        if (t >= duration)
            Destroy(gameObject);
    }
}
