using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// PATATA CALIENTE (2026-11-17) · party minigame for 2–6 players. One player holds a hot potato with a hidden fuse
/// (10–20 s); touching another player passes it (they freeze for a moment, and cannot pass it straight back). When it
/// blows up, whoever holds it is out. Round after round the fence closes in, until only one is left: the winner.
/// The host decides the fuse, who holds the potato and who is out; every computer moves its own players.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class PatataDirector : MonoBehaviour
{
    public const float CountdownSeconds = 4f, BoomSeconds = 3.2f, FuseMin = 10f, FuseMax = 20f, PassRange = 1.3f, NoBackSeconds = 1.1f, FirstPassDelay = 1f;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Playing, Boom, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public int Round { get; private set; }
    public int Rounds { get; private set; }
    public float RoundTime { get; private set; }
    public float PhaseTime { get; private set; }
    public int HolderSlot { get; private set; } = -1;
    public float Heat => State == Phase.Playing ? Mathf.Clamp01(RoundTime / FuseMax) : 0f;   // never tells the real fuse
    public readonly List<PatataPlayer> Players = new();
    public readonly List<int> Out = new();              // slots in the order they blew up
    public PatataPlayer Human { get; private set; }
    public PatataArena Arena { get; private set; }
    public PatataCamera View { get; private set; }
    public PatataNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string Notice { get; private set; } = ""; float noticeUntil;
    public string BigText { get; private set; } = ""; public Color BigColor { get; private set; } = Color.white; float bigUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    public bool CanMove => State == Phase.Playing || State == Phase.Boom;
    public int AliveCount => Players.Count(p => p.Alive);
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool InputFree => !Paused && CanMove && Human != null && Human.Alive;
    public bool Dirty { get; set; }
    public float Fuse => fuse;   // host only (tests)

    System.Random rng; float fuse, passReadyAt, noBackUntil; int noBackSlot = -1, pendingWinner = -1;
    PatataPotato potato;

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 6007 + PartyLink.Round * 271) : Random.Range(1, 999999);
        rng = new System.Random(Seed);
        RenderSettings.ambientLight = new Color(.64f, .62f, .66f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.75f, .85f, .97f); RenderSettings.fogStartDistance = 40f; RenderSettings.fogEndDistance = 95f;

        Arena = gameObject.AddComponent<PatataArena>(); Arena.Build();
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Rounds = count - 1;
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name, typeof(CharacterController));
            var at = SpawnPoint(i, count);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-new Vector3(at.x, 0, at.z)));
            var p = go.AddComponent<PatataPlayer>(); bool puppet = PartyLink.IsPuppet(i);
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<PatataBot>();
        }
        potato = PatataPotato.Create(this);

        var cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        View = cam.GetComponent<PatataCamera>(); if (View == null) View = cam.gameObject.AddComponent<PatataCamera>(); View.Director = this;
        gameObject.AddComponent<PatataHUD>().Director = this;
        PatataAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<PatataNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; PatataAudio.Release(); }

    public static Vector3 SpawnPoint(int slot, int count)
    {
        float a = (slot / (float)count * 360f + 180f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 8.5f + Vector3.up * .1f;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        float dt = Time.deltaTime;
        PhaseTime += dt;
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting: if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; PhaseTime = 0f; } break;
                case Phase.Countdown:
                    float before = Countdown; Countdown -= dt;
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) PatataAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { PatataAudio.UI("go", .8f); StartRound(); }
                    break;
                case Phase.Playing:
                    RoundTime += dt;
                    HostPasses();
                    if (RoundTime >= fuse && HolderSlot >= 0) HostBoom(HolderSlot);
                    break;
                case Phase.Boom:
                    if (PhaseTime >= BoomSeconds) { if (pendingWinner >= 0) Finish(pendingWinner); else StartRound(); }
                    break;
            }
        }
        else if (State == Phase.Playing) RoundTime += dt;
        if (Time.time > noticeUntil) Notice = "";
        if (Time.time > bigUntil) BigText = "";
    }

    public void Notify(string text) { Notice = text; noticeUntil = Time.time + 3f; }
    void Big(string text, Color c, float seconds = 1.6f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    // ------------------------------------------------------------------ host

    void StartRound()
    {
        var alive = Players.Where(p => p.Alive).ToList();
        int holder = alive[rng.Next(alive.Count)].Slot;
        fuse = Mathf.Lerp(FuseMin, FuseMax, (float)rng.NextDouble());
        State = Phase.Playing; PhaseTime = 0f; RoundTime = 0f; passReadyAt = FirstPassDelay; noBackSlot = -1;
        Arena.SnapTo(PatataArena.RadiusFor(Round, Rounds));
        SetHolder(holder, -1); Dirty = true;
    }

    void HostPasses()
    {
        if (HolderSlot < 0 || RoundTime < passReadyAt) return;
        var h = Players[HolderSlot]; if (!h.Alive) return;
        Vector3 hp = h.transform.position;
        PatataPlayer best = null; float bd = PassRange;
        foreach (var o in Players)
        {
            if (o == h || !o.Alive || (o.Slot == noBackSlot && RoundTime < noBackUntil)) continue;
            Vector3 d = o.transform.position - hp; if (Mathf.Abs(d.y) > 1.6f) continue; d.y = 0;
            if (d.magnitude < bd) { bd = d.magnitude; best = o; }
        }
        if (best == null) return;
        noBackSlot = HolderSlot; noBackUntil = RoundTime + NoBackSeconds; passReadyAt = RoundTime + .3f;
        SetHolder(best.Slot, HolderSlot); Dirty = true;
    }

    void HostBoom(int slot)
    {
        Out.Add(slot); BoomFx(slot);
        HolderSlot = -1;
        State = Phase.Boom; PhaseTime = 0f;
        var alive = Players.Where(p => p.Alive).ToList();
        if (alive.Count <= 1) pendingWinner = alive.Count == 1 ? alive[0].Slot : slot;
        else { Round++; Arena.ShrinkTo(PatataArena.RadiusFor(Round, Rounds)); }
        Dirty = true; Net?.SendWorld();
    }

    void Finish(int slot)
    {
        if (State == Phase.Finished) return;
        State = Phase.Finished; WinnerSlot = slot; PhaseTime = 0f;
        Result = slot >= 0 ? "¡" + Players[slot].DisplayName.ToUpperInvariant() + " SOBREVIVE A LA PATATA!" : "SIN GANADOR";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { Players[WinnerSlot].Celebrating = true; MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2f, 40, 8); }
        PatataAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); PatataAudio.FadeMusic(.15f); Paused = false;
        potato.Hide();
    }

    // ------------------------------------------------------------------ shared effects

    void SetHolder(int slot, int from)
    {
        HolderSlot = slot;
        if (slot < 0 || slot >= Players.Count) { potato.Hide(); return; }
        var p = Players[slot];
        potato.GiveTo(p, from >= 0 ? Players[from] : null);
        if (!p.Puppet && from >= 0) p.Receive();
        if (from >= 0)
        {
            PatataAudio.Play("pass", p.IsHuman || Players[from].IsHuman ? 1f : .5f);
            MultiverseFX.Burst(p.transform.position + Vector3.up * 1.8f, new Color(1f, .7f, .3f), 10, 4);
            FeedLine(Players[from].DisplayName + " → " + p.DisplayName);
            if (from == 0 && Human != null) Notify("¡Pasada! Corre, que no te la devuelvan");
        }
        else { PatataAudio.UI("bell", .8f); Big("RONDA " + (Round + 1) + " · ¡" + p.DisplayName.ToUpperInvariant() + " TIENE LA PATATA!", new Color(1f, .75f, .35f), 2.2f); }
        if (p.IsHuman) { Notify("¡TIENES LA PATATA! Toca a alguien para pasarla"); if (from >= 0) View?.Shake(.25f); }
    }

    void BoomFx(int slot)
    {
        var p = Players[slot]; var at = p.transform.position + Vector3.up * 1.6f;
        p.Explode(); potato.Hide();
        PatataAudio.UI("boom", 1f);
        MultiverseFX.Burst(at, new Color(1f, .55f, .2f), 40, 10); MultiverseFX.Burst(at, new Color(1f, .9f, .4f), 20, 6);
        MultiverseFX.Ring(at, 4f, new Color(1f, .6f, .2f), .6f);
        SurvivalKit.Puff(at, new Color(.35f, .33f, .32f), 9, .8f);
        MultiverseFX.FloatText(at + Vector3.up, "¡BOOM!", new Color(1f, .5f, .2f), 2f, 1.6f);
        View?.Shake(.9f);
        FeedLine("BOOM: " + p.DisplayName + " queda fuera");
        int left = Players.Count(o => o.Alive);
        Big(p.IsHuman ? "¡BOOM! ESTÁS FUERA" : "¡BOOM! " + p.DisplayName.ToUpperInvariant() + " FUERA", new Color(1f, .45f, .25f), 2.6f);
        if (left > 1 && slot != 0 && Human != null && Human.Alive) Notify("Quedáis " + left + " · la valla se cierra");
    }

    // ------------------------------------------------------------------ client

    public void ApplyWorld(Phase phase, float countdown, int round, float roundTime, int holder, bool[] alive, int winner, string result)
    {
        if (State == Phase.Finished) return;
        for (int s = 0; s < Players.Count && s < alive.Length; s++) if (Players[s].Alive && !alive[s]) { Out.Add(s); BoomFx(s); }
        if (round != Round) { Round = round; Arena.ShrinkTo(PatataArena.RadiusFor(Round, Rounds)); }
        if (phase == Phase.Countdown)
        {
            if (Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) PatataAudio.UI("beep", .7f);
            Countdown = countdown; State = Phase.Countdown;
        }
        else if (phase == Phase.Playing)
        {
            if (State != Phase.Playing) { if (State == Phase.Countdown) PatataAudio.UI("go", .8f); State = Phase.Playing; PhaseTime = 0f; RoundTime = roundTime; Arena.SnapTo(PatataArena.RadiusFor(Round, Rounds)); HolderSlot = -1; }
            float diff = roundTime - RoundTime; RoundTime = Mathf.Abs(diff) > .4f ? roundTime : RoundTime + diff * .25f;
        }
        else if (phase == Phase.Boom) { if (State != Phase.Boom) { State = Phase.Boom; PhaseTime = 0f; } }
        else if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winner; Result = result; Celebrate(); PartyLink.Finish(-1); return; }
        else State = phase;
        if (holder != HolderSlot && State == Phase.Playing && holder >= 0) SetHolder(holder, HolderSlot);
        else if (holder < 0 && HolderSlot >= 0) { HolderSlot = -1; potato.Hide(); }
    }

    public List<PatataPlayer> Ranking()
    {
        var list = new List<PatataPlayer>();
        if (WinnerSlot >= 0) list.Add(Players[WinnerSlot]);
        list.AddRange(Players.Where(p => p.Alive && p.Slot != WinnerSlot));
        for (int i = Out.Count - 1; i >= 0; i--) if (!list.Contains(Players[Out[i]])) list.Add(Players[Out[i]]);
        return list;
    }

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}

