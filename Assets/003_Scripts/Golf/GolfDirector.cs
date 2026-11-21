using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// MINIGOLF LOCO (2026-11-17) · party minigame for 2–6 players over ten holes, everybody playing the same hole at the
/// same time (balls pass through each other). When the first ball drops, a one-minute countdown starts for the rest.
/// Fifteen strokes is the limit: your ball vanishes and the hole counts 15 + 2. If the minute runs out, your strokes
/// so far + 2. Lowest total after ten holes wins. The host keeps the clock and the score card.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class GolfDirector : MonoBehaviour
{
    public const float CountdownSeconds = 4f, IntroSeconds = 3f, HoleEndSeconds = 4.5f, AfterFirst = 60f, HoleLimit = 240f;
    public const int Penalty = 2, Holes = 10;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Intro, Playing, HoleEnd, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public int HoleIndex { get; private set; } = -1;
    public GolfCourse.Hole CurrentHole => Course != null && HoleIndex >= 0 && HoleIndex < Course.Holes.Count ? Course.Holes[HoleIndex] : null;
    public float PhaseTime { get; private set; }       // seconds in the current phase
    public float HoleClock => State == Phase.Playing ? PhaseTime : 0f;
    public float FirstHoledAt { get; private set; } = -1f;
    public float CountdownLeft => FirstHoledAt < 0f ? -1f : Mathf.Max(0f, AfterFirst - (PhaseTime - FirstHoledAt));
    public readonly List<GolfPlayer> Players = new();
    public GolfPlayer Human { get; private set; }
    public GolfCourse Course { get; private set; }
    public GolfCamera View { get; private set; }
    public GolfNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public int[,] Card { get; private set; }            // [slot, hole] strokes (−1 = not yet)
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string Notice { get; private set; } = ""; float noticeUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool InputFree => !Paused && State == Phase.Playing && Human != null;
    public int Total(int slot) { int t = 0; for (int h = 0; h < Holes; h++) if (Card[slot, h] > 0) t += Card[slot, h]; return t; }
    public int ParSoFar(int upTo) { int t = 0; for (int h = 0; h <= upTo && h < Holes; h++) t += GolfCourse.Layouts[h].par; return t; }

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 433 + PartyLink.Round) : Random.Range(1, 999999);
        RenderSettings.ambientLight = new Color(.62f, .66f, .7f);
        Physics.defaultSolverIterations = Mathf.Max(Physics.defaultSolverIterations, 10);
        Course = new GameObject("Campo de minigolf").AddComponent<GolfCourse>(); Course.Build();
        Decorate();

        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Card = new int[count, Holes];
        for (int s = 0; s < count; s++) for (int h = 0; h < Holes; h++) Card[s, h] = -1;
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name);
            var p = go.AddComponent<GolfPlayer>(); bool puppet = PartyLink.IsPuppet(i);
            p.Init(this, PartyLink.Active ? Mathf.Max(0, member) % 6 : i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 911 + 2));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<GolfBot>();
        }
        // Balls go through each other.
        for (int a = 0; a < Players.Count; a++) for (int b = a + 1; b < Players.Count; b++)
            Physics.IgnoreCollision(Players[a].Ball.GetComponent<Collider>(), Players[b].Ball.GetComponent<Collider>());

        var cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        View = cam.GetComponent<GolfCamera>(); if (View == null) View = cam.gameObject.AddComponent<GolfCamera>(); View.Director = this;
        gameObject.AddComponent<GolfHUD>().Director = this;
        GolfAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<GolfNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; GolfAudio.Release(); }

    void Decorate()
    {
        var root = new GameObject("Decoración").transform;
        var rng = new System.Random(5);
        var trunk = EscapeKit.Paint(new Color(.45f, .3f, .18f)); var leaves = new[] { EscapeKit.Paint(new Color(.3f, .62f, .28f)), EscapeKit.Paint(new Color(.42f, .72f, .32f)) };
        for (int i = 0; i < 40; i++)
        {
            float x = -14f + (float)rng.NextDouble() * (GolfCourse.Layouts.Length * 18f + 10f), z = (float)rng.NextDouble() < .5f ? -6f - (float)rng.NextDouble() * 8f : 19f + (float)rng.NextDouble() * 10f;
            float h = 1.4f + (float)rng.NextDouble() * 1.6f;
            EscapeKit.Block(root, "Tronco", new Vector3(x, -1.4f + h * .5f, z), new Vector3(.35f, h, .35f), trunk, .04f, false);
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Copa", new Vector3(x, -1.4f + h + .6f, z), Vector3.one * (1.4f + (float)rng.NextDouble()), leaves[i % 2], false);
        }
        for (int i = 0; i < Holes; i++)
        {
            var h = Course.Holes[i];
            var sp = new Vector3(h.Area.center.x, 0, h.Area.min.z - 3.5f);
            var sign = EscapeKit.Text(root, (i + 1) + ". " + h.Name.ToUpperInvariant() + "\npar " + h.Par, sp + new Vector3(0, 3.2f, -.05f), Vector3.zero, .045f, new Color(1f, .95f, .7f));
            EscapeKit.Block(root, "Cartel", sp + new Vector3(0, 3.2f, 0), new Vector3(3.4f, 1.2f, .08f), EscapeKit.Paint(new Color(.35f, .22f, .14f)), .03f, false);
            EscapeKit.Block(root, "Poste", sp + new Vector3(0, 1.1f, .05f), new Vector3(.12f, 3.4f, .12f), EscapeKit.Paint(new Color(.35f, .22f, .14f)), .02f, false);
        }
        var sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .4f; sun.transform.rotation = Quaternion.Euler(60f, 30f, 0); sun.shadows = LightShadows.None;
    }

    // ------------------------------------------------------------------ loop

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame && (Human == null || Human.State != GolfPlayer.Phase.Charging)) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        float dt = Mathf.Min(Time.deltaTime, .1f);
        PhaseTime += dt;
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting: if (Net == null || PartyLink.AllReady) { PhaseTime = CountdownSeconds - 1f; BeginHole(0); } break;
                case Phase.Intro: if (PhaseTime >= IntroSeconds) BeginPlay(); break;
                case Phase.Playing: HostScore(); break;
                case Phase.HoleEnd: if (PhaseTime >= HoleEndSeconds) { if (HoleIndex + 1 < Holes) BeginHole(HoleIndex + 1); else Finish(); } break;
            }
        }
        Course.Tick(HoleIndex, HoleClock);
        if (Time.time > noticeUntil) Notice = "";
    }

    public void Notify(string t) { Notice = t; noticeUntil = Time.time + 3f; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    void BeginHole(int index)
    {
        HoleIndex = index; State = Phase.Intro; PhaseTime = 0f; FirstHoledAt = -1f;
        foreach (var p in Players) { p.ResetNet(); if (!p.Puppet) p.StartHole(CurrentHole); else p.StartHolePuppet(CurrentHole); }
        GolfAudio.UI("intro", .7f);
        Net?.SendState();
    }

    void BeginPlay() { State = Phase.Playing; PhaseTime = 0f; GolfAudio.UI("go", .7f); Net?.SendState(); }

    /// <summary>Host: write down holed / given-up balls, start the minute, close the hole.</summary>
    void HostScore()
    {
        for (int s = 0; s < Players.Count; s++)
        {
            var p = Players[s]; if (Card[s, HoleIndex] >= 0) continue;
            if (p.State == GolfPlayer.Phase.Holed) Card[s, HoleIndex] = Mathf.Max(1, p.Strokes);
            else if (p.State == GolfPlayer.Phase.Out) Card[s, HoleIndex] = (int)GolfPlayer.MaxStrokes + Penalty;
        }
        bool allDone = true; for (int s = 0; s < Players.Count; s++) if (Card[s, HoleIndex] < 0) allDone = false;
        bool timeUp = (FirstHoledAt >= 0f && CountdownLeft <= 0f) || PhaseTime >= HoleLimit;
        if (!allDone && !timeUp) return;
        for (int s = 0; s < Players.Count; s++)
            if (Card[s, HoleIndex] < 0) { Card[s, HoleIndex] = Mathf.Min((int)GolfPlayer.MaxStrokes + Penalty, Players[s].Strokes + Penalty); FeedLine(Players[s].DisplayName + ": se acabó el tiempo (+" + Penalty + ")"); }
        EndHole();
    }

    void EndHole()
    {
        State = Phase.HoleEnd; PhaseTime = 0f;
        foreach (var p in Players) p.Freeze();
        GolfAudio.UI("end", .7f);
        Net?.SendState();
    }

    public void OnHoled(GolfPlayer p, string word)
    {
        if (FirstHoledAt < 0f && State == Phase.Playing)
        {
            FirstHoledAt = PhaseTime;
            Notify(p.DisplayName + " ha embocado · ¡queda 1 minuto!");
            GolfAudio.UI("alarm", .5f);
        }
        FeedLine(p.DisplayName + " embocó en " + p.Strokes + (string.IsNullOrEmpty(word) ? "" : " · " + word));
    }
    public void OnGaveUp(GolfPlayer p) => FeedLine(p.DisplayName + ": 15 golpes · cuenta " + ((int)GolfPlayer.MaxStrokes + Penalty));

    void Finish()
    {
        State = Phase.Finished; PhaseTime = 0f;
        var rank = Ranking(); var w = rank[0];
        WinnerSlot = Players.IndexOf(w);
        Result = "¡GANA " + w.DisplayName.ToUpperInvariant() + " CON " + Total(WinnerSlot) + " GOLPES!";
        Celebrate(); Net?.SendState();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { var w = Players[WinnerSlot]; w.Celebrating = true; w.ShowForResults(true); }
        GolfAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); GolfAudio.FadeMusic(.15f); Paused = false;
    }

    public List<GolfPlayer> Ranking()
    {
        return Players.OrderBy(p => Total(Players.IndexOf(p)))
                      .ThenByDescending(p => Enumerable.Range(0, Holes).Count(h => Card[Players.IndexOf(p), h] > 0 && Card[Players.IndexOf(p), h] <= GolfCourse.Layouts[h].par))
                      .ThenBy(p => Players.IndexOf(p)).ToList();
    }

    // ------------------------------------------------------------------ client

    public void ApplyNet(Phase phase, int hole, float phaseTime, float firstHoledAt, int[] card, int winnerSlot, string result)
    {
        if (card != null) for (int s = 0; s < Players.Count; s++) for (int h = 0; h < Holes; h++) Card[s, h] = card[s * Holes + h];
        if (State == Phase.Finished) return;
        if (hole != HoleIndex && hole >= 0 && hole < Holes)
        {
            HoleIndex = hole; FirstHoledAt = -1f;
            foreach (var p in Players) { p.ResetNet(); if (!p.Puppet) p.StartHole(CurrentHole); else p.StartHolePuppet(CurrentHole); }
            GolfAudio.UI("intro", .7f);
        }
        if (phase != State)
        {
            if (phase == Phase.Playing) GolfAudio.UI("go", .7f);
            if (phase == Phase.HoleEnd) { foreach (var p in Players) p.Freeze(); GolfAudio.UI("end", .7f); }
            if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winnerSlot; Result = result; Celebrate(); PartyLink.Finish(-1); return; }
            State = phase;
        }
        if (Mathf.Abs(PhaseTime - phaseTime) > .4f) PhaseTime = phaseTime; else PhaseTime += (phaseTime - PhaseTime) * .25f;
        FirstHoledAt = firstHoledAt;
    }

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
