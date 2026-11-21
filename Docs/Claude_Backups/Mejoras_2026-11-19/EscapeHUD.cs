using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Runtime uGUI for the escape tower (MultiverseUI "Escape" skin): lobby, room card, timer, team, prompts, keypad, pause, results.</summary>
public sealed class EscapeHUD : MonoBehaviour
{
    public EscapeDirector Director;
    public bool KeypadOpen => keypad != null && keypad.activeSelf;
    public bool PauseOpen => pause != null && pause.activeSelf;
    public bool InputFree => !KeypadOpen && !PauseOpen;

    GameObject root, lobby, play, keypad, pause, results;
    Image crosshair;
    int keypadLength = 4;
    bool keypadLetters;
    Text speech;
    Text roomTitle, objective, hint, timer, promptText, notice, countdown, teamTitle, keypadTitle, keypadDigits, resultTitle, resultSub, resultBody, musicLabel;
    Image promptBar, promptPanel;
    readonly Image[] pips = new Image[16];
    readonly Text[] teamRows = new Text[6];
    readonly Image[] teamDots = new Image[6];
    readonly Button[] countButtons = new Button[3];
    RectTransform teamPanel, noticeRect;
    Func<string, bool> keypadSubmit;
    string typed = "";
    int selectedPlayers;
    float savedTimeScale = 1f;
    Font display, body;
    MultiverseUI.Theme theme;

    void Start()
    {
        display = MultiverseUI.DisplayFont; body = MultiverseUI.BodyFont;
        theme = MultiverseUI.For(MultiverseUI.Mode.Escape);
        selectedPlayers = Director != null ? Director.PlayerCount : 4;
        Build();
        MultiverseUI.Skin(root, MultiverseUI.Mode.Escape);
        RefreshCountButtons();
        if (Director != null && Director.AutoHuman) lobby.SetActive(false);
        MultiverseParty.StripMenus();
        // Party match: no start screen, the team is whoever is in the room (4–6).
        if (Director != null && MultiverseParty.AutoStart) { lobby.SetActive(false); Director.StartGame(MultiverseParty.CurrentRoster().Count); }
    }

    // ------------------------------------------------------------------ building helpers

    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    Text Label(string value, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int point, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool title = false)
    {
        var t = Rect("Texto", parent, anchor, pos, size).gameObject.AddComponent<Text>();
        t.font = title ? display : body; t.fontSize = point; t.color = color; t.text = value; t.alignment = align;
        t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float alpha = .92f)
    {
        var img = Rect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
        img.color = new Color(theme.panel.r, theme.panel.g, theme.panel.b, alpha);
        return img;
    }

    Button MakeButton(string label, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Action action, int point = 22)
    {
        var go = Rect("Botón " + label, parent, anchor, pos, size).gameObject;
        go.AddComponent<Image>().color = theme.button;
        var b = go.AddComponent<Button>();
        b.onClick.AddListener(() => action());
        Label(label, go.transform, new Vector2(.5f, .5f), Vector2.zero, size, point, theme.buttonText, TextAnchor.MiddleCenter, true);
        return b;
    }

