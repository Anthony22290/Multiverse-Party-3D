using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Touch controls for the Android version (2026-11-21).
///
/// The game reads Keyboard.current / Mouse.current / Gamepad.current directly in ~56 scripts. Instead of changing
/// each of them, this layer adds a VIRTUAL keyboard, mouse and gamepad to the Input System and "presses" them from
/// the screen: a floating joystick (WASD + left stick), round buttons (any key or mouse button), dragging on the right
/// half (mouse delta, optionally holding the right button) and a code pad (digits / letters + text events). Each
/// scene says which controls it wants in <see cref="MobileLayouts"/>.
///
/// Active on phones and tablets (Application.isMobilePlatform), or on PC with the "-touch" argument or
/// PlayerPrefs Multiverse_Tactil = 1 (to test). On PC nothing is created and the game is unchanged.
/// </summary>
public sealed class MobileControls : MonoBehaviour
{
    public static MobileControls Instance { get; private set; }
    public static bool Enabled =>
        Application.isMobilePlatform || PlayerPrefs.GetInt("Multiverse_Tactil", 0) == 1 || System.Environment.CommandLine.Contains("-touch");

    /// <summary>Tests: read the touches from this screen instead of Touchscreen.current.</summary>
    public static Touchscreen TestScreen;
    /// <summary>Tests: show the code pad whatever the scene says.</summary>
    public static bool ForcePad;

    Keyboard vk; Mouse vm; Gamepad vg;
    MobileLayouts.Layout layout = MobileLayouts.Empty;
    string layoutScene = "";

    // Touch tracking.
    enum Role { None, Stick, Look, Button, Pad }
    sealed class Finger { public Role role; public int index; public Vector2 start, last; public bool seen; }
    readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();

    // Output state.
    Vector2 stick, stickBase; bool stickOn; Vector2 lookDelta; bool lookOn;
    readonly HashSet<Key> held = new HashSet<Key>();
    readonly List<(Key key, int frames)> taps = new List<(Key, int)>();   // short presses (pad keys) kept down 2 frames
    readonly List<char> text = new List<char>();
    bool mouseL, mouseR;
    float[] buttonDownAt = new float[0];
    bool[] buttonDown = new bool[0];

    // Drawing.
    Texture2D circle, ring;
    GUIStyle label, small;
    float scale;
    Rect safe;

    static readonly string[] VirtualNames = { "Teclado táctil", "Ratón táctil", "Mando táctil" };

