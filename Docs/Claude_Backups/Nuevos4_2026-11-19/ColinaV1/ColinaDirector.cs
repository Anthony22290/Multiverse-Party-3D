using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// REY DE LA COLINA (2026-11-19) · party minigame for 2–6 players on a round island. A glowing crown zone moves to a
/// new spot every 15 s (the same on every computer: it depends only on the seed and the clock). Standing in it
/// scores points every second — double if you are alone in it. Dash into rivals to throw them out (or into the
/// water: you come back 2.5 s later); bananas make you slip. 90 s, most points wins.
/// Each computer moves its own players and handles their bananas and falls; the host scores, knocks and keeps time.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class ColinaDirector : MonoBehaviour, IMoverHost
{
    public const float CountdownSeconds = 4f, RoundSeconds = 90f, ZoneEvery = 15f, ZoneRadius = 3f, IslandRadius = 13f, BananaEvery = 6f, RespawnSeconds = 2.5f;
    public const int MaxBananas = 4;
    public static readonly Vector3[] Spots =
    {
        new Vector3(0, 0, 0), new Vector3(6.5f, .9f, 2f), new Vector3(-6f, 0, 5f), new Vector3(-4.5f, .9f, -6f), new Vector3(5f, 0, -6.5f), new Vector3(0, 1.8f, 8.2f), new Vector3(8.2f, 0, -1.5f), new Vector3(-8.2f, .9f, -1f)
    };

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Playing, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float TimeLeft => Mathf.Max(0f, RoundSeconds - Elapsed);
    public readonly List<PartyMover> Players = new();
    public float[] Points { get; private set; }
    public int[] KnockSeq { get; private set; }
    public PartyMover Human { get; private set; }
    public ColinaNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string BigText { get; private set; } = ""; public Color BigColor { get; private set; } = Color.white; float bigUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    public bool CanMove => State == Phase.Playing;
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool InputFree => !Paused && CanMove && Human != null && !Human.Out;
    public float CameraYaw => 0f;
    public bool Dirty { get; set; }

    public sealed class Banana { public int Id; public Vector3 Pos; public Transform T; }
    public readonly List<Banana> Bananas = new();
    System.Random rng; float nextBanana; int nextBananaId = 1; int shownZone = -1;
    Transform zoneRing, zoneBeam, crown; Light zoneLight; Camera cam; Vector3 focus; bool snap = true; float trauma;
    readonly Dictionary<PartyMover, float> respawnAt = new();

    /// <summary>Which spot the zone is on at time t (deterministic, never the same twice in a row).</summary>
    public int ZoneIndex(float t)
    {
        int k = Mathf.Max(0, Mathf.FloorToInt(t / ZoneEvery));
        int prev = 0, idx = 0;
        for (int i = 0; i <= k; i++) { var r = new System.Random(unchecked(Seed * 31 + i * 977)); idx = i == 0 ? 0 : (prev + 1 + r.Next(Spots.Length - 1)) % Spots.Length; prev = idx; }
        return idx;
    }
    public Vector3 ZonePos => Spots[ZoneIndex(State == Phase.Playing || State == Phase.Finished ? Elapsed : 0f)];
    public float ZoneTimeLeft => ZoneEvery - Mathf.Repeat(Elapsed, ZoneEvery);
    public bool InZone(PartyMover p) { if (p.Out) return false; var d = p.transform.position - ZonePos; return Mathf.Abs(d.y) < 1.6f && new Vector2(d.x, d.z).magnitude < ZoneRadius; }

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 2909 + PartyLink.Round * 23) : Random.Range(1, 999999);
        rng = new System.Random(Seed);
        RenderSettings.ambientLight = new Color(.62f, .66f, .72f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.65f, .85f, 1f); RenderSettings.fogStartDistance = 40f; RenderSettings.fogEndDistance = 110f;
        BuildWorld();
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Points = new float[count]; KnockSeq = new int[count];
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name, typeof(CharacterController));
            var at = SpawnPoint(member, count);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-new Vector3(at.x, 0, at.z)));
            var p = go.AddComponent<PartyMover>(); bool puppet = PartyLink.IsPuppet(i);
            p.DashSpeed = 16f; p.DashCooldown = 1.8f;
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) { go.AddComponent<ColinaBot>(); p.BotControlled = true; }
        }
        cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .8f, 1f); cam.fieldOfView = 52f; cam.farClipPlane = 220f;
        gameObject.AddComponent<ColinaHUD>().Director = this;
        ColinaAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<ColinaNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; ColinaAudio.Release(); }
    public static Vector3 SpawnPoint(int member, int count) { float a = (member / (float)count * 360f + 15f) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * 10f, .2f, Mathf.Sin(a) * 10f); }

    void BuildWorld()
    {
        var root = new GameObject("Isla").transform;
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Mar", new Vector3(0, -2.2f, 0), new Vector3(160, .2f, 160), EscapeKit.Paint(new Color(.25f, .6f, .9f), .85f), false);
        // Island: walkable cylinder (capsule collider replaced by a mesh collider) + sand rim.
        var island = EscapeKit.Shape(PrimitiveType.Cylinder, root, "Isla", new Vector3(0, -1f, 0), new Vector3(IslandRadius * 2f, 1f, IslandRadius * 2f), EscapeKit.Paint(new Color(.5f, .78f, .38f), .08f), false);
        var mc = island.AddComponent<MeshCollider>(); mc.sharedMesh = island.GetComponent<MeshFilter>().sharedMesh; mc.convex = true;
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Arena", new Vector3(0, -1.05f, 0), new Vector3(IslandRadius * 2f + 1.2f, .98f, IslandRadius * 2f + 1.2f), EscapeKit.Paint(new Color(.95f, .85f, .6f), .05f), false);
        // Raised platforms (some zone spots are on top of them) with ramps.
        var stone = EscapeKit.Paint(new Color(.82f, .78f, .7f), .15f); var top = EscapeKit.Paint(new Color(.6f, .85f, .45f), .1f);
        void Mesa(Vector3 at, float h, float r, Vector3 rampDir)
        {
            var m = EscapeKit.Shape(PrimitiveType.Cylinder, root, "Meseta", at + Vector3.up * (h * .5f), new Vector3(r * 2f, h * .5f, r * 2f), stone, false);
            var c = m.AddComponent<MeshCollider>(); c.sharedMesh = m.GetComponent<MeshFilter>().sharedMesh; c.convex = true;
            EscapeKit.Shape(PrimitiveType.Cylinder, root, "Césped meseta", at + Vector3.up * (h + .01f), new Vector3(r * 2f - .1f, .02f, r * 2f - .1f), top, false);
            float len = h / Mathf.Sin(22f * Mathf.Deg2Rad);
            var dir = rampDir.normalized; var start = at + dir * r;
            var ramp = EscapeKit.Block(root, "Rampa", start + dir * (len * .5f * Mathf.Cos(22f * Mathf.Deg2Rad)) + Vector3.up * (h * .5f - .1f), new Vector3(2.2f, .2f, len + .4f), stone, .05f, true);
            ramp.transform.rotation = Quaternion.LookRotation(-dir) * Quaternion.Euler(-22f, 0, 0);
        }
        Mesa(new Vector3(6.5f, 0, 2f), .9f, 2.6f, new Vector3(-1f, 0, -.3f));
        Mesa(new Vector3(-4.5f, 0, -6f), .9f, 2.6f, new Vector3(.4f, 0, 1f));
        Mesa(new Vector3(-8.2f, 0, -1f), .9f, 2.2f, new Vector3(1f, 0, .5f));
        Mesa(new Vector3(0, 0, 8.2f), 1.8f, 2.4f, new Vector3(0, 0, -1f));
        // Palm trees and rocks for flavour.
        var trunk = EscapeKit.Paint(new Color(.6f, .45f, .28f), .1f); var leaf = EscapeKit.Paint(new Color(.25f, .65f, .3f), .1f);
        foreach (var p in new[] { new Vector3(10.5f, 0, 6f), new Vector3(-10.8f, 0, 5.5f), new Vector3(9f, 0, -8f), new Vector3(-9.5f, 0, -8f), new Vector3(2f, 0, -11f) })
        {
            EscapeKit.Block(root, "Palmera", p + Vector3.up * 2f, new Vector3(.35f, 4f, .35f), trunk, .05f, true, new Vector3(0, 0, 6f));
            for (int k = 0; k < 5; k++) EscapeKit.Block(root, "Hoja", p + new Vector3(0, 4.1f, 0), new Vector3(.4f, .08f, 2.2f), leaf, .02f, false, new Vector3(-20f, k * 72f, 0));
        }
        // The zone: glowing ring, light beam and a floating crown.
        zoneRing = EscapeKit.Shape(PrimitiveType.Cylinder, null, "Zona", Vector3.zero, new Vector3(ZoneRadius * 2f, .02f, ZoneRadius * 2f), MultiverseFX.Glow(new Color(1f, .85f, .2f), .45f, 2f, true), false).transform;
        zoneBeam = EscapeKit.Shape(PrimitiveType.Cylinder, null, "Haz", Vector3.zero, new Vector3(ZoneRadius * 2f, 6f, ZoneRadius * 2f), MultiverseFX.Glow(new Color(1f, .9f, .4f), .08f, 1.5f), false).transform;
        crown = new GameObject("Corona").transform;
        var gold = EscapeKit.Glow(new Color(1f, .78f, .2f), 1.4f);
        EscapeKit.Shape(PrimitiveType.Cylinder, crown, "Aro", Vector3.zero, new Vector3(1f, .15f, 1f), gold, false);
        for (int k = 0; k < 5; k++) { float a = k * 72f * Mathf.Deg2Rad; EscapeKit.Block(crown, "Pico", new Vector3(Mathf.Sin(a) * .42f, .3f, Mathf.Cos(a) * .42f), new Vector3(.14f, .45f, .14f), gold, .02f, false); }
        zoneLight = EscapeKit.PointLight(crown, Vector3.down * 2f, new Color(1f, .85f, .4f), 9f, 2.5f);
        var sun = new GameObject("Luz").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.05f; sun.transform.rotation = Quaternion.Euler(52f, 40f, 0); sun.shadows = LightShadows.Soft;
    }

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
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) ColinaAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { Countdown = 0f; State = Phase.Playing; Elapsed = 0f; nextBanana = 4f; ColinaAudio.UI("go", .9f); Big("¡A LA CORONA!", new Color(1f, .85f, .4f)); Dirty = true; }
                    break;
                case Phase.Playing:
                    Elapsed += Time.deltaTime;
                    HostScore(Time.deltaTime); HostBananas(); HostKnocks();
                    if (Elapsed >= RoundSeconds) Finish();
                    break;
            }
        }
        else if (State == Phase.Playing) Elapsed += Time.deltaTime;
        if (State == Phase.Playing) OwnerRules();
        PresentZone();
        if (Time.time > bigUntil) BigText = "";
    }

    void PresentZone()
    {
        var z = ZonePos;
        int idx = ZoneIndex(State == Phase.Playing ? Elapsed : 0f);
        if (idx != shownZone)
        {
            if (shownZone >= 0 && State == Phase.Playing) { ColinaAudio.UI("move", .8f); Big("¡LA CORONA SE MUEVE!", new Color(1f, .85f, .4f), 1.3f); MultiverseFX.Burst(z + Vector3.up * 2f, new Color(1f, .85f, .3f), 25, 6); }
            shownZone = idx;
        }
        float pulse = .5f + .5f * Mathf.Sin(Time.time * 4f);
        bool warn = State == Phase.Playing && ZoneTimeLeft < 3f;
        zoneRing.position = z + Vector3.up * .04f; zoneRing.localScale = new Vector3(ZoneRadius * 2f, .02f, ZoneRadius * 2f) * (1f + (warn ? .06f * Mathf.Sin(Time.time * 20f) : 0f));
        zoneBeam.position = z + Vector3.up * 6f;
        crown.position = z + Vector3.up * (4.2f + pulse * .3f); crown.rotation = Quaternion.Euler(0, Time.time * 60f, 0);
        zoneLight.intensity = warn ? (Mathf.Repeat(Time.time * 6f, 1f) > .5f ? 3f : .5f) : 2.5f;
    }

    // ------------------------------------------------------------------ owners

    void OwnerRules()
    {
        foreach (var p in Players)
        {
            if (p.Puppet) continue;
            if (p.Out)
            {
                if (respawnAt.TryGetValue(p, out var at) && Time.time >= at) { respawnAt.Remove(p); p.Revive(SpawnPoint(p.Member, Players.Count) + Vector3.up * .5f); SurvivalKit.Puff(p.transform.position + Vector3.up, Color.white, 5, .4f); }
                continue;
            }
            if (p.transform.position.y < -1.8f)
            {
                p.SetOut(); respawnAt[p] = Time.time + RespawnSeconds;
                ColinaAudio.UI("splash", p.IsHuman ? 1f : .4f);
                MultiverseFX.Burst(new Vector3(p.transform.position.x, -2f, p.transform.position.z), new Color(.6f, .85f, 1f), 18, 5);
                if (p.IsHuman) Big("¡AL AGUA! Vuelves en un momento", new Color(.6f, .85f, 1f), 1.6f);
                FeedLine(p.DisplayName + " cae al agua");
                continue;
            }
            foreach (var b in Bananas)
            {
                Vector3 d = b.Pos - p.transform.position; d.y = 0;
                if (d.magnitude < .7f && p.Grounded && !p.Slipping)
                {
                    p.Slip(1.3f); ColinaAudio.UI("slip", p.IsHuman ? 1f : .4f);
                    if (IsClient) Net?.SendBanana(b.Id); else HostRemoveBanana(b.Id);
                    break;
                }
            }
        }
    }

    // ------------------------------------------------------------------ host

    void HostScore(float dt)
    {
        int inside = 0; foreach (var p in Players) if (InZone(p)) inside++;
        foreach (var p in Players)
        {
            if (!InZone(p)) continue;
            float before = Points[p.Slot];
            Points[p.Slot] += dt * (inside == 1 ? 2f : 1f);
            if (Mathf.FloorToInt(before) != Mathf.FloorToInt(Points[p.Slot]) && p.IsHuman) ColinaAudio.Play("tick", .25f);
        }
    }

    void HostBananas()
    {
        if (Elapsed < nextBanana) return;
        nextBanana = Elapsed + BananaEvery;
        if (Bananas.Count >= MaxBananas) return;
        for (int tries = 0; tries < 20; tries++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 2f + (float)rng.NextDouble() * 9f;
            var p = new Vector3(Mathf.Cos(a) * r, 5f, Mathf.Sin(a) * r);
            if (Physics.Raycast(p, Vector3.down, out var hit, 10f)) { AddBanana(nextBananaId++, hit.point); Dirty = true; return; }
        }
    }

    public void AddBanana(int id, Vector3 pos)
    {
        if (Bananas.Exists(b => b.Id == id)) return;
        var root = new GameObject("Plátano " + id).transform; root.position = pos;
        var yellow = EscapeKit.Paint(new Color(1f, .88f, .25f), .4f);
        for (int i = -2; i <= 2; i++) EscapeKit.Shape(PrimitiveType.Sphere, root, "Piel", new Vector3(i * .1f, .06f + Mathf.Abs(i) * .03f, Mathf.Abs(i) * .04f), new Vector3(.16f, .1f, .16f), yellow, false);
        Bananas.Add(new Banana { Id = id, Pos = pos, T = root });
    }

    public void HostRemoveBanana(int id) { RemoveBanana(id); Dirty = true; }
    public void RemoveBanana(int id)
    {
        int i = Bananas.FindIndex(b => b.Id == id); if (i < 0) return;
        if (Bananas[i].T != null) Destroy(Bananas[i].T.gameObject);
        Bananas.RemoveAt(i);
    }

    void HostKnocks()
    {
        foreach (var a in Players)
        {
            if (!a.Dashing || a.Out) continue;
            foreach (var b in Players)
            {
                if (a == b || b.Out || b.Stunned) continue;
                Vector3 d = b.transform.position - a.transform.position; d.y = 0;
                if (d.magnitude > 1.25f) continue;
                KnockSeq[b.Slot]++;
                var push = d.normalized * 10f + Vector3.up * 4f;
                if (!b.Puppet) b.Knock(push, .7f); else { b.ShowStun(.7f); pendingKnocks[b.Slot] = push; }
                ColinaAudio.UI("bump", a.IsHuman || b.IsHuman ? 1f : .4f);
                SurvivalKit.Puff(b.transform.position + Vector3.up, a.Color, 5, .3f);
                Dirty = true;
            }
        }
    }
    public readonly Dictionary<int, Vector3> pendingKnocks = new();

    public void ApplyKnock(int slot, int seq, Vector3 push)
    {
        if (seq <= KnockSeq[slot]) { KnockSeq[slot] = Mathf.Max(seq, KnockSeq[slot]); return; }
        KnockSeq[slot] = seq; var p = Players[slot];
        if (!p.Puppet) p.Knock(push, .7f);
    }

    void Finish()
    {
        if (State == Phase.Finished) return;
        int best = -1; for (int s = 0; s < Players.Count; s++) if (best < 0 || Points[s] > Points[best]) best = s;
        State = Phase.Finished; WinnerSlot = best;
        Result = best >= 0 ? "¡" + Players[best].DisplayName.ToUpperInvariant() + " ES EL REY DE LA COLINA!" : "SIN GANADOR";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { Players[WinnerSlot].Celebrating = true; MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2f, 40, 8); }
        ColinaAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); ColinaAudio.FadeMusic(.15f); Paused = false;
    }

    public List<PartyMover> Ranking() => Players.OrderByDescending(p => Mathf.FloorToInt(Points[p.Slot])).ThenBy(p => p.Slot == WinnerSlot ? 0 : 1).ToList();
    void Big(string text, Color c, float seconds = 1.6f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 want; float dist = 18f; var h = Human;
        if (State == Phase.Finished && WinnerSlot >= 0) { want = Players[WinnerSlot].transform.position; dist = 11f; }
        else if (State != Phase.Playing || h == null || h.Out) { want = Vector3.Lerp(Vector3.zero, ZonePos, .4f); dist = 24f; }
        else want = Vector3.Lerp(Vector3.Lerp(Vector3.zero, ZonePos, .35f), new Vector3(h.transform.position.x, 0, h.transform.position.z), .6f);
        focus = snap ? want : Vector3.Lerp(focus, want, Time.deltaTime * 3f); snap = false;
        var rot = Quaternion.Euler(54f, 0, 0);
        Vector3 pos = focus - rot * Vector3.forward * dist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .4f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 2f); }
        cam.transform.SetPositionAndRotation(pos, rot);
    }

    public void ApplyWorld(Phase phase, float countdown, float elapsed, float[] points, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) ColinaAudio.UI("beep", .7f);
        if (phase == Phase.Playing && State != Phase.Playing) { ColinaAudio.UI("go", .9f); Big("¡A LA CORONA!", new Color(1f, .85f, .4f)); Elapsed = elapsed; }
        Countdown = countdown;
        if (phase == Phase.Playing) { float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .5f ? elapsed : Elapsed + diff * .25f; }
        for (int s = 0; s < Points.Length && s < points.Length; s++) Points[s] = points[s];
        if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winner; Result = result; Celebrate(); PartyLink.Finish(-1); return; }
        State = phase;
    }

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
