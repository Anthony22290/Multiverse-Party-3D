using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Runtime uGUI for Invasión Zombi (MultiverseUI "Zombie" skin): lobby, wave banner, boss bar, objective,
/// team health, truck repair list, health bar, prompts, pause and results.</summary>
public sealed class SurvivalHUD : MonoBehaviour
{
    public SurvivalDirector Director;
    public bool PauseOpen => pause != null && pause.activeSelf;
    public bool InputFree => !PauseOpen;

    GameObject root, lobby, play, pause, results, bossPanel, escapePanel;
    Text waveTitle, waveSub, objTitle, objective, hint, promptText, notice, countdown, bossName, resultTitle, resultSub, resultBody, musicLabel, hpText, repairTitle, escapeText;
    Image promptBar, promptPanel, bossFill, hpFill, hurtFlash, escapeFill;
    readonly Text[] teamRows = new Text[SurvivalDirector.MaxPlayers];
    readonly Image[] teamDots = new Image[SurvivalDirector.MaxPlayers], teamBars = new Image[SurvivalDirector.MaxPlayers];
    readonly Text[] partRows = new Text[SurvivalDirector.TotalWaves];
    readonly Button[] countButtons = new Button[3];
    RectTransform teamPanel, noticeRect;
    int selectedPlayers;
    float savedTimeScale = 1f, hurt;
    Font display, body;
    MultiverseUI.Theme theme;
    static readonly Color Red = new Color(1f, .3f, .28f), Green = new Color(.45f, 1f, .5f);

