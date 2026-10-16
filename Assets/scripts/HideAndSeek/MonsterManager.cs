using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the hide-and-seek loop:
///   1. Wait a random 5-10 seconds.
///   2. Warning: lights flicker, the player plays the Scare animation and the warning
///      sound plays. The warning lasts as long as the warning sound (1 second if none).
///   3. The monster appears. If the player is not inside a HidingSpot at any moment
///      while it is there, it's game over and the scene restarts.
///   4. The monster leaves and the loop repeats.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MonsterManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The monster object in the scene. It is hidden until it comes.")]
    [SerializeField] GameObject monster;
    [Tooltip("Lights that flicker during the warning. Leave empty to use every 2D light in the scene.")]
    [SerializeField] Light2D[] flickerLights;
    [Tooltip("Leave empty to find the player automatically.")]
    [SerializeField] PlayerAnimator player;

    [Header("Timing")]
    [SerializeField] float minDelay = 5f;
    [SerializeField] float maxDelay = 10f;
    [Tooltip("Used only if no warning sound is set; otherwise the warning lasts as long as the sound.")]
    [SerializeField] float fallbackWarningDuration = 1f;
    [Tooltip("How long the monster stays. The player must stay hidden the whole time.")]
    [SerializeField] float monsterStayDuration = 6f;
    [Tooltip("Gap kept between the monster and the fire, in units.")]
    [SerializeField] float fireClearance = 0.5f;
    [Tooltip("Pause after being caught before the scene restarts.")]
    [SerializeField] float restartDelay = 1f;

    [Header("Sounds")]
    [Tooltip("Plays during the warning (your 1 second sound).")]
    [SerializeField] AudioClip warningSound;
    [Tooltip("Eerie sound that plays while the monster is there. Optional.")]
    [SerializeField] AudioClip monsterSound;
    [Tooltip("Plays when the player is caught. Optional.")]
    [SerializeField] AudioClip caughtSound;

    [Header("Flicker")]
    [Tooltip("Lowest brightness during the flicker, as a fraction of the normal brightness.")]
    [Range(0f, 1f)] [SerializeField] float flickerMinIntensity = 0.05f;
    [SerializeField] float flickerMinInterval = 0.03f;
    [SerializeField] float flickerMaxInterval = 0.12f;

    AudioSource audioSource;
    float[] baseIntensities;
    bool caught;
    Vector3 monsterHome;
    Fire[] fires;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (player == null)
            player = FindFirstObjectByType<PlayerAnimator>();
        if (flickerLights == null || flickerLights.Length == 0)
            flickerLights = FindObjectsByType<Light2D>(FindObjectsSortMode.None);

        baseIntensities = new float[flickerLights.Length];
        for (int i = 0; i < flickerLights.Length; i++)
            baseIntensities[i] = flickerLights[i].intensity;

        fires = FindObjectsByType<Fire>(FindObjectsSortMode.None);
        if (monster != null)
        {
            monsterHome = monster.transform.position;
            monster.SetActive(false);
        }
    }

    void Start() => StartCoroutine(Loop());

    IEnumerator Loop()
    {
        while (!caught)
        {
            yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
            yield return Warning();
            yield return MonsterVisit();
        }
    }

    IEnumerator Warning()
    {
        // Scare plays but the player can still run for a shadow.
        if (player != null)
            player.SetScared(true, freezeMovement: false);

        float duration = warningSound != null ? warningSound.length : fallbackWarningDuration;
        if (warningSound != null)
            audioSource.PlayOneShot(warningSound);

        float end = Time.time + duration;
        while (Time.time < end)
        {
            SetLightLevel(Random.Range(flickerMinIntensity, 1f));
            yield return new WaitForSeconds(Random.Range(flickerMinInterval, flickerMaxInterval));
        }

        SetLightLevel(1f);
        if (player != null)
            player.SetScared(false);
    }

    IEnumerator MonsterVisit()
    {
        if (monster != null)
        {
            monster.transform.position = monsterHome;
            monster.SetActive(true);
            KeepMonsterOutOfFire();
        }
        if (monsterSound != null)
        {
            audioSource.clip = monsterSound;
            audioSource.Play();
        }

        float end = Time.time + monsterStayDuration;
        while (Time.time < end)
        {
            if (!HidingSpot.IsPlayerHidden)
            {
                yield return GameOver();
                yield break;
            }
            yield return null;
            KeepMonsterOutOfFire(); // the fire keeps spreading during the visit
        }

        audioSource.Stop();
        if (monster != null)
            monster.SetActive(false);
    }

    /// <summary>
    /// Ends the run and restarts the scene. Used by other hazards such as Fire.
    /// </summary>
    public void CatchPlayer()
    {
        if (caught)
            return;
        StopAllCoroutines();
        SetLightLevel(1f);
        if (monster != null)
            monster.SetActive(false);
        StartCoroutine(GameOver());
    }

    IEnumerator GameOver()
    {
        caught = true;
        audioSource.Stop();
        if (caughtSound != null)
            audioSource.PlayOneShot(caughtSound);
        if (player != null)
            player.SetScared(true, freezeMovement: true);

        yield return new WaitForSeconds(restartDelay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// If the monster overlaps a fire, move it sideways to just in front of the fire's spreading edge.
    /// </summary>
    void KeepMonsterOutOfFire()
    {
        if (monster == null || fires.Length == 0)
            return;
        Renderer body = monster.GetComponentInChildren<Renderer>();
        if (body == null)
            return;

        foreach (Fire fire in fires)
        {
            if (fire == null)
                continue;
            Bounds f = fire.Area;
            Bounds m = body.bounds;
            bool overlapsX = m.max.x > f.min.x - fireClearance && m.min.x < f.max.x + fireClearance;
            bool overlapsY = m.max.y > f.min.y && m.min.y < f.max.y;
            if (!overlapsX || !overlapsY)
                continue;

            float shift = fire.GrowDirection == Fire.Direction.Right
                ? (f.max.x + fireClearance) - m.min.x
                : (f.min.x - fireClearance) - m.max.x;
            monster.transform.position += new Vector3(shift, 0f, 0f);
        }
    }

    void SetLightLevel(float fraction)
    {
        for (int i = 0; i < flickerLights.Length; i++)
            if (flickerLights[i] != null)
                flickerLights[i].intensity = baseIntensities[i] * fraction;
    }
}
