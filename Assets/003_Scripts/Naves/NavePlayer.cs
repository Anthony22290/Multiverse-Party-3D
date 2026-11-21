using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One player of Arma tu Nave: walks around the village (CharacterController), picks a piece up with E, carries it over
/// the head (a bit slower) and drops it on the own pad to install it. F / left click shoves: a shoved carrier drops
/// the piece. The computer that owns the slot moves it; pieces and installs are decided by the host.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class NavePlayer : MonoBehaviour
{
    public const float Speed = 6.4f, CarrySpeed = 5.2f, JumpSpeed = 6.5f, Gravity = 20f, PickRange = 2.1f, ShoveRange = 1.7f, ShoveCooldown = 1.4f;

    public NaveDirector Director { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    public NavePiece Carrying { get; set; }
    public bool Stunned => Time.time < stunUntil;
    public bool Celebrating { get; set; }
    public int Shoves { get; private set; }
    public Vector3 Velocity => vel;

    // Bot / input.
    public Vector3 InMove; public bool InJump, InPick, InDrop, InShove;
    public readonly NetPose Net = new NetPose();

    CharacterController cc; Vector3 vel, knock; float stunUntil, nextShove, shoveAnimUntil; Animator anim; string animState = "";
    Transform tagRoot, visual;

    public void Init(NaveDirector d, int slot, int member, string name, Color color, bool human, bool puppet, GameObject prefab, int lookSeed)
    {
        Director = d; Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet;
        cc = GetComponent<CharacterController>(); cc.height = 1.6f; cc.radius = .35f; cc.center = Vector3.up * .82f; cc.stepOffset = .35f; cc.slopeLimit = 50f;
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
        if (Puppet)
        {
            if (Net.Get(out var pos, out var rot)) { vel = (pos - transform.position) / Mathf.Max(Time.deltaTime, .001f); transform.SetPositionAndRotation(pos, rot); }
            Present(); return;
        }
        bool running = Director.State == NaveDirector.Phase.Running;
        if (IsHuman && GetComponent<NaveBot>() == null) ReadInput();
        if (!running || Stunned) { InJump = InPick = InDrop = InShove = false; }
        float dt = Time.deltaTime;

        Vector3 want = running && !Stunned ? Vector3.ClampMagnitude(new Vector3(InMove.x, 0, InMove.z), 1f) * (Carrying != null ? CarrySpeed : Speed) : Vector3.zero;
        Vector3 flat = Vector3.MoveTowards(new Vector3(vel.x, 0, vel.z), want, (cc.isGrounded ? 45f : 15f) * dt);
        vel.x = flat.x; vel.z = flat.z;
        if (cc.isGrounded && vel.y < 0f) vel.y = -2f;
        if (InJump && cc.isGrounded) { vel.y = JumpSpeed; NaveAudio.Play("jump", transform.position, IsHuman ? .5f : .2f); }
        vel.y -= Gravity * dt;
        knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * dt);
        if (cc.enabled) cc.Move((vel + knock) * dt);
        if (want.sqrMagnitude > .05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(want), dt * 12f);
        if (transform.position.y < -5f) Teleport(Director.SpawnPoint(Slot));

        if (InPick) { var p = Director.NearestFreePiece(transform.position, PickRange); if (p != null) Director.RequestPick(this, p); }
        if (InDrop && Carrying != null) Director.RequestDrop(this);
        if (InShove && Time.time >= nextShove) Shove();
        InJump = InPick = InDrop = InShove = false;
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
            InJump |= k.spaceKey.wasPressedThisFrame; InPick |= k.eKey.wasPressedThisFrame; InDrop |= k.qKey.wasPressedThisFrame; InShove |= k.fKey.wasPressedThisFrame;
        }
        if (m != null) InShove |= m.leftButton.wasPressedThisFrame;
        if (g != null)
        {
            var s = g.leftStick.ReadValue(); if (s.sqrMagnitude > axis.sqrMagnitude) axis = s;
            InJump |= g.buttonSouth.wasPressedThisFrame; InPick |= g.buttonWest.wasPressedThisFrame; InDrop |= g.buttonNorth.wasPressedThisFrame; InShove |= g.buttonEast.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame;
        }
        float yaw = Director.View != null ? Director.View.Yaw : 0f;
        InMove = Quaternion.Euler(0, yaw, 0) * new Vector3(axis.x, 0, axis.y);
    }

    void Shove()
    {
        nextShove = Time.time + ShoveCooldown; Shoves++; shoveAnimUntil = Time.time + .35f; animState = ""; Play("Punch");
        NaveAudio.Play("whoosh", transform.position, IsHuman ? .6f : .25f);
        NavePlayer best = null; float bd = ShoveRange;
        foreach (var o in Director.Players)
        {
            if (o == this) continue;
            Vector3 d = o.transform.position - transform.position; d.y = 0;
            if (d.magnitude > bd || Vector3.Dot(transform.forward, d.normalized) < .3f) continue;
            best = o; bd = d.magnitude;
        }
        if (best != null) Director.RequestShove(this, best, (best.transform.position - transform.position).normalized);
    }

    /// <summary>Applied on the computer that owns this player.</summary>
    public void Knock(Vector3 dir)
    {
        dir.y = 0; knock = dir.normalized * 9f; vel.y = 4f; stunUntil = Time.time + .55f;
        NaveAudio.Play("bonk", transform.position, IsHuman ? 1f : .5f);
        MultiverseFX.Burst(transform.position + Vector3.up * 1.2f, new Color(1f, .9f, .4f), 10, 4);
        if (IsHuman) Director.View?.Shake(.4f);
    }
    public void ShowStun() { stunUntil = Time.time + .55f; }

    public void Teleport(Vector3 p) { bool e = cc.enabled; cc.enabled = false; transform.position = p; cc.enabled = e; vel = Vector3.zero; Net.Clear(); }

    void Present()
    {
        if (tagRoot != null && Camera.main != null) tagRoot.rotation = Camera.main.transform.rotation;
        string state;
        float sp = new Vector2(vel.x, vel.z).magnitude;
        if (Celebrating) state = "Victory";
        else if (Stunned) state = "Hit";
        else if (Time.time < shoveAnimUntil) state = "Punch";
        else if (!Puppet && !cc.isGrounded && vel.y > 1f) state = "Jump";
        else state = sp > 3.5f ? "Run" : sp > .4f ? "Walk" : "Idle";
        Play(state);
    }
    void Play(string s) { if (anim == null || s == animState) return; animState = s; anim.CrossFadeInFixedTime(s, .12f); }
    public void PlayShoveAnim() { shoveAnimUntil = Time.time + .35f; animState = ""; Play("Punch"); }
    public string AnimFlags => animState;
}
