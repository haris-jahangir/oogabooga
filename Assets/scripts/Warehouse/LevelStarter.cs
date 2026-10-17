using System.Collections;
using UnityEngine;

/// <summary>
/// Holds the level still until the switch is used: the fire and the monster wait, and
/// the fog/black cover hides the warehouse. Begin() fades the fog away and starts them.
///
/// Wire it: on the switch's Interactable, add Begin() to On Used. For a level with no switch,
/// tick Start Automatically instead.
/// </summary>
public class LevelStarter : MonoBehaviour
{
    [Tooltip("Started by the switch. Leave empty to use every Fire and the MonsterManager in the scene.")]
    [SerializeField] Behaviour[] startOnSwitch;
    [Tooltip("Black screen / fog sprites covering the warehouse. They fade out, then switch off.")]
    [SerializeField] SpriteRenderer[] fog;
    [Tooltip("Seconds for the fog to fade away.")]
    [SerializeField] float fadeDuration = 1.5f;
    [Tooltip("Optional sound when the switch is flipped.")]
    [SerializeField] AudioClip switchSound;
    [Tooltip("Seconds between the switch and the fire catching, so the switch's own sound is heard first.")]
    [SerializeField] float fireDelay = 1f;
    [Tooltip("Begin on its own a moment after the scene loads (for levels without a switch).")]
    [SerializeField] bool startAutomatically;
    [SerializeField] float autoStartDelay = 2f;

    bool started;

    /// <summary>Seconds after the switch before the fire starts (its sound waits too).</summary>
    public float FireDelay => fireDelay;

    /// <summary>True once the switch has been used.</summary>
    public bool HasStarted => started;

    /// <summary>Raised when the switch is used (lights, music and fire sound listen for this).</summary>
    public event System.Action Started;

    void Awake()
    {
        if (startOnSwitch == null || startOnSwitch.Length == 0)
        {
            var found = new System.Collections.Generic.List<Behaviour>();
            found.AddRange(FindObjectsByType<Fire>(FindObjectsSortMode.None));
            MonsterManager monsters = FindFirstObjectByType<MonsterManager>();
            if (monsters != null)
                found.Add(monsters);
            startOnSwitch = found.ToArray();
        }

        // Disabled before their Start runs, so nothing happens until Begin().
        foreach (Behaviour b in startOnSwitch)
            if (b != null)
                b.enabled = false;
    }

    void Start()
    {
        if (startAutomatically)
            Invoke(nameof(Begin), autoStartDelay);
    }

    /// <summary>Called by the switch: fade the fog and start the fire and the monster.</summary>
    public void Begin()
    {
        if (started)
            return;
        started = true;

        if (switchSound != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(switchSound, Camera.main.transform.position);
        foreach (Behaviour b in startOnSwitch)
            if (b != null && !(b is Fire))
                b.enabled = true;
        StartCoroutine(LightFires());
        StartCoroutine(FadeFog());
        Started?.Invoke();
    }

    IEnumerator LightFires()
    {
        yield return new WaitForSeconds(fireDelay);
        foreach (Behaviour b in startOnSwitch)
            if (b is Fire)
                b.enabled = true;
    }

    IEnumerator FadeFog()
    {
        var startAlpha = new float[fog.Length];
        for (int i = 0; i < fog.Length; i++)
            if (fog[i] != null)
                startAlpha[i] = fog[i].color.a;

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            float k = 1f - t / fadeDuration;
            for (int i = 0; i < fog.Length; i++)
                if (fog[i] != null)
                {
                    Color c = fog[i].color;
                    c.a = startAlpha[i] * k;
                    fog[i].color = c;
                }
            yield return null;
        }

        foreach (SpriteRenderer f in fog)
            if (f != null)
                f.gameObject.SetActive(false);
    }
}
