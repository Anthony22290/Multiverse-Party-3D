using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// RODILLOS DEL CALABOZO (2026-11-17) · party minigame for 2–6 players.
/// A round stone platform over a lava lake, with two spiked rollers sweeping around a central hub: the low fire
/// roller has to be JUMPED, the high ice roller has to be DUCKED. They speed up, start reversing, and the floor tiles
/// begin to crumble. Three lives each; falling into the lava is out. The last one standing wins the party point.
/// Built entirely at runtime from Resources (RodillosArt, RodillosAudio); the scene only holds this component.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class RodillosDirector : MonoBehaviour
{
    // Layout (metres) and rules.
    public const float Radius = 11f, HubRadius = 1.9f, InnerSplit = 6.4f, ArmStart = 1.3f, ArmEnd = 11.4f;
    public const float LowHeight = .55f, LowHitRadius = .4f, HighHeight = 1.75f, HighHitRadius = .5f;
    public const float HighArrives = 14f, ReverseFrom = 28f, CrumbleFrom = 50f, SuddenDeath = 120f, HardEnd = 210f;
    public const int Lives = 3;
    public const float CountdownSeconds = 4.5f, MaxFrame = .25f;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    /// <summary>Tests: a bot also plays the local slot.</summary>
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Running, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public readonly List<RodillosRunner> Runners = new();
    public RodillosRunner Human { get; private set; }
    public RodillosArena Arena { get; private set; }
    public RodillosSpin Spin { get; private set; }
    public RodillosCamera View { get; private set; }
    public RodillosHUD HUD { get; private set; }
    public RodillosNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string Notice { get; private set; } = "";
    public readonly List<string> Feed = new();
    public int AliveCount => Runners.Count(r => r.Alive);
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    public bool InputFree => !Paused && State == Phase.Running && Human != null && Human.Alive;
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    float noticeUntil, lastNoticeT = -1f;
    readonly Dictionary<RodillosRunner, int> places = new();

    void Start()
    {
        Application.runInBackground = true;
        Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 7919 + PartyLink.Round * 104729) : Random.Range(1, 999999);
        Spin = new RodillosSpin(Seed);
        Arena = gameObject.AddComponent<RodillosArena>();
        Arena.Build(this, Seed);

        RenderSettings.ambientLight = new Color(.4f, .27f, .24f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.12f, .05f, .04f); RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 30f; RenderSettings.fogEndDistance = 75f;

        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            int spot = PartyLink.Active ? Mathf.Max(0, member) : i;   // same place for each player on every computer
            float a = 30f + spot * (360f / Mathf.Max(count, 6)) + 15f;
            var pos = Quaternion.Euler(0, a, 0) * Vector3.right * 7.6f;
            var go = new GameObject(i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1));
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-pos));
            var r = go.AddComponent<RodillosRunner>();
            bool puppet = PartyLink.IsPuppet(i);
            r.Init(this, i, member, go.name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 7717 + 3));
            Runners.Add(r);
            if (i == 0) Human = r;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<RodillosBot>();
        }

        var cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.08f, .03f, .03f);
        View = cam.gameObject.GetComponent<RodillosCamera>(); if (View == null) View = cam.gameObject.AddComponent<RodillosCamera>(); View.Director = this;
        HUD = gameObject.AddComponent<RodillosHUD>(); HUD.Director = this;
        RodillosAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<RodillosNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; RodillosAudio.Release(); }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        float dt = Mathf.Min(Time.deltaTime, MaxFrame);   // same cap as the players' simulation
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting:
                    if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; }
                    break;
                case Phase.Countdown:
                    float before = Countdown; Countdown -= dt;
                    CountdownSounds(before, Countdown);
                    if (Countdown <= 0f) Go();
                    break;
                case Phase.Running:
                    Elapsed += dt;
                    CheckEnd();
                    break;
            }
        }
        else if (State == Phase.Running) Elapsed += dt;   // clients: smooth local clock, corrected by the host

        Milestones();
        Spin.Advance(Elapsed);
        Arena.Tick(Elapsed, Spin, State == Phase.Running);
        if (Time.time > noticeUntil) Notice = "";
    }

    void CountdownSounds(float before, float now)
    {
        if (Mathf.CeilToInt(before) != Mathf.CeilToInt(now) && now > 0f && now <= 3f) RodillosAudio.UI("beep", .7f);
    }

    void Go() { State = Phase.Running; Elapsed = 0f; RodillosAudio.UI("go", .8f); Net?.SendState(); }

    /// <summary>Same notices on every computer: they only depend on the shared clock.</summary>
    void Milestones()
    {
        if (State != Phase.Running) { lastNoticeT = Elapsed; return; }
        float a = lastNoticeT, b = Elapsed; lastNoticeT = b;
        bool Crossed(float t) => a < t && b >= t;
        if (Crossed(HighArrives - 3f)) { Notify("¡Sube el RODILLO DE HIELO! Agáchate con Shift"); RodillosAudio.UI("alarm", .6f); }
        if (Crossed(ReverseFrom - 1.5f)) Notify("¡Cuidado! Ahora los rodillos pueden cambiar de sentido");
        if (Crossed(CrumbleFrom - 2f)) { Notify("¡El suelo empieza a romperse! Huye de las losas rojas"); RodillosAudio.UI("alarm", .6f); }
        if (Crossed(SuddenDeath)) { Notify("¡MUERTE SÚBITA! Más rápido… y los rodillos se juntan"); RodillosAudio.UI("alarm", .9f); }
        if (Spin.FlippedBetween(a, b)) { RodillosAudio.UI("reverse", .55f); if (b < ReverseFrom + 20f) Notify("¡Cambio de sentido!"); }
    }

    public void Notify(string text) { Notice = text; noticeUntil = Time.time + 3.5f; }
    void FeedLine(string text) { Feed.Insert(0, text); if (Feed.Count > 5) Feed.RemoveAt(5); }

    // ------------------------------------------------------------------ events from the runners

    public void OnHit(RodillosRunner r, int arm)
    {
        if (r.Lives > 0) FeedLine(r.DisplayName + (arm == RodillosSpin.High ? " no se agachó" : arm == RodillosSpin.Low ? " no saltó" : " recibió un golpe") + " · " + r.Lives + " vida" + (r.Lives == 1 ? "" : "s"));
    }

    public void OnEliminated(RodillosRunner r, bool lava)
    {
        places[r] = Runners.Count(o => o.Alive) + 1;
        FeedLine(r.DisplayName + (lava ? " cayó a la lava" : " salió volando") + " · ¡fuera!");
        if (r.IsHuman) RodillosAudio.UI("lose", .7f);
        if (!IsClient) CheckEnd();
    }

    public int PlaceOf(RodillosRunner r) => places.TryGetValue(r, out var p) ? p : Runners.Count(o => o.Alive) + 1;

    void CheckEnd()
    {
        if (State != Phase.Running) return;
        int alive = AliveCount;
        if (alive <= 1 && Runners.Count > 1)
        {
            var last = Runners.FirstOrDefault(r => r.Alive);
            if (last == null)
            {   // Everybody went out on the same frame: the one who lasted longer (fewer hits) takes it.
                last = Runners.OrderByDescending(r => r.EliminatedAt).ThenBy(r => r.Hits).First();
            }
            Finish(last);
        }
        else if (Elapsed >= HardEnd)
        {
            var best = Runners.Where(r => r.Alive).OrderByDescending(r => r.Lives).ThenBy(r => r.Hits).ThenBy(_ => Random.value).First();
            Finish(best);
        }
    }

    void Finish(RodillosRunner winner)
    {
        State = Phase.Finished;
        WinnerSlot = winner != null ? Runners.IndexOf(winner) : -1;
        Result = winner != null ? "¡GANA " + winner.DisplayName.ToUpperInvariant() + "!" : "SIN GANADOR";
        Celebrate();
        Net?.SendState();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Runners.Count)
        {
            var w = Runners[WinnerSlot]; w.Celebrating = true;
            MultiverseFX.Rainbow(w.transform.position + Vector3.up * 1.5f, 40, 8);
        }
        bool won = WinnerSlot == 0;
        RodillosAudio.UI(won ? "win" : "lose", .9f);
        RodillosAudio.FadeMusic(.18f);
        Paused = false;
    }

    /// <summary>Client: the host's clock and result.</summary>
    public void ApplyNet(Phase phase, float countdown, float elapsed, int winnerSlot, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown) { CountdownSounds(Countdown, countdown); Countdown = countdown; State = Phase.Countdown; }
        else if (phase == Phase.Running)
        {
            if (State != Phase.Running) { State = Phase.Running; Elapsed = elapsed; RodillosAudio.UI("go", .8f); }
            float diff = elapsed - Elapsed;
            Elapsed = Mathf.Abs(diff) > .35f ? elapsed : Elapsed + diff * .25f;
        }
        else if (phase == Phase.Finished)
        {
            State = Phase.Finished; Elapsed = Mathf.Max(Elapsed, elapsed);
            WinnerSlot = winnerSlot; Result = result;
            Celebrate();
            PartyLink.Finish(-1);
        }
        else State = phase;
    }

    /// <summary>Places for the results screen: still standing first, then whoever lasted longer.</summary>
    public List<RodillosRunner> Ranking() =>
        Runners.OrderByDescending(r => Runners.IndexOf(r) == WinnerSlot).ThenByDescending(r => r.Alive).ThenByDescending(r => r.Alive ? r.Lives : 0).ThenByDescending(r => r.EliminatedAt).ToList();

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
