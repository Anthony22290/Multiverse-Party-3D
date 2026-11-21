using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One player of Rodillos del Calabozo. Kinematic movement on the round platform (no physics engine): run, jump over
/// the low fire roller, duck under the high ice roller. Three lives; a hit knocks you back, falling into the lava is out.
/// The computer that owns the slot simulates it (you, or the host for bots); everybody else sees a network puppet.
/// </summary>
public sealed class RodillosRunner : MonoBehaviour
{
    public const float Speed = 5.6f, JumpSpeed = 8.1f, Gravity = 20f, BodyRadius = .28f, StandHeight = 1.62f, DuckHeight = .98f;
    public const float InvulnSeconds = 1.5f, StunSeconds = .45f;

    public RodillosDirector Director { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }      // the person at this computer
    public bool Puppet { get; private set; }       // simulated on another computer
    public int Lives { get; private set; } = RodillosDirector.Lives;
    public bool Alive { get; private set; } = true;
    public bool Ducking { get; private set; }
    public bool Grounded { get; private set; } = true;
    public bool Stunned => Time.time < stunUntil;
    public bool Invulnerable => Time.time < invulnUntil;
    public int Hits { get; private set; }
    public int Jumps { get; private set; }
    public float EliminatedAt { get; private set; } = -1f;
    public float LastJumpAt { get; private set; } = -99f;
    public bool Celebrating { get; set; }
    public Vector3 Velocity => vel;

    // Input (set by the keyboard for the person here, or by RodillosBot).
    public Vector3 InMove; public bool InJump, InDuck;
    public readonly NetPose Net = new NetPose();

    Vector3 vel; float stunUntil, invulnUntil, jumpBuffer, deathTimer; bool launched, sunk;
    Transform model, visual, tagRoot, hearts; Animator anim; string animState = "";
    Renderer[] renderers; TextMesh nameText; Renderer[] heartRenderers; Material heartOn, heartOff;
    Transform shadow; float duckScale = 1f; bool wasGrounded = true;

