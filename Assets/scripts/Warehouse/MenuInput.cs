using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// "Get me out of this screen" input for clue pictures and the keypad: Escape, any arrow
/// or WASD movement key, a mouse click anywhere, or the gamepad's back button / stick.
/// </summary>
public static class MenuInput
{
    public static bool DismissPressed()
    {
        Keyboard k = Keyboard.current;
        if (k != null && (k.escapeKey.wasPressedThisFrame
            || k.leftArrowKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame
            || k.upArrowKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame
            || k.aKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame
            || k.wKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame))
            return true;

        Mouse m = Mouse.current;
        if (m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame))
            return true;

        Gamepad g = Gamepad.current;
        return g != null && (g.buttonEast.wasPressedThisFrame || g.dpad.left.wasPressedThisFrame
            || g.dpad.right.wasPressedThisFrame || Mathf.Abs(g.leftStick.ReadValue().x) > 0.5f);
    }
}
