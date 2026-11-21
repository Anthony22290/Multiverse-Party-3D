using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One flyer of Propulsores: a party character with a jetpack. Tap (Space / click / W / A) to thrust upwards; gravity
/// does the rest. Passing a tower scores a point; touching a tower or the ground is a crash: −2 points, a tumble and
/// a short respawn with a blinking shield. The owner computer simulates it; others see a puppet.
/// </summary>
public sealed class FlapPlayer : MonoBehaviour
{
    public const float Gravity = 22f, Thrust = 6.8f, MaxFall = -12f, Radius = .42f, RespawnSeconds = 1.3f, ShieldSeconds = 1.6f;

    public FlapDirector Director { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    public int Score { get; private set; }
    public int Crashes { get; private set; }
    public int Flaps { get; private set; }
    public float ReachedAt { get; private set; } = 9999f;
    public bool Down => Dead || Time.time < respawnAt;
    /// <summary>2026-11-17: one spare life. The second crash is out and the score stays as it was.</summary>
    public int Lives { get; private set; } = FlapDirector.StartLives;
    public bool Dead { get; private set; }
    public float DeadAt { get; private set; } = 9999f;
    public bool Shielded => Time.time < shieldUntil;
    public bool Celebrating { get; set; }
    public float X { get; private set; }
    public float VelY => vy;
    public bool InFlap;
    public static bool Debugging; public string LastCrash = "";
    public readonly NetPose Net = new NetPose();

    float vy, respawnAt, shieldUntil, thrustGlow; int nextPipe; Vector3 home;
    Transform visual, flames, tagRoot; Animator anim; string animState = ""; TextMesh scoreText; Renderer[] renderers;

    public void Init(FlapDirector d, int slot, int member, string name, Color color, bool human, bool puppet, Vector3 at, GameObject prefab, int lookSeed)
    {
        Director = d; Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet; X = at.x; home = at;
        transform.position = at; transform.rotation = Quaternion.LookRotation(Vector3.right);
        visual = new GameObject("Cuerpo").transform; visual.SetParent(transform, false);
        if (prefab != null)
        {
            var go = Instantiate(prefab, visual, false); go.name = "Fiestero";
            var b = PirateArenaVisuals.BoundsOf(go); go.transform.localScale *= 1.3f / Mathf.Max(.1f, b.size.y);
            b = PirateArenaVisuals.BoundsOf(go); go.transform.position += new Vector3(0, transform.position.y - b.center.y, 0);
            anim = go.GetComponentInChildren<Animator>(); if (anim != null) anim.applyRootMotion = false;
            var look = go.GetComponent<CharacterLook>();
            if (look != null)
            {
                var saved = Random.state; Random.InitState(lookSeed); look.Init(); look.Randomize();
                int ropa = System.Array.IndexOf(CharacterLook.ColorSlots, "Ropa");
                if (ropa >= 0)
                {
                    var opts = CharacterLook.ColorOptions[ropa]; int best = 0; float bd = 9f;
                    for (int i = 0; i < opts.Length; i++) { float dd = Mathf.Abs(opts[i].r - color.r) + Mathf.Abs(opts[i].g - color.g) + Mathf.Abs(opts[i].b - color.b); if (dd < bd) { bd = dd; best = i; } }
                    look.colorSel[ropa] = best; look.Apply();
                }
                Random.state = saved;
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Espalda_")) r.gameObject.SetActive(false);   // the jetpack goes on the back
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else EscapeKit.Shape(PrimitiveType.Capsule, visual, "Cápsula", Vector3.zero, new Vector3(.6f, .6f, .6f), EscapeKit.Paint(color), false);

        // Jetpack: two tanks in the player's colour and flames.
        var pack = new GameObject("Propulsor").transform; pack.SetParent(visual, false); pack.localPosition = new Vector3(0, .05f, -.32f);
        var tank = EscapeKit.Paint(color, .4f); var metal = EscapeKit.Paint(new Color(.75f, .78f, .84f), .6f);
        foreach (float x in new[] { -.13f, .13f })
        {
            EscapeKit.Shape(PrimitiveType.Capsule, pack, "Depósito", new Vector3(x, 0, 0), new Vector3(.2f, .26f, .2f), tank, false);
            EscapeKit.Shape(PrimitiveType.Cylinder, pack, "Tobera", new Vector3(x, -.3f, 0), new Vector3(.14f, .06f, .14f), metal, false);
        }
        EscapeKit.Block(pack, "Correa", new Vector3(0, .05f, .12f), new Vector3(.44f, .08f, .08f), metal, .02f, false);
        flames = new GameObject("Llamas").transform; flames.SetParent(pack, false); flames.localPosition = new Vector3(0, -.48f, 0);
        var flameMat = EscapeKit.Glow(new Color(1f, .6f, .15f), 2.6f); var core = EscapeKit.Glow(new Color(1f, .95f, .5f), 3f);
        foreach (float x in new[] { -.13f, .13f })
        {
            EscapeKit.Shape(PrimitiveType.Sphere, flames, "Llama", new Vector3(x, 0, 0), new Vector3(.14f, .3f, .14f), flameMat, false);
            EscapeKit.Shape(PrimitiveType.Sphere, flames, "Núcleo", new Vector3(x, .05f, 0), new Vector3(.07f, .16f, .07f), core, false);
        }

        tagRoot = new GameObject("Etiqueta").transform; tagRoot.SetParent(transform, false); tagRoot.localPosition = Vector3.up * 1.05f;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText = new GameObject("Nombre").AddComponent<TextMesh>(); scoreText.transform.SetParent(tagRoot, false);
        scoreText.font = font; scoreText.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        scoreText.anchor = TextAnchor.MiddleCenter; scoreText.alignment = TextAlignment.Center; scoreText.fontSize = 48; scoreText.characterSize = .035f; scoreText.color = color; scoreText.fontStyle = FontStyle.Bold;
        RefreshTag();
        Play("Idle");
    }

    void RefreshTag() { if (scoreText != null) scoreText.text = (IsHuman ? DisplayName + " (tú)" : DisplayName) + "  " + Score + (Lives > 1 ? "  ♥♥" : Lives == 1 ? "  ♥" : ""); }

    void Update()
    {
        if (Director == null) return;
        if (Puppet && Dead) { var q = transform.position; vy -= Gravity * Time.deltaTime; q.y += vy * Time.deltaTime; transform.position = q; visual.Rotate(Vector3.right, 500f * Time.deltaTime, Space.Self); if (q.y < -6f) visual.gameObject.SetActive(false); return; }
        if (Puppet) { if (Net.Get(out var pos, out _)) { vy = (pos.y - transform.position.y) / Mathf.Max(Time.deltaTime, .001f); transform.position = pos; } Present(); return; }
        if (IsHuman && GetComponent<FlapBot>() == null) ReadInput();
        float dt = Mathf.Min(Time.deltaTime, FlapDirector.MaxFrame);
        var p = transform.position;
        bool running = Director.State == FlapDirector.Phase.Running;
        if (!running || Celebrating)
        {   // Hover gently before the start and after the end.
            p.y = Mathf.Lerp(p.y, home.y + Mathf.Sin(Time.time * 2f + Slot) * .25f, dt * 3f); vy = 0f; InFlap = false;
            transform.position = p; Present(); return;
        }
        if (Dead)
        {
            vy -= Gravity * dt; p.y += vy * dt; p.x -= 2.5f * dt; transform.position = p;
            visual.Rotate(Vector3.right, 500f * dt, Space.Self);
            if (p.y < -6f && visual.gameObject.activeSelf) { visual.gameObject.SetActive(false); tagRoot.gameObject.SetActive(false); }
            return;
        }
        if (Down)
        {
            InFlap = false;
            vy -= Gravity * dt; p.y = Mathf.Max(FlapDirector.Floor - .2f, p.y + vy * dt);
            visual.Rotate(Vector3.right, 600f * dt, Space.Self);
            transform.position = p; Present(); return;
        }
        if (Time.time >= respawnAt && respawnAt > 0f) Respawn(ref p);

        if (InFlap) { vy = Thrust; Flaps++; thrustGlow = .25f; FlapAudio.Play("flap", p, IsHuman ? .5f : .12f); InFlap = false; }
        vy = Mathf.Max(MaxFall, vy - Gravity * dt);
        p.y += vy * dt;
        if (p.y > FlapDirector.Ceiling) { p.y = FlapDirector.Ceiling; vy = Mathf.Min(vy, 0f); }
        transform.position = p;

        // Towers: score when one goes past, crash when touching one or the ground.
        int k = Director.PipeAhead(X - FlapDirector.PipeHalf - Radius - .01f);
        if (k > nextPipe) { int gained = k - nextPipe; nextPipe = k; AddPoints(gained); }
        if (!Shielded)
        {
            bool crash = p.y - Radius < FlapDirector.Floor; if (crash) LastCrash = $"suelo y={p.y:0.00}";
            for (int j = Mathf.Max(0, k - 1); j <= k + 1 && !crash; j++)
            {
                float px = Director.PipeX(j);
                if (Mathf.Abs(px - X) > FlapDirector.PipeHalf + Radius * .8f) continue;
                float gy = Director.GapY(j), half = FlapDirector.GapSize(j) * .5f;
                if (p.y - Radius * .8f < gy - half || p.y + Radius * .8f > gy + half) { crash = true; LastCrash = $"torre {j} x={px - X:0.00} y={p.y:0.00} hueco={gy:0.00}±{half:0.00} vy={vy:0.0}"; }
            }
            if (crash) Crash();
        }
        Present();
    }

    void ReadInput()
    {
        if (!Director.InputFree) return;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        if (k != null && (k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame)) InFlap = true;
        if (m != null && m.leftButton.wasPressedThisFrame) InFlap = true;
        if (g != null && (g.buttonSouth.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame)) InFlap = true;
    }

    void AddPoints(int n)
    {
        if (Down) return;
        Score += n; RefreshTag();
        if (Score >= FlapDirector.Goal && ReachedAt > 9000f) ReachedAt = Director.Elapsed;
        if (IsHuman) { FlapAudio.UI("point", .55f); MultiverseFX.FloatText(transform.position + Vector3.up * 1.4f, "+1", new Color(1f, .95f, .4f), .8f, .7f); }
    }

    void Crash()
    {
        if (Debugging) Debug.LogWarning("Propulsores: " + DisplayName + " chocó · " + LastCrash + " · t=" + Director.Elapsed.ToString("0.0"));
        Crashes++; Lives--; RefreshTag();
        vy = 5f;
        if (Lives <= 0) { Dead = true; DeadAt = Director.Elapsed; Director.OnDied(this); }
        else respawnAt = Time.time + RespawnSeconds;
        FlapAudio.Play("crash", transform.position, IsHuman ? 1f : .4f);
        MultiverseFX.Burst(transform.position, Color, 16, 6);
        MultiverseFX.FloatText(transform.position + Vector3.up * 1.4f, Lives > 0 ? "¡ÚLTIMA VIDA!" : "¡FUERA!", new Color(1f, .4f, .35f), 1f, .9f);
        if (IsHuman) Director.Shake(.6f);
        Director.FeedLine(DisplayName + (Lives > 0 ? " se estrelló · le queda 1 vida" : " se estrelló · ¡fuera con " + Score + " puntos!"));
        Play("Hit");
    }

    void Respawn(ref Vector3 p)
    {
        respawnAt = 0f; shieldUntil = Time.time + ShieldSeconds;
        int k = Director.PipeAhead(X);
        p.y = Director.GapY(k); vy = 2f; visual.localRotation = Quaternion.identity;
        nextPipe = Director.PipeAhead(X - FlapDirector.PipeHalf - Radius - .01f);
        FlapAudio.Play("respawn", p, IsHuman ? .6f : .2f);
    }

    // Network puppets.
    public void ApplyNet(Vector3 pos, int score, int crashes, bool down, bool shielded, float reachedAt, bool celebrate, int lives, bool dead, float deadAt)
    {
        Lives = lives;
        if (dead && !Dead) { Dead = true; DeadAt = deadAt; vy = 5f; Director.OnDied(this); }
        Net.Add(pos, Quaternion.identity);
        if (crashes > Crashes) { MultiverseFX.Burst(pos, Color, 12, 5); FlapAudio.Play("crash", pos, .35f); }
        bool scoreChanged = score != Score;
        Score = score; Crashes = crashes; Celebrating = celebrate; if (reachedAt < 9000f) ReachedAt = reachedAt;
        if (down) respawnAt = Time.time + .15f; else respawnAt = 0f;
        if (shielded) shieldUntil = Time.time + .15f;
        if (scoreChanged) RefreshTag();
    }

    void Present()
    {
        thrustGlow = Mathf.Max(0f, thrustGlow - Time.deltaTime);
        if (flames != null) { float s = Down ? 0f : .5f + thrustGlow * 3f + Mathf.Sin(Time.time * 40f) * .08f; flames.localScale = Vector3.one * Mathf.Max(.01f, s); }
        if (!Down && visual != null) visual.localRotation = Quaternion.Euler(Mathf.Clamp(-vy * 3.5f, -30f, 40f), 0, 0);
        bool show = !Shielded || Mathf.Repeat(Time.time * 10f, 1f) < .6f;
        if (renderers == null) renderers = visual.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) if (r != null) r.enabled = show;
        if (tagRoot != null && Director.Cam != null) tagRoot.rotation = Director.Cam.transform.rotation;
        Play(Celebrating ? "Victory" : Down ? "Hit" : thrustGlow > 0f ? "Jump" : "Idle");
    }
    void Play(string s) { if (anim == null || s == animState) return; animState = s; anim.CrossFadeInFixedTime(s, .1f); }
}
