#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Touch controls test (2026-11-21), in Play mode from any minigame scene:
///   run MobileTouchTest.Run
/// Adds a fake touchscreen, puts fingers on the joystick, buttons, camera area and code pad of several scenes and checks
/// what the game receives (Keyboard/Mouse/Gamepad.current) and that the player really moves. Screenshots in
/// Docs/Claude_Pruebas/tactil_*.png.
/// </summary>
public static class MobileTouchTest
{
    static readonly List<string> report = new List<string>();
    static Touchscreen screen;

    /// <summary>On PC (no -touch, no PlayerPrefs) the touch layer must not exist.</summary>
    public static string PcCheck() =>
        (!MobileControls.Enabled && MobileControls.Instance == null && Object.FindAnyObjectByType<MobileControls>() == null ? "PASS" : "FAIL")
        + $" PC: táctil activado={MobileControls.Enabled} · capa táctil creada={MobileControls.Instance != null} · teclado actual={Keyboard.current?.name}";

    public static async Task<string[]> Run()
    {
        report.Clear();
        if (!Application.isPlaying) return new[] { "FAIL no está en Play" };
        if (MobileControls.Instance == null) { var go = new GameObject("Controles táctiles"); Object.DontDestroyOnLoad(go); go.AddComponent<MobileControls>(); }
        var mc = MobileControls.Instance;
        screen = InputSystem.AddDevice<Touchscreen>("Pantalla de prueba");
        MobileControls.TestScreen = screen;
        Directory.CreateDirectory("Docs/Claude_Pruebas");
        try
        {
            await Colina(mc);
            await Simon(mc);
            await Mortal(mc);
        }
        catch (System.Exception e) { report.Add("FAIL excepción: " + e); }
        finally
        {
            MobileControls.TestScreen = null;
            if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
        }
        File.WriteAllLines("Docs/Claude_Pruebas/Tactil.txt", report);
        return report.ToArray();
    }

    static async Task Load(string scene)
    {
        if (SceneManager.GetActiveScene().name != scene) SceneManager.LoadScene(scene);
        for (int i = 0; i < 100 && SceneManager.GetActiveScene().name != scene; i++) await Task.Delay(100);
        await Task.Delay(2500);
    }

