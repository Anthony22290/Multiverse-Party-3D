using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// PINTURA LOCA (2026-11-19) · party minigame for 2–6 players. A square yard of white tiles: every tile you step on
/// takes your colour. Paint buckets splash a whole circle; a dash into someone stuns them. 90 s, most tiles wins.
/// Each computer paints under its own players at once (no lag) and tells the host; the host keeps the true floor and
/// sends it to everybody four times a second, plus the buckets, the clock and the result.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class PinturaDirector : MonoBehaviour, IMoverHost
{
    public const float CountdownSeconds = 4f, RoundSeconds = 90f, Tile = 1.25f, BucketEvery = 5f, BucketRadius = 3.2f;
    public const int N = 20, MaxBuckets = 3;
    public static float Half => N * Tile * .5f;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Playing, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float TimeLeft => Mathf.Max(0f, RoundSeconds - Elapsed);
    public readonly List<PartyMover> Players = new();
    public PartyMover Human { get; private set; }
    public PinturaNet Net { get; private set; }
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
    public bool InputFree => !Paused && CanMove && Human != null;
    public float CameraYaw => 0f;
    public bool Dirty { get; set; }

    /// <summary>Owner of every tile: 0 = white, otherwise member + 1.</summary>
    public readonly byte[] Grid = new byte[N * N];
    public int[] Counts { get; private set; }    // by slot
    public int[] KnockSeq { get; private set; }
    public sealed class Bucket { public int Id; public Vector3 Pos; public Transform T; }
    public readonly List<Bucket> Buckets = new();
    public readonly List<(int tile, int member)> PendingPaint = new();   // client → host

    readonly Dictionary<int, float> recentLocal = new();
    System.Random rng; Texture2D tex; Color[] pixels; bool texDirty; float nextBucket; int nextBucketId = 1;
    Camera cam; Vector3 focus; bool snap = true; float trauma;

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 1543 + PartyLink.Round * 19) : Random.Range(1, 999999);
        rng = new System.Random(Seed);
        RenderSettings.ambientLight = new Color(.66f, .66f, .7f); RenderSettings.fog = false;
        BuildWorld();
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Counts = new int[count]; KnockSeq = new int[count];
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name, typeof(CharacterController));
            float a = (member / (float)count * 360f + 45f) * Mathf.Deg2Rad;
            var at = new Vector3(Mathf.Cos(a) * 8.5f, .1f, Mathf.Sin(a) * 8.5f);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-new Vector3(at.x, 0, at.z)));
            var p = go.AddComponent<PartyMover>(); bool puppet = PartyLink.IsPuppet(i);
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) { go.AddComponent<PinturaBot>(); p.BotControlled = true; }
        }
        cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.6f, .82f, 1f); cam.fieldOfView = 52f;
        gameObject.AddComponent<PinturaHUD>().Director = this;
        PinturaAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<PinturaNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; PinturaAudio.Release(); }

    void BuildWorld()
    {
        var root = new GameObject("Patio de pintura").transform;
        var floor = new GameObject("Suelo", typeof(BoxCollider)); floor.transform.SetParent(root, false);
        floor.transform.position = new Vector3(0, -.5f, 0); floor.GetComponent<BoxCollider>().size = new Vector3(120, 1, 120);
        EscapeKit.Block(root, "Césped", new Vector3(0, -.08f, 0), new Vector3(120, .1f, 120), EscapeKit.Paint(new Color(.5f, .75f, .4f), .05f), .01f, false);
        // The painted floor: one texture, one pixel per tile.
        tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        pixels = new Color[N * N]; for (int i = 0; i < pixels.Length; i++) pixels[i] = TileWhite(i); tex.SetPixels(pixels); tex.Apply();
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "Baldosas"; Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(root, false); quad.transform.SetPositionAndRotation(new Vector3(0, .02f, 0), Quaternion.Euler(90f, 0, 0)); quad.transform.localScale = new Vector3(N * Tile, N * Tile, 1);
        var mat = new Material(EscapeKit.Paint(Color.white, .35f)); mat.mainTexture = tex; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        quad.GetComponent<Renderer>().sharedMaterial = mat;
        // Grout lines.
        var grout = EscapeKit.Paint(new Color(.7f, .7f, .72f), .1f);
        for (int i = 0; i <= N; i++)
        {
            float x = -Half + i * Tile;
            EscapeKit.Block(root, "Junta", new Vector3(x, .03f, 0), new Vector3(.05f, .01f, N * Tile), grout, .002f, false);
            EscapeKit.Block(root, "Junta", new Vector3(0, .03f, x), new Vector3(N * Tile, .01f, .05f), grout, .002f, false);
        }
        // Low walls with paint splats.
        Color[] splats = { new Color(1f, .35f, .4f), new Color(.35f, .6f, 1f), new Color(1f, .85f, .3f), new Color(.45f, .9f, .5f), new Color(.8f, .5f, 1f) };
        var wall = EscapeKit.Paint(new Color(.96f, .95f, .92f), .2f);
        for (int s = 0; s < 4; s++)
        {
            var dir = Quaternion.Euler(0, s * 90f, 0);
            var w = EscapeKit.Block(root, "Muro", dir * new Vector3(0, .6f, Half + .3f), new Vector3(N * Tile + 1.2f, 1.2f, .5f), wall, .08f, true);
            w.transform.rotation = dir;
            var col = w.GetComponent<BoxCollider>(); col.size = new Vector3(col.size.x, 4f, col.size.z); col.center = Vector3.up * 1.4f;
            for (int k = 0; k < 6; k++)
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Mancha", dir * new Vector3(-Half + 2f + k * 4.2f, .5f + (k % 2) * .3f, Half + .05f), new Vector3(.9f, .7f, .1f), EscapeKit.Paint(splats[(k + s) % splats.Length], .4f), false);
        }
        // Giant paint pots in the corners (decoration).
        for (int s = 0; s < 4; s++)
        {
            var c = new Vector3((s % 2 == 0 ? -1 : 1) * (Half + 3f), 0, (s < 2 ? -1 : 1) * (Half + 3f));
            EscapeKit.Shape(PrimitiveType.Cylinder, root, "Bote", c + Vector3.up * 1.2f, new Vector3(2.4f, 1.2f, 2.4f), EscapeKit.Paint(new Color(.85f, .85f, .88f), .6f), false);
            EscapeKit.Shape(PrimitiveType.Cylinder, root, "Pintura", c + Vector3.up * 2.42f, new Vector3(2.2f, .02f, 2.2f), EscapeKit.Paint(splats[s], .7f), false);
        }
        var sun = new GameObject("Luz").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.05f; sun.transform.rotation = Quaternion.Euler(55f, 35f, 0); sun.shadows = LightShadows.Soft;
    }

    static Color TileWhite(int i) => ((i % N) + (i / N)) % 2 == 0 ? new Color(.97f, .97f, .96f) : new Color(.9f, .9f, .9f);
    public static int TileAt(Vector3 p)
    {
        int x = Mathf.FloorToInt((p.x + Half) / Tile), z = Mathf.FloorToInt((p.z + Half) / Tile);
        return x < 0 || z < 0 || x >= N || z >= N ? -1 : z * N + x;
    }
    public static Vector3 TileCenter(int i) => new Vector3(-Half + (i % N + .5f) * Tile, 0, -Half + (i / N + .5f) * Tile);
    public PartyMover OwnerOf(int member) { foreach (var p in Players) if (p.Member == member) return p; return null; }
    public int TileOwnerSlot(int i) { if (Grid[i] == 0) return -1; var p = OwnerOf(Grid[i] - 1); return p != null ? p.Slot : -1; }

    public void SetTile(int i, int member)
    {
        if (i < 0 || i >= Grid.Length || Grid[i] == member + 1) return;
        Grid[i] = (byte)(member + 1);
        var p = OwnerOf(member);
        pixels[i] = p != null ? Color.Lerp(p.Color, Color.white, .12f) : TileWhite(i);
        texDirty = true;
    }

    /// <summary>Host: the whole floor from the network.</summary>
    public void SetGrid(byte[] g)
    {
        for (int i = 0; i < Grid.Length && i < g.Length; i++)
        {
            if (Grid[i] == g[i]) continue;
            if (recentLocal.TryGetValue(i, out var at) && Time.time - at < .7f) continue;   // our own fresh paint wins until the host catches up
            Grid[i] = g[i]; var p = g[i] == 0 ? null : OwnerOf(g[i] - 1);
            pixels[i] = p != null ? Color.Lerp(p.Color, Color.white, .12f) : TileWhite(i); texDirty = true;
        }
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
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) PinturaAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { Countdown = 0f; State = Phase.Playing; Elapsed = 0f; nextBucket = 3f; PinturaAudio.UI("go", .9f); Big("¡A PINTAR!", new Color(1f, .85f, .4f)); Dirty = true; }
                    break;
                case Phase.Playing:
                    Elapsed += Time.deltaTime;
                    HostBuckets(); HostKnocks();
                    if (Elapsed >= RoundSeconds) Finish();
                    break;
            }
        }
        else if (State == Phase.Playing) Elapsed += Time.deltaTime;
        if (State == Phase.Playing) OwnerRules();
        Recount();
        if (texDirty) { tex.SetPixels(pixels); tex.Apply(false); texDirty = false; }
        foreach (var b in Buckets) if (b.T != null) b.T.rotation = Quaternion.Euler(0, Time.time * 90f, 0);
        if (Time.time > bigUntil) BigText = "";
    }

    void OwnerRules()
    {
        foreach (var p in Players)
        {
            if (p.Puppet) continue;
            int t = TileAt(p.transform.position);
            if (t >= 0 && Grid[t] != p.Member + 1 && p.transform.position.y < .8f)
            {
                SetTile(t, p.Member);
                if (IsClient) { PendingPaint.Add((t, p.Member)); recentLocal[t] = Time.time; } else Dirty = true;
                if (p.IsHuman && Random.value < .25f) PinturaAudio.Play("splat", .15f);
            }
            foreach (var b in Buckets)
            {
                Vector3 d = b.Pos - p.transform.position; d.y = 0;
                if (d.magnitude < 1f) { if (IsClient) Net?.SendBucket(b.Id, p.Member); else HostTakeBucket(b.Id, p.Slot); break; }
            }
        }
    }

    void HostBuckets()
    {
        if (Elapsed < nextBucket) return;
        nextBucket = Elapsed + BucketEvery;
        if (Buckets.Count >= MaxBuckets) return;
        int t = rng.Next(N * N);
        AddBucket(nextBucketId++, TileCenter(t));
        Dirty = true;
    }

    public void AddBucket(int id, Vector3 pos)
    {
        if (Buckets.Exists(b => b.Id == id)) return;
        var root = new GameObject("Cubo de pintura " + id).transform; root.position = pos;
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Cubo", Vector3.up * .4f, new Vector3(.7f, .35f, .7f), EscapeKit.Paint(new Color(.85f, .87f, .9f), .7f), false);
        EscapeKit.Shape(PrimitiveType.Sphere, root, "Pintura arcoíris", Vector3.up * .78f, new Vector3(.62f, .2f, .62f), EscapeKit.Glow(new Color(1f, .5f, .9f), 1.5f), false);
        EscapeKit.PointLight(root, Vector3.up * 1.2f, new Color(1f, .6f, .9f), 3f, 1.5f);
        Buckets.Add(new Bucket { Id = id, Pos = pos, T = root });
        PinturaAudio.UI("bucket", .5f);
    }

    public void RemoveBucket(int id)
    {
        int i = Buckets.FindIndex(b => b.Id == id); if (i < 0) return;
        if (Buckets[i].T != null) Destroy(Buckets[i].T.gameObject);
        Buckets.RemoveAt(i);
    }

    public void HostTakeBucket(int id, int slot)
    {
        var b = Buckets.Find(x => x.Id == id); if (b == null || slot < 0 || slot >= Players.Count) return;
        var p = Players[slot];
        for (int i = 0; i < Grid.Length; i++) { var c = TileCenter(i); c.y = 0; if ((c - new Vector3(b.Pos.x, 0, b.Pos.z)).magnitude < BucketRadius) SetTile(i, p.Member); }
        SplashFx(b.Pos, p);
        RemoveBucket(id); Dirty = true; Net?.SendSplash(b.Pos, p.Member);
    }

    public void SplashFx(Vector3 at, PartyMover p)
    {
        PinturaAudio.UI("splash", p.IsHuman ? 1f : .5f);
        MultiverseFX.Burst(at + Vector3.up, p.Color, 30, 7); MultiverseFX.Ring(at + Vector3.up * .1f, BucketRadius, p.Color, .5f);
        FeedLine("¡" + p.DisplayName + " vuelca un cubo de pintura!");
        if (p.IsHuman) Shake(.3f);
    }

    void HostKnocks()
    {
        foreach (var a in Players)
        {
            if (!a.Dashing) continue;
            foreach (var b in Players)
            {
                if (a == b || b.Stunned) continue;
                Vector3 d = b.transform.position - a.transform.position; d.y = 0;
                if (d.magnitude > 1.2f) continue;
                KnockSeq[b.Slot]++;
                if (!b.Puppet) b.Knock(d.normalized * 7f + Vector3.up * 3f, .9f); else b.ShowStun(.9f);
                PinturaAudio.UI("bump", a.IsHuman || b.IsHuman ? 1f : .4f);
                SurvivalKit.Puff(b.transform.position + Vector3.up, a.Color, 5, .3f);
                Dirty = true;
            }
        }
    }

    public void ApplyKnock(int slot, int seq)
    {
        if (seq <= KnockSeq[slot]) { KnockSeq[slot] = seq; return; }
        KnockSeq[slot] = seq; var p = Players[slot];
        if (!p.Puppet) p.Knock(-p.transform.forward * 6f + Vector3.up * 3f, .9f);
    }

    void Recount()
    {
        System.Array.Clear(Counts, 0, Counts.Length);
        for (int i = 0; i < Grid.Length; i++) { if (Grid[i] == 0) continue; var p = OwnerOf(Grid[i] - 1); if (p != null) Counts[p.Slot]++; }
    }

    void Finish()
    {
        if (State == Phase.Finished) return;
        Recount();
        int best = -1; for (int s = 0; s < Players.Count; s++) if (best < 0 || Counts[s] > Counts[best]) best = s;
        State = Phase.Finished; WinnerSlot = best;
        Result = best >= 0 ? "¡" + Players[best].DisplayName.ToUpperInvariant() + " PINTA EL " + Mathf.RoundToInt(Counts[best] * 100f / Grid.Length) + "%!" : "SIN GANADOR";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count) { Players[WinnerSlot].Celebrating = true; MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2f, 40, 8); }
        PinturaAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); PinturaAudio.FadeMusic(.15f); Paused = false;
    }

    public List<PartyMover> Ranking() => Players.OrderByDescending(p => Counts[p.Slot]).ThenBy(p => p.Slot == WinnerSlot ? 0 : 1).ToList();
    void Big(string text, Color c, float seconds = 1.6f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    public void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }
    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 want; float dist = 17f; var h = Human;
        if (State == Phase.Finished) { want = Vector3.zero; dist = 27f; }
        else if (State != Phase.Playing) { want = Vector3.zero; dist = 27f; }
        else want = h != null ? Vector3.Lerp(Vector3.zero, new Vector3(h.transform.position.x, 0, h.transform.position.z), .65f) : Vector3.zero;
        focus = snap ? want : Vector3.Lerp(focus, want, Time.deltaTime * 3.5f); snap = false;
        var rot = Quaternion.Euler(58f, 0, 0);
        Vector3 pos = focus - rot * Vector3.forward * dist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .4f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 2f); }
        cam.transform.SetPositionAndRotation(Vector3.Lerp(cam.transform.position, pos, snap ? 1f : Time.deltaTime * 8f), rot);
    }

    public void ApplyWorld(Phase phase, float countdown, float elapsed, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) PinturaAudio.UI("beep", .7f);
        if (phase == Phase.Playing && State != Phase.Playing) { PinturaAudio.UI("go", .9f); Big("¡A PINTAR!", new Color(1f, .85f, .4f)); Elapsed = elapsed; }
        Countdown = countdown;
        if (phase == Phase.Playing) { float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .5f ? elapsed : Elapsed + diff * .25f; }
        if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winner; Result = result; Recount(); Celebrate(); PartyLink.Finish(-1); return; }
        State = phase;
    }

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