    /// <summary>Editor: virtual devices left by an earlier Play session (or a test) would stay ".current".</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void CleanUp()
    {
        Instance = null;
        foreach (var d in InputSystem.devices.ToArray())
            if (System.Array.IndexOf(VirtualNames, d.name) >= 0) InputSystem.RemoveDevice(d);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || Instance != null) return;
        var go = new GameObject("Controles táctiles");
        DontDestroyOnLoad(go);
        go.AddComponent<MobileControls>();
    }

    void Awake()
    {
        Instance = this;
        vk = InputSystem.AddDevice<Keyboard>("Teclado táctil");
        vm = InputSystem.AddDevice<Mouse>("Ratón táctil");
        vg = InputSystem.AddDevice<Gamepad>("Mando táctil");
        circle = MakeCircle(128, false); ring = MakeCircle(128, true);
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.multiTouchEnabled = true;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var d in new InputDevice[] { vk, vm, vg }) if (d != null && d.added) InputSystem.RemoveDevice(d);
    }

    // ------------------------------------------------------------------ state

    /// <summary>The controls are drawn and read right now (not in menus, loading screens, popups…).</summary>
    public bool Showing { get; private set; }
    public MobileLayouts.Layout Layout => layout;
    public Rect ButtonRect(int i) => i >= 0 && i < layout.Buttons.Length ? ButtonScreenRect(i) : default;

    bool Visible()
    {
        if (layout.Empty) return false;
        if (PartyGameIntro.Holding) return false;
        if (MultiverseSettingsPanel.IsOpen) return false;
        var p = MultiverseParty.Instance;
        if (p != null && (p.Loading || (p.Online && !p.IsHost && !p.Seated))) return false;
        return true;
    }

    bool PadOpen => ForcePad || (layout.Pad != null && SafePad());
    bool SafePad() { try { return layout.Pad(); } catch { return false; } }

    void Update()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene != layoutScene) { layoutScene = scene; layout = MobileLayouts.For(scene); ResetAll(); buttonDown = new bool[layout.Buttons.Length]; buttonDownAt = new float[layout.Buttons.Length]; }
        scale = Mathf.Min(Screen.height / 720f, Screen.width / 1280f) * 1.05f;
        safe = Screen.safeArea;
        bool show = Visible();
        if (show != Showing) { Showing = show; if (!show) ResetAll(); }
        if (Showing) ReadTouches();
        Write();
    }

    void ResetAll()
    {
        fingers.Clear(); held.Clear(); taps.Clear(); text.Clear();
        stick = Vector2.zero; stickOn = false; lookOn = false; lookDelta = Vector2.zero; mouseL = mouseR = false;
        for (int i = 0; i < buttonDown.Length; i++) buttonDown[i] = false;
    }

    // ------------------------------------------------------------------ touches

    void ReadTouches()
    {
        var screen = TestScreen != null ? TestScreen : Touchscreen.current;
        foreach (var f in fingers.Values) f.seen = false;
        lookDelta = Vector2.zero;
        bool pad = PadOpen;
        if (screen != null)
        {
            foreach (var t in screen.touches)
            {
                if (!t.isInProgress) continue;
                int id = t.touchId.ReadValue();
                Vector2 pos = t.position.ReadValue();
                if (!fingers.TryGetValue(id, out var f)) { f = Begin(id, pos, pad); fingers[id] = f; }
                f.seen = true;
                Move(f, pos);
            }
        }
        var gone = new List<int>();
        foreach (var kv in fingers) if (!kv.Value.seen) gone.Add(kv.Key);
        foreach (int id in gone) { End(fingers[id]); fingers.Remove(id); }

        // Rebuild the outputs from the fingers that are down.
        held.Clear(); mouseL = mouseR = false; stickOn = false; lookOn = false;
        for (int i = 0; i < buttonDown.Length; i++) buttonDown[i] = false;
        foreach (var f in fingers.Values)
        {
            if (f.role == Role.Stick) stickOn = true;
            else if (f.role == Role.Look) { lookOn = true; if (layout.Look == MobileLayouts.Look.RightDrag) mouseR = true; }
            else if (f.role == Role.Button && f.index < layout.Buttons.Length) { buttonDown[f.index] = true; Press(layout.Buttons[f.index]); }
        }
        if (!stickOn) stick = Vector2.zero;
        // Released quick taps stay down at least one frame so wasPressedThisFrame always fires.
        for (int i = taps.Count - 1; i >= 0; i--)
        {
            held.Add(taps[i].key);
            if (taps[i].frames <= 0) taps.RemoveAt(i); else taps[i] = (taps[i].key, taps[i].frames - 1);
        }
        for (int i = 0; i < layout.Buttons.Length; i++)
            if (!buttonDown[i] && Time.unscaledTime - buttonDownAt[i] < .06f) { buttonDown[i] = true; Press(layout.Buttons[i]); }
        if (stickOn && layout.Stick)
        {
            const float on = .45f;
            if (stick.y > on) held.Add(Key.W); if (stick.y < -on) held.Add(Key.S);
            if (stick.x > on) held.Add(Key.D); if (stick.x < -on) held.Add(Key.A);
        }
    }

    void Press(MobileLayouts.Button b)
    {
        if (b.Key != Key.None) held.Add(b.Key);
        if (b.Key2 != Key.None) held.Add(b.Key2);
        if (b.Mouse == 1) mouseL = true; else if (b.Mouse == 2) mouseR = true;
    }

    Finger Begin(int id, Vector2 pos, bool pad)
    {
        var f = new Finger { start = pos, last = pos };
        if (pad)
        {
            int k = PadKeyAt(pos);
            if (k >= 0) { f.role = Role.Pad; f.index = k; PadPress(k); }
            return f;
        }
        for (int i = 0; i < layout.Buttons.Length; i++)
            if (Hit(ButtonScreenRect(i), pos)) { f.role = Role.Button; f.index = i; buttonDownAt[i] = Time.unscaledTime; return f; }
        if (OverUI(id)) return f;
        if (layout.Stick && pos.x < safe.xMin + Screen.width * .42f && pos.y < safe.yMin + Screen.height * .72f)
        { f.role = Role.Stick; stickBase = pos; stick = Vector2.zero; return f; }
        if (layout.Look != MobileLayouts.Look.None && pos.y > safe.yMin + 10 * scale) { f.role = Role.Look; return f; }
        return f;
    }

    void Move(Finger f, Vector2 pos)
    {
        if (f.role == Role.Stick)
        {
            float radius = 95f * scale;
            var d = pos - stickBase;
            if (d.magnitude > radius * 1.25f) stickBase = pos - d.normalized * radius * 1.25f;   // the base follows the thumb
            stick = Vector2.ClampMagnitude((pos - stickBase) / radius, 1f);
        }
        else if (f.role == Role.Look)
        {
            // Phone pixels → desktop-like mouse counts, so the game's mouse sensitivity still makes sense.
            float k = 1500f / Mathf.Max(800f, Screen.width) * layout.LookSpeed;
            lookDelta += (pos - f.last) * k;
        }
        f.last = pos;
    }

    void End(Finger f) { }

    bool OverUI(int touchId)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        try { return es.IsPointerOverGameObject(touchId); } catch { return false; }
    }

    static bool Hit(Rect r, Vector2 p)
    {
        var c = r.center; float rad = r.width * .5f * 1.12f;
        return (p - c).sqrMagnitude <= rad * rad;
    }

    // ------------------------------------------------------------------ virtual devices

    void Write()
    {
        if (vk == null) return;
        var ks = new KeyboardState();
        foreach (var k in held) ks.Set(k, true);
        InputSystem.QueueStateEvent(vk, ks);
        foreach (var c in text) InputSystem.QueueTextEvent(vk, c);
        text.Clear();

        var ms = new MouseState { position = new Vector2(2, 2), delta = lookOn ? lookDelta : Vector2.zero }
            .WithButton(MouseButton.Left, mouseL).WithButton(MouseButton.Right, mouseR);
        InputSystem.QueueStateEvent(vm, ms);

        var gs = new GamepadState { leftStick = layout.Stick ? stick : Vector2.zero };
        if (layout.Triggers) { gs.rightTrigger = Mathf.Max(0f, stick.y); gs.leftTrigger = Mathf.Max(0f, -stick.y); }
        InputSystem.QueueStateEvent(vg, gs);

        // The game reads ".current": make sure it is us (also after a real device was touched).
        if (Showing || held.Count > 0) { vk.MakeCurrent(); vm.MakeCurrent(); vg.MakeCurrent(); }
    }

    // ------------------------------------------------------------------ code pad

    static readonly string[] PadRows = { "1234567890", "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
    const int PadBack = 100, PadOk = 101, PadExit = 102;

    // Pad geometry (GUI space, top-left origin): 4 rows of keys + 1 row of BORRAR / OK / CERRAR, glued to the bottom.
    const float PadW = 84f, PadH = 64f, PadGap = 7f;
    float PadTop => Screen.height - safeBottom - (4 * (PadH + PadGap) + 8f + PadH + 14f) * scale;
    float safeBottom => Screen.safeArea.yMin;

    Rect PadKeyRect(int row, int col, int count)
    {
        float w = PadW * scale, h = PadH * scale, gap = PadGap * scale;
        float total = count * w + (count - 1) * gap;
        float x0 = Screen.width * .5f - total * .5f;
        return new Rect(x0 + col * (w + gap), PadTop + row * (h + gap), w, h);
    }

    Rect PadSpecialRect(int which)
    {
        float w = 190f * scale, h = PadH * scale, gap = 12f * scale;
        float y = PadTop + 4 * (PadH + PadGap) * scale + 8f * scale;
        float x0 = Screen.width * .5f - (3 * w + 2 * gap) * .5f;
        return new Rect(x0 + which * (w + gap), y, w, h);
    }

    /// <summary>Tests: screen (bottom-left) position of a pad key.</summary>
    public Vector2 PadKeyCenter(char c)
    {
        for (int r = 0; r < PadRows.Length; r++)
        {
            int col = PadRows[r].IndexOf(c);
            if (col < 0) continue;
            var g = PadKeyRect(r, col, PadRows[r].Length).center;
            return new Vector2(g.x, Screen.height - g.y);
        }
        return Vector2.zero;
    }

    int PadKeyAt(Vector2 screenPos)
    {
        var gui = new Vector2(screenPos.x, Screen.height - screenPos.y);
        for (int r = 0; r < PadRows.Length; r++)
            for (int c = 0; c < PadRows[r].Length; c++)
                if (PadKeyRect(r, c, PadRows[r].Length).Contains(gui)) return r * 16 + c;
        for (int s = 0; s < 3; s++) if (PadSpecialRect(s).Contains(gui)) return s == 0 ? PadBack : s == 1 ? PadOk : PadExit;
        return -1;
    }

    void PadPress(int code)
    {
        if (code == PadBack) { taps.Add((Key.Backspace, 1)); return; }
        if (code == PadOk) { taps.Add((Key.Enter, 1)); return; }
        if (code == PadExit) { taps.Add((Key.Escape, 1)); return; }
        char ch = PadRows[code / 16][code % 16];
        Key key = ch >= '0' && ch <= '9' ? (ch == '0' ? Key.Digit0 : Key.Digit1 + (ch - '1')) : Key.A + (ch - 'A');
        taps.Add((key, 1));
        text.Add(ch);
    }

    // ------------------------------------------------------------------ drawing

    Rect ButtonScreenRect(int i)
    {
        // GUI coordinates are top-left based; touches bottom-left. This returns SCREEN (bottom-left) space.
        var b = layout.Buttons[i];
        float r = b.Radius * scale;
        Vector2 anchor = b.Anchor == MobileLayouts.Anchor.Right ? new Vector2(safe.xMax, safe.yMin)
                       : b.Anchor == MobileLayouts.Anchor.Left ? new Vector2(safe.xMin, safe.yMin)
                       : b.Anchor == MobileLayouts.Anchor.Center ? new Vector2(Screen.width * .5f, safe.yMin)
                       : new Vector2(safe.xMin, safe.yMax);   // TopLeft
        Vector2 c = anchor + b.Position * scale;
        return new Rect(c.x - r, c.y - r, r * 2, r * 2);
    }

    void OnGUI()
    {
        if (!Showing) return;
        GUI.depth = -900;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true, richText = true, font = MultiverseUI.DisplayFont };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label);
        }
        if (PadOpen) { DrawPad(); return; }

        // Joystick.
        if (layout.Stick)
        {
            float radius = 95f * scale;
            Vector2 baseScreen = stickOn ? stickBase : new Vector2(safe.xMin + 190f * scale, safe.yMin + 170f * scale);
            var g = new Vector2(baseScreen.x, Screen.height - baseScreen.y);
            GUI.color = new Color(1, 1, 1, stickOn ? .28f : .16f);
            GUI.DrawTexture(new Rect(g.x - radius, g.y - radius, radius * 2, radius * 2), ring);
            var knob = g + new Vector2(stick.x, -stick.y) * radius;
            float kr = 46f * scale;
            GUI.color = new Color(1, 1, 1, stickOn ? .55f : .3f);
            GUI.DrawTexture(new Rect(knob.x - kr, knob.y - kr, kr * 2, kr * 2), circle);
        }
        // Buttons.
        for (int i = 0; i < layout.Buttons.Length; i++)
        {
            var b = layout.Buttons[i];
            var r = ButtonScreenRect(i);
            var gr = new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
            bool down = i < buttonDown.Length && buttonDown[i];
            var col = b.Color; col.a = down ? .85f : .5f;
            GUI.color = col; GUI.DrawTexture(gr, circle);
            GUI.color = new Color(1, 1, 1, down ? .9f : .55f); GUI.DrawTexture(gr, ring);
            GUI.color = Color.white;
            // Biggest size that fits on one line inside the circle (two lines only when the label has \n).
            int size = Mathf.RoundToInt(Mathf.Clamp(b.Radius * .34f, 13f, 30f) * scale);
            label.wordWrap = false;
            for (int guard = 0; guard < 20 && size > 8; guard++)
            {
                label.fontSize = size;
                var need = label.CalcSize(new GUIContent(b.Label));
                if (need.x <= gr.width * .84f && need.y <= gr.height * .8f) break;
                size = Mathf.RoundToInt(size * .9f);
            }
            GUI.Label(gr, b.Label, label);
        }
        GUI.color = Color.white;
    }

    void DrawPad()
    {
        GUI.color = new Color(0, 0, 0, .55f);
        GUI.DrawTexture(new Rect(0, PadTop - 12f * scale, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        label.wordWrap = false; label.fontSize = Mathf.RoundToInt(26 * scale);
        for (int r = 0; r < PadRows.Length; r++)
            for (int c = 0; c < PadRows[r].Length; c++)
                PadKey(PadKeyRect(r, c, PadRows[r].Length), PadRows[r][c].ToString(), r == 0 ? new Color(1f, .8f, .3f, .9f) : new Color(.55f, .7f, 1f, .85f));
        label.fontSize = Mathf.RoundToInt(21 * scale);
        PadKey(PadSpecialRect(0), "BORRAR", new Color(1f, .5f, .45f, .9f));
        PadKey(PadSpecialRect(1), "OK", new Color(.45f, .9f, .5f, .95f));
        PadKey(PadSpecialRect(2), "CERRAR", new Color(.75f, .75f, .8f, .85f));
    }

    void PadKey(Rect r, string text, Color c)
    {
        GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white; GUI.Label(r, "<color=#10131F>" + text + "</color>", label);
    }

    static Texture2D MakeCircle(int size, bool hollow)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        float c = (size - 1) * .5f, r = size * .5f - 1;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(r - d);
                if (hollow) a *= Mathf.Clamp01(d - (r - size * .06f));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }
}
