using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// SUELO QUE SE CAE (2026-11-19) · party minigame for 2–6 players. Three floors of hexagonal tiles over lava: every
/// tile you step on cracks and drops a moment later. Fall through a hole and you land on the floor below; fall off
/// the last floor and you are out. Last one standing wins (after 150 s, whoever is highest).
/// Every computer cracks the tiles its own players step on and tells the others (a crack is never undone, so the
/// floors end up the same everywhere); the host keeps the clock, who is out and the winner.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class SueloDirector : MonoBehaviour, IMoverHost
{
    public const float CountdownSeconds = 4f, RoundSeconds = 150f, CrackTime = .7f, LavaY = -4.5f, TileR = 1.1f, TileH = .35f;
    public static readonly float[] FloorY = { 8f, 4f, 0f };
    public const int Rings = 8;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Playing, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float TimeLeft => Mathf.Max(0f, RoundSeconds - Elapsed);
    public readonly List<PartyMover> Players = new();
    public readonly List<int> OutOrder = new();
    public PartyMover Human { get; private set; }
    public SueloNet Net { get; private set; }
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
    public int AliveCount => Players.Count(p => !p.Out);
    public PartyMover Watching { get; private set; }

    public sealed class Tile { public readonly List<Tile> Near = new(); public int Id, Floor; public Vector3 Pos; public Transform T; public Renderer R; public Collider C; public float CrackedAt = -1f; public bool Gone; public Material Mat; public Color Base; }
    public readonly List<Tile> Tiles = new();
    readonly Dictionary<Collider, Tile> byCollider = new();
    public readonly List<int> Pending = new();   // cracks to send (this computer)
    Camera cam; Vector3 focus; float trauma, finishAt = -1f; bool snap = true;

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 3571 + PartyLink.Round * 13) : Random.Range(1, 999999);
        RenderSettings.ambientLight = new Color(.55f, .45f, .5f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.35f, .12f, .1f); RenderSettings.fogStartDistance = 30f; RenderSettings.fogEndDistance = 80f;
        BuildWorld();
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name, typeof(CharacterController));
            float a = (member / (float)count * 360f + 90f) * Mathf.Deg2Rad;
            var at = new Vector3(Mathf.Cos(a) * 6f, FloorY[0] + .3f, Mathf.Sin(a) * 6f);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-new Vector3(at.x, 0, at.z)));
            var p = go.AddComponent<PartyMover>(); bool puppet = PartyLink.IsPuppet(i);
            p.CanDash = false; p.Speed = 5.4f;
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) { go.AddComponent<SueloBot>(); p.BotControlled = true; }
        }
        cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .07f, .08f); cam.fieldOfView = 52f; cam.farClipPlane = 200f;
        gameObject.AddComponent<SueloHUD>().Director = this;
        SueloAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<SueloNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; SueloAudio.Release(); }

    // ------------------------------------------------------------------ world

    static Mesh hexMesh;
    static Mesh HexMesh()
    {
        if (hexMesh != null) return hexMesh;
        var v = new List<Vector3>(); var t = new List<int>();
        float r = TileR * .96f, h = TileH;
        Vector3 P(int i, float y) { float a = (60f * i + 30f) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        // top and bottom caps
        int top = v.Count; v.Add(new Vector3(0, 0, 0)); for (int i = 0; i < 6; i++) v.Add(P(i, 0));
        for (int i = 0; i < 6; i++) { t.Add(top); t.Add(top + 1 + (i + 1) % 6); t.Add(top + 1 + i); }
        int bot = v.Count; v.Add(new Vector3(0, -h, 0)); for (int i = 0; i < 6; i++) v.Add(P(i, -h));
        for (int i = 0; i < 6; i++) { t.Add(bot); t.Add(bot + 1 + i); t.Add(bot + 1 + (i + 1) % 6); }
        for (int i = 0; i < 6; i++)
        {
            int b = v.Count; v.Add(P(i, 0)); v.Add(P((i + 1) % 6, 0)); v.Add(P((i + 1) % 6, -h)); v.Add(P(i, -h));
            t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }
        hexMesh = new Mesh { name = "Hexágono" }; hexMesh.SetVertices(v); hexMesh.SetTriangles(t, 0); hexMesh.RecalculateNormals(); hexMesh.RecalculateBounds();
        return hexMesh;
    }

    void BuildWorld()
    {
        var root = new GameObject("Torre de suelos").transform;
        Color[] colors = { new Color(1f, .55f, .7f), new Color(1f, .85f, .35f), new Color(.45f, .8f, 1f) };
        int id = 0;
        for (int f = 0; f < FloorY.Length; f++)
        {
            for (int q = -Rings; q <= Rings; q++)
                for (int r = Mathf.Max(-Rings, -q - Rings); r <= Mathf.Min(Rings, -q + Rings); r++)
                {
                    var pos = new Vector3(TileR * Mathf.Sqrt(3f) * (q + r * .5f), FloorY[f], TileR * 1.5f * r);
                    var go = new GameObject("Baldosa " + id); go.transform.SetParent(root, false); go.transform.position = pos;
                    go.AddComponent<MeshFilter>().sharedMesh = HexMesh();
                    bool alt = ((q - r) % 3 + 3) % 3 == 0;
                    var baseColor = Color.Lerp(colors[f], Color.white, alt ? .25f : 0f);
                    var mat = new Material(EscapeKit.Paint(baseColor, .35f));   // own copy: it is recoloured when it cracks
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                    var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = HexMesh(); mc.convex = true;
                    var tile = new Tile { Id = id, Floor = f, Pos = pos, T = go.transform, R = mr, C = mc, Mat = mat, Base = baseColor };
                    Tiles.Add(tile); byCollider[mc] = tile; id++;
                }
        }
        // Neighbours (same floor) for the bots.
        float near = TileR * 2.1f * TileR * 2.1f;
        foreach (var a in Tiles) foreach (var b in Tiles) if (a != b && a.Floor == b.Floor && (a.Pos - b.Pos).sqrMagnitude < near) a.Near.Add(b);
        // Lava lake with glowing bubbles, and dark rock pillars round it.
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Lava", new Vector3(0, LavaY - .5f, 0), new Vector3(70, .5f, 70), EscapeKit.Glow(new Color(.95f, .28f, .05f), .9f), false);
        var rock = EscapeKit.Paint(new Color(.22f, .16f, .17f), .1f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad; float rr = 25f + (i % 3) * 3f; float h = 6f + (i * 7 % 5) * 3f;
            EscapeKit.Block(root, "Roca", new Vector3(Mathf.Cos(a) * rr, LavaY + h * .5f - 1f, Mathf.Sin(a) * rr), new Vector3(2.2f, h, 2.2f), rock, .3f, false, new Vector3(0, i * 37f, 0));
        }
        EscapeKit.PointLight(root, new Vector3(0, LavaY + 2f, 0), new Color(1f, .45f, .15f), 30f, 2.5f);
        var sun = new GameObject("Luz").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .9f; sun.transform.rotation = Quaternion.Euler(55f, 30f, 0); sun.shadows = LightShadows.Soft;
    }

    public Tile TileUnder(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * .4f, Vector3.down, out var hit, 1.1f, ~0, QueryTriggerInteraction.Ignore) && byCollider.TryGetValue(hit.collider, out var t)) return t;
        return null;
    }
    public bool Solid(Tile t) => t != null && t.CrackedAt < 0f && !t.Gone;
    public int FloorOf(float y) { for (int f = 0; f < FloorY.Length; f++) if (y > FloorY[f] - 2f) return f; return FloorY.Length; }

    /// <summary>Starts cracking a tile (any computer; idempotent).</summary>
    public void Crack(int id, bool mine)
    {
        if (id < 0 || id >= Tiles.Count) return;
        var t = Tiles[id]; if (t.CrackedAt >= 0f || t.Gone) return;
        t.CrackedAt = Time.time;
        if (mine) Pending.Add(id);
        if (Human != null && Vector3.Distance(Human.transform.position, t.Pos) < 6f && Random.value < .35f) SueloAudio.Play("crack", .25f);
    }

    void TickTiles()
    {
        foreach (var t in Tiles)
        {
            if (t.Gone || t.CrackedAt < 0f) continue;
            float age = Time.time - t.CrackedAt;
            if (age < CrackTime)
            {
                float k = age / CrackTime;
                t.Mat.color = Color.Lerp(t.Base, new Color(1f, .3f, .2f), k);
                t.T.position = t.Pos + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)) * .04f * k;
            }
            else
            {
                if (t.C.enabled) { t.C.enabled = false; }
                float fall = age - CrackTime;
                t.T.position = t.Pos + Vector3.down * (4.9f * fall * fall);
                t.T.rotation = Quaternion.Euler(fall * 60f, 0, fall * 40f);
                if (fall > 1.6f) { t.Gone = true; t.T.gameObject.SetActive(false); }
            }
        }
    }

    // ------------------------------------------------------------------ loop

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
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) SueloAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { Countdown = 0f; State = Phase.Playing; Elapsed = 0f; SueloAudio.UI("go", .9f); Big("¡NO DEJES DE MOVERTE!", new Color(1f, .85f, .4f)); Dirty = true; }
                    break;
                case Phase.Playing:
                    Elapsed += Time.deltaTime;
                    if (finishAt < 0f && (AliveCount <= (Players.Count > 1 ? 1 : 0) || Elapsed >= RoundSeconds)) finishAt = Elapsed + (Elapsed >= RoundSeconds ? 0f : 1.2f);
                    if (finishAt >= 0f && Elapsed >= finishAt) Finish();
                    break;
            }
        }
        else if (State == Phase.Playing) Elapsed += Time.deltaTime;
        if (State == Phase.Playing) OwnerRules();
        TickTiles();
        if (Time.time > bigUntil) BigText = "";
    }

    /// <summary>This computer's players: crack what they stand on, fall into the lava.</summary>
    void OwnerRules()
    {
        foreach (var p in Players)
        {
            if (p.Puppet || p.Out) continue;
            if (p.Grounded) { var t = TileUnder(p.transform.position); if (Solid(t)) Crack(t.Id, true); }
            if (p.transform.position.y < LavaY + .6f) OwnerOut(p);
        }
    }

    void OwnerOut(PartyMover p)
    {
        p.SetOut();
        if (IsClient) { Net?.SendOut(p.Member); OutFx(p); return; }
        HostOut(p.Slot);
    }

    public void HostOut(int slot)
    {
        if (OutOrder.Contains(slot)) return;
        OutOrder.Add(slot); OutFx(Players[slot]);
        if (!Players[slot].Out) Players[slot].SetOut();
        Dirty = true;
    }

    public void OutFx(PartyMover p)
    {
        var at = new Vector3(p.transform.position.x, LavaY, p.transform.position.z);
        SueloAudio.UI("splash", p.IsHuman ? 1f : .5f);
        MultiverseFX.Burst(at + Vector3.up, new Color(1f, .5f, .15f), 26, 7); SurvivalKit.Puff(at + Vector3.up, new Color(.3f, .3f, .3f), 6, .6f);
        if (!Feed.Exists(f => f.StartsWith(p.DisplayName + " cae"))) FeedLine(p.DisplayName + " cae a la lava");
        if (p.IsHuman) Big("¡A LA LAVA! Mira cómo acaban los demás", new Color(1f, .5f, .3f), 2.2f);
    }

    void Finish()
    {
        if (State == Phase.Finished) return;
        var alive = Players.Where(p => !p.Out).OrderByDescending(p => p.transform.position.y).ToList();
        WinnerSlot = alive.Count > 0 ? alive[0].Slot : OutOrder.Count > 0 ? OutOrder[OutOrder.Count - 1] : -1;
        State = Phase.Finished;
        Result = WinnerSlot >= 0 ? "¡" + Players[WinnerSlot].DisplayName.ToUpperInvariant() + " ES EL ÚLTIMO EN PIE!" : "SIN GANADOR";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count && !Players[WinnerSlot].Out) { Players[WinnerSlot].Celebrating = true; MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2f, 40, 8); }
        SueloAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); SueloAudio.FadeMusic(.15f); Paused = false;
    }

    public List<PartyMover> Ranking()
    {
        var list = Players.Where(p => !p.Out).OrderBy(p => p.Slot == WinnerSlot ? 0 : 1).ThenByDescending(p => p.transform.position.y).ToList();
        for (int i = OutOrder.Count - 1; i >= 0; i--) if (!list.Contains(Players[OutOrder[i]])) list.Add(Players[OutOrder[i]]);
        foreach (var p in Players) if (!list.Contains(p)) list.Add(p);
        return list;
    }

    void Big(string text, Color c, float seconds = 1.6f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }
    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 want; float dist = 19f;
        var h = Human;
        if (State == Phase.Finished && WinnerSlot >= 0 && WinnerSlot < Players.Count) { want = Players[WinnerSlot].transform.position; dist = 11f; }
        else if (h != null && !h.Out && State != Phase.Waiting && State != Phase.Countdown) { want = Vector3.Lerp(new Vector3(0, h.transform.position.y, 0), h.transform.position, .6f); Watching = null; }
        else if (h != null && h.Out && State == Phase.Playing)
        {
            var alive = Players.FindAll(p => !p.Out);
            Watching = alive.Count > 0 ? alive[PartySpectate.Pick(alive.Count)] : null;
            if (PartySpectate.Overview) Watching = null;
            want = Watching != null ? Watching.transform.position : new Vector3(0, FloorY[1], 0);
            if (Watching == null) dist = 32f;
        }
        else { want = new Vector3(0, FloorY[0], 0); dist = 30f; }
        focus = snap ? want : Vector3.Lerp(focus, want, Time.deltaTime * 3f); snap = false;
        var rot = Quaternion.Euler(55f, 0, 0);
        Vector3 pos = focus + Vector3.up * .5f - rot * Vector3.forward * dist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .4f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 2f); }
        cam.transform.SetPositionAndRotation(pos, rot);
        // Floors above the one we are looking at would block the view: hide them.
        int shownFloor = FloorOf(want.y + .5f);
        if (shownFloor != hiddenAbove)
        {
            hiddenAbove = shownFloor;
            foreach (var t in Tiles) t.R.enabled = t.Floor >= shownFloor;
        }
    }
    int hiddenAbove = -1;

    // ------------------------------------------------------------------ client

    public void ApplyWorld(Phase phase, float countdown, float elapsed, List<int> outSlots, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) SueloAudio.UI("beep", .7f);
        if (phase == Phase.Playing && State != Phase.Playing) { SueloAudio.UI("go", .9f); Big("¡NO DEJES DE MOVERTE!", new Color(1f, .85f, .4f)); Elapsed = elapsed; }
        Countdown = countdown;
        if (phase == Phase.Playing) { float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .5f ? elapsed : Elapsed + diff * .25f; }
        foreach (var s in outSlots) if (!OutOrder.Contains(s)) { OutOrder.Add(s); if (Players[s].Puppet) OutFx(Players[s]); }
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
