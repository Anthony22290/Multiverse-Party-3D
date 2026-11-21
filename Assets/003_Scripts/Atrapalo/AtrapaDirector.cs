using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ¡ATRÁPALO! (2026-11-18) · reflex party minigame for 2–6 players on a TV-show stage. A cannon fires one thing at a
/// time towards the contestants: whoever presses first (Space / click) catches it. Fruit = +1, golden pineapple = +2,
/// an old boot or a bomb = −1 life (better not to press!). Pressing before the shot is a false start (out of that
/// throw). 3 lives; first to 10 points wins, or the best after 30 throws or when only one is left with lives.
/// Online, each computer times its own reaction against the shared clock (so lag does not matter much) and the
/// host gives the throw to the fastest.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class AtrapaDirector : MonoBehaviour
{
    public const float CountdownSeconds = 4f, Flight = 1.5f, CatchWindow = 1.45f, ResolveSlack = .4f, AfterThrow = 1.3f;
    public const int StartLives = 3, Goal = 10, MaxThrows = 30;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Playing, Finished }
    public enum Throw { Ready, Flying, Done }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public int ThrowIndex { get; private set; } = -1;
    public Throw ThrowState { get; private set; } = Throw.Done;
    public AtrapaItem.Kind Item { get; private set; }
    public float LaunchAt { get; private set; }
    public int CatcherSlot { get; private set; } = -1;
    public float CatcherReaction { get; private set; }
    public readonly List<AtrapaPlayer> Players = new();
    public AtrapaPlayer Human { get; private set; }
    public AtrapaNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string BigText { get; private set; } = ""; public Color BigColor { get; private set; } = Color.white; float bigUntil;
    public string MyNote { get; private set; } = ""; float myNoteUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    bool paused;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool Dirty { get; set; }
    public float MyReaction { get; private set; } = -1f;   // this computer's human, this throw
    public bool MyFalseStart { get; private set; }

    System.Random rng; float nextThrowAt; float doneAt;
    readonly Dictionary<int, float> presses = new();      // slot → reaction (host)
    readonly HashSet<int> falseStarts = new();             // slots (host)
    readonly Dictionary<int, float> botPlan = new();       // slot → planned reaction, -1 = will not press
    Transform item, cannonBarrel; Vector3 from, to, flyFrom; float catchT; int shownThrow = -1, shownCatcher = -2;
    Camera cam; Vector3 camHome; float trauma; readonly List<Light> showLights = new();

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 9001 + PartyLink.Round * 61) : Random.Range(1, 999999);
        rng = new System.Random(Seed);
        RenderSettings.ambientLight = new Color(.42f, .4f, .5f); RenderSettings.fog = false;
        BuildStage();
        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            float a = count == 1 ? 0f : Mathf.Lerp(-52f, 52f, member / (float)(count - 1));
            var at = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 7.2f, 0, Mathf.Cos(a * Mathf.Deg2Rad) * 7.2f - .4f);
            var p = new GameObject(name).AddComponent<AtrapaPlayer>();
            p.Init(i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, PartyLink.IsPuppet(i), characterPrefab, unchecked(Seed + member * 3301 + 5), at, Vector3.zero);
            p.IsBot = !p.Puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i);
            Players.Add(p); if (i == 0) Human = p;
        }
        cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.08f, .06f, .14f); cam.fieldOfView = 50f;
        camHome = new Vector3(0, 6.2f, 15.5f);
        cam.transform.SetPositionAndRotation(camHome, Quaternion.LookRotation(new Vector3(0, 1.6f, 1.5f) - camHome));
        gameObject.AddComponent<AtrapaHUD>().Director = this;
        AtrapaAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<AtrapaNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; AtrapaAudio.Release(); }

    void BuildStage()
    {
        var root = new GameObject("Plató").transform;
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Escenario", new Vector3(0, -.15f, 4f), new Vector3(22f, .15f, 18f), EscapeKit.Paint(new Color(.2f, .16f, .32f), .5f), false);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Anillo", new Vector3(0, .01f, 4f), new Vector3(20f, .02f, 16f), EscapeKit.Paint(new Color(.95f, .75f, .25f), .6f), false);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Centro", new Vector3(0, .02f, 4f), new Vector3(18.6f, .02f, 14.6f), EscapeKit.Paint(new Color(.3f, .2f, .45f), .6f), false);
        // Curtain and screen behind the cannon.
        var curtain = EscapeKit.Paint(new Color(.6f, .1f, .18f), .2f);
        for (int i = -9; i <= 9; i++) EscapeKit.Block(root, "Telón", new Vector3(i * 1.3f, 4.5f, -6f + Mathf.Abs(i) * .05f), new Vector3(1.25f, 9f, .3f), curtain, .2f, false);
        EscapeKit.Block(root, "Pantalla", new Vector3(0, 5.6f, -5.6f), new Vector3(9f, 3.2f, .2f), EscapeKit.Paint(new Color(.06f, .06f, .1f), .8f), .05f, false);
        EscapeKit.Text(root, "¡ATRÁPALO!", new Vector3(0, 6.1f, -5.45f), new Vector3(0, 180f, 0), .16f, new Color(1f, .8f, .3f));
        EscapeKit.Text(root, "fruta +1   ·   piña dorada +2   ·   bota / bomba = −1 vida", new Vector3(0, 4.9f, -5.45f), new Vector3(0, 180f, 0), .045f, Color.white);
        // Light bulbs round the screen.
        var bulb = EscapeKit.Glow(new Color(1f, .85f, .5f), 2.5f);
        for (int i = 0; i < 24; i++)
        {
            float u = i / 24f; Vector3 p = u < .5f ? new Vector3(Mathf.Lerp(-4.7f, 4.7f, u * 2f), 7.4f, -5.5f) : new Vector3(Mathf.Lerp(4.7f, -4.7f, (u - .5f) * 2f), 3.8f, -5.5f);
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Bombilla", p, Vector3.one * .22f, bulb, false);
        }
        // The cannon.
        var metal = EscapeKit.Paint(new Color(.85f, .2f, .25f), .6f); var gold = EscapeKit.Paint(new Color(1f, .8f, .3f), .7f); var wood = EscapeKit.Paint(new Color(.45f, .3f, .18f), .2f);
        EscapeKit.Block(root, "Cureña", new Vector3(0, .45f, -.4f), new Vector3(1.6f, .6f, 2f), wood, .08f, false);
        foreach (int s in new[] { -1, 1 }) EscapeKit.Shape(PrimitiveType.Cylinder, root, "Rueda", new Vector3(s * .9f, .55f, -.4f), new Vector3(1.1f, .1f, 1.1f), gold, false, new Vector3(0, 0, 90f));
        cannonBarrel = new GameObject("Cañón").transform; cannonBarrel.SetParent(root, false); cannonBarrel.SetPositionAndRotation(new Vector3(0, 1.05f, -.3f), Quaternion.Euler(-28f, 0, 0));
        EscapeKit.Shape(PrimitiveType.Cylinder, cannonBarrel, "Tubo", new Vector3(0, 0, .7f), new Vector3(.7f, .9f, .7f), metal, false, new Vector3(90f, 0, 0));
        EscapeKit.Shape(PrimitiveType.Cylinder, cannonBarrel, "Boca", new Vector3(0, 0, 1.6f), new Vector3(.86f, .08f, .86f), gold, false, new Vector3(90f, 0, 0));
        EscapeKit.Shape(PrimitiveType.Cylinder, cannonBarrel, "Aro", new Vector3(0, 0, .3f), new Vector3(.8f, .06f, .8f), gold, false, new Vector3(90f, 0, 0));
        from = new Vector3(0, 1.85f, 1.25f); to = new Vector3(0, 3.1f, 6.2f);
        // Spotlights.
        foreach (var x in new[] { -6f, 6f })
        {
            var l = new GameObject("Foco").AddComponent<Light>(); l.type = LightType.Spot; l.range = 30f; l.spotAngle = 55f; l.intensity = 3f; l.color = x < 0 ? new Color(1f, .6f, .9f) : new Color(.6f, .8f, 1f);
            l.transform.SetPositionAndRotation(new Vector3(x, 9f, 9f), Quaternion.LookRotation(new Vector3(0, 0, 4f) - new Vector3(x, 9f, 9f))); showLights.Add(l);
        }
        var sun = new GameObject("Luz general").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .7f; sun.transform.rotation = Quaternion.Euler(50f, 160f, 0); sun.shadows = LightShadows.Soft;
        // Audience silhouettes.
        var crowd = EscapeKit.Paint(new Color(.16f, .12f, .24f), .1f);
        for (int i = 0; i < 26; i++)
        {
            float x = -13f + i, z = 17.5f + (i % 3) * .6f;
            EscapeKit.Shape(PrimitiveType.Capsule, root, "Público", new Vector3(x, .6f + (i % 2) * .15f, z), new Vector3(.6f, .7f, .5f), crowd, false);
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
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) AtrapaAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { Countdown = 0f; State = Phase.Playing; Elapsed = 0f; AtrapaAudio.UI("go", .9f); nextThrowAt = .5f; Dirty = true; }
                    break;
                case Phase.Playing: Elapsed += Time.deltaTime; HostThrows(); break;
            }
        }
        else if (State == Phase.Playing) Elapsed += Time.deltaTime;
        LocalInput();
        Present();
        if (Time.time > bigUntil) BigText = "";
        if (Time.time > myNoteUntil) MyNote = "";
    }

    void LateUpdate()
    {
        if (cam == null) return;
        Vector3 pos = camHome;
        if (State == Phase.Finished && WinnerSlot >= 0 && WinnerSlot < Players.Count) pos = Vector3.Lerp(cam.transform.position, Players[WinnerSlot].transform.position + new Vector3(0, 3.2f, 5.5f), Time.deltaTime * 2f);
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .35f; trauma = Mathf.Max(0f, trauma - Time.deltaTime * 2f); }
        cam.transform.position = pos;
        if (State == Phase.Finished && WinnerSlot >= 0 && WinnerSlot < Players.Count) cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(Players[WinnerSlot].transform.position + Vector3.up * 1.6f - cam.transform.position), Time.deltaTime * 3f);
        for (int i = 0; i < showLights.Count; i++) showLights[i].transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(Time.time * .7f + i * 2f) * 4f, 0, 5f) - showLights[i].transform.position);
    }

    void Big(string text, Color c, float seconds = 1.5f) { BigText = text; BigColor = c; bigUntil = Time.time + seconds; }
    void Note(string text) { MyNote = text; myNoteUntil = Time.time + 1.4f; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    // ------------------------------------------------------------------ input (this computer's human)

    void LocalInput()
    {
        if (Paused || State != Phase.Playing || Human == null || Human.IsBot || Human.Out) return;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        bool press = (k != null && (k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                  || (m != null && m.leftButton.wasPressedThisFrame) || (g != null && (g.buttonSouth.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame));
        if (!press || ThrowIndex < 0 || MyReaction >= 0f || MyFalseStart || ThrowState == Throw.Done) return;
        float rt = Elapsed - LaunchAt;
        bool early = rt < 0f;   // judged on this computer's clock, not on when the host's message arrives
        if (!early && rt > CatchWindow) return;
        Human.Act("Jump", .5f);
        if (early) { MyFalseStart = true; Note("¡Muy pronto! Te quedas fuera de este lanzamiento"); AtrapaAudio.UI("buzz", .8f); }
        else { MyReaction = rt; Note("¡YA! " + Mathf.RoundToInt(rt * 1000f) + " ms"); AtrapaAudio.UI("press", .7f); }
        if (IsClient) Net?.SendPress(Human.Member, ThrowIndex, early ? -1f : rt);
        else HostPress(Human.Slot, ThrowIndex, early ? -1f : rt);
    }

    // ------------------------------------------------------------------ host

    void HostThrows()
    {
        if (ThrowState == Throw.Done)
        {
            if (Elapsed < nextThrowAt) return;
            if (EndCheck()) return;
            ThrowIndex++;
            double r = rng.NextDouble();
            Item = r < .14 ? AtrapaItem.Kind.GoldPineapple : r < .40 ? (rng.NextDouble() < .5 ? AtrapaItem.Kind.Boot : AtrapaItem.Kind.Bomb) : (AtrapaItem.Kind)rng.Next(4);
            LaunchAt = Elapsed + .8f + (float)rng.NextDouble() * 1.8f;
            ThrowState = Throw.Ready; CatcherSlot = -1; presses.Clear(); falseStarts.Clear(); botPlan.Clear();
            foreach (var p in Players)
            {
                if (!p.IsBot || p.Out) continue;
                float skill = MultiverseSettings.BotSkill(.35f + (float)(new System.Random(unchecked(Seed + p.Slot * 77)).NextDouble()) * .55f);
                if (AtrapaItem.Bad(Item)) botPlan[p.Slot] = rng.NextDouble() < .22 * (1f - skill) + .03 ? .28f + (float)rng.NextDouble() * .3f : -1f;
                else if (rng.NextDouble() < .05) botPlan[p.Slot] = -2f;   // false start now and then
                else botPlan[p.Slot] = Mathf.Lerp(.62f, .34f, skill) + (float)rng.NextDouble() * .3f - (Item == AtrapaItem.Kind.GoldPineapple ? .04f : 0f);
            }
            Dirty = true;
            return;
        }
        // Bots press.
        foreach (var kv in botPlan.ToList())
        {
            if (kv.Value == -2f && Elapsed > LaunchAt - .5f) { HostPress(kv.Key, ThrowIndex, -1f); botPlan.Remove(kv.Key); Players[kv.Key].Act("Jump", .5f); continue; }
            if (kv.Value >= 0f && Elapsed >= LaunchAt + kv.Value) { HostPress(kv.Key, ThrowIndex, kv.Value); botPlan.Remove(kv.Key); Players[kv.Key].Act("Jump", .5f); }
        }
        if (ThrowState == Throw.Ready && Elapsed >= LaunchAt) { ThrowState = Throw.Flying; Dirty = true; }
        if (ThrowState == Throw.Flying)
        {
            float earliest = presses.Count > 0 ? presses.Values.Min() : float.MaxValue;
            bool timeUp = Elapsed >= LaunchAt + CatchWindow + ResolveSlack;
            if (timeUp || Elapsed >= LaunchAt + earliest + ResolveSlack) Resolve();
        }
    }

    public void HostPress(int slot, int throwIndex, float reaction)
    {
        if (throwIndex != ThrowIndex || ThrowState == Throw.Done || slot < 0 || slot >= Players.Count || Players[slot].Out) return;
        if (presses.ContainsKey(slot) || falseStarts.Contains(slot)) return;
        if (reaction < 0f) { falseStarts.Add(slot); return; }
        presses[slot] = Mathf.Max(.05f, reaction);
    }

    void Resolve()
    {
        int best = -1; float bt = float.MaxValue;
        foreach (var kv in presses) if (kv.Value < bt || (Mathf.Approximately(kv.Value, bt) && rng.NextDouble() < .5)) { bt = kv.Value; best = kv.Key; }
        CatcherSlot = best; CatcherReaction = best >= 0 ? bt : 0f;
        if (best >= 0)
        {
            var p = Players[best];
            if (AtrapaItem.Bad(Item)) p.Lives = Mathf.Max(0, p.Lives - 1); else p.Score += AtrapaItem.Points(Item);
        }
        ThrowState = Throw.Done; doneAt = Elapsed; nextThrowAt = Elapsed + AfterThrow;
        Dirty = true; Net?.SendWorld();
    }

    bool EndCheck()
    {
        int alive = Players.Count(p => !p.Out);
        bool goal = Players.Any(p => p.Score >= Goal);
        if (goal || ThrowIndex + 1 >= MaxThrows || alive <= (Players.Count > 1 ? 1 : 0)) { Finish(); return true; }
        return false;
    }

    void Finish()
    {
        if (State == Phase.Finished) return;
        var best = Ranking().FirstOrDefault();
        State = Phase.Finished; WinnerSlot = best != null ? best.Slot : -1;
        Result = best != null ? "¡" + best.DisplayName.ToUpperInvariant() + " GANA CON " + best.Score + " PUNTOS!" : "SIN GANADOR";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0) { Players[WinnerSlot].Act("Victory", 999f); MultiverseFX.Rainbow(Players[WinnerSlot].transform.position + Vector3.up * 2.5f, 50, 8); }
        AtrapaAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); AtrapaAudio.FadeMusic(.15f); Paused = false;
        if (item != null) item.gameObject.SetActive(false);
    }

    public List<AtrapaPlayer> Ranking() => Players.OrderByDescending(p => p.Score).ThenByDescending(p => p.Lives).ThenBy(p => p.Slot == WinnerSlot ? 0 : 1).ToList();

    // ------------------------------------------------------------------ presentation (every computer)

    void Present()
    {
        if (ThrowIndex != shownThrow)
        {
            shownThrow = ThrowIndex; shownCatcher = -2; MyReaction = -1f; MyFalseStart = false;
            if (item != null) Destroy(item.gameObject);
            item = AtrapaItem.Build(Item); item.localScale = Vector3.one * 1.9f; item.gameObject.SetActive(false);
        }
        if (cannonBarrel != null)
        {
            float wob = ThrowState == Throw.Ready ? Mathf.Sin(Time.time * 18f) * 2f : 0f;
            cannonBarrel.localRotation = Quaternion.Euler(-28f + wob, Mathf.Sin(Time.time * .9f) * 6f, 0);
        }
        if (item == null || State == Phase.Finished) return;
        float u = (Elapsed - LaunchAt) / Flight;
        if (ThrowState == Throw.Ready && u < 0f) { item.gameObject.SetActive(false); return; }
        if (ThrowState != Throw.Done || CatcherSlot < 0)
        {
            if (!item.gameObject.activeSelf && u >= 0f && u < 1.5f)
            {
                item.gameObject.SetActive(true); AtrapaAudio.UI("launch", .9f);
                MultiverseFX.Burst(from, new Color(1f, .9f, .6f), 14, 5); SurvivalKit.Puff(from, new Color(.9f, .9f, .9f), 4, .3f); trauma = .3f;
            }
            // Past the end of the flight it keeps falling behind the podiums.
            Vector3 p = u <= 1f ? Vector3.Lerp(from, to, u) + Vector3.up * 4f * u * (1f - u) * 2.2f : to + new Vector3(0, -(u - 1f) * 7f, (u - 1f) * 4f);
            item.position = p; item.rotation = Quaternion.Euler(Time.time * 200f, Time.time * 130f, 0);
            if (ThrowState == Throw.Done && shownCatcher != -1)
            {
                shownCatcher = -1;
                FeedLine(AtrapaItem.Bad(Item) ? "Nadie toca la " + AtrapaItem.Name(Item) + " · ¡bien hecho!" : "Nadie atrapa la " + AtrapaItem.Name(Item));
            }
            if (u > 1.6f) item.gameObject.SetActive(false);
            catchT = 0f; flyFrom = item.position;
            return;
        }
        // Caught: fly to the catcher's hand.
        if (CatcherSlot >= 0)
        {
            var c = Players[CatcherSlot];
            if (shownCatcher != CatcherSlot)
            {
                shownCatcher = CatcherSlot; catchT = 0f; flyFrom = item.gameObject.activeSelf ? item.position : from; item.gameObject.SetActive(true);
                bool bad = AtrapaItem.Bad(Item);
                c.Act(bad ? "Hit" : "Victory", 1.2f);
                string ms = Mathf.RoundToInt(CatcherReaction * 1000f) + " ms";
                if (bad)
                {
                    AtrapaAudio.UI(Item == AtrapaItem.Kind.Bomb ? "boom" : "bonk", 1f);
                    if (Item == AtrapaItem.Kind.Bomb) { MultiverseFX.Burst(c.transform.position + Vector3.up * 2f, new Color(1f, .5f, .2f), 30, 7); SurvivalKit.Puff(c.transform.position + Vector3.up * 2f, new Color(.3f, .3f, .3f), 7, .6f); }
                    MultiverseFX.FloatText(c.transform.position + Vector3.up * 3.3f, "−1 ♥", new Color(1f, .4f, .4f), 1.4f);
                    FeedLine(c.DisplayName + " atrapa la " + AtrapaItem.Name(Item) + " (" + ms + ") · ¡pierde una vida!" + (c.Out ? " · FUERA" : ""));
                    if (c.IsHuman) { Big(c.Out ? "¡SIN VIDAS! Estás fuera" : "¡AY! Era una " + AtrapaItem.Name(Item) + " · −1 vida", new Color(1f, .45f, .4f), 1.8f); trauma = .7f; }
                }
                else
                {
                    int pts = AtrapaItem.Points(Item);
                    AtrapaAudio.UI(pts == 2 ? "gold" : "catch", c.IsHuman ? 1f : .6f);
                    MultiverseFX.FloatText(c.transform.position + Vector3.up * 3.3f, "+" + pts, c.Color, pts == 2 ? 1.8f : 1.3f);
                    MultiverseFX.Burst(c.transform.position + Vector3.up * 2.2f, pts == 2 ? new Color(1f, .85f, .3f) : c.Color, 14, 5);
                    FeedLine(c.DisplayName + " atrapa la " + AtrapaItem.Name(Item) + " (" + ms + ")" + (pts == 2 ? " · +2" : ""));
                    if (c.IsHuman) Big("¡ATRAPADA! +" + pts + " · " + ms, new Color(.5f, 1f, .55f), 1.3f);
                }
            }
            catchT = Mathf.Min(1f, catchT + Time.deltaTime / .22f);
            item.position = Vector3.Lerp(flyFrom, c.Hand.position, catchT);
            if (Elapsed - doneAt > AfterThrow - .3f) item.gameObject.SetActive(false);
        }
    }

    // ------------------------------------------------------------------ client

    public void ApplyWorld(Phase phase, float countdown, float elapsed, int throwIndex, Throw throwState, AtrapaItem.Kind kind, float launchAt, int catcher, float reaction, int[] scores, int[] lives, int winner, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) AtrapaAudio.UI("beep", .7f);
        if (phase == Phase.Playing && State != Phase.Playing) { AtrapaAudio.UI("go", .9f); Elapsed = elapsed; }
        Countdown = countdown;
        if (phase == Phase.Playing) { float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .35f ? elapsed : Elapsed + diff * .2f; }
        Item = kind; ThrowIndex = throwIndex; LaunchAt = launchAt; CatcherReaction = reaction;
        if (throwState == Throw.Done && ThrowState != Throw.Done) doneAt = Elapsed;
        ThrowState = throwState; CatcherSlot = catcher;
        for (int s = 0; s < Players.Count; s++) { Players[s].Score = scores[s]; Players[s].Lives = lives[s]; }
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
