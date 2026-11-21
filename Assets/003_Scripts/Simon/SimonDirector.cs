using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// MEMORIA MUSICAL (2026-11-19) · "Simon says" for 2–6 players. A giant board with four coloured pads plays a
/// sequence of lights and notes; then everybody repeats it at the same time with ↑ → ↓ ← (or W D S A, 1–4, the pad
/// buttons). One more note every round, a bit faster. A mistake or running out of time costs a life (2 lives);
/// if everybody fails, the round is repeated. Last one with lives wins (after 14 rounds: most lives, then fastest).
/// The sequence and its timing come from the seed and the shared clock; each computer judges its own player and
/// tells the host, which decides lives and the winner (bots play on the host).
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class SimonDirector : MonoBehaviour
{
    public const float CountdownSeconds = 4f, JudgeSeconds = 2.4f;
    public const int StartLives = 2, MaxRounds = 14, StartLength = 3;
    public static readonly Color[] PadColors = { new Color(1f, .25f, .25f), new Color(.25f, .55f, 1f), new Color(.3f, .9f, .35f), new Color(1f, .85f, .2f) };
    public static readonly string[] PadNames = { "rojo", "azul", "verde", "amarillo" };
    public static readonly string[] PadKeys = { "↑", "→", "↓", "←" };

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Show, Input, Judge, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public int Round { get; private set; } = 1;
    public int Length => Round + StartLength - 1;
    public float Step => Mathf.Max(.3f, .62f - (Round - 1) * .025f);
    public float ShowStart { get; private set; }
    public float InputStart => ShowStart + Length * Step + .35f;
    public float InputEnd => InputStart + Length * .85f + 2.5f;
    public readonly List<SimonPlayer> Players = new();
    public SimonPlayer Human { get; private set; }
    public SimonNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string BigText { get; private set; } = ""; public Color BigColor { get; private set; } = Color.white; float bigUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool Dirty { get; set; }

    // Per slot, this round: 0 = playing, 1 = done right, 2 = failed. Progress = notes entered right.
    public int[] Status { get; private set; }
    public int[] Progress { get; private set; }
    public float[] DoneTime { get; private set; }
    public float[] TotalTime { get; private set; }
    public int MyPressed { get; private set; }      // this computer's human, this round
    public bool AllFailedLast { get; private set; }

    readonly int[] seq = new int[64];
    readonly Dictionary<int, (bool ok, int failAt, float time)> botPlan = new();
    Renderer[] pads; Material[] padMats; Light[] padLights; readonly List<TextMesh> marks = new();
    float[] padFlashUntil = new float[4]; int lastShown = -1; Camera cam; float trauma; Phase shownPhase = Phase.Waiting; int shownRound = -1;

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 6151 + PartyLink.Round * 29) : Random.Range(1, 999999);
        var rng = new System.Random(Seed);
        for (int i = 0; i < seq.Length; i++) seq[i] = rng.Next(4);
        RenderSettings.ambientLight = new Color(.35f, .32f, .45f); RenderSettings.fog = false;
        BuildStage();
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Status = new int[count]; Progress = new int[count]; DoneTime = new float[count]; TotalTime = new float[count];
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            float a = count == 1 ? 0f : Mathf.Lerp(-50f, 50f, member / (float)(count - 1));
            var at = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 7.4f, 0, Mathf.Cos(a * Mathf.Deg2Rad) * 7.4f);
            var p = new GameObject(name).AddComponent<SimonPlayer>();
            p.Init(i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, PartyLink.IsPuppet(i), characterPrefab, unchecked(Seed + member * 3301 + 5), at, new Vector3(0, 0, -2f));
            p.IsBot = !p.Puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i);
            Players.Add(p); if (i == 0) Human = p;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var t = new GameObject("Progreso").AddComponent<TextMesh>(); t.transform.SetParent(p.transform, false); t.transform.localPosition = Vector3.up * 3.3f;
            t.font = font; t.GetComponent<MeshRenderer>().sharedMaterial = font.material; t.anchor = TextAnchor.MiddleCenter; t.fontSize = 48; t.characterSize = .045f; t.fontStyle = FontStyle.Bold;
            marks.Add(t);
        }
        cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .04f, .1f); cam.fieldOfView = 52f;
        var home = new Vector3(0, 6.4f, 15.5f); cam.transform.SetPositionAndRotation(home, Quaternion.LookRotation(new Vector3(0, 2.6f, 0) - home));
        gameObject.AddComponent<SimonHUD>().Director = this;
        SimonAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<SimonNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; SimonAudio.Release(); }

    void BuildStage()
    {
        var root = new GameObject("Escenario disco").transform;
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Pista", new Vector3(0, -.15f, 3.5f), new Vector3(22f, .15f, 18f), EscapeKit.Paint(new Color(.12f, .1f, .2f), .6f), false);
        // Disco floor squares.
        Color[] floor = { new Color(.6f, .2f, .7f), new Color(.2f, .4f, .8f), new Color(.8f, .3f, .5f), new Color(.2f, .6f, .6f) };
        for (int x = -4; x <= 4; x++) for (int z = -1; z <= 7; z++)
            if ((new Vector2(x * 1.6f, (z - 3) * 1.6f)).magnitude < 7.5f)
                EscapeKit.Block(root, "Baldosa disco", new Vector3(x * 1.6f, .01f, z * 1.2f + 1f), new Vector3(1.5f, .02f, 1.1f), EscapeKit.Glow(floor[(x + z + 8) % 4], .25f), .01f, false);
        // Back wall with the giant Simon board.
        EscapeKit.Block(root, "Pared", new Vector3(0, 4.5f, -4.2f), new Vector3(20f, 9f, .4f), EscapeKit.Paint(new Color(.1f, .08f, .16f), .4f), .1f, false);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Marco", new Vector3(0, 4.3f, -3.95f), new Vector3(7.4f, .1f, 7.4f), EscapeKit.Paint(new Color(.15f, .15f, .18f), .8f), false, new Vector3(90f, 0, 0));
        pads = new Renderer[4]; padMats = new Material[4]; padLights = new Light[4];
        // The camera looks towards −z, so screen-right is world −x.
        Vector3[] pos = { new Vector3(0, 6.05f, -3.85f), new Vector3(-1.75f, 4.3f, -3.85f), new Vector3(0, 2.55f, -3.85f), new Vector3(1.75f, 4.3f, -3.85f) };
        for (int i = 0; i < 4; i++)
        {
            padMats[i] = EscapeKit.Glow(PadColors[i] * .45f, .4f);
            var pad = EscapeKit.Block(root, "Botón " + PadNames[i], pos[i], new Vector3(1.6f, 1.6f, .2f), padMats[i], .25f, false, new Vector3(0, 0, 45f));
            pads[i] = pad.GetComponent<Renderer>();
            padLights[i] = EscapeKit.PointLight(root, pos[i] + Vector3.forward * 1.5f, PadColors[i], 9f, 0f);
            EscapeKit.Text(root, PadKeys[i], pos[i] + new Vector3(0, 0, .2f), new Vector3(0, 180f, 0), .09f, new Color(1, 1, 1, .8f));
        }
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Centro", new Vector3(0, 4.3f, -3.8f), new Vector3(1.4f, .12f, 1.4f), EscapeKit.Paint(new Color(.2f, .2f, .24f), .7f), false, new Vector3(90f, 0, 0));
        EscapeKit.Text(root, "MEMORIA MUSICAL", new Vector3(0, 8.1f, -3.95f), new Vector3(0, 180f, 0), .12f, new Color(1f, .85f, .4f));
        // Speakers, mirror ball and coloured spots.
        var dark = EscapeKit.Paint(new Color(.08f, .08f, .1f), .3f); var cone = EscapeKit.Paint(new Color(.25f, .25f, .3f), .6f);
        foreach (var x in new[] { -7f, 7f })
        {
            EscapeKit.Block(root, "Altavoz", new Vector3(x, 1.6f, -3f), new Vector3(1.8f, 3.2f, 1.4f), dark, .1f, false);
            EscapeKit.Shape(PrimitiveType.Cylinder, root, "Cono", new Vector3(x, 2.2f, -2.28f), new Vector3(1.2f, .05f, 1.2f), cone, false, new Vector3(90f, 0, 0));
            EscapeKit.Shape(PrimitiveType.Cylinder, root, "Cono", new Vector3(x, .9f, -2.28f), new Vector3(.8f, .05f, .8f), cone, false, new Vector3(90f, 0, 0));
        }
        EscapeKit.Shape(PrimitiveType.Sphere, root, "Bola de espejos", new Vector3(0, 9.5f, 3f), Vector3.one * 1.2f, EscapeKit.Paint(new Color(.85f, .88f, .95f), .95f), false);
        var sun = new GameObject("Luz").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .55f; sun.transform.rotation = Quaternion.Euler(50f, 170f, 0);
        foreach (var x in new[] { -6f, 6f })
        {
            var l = new GameObject("Foco").AddComponent<Light>(); l.type = LightType.Spot; l.range = 30f; l.spotAngle = 60f; l.intensity = 2.2f; l.color = x < 0 ? new Color(1f, .5f, .9f) : new Color(.5f, .8f, 1f);
            l.transform.SetPositionAndRotation(new Vector3(x, 9f, 10f), Quaternion.LookRotation(new Vector3(0, 0, 5f) - new Vector3(x, 9f, 10f)));
        }
    }

    public int SeqAt(int i) => seq[i % seq.Length];

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        if (!IsClient) HostLoop(); else if (State != Phase.Waiting && State != Phase.Countdown && State != Phase.Finished) Elapsed += Time.deltaTime;
        LocalInput();
        Present();
        if (Time.time > bigUntil) BigText = "";
    }

    // ------------------------------------------------------------------ host

    void HostLoop()
    {
        switch (State)
        {
            case Phase.Waiting: if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; } break;
            case Phase.Countdown:
                float before = Countdown; Countdown -= Time.deltaTime;
                if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) SimonAudio.UI("beep", .7f);
                if (Countdown <= 0f) { Countdown = 0f; Elapsed = 0f; StartRound(); }
                break;
            case Phase.Show:
                Elapsed += Time.deltaTime;
                if (Elapsed >= InputStart) { State = Phase.Input; PlanBots(); Dirty = true; }
                break;
            case Phase.Input:
                Elapsed += Time.deltaTime;
                BotsPlay();
                bool all = true; foreach (var p in Players) if (!p.Out && Status[p.Slot] == 0) all = false;
                if (all || Elapsed >= InputEnd + .4f) Judge();
                break;
            case Phase.Judge:
                Elapsed += Time.deltaTime;
                if (Elapsed >= judgeAt + JudgeSeconds)
                {
                    int alive = Players.Count(p => !p.Out);
                    if (alive <= (Players.Count > 1 ? 1 : 0) || Round >= MaxRounds) Finish();
                    else { if (!AllFailedLast) Round++; StartRound(); }
                }
                break;
        }
    }
    float judgeAt;

    void StartRound()
    {
        foreach (var p in Players) { Status[p.Slot] = p.Out ? 2 : 0; Progress[p.Slot] = 0; DoneTime[p.Slot] = 0f; }
        ShowStart = Elapsed + .9f; State = Phase.Show; Dirty = true;
    }

    void PlanBots()
    {
        botPlan.Clear();
        foreach (var p in Players)
        {
            if (!p.IsBot || p.Out) continue;
            float skill = MultiverseSettings.BotSkill(.35f + (float)new System.Random(unchecked(Seed + p.Slot * 91)).NextDouble() * .55f);
            float pe = Mathf.Lerp(.94f, .992f, skill);
            int failAt = -1; for (int i = 0; i < Length; i++) if (Random.value > pe) { failAt = i; break; }
            float perNote = Mathf.Lerp(.55f, .3f, skill);
            botPlan[p.Slot] = (failAt < 0, failAt, Length * perNote + Random.Range(.2f, .7f));
        }
    }

    void BotsPlay()
    {
        float t = Elapsed - InputStart;
        foreach (var kv in botPlan)
        {
            int s = kv.Key; if (Status[s] != 0) continue;
            var (ok, failAt, time) = kv.Value;
            int shouldHave = Mathf.Clamp(Mathf.FloorToInt(t / time * Length), 0, Length);
            if (!ok && shouldHave > failAt) { HostResult(s, false, failAt, t); Players[s].Act("Hit", .8f); continue; }
            if (shouldHave > Progress[s]) { Progress[s] = shouldHave; Players[s].Act("Attack", .25f); Dirty = true; }
            if (ok && t >= time) HostResult(s, true, Length, t);
        }
    }

    public void HostResult(int slot, bool ok, int progress, float time)
    {
        if (slot < 0 || slot >= Players.Count || Status[slot] != 0 || State != Phase.Input) return;
        Status[slot] = ok ? 1 : 2; Progress[slot] = progress; DoneTime[slot] = time;
        Dirty = true;
    }

    public void HostProgress(int slot, int progress) { if (slot >= 0 && slot < Players.Count && Status[slot] == 0) { Progress[slot] = progress; Dirty = true; } }

    void Judge()
    {
        foreach (var p in Players) if (!p.Out && Status[p.Slot] == 0) Status[p.Slot] = 2;   // ran out of time
        var alive = Players.Where(p => !p.Out).ToList();
        AllFailedLast = alive.All(p => Status[p.Slot] == 2);
        if (!AllFailedLast)
            foreach (var p in alive)
            {
                if (Status[p.Slot] == 2) p.Lives = Mathf.Max(0, p.Lives - 1);
                else TotalTime[p.Slot] += DoneTime[p.Slot];
            }
        State = Phase.Judge; judgeAt = Elapsed; Dirty = true;
        Net?.SendWorld();
    }

    void Finish()
    {
        if (State == Phase.Finished) return;
        var best = Ranking().FirstOrDefault();
        State = Phase.Finished; WinnerSlot = best != null ? best.Slot : -1;
        Result = best != null ? "¡" + best.DisplayName.ToUpperInvariant() + " TIENE LA MEJOR MEMORIA!" : "SIN GANADOR";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { Players[WinnerSlot].Act("Victory", 999f); MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2.5f, 50, 8); }
        SimonAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); SimonAudio.FadeMusic(.15f); Paused = false;
    }

    public List<SimonPlayer> Ranking() => Players.OrderByDescending(p => p.Lives).ThenBy(p => TotalTime[p.Slot]).ThenBy(p => p.Slot == WinnerSlot ? 0 : 1).ToList();

    // ------------------------------------------------------------------ this computer's player

    void LocalInput()
    {
        if (Paused || Human == null || Human.IsBot || Human.Out || State != Phase.Input && !(IsClient && State == Phase.Show && Elapsed >= InputStart)) return;
        if (Elapsed < InputStart || Elapsed > InputEnd || Status[Human.Slot] != 0 || myDone) return;
        var k = Keyboard.current; var g = Gamepad.current;
        int pad = -1;
        if (k != null)
        {
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.digit1Key.wasPressedThisFrame || k.numpad8Key.wasPressedThisFrame) pad = 0;
            else if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame || k.digit2Key.wasPressedThisFrame || k.numpad6Key.wasPressedThisFrame) pad = 1;
            else if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame || k.digit3Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) pad = 2;
            else if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame || k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) pad = 3;
        }
        if (g != null && pad < 0)
        {
            if (g.dpad.up.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame) pad = 0;
            else if (g.dpad.right.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame) pad = 1;
            else if (g.dpad.down.wasPressedThisFrame || g.buttonSouth.wasPressedThisFrame) pad = 2;
            else if (g.dpad.left.wasPressedThisFrame || g.buttonWest.wasPressedThisFrame) pad = 3;
        }
        if (pad < 0) return;
        Flash(pad, .25f, true);
        Human.Act("Attack", .25f);
        float t = Elapsed - InputStart;
        if (pad == SeqAt(MyPressed))
        {
            MyPressed++;
            if (MyPressed >= Length) { myDone = true; SimonAudio.UI("ok", .9f); Big("¡PERFECTO! " + t.ToString("0.0") + " s", new Color(.5f, 1f, .55f), 1.4f); Report(true, MyPressed, t); }
            else Report(false, MyPressed, t, true);
        }
        else
        {
            myDone = true; SimonAudio.UI("fail", 1f); trauma = .5f;
            Big("¡FALLO! Era " + PadNames[SeqAt(MyPressed)].ToUpperInvariant(), new Color(1f, .45f, .4f), 1.6f);
            Human.Act("Hit", .8f);
            Report(false, MyPressed, t);
        }
    }
    bool myDone;

    void Report(bool ok, int progress, float time, bool partial = false)
    {
        if (IsClient) { Net?.SendPress(Human.Member, Round, ok, progress, time, partial); return; }
        if (partial) HostProgress(Human.Slot, progress); else HostResult(Human.Slot, ok, progress, time);
    }

    // ------------------------------------------------------------------ presentation

    void Flash(int pad, float seconds, bool sound)
    {
        padFlashUntil[pad] = Time.time + seconds;
        if (sound) SimonAudio.UI("pad" + pad, .9f);
    }

    void Present()
    {
        if (State != shownPhase || Round != shownRound)
        {
            if (State == Phase.Show && (shownPhase != Phase.Show || Round != shownRound)) { MyPressed = 0; myDone = false; lastShown = -1; Big("RONDA " + Round + " · ¡MIRA Y ESCUCHA!", new Color(.7f, .85f, 1f), 1.4f); SimonAudio.UI("round", .6f); }
            if (State == Phase.Input && shownPhase != Phase.Input) { SimonAudio.UI("go", .7f); if (Human != null && !Human.Out) Big("¡TU TURNO! " + Length + " notas", new Color(1f, .85f, .4f), 1.2f); }
            if (State == Phase.Judge && shownPhase != Phase.Judge) ShowJudge();
            shownPhase = State; shownRound = Round;
        }
        // The board plays the sequence on the shared clock.
        if (State == Phase.Show || (State == Phase.Input && Elapsed < InputStart))
        {
            int i = Mathf.FloorToInt((Elapsed - ShowStart) / Step);
            if (i >= 0 && i < Length && i != lastShown && (Elapsed - ShowStart) - i * Step < Step * .75f) { lastShown = i; Flash(SeqAt(i), Step * .7f, true); }
        }
        for (int p = 0; p < 4; p++)
        {
            bool on = Time.time < padFlashUntil[p];
            padMats[p].color = on ? PadColors[p] : PadColors[p] * .4f;
            EscapeKit.SetGlow(padMats[p], PadColors[p], on ? 3f : .35f);
            padLights[p].intensity = on ? 4f : 0f;
        }
        // Marks over the contestants.
        for (int s = 0; s < Players.Count; s++)
        {
            var pl = Players[s]; var m = marks[s];
            if (Camera.main != null) m.transform.rotation = Camera.main.transform.rotation;
            if (pl.Out) { m.text = ""; continue; }
            if (State == Phase.Input || State == Phase.Judge)
            {
                int done = s == Human?.Slot && !Human.IsBot ? MyPressed : Progress[s];
                m.text = Status[s] == 1 ? "¡BIEN!" : Status[s] == 2 && State == Phase.Judge ? "FALLO" : new string('●', Mathf.Min(done, Length)) + new string('○', Mathf.Max(0, Length - done));
                m.color = Status[s] == 1 ? new Color(.5f, 1f, .5f) : Status[s] == 2 && State == Phase.Judge ? new Color(1f, .4f, .4f) : Color.white;
                m.characterSize = Length > 9 ? .03f : .045f;
            }
            else m.text = "";
        }
        if (cam != null && trauma > 0f) { cam.transform.position = new Vector3(0, 6.4f, 15.5f) + Random.insideUnitSphere * trauma * trauma * .3f; trauma = Mathf.Max(0f, trauma - Time.deltaTime * 2f); }
    }

    void ShowJudge()
    {
        if (AllFailedLast) { Big("¡NADIE LO HA CONSEGUIDO! Se repite la ronda", new Color(1f, .8f, .5f), 2f); FeedLine("Ronda " + Round + ": todos fallan, se repite"); SimonAudio.UI("fail", .6f); return; }
        var fails = Players.Where(p => !p.Out && Status[p.Slot] == 2 || (p.Out && Status[p.Slot] == 2 && p.Lives == 0 && !announcedOut.Contains(p.Slot))).ToList();
        foreach (var p in Players)
        {
            if (Status[p.Slot] == 2 && !announcedOut.Contains(p.Slot))
            {
                if (p.Out) { announcedOut.Add(p.Slot); FeedLine(p.DisplayName + " se queda sin vidas"); if (p.IsHuman) Big("¡SIN VIDAS! Estás fuera", new Color(1f, .45f, .4f), 2f); }
                else FeedLine(p.DisplayName + " falla y pierde una vida");
            }
        }
        var best = Players.Where(p => Status[p.Slot] == 1).OrderBy(p => DoneTime[p.Slot]).FirstOrDefault();
        if (best != null) FeedLine("Ronda " + Round + ": el más rápido, " + best.DisplayName + " (" + DoneTime[best.Slot].ToString("0.0") + " s)");
        SimonAudio.UI(fails.Count > 0 ? "judge" : "ok", .6f);
    }
    readonly HashSet<int> announcedOut = new();

    void Big(string text, Color c, float seconds = 1.6f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    // ------------------------------------------------------------------ client

    public void ApplyWorld(Phase phase, float countdown, float elapsed, int round, float showStart, bool allFailed, int[] status, int[] progress, float[] doneTime, int[] lives, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) SimonAudio.UI("beep", .7f);
        Countdown = countdown;
        if (phase != Phase.Waiting && phase != Phase.Countdown)
        {
            if (State == Phase.Waiting || State == Phase.Countdown) Elapsed = elapsed;
            float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .35f ? elapsed : Elapsed + diff * .2f;
        }
        Round = round; ShowStart = showStart; AllFailedLast = allFailed;
        for (int s = 0; s < Players.Count; s++)
        {
            if (Human != null && s == Human.Slot && status[s] == 0 && Status[s] != 0 && phase == Phase.Input) { }
            Status[s] = status[s]; Progress[s] = progress[s]; DoneTime[s] = doneTime[s]; Players[s].Lives = lives[s];
        }
        if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winner; Result = result; Celebrate(); PartyLink.Finish(-1); return; }
        // Show/Input are both "show then input" on the clock: keep the client's own view of when input starts.
        State = phase;
    }

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