    static void Shot(string name)
    {
        var cam = Camera.main;
        if (cam != null) ClaudeBridge.CaptureCamera(cam, Path.GetFullPath($"Docs/Claude_Pruebas/tactil_{name}.png"), 1280, 720);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath($"Docs/Claude_Pruebas/tactil_{name}_gui.png"));
    }

    static void Touch(int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 pos) =>
        InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = pos, pressure = 1f });

    static async Task Frames(int n) { for (int i = 0; i < n; i++) await Task.Yield(); await Task.Delay(30 * n); }

    static void Check(bool ok, string what) => report.Add((ok ? "PASS " : "FAIL ") + what);

    static Vector2 ButtonCenter(MobileControls mc, int i) => mc.ButtonRect(i).center;

    static async Task Colina(MobileControls mc)
    {
        await Load("ReyColina");
        var d = Object.FindAnyObjectByType<ColinaDirector>();
        for (int i = 0; i < 120 && d != null && d.State != ColinaDirector.Phase.Playing; i++) await Task.Delay(100);
        Check(mc.Showing, $"REY DE LA COLINA: controles visibles ({mc.Layout.Buttons.Length} botones, joystick {mc.Layout.Stick})");
        // Joystick: thumb down on the left, then to the right.
        var start = new Vector2(Screen.width * .15f, Screen.height * .25f);
        Vector3 before = d.Human.transform.position;
        Touch(1, UnityEngine.InputSystem.TouchPhase.Began, start); await Frames(2);
        for (int s = 1; s <= 5; s++) { Touch(1, UnityEngine.InputSystem.TouchPhase.Moved, start + new Vector2(30f * s, 0)); await Frames(1); }
        await Task.Delay(1200);
        bool dKey = Keyboard.current != null && Keyboard.current.dKey.isPressed;
        float stickX = Gamepad.current != null ? Gamepad.current.leftStick.x.ReadValue() : 0f;
        Shot("colina");
        Vector3 after = d.Human.transform.position;
        Touch(1, UnityEngine.InputSystem.TouchPhase.Ended, start + new Vector2(150, 0)); await Frames(3);
        Check(dKey && stickX > .5f, $"Joystick a la derecha → tecla D={dKey} · stick x={stickX:0.00} (teclado actual: {Keyboard.current?.name})");
        Check((after - before).magnitude > .5f, $"El personaje se movió {(after - before).magnitude:0.0} m con el joystick");
        Check(Keyboard.current == null || !Keyboard.current.dKey.isPressed, "Al soltar el joystick se suelta la D");
        // Jump button = Space.
        var jump = ButtonCenter(mc, 0);
        Touch(2, UnityEngine.InputSystem.TouchPhase.Began, jump); await Frames(2);
        bool space = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        Touch(2, UnityEngine.InputSystem.TouchPhase.Ended, jump); await Frames(4);
        Check(space, $"Botón {mc.Layout.Buttons[0].Label} → Espacio pulsado");
        Check(!Keyboard.current.spaceKey.isPressed, "Al soltar el botón se suelta Espacio");
        // Two fingers at once: joystick + button.
        Touch(3, UnityEngine.InputSystem.TouchPhase.Began, start); await Frames(1);
        Touch(3, UnityEngine.InputSystem.TouchPhase.Moved, start + new Vector2(0, 140)); await Frames(1);
        var bomb = ButtonCenter(mc, 1);
        Touch(4, UnityEngine.InputSystem.TouchPhase.Began, bomb); await Frames(2);
        bool both = Keyboard.current.wKey.isPressed && Keyboard.current.eKey.isPressed;
        Touch(4, UnityEngine.InputSystem.TouchPhase.Ended, bomb); Touch(3, UnityEngine.InputSystem.TouchPhase.Ended, start); await Frames(3);
        Check(both, "Dos dedos a la vez: W (joystick arriba) + E (bomba)");
    }

    static async Task Simon(MobileControls mc)
    {
        await Load("MemoriaMusical");
        Check(mc.Showing && !mc.Layout.Stick && mc.Layout.Buttons.Length >= 4, "MEMORIA MUSICAL: 4 botones de colores, sin joystick");
        string[] names = { "↑", "→", "↓", "←" }; Key[] keys = { Key.UpArrow, Key.RightArrow, Key.DownArrow, Key.LeftArrow };
        Shot("memoria");
        for (int i = 0; i < 4; i++)
        {
            var p = ButtonCenter(mc, i);
            Touch(10 + i, UnityEngine.InputSystem.TouchPhase.Began, p); await Frames(2);
            bool ok = Keyboard.current[keys[i]].isPressed;
            Touch(10 + i, UnityEngine.InputSystem.TouchPhase.Ended, p); await Frames(3);
            Check(ok, $"Botón {mc.Layout.Buttons[i].Label} → flecha {names[i]}");
        }
    }

    static async Task Mortal(MobileControls mc)
    {
        await Load("CodigoMortal");
        Check(mc.Showing && mc.Layout.Look == MobileLayouts.Look.RightDrag, "CÓDIGO MORTAL: controles con cámara arrastrando");
        // Drag on the right half: right mouse button held + mouse movement (sampled after every input update).
        var p = new Vector2(Screen.width * .7f, Screen.height * .6f);
        float moved = 0f; bool right = false;
        System.Action sample = () => { if (Mouse.current != null) { moved += Mouse.current.delta.x.ReadValue(); right |= Mouse.current.rightButton.isPressed; } };
        InputSystem.onAfterUpdate += sample;
        Touch(20, UnityEngine.InputSystem.TouchPhase.Began, p); await Frames(2);
        for (int s = 1; s <= 8; s++) { Touch(20, UnityEngine.InputSystem.TouchPhase.Moved, p + new Vector2(25f * s, 0)); await Frames(1); }
        await Frames(3);
        Touch(20, UnityEngine.InputSystem.TouchPhase.Ended, p + new Vector2(200, 0)); await Frames(3);
        InputSystem.onAfterUpdate -= sample;
        Check(right && moved > 0f, $"Arrastrar a la derecha → botón derecho={right} · movimiento total del ratón x={moved:0}");
        // Code pad (forced open): type 1-2-3-4, then BORRAR and OK.
        MobileControls.ForcePad = true; await Frames(3);
        Shot("teclado_codigos");
        string typed = ""; int digitKeys = 0;
        void OnText(char c) => typed += c;
        var kb = Keyboard.current;
        kb.onTextInput += OnText;
        System.Action keys = () => { var k = Keyboard.current; if (k != null) for (int i = 1; i <= 4; i++) if (k[Key.Digit1 + (i - 1)].wasPressedThisFrame) digitKeys++; };
        InputSystem.onAfterUpdate += keys;
        foreach (char c in "1234")
        {
            var r = MobileControls.Instance.PadKeyCenter(c);
            Touch(30, UnityEngine.InputSystem.TouchPhase.Began, r); await Frames(2);
            Touch(30, UnityEngine.InputSystem.TouchPhase.Ended, r); await Frames(3);
        }
        InputSystem.onAfterUpdate -= keys;
        kb.onTextInput -= OnText;
        MobileControls.ForcePad = false; await Frames(2);
        Check(typed == "1234" && digitKeys >= 4, $"Teclado de códigos: tocar 1-2-3-4 escribe \"{typed}\" y pulsa {digitKeys} teclas de número");
    }

}
#endif
