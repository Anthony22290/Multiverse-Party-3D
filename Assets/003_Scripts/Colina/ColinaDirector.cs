using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// REY DE LA COLINA (2026-11-19, v2 "volcán") · 2–6 players on a floating volcano: a ground ring, a middle terrace and
/// the summit, joined by three ramps (front, left, right) over the void. The first one to reach the summit plants
/// the flag in their colour, and the flag scores 1 point per second for its colour. It changes colour when somebody
/// else stands on the summit while its owner is not there — so push the king off (Shift) or blow him away with
/// bombs (E / left click). Falling into the void respawns you on the ground ring 2 s later. 2 minutes, most points wins.
/// Each computer moves its own players, simulates every bomb and applies blasts to its own players; the host keeps
/// time, the flag and the points, and resolves shoves.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class ColinaDirector : MonoBehaviour, IMoverHost
{
    public const float CountdownSeconds = 4f, RoundSeconds = 120f, RespawnSeconds = 2f, VoidY = -10f, CaptureTime = .35f;
    public const float SummitY = 5.6f, SummitR = 3.2f, MidY = 3f, MidIn = 5.4f, MidOut = 9.5f, BaseIn = 12.5f, BaseOut = 19f;
    public const float BombCooldown = 2.2f, BombRadius = 3.3f, BombFuse = 3f, BombGravity = 18f;
    public static readonly float[] PathAngles = { -90f, 30f, 150f };   // front (towards the camera), right, left

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
    public int FlagOwner { get; private set; } = -1;   // slot, -1 = nobody yet
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

    sealed class Bomb { public int Id, Owner; public Vector3 Pos, Vel; public float Born; public Transform T; }
    readonly List<Bomb> bombs = new();
    float[] nextThrow; float[] topSince; int bombSeq;
    readonly Dictionary<PartyMover, float> respawnAt = new();
    Material flagMat, summitGlow; Light summitLight; Transform cloth; int shownOwner = -2;
    Camera cam; Vector3 focus; bool snap = true; float trauma;

    public static Vector3 Polar(float deg, float r, float y) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * r, y, Mathf.Sin(deg * Mathf.Deg2Rad) * r);
    public static bool OnSummit(Vector3 p) => p.y > SummitY - .7f && new Vector2(p.x, p.z).magnitude < SummitR + .1f;
    public bool AtTop(PartyMover p) => !p.Out && OnSummit(p.transform.position);
    public float BombReady(PartyMover p) => nextThrow == null ? 1f : Mathf.Clamp01(1f - (nextThrow[p.Slot] - Time.time) / BombCooldown);

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 2909 + PartyLink.Round * 23) : Random.Range(1, 999999);
        RenderSettings.ambientLight = new Color(.62f, .66f, .74f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.62f, .8f, 1f); RenderSettings.fogStartDistance = 45f; RenderSettings.fogEndDistance = 120f;
        BuildWorld();
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        Points = new float[count]; KnockSeq = new int[count]; nextThrow = new float[count]; topSince = Enumerable.Repeat(-1f, count).ToArray();
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name, typeof(CharacterController));
            var at = SpawnPoint(member, count);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-new Vector3(at.x, 0, at.z)));
            var p = go.AddComponent<PartyMover>(); bool puppet = PartyLink.IsPuppet(i);
            p.DashSpeed = 16f; p.DashCooldown = 1.8f; p.LeftClickDashes = false;
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) { go.AddComponent<ColinaBot>(); p.BotControlled = true; }
        }
        cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .78f, 1f); cam.fieldOfView = 52f; cam.farClipPlane = 220f;
        gameObject.AddComponent<ColinaHUD>().Director = this;
        ColinaAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<ColinaNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; ColinaAudio.Release(); }

    /// <summary>Ground ring, between the ramps.</summary>
    public static Vector3 SpawnPoint(int member, int count)
    {
        float a = -90f + 60f + (member % 3) * 120f + (member / 3) * 22f;   // the gaps between the three ramps
        return Polar(a, 16f, .3f);
    }

    // ------------------------------------------------------------------ world

    static Mesh RingMesh(float rIn, float rOut, float thick, int seg)
    {
        var v = new List<Vector3>(); var top = new List<int>(); var side = new List<int>();
        Vector3 P(int i, float r, float y) { float a = i / (float)seg * Mathf.PI * 2f; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        int t0 = v.Count; for (int i = 0; i <= seg; i++) { v.Add(P(i, rIn, 0)); v.Add(P(i, rOut, 0)); }
        for (int i = 0; i < seg; i++) { int a = t0 + i * 2; top.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); }
        int b0 = v.Count; for (int i = 0; i <= seg; i++) { v.Add(P(i, rIn, -thick)); v.Add(P(i, rOut, -thick)); }
        for (int i = 0; i < seg; i++) { int a = b0 + i * 2; side.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
        int o0 = v.Count; for (int i = 0; i <= seg; i++) { v.Add(P(i, rOut, 0)); v.Add(P(i, rOut * .97f, -thick)); }
        for (int i = 0; i < seg; i++) { int a = o0 + i * 2; side.AddRange(new[] { a + 1, a, a + 2, a + 1, a + 2, a + 3 }); }
        if (rIn > .05f)
        {
            int n0 = v.Count; for (int i = 0; i <= seg; i++) { v.Add(P(i, rIn, 0)); v.Add(P(i, rIn * 1.03f, -thick)); }
            for (int i = 0; i < seg; i++) { int a = n0 + i * 2; side.AddRange(new[] { a + 1, a + 2, a, a + 1, a + 3, a + 2 }); }
        }
        var m = new Mesh { name = "Anillo" }; m.SetVertices(v); m.subMeshCount = 2; m.SetTriangles(top, 0); m.SetTriangles(side, 1);
        m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    void Terrace(Transform root, string name, float rIn, float rOut, float y, float thick, Material grass, Material rock)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.position = new Vector3(0, y, 0);
        var mesh = RingMesh(rIn, rOut, thick, 72);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = new[] { grass, rock };
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    void Ramp(Transform root, float ang, float r0, float y0, float r1, float y1, float width, Material mat, Material edge)
    {
        Vector3 s = Polar(ang, r0, y0), e = Polar(ang, r1, y1);
        Vector3 f = e - s; float len = f.magnitude; f /= len;
        var rot = Quaternion.LookRotation(f, Vector3.up);
        var go = EscapeKit.Block(root, "Rampa", (s + e) * .5f - rot * Vector3.up * .2f, new Vector3(width, .4f, len + .2f), mat, .05f, true);
        go.transform.rotation = rot;
        // Painted stripes at the edges so the fall is easy to see (no railings: you can fall).
        foreach (var side in new[] { -1f, 1f })
        {
            var st = EscapeKit.Block(root, "Borde", (s + e) * .5f + rot * new Vector3(side * (width * .5f - .12f), .02f, 0), new Vector3(.2f, .04f, len), edge, .01f, false);
            st.transform.rotation = rot;
        }
    }

    void BuildWorld()
    {
        var root = new GameObject("Volcán").transform;
        var grass = EscapeKit.Paint(new Color(.5f, .78f, .38f), .08f); var grass2 = EscapeKit.Paint(new Color(.6f, .84f, .42f), .08f);
        var rock = EscapeKit.Paint(new Color(.72f, .6f, .5f), .12f); var rockDark = EscapeKit.Paint(new Color(.55f, .45f, .4f), .12f);
        var wood = EscapeKit.Paint(new Color(.85f, .72f, .55f), .15f); var edge = EscapeKit.Paint(new Color(1f, .95f, .85f), .2f);
        Terrace(root, "Planta baja", BaseIn, BaseOut, 0f, 2.4f, grass, rock);
        Terrace(root, "Terraza", MidIn, MidOut, MidY, 1.8f, grass2, rockDark);
        Terrace(root, "Cima", .01f, SummitR, SummitY, 1.6f, EscapeKit.Paint(new Color(.95f, .9f, .75f), .1f), rock);
        foreach (var a in PathAngles)
        {
            Ramp(root, a, 13.7f, 0f, MidOut - .05f, MidY, 2.6f, wood, edge);   // ends right at the upper edge: no step
            Ramp(root, a, 6.4f, MidY, SummitR - .05f, SummitY, 2.4f, wood, edge);
        }
        // Volcano flavour: rocks hanging under the terraces, palms on the ground ring, clouds far below.
        for (int i = 0; i < 18; i++)
        {
            float a = i * 20f + 7f; float r = Mathf.Lerp(BaseIn + 1f, BaseOut - 1.5f, (i * 37 % 10) / 10f);
            EscapeKit.Block(root, "Roca colgante", Polar(a, r, -2.4f - (i % 3) * .8f), new Vector3(2.2f, 2.5f + (i % 4), 2.2f), i % 2 == 0 ? rock : rockDark, .4f, false, new Vector3(i * 13f, i * 41f, 180f));
        }
        var trunk = EscapeKit.Paint(new Color(.6f, .45f, .28f), .1f); var leaf = EscapeKit.Paint(new Color(.25f, .65f, .3f), .1f);
        foreach (var a in new[] { -45f, 75f, 195f, -120f, 105f })
        {
            var p = Polar(a, 17.6f, 0f);
            EscapeKit.Block(root, "Palmera", p + Vector3.up * 2f, new Vector3(.35f, 4f, .35f), trunk, .05f, true, new Vector3(0, 0, 6f));
            for (int k = 0; k < 5; k++) EscapeKit.Block(root, "Hoja", p + new Vector3(0, 4.1f, 0), new Vector3(.4f, .08f, 2.2f), leaf, .02f, false, new Vector3(-20f, k * 72f, 0));
        }
        var cloud = EscapeKit.Paint(new Color(.97f, .98f, 1f), .05f);
        for (int i = 0; i < 26; i++)
        {
            float a = i * 47f; float r = 6f + (i * 53 % 34);
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Nube", Polar(a, r, -16f - (i % 5) * 2f), new Vector3(7f + i % 4 * 2f, 1.6f, 5f + i % 3 * 2f), cloud, false);
        }
        // The flag on the summit and a glowing ring that takes the owner's colour.
        var pole = EscapeKit.Paint(new Color(.92f, .92f, .95f), .6f);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Mástil", new Vector3(0, SummitY + 2.3f, 0), new Vector3(.16f, 2.3f, .16f), pole, false);
        EscapeKit.Shape(PrimitiveType.Sphere, root, "Bola", new Vector3(0, SummitY + 4.7f, 0), Vector3.one * .32f, EscapeKit.Glow(new Color(1f, .8f, .25f), 1.2f), false);
        cloth = new GameObject("Bandera").transform; cloth.SetParent(root, false); cloth.position = new Vector3(0, SummitY + 3.95f, 0);
        flagMat = new Material(EscapeKit.Paint(Color.white, .2f));
        EscapeKit.Block(cloth, "Tela", new Vector3(1.05f, 0, 0), new Vector3(2f, 1.2f, .06f), flagMat, .02f, false);
        var ring = new GameObject("Aro de la cima"); ring.transform.SetParent(root, false); ring.transform.position = new Vector3(0, SummitY + .03f, 0);
        summitGlow = EscapeKit.Glow(Color.white, .6f);
        ring.AddComponent<MeshFilter>().sharedMesh = RingMesh(SummitR - .45f, SummitR - .05f, .02f, 48);
        ring.AddComponent<MeshRenderer>().sharedMaterials = new[] { summitGlow, summitGlow };
        summitLight = EscapeKit.PointLight(root, new Vector3(0, SummitY + 2.5f, 0), Color.white, 10f, 1.5f);
        var sun = new GameObject("Luz").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.05f; sun.transform.rotation = Quaternion.Euler(52f, 40f, 0); sun.shadows = LightShadows.Soft;
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
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) ColinaAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { Countdown = 0f; State = Phase.Playing; Elapsed = 0f; ColinaAudio.UI("go", .9f); Big("¡A LA CIMA!", new Color(1f, .85f, .4f)); Dirty = true; }
                    break;
                case Phase.Playing:
                    Elapsed += Time.deltaTime;
                    HostFlag(Time.deltaTime); HostKnocks();
                    if (Elapsed >= RoundSeconds) Finish();
                    break;
            }
        }
        else if (State == Phase.Playing) Elapsed += Time.deltaTime;
        if (State == Phase.Playing) { OwnerRules(); HumanBombInput(); }
        TickBombs();
        PresentFlag();
        if (Time.time > bigUntil) BigText = "";
    }

    void HumanBombInput()
    {
        var h = Human; if (h == null || h.BotControlled || h.Puppet || !InputFree) return;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        bool press = (k != null && (k.eKey.wasPressedThisFrame || k.fKey.wasPressedThisFrame)) || (m != null && m.leftButton.wasPressedThisFrame)
                  || (g != null && (g.buttonEast.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame));
        if (press) TryThrow(h);
    }

    void OwnerRules()
    {
        foreach (var p in Players)
        {
            if (p.Puppet) continue;
            if (p.Out)
            {
                if (respawnAt.TryGetValue(p, out var at) && Time.time >= at)
                {
                    respawnAt.Remove(p); var sp = SpawnPoint(p.Member, Players.Count);
                    p.Revive(sp + Vector3.up * .5f); p.transform.rotation = Quaternion.LookRotation(-new Vector3(sp.x, 0, sp.z));
                    SurvivalKit.Puff(p.transform.position + Vector3.up, Color.white, 5, .4f);
                }
                continue;
            }
            if (p.transform.position.y < VoidY)
            {
                p.SetOut(); respawnAt[p] = Time.time + RespawnSeconds;
                ColinaAudio.UI("splash", p.IsHuman ? 1f : .35f);
                if (p.IsHuman) Big("¡AL VACÍO! Vuelves abajo en un momento", new Color(.6f, .85f, 1f), 1.6f);
                FeedLine(p.DisplayName + " cae al vacío");
            }
        }
    }

    // ------------------------------------------------------------------ flag (host)

    void HostFlag(float dt)
    {
        foreach (var p in Players)
        {
            bool top = AtTop(p);
            if (top && topSince[p.Slot] < 0f) topSince[p.Slot] = Elapsed;
            if (!top) topSince[p.Slot] = -1f;
        }
        bool ownerHolds = FlagOwner >= 0 && AtTop(Players[FlagOwner]);
        if (!ownerHolds)
        {
            PartyMover taker = null;
            foreach (var p in Players)
                if (p.Slot != FlagOwner && topSince[p.Slot] >= 0f && Elapsed - topSince[p.Slot] >= CaptureTime && (taker == null || topSince[p.Slot] < topSince[taker.Slot])) taker = p;
            if (taker != null) { FlagOwner = taker.Slot; Dirty = true; }
        }
        if (FlagOwner >= 0)
        {
            float before = Points[FlagOwner];
            Points[FlagOwner] += dt;
            if (Mathf.FloorToInt(before) != Mathf.FloorToInt(Points[FlagOwner]) && Players[FlagOwner].IsHuman) ColinaAudio.Play("tick", .2f);
        }
    }

    void PresentFlag()
    {
        if (cloth != null) cloth.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time * 1.7f) * 18f + Time.time * 8f % 360f, Mathf.Sin(Time.time * 5f) * 3f);
        if (FlagOwner == shownOwner) return;
        var c = FlagOwner >= 0 && FlagOwner < Players.Count ? Players[FlagOwner].Color : Color.white;
        flagMat.color = c; EscapeKit.SetGlow(summitGlow, c, FlagOwner >= 0 ? 1.6f : .5f); summitLight.color = c; summitLight.intensity = FlagOwner >= 0 ? 3f : 1.5f;
        if (shownOwner != -2 && FlagOwner >= 0)
        {
            var o = Players[FlagOwner];
            ColinaAudio.UI("move", .8f);
            MultiverseFX.Burst(new Vector3(0, SummitY + 4f, 0), c, 30, 7);
            FeedLine(o.DisplayName + " planta su bandera en la cima");
            if (o.IsHuman) Big("¡LA BANDERA ES TUYA! Aguanta arriba", new Color(1f, .85f, .4f), 1.8f);
            else if (shownOwner >= 0 && shownOwner < Players.Count && Players[shownOwner].IsHuman) Big("¡" + o.DisplayName.ToUpperInvariant() + " TE HA QUITADO LA BANDERA!", new Color(1f, .55f, .45f), 1.8f);
        }
        shownOwner = FlagOwner;
    }

    // ------------------------------------------------------------------ bombs (every computer)

    /// <summary>Throws a bomb where <paramref name="p"/> faces (local players only).</summary>
    public bool TryThrow(PartyMover p)
    {
        if (p == null || p.Puppet || p.Out || p.Stunned || State != Phase.Playing || Time.time < nextThrow[p.Slot]) return false;
        nextThrow[p.Slot] = Time.time + BombCooldown;
        var f = p.transform.forward; f.y = 0; f = f.sqrMagnitude > .01f ? f.normalized : Vector3.forward;
        var origin = p.transform.position + Vector3.up * 1.45f + f * .75f;
        var vel = f * 10f + Vector3.up * 6.2f + new Vector3(p.Velocity.x, 0, p.Velocity.z) * .4f;
        int id = (p.Member + 1) * 100000 + (++bombSeq);
        SpawnBomb(id, p.Slot, origin, vel);
        Net?.SendBomb(id, p.Slot, origin, vel);
        return true;
    }

    public void SpawnBomb(int id, int owner, Vector3 origin, Vector3 vel)
    {
        if (bombs.Exists(b => b.Id == id)) return;
        var t = new GameObject("Bomba " + id).transform; t.position = origin;
        EscapeKit.Shape(PrimitiveType.Sphere, t, "Cuerpo", Vector3.zero, Vector3.one * .5f, EscapeKit.Paint(new Color(.12f, .12f, .15f), .7f), false);
        EscapeKit.Shape(PrimitiveType.Cylinder, t, "Mecha", new Vector3(0, .3f, 0), new Vector3(.08f, .08f, .08f), EscapeKit.Paint(new Color(.6f, .5f, .35f), .1f), false);
        EscapeKit.Shape(PrimitiveType.Sphere, t, "Chispa", new Vector3(0, .4f, 0), Vector3.one * .16f, EscapeKit.Glow(new Color(1f, .7f, .2f), 3f), false);
        bombs.Add(new Bomb { Id = id, Owner = owner, Pos = origin, Vel = vel, Born = Time.time, T = t });
        bool near = Human != null && Vector3.Distance(Human.transform.position, origin) < 14f;
        ColinaAudio.Play("throw", near ? .6f : .25f);
    }

    void TickBombs()
    {
        float dt = Mathf.Min(Time.deltaTime, .05f);
        for (int i = bombs.Count - 1; i >= 0; i--)
        {
            var b = bombs[i]; float age = Time.time - b.Born;
            Vector3 next = b.Pos + b.Vel * dt; b.Vel.y -= BombGravity * dt;
            bool boom = age > BombFuse;
            if (!boom && Physics.Linecast(b.Pos, next, out var hit, ~0, QueryTriggerInteraction.Ignore))
            {
                bool self = age < .3f && b.Owner >= 0 && b.Owner < Players.Count && hit.collider.gameObject == Players[b.Owner].gameObject;
                if (!self) { boom = true; next = hit.point; }
            }
            if (!boom)
                foreach (var p in Players)
                    if (!p.Out && (p.Slot != b.Owner || age > .3f) && Vector3.Distance(p.transform.position + Vector3.up * .8f, next) < .75f) { boom = true; break; }
            b.Pos = next;
            if (b.T != null) { b.T.position = next; b.T.Rotate(Vector3.right, 400f * dt); }
            if (next.y < VoidY - 8f) { Remove(i); continue; }
            if (boom) { Explode(b.Pos, b.Owner); Remove(i); }
        }
    }

    void Remove(int i) { if (bombs[i].T != null) Destroy(bombs[i].T.gameObject); bombs.RemoveAt(i); }

    void Explode(Vector3 at, int owner)
    {
        MultiverseFX.Burst(at, new Color(1f, .6f, .2f), 26, 7); SurvivalKit.Puff(at, new Color(.35f, .33f, .35f), 8, .9f);
        float hd = Human != null ? Vector3.Distance(Human.transform.position, at) : 99f;
        ColinaAudio.Play("boom", hd < 16f ? 1f : .35f);
        if (hd < 9f) trauma = Mathf.Max(trauma, .55f * (1f - hd / 9f) + .15f);
        foreach (var p in Players)
        {
            if (p.Puppet || p.Out) continue;   // every computer pushes only its own players
            Vector3 d = p.transform.position + Vector3.up * .8f - at; float dist = d.magnitude;
            if (dist > BombRadius) continue;
            Vector3 flat = new Vector3(d.x, 0, d.z);
            if (flat.sqrMagnitude < .01f) flat = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
            float k = Mathf.Lerp(1f, .45f, dist / BombRadius) * (p.Slot == owner ? .6f : 1f);
            p.Knock((flat.normalized * 12f + Vector3.up * 6.5f) * k, .8f);
        }
    }

    // ------------------------------------------------------------------ shoves (host)

    void HostKnocks()
    {
        foreach (var a in Players)
        {
            if (!a.Dashing || a.Out) continue;
            foreach (var b in Players)
            {
                if (a == b || b.Out || b.Stunned) continue;
                Vector3 d = b.transform.position - a.transform.position; d.y = 0;
                if (d.magnitude > 1.25f || Mathf.Abs(b.transform.position.y - a.transform.position.y) > 1.5f) continue;
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

    // ------------------------------------------------------------------ end

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
        Vector3 summit = new Vector3(0, SummitY - 1f, 0);
        Vector3 want; float dist = 23f; var h = Human;
        if (State == Phase.Finished && WinnerSlot >= 0) { want = Players[WinnerSlot].transform.position; dist = 12f; }
        else if (State != Phase.Playing || h == null || h.Out) { want = new Vector3(0, 2f, -2f); dist = 34f; }
        else want = Vector3.Lerp(summit, h.transform.position, .55f);
        focus = snap ? want : Vector3.Lerp(focus, want, Time.deltaTime * 3f); snap = false;
        var rot = Quaternion.Euler(48f, 0, 0);
        Vector3 pos = focus - rot * Vector3.forward * dist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .5f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 2f); }
        cam.transform.SetPositionAndRotation(pos, rot);
    }

    // ------------------------------------------------------------------ client

    public void ApplyWorld(Phase phase, float countdown, float elapsed, float[] points, int flagOwner, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) ColinaAudio.UI("beep", .7f);
        if (phase == Phase.Playing && State != Phase.Playing) { ColinaAudio.UI("go", .9f); Big("¡A LA CIMA!", new Color(1f, .85f, .4f)); Elapsed = elapsed; }
        Countdown = countdown;
        if (phase == Phase.Playing) { float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .5f ? elapsed : Elapsed + diff * .25f; }
        for (int s = 0; s < Points.Length && s < points.Length; s++) Points[s] = points[s];
        FlagOwner = flagOwner;
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
