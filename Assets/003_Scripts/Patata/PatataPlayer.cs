using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One player of Patata Caliente: runs around the fair (CharacterController), jumps, dashes (Shift / left click) and
/// bounces on the trampolines. The computer that owns the slot moves it; the host decides who holds the potato.
/// When the potato blows up in your hands you fly out of the arena (played the same on every computer).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PatataPlayer : MonoBehaviour
{
    public const float Speed = 6.4f, HolderSpeed = 7.1f, JumpSpeed = 7f, Gravity = 21f, DashSpeed = 15f, DashTime = .17f, DashCooldown = 2.2f, TrampolineSpeed = 13f, ReceiveStun = .45f;

    public PatataDirector Director { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    public bool Alive { get; private set; } = true;
    public bool Celebrating { get; set; }
    public bool Stunned => Time.time < stunUntil;
    public bool Dashing => Time.time < dashUntil;
    public float DashReady => Mathf.Clamp01(1f - (nextDash - Time.time) / DashCooldown);
    public Vector3 Velocity => vel;
    public Transform Head { get; private set; }

    // Bot / input.
    public Vector3 InMove; public bool InJump, InDash;
    public readonly NetPose Net = new NetPose();

    CharacterController cc; Vector3 vel, dashDir; float stunUntil, dashUntil, nextDash, deadAt; Animator anim; string animState = "";
    Transform tagRoot, visual; Vector3 flyVel; bool hidden;

    public void Init(PatataDirector d, int slot, int member, string name, Color color, bool human, bool puppet, GameObject prefab, int lookSeed)
    {
        Director = d; Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet;
        cc = GetComponent<CharacterController>(); cc.height = 1.6f; cc.radius = .35f; cc.center = Vector3.up * .82f; cc.stepOffset = .3f; cc.slopeLimit = 50f;
        if (puppet) cc.enabled = false;
        visual = new GameObject("Cuerpo").transform; visual.SetParent(transform, false);
        if (prefab != null)
        {
            var go = Instantiate(prefab, visual, false); go.name = "Fiestero";
            var b = PirateArenaVisuals.BoundsOf(go); go.transform.localScale *= 1.55f / Mathf.Max(.1f, b.size.y);
            b = PirateArenaVisuals.BoundsOf(go); go.transform.position += new Vector3(0, transform.position.y - b.min.y, 0);
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
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else EscapeKit.Shape(PrimitiveType.Capsule, visual, "Cápsula", Vector3.up * .8f, new Vector3(.6f, .8f, .6f), EscapeKit.Paint(color), false);
        Head = new GameObject("Sobre la cabeza").transform; Head.SetParent(transform, false); Head.localPosition = Vector3.up * 2.05f;
        tagRoot = new GameObject("Etiqueta").transform; tagRoot.SetParent(transform, false); tagRoot.localPosition = Vector3.up * 2.75f;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var nt = new GameObject("Nombre").AddComponent<TextMesh>(); nt.transform.SetParent(tagRoot, false);
        nt.font = font; nt.GetComponent<MeshRenderer>().sharedMaterial = font.material; nt.text = human ? name + " (tú)" : name;
        nt.anchor = TextAnchor.MiddleCenter; nt.alignment = TextAlignment.Center; nt.fontSize = 48; nt.characterSize = .04f; nt.color = color; nt.fontStyle = FontStyle.Bold;
        Play("Idle");
    }

    void Update()
    {
        if (Director == null) return;
        if (!Alive) { FlyAway(); return; }
        if (Puppet)
        {
            if (Net.Get(out var pos, out var rot)) { vel = (pos - transform.position) / Mathf.Max(Time.deltaTime, .001f); transform.SetPositionAndRotation(pos, rot); }
            Present(); return;
        }
        bool playing = Director.CanMove;
        if (IsHuman && GetComponent<PatataBot>() == null) ReadInput();
        if (!playing || Stunned) { InJump = InDash = false; }
        float dt = Mathf.Min(Time.deltaTime, .05f);

        bool holder = Director.HolderSlot == Slot;
        Vector3 want = playing && !Stunned ? Vector3.ClampMagnitude(new Vector3(InMove.x, 0, InMove.z), 1f) * (holder ? HolderSpeed : Speed) : Vector3.zero;
        Vector3 flat = Vector3.MoveTowards(new Vector3(vel.x, 0, vel.z), want, (cc.isGrounded ? 50f : 14f) * dt);
        vel.x = flat.x; vel.z = flat.z;
        if (cc.isGrounded && vel.y < 0f) vel.y = -2f;
        if (InJump && cc.isGrounded) { vel.y = JumpSpeed; PatataAudio.Play("jump", IsHuman ? .5f : .15f); }
        if (InDash && Time.time >= nextDash)
        {
            nextDash = Time.time + DashCooldown; dashUntil = Time.time + DashTime;
            dashDir = want.sqrMagnitude > .1f ? want.normalized : transform.forward;
            PatataAudio.Play("dash", IsHuman ? .7f : .2f);
            SurvivalKit.Puff(transform.position + Vector3.up * .2f, new Color(.95f, .95f, .9f), 3, .2f);
        }
        if (cc.isGrounded)
        {
            int t = Director.Arena.TrampolineAt(transform.position);
            if (t >= 0) { vel.y = TrampolineSpeed; Director.Arena.Wobble(t); PatataAudio.Play("boing", IsHuman ? .8f : .3f); }
        }
        vel.y -= Gravity * dt;
        Vector3 move = vel + (Dashing ? dashDir * DashSpeed : Vector3.zero);
        if (cc.enabled) cc.Move(move * dt);
        if (want.sqrMagnitude > .05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Dashing ? dashDir : want), dt * 14f);
        // Keep inside the fence (it closes in after every explosion) and on the floor.
        Vector3 p = transform.position; Vector3 fp = new Vector3(p.x, 0, p.z); float lim = Director.Arena.Radius - .45f;
        if (fp.magnitude > lim) { fp = fp.normalized * lim; Teleport(new Vector3(fp.x, p.y, fp.z), false); }
        if (p.y < -3f) Teleport(Vector3.up * .2f, true);
        InJump = InDash = false;
        Present();
    }

    void ReadInput()
    {
        InMove = Vector3.zero;
        if (!Director.InputFree) return;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        Vector2 axis = Vector2.zero;
        if (k != null)
        {
            axis.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            axis.y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            InJump |= k.spaceKey.wasPressedThisFrame; InDash |= k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame;
        }
        if (m != null) InDash |= m.leftButton.wasPressedThisFrame;
        if (g != null)
        {
            var s = g.leftStick.ReadValue(); if (s.sqrMagnitude > axis.sqrMagnitude) axis = s;
            InJump |= g.buttonSouth.wasPressedThisFrame; InDash |= g.buttonWest.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame;
        }
        float yaw = Director.View != null ? Director.View.Yaw : 0f;
        InMove = Quaternion.Euler(0, yaw, 0) * new Vector3(axis.x, 0, axis.y);
    }

    /// <summary>Just got the potato: frozen for a moment so the one who passed it can run.</summary>
    public void Receive() { stunUntil = Time.time + ReceiveStun; vel.x = vel.z = 0f; }

    /// <summary>The potato blew up: fly out of the arena with a spin, then disappear (same on every computer).</summary>
    public void Explode()
    {
        if (!Alive) return;
        Alive = false; deadAt = Time.time; Celebrating = false;
        if (cc != null) cc.enabled = false;
        Vector3 outDir = new Vector3(transform.position.x, 0, transform.position.z); outDir = outDir.sqrMagnitude > .1f ? outDir.normalized : Vector3.forward;
        flyVel = outDir * 7f + Vector3.up * 13f;
        animState = ""; Play("Death");
    }

    void FlyAway()
    {
        if (hidden) return;
        float dt = Time.deltaTime;
        flyVel.y -= 16f * dt;
        transform.position += flyVel * dt;
        visual.Rotate(Vector3.right, 540f * dt, Space.Self);
        if (tagRoot != null && Camera.main != null) tagRoot.rotation = Camera.main.transform.rotation;
        if (Time.time - deadAt > 1.6f)
        {
            hidden = true; SurvivalKit.Puff(transform.position, new Color(.5f, .5f, .5f), 5, .4f);
            visual.gameObject.SetActive(false); tagRoot.gameObject.SetActive(false);
        }
    }

    public void Teleport(Vector3 p, bool resetVel) { bool e = cc.enabled; cc.enabled = false; transform.position = p; cc.enabled = e; if (resetVel) vel = Vector3.zero; Net.Clear(); }

    void Present()
    {
        if (tagRoot != null && Camera.main != null) tagRoot.rotation = Camera.main.transform.rotation;
        string state;
        float sp = new Vector2(vel.x, vel.z).magnitude;
        if (Celebrating) state = "Victory";
        else if (Stunned) state = "Hit";
        else if (!Puppet && !cc.isGrounded && vel.y > 1f) state = "Jump";
        else if (Puppet && vel.y > 2f) state = "Jump";
        else state = sp > 3.5f ? "Run" : sp > .4f ? "Walk" : "Idle";
        Play(state);
    }
    void Play(string s) { if (anim == null || s == animState) return; animState = s; anim.CrossFadeInFixedTime(s, .1f); }
    public void ShowStun() { stunUntil = Time.time + ReceiveStun; }
    public void ShowDash() { if (!Dashing) dashUntil = Time.time + DashTime; }
}
