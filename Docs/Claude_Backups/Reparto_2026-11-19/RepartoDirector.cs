using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// REPARTO LOCO (2026-11-18) · party minigame for 2–6 players. Carry parcels across a busy street: from the shop
/// to the neighbourhood (+1), where you get another one to take back (+1)… Vehicles do not kill: they throw you
/// aside and the parcel breaks, so you must fetch a new one at either end. 90 s; most deliveries wins.
/// Traffic depends only on the seed and the clock (the same on every computer); each computer checks its own
/// players against it and tells the host about deliveries and accidents; the host keeps the score and the clock.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class RepartoDirector : MonoBehaviour
{
    public const float CountdownSeconds = 4f, RoundSeconds = RepartoRoad.RoundSeconds;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman, LogHits;

    public enum Phase { Waiting, Countdown, Playing, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float TimeLeft => Mathf.Max(0f, RoundSeconds - Elapsed);
    /// <summary>Clock of the traffic: runs during the countdown too, so the cars are already moving.</summary>
    public float TrafficTime => State == Phase.Playing || State == Phase.Finished ? Elapsed : -Countdown;
    public readonly List<RepartoPlayer> Players = new();
    public int[] Scores { get; private set; }
    public int[] Breaks { get; private set; }
    public float[] LastAt { get; private set; }   // time of the last delivery (tie-break: who got there first)
    public RepartoPlayer Human { get; private set; }
    public RepartoRoad Road { get; private set; }
    public RepartoCamera View { get; private set; }
    public RepartoNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string Notice { get; private set; } = ""; float noticeUntil;
    public string BigText { get; private set; } = ""; public Color BigColor { get; private set; } = Color.white; float bigUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    public bool CanMove => State == Phase.Playing;
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool InputFree => !Paused && CanMove && Human != null;
    public bool Dirty { get; set; }
    float nextHonk;

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 4211 + PartyLink.Round * 97) : Random.Range(1, 999999);
        RenderSettings.ambientLight = new Color(.66f, .66f, .7f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.78f, .87f, .97f); RenderSettings.fogStartDistance = 45f; RenderSettings.fogEndDistance = 110f;

        Road = gameObject.AddComponent<RepartoRoad>(); Road.Build(Seed);
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Scores = new int[count]; Breaks = new int[count]; LastAt = new float[count];
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name, typeof(CharacterController));
            go.transform.SetPositionAndRotation(SpawnFor(member, count), Quaternion.identity);
            var p = go.AddComponent<RepartoPlayer>(); bool puppet = PartyLink.IsPuppet(i);
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            p.Carry = 1;
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<RepartoBot>();
        }
        var cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        View = cam.GetComponent<RepartoCamera>(); if (View == null) View = cam.gameObject.AddComponent<RepartoCamera>(); View.Director = this;
        gameObject.AddComponent<RepartoHUD>().Director = this;
        RepartoAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<RepartoNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; RepartoAudio.Release(); }

    static Vector3 SpawnFor(int member, int count) => new Vector3(Mathf.Lerp(-9f, 9f, count <= 1 ? .5f : member / (float)(count - 1)), .1f, -2.6f);
    public Vector3 SpawnPoint(int slot) => SpawnFor(Players[slot].Member, Players.Count);

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting: if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; } break;
                case Phase.Countdown:
                    float before = Countdown; Countdown -= Time.deltaTime;
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) RepartoAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { Countdown = 0f; State = Phase.Playing; Elapsed = 0f; RepartoAudio.UI("go", .9f); Big("¡A REPARTIR!", new Color(1f, .85f, .4f)); Dirty = true; }
                    break;
                case Phase.Playing:
                    Elapsed += Time.deltaTime;
                    if (Elapsed >= RoundSeconds) Finish();
                    break;
            }
        }
        else if (State == Phase.Playing) Elapsed += Time.deltaTime;
        else if (State == Phase.Countdown) Countdown = Mathf.Max(0f, Countdown - Time.deltaTime);
        Road.Tick(TrafficTime);
        // A honk now and then from a lane near you.
        if (Time.time > nextHonk && Human != null) { nextHonk = Time.time + Random.Range(2.5f, 6f); if (RepartoRoad.LaneOf(Human.transform.position.z) >= 0 || Random.value < .3f) RepartoAudio.UI(Random.value < .5f ? "honk" : "honk2", .25f); }
        if (Time.time > noticeUntil) Notice = "";
        if (Time.time > bigUntil) BigText = "";
    }

    public void Notify(string text) { Notice = text; noticeUntil = Time.time + 2.6f; }
    void Big(string text, Color c, float seconds = 1.6f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    // ------------------------------------------------------------------ events from the owners

    public void OwnerDelivered(int slot)
    {
        var p = Players[slot];
        RepartoAudio.UI("deliver", p.IsHuman ? 1f : .3f);
        MultiverseFX.FloatText(p.transform.position + Vector3.up * 2.4f, "+1", p.Color, 1.4f);
        MultiverseFX.Burst(p.transform.position + Vector3.up * 2f, p.Color, 12, 4);
        if (p.IsHuman) Big("¡ENTREGADO! +1 · ¡llévate el siguiente!", new Color(.5f, 1f, .55f), 1.6f);
        if (IsClient) { Net?.SendEvent(p.Member, 1); return; }
        HostDelivered(slot); Net?.Broadcast(p.Member, 1);
    }

    public void OwnerHit(int slot, bool hadParcel)
    {
        var p = Players[slot];
        HitFx(p, hadParcel);
        if (IsClient) { Net?.SendEvent(p.Member, hadParcel ? 2 : 3); return; }
        HostHit(slot, hadParcel, false); Net?.Broadcast(p.Member, hadParcel ? 2 : 3);
    }

    void HitFx(RepartoPlayer p, bool hadParcel)
    {
        RepartoAudio.UI("crash", p.IsHuman ? 1f : .45f);
        SurvivalKit.Puff(p.transform.position + Vector3.up, new Color(.9f, .9f, .9f), 6, .35f);
        if (hadParcel)
        {
            MultiverseFX.Burst(p.transform.position + Vector3.up * 1.8f, new Color(.78f, .58f, .36f), 18, 6);
            MultiverseFX.FloatText(p.transform.position + Vector3.up * 2.4f, "¡PEDIDO ROTO!", new Color(1f, .5f, .3f), 1.1f);
        }
        if (p.IsHuman) { View?.Shake(.6f); if (hadParcel) Big("¡CRASH! El pedido se ha roto · coge otro", new Color(1f, .5f, .3f), 1.8f); else Notify("¡Cuidado con los coches!"); }
    }

    public void HostDelivered(int slot)
    {
        if (State != Phase.Playing) return;
        Scores[slot]++; LastAt[slot] = Elapsed; Dirty = true;
        FeedLine(Players[slot].DisplayName + " entrega un pedido (" + Scores[slot] + ")");
    }

    public void HostHit(int slot, bool hadParcel, bool remote)
    {
        if (remote) HitFx(Players[slot], hadParcel);
        if (hadParcel) { Breaks[slot]++; FeedLine("CRASH: a " + Players[slot].DisplayName + " se le rompe el pedido"); }
        Dirty = true;
    }

    /// <summary>Client: another player's delivery or crash, sent by the host (visual only).</summary>
    public void ShowRemote(int slot, int kind)
    {
        if (slot < 0 || slot >= Players.Count || !Players[slot].Puppet) return;
        var p = Players[slot];
        if (kind == 1) { MultiverseFX.FloatText(p.transform.position + Vector3.up * 2.4f, "+1", p.Color, 1.4f); RepartoAudio.UI("deliver", .3f); }
        else HitFx(p, kind == 2);
    }

    void Finish()
    {
        if (State == Phase.Finished) return;
        int best = -1;
        for (int s = 0; s < Players.Count; s++)
            if (best < 0 || Scores[s] > Scores[best] || (Scores[s] == Scores[best] && (Breaks[s] < Breaks[best] || (Breaks[s] == Breaks[best] && LastAt[s] < LastAt[best])))) best = s;
        State = Phase.Finished; WinnerSlot = best;
        Result = best >= 0 ? "¡" + Players[best].DisplayName.ToUpperInvariant() + " GANA CON " + Scores[best] + (Scores[best] == 1 ? " ENTREGA!" : " ENTREGAS!") : "SIN GANADOR";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { Players[WinnerSlot].Celebrating = true; MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2f, 40, 8); }
        RepartoAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); RepartoAudio.FadeMusic(.15f); Paused = false;
    }

    public List<RepartoPlayer> Ranking() => Players.OrderByDescending(p => Scores[p.Slot]).ThenBy(p => Breaks[p.Slot]).ThenBy(p => p.Slot == WinnerSlot ? 0 : 1).ToList();

    public void ApplyWorld(Phase phase, float countdown, float elapsed, int[] scores, int[] breaks, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) RepartoAudio.UI("beep", .7f);
        if (phase == Phase.Playing && State != Phase.Playing) { RepartoAudio.UI("go", .9f); Big("¡A REPARTIR!", new Color(1f, .85f, .4f)); Elapsed = elapsed; }
        if (phase == Phase.Countdown) { float d = countdown - Countdown; Countdown = Mathf.Abs(d) > .4f ? countdown : Countdown + d * .3f; }
        if (phase == Phase.Playing) { float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .4f ? elapsed : Elapsed + diff * .25f; Countdown = 0f; }
        for (int s = 0; s < Players.Count; s++)
        {
            if (scores[s] > Scores[s] && Players[s].Puppet) FeedLine(Players[s].DisplayName + " entrega un pedido (" + scores[s] + ")");
            Scores[s] = scores[s]; Breaks[s] = breaks[s];
        }
        if (phase == Phase.Finished) { State = Phase.Finished; Elapsed = elapsed; WinnerSlot = winner; Result = result; Celebrate(); PartyLink.Finish(-1); return; }
        State = phase;
    }

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
