using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// "Cómo se juega" card shown when a minigame of the party loads (2026-11-19): title, what to do, controls and a
/// tip, until the player presses Space / Enter / click (or 25 s pass). Offline the game waits frozen behind it;
/// online this computer only reports "ready" to the host after closing it, so the minigame starts when everybody
/// has read it (the host still starts after its usual 25 s if someone is away).
/// In the editor it closes by itself after 3 s so the automatic tests keep working.
/// </summary>
public sealed class PartyGameIntro : MonoBehaviour
{
    public const float MaxSeconds = 25f;
    public static bool Holding => current != null && !current.closed;
    public static bool PendingReady;
    static PartyGameIntro current;

    int game; float opened; bool closed; string tip; GUIStyle title, body, small, keys;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded += OnScene; }

    static void OnScene(Scene s, LoadSceneMode mode)
    {
        int g = PartyGames.FromScene(s.name);
        if (g < 0 || !MultiverseParty.InMatch) return;
        PendingReady = false;
        current = new GameObject("Cómo se juega").AddComponent<PartyGameIntro>();
        current.game = g; current.opened = Time.unscaledTime; current.tip = PartyGames.Tip(g);
    }

    void Update()
    {
        if (closed) return;
        if (!PartyLink.Active) Time.timeScale = 0f;                      // offline: everything waits
        var k = Keyboard.current; var m = Mouse.current; var gp = Gamepad.current;
        bool go = (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
               || (m != null && m.leftButton.wasPressedThisFrame) || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) || (gp != null && (gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame));
        float age = Time.unscaledTime - opened;
        if (age < .4f) go = false;                                         // do not skip it with a key still held from the plaza
        if (go || age > MaxSeconds || (Application.isEditor && age > 3f) || System.Environment.CommandLine.Contains("-party-")) Close();
    }

    void Close()
    {
        if (closed) return;
        closed = true;
        if (!PartyLink.Active) Time.timeScale = 1f;
        if (PendingReady) { PendingReady = false; PartyLink.MarkReady(); }
        Destroy(gameObject, .05f);
    }

    void OnDestroy() { if (current == this) current = null; if (!closed && !PartyLink.Active) Time.timeScale = 1f; }

    void OnGUI()
    {
        if (closed) return;
        GUI.depth = -1000;
        if (title == null)
        {
            GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
            title = S(MultiverseUI.DisplayFont, 48, TextAnchor.MiddleCenter); body = S(MultiverseUI.BodyFont, 21, TextAnchor.UpperCenter);
            small = S(MultiverseUI.BodyFont, 17, TextAnchor.UpperCenter); keys = S(MultiverseUI.DisplayFont, 24, TextAnchor.MiddleCenter);
        }
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        float W = Screen.width / scale, H = Screen.height / scale;
        var info = PartyGames.All[game];
        GUI.color = new Color(0, 0, 0, .6f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); GUI.color = Color.white;
        var box = new Rect(W * .5f - 440, H * .5f - 270, 880, 540);
        MultiverseUI.DrawPanel(box, new Color(.07f, .1f, .2f, .97f), new Color(info.Color.r, info.Color.g, info.Color.b, .95f));
        title.normal.textColor = info.Color; GUI.Label(new Rect(box.x, box.y + 18, box.width, 60), info.Title, title);
        GUI.Label(new Rect(box.x, box.y + 76, box.width, 28), "<color=#C9D3F0>" + info.Tagline + "  ·  " + info.Mode + "</color>", small);
        GUI.Label(new Rect(box.x + 50, box.y + 112, box.width - 100, 120), info.Description.Replace("\n", "\n\n"), body);
        MultiverseUI.DrawPanel(new Rect(box.x + 40, box.y + 252, box.width - 80, 128), new Color(1, 1, 1, .06f), new Color(1, 1, 1, .15f));
        GUI.Label(new Rect(box.x + 40, box.y + 262, box.width - 80, 26), "<color=#FFD54A>CONTROLES</color>", small);
        GUI.Label(new Rect(box.x + 60, box.y + 292, box.width - 120, 84), (game < PartyGames.Controls.Length ? PartyGames.Controls[game] : "").Replace(" · ", "   ·   "), small);
        if (!string.IsNullOrEmpty(tip)) GUI.Label(new Rect(box.x + 60, box.y + 392, box.width - 120, 50), "<color=#9BE59B>Consejo:</color> " + tip, small);
        float left = MaxSeconds - (Time.unscaledTime - opened);
        float pulse = .75f + .25f * Mathf.Sin(Time.unscaledTime * 5f);
        keys.normal.textColor = new Color(1f, .85f, .4f, pulse);
        GUI.Label(new Rect(box.x, box.yMax - 82, box.width, 40), (MobileControls.Enabled ? "TOCA LA PANTALLA: ¡listo!" : "ESPACIO, ENTER o CLIC: ¡listo!"), keys);
        string wait = PartyLink.Active ? "Empieza cuando todos estén listos" : "El juego espera a que pulses";
        GUI.Label(new Rect(box.x, box.yMax - 42, box.width, 28), "<color=#AAB4D0>" + wait + " · se cierra solo en " + Mathf.CeilToInt(left) + " s</color>", small);
    }
}