    public void Init(RodillosDirector d, int slot, int member, string name, Color color, bool human, bool puppet, GameObject prefab, int lookSeed)
    {
        Director = d; Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet;
        visual = new GameObject("Cuerpo").transform; visual.SetParent(transform, false);
        if (prefab != null)
        {
            var go = Instantiate(prefab, visual, false); go.name = "Fiestero"; model = go.transform;
            var b = PirateArenaVisuals.BoundsOf(go); float h = Mathf.Max(.1f, b.size.y);
            model.localScale *= 1.55f / h;
            b = PirateArenaVisuals.BoundsOf(go); model.position += new Vector3(0, transform.position.y - b.min.y, 0);
            anim = go.GetComponentInChildren<Animator>();
            if (anim != null) anim.applyRootMotion = false;
            var look = go.GetComponent<CharacterLook>();
            if (look != null)
            {
                var saved = Random.state; Random.InitState(lookSeed);
                look.Init(); look.Randomize();
                // Clothes in the player's colour, so everyone is easy to tell apart.
                int ropa = System.Array.IndexOf(CharacterLook.ColorSlots, "Ropa");
                if (ropa >= 0)
                {
                    var opts = CharacterLook.ColorOptions[ropa]; int best = 0; float bd = 9f;
                    for (int i = 0; i < opts.Length; i++) { float dd = Mathf.Abs(opts[i].r - color.r) + Mathf.Abs(opts[i].g - color.g) + Mathf.Abs(opts[i].b - color.b); if (dd < bd) { bd = dd; best = i; } }
                    look.colorSel[ropa] = best; look.Apply();
                }
                Random.state = saved;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else EscapeKit.Shape(PrimitiveType.Capsule, visual, "Cápsula", Vector3.up * .8f, new Vector3(.6f, .8f, .6f), EscapeKit.Paint(color), false);
        renderers = visual.GetComponentsInChildren<Renderer>();

        // Blob shadow: makes jumps easy to read from above.
        shadow = EscapeKit.Shape(PrimitiveType.Cylinder, null, "Sombra de " + name, Vector3.zero, new Vector3(.75f, .006f, .75f), EscapeKit.Paint(new Color(.05f, .04f, .04f)), false).transform;

        // Name tag and lives.
        tagRoot = new GameObject("Etiqueta").transform; tagRoot.SetParent(transform, false); tagRoot.localPosition = Vector3.up * 2.35f;
        nameText = new GameObject("Nombre").AddComponent<TextMesh>(); nameText.transform.SetParent(tagRoot, false);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.font = font; nameText.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        nameText.text = human ? name + " (tú)" : name; nameText.anchor = TextAnchor.MiddleCenter; nameText.alignment = TextAlignment.Center;
        nameText.fontSize = 48; nameText.characterSize = .045f; nameText.color = color; nameText.fontStyle = FontStyle.Bold;
        hearts = new GameObject("Vidas").transform; hearts.SetParent(tagRoot, false); hearts.localPosition = Vector3.down * .32f;
        heartOn = EscapeKit.Glow(new Color(1f, .25f, .3f), 1.4f); heartOff = EscapeKit.Paint(new Color(.2f, .2f, .22f));
        heartRenderers = new Renderer[RodillosDirector.Lives];
        for (int i = 0; i < heartRenderers.Length; i++)
            heartRenderers[i] = EscapeKit.Shape(PrimitiveType.Sphere, hearts, "Vida", new Vector3((i - 1) * .24f, 0, 0), Vector3.one * .17f, heartOn, false).GetComponent<Renderer>();
        Play("Idle");
    }

    void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }

    // ------------------------------------------------------------------ simulation (owner)

    void Update()
    {
        if (Director == null) return;
        if (Puppet) { PuppetTick(); Present(Time.deltaTime); return; }
        if (!Alive) { DeadTick(Time.deltaTime); Present(Time.deltaTime); return; }

        bool canAct = Director.State == RodillosDirector.Phase.Running && !Stunned;
        if (IsHuman && GetComponent<RodillosBot>() == null) ReadInput();
        if (!canAct) { InJump = false; InDuck = Director.State == RodillosDirector.Phase.Running && InDuck; }
        if (InJump) jumpBuffer = .14f;
        InJump = false;
        // Fixed small steps: the same jump arc at any frame rate (and with the test's time scale).
        float total = Mathf.Min(Time.deltaTime, RodillosDirector.MaxFrame);
        int steps = Mathf.Max(1, Mathf.CeilToInt(total / .02f));
        for (int i = 0; i < steps && Alive; i++) Step(total / steps, canAct && !Stunned);
        if (Alive) Present(Time.deltaTime);
    }

    void Step(float dt, bool canAct)
    {
        bool frozen = Director.State != RodillosDirector.Phase.Running && Director.State != RodillosDirector.Phase.Finished;
        Ducking = Grounded && InDuck && canAct;
        jumpBuffer -= dt;
        if (canAct && Grounded && !Ducking && jumpBuffer > 0f)
        {
            vel.y = JumpSpeed; Grounded = false; jumpBuffer = 0f; Jumps++; LastJumpAt = Director.Elapsed;
            RodillosAudio.Play("jump", transform.position, IsHuman ? .8f : .35f);
            Play("Jump");
        }

        Vector3 want = canAct && !frozen ? Vector3.ClampMagnitude(new Vector3(InMove.x, 0, InMove.z), 1f) * Speed * (Ducking ? .42f : 1f) : Vector3.zero;
        float accel = Stunned ? 5f : Grounded ? 38f : 14f;
        Vector3 flat = new Vector3(vel.x, 0, vel.z);
        flat = Vector3.MoveTowards(flat, want, accel * dt);
        vel.x = flat.x; vel.z = flat.z;
        vel.y -= Gravity * dt;
        // Duck in the air = fast fall: jump the fire roller and get down in time for the ice one.
        if (!Grounded && InDuck && canAct && vel.y < 5f) vel.y -= Gravity * 2.2f * dt;

        Vector3 p = transform.position + vel * dt;
        // Hub pillar.
        Vector2 xz = new Vector2(p.x, p.z); float r = xz.magnitude;
        if (r < RodillosDirector.HubRadius && p.y < 4.5f)
        {
            Vector2 n = r > .01f ? xz / r : Vector2.right;
            p.x = n.x * RodillosDirector.HubRadius; p.z = n.y * RodillosDirector.HubRadius;
            float into = Vector2.Dot(new Vector2(vel.x, vel.z), n); if (into < 0) { vel.x -= n.x * into; vel.z -= n.y * into; }
        }
        // Other players: soft bump.
        foreach (var o in Director.Runners)
        {
            if (o == this || !o.Alive) continue;
            Vector3 d = p - o.transform.position; d.y = 0; float dist = d.magnitude;
            if (dist < BodyRadius * 2.2f && Mathf.Abs(p.y - o.transform.position.y) < 1.2f)
            {
                Vector3 push = dist > .01f ? d / dist : Quaternion.Euler(0, Slot * 60f, 0) * Vector3.right;
                p += push * (BodyRadius * 2.2f - dist) * .5f;
            }
        }
        // Floor.
        bool ground = Director.Arena.Ground(p, Director.Elapsed);
        float prevY = transform.position.y;
        if (ground && p.y <= 0f && (p.y > -.4f || prevY >= -.05f) && vel.y <= 0f)
        {
            if (!wasGrounded && vel.y < -4f) RodillosAudio.Play("land", p, IsHuman ? .45f : .2f);
            p.y = 0f; vel.y = 0f; Grounded = true;
        }
        else Grounded = false;
        wasGrounded = Grounded;
        transform.position = p;
        if (want.sqrMagnitude > .01f && !Stunned) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(want), dt * 14f);

        if (p.y < -1.9f) { Eliminate(true); return; }
        if (Director.State == RodillosDirector.Phase.Running) CheckRollers();
    }