/// <summary>The hot potato: sits over the holder's head, flies to the next one, glows redder and ticks faster with time.</summary>
public sealed class PatataPotato : MonoBehaviour
{
    PatataDirector d; Transform body, spark; Material skin, sparkMat; Light glow;
    PatataPlayer holder; Vector3 flyFrom; float flyT = 1f, nextTick;

    public static PatataPotato Create(PatataDirector d)
    {
        var go = new GameObject("Patata"); var p = go.AddComponent<PatataPotato>(); p.d = d;
        p.skin = EscapeKit.Paint(new Color(.72f, .52f, .3f), .25f);
        p.body = EscapeKit.Shape(PrimitiveType.Sphere, go.transform, "Patata", Vector3.zero, new Vector3(.62f, .48f, .5f), p.skin, false).transform;
        var spot = EscapeKit.Paint(new Color(.5f, .35f, .2f), .1f);
        EscapeKit.Shape(PrimitiveType.Sphere, p.body, "Ojo", new Vector3(.32f, .2f, .3f), Vector3.one * .15f, spot, false);
        EscapeKit.Shape(PrimitiveType.Sphere, p.body, "Ojo", new Vector3(-.3f, -.15f, .35f), Vector3.one * .12f, spot, false);
        EscapeKit.Shape(PrimitiveType.Cylinder, go.transform, "Mecha", new Vector3(0, .32f, 0), new Vector3(.05f, .12f, .05f), EscapeKit.Paint(new Color(.2f, .2f, .2f)), false, new Vector3(0, 0, 15f));
        p.sparkMat = EscapeKit.Glow(new Color(1f, .8f, .3f), 4f);
        p.spark = EscapeKit.Shape(PrimitiveType.Sphere, go.transform, "Chispa", new Vector3(.04f, .46f, 0), Vector3.one * .14f, p.sparkMat, false).transform;
        p.glow = EscapeKit.PointLight(go.transform, Vector3.zero, new Color(1f, .5f, .2f), 4f, 0f);
        go.SetActive(false);
        return p;
    }

