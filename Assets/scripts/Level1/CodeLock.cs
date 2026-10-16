using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The carriage 1 combination lock. Clues call Reveal(position) and the HUD fills in
/// that digit (7 _ 9 _). Open() shows the dial: A/D pick a wheel, W/S turn it,
/// E or Enter tries the code, Esc steps away. The right code removes the door.
/// When the shadow's warning starts, the player is pulled off the dial to go hide.
/// </summary>
public class CodeLock : MonoBehaviour
{
    public string code = "7391";
    public GameObject door;

    readonly bool[] found = new bool[4];
    readonly int[] wheels = new int[4];
    int wheel, openedFrame;
    bool dialOpen, solved;
    PlayerMovement player;
    string message;
    float messageUntil;

    void Start() => player = FindFirstObjectByType<PlayerMovement>();

    public void Reveal(int position)
    {
        found[position] = true;
        Say("You found a digit: " + Digits(i => i == position));
    }

    public void Say(string text)
    {
        message = text;
        messageUntil = Time.time + 3f;
    }

    public void Open()
    {
        if (solved || player == null)
            return;
        dialOpen = true;
        openedFrame = Time.frameCount; // the E that opened the dial must not also try the code
        Interactable.Blocked = true;
        player.CanMove = false;
    }

    void Close()
    {
        dialOpen = false;
        Interactable.Blocked = false;
        if (player != null)
            player.CanMove = true;
    }

    void OnDisable()
    {
        if (dialOpen)
            Close();
    }

    void Update()
    {
        if (!dialOpen || Time.frameCount == openedFrame)
            return;
        // Something else gave movement back: that's the shadow's warning. Go hide.
        if (player.CanMove)
        {
            dialOpen = false;
            Interactable.Blocked = false;
            Say("Something is coming. Hide!");
            return;
        }

        Keyboard k = Keyboard.current;
        if (k == null)
            return;
        if (k.escapeKey.wasPressedThisFrame)
            Close();
        else if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame)
            wheel = (wheel + 3) % 4;
        else if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame)
            wheel = (wheel + 1) % 4;
        else if (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)
            wheels[wheel] = (wheels[wheel] + 1) % 10;
        else if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame)
            wheels[wheel] = (wheels[wheel] + 9) % 10;
        else if (k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)
            TryCode();
    }

    void TryCode()
    {
        if (string.Concat(wheels) != code)
        {
            Say("The lock rattles. Wrong code.");
            return;
        }
        solved = true;
        Close();
        if (door != null)
            door.SetActive(false);
        Say("The lock clicks open.");
    }

    string Digits(System.Func<int, bool> show)
    {
        var parts = new string[4];
        for (int i = 0; i < 4; i++)
            parts[i] = show(i) ? code[i].ToString() : "_";
        return string.Join(" ", parts);
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        GUI.Label(new Rect(20f, 16f, 400f, 34f), "CODE   " + Digits(i => found[i]), style);
        if (Time.time < messageUntil)
        {
            var centre = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(0f, Screen.height - 90f, Screen.width, 40f), message, centre);
        }
        if (!dialOpen)
            return;

        var box = new Rect(Screen.width / 2f - 170f, Screen.height / 2f - 80f, 340f, 160f);
        GUI.Box(box, "");
        var big = new GUIStyle(style) { fontSize = 40, alignment = TextAnchor.MiddleCenter };
        string dial = "";
        for (int i = 0; i < 4; i++)
            dial += i == wheel ? $"[{wheels[i]}]" : $" {wheels[i]} ";
        GUI.Label(new Rect(box.x, box.y + 20f, box.width, 60f), dial, big);
        var hint = new GUIStyle(style) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(box.x, box.y + 100f, box.width, 40f), "A/D wheel   W/S turn   E try   Esc step away", hint);
    }
}