    void ReadInput()
    {
        InMove = Vector3.zero; bool jump = false, duck = false;
        if (!Director.InputFree) { InDuck = false; return; }
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        Vector2 axis = Vector2.zero;
        if (k != null)
        {
            axis.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            axis.y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            jump |= k.spaceKey.wasPressedThisFrame;
            duck |= k.leftShiftKey.isPressed || k.rightShiftKey.isPressed || k.leftCtrlKey.isPressed || k.cKey.isPressed;
        }
        if (m != null) { jump |= m.leftButton.wasPressedThisFrame; duck |= m.rightButton.isPressed; }
        if (g != null)
        {
            var s = g.leftStick.ReadValue(); if (s.sqrMagnitude > axis.sqrMagnitude) axis = s;
            jump |= g.buttonSouth.wasPressedThisFrame; duck |= g.buttonEast.isPressed || g.rightTrigger.isPressed || g.leftTrigger.isPressed;
        }
        float yaw = Director.View != null ? Director.View.Yaw : 0f;
        InMove = Quaternion.Euler(0, yaw, 0) * new Vector3(axis.x, 0, axis.y);
        InJump = jump; InDuck = duck;
        if (duck && !wasDucking && Grounded) RodillosAudio.UI("duck", .35f);
        wasDucking = duck;
    }
    bool wasDucking;

    /// <summary>Body top for the high roller test.</summary>
    public float Top => transform.position.y + (Ducking ? DuckHeight : StandHeight);