    GameObject Fullscreen(string name, Transform parent, float alpha)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        r.gameObject.AddComponent<Image>().color = new Color(theme.ink.r, theme.ink.g, theme.ink.b, alpha);
        return r.gameObject;
    }

    void Build()
    {
        root = new GameObject("Interfaz de la torre", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Color text = theme.text, accent = theme.panelEdge, gold = theme.button;

        // ---- in-game HUD
        play = new GameObject("HUD", typeof(RectTransform));
        play.transform.SetParent(root.transform, false);
        var pr = play.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;

        var card = Panel("Tarjeta de la sala", play.transform, new Vector2(0, 1), new Vector2(262, -120), new Vector2(500, 220));
        roomTitle = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, 86), new Vector2(470, 34), 22, accent, TextAnchor.MiddleLeft, true);
        objective = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, 32), new Vector2(470, 74), 15, text, TextAnchor.MiddleLeft);
        objective.supportRichText = true;
        hint = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, -62), new Vector2(470, 82), 13, new Color(1f, .9f, .62f, .92f), TextAnchor.UpperLeft);
        // Co-op: what teammates say (requests, "¡te toca!", clues read aloud).
        speech = Label("", play.transform, new Vector2(0, 0), new Vector2(330, 190), new Vector2(620, 150), 16, Color.white, TextAnchor.LowerLeft);
        speech.supportRichText = true;
        speech.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .75f);

        crosshair = Rect("Mira", play.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(10, 10)).gameObject.AddComponent<Image>();
        crosshair.sprite = MultiverseUI.Pill; crosshair.raycastTarget = false; crosshair.color = new Color(1, 1, 1, .75f);

        var timerPanel = Panel("Reloj", play.transform, new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(230, 66));
        timer = Label("15:00", timerPanel.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(220, 60), 36, gold, TextAnchor.MiddleCenter, true);
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i] = Rect("Progreso " + (i + 1), play.transform, new Vector2(.5f, 1), new Vector2(-117 + i * 26, -95), new Vector2(20, 10)).gameObject.AddComponent<Image>();
        }

        teamPanel = Panel("Equipo", play.transform, new Vector2(1, 1), new Vector2(-142, -120), new Vector2(256, 220)).rectTransform;
        teamTitle = Label("CARRERA", teamPanel, new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(230, 26), 16, accent, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < 6; i++)
        {
            teamDots[i] = Rect("Color", teamPanel, new Vector2(0, 1), new Vector2(24, -50 - i * 28), new Vector2(14, 14)).gameObject.AddComponent<Image>();
            teamDots[i].sprite = MultiverseUI.Pill;
            teamRows[i] = Label("", teamPanel, new Vector2(0, 1), new Vector2(140, -50 - i * 28), new Vector2(210, 26), 15, text, TextAnchor.MiddleLeft);
        }

        promptPanel = Panel("Acción", play.transform, new Vector2(.5f, 0), new Vector2(0, 78), new Vector2(460, 62));
        promptText = Label("", promptPanel.transform, new Vector2(.5f, .5f), new Vector2(0, 6), new Vector2(440, 34), 19, text, TextAnchor.MiddleCenter, true);
        promptText.resizeTextForBestFit = true; promptText.resizeTextMinSize = 11; promptText.resizeTextMaxSize = 19;
        var barBack = Rect("Barra", promptPanel.transform, new Vector2(.5f, .5f), new Vector2(0, -19), new Vector2(400, 8)).gameObject.AddComponent<Image>();
        barBack.color = new Color(1, 1, 1, .15f);
        promptBar = Rect("Relleno", barBack.transform, new Vector2(0, .5f), new Vector2(0, 0), new Vector2(0, 8)).gameObject.AddComponent<Image>();
        promptBar.rectTransform.pivot = new Vector2(0, .5f);
        promptBar.color = accent;

        notice = Label("", play.transform, new Vector2(.5f, .5f), new Vector2(0, 150), new Vector2(1000, 70), 32, Color.white, TextAnchor.MiddleCenter, true);
        noticeRect = notice.rectTransform;
        var outline = notice.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .7f); outline.effectDistance = new Vector2(2, -2);
        countdown = Label("", play.transform, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(400, 200), 130, gold, TextAnchor.MiddleCenter, true);
        countdown.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .7f);
        Label("WASD mover · Ratón mirar · Espacio saltar · E usar lo que apuntas (mantén) · Q soltar · H pedir pista (10 s quieto) · M música · Esc pausa",
            play.transform, new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(1100, 24), 13, new Color(1, 1, 1, .7f));

        // ---- lobby
        lobby = Fullscreen("Sala de espera", root.transform, .45f);
        var lc = Panel("Tarjeta de inicio", lobby.transform, new Vector2(.5f, .5f), new Vector2(0, 0), new Vector2(680, 520), .95f);
        Label("TORRE DEL MULTIVERSO", lc.transform, new Vector2(.5f, .5f), new Vector2(0, 212), new Vector2(640, 56), 40, gold, TextAnchor.MiddleCenter, true);
        Label("Carrera de escape en primera persona · 11 salas · cada uno en su torre", lc.transform, new Vector2(.5f, .5f), new Vector2(0, 168), new Vector2(640, 30), 18, accent);
        Label("CARRERA: cada jugador escapa SOLO de su propia copia de la torre. Las mismas salas para todos, pero cada uno con sus propias soluciones. " +
              "Gana quien llegue primero al final. ¿Atascado 1:30 en una sala? Llega un ayudante y la resuelve por ti.",
            lc.transform, new Vector2(.5f, .5f), new Vector2(0, 104), new Vector2(600, 84), 16, text);
        Label("JUGADORES", lc.transform, new Vector2(.5f, .5f), new Vector2(0, 38), new Vector2(300, 28), 18, accent, TextAnchor.MiddleCenter, true);
        for (int i = 0; i < 3; i++)
        {
            int n = 4 + i;
            countButtons[i] = MakeButton(n.ToString(), lc.transform, new Vector2(.5f, .5f), new Vector2(-140 + i * 140, -14), new Vector2(118, 56), () => { selectedPlayers = n; RefreshCountButtons(); MultiverseUI.PlayClick(false); }, 26);
        }
        Label("Tú + rivales de la IA (solo marcan tiempos, sin cuerpo)", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -56), new Vector2(500, 24), 14, new Color(text.r, text.g, text.b, .7f));
        MakeButton("¡EMPEZAR!", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -112), new Vector2(280, 62), StartPressed, 26);
        MakeButton("Volver al Hub", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -180), new Vector2(220, 44), () => Director.ReturnToHub(), 18);
        Label("WASD · Espacio · E usar · Q soltar · Esc pausa", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -230), new Vector2(600, 24), 13, new Color(text.r, text.g, text.b, .6f));

        // ---- keypad
        keypad = Fullscreen("Teclado", root.transform, .35f);
        var kc = Panel("Tarjeta del teclado", keypad.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(420, 300), .96f);
        keypadTitle = Label("CÓDIGO", kc.transform, new Vector2(.5f, .5f), new Vector2(0, 112), new Vector2(380, 32), 20, accent, TextAnchor.MiddleCenter, true);
        keypadDigits = Label("_ _ _ _", kc.transform, new Vector2(.5f, .5f), new Vector2(0, 36), new Vector2(380, 90), 64, gold, TextAnchor.MiddleCenter, true);
        Label("Escribe con el teclado · Enter confirmar · Retroceso borrar · Esc salir", kc.transform, new Vector2(.5f, .5f), new Vector2(0, -40), new Vector2(380, 44), 14, text);
        MakeButton("Confirmar", kc.transform, new Vector2(.5f, .5f), new Vector2(-90, -104), new Vector2(160, 46), ConfirmKeypad, 18);
        MakeButton("Salir", kc.transform, new Vector2(.5f, .5f), new Vector2(90, -104), new Vector2(160, 46), CloseKeypad, 18);
        keypad.SetActive(false);

        // ---- pause
        pause = Fullscreen("Pausa", root.transform, .5f);
        var pc = Panel("Tarjeta de pausa", pause.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(380, 360), .96f);
        Label("PAUSA", pc.transform, new Vector2(.5f, .5f), new Vector2(0, 130), new Vector2(340, 50), 36, gold, TextAnchor.MiddleCenter, true);
        MakeButton("Continuar", pc.transform, new Vector2(.5f, .5f), new Vector2(0, 58), new Vector2(240, 50), TogglePause, 20);
        var mb = MakeButton("Música: sí", pc.transform, new Vector2(.5f, .5f), new Vector2(0, -2), new Vector2(240, 46), () => { EscapeAudio.ToggleMusic(); RefreshMusic(); }, 18);
        musicLabel = mb.GetComponentInChildren<Text>();
        MakeButton("Reiniciar la torre", pc.transform, new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(240, 46), () => { Time.timeScale = 1f; Director.Restart(); }, 18);
        MakeButton("Volver al Hub", pc.transform, new Vector2(.5f, .5f), new Vector2(0, -118), new Vector2(240, 46), () => { Time.timeScale = 1f; Director.ReturnToHub(); }, 18);
        pause.SetActive(false);

        // ---- results
        results = Fullscreen("Resultados", root.transform, .55f);
        var rc = Panel("Tarjeta de resultados", results.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(700, 560), .96f);
        resultTitle = Label("", rc.transform, new Vector2(.5f, .5f), new Vector2(0, 228), new Vector2(660, 60), 42, gold, TextAnchor.MiddleCenter, true);
        resultSub = Label("", rc.transform, new Vector2(.5f, .5f), new Vector2(0, 180), new Vector2(660, 30), 18, accent);
        resultBody = Label("", rc.transform, new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(640, 340), 14, text, TextAnchor.UpperLeft);
        MakeButton("Jugar otra vez", rc.transform, new Vector2(.5f, .5f), new Vector2(-130, -235), new Vector2(230, 54), () => Director.Restart(), 20);
        MakeButton("Volver al Hub", rc.transform, new Vector2(.5f, .5f), new Vector2(130, -235), new Vector2(230, 54), () => Director.ReturnToHub(), 20);
        results.SetActive(false);
    }

    void RefreshCountButtons()
    {
        for (int i = 0; i < 3; i++)
        {
            int n = 4 + i;
            var t = countButtons[i].GetComponentInChildren<Text>();
            t.text = n == selectedPlayers ? "► " + n + " ◄" : n.ToString();
            countButtons[i].GetComponent<Image>().color = n == selectedPlayers ? theme.panelEdge : theme.button;
        }
    }

    void RefreshMusic() { if (musicLabel != null) musicLabel.text = EscapeAudio.MusicOn ? "Música: sí" : "Música: no"; }

    void StartPressed()
    {
        lobby.SetActive(false);
        Director.StartGame(selectedPlayers);
    }

    // ------------------------------------------------------------------ public API

    public void OpenKeypad(string title, Func<string, bool> submit, int length = 4, bool letters = false)
    {
        keypadLength = Mathf.Clamp(length, 1, 8); keypadLetters = letters;
        keypadTitle.text = title;
        keypadSubmit = submit;
        typed = "";
        keypad.SetActive(true);
        RefreshKeypad();
    }

    public void CloseKeypad()
    {
        keypad.SetActive(false);
        keypadSubmit = null;
    }

    void ConfirmKeypad()
    {
        if (typed.Length < keypadLength || keypadSubmit == null) return;
        bool ok = keypadSubmit(typed);
        typed = "";
        if (ok) CloseKeypad(); else RefreshKeypad();
    }

    void RefreshKeypad()
    {
        var s = "";
        for (int i = 0; i < keypadLength; i++) s += (i < typed.Length ? typed[i].ToString() : "_") + (i < keypadLength - 1 ? " " : "");
        keypadDigits.text = s;
        keypadDigits.fontSize = keypadLength > 5 ? 46 : 64;
    }

    void TogglePause()
    {
        if (!PauseOpen)
        {
            if (!Director.Running) return;
            savedTimeScale = Time.timeScale;
            if (!PartyLink.Active) Time.timeScale = 0f;   // online the tower keeps going for the others
            pause.SetActive(true);
            RefreshMusic();
        }
        else
        {
            pause.SetActive(false);
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
        }
    }

    static string RaceRow(EscapeDirector d, EscapeDirector.Racer r, int pos)
    {
        string name = r.Local ? "<b>TÚ</b>" : r.Name + (r.Ghost ? " (IA)" : "");
        string state = r.Finished ? "✔ " + EscapeDirector.Clock(r.Time) : r.Gone ? "se fue" : "sala " + Mathf.Min(r.Rooms + 1, d.Rooms.Length) + "/" + d.Rooms.Length;
        return $"{pos}. {name}  <color=#FFFFFFAA>{state}</color>";
    }

    float nextResults;

    public void ShowResults()
    {
        if (KeypadOpen) CloseKeypad();
        RefreshResults();
        results.SetActive(true);
    }

    void RefreshResults()
    {
        var d = Director;
        nextResults = Time.unscaledTime + .5f;
        bool won = d.State == EscapeDirector.Phase.Won;
        int place = d.Place(d.Me);
        resultTitle.text = won ? (place == 1 ? "¡PRIMER LUGAR!" : $"¡ESCAPASTE! · {place}.º LUGAR") : "SE ACABÓ LA CARRERA";
        resultSub.text = won ? $"Tu tiempo {EscapeDirector.Clock(d.Elapsed)}" + (d.NewRecord ? "  ·  ¡NUEVO RÉCORD!" : $"  ·  Récord {EscapeDirector.Clock(d.BestTime)}")
                             : $"Salas superadas: {d.RoomsSolved}/{d.Rooms.Length}";
        var lines = new System.Text.StringBuilder();
        lines.AppendLine("<b>CLASIFICACIÓN</b>");
        var ranking = d.Ranking;
        for (int i = 0; i < ranking.Count; i++) lines.AppendLine(RaceRow(d, ranking[i], i + 1));
        if (!d.RaceOver) lines.AppendLine("<color=#FFD27A>Los demás siguen jugando…</color>");
        lines.AppendLine();
        lines.AppendLine("<b>TUS SALAS</b>");
        float previous = 0f;
        for (int i = 0; i < d.Rooms.Length; i++)
        {
            string time = "—";
            if (d.Rooms[i].Solved) { float split = d.SolvedAt[i] - previous; previous = d.SolvedAt[i]; time = EscapeDirector.Clock(split); }
            string cell = $"{i + 1}. {d.Rooms[i].Title} {time}";
            if (i % 2 == 0) lines.Append(cell.PadRight(40)); else lines.AppendLine("   " + cell);
        }
        resultBody.supportRichText = true;
        resultBody.text = lines.ToString();
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        var d = Director;
        if (d == null || root == null) return;
        var k = Keyboard.current;

        if (lobby.activeSelf && k != null)
        {
            if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) { selectedPlayers = 4; RefreshCountButtons(); }
            if (k.digit5Key.wasPressedThisFrame || k.numpad5Key.wasPressedThisFrame) { selectedPlayers = 5; RefreshCountButtons(); }
            if (k.digit6Key.wasPressedThisFrame || k.numpad6Key.wasPressedThisFrame) { selectedPlayers = 6; RefreshCountButtons(); }
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) StartPressed();
        }

        if (KeypadOpen && k != null)
        {
            if (!keypadLetters)
                for (int i = 0; i < 10; i++)
                {
                    var digit = i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1);
                    if ((k[digit].wasPressedThisFrame || k[Key.Numpad0 + i].wasPressedThisFrame) && typed.Length < keypadLength)
                    {
                        typed += i.ToString(); RefreshKeypad();
                        EscapeAudio.PlayUI("beep", .5f);
                    }
                }
            else
                for (int i = 0; i < 26; i++)
                    if (k[Key.A + i].wasPressedThisFrame && typed.Length < keypadLength)
                    {
                        typed += (char)('A' + i); RefreshKeypad();
                        EscapeAudio.PlayUI("beep", .5f);
                    }
            if (k.backspaceKey.wasPressedThisFrame && typed.Length > 0) { typed = typed.Substring(0, typed.Length - 1); RefreshKeypad(); }
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) ConfirmKeypad();
            if (k.escapeKey.wasPressedThisFrame) CloseKeypad();
            if (!d.Running) CloseKeypad();
        }
        else if (k != null && k.escapeKey.wasPressedThisFrame && !lobby.activeSelf && !results.activeSelf) TogglePause();
        else if (k != null && k.hKey.wasPressedThisFrame && d.InputFree) d.RequestClue();

        if (results.activeSelf && Time.unscaledTime >= nextResults) RefreshResults();
        bool inGame = d.State != EscapeDirector.Phase.Lobby;
        play.SetActive(inGame && !results.activeSelf);
        if (!inGame) return;

        // Timer and room progress.
        float el = d.Elapsed;
        timer.text = $"{Mathf.FloorToInt(el / 60f):00}:{Mathf.FloorToInt(el % 60f):00}";
        timer.color = theme.button;
        for (int i = 0; i < pips.Length; i++)
        {
            pips[i].gameObject.SetActive(i < d.Rooms.Length);
            if (i >= d.Rooms.Length) continue;
            pips[i].rectTransform.anchoredPosition = new Vector2((i - (d.Rooms.Length - 1) * .5f) * 26, -95);
            bool done = d.Rooms[i].Solved, current = i == d.ActiveRoomIndex;
            pips[i].color = done ? theme.panelEdge : current ? theme.button : new Color(1, 1, 1, .2f);
            pips[i].rectTransform.sizeDelta = new Vector2(current ? 24 : 20, current ? 12 : 10);
        }

        var room = d.ActiveRoom;
        if (room != null)
        {
            roomTitle.text = $"SALA {room.Index + 1}/{d.Rooms.Length} · {room.Title.ToUpperInvariant()}";
            objective.text = d.ObjectiveFor(room);
            var clues = room.Clues;
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < room.CluesShown; c++) sb.Append("PISTA ").Append(c + 1).Append(": ").Append(clues[c]).Append('\n');
            float next = room.NextClueIn;
            if (next >= 0f)
                sb.Append("<color=#FFFFFF99>Pista ").Append(room.CluesShown + 1).Append(" en ").Append(Mathf.FloorToInt(next / 60f)).Append(':').Append(Mathf.FloorToInt(next % 60f).ToString("00"))
                  .Append(room.CanRequestClue ? "  ·  H: pedirla ya (10 s quieto)" : "").Append("</color>");
            float helper = d.HelperIn;
            if (helper >= 0f) sb.Append("\n<color=#FFFFFF99>Ayudante en ").Append(EscapeDirector.Clock(helper)).Append("</color>");
            else if (d.HelperBot != null) sb.Append("\n<color=#FFD27A>¡El AYUDANTE está resolviendo la sala!</color>");
            hint.supportRichText = true;
            hint.text = sb.ToString();
        }

        // Race list (2026-11-17): everybody's progress, best first.
        var ranking = d.Ranking;
        int n = Mathf.Min(6, ranking.Count);
        teamPanel.sizeDelta = new Vector2(256, 60 + n * 28);
        teamPanel.anchoredPosition = new Vector2(-142, -(60 + n * 28) * .5f - 16);
        for (int i = 0; i < 6; i++)
        {
            bool on = i < n;
            teamRows[i].gameObject.SetActive(on); teamDots[i].gameObject.SetActive(on);
            if (!on) continue;
            teamDots[i].color = ranking[i].Color;
            teamRows[i].supportRichText = true;
            teamRows[i].text = RaceRow(d, ranking[i], i + 1);
        }

        // Interaction prompt.
        var human = d.Human;
        var focus = human != null && d.InputFree ? human.Focus : null;
        promptPanel.gameObject.SetActive(focus != null);
        crosshair.gameObject.SetActive(d.View != null && d.View.FirstPerson && d.InputFree);
        crosshair.color = focus != null ? theme.button : new Color(1, 1, 1, .7f);
        crosshair.rectTransform.sizeDelta = Vector2.one * (focus != null ? 16f : 9f);
        if (focus != null)
        {
            string p = focus.PromptFor(human);
            if (!focus.Mine(human))
            {
                var owner = d.ActorOfSeat(focus.Owner);
                string who = owner != null ? owner.DisplayName.ToUpperInvariant() : "OTRO";
                bool bot = d.IsBotActor(owner);
                promptText.text = focus.NetLocal && bot ? $"E  ·  Dictar a {who}: {p}" : bot ? $"E  ·  Pedir a {who}: {p}" : $"Puesto de {who}  ·  ¡pídeselo!";
            }
            else promptText.text = "E  ·  " + (focus.Owner >= 0 ? "TU PUESTO · " : "") + p + (focus.hold > 0f ? "  (mantén)" : "");
            promptBar.rectTransform.sizeDelta = new Vector2(400f * Mathf.Clamp01(human.UseProgress), 8);
        }

        speech.text = d.Speech;

        // Notice pop and countdown.
        notice.text = d.Notice;
        float age = Time.time - d.NoticeAt;
        float pop = d.Notice.Length > 0 ? 1f + Mathf.Max(0f, .25f - age) * 1.6f : 1f;
        noticeRect.localScale = Vector3.one * pop;
        if (d.State == EscapeDirector.Phase.Countdown)
        {
            int c = Mathf.CeilToInt(d.Countdown);
            countdown.text = c > 3 ? "" : c.ToString();
            countdown.rectTransform.localScale = Vector3.one * (1f + Mathf.Repeat(d.Countdown, 1f) * .4f);
        }
        else countdown.text = "";
    }
}
