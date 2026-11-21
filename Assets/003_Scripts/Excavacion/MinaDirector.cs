using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// PICO, PALA Y TIJERA (2026-11-17) · party minigame for 2–6 players: a race to the treasure at the bottom.
/// Every player digs straight down through the same column of blocks: dirt needs the shovel (1), stone the pickaxe (2),
/// leaves the shears (3); hard stone takes two hits and gold breaks with anything. Pick the right tool and click; the
/// wrong one stuns you for a moment. First to reach the treasure wins; when time runs out, the deepest wins.
/// Everything is built at runtime; the scene only holds this component, a camera and a light.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class MinaDirector : MonoBehaviour
{
    public const int Depth = 100;
    public const float Spacing = 2.4f, RoundSeconds = 120f, CountdownSeconds = 4f, FinishGrace = 0f;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Running, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float Remaining => Mathf.Max(0f, RoundSeconds - Elapsed);
    public MinaKit.Block[] Seq { get; private set; }
    public readonly List<MinaMiner> Miners = new();
    public MinaMiner Human { get; private set; }
    public MinaCamera View { get; private set; }
    public MinaNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string Notice { get; private set; } = ""; float noticeUntil;
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool InputFree => !Paused && State == Phase.Running && Human != null && !Human.Finished;
    readonly List<MinaMiner> arrivals = new();
    bool tenLeft;

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 6007 + PartyLink.Round * 911) : Random.Range(1, 999999);
        Seq = MinaKit.Sequence(Seed, Depth);

        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        // Same order on every computer: by party member.
        var order = Enumerable.Range(0, count).Select(i => (slot: i, member: PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i)))
                              .OrderBy(p => PartyLink.Active ? p.member : p.slot).Select(p => p.slot).ToList();
        var byColumn = new int[count];
        for (int c = 0; c < count; c++) byColumn[order[c]] = c;
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            float x = (byColumn[i] - (count - 1) * .5f) * Spacing;
            var go = new GameObject(i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1));
            var m = go.AddComponent<MinaMiner>();
            bool puppet = PartyLink.IsPuppet(i);
            m.Init(this, i, member, go.name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, x, characterPrefab, unchecked(Seed + member * 4441 + 9));
            Miners.Add(m);
            if (i == 0) Human = m;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<MinaBot>();
        }
        BuildWorld(count);

        var cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        View = cam.GetComponent<MinaCamera>(); if (View == null) View = cam.gameObject.AddComponent<MinaCamera>(); View.Director = this;
        gameObject.AddComponent<MinaHUD>().Director = this;
        MinaAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<MinaNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; MinaAudio.Release(); }

    void BuildWorld(int count)
    {
        var root = new GameObject("Mina").transform;
        float half = (count - 1) * .5f * Spacing + 1.2f;
        RenderSettings.ambientLight = new Color(.55f, .5f, .48f);
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(.08f, .06f, .05f);
        RenderSettings.fogStartDistance = 18f; RenderSettings.fogEndDistance = 45f;

        // The earth in section: three strata as big tiled slabs (back wall + the earth between the shafts).
        float left = -half - 4f, right = half + 4f;
        var strata = new[] { (MinaKit.Block.Tierra, 0f, Depth * .3f, new Color(.62f, .55f, .5f)), (MinaKit.Block.Piedra, Depth * .3f, Depth * .65f, new Color(.55f, .52f, .52f)), (MinaKit.Block.PiedraDura, Depth * .65f, Depth + 3f, new Color(.7f, .7f, .75f)) };
        var shafts = Enumerable.Range(0, count).Select(c => (c - (count - 1) * .5f) * Spacing).ToList();
        var edges = new List<(float a, float b)>();
        float cursor = left;
        foreach (float sx in shafts) { edges.Add((cursor, sx - .5f)); cursor = sx + .5f; }
        edges.Add((cursor, right));
        foreach (var (look, top, bottom, tint) in strata)
        {
            float h = Mathf.Round(bottom) - Mathf.Round(top), cy = -(Mathf.Round(top) + h * .5f);
            var back = new Material(MinaKit.BlockMat(look)) { name = "Pared " + look, color = tint * .8f };
            back.mainTextureScale = new Vector2(right - left, h); if (back.HasProperty("_BaseMap")) back.SetTextureScale("_BaseMap", new Vector2(right - left, h));
            Cube(root, new Vector3((left + right) * .5f, cy, 1f), new Vector3(right - left, h, 1f), back);
            foreach (var (ea, eb) in edges)
            {
                float w = eb - ea; if (w < .05f) continue;
                var fill = new Material(MinaKit.BlockMat(look)) { name = "Tierra " + look, color = tint * .62f };
                fill.mainTextureScale = new Vector2(w, h); if (fill.HasProperty("_BaseMap")) fill.SetTextureScale("_BaseMap", new Vector2(w, h));
                Cube(root, new Vector3((ea + eb) * .5f, cy, 0f), new Vector3(w, h, 1f), fill);
            }
        }
        // Gems and torches on the back wall.
        var rng = new System.Random(Seed);
        Color[] gems = { new Color(.3f, .9f, 1f), new Color(1f, .3f, .5f), new Color(.5f, 1f, .4f), new Color(.8f, .5f, 1f) };
        for (int i = 0; i < Depth / 2; i++)
        {
            var (ea, eb) = edges[rng.Next(edges.Count)]; if (eb - ea < .4f) continue;
            var at = new Vector3(ea + .2f + (float)rng.NextDouble() * (eb - ea - .4f), -2f - (float)rng.NextDouble() * Depth, -.5f);
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Gema", at, Vector3.one * (.12f + (float)rng.NextDouble() * .12f), EscapeKit.Glow(gems[rng.Next(gems.Length)], 1.5f), false);
        }
        for (int y = 4; y < Depth; y += 6)
            foreach (float side in new[] { -half - .9f, half + .9f })
            {
                EscapeKit.Block(root, "Antorcha", new Vector3(side, -y + .2f, -.6f), new Vector3(.1f, .5f, .1f), EscapeKit.Paint(new Color(.45f, .3f, .18f)), .02f, false);
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Llama", new Vector3(side, -y + .52f, -.6f), new Vector3(.18f, .26f, .18f), EscapeKit.Glow(new Color(1f, .6f, .2f), 2.4f), false);
                EscapeKit.PointLight(root, new Vector3(side, -y + .6f, -1.4f), new Color(1f, .72f, .45f), 9f, 2.2f);
            }
        // Bottom: treasure cave.
        Cube(root, new Vector3(0, -Depth - 1.5f, .5f), new Vector3(half * 2f + 8f, 1f, 2f), MinaKit.Tinted("Fondo", MinaKit.Block.PiedraDura, new Color(.5f, .5f, .55f)));
        EscapeKit.PointLight(root, new Vector3(0, -Depth - .2f, -1f), new Color(1f, .85f, .4f), 12f, 3f);

        // Surface: grass, sky, trees and a sign.
        var grass = EscapeKit.Paint(new Color(.38f, .72f, .3f), .1f);
        EscapeKit.Block(root, "Césped", new Vector3(0, .05f, .5f), new Vector3(half * 2f + 8f, .1f, 3f), grass, .03f, false);
        for (int c = 0; c < count; c++) EscapeKit.Block(root, "Césped columna", new Vector3((c - (count - 1) * .5f) * Spacing, -.02f, 0), new Vector3(1f, .06f, 1f), grass, .02f, false);
        var trunk = EscapeKit.Paint(new Color(.45f, .3f, .18f)); var leaf = MinaKit.BlockMat(MinaKit.Block.Hojas);
        foreach (float x in new[] { -half - 2.6f, half + 2.4f, -half - 4.6f, half + 4.8f })
        {
            float h = 1.6f + Mathf.Abs(x) % 1.1f;
            EscapeKit.Block(root, "Tronco", new Vector3(x, h * .5f, 1.2f), new Vector3(.4f, h, .4f), trunk, .03f, false);
            Cube(root, new Vector3(x, h + .5f, 1.2f), new Vector3(1.6f, 1.2f, 1.6f), leaf);
        }
        var sky = new GameObject("Cielo").AddComponent<Light>(); sky.type = LightType.Directional; sky.intensity = .5f; sky.color = new Color(1f, .95f, .85f);
        sky.transform.rotation = Quaternion.Euler(40f, 20f, 0f); sky.shadows = LightShadows.None;
        var signTm = EscapeKit.Text(root, "¡A CAVAR!", new Vector3(-half - 1.8f, 2.2f, .3f), Vector3.zero, .05f, new Color(1f, .85f, .3f));
        signTm.transform.rotation = Quaternion.identity;
    }

    static GameObject Cube(Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false); go.transform.position = pos; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat; return go;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        float dt = Time.deltaTime;
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting: if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; } break;
                case Phase.Countdown:
                    float before = Countdown; Countdown -= dt;
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) MinaAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { State = Phase.Running; Elapsed = 0f; MinaAudio.UI("go", .8f); Net?.SendState(); }
                    break;
                case Phase.Running:
                    Elapsed += dt;
                    if (arrivals.Count > 0) Finish(arrivals[0]);
                    else if (Elapsed >= RoundSeconds) Finish(Miners.OrderByDescending(m => m.Depth).ThenBy(m => m.Mistakes).First());
                    break;
            }
        }
        else if (State == Phase.Running) Elapsed += dt;
        if (State == Phase.Running && !tenLeft && Remaining <= 10f) { tenLeft = true; Notify("¡Quedan 10 segundos!"); MinaAudio.UI("alarm", .6f); }
        if (Time.time > noticeUntil) Notice = "";
    }

    public void Notify(string text) { Notice = text; noticeUntil = Time.time + 3f; }

    public void OnFinished(MinaMiner m)
    {
        if (!arrivals.Contains(m)) arrivals.Add(m);
        if (arrivals.Count == 1) { Notify("¡" + m.DisplayName + " llegó al tesoro!"); MultiverseFX.Rainbow(m.transform.position + Vector3.up, 30, 6); }
    }

    void Finish(MinaMiner winner)
    {
        if (State == Phase.Finished) return;
        State = Phase.Finished;
        WinnerSlot = winner != null ? Miners.IndexOf(winner) : -1;
        Result = winner == null ? "SIN GANADOR" : winner.Finished ? "¡" + winner.DisplayName.ToUpperInvariant() + " ENCONTRÓ EL TESORO!" : "¡GANA " + winner.DisplayName.ToUpperInvariant() + "! (LLEGÓ MÁS HONDO)";
        Celebrate();
        Net?.SendState();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Miners.Count) Miners[WinnerSlot].Celebrating = true;
        MinaAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f);
        MinaAudio.FadeMusic(.15f);
        Paused = false;
    }

    public void ApplyNet(Phase phase, float countdown, float elapsed, int winnerSlot, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown)
        {
            if (Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) MinaAudio.UI("beep", .7f);
            Countdown = countdown; State = Phase.Countdown;
        }
        else if (phase == Phase.Running)
        {
            if (State != Phase.Running) { State = Phase.Running; Elapsed = elapsed; MinaAudio.UI("go", .8f); }
            float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .35f ? elapsed : Elapsed + diff * .25f;
        }
        else if (phase == Phase.Finished)
        {
            State = Phase.Finished; WinnerSlot = winnerSlot; Result = result;
            Celebrate(); PartyLink.Finish(-1);
        }
        else State = phase;
    }

    /// <summary>Treasure first (by arrival), then the deepest, then fewer mistakes.</summary>
    public List<MinaMiner> Ranking() =>
        Miners.OrderByDescending(m => Miners.IndexOf(m) == WinnerSlot).ThenByDescending(m => m.Finished).ThenBy(m => m.Finished ? m.FinishedAt : 0f)
              .ThenByDescending(m => m.Depth).ThenBy(m => m.Mistakes).ToList();

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}