    void CheckRollers()
    {
        if (Invulnerable) return;
        var spin = Director.Spin; float t = Director.Elapsed;
        Vector3 p = transform.position;
        for (int arm = 0; arm < 2; arm++)
        {
            if (arm == RodillosSpin.High && t < RodillosDirector.HighArrives) continue;
            Vector3 dir = spin.ArmDir(arm);
            Vector3 flat = new Vector3(p.x, 0, p.z);
            float s = Vector3.Dot(flat, dir);
            if (s < RodillosDirector.ArmStart - .3f || s > RodillosDirector.ArmEnd + .2f) continue;
            float perp = (flat - dir * s).magnitude;
            float hit = arm == RodillosSpin.Low ? RodillosDirector.LowHitRadius : RodillosDirector.HighHitRadius;
            if (perp > hit + BodyRadius) continue;
            bool overlap = arm == RodillosSpin.Low
                ? p.y < RodillosDirector.LowHeight + hit - .18f
                : Top > RodillosDirector.HighHeight - hit && p.y < RodillosDirector.HighHeight + hit;
            if (!overlap) continue;
            // Push along the roller's travel, a little outwards and up.
            Vector3 tangent = Vector3.Cross(Vector3.up, dir) * -Mathf.Sign(spin.YawSpeed(arm));
            Vector3 knock = (tangent * 1f + dir * .35f).normalized;
            Hit(arm, knock);
            return;
        }
    }

    void Hit(int arm, Vector3 knock)
    {
        Lives--; Hits++;
        GetComponent<RodillosBot>()?.Report(arm);
        invulnUntil = Time.time + InvulnSeconds; stunUntil = Time.time + StunSeconds;
        vel = knock * 8.5f + Vector3.up * 5.5f; Grounded = false;
        Director.OnHit(this, arm);
        if (Director.Net != null) Director.Net.SendEvent(this, RodillosNet.EvHit, knock);
        HitFx(arm);
        if (Lives <= 0) Eliminate(false, knock);
    }

    void HitFx(int arm)
    {
        Play("Hit");
        RodillosAudio.Play("hit", transform.position, IsHuman ? 1f : .6f);
        MultiverseFX.Burst(transform.position + Vector3.up * 1f, arm == RodillosSpin.Low ? new Color(1f, .55f, .15f) : new Color(.4f, .85f, 1f), 16, 6);
        MultiverseFX.FloatText(transform.position + Vector3.up * 2.6f, Lives > 0 ? "-1" : "¡FUERA!", new Color(1f, .35f, .3f), 1.1f);
        if (IsHuman) Director.View?.Shake(.6f);
    }

    void Eliminate(bool lava, Vector3 knock = default)
    {
        if (!Alive) return;
        Alive = false; Lives = 0; EliminatedAt = Director.Elapsed;
        launched = !lava; sunk = lava; deathTimer = 0f;
        if (!lava) vel = knock * 9f + Vector3.up * 9f;
        if (lava) LavaFx(); else RodillosAudio.Play("eliminated", transform.position, IsHuman ? 1f : .7f);
        Director.OnEliminated(this, lava);
        if (Director.Net != null) Director.Net.SendEvent(this, lava ? RodillosNet.EvLava : RodillosNet.EvOut, knock);
    }

    void LavaFx()
    {
        RodillosAudio.Play("lava", transform.position, IsHuman ? 1f : .7f);
        SurvivalKit.Puff(new Vector3(transform.position.x, -1.5f, transform.position.z), new Color(1f, .5f, .15f), 10, 1.4f);
        MultiverseFX.Burst(new Vector3(transform.position.x, -1.2f, transform.position.z), new Color(1f, .45f, .1f), 20, 7);
        if (IsHuman) Director.View?.Shake(.5f);
    }

    void DeadTick(float dt)
    {
        deathTimer += dt;
        if (launched)
        {
            vel.y -= Gravity * dt;
            transform.position += vel * dt;
            visual.Rotate(Vector3.right, 540f * dt, Space.Self);
            if (transform.position.y < -1.6f) { launched = false; sunk = true; LavaFx(); }
        }
        else if (sunk)
        {
            transform.position += Vector3.down * 1.2f * dt;
            if (transform.position.y < -4f && visual.gameObject.activeSelf) { visual.gameObject.SetActive(false); tagRoot.gameObject.SetActive(false); }
        }
    }

    // ------------------------------------------------------------------ network puppets

