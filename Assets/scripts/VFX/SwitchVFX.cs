using UnityEngine;

/// <summary>
/// When the light switch is used: a shower of electric sparks and a puff of smoke from the
/// switch, a crackle, and a small camera jolt. Finds the switch by looking for the
/// Interactable whose On Used calls LevelStarter.Begin.
/// Added to the LevelStarter automatically (see VfxBootstrap).
/// </summary>
[RequireComponent(typeof(LevelStarter))]
public class SwitchVFX : MonoBehaviour
{
    [Tooltip("Optional: the switch picture once flipped (e.g. Prop_Switch_On).")]
    [SerializeField] Sprite switchOnSprite;
    [SerializeField] Color sparkColor = new(1f, 0.9f, 0.55f);
    [SerializeField] int sparkCount = 40;
    [SerializeField] AudioClip sparkSound;
    [Range(0f, 1f)] [SerializeField] float sparkVolume = 0.6f;

    LevelStarter starter;

    void Start()
    {
        starter = GetComponent<LevelStarter>();
        if (sparkSound == null)
            sparkSound = Vfx.Sound("spark");
        starter.Started += OnSwitch;
    }

    void OnDestroy()
    {
        if (starter != null)
            starter.Started -= OnSwitch;
    }

    void OnSwitch()
    {
        foreach (Interactable i in FindObjectsByType<Interactable>(FindObjectsSortMode.None))
            for (int e = 0; e < i.onUsed.GetPersistentEventCount(); e++)
                if (i.onUsed.GetPersistentTarget(e) == starter && i.onUsed.GetPersistentMethodName(e) == nameof(LevelStarter.Begin))
                {
                    Burst(i.transform.position);
                    if (switchOnSprite != null && i.TryGetComponent(out SpriteRenderer sr))
                        sr.sprite = switchOnSprite;
                    break;
                }
        Vfx.Play2D(sparkSound, sparkVolume);
        CameraFX.Shake(0.2f);
    }

    void Burst(Vector3 at)
    {
        var root = new GameObject("Switch Sparks").transform;
        root.position = at;
        Destroy(root.gameObject, 3f);

        ParticleSystem sparks = Vfx.MakeSystem(root, "Sparks", Vfx.Additive, 150);
        var main = sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = sparkColor;
        main.gravityModifier = 1.2f;
        var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.05f;
        var col = sparks.colorOverLifetime; col.enabled = true; col.color = Vfx.Fade(Color.white, new Color(1f, 0.4f, 0.1f));
        var r = sparks.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.05f;
        r.lengthScale = 1.5f;
        sparks.Emit(sparkCount);

        ParticleSystem puff = Vfx.MakeSystem(root, "Smoke", Vfx.Alpha, 149);
        var pm = puff.main;
        pm.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        pm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        pm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        pm.startColor = new Color(0.6f, 0.6f, 0.6f, 0.4f);
        pm.gravityModifier = -0.05f;
        var pc = puff.colorOverLifetime; pc.enabled = true; pc.color = Vfx.Fade(Color.white, Color.white);
        var ps = puff.sizeOverLifetime; ps.enabled = true; ps.size = Vfx.Shrink(0.5f, 1.5f);
        puff.Emit(6);
    }
}
