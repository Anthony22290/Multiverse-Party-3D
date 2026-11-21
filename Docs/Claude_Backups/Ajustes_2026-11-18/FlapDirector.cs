using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// PROPULSORES (2026-11-17) · party minigame for 2–6 players: everybody flies together through the same course of
/// towers with a jetpack (tap to thrust, like Flappy Bird). Each tower passed is a point. Two lives: the first crash
/// respawns you, the second one is out and your score stays as it was. First to 30 points wins; when time runs out, the highest score wins.
/// The course is a pure function of the party seed and the clock, so online peers only share the clock and scores.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class FlapDirector : MonoBehaviour
{
    public const int Goal = 30, StartLives = 2;
    public const float RoundSeconds = 150f, CountdownSeconds = 4f, Spacing = 6.6f, FirstPipe = 14f, PipeHalf = .75f, Floor = .4f, Ceiling = 10.5f, MaxFrame = .1f;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Running, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float Remaining => Mathf.Max(0f, RoundSeconds - Elapsed);
    public readonly List<FlapPlayer> Players = new();
    public FlapPlayer Human { get; private set; }
    public FlapNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string Notice { get; private set; } = ""; float noticeUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    bool paused, tenLeft;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool InputFree => !Paused && State == Phase.Running && Human != null;
    public Camera Cam { get; private set; }
    float trauma;
    static readonly Vector3 CamHome = new Vector3(2.6f, 5.3f, -11.5f);

    // Course.
    System.Random rng; readonly List<float> gapY = new();
    readonly List<Transform> pipes = new(); Transform pipeRoot, scenery; readonly List<(Transform t, float factor, float width)> parallax = new();

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 1009 + PartyLink.Round * 7) : Random.Range(1, 999999);
        rng = new System.Random(Seed);
        BuildScenery();

        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        var order = Enumerable.Range(0, count).OrderBy(i => PartyLink.Active ? PartyLink.MemberOfSlot(i) : i).ToList();
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            int lane = order.IndexOf(i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name);
            var p = go.AddComponent<FlapPlayer>(); bool puppet = PartyLink.IsPuppet(i);
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet,
                   new Vector3(-.75f * lane, 4.5f + (lane % 2) * .6f, lane * .8f), characterPrefab, unchecked(Seed + member * 977 + 1));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<FlapBot>();
        }

        Cam = Camera.main;
        if (Cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; Cam = c.GetComponent<Camera>(); }
        Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = new Color(.5f, .78f, 1f); Cam.fieldOfView = 50f;
        Cam.transform.SetPositionAndRotation(CamHome, Quaternion.Euler(3f, -5f, 0));
        gameObject.AddComponent<FlapHUD>().Director = this;
        FlapAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<FlapNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; FlapAudio.Release(); }

    // ------------------------------------------------------------------ course (deterministic)

    /// <summary>Distance the world has scrolled after t seconds of race (speed 5 → 7 m/s).</summary>
    public static float Scroll(float t)
    {
        const float a = .02f, v0 = 5f, cap = 100f;   // speed = v0 + a·t until t = cap
        if (t <= cap) return v0 * t + .5f * a * t * t;
        return v0 * cap + .5f * a * cap * cap + (v0 + a * cap) * (t - cap);
    }
    public float ScrollNow => State == Phase.Countdown || State == Phase.Waiting ? 0f : Scroll(Elapsed);
    public float PipeX(int k) => FirstPipe + k * Spacing - ScrollNow;
    public float GapY(int k)
    {
        while (gapY.Count <= k)
        {
            float prev = gapY.Count > 0 ? gapY[gapY.Count - 1] : 5f;
            float y = Mathf.Clamp(prev + ((float)rng.NextDouble() - .5f) * 4.4f, 2.6f, 8.2f);
            gapY.Add(y);
        }
        return gapY[k];
    }
    public static float GapSize(int k) => Mathf.Lerp(3.7f, 2.75f, Mathf.Clamp01(k / 45f));
    /// <summary>Index of the first tower whose right edge is still ahead of x.</summary>
    public int PipeAhead(float x) { int k = Mathf.Max(0, Mathf.FloorToInt((x - PipeHalf - FirstPipe + ScrollNow) / Spacing)); while (PipeX(k) + PipeHalf < x) k++; return k; }

    void BuildScenery()
    {
        scenery = new GameObject("Paisaje").transform;
        var grass = EscapeKit.Paint(new Color(.45f, .78f, .35f), .05f);
        EscapeKit.Block(scenery, "Suelo", new Vector3(10f, -.5f, 3f), new Vector3(120f, 1.8f, 20f), grass, .05f, false);
        // Parallax layers: hills, far city, clouds.
        Color[] hill = { new Color(.38f, .66f, .36f), new Color(.5f, .74f, .42f) };
        for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < 9; i++)
            {
                var h = EscapeKit.Shape(PrimitiveType.Sphere, scenery, "Colina", new Vector3(-20f + i * 9f + layer * 4f, -1f, 14f + layer * 8f), new Vector3(12f, 7f + (i % 3) * 2f, 4f), EscapeKit.Paint(hill[layer]), false);
                parallax.Add((h.transform, .25f + layer * -.12f, 81f));
            }
        var cloud = EscapeKit.Paint(new Color(1f, 1f, 1f), .05f);
        for (int i = 0; i < 10; i++)
        {
            var c = new GameObject("Nube").transform; c.SetParent(scenery, false);
            c.position = new Vector3(-20f + i * 9f, 9f + (i % 3) * 2.2f, 18f + (i % 2) * 6f);
            for (int b = 0; b < 4; b++) EscapeKit.Shape(PrimitiveType.Sphere, c, "Bola", new Vector3(b * 1.1f - 1.6f, (b % 2) * .5f, 0), Vector3.one * (1.6f + (b % 2) * .7f), cloud, false);
            parallax.Add((c, .12f, 90f));
        }
        EscapeKit.Shape(PrimitiveType.Sphere, scenery, "Sol", new Vector3(22f, 15f, 40f), Vector3.one * 5f, EscapeKit.Glow(new Color(1f, .9f, .5f), 2.4f), false);
        RenderSettings.ambientLight = new Color(.62f, .66f, .72f);
        // Towers pool.
        pipeRoot = new GameObject("Torres").transform;
        for (int i = 0; i < 6; i++) pipes.Add(BuildTower(i));
    }

    Transform BuildTower(int i)
    {
        var t = new GameObject("Torre").transform; t.SetParent(pipeRoot, false);
        Color[] cols = { new Color(.35f, .8f, .4f), new Color(.95f, .55f, .3f), new Color(.45f, .6f, 1f), new Color(.95f, .45f, .7f) };
        var body = EscapeKit.Paint(cols[i % cols.Length], .2f); var cap = EscapeKit.Paint(cols[i % cols.Length] * .8f, .25f);
        var stripe = EscapeKit.Paint(Color.white, .2f);
        foreach (string part in new[] { "Abajo", "Arriba" })
        {
            var p = new GameObject(part).transform; p.SetParent(t, false);
            EscapeKit.Block(p, "Tubo", new Vector3(0, 0, 0), new Vector3(PipeHalf * 2f - .2f, 1f, 5.5f), body, .08f, false);   // scaled in Y each frame
            EscapeKit.Block(p, "Boca", new Vector3(0, 0, 0), new Vector3(PipeHalf * 2f, .5f, 5.8f), cap, .08f, false);
            EscapeKit.Block(p, "Franja", new Vector3(0, 0, 0), new Vector3(PipeHalf * 2f + .02f, .12f, 5.82f), stripe, .03f, false);
        }
        t.position = new Vector3(0, 0, 2.2f);
        return t;
    }

    void LayoutTowers()
    {
        int first = Mathf.Max(0, Mathf.FloorToInt((ScrollNow - FirstPipe - 12f) / Spacing));
        for (int i = 0; i < pipes.Count; i++)
        {
            int k = first + i; var t = pipes[i];
            float x = PipeX(k), gy = GapY(k), half = GapSize(k) * .5f;
            t.position = new Vector3(x, 0, 2.2f);
            float bottomTop = gy - half, topBottom = gy + half, top = Ceiling + 4f;
            var lo = t.GetChild(0); var hi = t.GetChild(1);
            lo.GetChild(0).localPosition = new Vector3(0, bottomTop * .5f - .5f, 0); lo.GetChild(0).localScale = new Vector3(1, bottomTop + 1f, 1);
            lo.GetChild(1).localPosition = new Vector3(0, bottomTop - .25f, 0); lo.GetChild(2).localPosition = new Vector3(0, bottomTop - .6f, 0);
            hi.GetChild(0).localPosition = new Vector3(0, (topBottom + top) * .5f, 0); hi.GetChild(0).localScale = new Vector3(1, top - topBottom, 1);
            hi.GetChild(1).localPosition = new Vector3(0, topBottom + .25f, 0); hi.GetChild(2).localPosition = new Vector3(0, topBottom + .6f, 0);
        }
        float s = ScrollNow;
        foreach (var (tr, f, w) in parallax)
        {
            var p = tr.position;
            p.x -= (s - lastScroll) * f;
            if (p.x < -24f) p.x += w;
            tr.position = p;
        }
        lastScroll = s;
    }
    float lastScroll;

    // ------------------------------------------------------------------ loop

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        float dt = Mathf.Min(Time.deltaTime, MaxFrame);
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting: if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; } break;
                case Phase.Countdown:
                    float before = Countdown; Countdown -= dt;
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) FlapAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { State = Phase.Running; Elapsed = 0f; FlapAudio.UI("go", .8f); Net?.SendState(); }
                    break;
                case Phase.Running:
                    Elapsed += dt;
                    var done = Players.Where(p => p.Score >= Goal).OrderBy(p => p.ReachedAt).FirstOrDefault();
                    if (done != null) Finish(done);
                    else if (Players.All(p => p.Dead) || Elapsed >= RoundSeconds) Finish(Best());
                    break;
            }
        }
        else if (State == Phase.Running) Elapsed += dt;
        if (State == Phase.Running && !tenLeft && Remaining <= 10f) { tenLeft = true; Notify("¡Quedan 10 segundos!"); FlapAudio.UI("alarm", .6f); }
        LayoutTowers();
        if (trauma > 0f) { Cam.transform.position = CamHome + (Vector3)Random.insideUnitCircle * trauma * trauma * .3f; trauma = Mathf.Max(0f, trauma - Time.deltaTime * 2.5f); }
        if (Time.time > noticeUntil) Notice = "";
    }

    /// <summary>Highest score; on a tie, whoever is still flying, then whoever lasted longer.</summary>
    FlapPlayer Best() => Players.OrderByDescending(p => p.Score).ThenBy(p => p.Dead).ThenByDescending(p => p.DeadAt).ThenBy(p => p.Crashes).First();

    public void OnDied(FlapPlayer p)
    {
        if (p.IsHuman) { Notify("¡Te quedaste sin vidas! Tus puntos: " + p.Score); FlapAudio.UI("lose", .6f); }
        int alive = Players.Count(o => !o.Dead);
        if (alive > 0 && alive <= 2 && Players.Count > 2) Notify(alive == 1 ? "¡Solo queda uno en el aire!" : "¡Quedan dos en el aire!");
    }

    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));
    public void Notify(string text) { Notice = text; noticeUntil = Time.time + 3f; }
    public void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    void Finish(FlapPlayer w)
    {
        if (State == Phase.Finished) return;
        State = Phase.Finished; WinnerSlot = w != null ? w.Slot : -1;
        Result = w == null ? "SIN GANADOR" : w.Score >= Goal ? "¡" + w.DisplayName.ToUpperInvariant() + " LLEGA A " + Goal + "!" : "¡GANA " + w.DisplayName.ToUpperInvariant() + "! (" + w.Score + " PUNTOS)";
        Celebrate(); Net?.SendState();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { Players[WinnerSlot].Celebrating = true; MultiverseFX.Rainbow(Players[WinnerSlot].transform.position, 40, 8); }
        FlapAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); FlapAudio.FadeMusic(.15f); Paused = false;
    }

    public void ApplyNet(Phase phase, float countdown, float elapsed, int winnerSlot, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown)
        {
            if (Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) FlapAudio.UI("beep", .7f);
            Countdown = countdown; State = Phase.Countdown;
        }
        else if (phase == Phase.Running)
        {
            if (State != Phase.Running) { State = Phase.Running; Elapsed = elapsed; FlapAudio.UI("go", .8f); }
            float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .3f ? elapsed : Elapsed + diff * .25f;
        }
        else if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winnerSlot; Result = result; Celebrate(); PartyLink.Finish(-1); }
        else State = phase;
    }

    public List<FlapPlayer> Ranking() => Players.OrderByDescending(p => p.Slot == WinnerSlot).ThenByDescending(p => p.Score).ThenBy(p => p.ReachedAt).ThenBy(p => p.Dead).ThenByDescending(p => p.DeadAt).ToList();

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
