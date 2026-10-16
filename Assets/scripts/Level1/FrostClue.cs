using UnityEngine;

/// <summary>
/// A digit traced in a window's frost. It only shows while the shadow is visiting,
/// and the first time it shows it counts as found on the lock.
/// </summary>
public class FrostClue : MonoBehaviour
{
    [Tooltip("The shadow object that MonsterManager turns on during a visit.")]
    public GameObject shadow;
    public CodeLock codeLock;
    public int digitPosition = 3;
    [Tooltip("The frost writing, hidden except during a visit.")]
    public Renderer frostText;

    bool revealed;

    void Update()
    {
        bool visiting = shadow != null && shadow.activeInHierarchy;
        if (frostText != null)
            frostText.enabled = visiting;
        if (visiting && !revealed && codeLock != null)
        {
            revealed = true;
            codeLock.Reveal(digitPosition);
        }
    }
}
