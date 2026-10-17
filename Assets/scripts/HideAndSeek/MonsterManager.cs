using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Runs the hide-and-seek loop:
///   1. Wait a random 5-10 seconds.
///   2. Warning: lights flicker, the player plays the Scare animation and the warning
///      sound plays. The flicker lasts Warning Duration seconds (the sound may run on).
///   3. The monster appears above the player, 1-5 units to one side, never in fire. If the player is not inside a HidingSpot at any moment
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
    [Tooltip("How long the lights flicker before the monster comes, in seconds.")]
    [FormerlySerializedAs("fallbackWarningDuration")]
    [Min(0f)] [SerializeField] float warningDuration = 1.5f;
    [Tooltip("How long the monster stays. The player must stay hidden the whole time.")]
    [SerializeField] float monsterStayDuration = 6f;
    [Header("Spawn Position")]
    [Tooltip("Closest the monster appears to the player, sideways, in units.")]
    [SerializeField] float minSpawnDistance = 1f;
    [Tooltip("Furthest the monster appears from the player, sideways, in units.")]
    [SerializeField] float maxSpawnDistance = 5f;
    [Tooltip("How far above the player the monster appears.")]
    [SerializeField] float spawnHeightAbovePlayer = 1f;
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
            monster.SetActive(false);
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

        float duration = warningDuration;
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
            monster.SetActive(true);
            PlaceMonsterNearPlayer();
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
    /// Stops the monster for good (e.g. once the level is solved): no more warnings or visits.
    /// </summary>
    public void StopHunting()
    {
        if (caught)
            return;
        StopAllCoroutines();
        SetLightLevel(1f);
        audioSource.Stop();
        if (monster != null)
            monster.SetActive(false);
        if (player != null)
            player.SetScared(false);
        enabled = false;
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
    /// Puts the monster above the player, 1-5 units to the left or right.
    /// Tries a few random spots to find one outside the fire.
    /// </summary>
    void PlaceMonsterNearPlayer()
    {
        if (player == null)
            return;

        Vector3 origin = player.transform.position;
        Vector3 pos = monster.transform.position;
        pos.y = origin.y + spawnHeightAbovePlayer;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            pos.x = origin.x + side * Random.Range(minSpawnDistance, maxSpawnDistance);
            monster.transform.position = pos;
            if (!MonsterInFire())
                return;
        }
        // Every try landed in fire; KeepMonsterOutOfFire pushes it clear.
    }

    bool MonsterInFire()
    {
        Renderer body = monster.GetComponentInChildren<Renderer>();
        if (body == null)
            return false;
        Bounds m = body.bounds;
        foreach (Fire fire in fires)
        {
            if (fire == null)
                continue;
            Bounds f = fire.Area;
            if (m.max.x > f.min.x - fireClearance && m.min.x < f.max.x + fireClearance
                && m.max.y > f.min.y && m.min.y < f.max.y)
                return true;
        }
        return false;
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