    void Start()
    {
        display = MultiverseUI.DisplayFont; body = MultiverseUI.BodyFont;
        theme = MultiverseUI.For(MultiverseUI.Mode.Zombie);
        selectedPlayers = Director != null ? Director.PlayerCount : 4;
        Build();
        MultiverseUI.Skin(root, MultiverseUI.Mode.Zombie);
        // Bars keep their own colours after skinning.
        hpFill.color = Green; bossFill.color = Red; escapeFill.color = Green; promptBar.color = theme.panelEdge;
        hurtFlash.color = new Color(1f, 0f, 0f, 0f);
        RefreshCountButtons();
        if (Director != null && Director.AutoHuman) lobby.SetActive(false);
        MultiverseParty.StripMenus();
        // Party match: no lobby, the team is whoever is in the room (4–6).
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

    Image Bar(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, out Image fill)
    {
        var back = Rect(name, parent, anchor, pos, size).gameObject.AddComponent<Image>();
        back.color = new Color(0, 0, 0, .45f);
        back.sprite = MultiverseUI.Pill; back.type = Image.Type.Sliced;
        fill = Rect("Relleno", back.transform, new Vector2(0, .5f), Vector2.zero, size).gameObject.AddComponent<Image>();
        fill.rectTransform.pivot = new Vector2(0, .5f);
        fill.sprite = MultiverseUI.Pill; fill.type = Image.Type.Sliced;
        return back;
    }

    static void SetFill(Image fill, float width, float t) => fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, width * Mathf.Clamp01(t)), fill.rectTransform.sizeDelta.y);

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
        root = new GameObject("Interfaz zombi", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f; scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Color text = theme.text, accent = theme.panelEdge, gold = theme.button;

        // ---- hurt flash (under everything)
        var hf = new GameObject("Daño", typeof(RectTransform)).GetComponent<RectTransform>();
        hf.SetParent(root.transform, false); hf.anchorMin = Vector2.zero; hf.anchorMax = Vector2.one; hf.offsetMin = hf.offsetMax = Vector2.zero;
        hurtFlash = hf.gameObject.AddComponent<Image>(); hurtFlash.raycastTarget = false; hurtFlash.sprite = Vignette();

        // ---- in-game HUD
        play = new GameObject("HUD", typeof(RectTransform));
        play.transform.SetParent(root.transform, false);
        var pr = play.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;

        var wave = Panel("Oleada", play.transform, new Vector2(.5f, 1), new Vector2(0, -50), new Vector2(330, 78));
        waveTitle = Label("OLEADA 1/10", wave.transform, new Vector2(.5f, .5f), new Vector2(0, 12), new Vector2(320, 44), 32, gold, TextAnchor.MiddleCenter, true);
        waveSub = Label("", wave.transform, new Vector2(.5f, .5f), new Vector2(0, -22), new Vector2(320, 24), 15, text);

        bossPanel = Rect("Jefe", play.transform, new Vector2(.5f, 1), new Vector2(0, -118), new Vector2(520, 44)).gameObject;
        bossName = Label("", bossPanel.transform, new Vector2(.5f, .5f), new Vector2(0, 12), new Vector2(520, 24), 18, Red, TextAnchor.MiddleCenter, true);
        bossName.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .7f);
        Bar("Vida del jefe", bossPanel.transform, new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(500, 16), out bossFill);

        var card = Panel("Objetivo", play.transform, new Vector2(0, 1), new Vector2(232, -70), new Vector2(440, 118));
        objTitle = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, 34), new Vector2(410, 30), 20, accent, TextAnchor.MiddleLeft, true);
        objective = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, -4), new Vector2(410, 46), 16, text, TextAnchor.MiddleLeft);
        hint = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, -40), new Vector2(410, 26), 13, new Color(text.r, text.g, text.b, .75f), TextAnchor.MiddleLeft);

        var repair = Panel("Camioneta", play.transform, new Vector2(0, .5f), new Vector2(122, -20), new Vector2(220, 300));
        repairTitle = Label("CAMIONETA 0/10", repair.transform, new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(200, 28), 17, accent, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < partRows.Length; i++)
            partRows[i] = Label("", repair.transform, new Vector2(.5f, 1), new Vector2(0, -52 - i * 24), new Vector2(200, 24), 14, text, TextAnchor.MiddleLeft);

        teamPanel = Panel("Equipo", play.transform, new Vector2(1, 1), new Vector2(-142, -100), new Vector2(256, 170)).rectTransform;
        Label("EQUIPO", teamPanel, new Vector2(.5f, 1), new Vector2(0, -20), new Vector2(230, 26), 16, accent, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < SurvivalDirector.MaxPlayers; i++)
        {
            teamDots[i] = Rect("Color", teamPanel, new Vector2(0, 1), new Vector2(22, -52 - i * 34), new Vector2(14, 14)).gameObject.AddComponent<Image>();
            teamDots[i].sprite = MultiverseUI.Pill;
            teamRows[i] = Label("", teamPanel, new Vector2(0, 1), new Vector2(138, -47 - i * 34), new Vector2(210, 22), 14, text, TextAnchor.MiddleLeft);
            Bar("Vida", teamPanel, new Vector2(0, 1), new Vector2(138, -63 - i * 34), new Vector2(206, 7), out teamBars[i]);
        }

        var hp = Panel("Tu vida", play.transform, new Vector2(.5f, 0), new Vector2(0, 148), new Vector2(360, 40));
        hpText = Label("VIDA 100", hp.transform, new Vector2(0, .5f), new Vector2(52, 0), new Vector2(90, 30), 15, text, TextAnchor.MiddleCenter, true);
        Bar("Barra de vida", hp.transform, new Vector2(.5f, .5f), new Vector2(44, 0), new Vector2(240, 16), out hpFill);

        promptPanel = Panel("Acción", play.transform, new Vector2(.5f, 0), new Vector2(0, 82), new Vector2(460, 62));
        promptText = Label("", promptPanel.transform, new Vector2(.5f, .5f), new Vector2(0, 6), new Vector2(440, 34), 19, text, TextAnchor.MiddleCenter, true);
        var barBack = Rect("Barra", promptPanel.transform, new Vector2(.5f, .5f), new Vector2(0, -19), new Vector2(400, 8)).gameObject.AddComponent<Image>();
        barBack.color = new Color(1, 1, 1, .15f);
        promptBar = Rect("Relleno", barBack.transform, new Vector2(0, .5f), Vector2.zero, new Vector2(0, 8)).gameObject.AddComponent<Image>();
        promptBar.rectTransform.pivot = new Vector2(0, .5f);

        escapePanel = Panel("Escape", play.transform, new Vector2(.5f, .5f), new Vector2(0, -150), new Vector2(520, 70)).gameObject;
        escapeText = Label("", escapePanel.transform, new Vector2(.5f, .5f), new Vector2(0, 14), new Vector2(500, 30), 20, Green, TextAnchor.MiddleCenter, true);
        Bar("Arranque", escapePanel.transform, new Vector2(.5f, .5f), new Vector2(0, -16), new Vector2(480, 16), out escapeFill);

        notice = Label("", play.transform, new Vector2(.5f, .5f), new Vector2(0, 130), new Vector2(1000, 70), 30, Color.white, TextAnchor.MiddleCenter, true);
        noticeRect = notice.rectTransform;
        var outline = notice.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .7f); outline.effectDistance = new Vector2(2, -2);
        countdown = Label("", play.transform, new Vector2(.5f, .5f), new Vector2(0, 30), new Vector2(400, 200), 130, gold, TextAnchor.MiddleCenter, true);
        countdown.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .7f);
        Label("WASD mover · Espacio saltar · Clic izq./J golpear · E mantener (instalar / reanimar) · Q soltar pieza · Clic der./Z C girar · M música · Esc pausa",
            play.transform, new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(1180, 24), 13, new Color(1, 1, 1, .7f));

        // ---- lobby
        lobby = Fullscreen("Sala de espera", root.transform, .45f);
        var lc = Panel("Tarjeta de inicio", lobby.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(700, 540), .95f);
        Label("INVASIÓN ZOMBI", lc.transform, new Vector2(.5f, .5f), new Vector2(0, 222), new Vector2(660, 56), 44, gold, TextAnchor.MiddleCenter, true);
        Label("Supervivencia cooperativa · 10 oleadas · repara la camioneta y escapa", lc.transform, new Vector2(.5f, .5f), new Vector2(0, 176), new Vector2(660, 30), 18, accent);
        Label("En cada oleada un zombi brillante —o un jefe en las oleadas 3, 6, 9 y 10— suelta una pieza de la camioneta. " +
              "Llévala a la zona de reparación y mantén E para instalarla. Con las 10 piezas, todos a la camioneta y ¡a escapar! " +
              "Si caéis todos a la vez, se acabó: reanimad a los compañeros derribados.",
            lc.transform, new Vector2(.5f, .5f), new Vector2(0, 100), new Vector2(620, 100), 15, text);
        Label("JUGADORES", lc.transform, new Vector2(.5f, .5f), new Vector2(0, 26), new Vector2(300, 28), 18, accent, TextAnchor.MiddleCenter, true);
        for (int i = 0; i < 3; i++)
        {
            int n = 2 + i;
            countButtons[i] = MakeButton(n.ToString(), lc.transform, new Vector2(.5f, .5f), new Vector2(-140 + i * 140, -26), new Vector2(118, 56), () => { selectedPlayers = n; RefreshCountButtons(); MultiverseUI.PlayClick(false); }, 26);
        }
        Label("Tú + compañeros controlados por la IA", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -68), new Vector2(500, 24), 14, new Color(text.r, text.g, text.b, .7f));
        MakeButton("¡SOBREVIVIR!", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -124), new Vector2(290, 62), StartPressed, 26);
        MakeButton("Volver al Hub", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -192), new Vector2(220, 44), () => Director.ReturnToHub(), 18);
        Label("WASD · Clic izq./J golpear · E instalar / reanimar · Q soltar · Esc pausa", lc.transform, new Vector2(.5f, .5f), new Vector2(0, -242), new Vector2(620, 24), 13, new Color(text.r, text.g, text.b, .6f));

        // ---- pause
        pause = Fullscreen("Pausa", root.transform, .5f);
        var pc = Panel("Tarjeta de pausa", pause.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(380, 360), .96f);
        Label("PAUSA", pc.transform, new Vector2(.5f, .5f), new Vector2(0, 130), new Vector2(340, 50), 36, gold, TextAnchor.MiddleCenter, true);
        MakeButton("Continuar", pc.transform, new Vector2(.5f, .5f), new Vector2(0, 58), new Vector2(240, 50), TogglePause, 20);
        var mb = MakeButton("Música: sí", pc.transform, new Vector2(.5f, .5f), new Vector2(0, -2), new Vector2(240, 46), () => { EscapeAudio.ToggleMusic(); RefreshMusic(); }, 18);
        musicLabel = mb.GetComponentInChildren<Text>();
        MakeButton("Reiniciar", pc.transform, new Vector2(.5f, .5f), new Vector2(0, -60), new Vector2(240, 46), () => Director.Restart(), 18);
        MakeButton("Volver al Hub", pc.transform, new Vector2(.5f, .5f), new Vector2(0, -118), new Vector2(240, 46), () => Director.ReturnToHub(), 18);
        pause.SetActive(false);

        // ---- results
        results = Fullscreen("Resultados", root.transform, .55f);
        var rc = Panel("Tarjeta de resultados", results.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(700, 540), .96f);
        resultTitle = Label("", rc.transform, new Vector2(.5f, .5f), new Vector2(0, 218), new Vector2(660, 60), 42, gold, TextAnchor.MiddleCenter, true);
        resultSub = Label("", rc.transform, new Vector2(.5f, .5f), new Vector2(0, 172), new Vector2(660, 30), 18, accent);
        resultBody = Label("", rc.transform, new Vector2(.5f, .5f), new Vector2(0, -6), new Vector2(600, 310), 16, text, TextAnchor.UpperLeft);
        MakeButton("Jugar otra vez", rc.transform, new Vector2(.5f, .5f), new Vector2(-130, -222), new Vector2(230, 54), () => Director.Restart(), 20);
        MakeButton("Volver al Hub", rc.transform, new Vector2(.5f, .5f), new Vector2(130, -222), new Vector2(230, 54), () => Director.ReturnToHub(), 20);
        results.SetActive(false);
    }

    static Sprite vignette;
    static Sprite Vignette()
    {
        if (vignette != null) return vignette;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + .5f) / n * 2f - 1f, dy = (y + .5f) / n * 2f - 1f;
                float a = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .55f) / .6f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
        tex.Apply();
        return vignette = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f));
    }

    void RefreshCountButtons()
    {
        for (int i = 0; i < 3; i++)
        {
            int n = 2 + i;
            countButtons[i].GetComponentInChildren<Text>().text = n == selectedPlayers ? "► " + n + " ◄" : n.ToString();
            countButtons[i].GetComponent<Image>().color = n == selectedPlayers ? theme.panelEdge : theme.button;
        }
    }

    void RefreshMusic() { if (musicLabel != null) musicLabel.text = EscapeAudio.MusicOn ? "Música: sí" : "Música: no"; }

    void StartPressed()
    {
        lobby.SetActive(false);
        Director.StartGame(selectedPlayers);
    }

    void TogglePause()
    {
        if (!PauseOpen)
        {
            if (!Director.Fighting) return;
            savedTimeScale = Time.timeScale;
            if (!PartyLink.Active) Time.timeScale = 0f;   // online the battle goes on for the others
            pause.SetActive(true);
            RefreshMusic();
        }
        else
        {
            pause.SetActive(false);
            Time.timeScale = savedTimeScale <= 0f ? 1f : savedTimeScale;
        }
    }

    public void Hurt(float amount) => hurt = Mathf.Clamp01(hurt + amount / 35f);

    public void ShowResults()
    {
        var d = Director;
        bool won = d.State == SurvivalDirector.Phase.Won;
        resultTitle.text = won ? "¡HABÉIS ESCAPADO!" : "OS HAN RODEADO";
        int m = Mathf.FloorToInt(d.Elapsed / 60f), s = Mathf.FloorToInt(d.Elapsed % 60f);
        resultSub.text = won ? $"10 oleadas superadas · {m:00}:{s:00} · {d.PlayerCount} jugadores"
                             : $"Llegasteis a la oleada {d.Wave}/10 · camioneta {d.PartsInstalled}/10";
        var lines = new System.Text.StringBuilder();
        foreach (var p in d.Team)
            lines.AppendLine($"{(p.IsHuman ? "TÚ" : p.DisplayName)}:  {p.Kills} zombis · {p.Parts} piezas · {p.Revives} reanimaciones · {p.Downs} caídas");
        lines.AppendLine();
        var best = d.MostValuable;
        if (best != null) lines.AppendLine($"MVP: {(best.IsHuman ? "TÚ" : best.DisplayName)} ({best.Score} puntos)");
        lines.AppendLine($"Zombis derrotados en total: {d.TotalKills}");
        int bosses = new[] { 3, 6, 9, 10 }.Count(w => d.Wave > w || (d.Wave == w && won));
        lines.AppendLine($"Jefes derrotados: {bosses}/4");
        resultBody.text = lines.ToString();
        results.SetActive(true);
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        var d = Director;
        if (d == null || root == null) return;
        var k = Keyboard.current;

        if (lobby.activeSelf && k != null)
        {
            if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) { selectedPlayers = 2; RefreshCountButtons(); }
            if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) { selectedPlayers = 3; RefreshCountButtons(); }
            if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) { selectedPlayers = 4; RefreshCountButtons(); }
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) StartPressed();
        }
        else if (k != null && k.escapeKey.wasPressedThisFrame && !results.activeSelf) TogglePause();

        hurt = Mathf.MoveTowards(hurt, 0f, Time.unscaledDeltaTime * 1.4f);
        float lowHp = d.Human != null && !d.Human.Downed && d.Human.Health < 30f ? .25f + Mathf.PingPong(Time.unscaledTime, .5f) * .3f : 0f;
        hurtFlash.color = new Color(1f, .05f, .05f, Mathf.Max(hurt * .7f, lowHp, d.Human != null && d.Human.Downed ? .5f : 0f));

        bool inGame = d.State != SurvivalDirector.Phase.Lobby;
        play.SetActive(inGame && !results.activeSelf);
        if (!inGame) return;

        // Wave banner.
        waveTitle.text = d.State == SurvivalDirector.Phase.Escape || d.State == SurvivalDirector.Phase.Won ? "¡ESCAPE!" : $"OLEADA {Mathf.Max(1, d.Wave)}/10";
        waveSub.text = d.State switch
        {
            SurvivalDirector.Phase.Wave => $"Zombis restantes: {d.ZombiesLeft}",
            SurvivalDirector.Phase.Break => $"Siguiente oleada en {Mathf.CeilToInt(d.BreakLeft)} s",
            SurvivalDirector.Phase.Escape => "¡La horda no para!",
            SurvivalDirector.Phase.Won => "¡Fuera de la ciudad!",
            SurvivalDirector.Phase.Lost => "Derrota",
            _ => "Preparaos…"
        };

        var boss = d.Boss;
        bossPanel.SetActive(boss != null && !boss.Dead);
        if (boss != null)
        {
            bossName.text = boss.Title + (boss.Enraged ? "  ·  ¡FURIOSO!" : "");
            SetFill(bossFill, 500f, boss.Health / boss.MaxHealth);
        }

        // Objective card.
        string partName = d.Wave >= 1 && d.Wave <= SurvivalDirector.TotalWaves ? SurvivalDirector.PartNames[d.Wave - 1] : "";
        var carrierPlayer = d.Team.FirstOrDefault(p => p.Carried != null);
        if (d.State == SurvivalDirector.Phase.Escape)
        {
            objTitle.text = "¡A LA CAMIONETA!";
            objective.text = "Todo el equipo en pie dentro de la zona verde hasta que arranque el motor.";
            hint.text = "Si alguien sale de la zona, el arranque se detiene.";
        }
        else if (d.State == SurvivalDirector.Phase.Break)
        {
            objTitle.text = "DESCANSO";
            objective.text = $"Recuperad fuerzas. Próxima pieza: {SurvivalDirector.PartNames[Mathf.Min(d.Wave, 9)]}.";
            hint.text = SurvivalDirector.BossFor(d.Wave + 1) != SurvivalZombie.BossKind.None ? $"¡Se acerca {SurvivalZombie.BossTitle(SurvivalDirector.BossFor(d.Wave + 1))}!" : "Los compañeros caídos se levantan en el descanso.";
        }
        else if (d.State == SurvivalDirector.Phase.Wave)
        {
            objTitle.text = $"PIEZA: {partName.ToUpperInvariant()}";
            if (d.PartReady) { objective.text = $"¡Pieza instalada! Acabad con los {d.ZombiesLeft} zombis que quedan."; hint.text = "La oleada termina cuando no queda ninguno."; }
            else if (carrierPlayer != null) { objective.text = $"{(carrierPlayer.IsHuman ? "Llevas" : carrierPlayer.DisplayName + " lleva")} la pieza → zona de reparación y mantén E."; hint.text = "Con la pieza encima no puedes golpear. Q para soltarla."; }
            else if (d.LoosePart != null)
            {
                float dist = d.Human != null ? Vector3.Distance(d.Human.transform.position, d.LoosePart.transform.position) : 0f;
                objective.text = $"¡La pieza está en el suelo a {dist:0} m! Recógela (haz de luz dorado).";
                hint.text = "Pasa por encima para cogerla.";
            }
            else if (d.Boss != null) { objective.text = $"Derrotad a {d.Boss.Title}: suelta la pieza."; hint.text = BossHint(d.Boss.Boss); }
            else if (d.Carrier != null) { objective.text = "Derrotad al zombi brillante (aura verde): lleva la pieza."; hint.text = "Su nombre flota sobre él."; }
            else { objective.text = "Aguantad: el portador de la pieza aún no ha llegado."; hint.text = "Vienen por las cuatro entradas."; }
        }
        else { objTitle.text = "PREPARAOS"; objective.text = "Defended la camioneta averiada."; hint.text = ""; }

        // Truck repair list.
        repairTitle.text = $"CAMIONETA {d.PartsInstalled}/10";
        for (int i = 0; i < partRows.Length; i++)
        {
            bool done = i < d.PartsInstalled, current = i == d.Wave - 1 && !done && d.State == SurvivalDirector.Phase.Wave;
            partRows[i].text = (done ? "✓ " : current ? "► " : "· ") + SurvivalDirector.PartNames[i];
            partRows[i].color = done ? Green : current ? theme.button : new Color(theme.text.r, theme.text.g, theme.text.b, .5f);
        }

        // Team.
        int n = d.Team.Count;
        teamPanel.sizeDelta = new Vector2(256, 46 + n * 34);
        teamPanel.anchoredPosition = new Vector2(-142, -(46 + n * 34) * .5f - 16);
        for (int i = 0; i < SurvivalDirector.MaxPlayers; i++)
        {
            bool on = i < n;
            teamRows[i].gameObject.SetActive(on); teamDots[i].gameObject.SetActive(on); teamBars[i].transform.parent.gameObject.SetActive(on);
            if (!on) continue;
            var p = d.Team[i];
            teamDots[i].color = p.Tint;
            string state = p.Downed ? $"  · DERRIBADO {Mathf.Max(0, Mathf.CeilToInt(SurvivalPlayer.AutoReviveSeconds - (Time.time - p.DownedAt)))}s" : p.Carried != null ? "  · lleva pieza" : "";
            teamRows[i].text = (p.IsHuman ? "TÚ" : p.DisplayName) + state;
            teamRows[i].color = p.Downed ? Red : theme.text;
            SetFill(teamBars[i], 206f, p.Health / SurvivalPlayer.MaxHealth);
            teamBars[i].color = p.Health < 30f ? Red : Green;
        }

        // Own health.
        var human = d.Human;
        if (human != null)
        {
            hpText.text = human.Downed ? "KO" : "VIDA " + Mathf.CeilToInt(human.Health);
            SetFill(hpFill, 240f, human.Health / SurvivalPlayer.MaxHealth);
            hpFill.color = human.Health < 30f ? Red : Green;
        }

        // Prompt.
        string prompt = null; float progress = 0f;
        if (human != null && d.InputFree)
        {
            if (human.Downed) prompt = $"Derribado · un compañero puede reanimarte ({Mathf.Max(0, Mathf.CeilToInt(SurvivalPlayer.AutoReviveSeconds - (Time.time - human.DownedAt)))} s)";
            else if (human.Focus == SurvivalPlayer.UseKind.Install && human.Carried != null) { prompt = "E  ·  Instalar " + human.Carried.Label + "  (mantén)"; progress = human.UseProgress; }
            else if (human.Focus == SurvivalPlayer.UseKind.Revive && human.FocusPlayer != null) { prompt = "E  ·  Reanimar a " + human.FocusPlayer.DisplayName + "  (mantén)"; progress = human.UseProgress; }
            else if (human.Carried != null) prompt = "Lleva la pieza a la zona de reparación";
        }
        promptPanel.gameObject.SetActive(prompt != null);
        if (prompt != null) { promptText.text = prompt; promptBar.rectTransform.sizeDelta = new Vector2(400f * Mathf.Clamp01(progress), 8); }

        // Escape progress.
        escapePanel.SetActive(d.State == SurvivalDirector.Phase.Escape);
        if (d.State == SurvivalDirector.Phase.Escape)
        {
            int inside = d.Team.Count(p => !p.Downed && d.Level.InTruckZone(p.transform.position));
            int standing = d.Team.Count(p => !p.Downed);
            escapeText.text = $"ARRANCANDO EL MOTOR · {inside}/{standing} en la camioneta";
            SetFill(escapeFill, 480f, d.EscapeProgress);
        }

        // Notice pop and countdown.
        notice.text = d.Notice;
        float age = Time.time - d.NoticeAt;
        noticeRect.localScale = Vector3.one * (d.Notice.Length > 0 ? 1f + Mathf.Max(0f, .25f - age) * 1.6f : 1f);
        if (d.State == SurvivalDirector.Phase.Countdown)
        {
            int c = Mathf.CeilToInt(d.Countdown);
            countdown.text = c > 3 ? "" : c.ToString();
            countdown.rectTransform.localScale = Vector3.one * (1f + Mathf.Repeat(d.Countdown, 1f) * .4f);
        }
        else countdown.text = "";
    }

    static string BossHint(SurvivalZombie.BossKind b) => b switch
    {
        SurvivalZombie.BossKind.Gordo => "Cuando aparezca el círculo rojo, ¡apártate! Golpea tras el pisotón.",
        SurvivalZombie.BossKind.Brazos => "La franja roja avisa de su embestida: esquívala de lado. Se aturde si choca.",
        SurvivalZombie.BossKind.Costillas => "Invoca corredores: no te separes del equipo.",
        _ => "Pisotón, invocaciones y, al enfurecerse, embestidas. ¡Todo el equipo a por él!"
    };
}
