using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// GALLINAS LOCAS (2026-11-18) · party minigame for 2–6 players on a farm. Every player has a pen round the yard.
/// Chickens run away from whoever comes near: herd them into your pen, or grab one (E / click) and drop it inside.
/// Golden chickens are worth 5. Some chickens are traps: they look normal (their eyes flash red now and then) and,
/// once in a pen, tick for 2 s and blow up every chicken in it — unless someone grabs them out in time.
/// 90 s; the points are the chickens in your pen at the end. The host moves the chickens and decides everything;
/// every computer moves its own farmers.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class GranjaDirector : MonoBehaviour
{
    public const float CountdownSeconds = 4f, RoundSeconds = 90f, TrapFuse = 2.2f, GrabRange = 1.5f, FleeRadius = 5f, GoldFleeRadius = 6.5f,
        FleeSpeed = 4.3f, GoldFleeSpeed = 5.2f, WanderSpeed = 1.1f, SpawnEvery = 3.2f;
    public static readonly float[] GoldTimes = { 18f, 45f, 70f };

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Playing, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float TimeLeft => Mathf.Max(0f, RoundSeconds - Elapsed);
    public readonly List<GranjaPlayer> Players = new();
    public readonly List<GranjaChicken> Chickens = new();
    public int[] Scores { get; private set; }           // by slot
    public int[] Golds { get; private set; }            // golden chickens in the pen, by slot
    public int[] Booms { get; private set; }            // times the pen blew up, by slot
    public GranjaPlayer Human { get; private set; }
    public GranjaArena Arena { get; private set; }
    public GranjaCamera View { get; private set; }
    public GranjaNet Net { get; private set; }
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
    public int BoomSeq { get; private set; } public int LastBoomPen { get; private set; } = -1;
    public int[] KnockSeq { get; private set; }

    System.Random rng; int nextId = 1, goldIndex, trapsLeft, spawned; float nextSpawn; readonly Dictionary<int, float> grabCooldown = new();

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 7013 + PartyLink.Round * 389) : Random.Range(1, 999999);
        rng = new System.Random(Seed);
        RenderSettings.ambientLight = new Color(.66f, .64f, .62f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.78f, .87f, .97f); RenderSettings.fogStartDistance = 45f; RenderSettings.fogEndDistance = 110f;

        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Arena = gameObject.AddComponent<GranjaArena>(); Arena.Build(count);
        Scores = new int[count]; Golds = new int[count]; Booms = new int[count]; KnockSeq = new int[count];
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            int pen = Mathf.Clamp(member, 0, count - 1);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var color = i < roster.Colors.Length ? roster.Colors[i] : Color.white;
            var go = new GameObject(name, typeof(CharacterController));
            var at = Arena.Spawn(pen);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-new Vector3(at.x, 0, at.z)));
            var p = go.AddComponent<GranjaPlayer>(); bool puppet = PartyLink.IsPuppet(i);
            p.Init(this, i, member, name, color, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            p.Pen = pen; Arena.SetOwner(pen, name, color);
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<GranjaBot>();
        }
        if (!PartyLink.Active || PartyLink.IsHost)
        {
            // Traps in the whole round: 2 with 2–4 players, up to 4 with 6 (one hidden from the start, the rest later).
            trapsLeft = Mathf.Clamp(count - 2, 2, 4) - 1;
            int start = 8 + count * 3;
            for (int i = 0; i < start; i++) SpawnChicken(i == start / 2 ? GranjaChicken.Kind.Trap : GranjaChicken.Kind.Normal, RandomYardPoint());
        }

        var cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        View = cam.GetComponent<GranjaCamera>(); if (View == null) View = cam.gameObject.AddComponent<GranjaCamera>(); View.Director = this;
        gameObject.AddComponent<GranjaHUD>().Director = this;
        GranjaAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<GranjaNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; GranjaAudio.Release(); }

    public GranjaPlayer PlayerOfPen(int pen) => Players.FirstOrDefault(p => p.Pen == pen);
    public GranjaChicken CarriedBy(int slot) { foreach (var c in Chickens) if (c.Mode == GranjaChicken.State.Carried && c.Carrier == slot) return c; return null; }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        float dt = Mathf.Min(Time.deltaTime, .05f);
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting: if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; } break;
                case Phase.Countdown:
                    float before = Countdown; Countdown -= Time.deltaTime;
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) GranjaAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { State = Phase.Playing; Elapsed = 0f; nextSpawn = SpawnEvery; GranjaAudio.UI("go", .9f); Big("¡A POR LAS GALLINAS!", new Color(1f, .85f, .4f)); Dirty = true; }
                    break;
                case Phase.Playing:
                    Elapsed += Time.deltaTime;
                    HostSpawns();
                    HostKnocks();
                    if (Elapsed >= RoundSeconds) Finish();
                    break;
            }
            if (State == Phase.Playing || State == Phase.Countdown) SimulateChickens(dt);
            Recount();
        }
        else if (State == Phase.Playing) Elapsed += Time.deltaTime;
        if (Time.time > noticeUntil) Notice = "";
        if (Time.time > bigUntil) BigText = "";
        // Ambient clucks.
        if (Random.value < Time.deltaTime * .7f && Chickens.Count > 0) GranjaAudio.UI(Random.value < .5f ? "cluck" : "cluck2", .12f);
    }

    public void Notify(string text) { Notice = text; noticeUntil = Time.time + 3f; }
    void Big(string text, Color c, float seconds = 1.8f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    // ------------------------------------------------------------------ host: chickens

    Vector3 RandomYardPoint()
    {
        for (int tries = 0; tries < 30; tries++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Lerp(4.5f, 12.5f, (float)rng.NextDouble());
            var p = new Vector3(Mathf.Sin(a) * r, .05f, Mathf.Cos(a) * r);
            if (!Physics.CheckSphere(p + Vector3.up * .4f, .45f, ~0, QueryTriggerInteraction.Ignore)) return p;
        }
        return Arena.BarnDoor;
    }

    GranjaChicken SpawnChicken(GranjaChicken.Kind kind, Vector3 at)
    {
        var c = GranjaChicken.Create(nextId++, kind, at, true);
        c.WanderDir = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0) * Vector3.forward;
        c.transform.rotation = Quaternion.LookRotation(c.WanderDir);
        Chickens.Add(c); spawned++; Dirty = true;
        return c;
    }

    void HostSpawns()
    {
        if (goldIndex < GoldTimes.Length && Elapsed >= GoldTimes[goldIndex])
        {
            goldIndex++;
            var g = SpawnChicken(GranjaChicken.Kind.Gold, Arena.BarnDoor + Vector3.up * .05f);
            g.Vel = Vector3.forward * 3f; AnnounceGold(g);
        }
        int free = Chickens.Count(c => c.Mode == GranjaChicken.State.Free || c.Mode == GranjaChicken.State.Carried);
        int cap = 8 + Players.Count * 3;
        if (Elapsed >= nextSpawn)
        {
            nextSpawn = Elapsed + SpawnEvery;
            if (free < cap)
            {
                bool trap = trapsLeft > 0 && Elapsed > 10f && rng.NextDouble() < .22;
                if (trap) trapsLeft--;
                var c = SpawnChicken(trap ? GranjaChicken.Kind.Trap : GranjaChicken.Kind.Normal, rng.NextDouble() < .5 ? Arena.BarnDoor + Vector3.up * .05f : RandomYardPoint());
                if ((c.transform.position - Arena.BarnDoor).sqrMagnitude < 1f) c.Vel = Vector3.forward * 2.5f;
            }
        }
    }

    void AnnounceGold(GranjaChicken g)
    {
        GranjaAudio.UI("gold", .9f); MultiverseFX.Burst(g.transform.position + Vector3.up, new Color(1f, .85f, .3f), 25, 6);
        Big("¡GALLINA DORADA! (5 puntos)", new Color(1f, .82f, .3f), 2.2f);
    }

    void SimulateChickens(float dt)
    {
        var live = Players;
        for (int i = Chickens.Count - 1; i >= 0; i--)
        {
            var c = Chickens[i];
            switch (c.Mode)
            {
                case GranjaChicken.State.Carried:
                {
                    var p = c.Carrier >= 0 && c.Carrier < Players.Count ? Players[c.Carrier] : null;
                    if (p == null) { Release(c, c.transform.position); break; }
                    c.transform.position = p.Head.position; c.transform.rotation = p.transform.rotation; c.Vel = p.Velocity;
                    if (c.Type == GranjaChicken.Kind.Trap && c.Pen >= 0) { c.Fuse -= dt; if (c.Fuse <= 0f) ExplodeCarried(c, p); }
                    break;
                }
                case GranjaChicken.State.Free: SteerFree(c, dt); break;
                case GranjaChicken.State.Penned:
                case GranjaChicken.State.Ticking:
                    SteerPen(c, dt);
                    if (c.Mode == GranjaChicken.State.Ticking)
                    {
                        float before = c.Fuse; c.Fuse -= dt;
                        if (Mathf.Floor(before * 4f) != Mathf.Floor(c.Fuse * 4f)) GranjaAudio.UI("tick", .5f);
                        if (c.Fuse <= 0f) { ExplodePen(c.Pen); return; }
                    }
                    break;
            }
        }
    }

    void SteerFree(GranjaChicken c, float dt)
    {
        Vector3 pos = c.transform.position; Vector3 v = Vector3.zero; float panic = 0f;
        bool gold = c.Type == GranjaChicken.Kind.Gold; float radius = gold ? GoldFleeRadius : FleeRadius;
        foreach (var p in Players)
        {
            Vector3 d = pos - p.transform.position; d.y = 0; float dist = d.magnitude;
            if (dist < radius && dist > .01f) { float w = (radius - dist) / radius; v += d / dist * w * 2f; panic = Mathf.Max(panic, w); }
        }
        if (Time.time > c.NextWander) { c.NextWander = Time.time + .8f + (float)rng.NextDouble() * 2.2f; c.WanderDir = rng.NextDouble() < .25 ? Vector3.zero : Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0) * Vector3.forward; }
        if (panic < .05f)
        {
            v += c.WanderDir;
            // Calm chickens keep away from the pens (so pens only fill by herding or carrying).
            for (int i = 0; i < Arena.Pens; i++) { Vector3 d = pos - Arena.PenCenter[i]; d.y = 0; if (d.magnitude < 6.5f) v += d.normalized * (6.5f - d.magnitude) * .35f; }
        }
        foreach (var o in Chickens)
        {
            if (o == c || o.Mode != GranjaChicken.State.Free) continue;
            Vector3 d = pos - o.transform.position; d.y = 0; float m = d.magnitude;
            if (m < .7f && m > .01f) v += d / m * (.7f - m) * 3f;
        }
        Vector3 flat = new Vector3(pos.x, 0, pos.z); float r = flat.magnitude;
        if (r > GranjaArena.FieldRadius - 2.5f) v += -flat / r * (r - (GranjaArena.FieldRadius - 2.5f)) * 1.5f;
        float speed = Mathf.Lerp(WanderSpeed, gold ? GoldFleeSpeed : FleeSpeed, Mathf.Clamp01(panic * 1.8f));
        Vector3 want = v.sqrMagnitude > .0001f ? Vector3.ClampMagnitude(v, 1f) * speed : Vector3.zero;
        if (v.sqrMagnitude > .0001f && v.magnitude < 1f && panic > .05f) want = v.normalized * speed;
        Vector3 hv = Vector3.MoveTowards(new Vector3(c.Vel.x, 0, c.Vel.z), want, 14f * dt);
        c.Vel.x = hv.x; c.Vel.z = hv.z;
        c.Vel.y = c.Cc.isGrounded ? -1f : c.Vel.y - 20f * dt;
        if (c.Cc.enabled) c.Cc.Move(c.Vel * dt);
        if (hv.sqrMagnitude > .05f) c.transform.rotation = Quaternion.Slerp(c.transform.rotation, Quaternion.LookRotation(hv), dt * 10f);
        if (c.transform.position.y < -2f) { c.Cc.enabled = false; c.transform.position = RandomYardPoint(); c.Cc.enabled = true; }
        int pen = Arena.PenAt(c.transform.position);
        if (pen >= 0) Pen(c, pen);
    }

    void SteerPen(GranjaChicken c, float dt)
    {
        Vector3 pos = c.transform.position;
        if (Time.time > c.NextWander)
        {
            c.NextWander = Time.time + 1f + (float)rng.NextDouble() * 2f;
            var target = Arena.FromPen(c.Pen, new Vector3(((float)rng.NextDouble() - .5f) * 3.8f, 0, -.4f - (float)rng.NextDouble() * 1.9f));
            c.WanderDir = target - pos; c.WanderDir.y = 0;
        }
        Vector3 want = c.WanderDir.magnitude > .3f ? c.WanderDir.normalized * .9f : Vector3.zero;
        if (c.Mode == GranjaChicken.State.Ticking) want = Quaternion.Euler(0, Time.time * 400f, 0) * Vector3.forward * 1.5f;
        Vector3 hv = Vector3.MoveTowards(new Vector3(c.Vel.x, 0, c.Vel.z), want, 8f * dt);
        c.Vel = new Vector3(hv.x, c.Cc.isGrounded ? -1f : c.Vel.y - 20f * dt, hv.z);
        if (c.Cc.enabled) c.Cc.Move(c.Vel * dt);
        c.WanderDir -= hv * dt;
        var clamped = Arena.ClampInside(c.Pen, c.transform.position);
        if ((clamped - c.transform.position).sqrMagnitude > .0001f) { c.Cc.enabled = false; c.transform.position = new Vector3(clamped.x, c.transform.position.y, clamped.z); c.Cc.enabled = true; }
        if (hv.sqrMagnitude > .05f) c.transform.rotation = Quaternion.Slerp(c.transform.rotation, Quaternion.LookRotation(hv), dt * 8f);
    }

    void Pen(GranjaChicken c, int pen)
    {
        c.Pen = pen; c.Carrier = -1; c.NextWander = 0f;
        if (c.Cc != null) c.Cc.enabled = true;
        var owner = PlayerOfPen(pen);
        if (c.Type == GranjaChicken.Kind.Trap)
        {
            c.Mode = GranjaChicken.State.Ticking; c.Fuse = TrapFuse;
            GranjaAudio.UI("alarm", owner != null && owner.IsHuman ? 1f : .6f);
            FeedLine("¡Una gallina trampa en el corral de " + (owner != null ? owner.DisplayName : "?") + "!");
            if (owner != null && owner.IsHuman) Big("¡GALLINA TRAMPA! ¡SÁCALA (E)!", new Color(1f, .35f, .25f), 2f);
        }
        else
        {
            c.Mode = GranjaChicken.State.Penned;
            GranjaAudio.UI(c.Type == GranjaChicken.Kind.Gold ? "gold" : "pen", owner != null && owner.IsHuman ? .9f : .4f);
            MultiverseFX.FloatText(c.transform.position + Vector3.up * 1.2f, "+" + c.Value, owner != null ? owner.Color : Color.white, c.Type == GranjaChicken.Kind.Gold ? 1.6f : 1f);
            if (c.Type == GranjaChicken.Kind.Gold) FeedLine("¡" + (owner != null ? owner.DisplayName : "?") + " mete la gallina dorada!");
        }
        Dirty = true;
    }

    void Release(GranjaChicken c, Vector3 at)
    {
        c.Carrier = -1;
        c.Cc.enabled = false; c.transform.position = at; c.Cc.enabled = true;
        int pen = Arena.PenAt(at);
        if (pen >= 0) Pen(c, pen);
        else { c.Mode = GranjaChicken.State.Free; c.Pen = -1; if (c.Type == GranjaChicken.Kind.Trap) c.Fuse = 0f; }
        Dirty = true;
    }

    // ------------------------------------------------------------------ host: grabbing, dashing, explosions

    /// <summary>Owner of the slot pressed grab: host applies it, a client asks the host.</summary>
    public void RequestGrab(int slot)
    {
        if (State != Phase.Playing) return;
        if (IsClient) { Net?.SendGrab(Players[slot].Member); return; }
        HostGrab(slot);
    }

    public void HostGrab(int slot)
    {
        if (State != Phase.Playing || slot < 0 || slot >= Players.Count) return;
        if (grabCooldown.TryGetValue(slot, out var until) && Time.time < until) return;
        grabCooldown[slot] = Time.time + .25f;
        var p = Players[slot];
        var held = CarriedBy(slot);
        if (held != null)
        {
            // Drop in front: over a pen fence it lands inside.
            Vector3 fwd = p.transform.forward; fwd.y = 0; fwd = fwd.sqrMagnitude > .01f ? fwd.normalized : Vector3.forward;
            Vector3 at = p.transform.position + fwd * 1.35f + Vector3.up * .15f;
            int pen = -1; for (int i = 0; i < Arena.Pens; i++) if (Arena.Inside(i, p.transform.position + fwd * 1.9f, .1f) || Arena.Inside(i, at, .1f)) { pen = i; break; }
            if (pen >= 0) at = Arena.ClampInside(pen, at);
            else if (Physics.CheckSphere(at + Vector3.up * .3f, .25f, ~0, QueryTriggerInteraction.Ignore)) at = p.transform.position + Vector3.up * .15f;
            Release(held, at); held.Vel = fwd * 2f;
            GranjaAudio.Play("drop", p.IsHuman ? .8f : .25f);
            return;
        }
        GranjaChicken best = null; float bd = GrabRange;
        foreach (var c in Chickens)
        {
            bool ok = c.Mode == GranjaChicken.State.Free || c.Mode == GranjaChicken.State.Ticking;
            if (!ok) continue;
            Vector3 d = c.transform.position - p.transform.position; if (Mathf.Abs(d.y) > 1.6f) continue; d.y = 0;
            float m = d.magnitude; if (m < bd) { bd = m; best = c; }
        }
        if (best == null) return;
        bool ticking = best.Mode == GranjaChicken.State.Ticking;
        best.Mode = GranjaChicken.State.Carried; best.Carrier = slot; best.Cc.enabled = false;
        if (!ticking) best.Pen = -1;   // a ticking trap keeps ticking (Pen ≥ 0) while carried
        GranjaAudio.Play("grab", p.IsHuman ? 1f : .3f);
        if (ticking && p.IsHuman) Notify("¡Tírala fuera de tu corral, rápido!");
        Dirty = true;
    }

    void HostKnocks()
    {
        foreach (var a in Players)
        {
            if (!a.Dashing) continue;
            foreach (var b in Players)
            {
                if (a == b) continue;
                Vector3 d = b.transform.position - a.transform.position; d.y = 0;
                if (d.magnitude > 1.25f) continue;
                var held = CarriedBy(b.Slot); if (held == null) continue;
                Release(held, b.transform.position + d.normalized * .9f + Vector3.up * .2f);
                held.Vel = d.normalized * 3f;
                KnockSeq[b.Slot]++;
                if (!b.Puppet) b.Knock(d.normalized * 5f);
                GranjaAudio.Play("bump", a.IsHuman || b.IsHuman ? 1f : .4f);
                SurvivalKit.Puff(b.transform.position + Vector3.up, new Color(.95f, .95f, .95f), 6, .3f);
                if (b.IsHuman) Notify("¡" + a.DisplayName + " te ha hecho soltar la gallina!");
                Dirty = true;
            }
        }
    }

    void ExplodeCarried(GranjaChicken c, GranjaPlayer p)
    {
        Kill(c, true);
        if (!p.Puppet) p.Knock(Vector3.zero);
        KnockSeq[p.Slot]++;
        BoomFx(p.transform.position + Vector3.up * 1.5f, -1, p);
        BoomSeq++; LastBoomPen = -1 - p.Slot; Dirty = true;
    }

    void ExplodePen(int pen)
    {
        foreach (var c in Chickens.ToList()) if ((c.Mode == GranjaChicken.State.Penned || c.Mode == GranjaChicken.State.Ticking) && c.Pen == pen) Kill(c, false);
        var owner = PlayerOfPen(pen); if (owner != null) Booms[owner.Slot]++;
        BoomFx(Arena.PenCenter[pen] + Vector3.up, pen, owner);
        BoomSeq++; LastBoomPen = pen; Dirty = true; Net?.SendWorld();
    }

    void Kill(GranjaChicken c, bool quiet)
    {
        c.Mode = GranjaChicken.State.Dead; Chickens.Remove(c);
        SurvivalKit.Puff(c.transform.position + Vector3.up * .3f, new Color(.98f, .96f, .92f), quiet ? 3 : 4, .25f);
        Destroy(c.gameObject);
    }

    /// <summary>Explosion effects (every computer): pen ≥ 0 = a pen blew up; pen &lt; 0 = a trap blew up in someone's hands.</summary>
    public void BoomFx(Vector3 at, int pen, GranjaPlayer owner)
    {
        GranjaAudio.UI("boom", 1f);
        MultiverseFX.Burst(at, new Color(1f, .55f, .2f), 40, 9); MultiverseFX.Burst(at, new Color(1f, 1f, 1f), 30, 7);
        MultiverseFX.Ring(at, 4f, new Color(1f, .6f, .2f), .6f);
        SurvivalKit.Puff(at, new Color(.35f, .33f, .32f), 9, .8f);
        MultiverseFX.FloatText(at + Vector3.up, "¡BOOM!", new Color(1f, .5f, .2f), 2f, 1.6f);
        bool mine = owner != null && owner.IsHuman;
        View?.Shake(mine ? 1f : .45f);
        if (pen >= 0)
        {
            FeedLine("BOOM: el corral de " + (owner != null ? owner.DisplayName : "?") + " explota");
            Big(mine ? "¡BOOM! ¡TU CORRAL HA EXPLOTADO!" : "¡BOOM! Explota el corral de " + (owner != null ? owner.DisplayName : "?"), new Color(1f, .45f, .25f), 2.4f);
        }
        else if (owner != null) { FeedLine("BOOM: a " + owner.DisplayName + " le explota en las manos"); if (mine) Big("¡TE HA EXPLOTADO EN LAS MANOS!", new Color(1f, .45f, .25f), 2f); }
    }

    void Recount()
    {
        System.Array.Clear(Scores, 0, Scores.Length); System.Array.Clear(Golds, 0, Golds.Length);
        foreach (var c in Chickens)
        {
            if (c.Mode != GranjaChicken.State.Penned && c.Mode != GranjaChicken.State.Ticking) continue;
            var o = PlayerOfPen(c.Pen); if (o == null) continue;
            Scores[o.Slot] += c.Value; if (c.Type == GranjaChicken.Kind.Gold) Golds[o.Slot]++;
        }
    }

    void Finish()
    {
        if (State == Phase.Finished) return;
        Recount();
        int best = -1;
        for (int s = 0; s < Players.Count; s++)
            if (best < 0 || Scores[s] > Scores[best] || (Scores[s] == Scores[best] && Golds[s] > Golds[best])) best = s;
        State = Phase.Finished; WinnerSlot = best;
        Result = best >= 0 ? "¡" + Players[best].DisplayName.ToUpperInvariant() + " GANA CON " + Scores[best] + " PUNTOS!" : "SIN GANADOR";
        foreach (var c in Chickens) if (c.Mode == GranjaChicken.State.Carried) { c.Mode = GranjaChicken.State.Free; c.Carrier = -1; }
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { Players[WinnerSlot].Celebrating = true; MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2f, 40, 8); }
        GranjaAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); GranjaAudio.FadeMusic(.15f); Paused = false;
    }

    public List<GranjaPlayer> Ranking() => Players.OrderByDescending(p => Scores[p.Slot]).ThenByDescending(p => Golds[p.Slot]).ThenBy(p => p.Slot == WinnerSlot ? 0 : 1).ToList();

    // ------------------------------------------------------------------ client

    readonly Dictionary<int, GranjaChicken> byId = new();
    public void ApplyWorld(Phase phase, float countdown, float elapsed, int[] scores, int[] golds, int[] booms, int[] knocks, int boomSeq, int boomPen, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) GranjaAudio.UI("beep", .7f);
        if (phase == Phase.Playing && State != Phase.Playing) { GranjaAudio.UI("go", .9f); Big("¡A POR LAS GALLINAS!", new Color(1f, .85f, .4f)); }
        Countdown = countdown;
        if (phase == Phase.Playing) { float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .5f ? elapsed : Elapsed + diff * .25f; }
        for (int s = 0; s < Players.Count; s++) { Scores[s] = scores[s]; Golds[s] = golds[s]; Booms[s] = booms[s]; if (knocks[s] != KnockSeq[s]) { if (knocks[s] > KnockSeq[s] && !Players[s].Puppet) Players[s].Knock(Vector3.zero); KnockSeq[s] = knocks[s]; } }
        if (boomSeq != BoomSeq)
        {
            BoomSeq = boomSeq; LastBoomPen = boomPen;
            if (boomPen >= 0) BoomFx(Arena.PenCenter[boomPen] + Vector3.up, boomPen, PlayerOfPen(boomPen));
            else { int slot = -1 - boomPen; if (slot >= 0 && slot < Players.Count) BoomFx(Players[slot].transform.position + Vector3.up * 1.5f, -1, Players[slot]); }
        }
        if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winner; Result = result; Celebrate(); PartyLink.Finish(-1); return; }
        State = phase;
    }

    /// <summary>Client: the host's list of chickens (missing ones died).</summary>
    public void ApplyChickens(List<(int id, GranjaChicken.Kind kind, GranjaChicken.State mode, int pen, int carrierSlot, Vector3 pos, float yaw)> list)
    {
        var seen = new HashSet<int>();
        foreach (var e in list)
        {
            seen.Add(e.id);
            if (!byId.TryGetValue(e.id, out var c) || c == null)
            {
                c = GranjaChicken.Create(e.id, e.kind, e.pos, false); byId[e.id] = c; Chickens.Add(c);
                if (e.kind == GranjaChicken.Kind.Gold && State == Phase.Playing) AnnounceGold(c);
            }
            var before = c.Mode;
            c.SetNet(e.mode == GranjaChicken.State.Carried && e.carrierSlot >= 0 && e.carrierSlot < Players.Count ? Players[e.carrierSlot].Head.position : e.pos, e.yaw, e.mode, e.pen, e.carrierSlot);
            if (before != e.mode && (e.mode == GranjaChicken.State.Penned || e.mode == GranjaChicken.State.Ticking))
            {
                var owner = PlayerOfPen(e.pen);
                if (e.mode == GranjaChicken.State.Ticking) { GranjaAudio.UI("alarm", owner != null && owner.IsHuman ? 1f : .6f); if (owner != null && owner.IsHuman) Big("¡GALLINA TRAMPA! ¡SÁCALA (E)!", new Color(1f, .35f, .25f), 2f); }
                else { GranjaAudio.UI(e.kind == GranjaChicken.Kind.Gold ? "gold" : "pen", owner != null && owner.IsHuman ? .9f : .4f); MultiverseFX.FloatText(e.pos + Vector3.up * 1.2f, "+" + c.Value, owner != null ? owner.Color : Color.white, e.kind == GranjaChicken.Kind.Gold ? 1.6f : 1f); }
            }
            if (before != e.mode && e.mode == GranjaChicken.State.Carried && e.carrierSlot >= 0 && e.carrierSlot < Players.Count) GranjaAudio.Play("grab", Players[e.carrierSlot].IsHuman ? 1f : .3f);
        }
        foreach (var id in byId.Keys.ToList())
        {
            if (seen.Contains(id)) continue;
            var c = byId[id]; byId.Remove(id);
            if (c != null) { Chickens.Remove(c); SurvivalKit.Puff(c.transform.position + Vector3.up * .3f, new Color(.98f, .96f, .92f), 4, .25f); Destroy(c.gameObject); }
        }
    }

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