/// <summary>Side view of all the columns; follows the player down (or the leader once you are done).</summary>
public sealed class MinaCamera : MonoBehaviour
{
    public MinaDirector Director;
    float y, trauma; bool snap = true;
    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));
    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>(); cam.fieldOfView = 50f; cam.nearClipPlane = .1f; cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .78f, .95f);
        var focus = Director.Human;
        if (focus == null || focus.Finished || Director.State == MinaDirector.Phase.Finished)
            focus = Director.WinnerSlot >= 0 ? Director.Miners[Director.WinnerSlot] : Director.Miners.OrderByDescending(m => m.Depth).FirstOrDefault();
        float targetY = focus != null ? focus.transform.position.y : 0f;
        y = snap ? targetY : Mathf.Lerp(y, targetY, Time.deltaTime * 6f); snap = false;
        int n = Mathf.Max(2, Director.Miners.Count);
        float width = (n - 1) * MinaDirector.Spacing + 3.6f;
        float halfH = Mathf.Atan(Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * cam.aspect);
        float dist = Mathf.Max(8.5f, width * .5f / Mathf.Tan(halfH) * 1.22f + 1f);
        Vector3 pos = new Vector3(0, y + 1.2f, -dist);
        if (trauma > 0f) { pos += (Vector3)Random.insideUnitCircle * trauma * trauma * .25f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 2.5f); }
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(new Vector3(0, y - 1.3f, 0) - new Vector3(0, y + 1.2f, -dist));
        // Above ground: blue sky; underground: dark earth behind everything.
        cam.backgroundColor = y > -2f ? new Color(.55f, .78f, .95f) : new Color(.08f, .06f, .05f);
    }
}
