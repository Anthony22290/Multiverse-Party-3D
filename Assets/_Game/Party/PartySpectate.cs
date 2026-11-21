using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Spectator controls for minigames where you can be knocked out (2026-11-19): ←/→ (or A/D, click, D-pad, bumpers)
/// switch between the players still in; Tab toggles the overview. Cameras call <see cref="Pick"/> once per frame.
/// </summary>
public static class PartySpectate
{
    static int index; static int frame = -1;
    public static bool Overview { get; private set; }
    public const string Hint = "←/→ o clic: cambiar de jugador  ·  Tab: vista general";

    public static int Pick(int count)
    {
        if (frame == Time.frameCount) return Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
        frame = Time.frameCount;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        int step = 0;
        if (k != null)
        {
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) step = 1;
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) step = -1;
            if (k.tabKey.wasPressedThisFrame) Overview = !Overview;
        }
        if (m != null && m.leftButton.wasPressedThisFrame) step = 1;
        if (g != null)
        {
            if (g.dpad.right.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame) step = 1;
            if (g.dpad.left.wasPressedThisFrame || g.leftShoulder.wasPressedThisFrame) step = -1;
            if (g.buttonNorth.wasPressedThisFrame) Overview = !Overview;
        }
        if (step != 0) Overview = false;
        if (count <= 0) { index = 0; return 0; }
        index = ((index + step) % count + count) % count;
        return index;
    }
}