    public void GiveTo(PatataPlayer p, PatataPlayer from)
    {
        gameObject.SetActive(true);
        flyFrom = from != null ? from.Head.position : p.Head.position + Vector3.up * 3f; flyT = 0f;
        holder = p;
    }
    public void Hide() { holder = null; gameObject.SetActive(false); }

    void LateUpdate()
    {
        if (holder == null) return;
        float heat = d.Heat;
        flyT = Mathf.Min(1f, flyT + Time.deltaTime / .22f);
        Vector3 to = holder.Head.position;
        Vector3 pos = Vector3.Lerp(flyFrom, to, flyT) + Vector3.up * Mathf.Sin(flyT * Mathf.PI) * 1.2f;
        float shake = Mathf.Lerp(.01f, .09f, heat * heat);
        transform.position = pos + Random.insideUnitSphere * shake;
        transform.rotation = Quaternion.Euler(0, Time.time * 90f, Mathf.Sin(Time.time * 20f) * 10f * heat);
        float pulse = .5f + .5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 22f, heat));
        skin.color = Color.Lerp(new Color(.72f, .52f, .3f), new Color(1f, .25f, .12f), heat * (.6f + .4f * pulse));
        body.localScale = new Vector3(.62f, .48f, .5f) * (1f + heat * .18f * pulse);
        spark.localScale = Vector3.one * (.1f + .08f * Random.value);
        glow.intensity = heat * 3f * pulse;
        if (d.State == PatataDirector.Phase.Playing && Time.time >= nextTick)
        {
            nextTick = Time.time + Mathf.Lerp(.65f, .11f, heat);
            PatataAudio.Play("tick", holder.IsHuman ? .9f : .35f + heat * .3f);
        }
    }
}