    bool netDuck, netGround = true, netStun;
    public void ApplyNet(Vector3 pos, float yaw, bool alive, bool duck, bool grounded, bool stunned, int lives, int hits, bool celebrate)
    {
        Net.Add(pos, Quaternion.Euler(0, yaw, 0));
        netDuck = duck; netGround = grounded; netStun = stunned; Celebrating = celebrate;
        if (alive) { Lives = lives; Hits = hits; }
    }
    /// <summary>A hit or an elimination simulated on another computer.</summary>
    public void NetEvent(byte kind, int lives, float at, Vector3 knock)
    {
        if (kind == RodillosNet.EvHit)
        {
            Lives = lives; Hits++; invulnUntil = Time.time + InvulnSeconds; HitFx(0);
            Director.OnHit(this, -1);
            return;
        }
        if (!Alive) return;
        Alive = false; Lives = 0; EliminatedAt = at;
        launched = kind == RodillosNet.EvOut; sunk = !launched; vel = knock * 9f + Vector3.up * 9f;
        if (sunk) LavaFx(); else RodillosAudio.Play("eliminated", transform.position, .7f);
        Director.OnEliminated(this, kind == RodillosNet.EvLava);
    }
    void PuppetTick()
    {
        if (!Alive) { DeadTick(Time.deltaTime); return; }
        if (Net.Get(out var pos, out var rot))
        {
            vel = (pos - transform.position) / Mathf.Max(Time.deltaTime, .001f);
            transform.SetPositionAndRotation(pos, rot);
        }
        Ducking = netDuck; Grounded = netGround;
        if (netStun) stunUntil = Time.time + .1f;
    }

    // ------------------------------------------------------------------ presentation

    void Present(float dt)
    {
        // Duck = squash (cartoon), jump = a little stretch.
        float targetScale = Ducking ? .6f : (!Grounded && Alive ? 1.06f : 1f);
        duckScale = Mathf.MoveTowards(duckScale, targetScale, dt * 6f);
        if (visual != null && Alive) visual.localScale = new Vector3(1f + (1f - duckScale) * .45f, duckScale, 1f + (1f - duckScale) * .45f);
        // Blink while invulnerable.
        bool show = !Alive || !Invulnerable || Mathf.Repeat(Time.time * 10f, 1f) < .6f;
        if (renderers != null) foreach (var r in renderers) if (r != null) r.enabled = show;
        // Shadow on the floor (if there is floor).
        if (shadow != null)
        {
            Vector3 p = transform.position; bool floor = Alive && Director.Arena.Ground(p, Director.Elapsed) && p.y > -.5f;
            shadow.gameObject.SetActive(floor);
            if (floor) { float k = Mathf.Clamp01(1f - p.y / 2.5f); shadow.position = new Vector3(p.x, .015f, p.z); shadow.localScale = new Vector3(.75f * (.5f + .5f * k), .006f, .75f * (.5f + .5f * k)); }
        }
        // Lives and the name always face the camera.
        if (tagRoot != null && Director.View != null) tagRoot.rotation = Quaternion.LookRotation(tagRoot.position - Director.View.transform.position);
        if (heartRenderers != null) for (int i = 0; i < heartRenderers.Length; i++) heartRenderers[i].sharedMaterial = i < Lives ? heartOn : heartOff;

        string state;
        if (!Alive) state = "Hit";
        else if (Celebrating) state = Grounded ? "Victory" : "Jump";
        else if (Stunned) state = "Hit";
        else if (!Grounded) state = "Jump";
        else
        {
            float sp = new Vector2(vel.x, vel.z).magnitude;
            state = sp > 3.2f ? "Run" : sp > .5f ? "Walk" : "Idle";
        }
        Play(state);
    }

    void Play(string state)
    {
        if (anim == null || state == animState) return;
        animState = state;
        anim.CrossFadeInFixedTime(state, state == "Hit" ? .05f : .12f);
    }

    public void Teleport(Vector3 p) { transform.position = p; vel = Vector3.zero; Net.Clear(); }
}
