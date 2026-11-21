using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One courier of Reparto Loco: runs across the street (CharacterController) with a parcel over the head, jumps and
/// dashes (Shift / right click). Each computer checks its own players against the traffic (no lag): a vehicle does
/// not kill, it throws you aside and the parcel you carried breaks. Parcels are picked up automatically at either end.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class RepartoPlayer : MonoBehaviour
{
    public const float Speed = 6.2f, JumpSpeed = 7f, Gravity = 21f, DashSpeed = 14f, DashTime = .16f, DashCooldown = 2.4f, KnockStun = .9f, Invulnerable = 1.5f;

    public RepartoDirector Director { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    /// <summary>+1 = carrying a parcel to the neighbourhood (north), −1 = to the shop (south), 0 = empty-handed.</summary>
    public int Carry { get; set; }
    public bool Safe => Time.time < safeUntil;
    public bool Celebrating { get; set; }
    public bool Stunned => Time.time < stunUntil;
    public bool Dashing => Time.time < dashUntil;
    public float DashReady => Mathf.Clamp01(1f - (nextDash - Time.time) / DashCooldown);
    public Vector3 Velocity => vel;
    public Transform Head { get; private set; }

    // Bot / input.
    public Vector3 InMove; public bool InJump, InDash;
    public readonly NetPose Net = new NetPose();

    CharacterController cc; Vector3 vel, dashDir; float stunUntil, dashUntil, nextDash, safeUntil; Animator anim; string animState = "";
    Transform tagRoot, visual, parcel;

    public void Init(RepartoDirector d, int slot, int member, string name, Color color, bool human, bool puppet, GameObject prefab, int lookSeed)
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
        Head = new GameObject("Sobre la cabeza").transform; Head.SetParent(transform, false); Head.localPosition = Vector3.up * 1.75f;
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
        bool playing = Director.CanMove;
        if (IsHuman && GetComponent<RepartoBot>() == null) ReadInput();
        if (!playing || Stunned) { InJump = InDash = false; }
        float dt = Mathf.Min(Time.deltaTime, .05f);

        Vector3 want = playing && !Stunned ? Vector3.ClampMagnitude(new Vector3(InMove.x, 0, InMove.z), 1f) * Speed : Vector3.zero;
        Vector3 flat = Vector3.MoveTowards(new Vector3(vel.x, 0, vel.z), want, (cc.isGrounded ? 50f : 14f) * dt);
        vel.x = flat.x; vel.z = flat.z;
        if (cc.isGrounded && vel.y < 0f) vel.y = -2f;
        if (InJump && cc.isGrounded) { vel.y = JumpSpeed; RepartoAudio.Play("jump", IsHuman ? .5f : .15f); }
        if (InDash && Time.time >= nextDash)
        {
            nextDash = Time.time + DashCooldown; dashUntil = Time.time + DashTime;
            dashDir = want.sqrMagnitude > .1f ? want.normalized : transform.forward;
            RepartoAudio.Play("dash", IsHuman ? .7f : .2f);
            SurvivalKit.Puff(transform.position + Vector3.up * .2f, new Color(.95f, .95f, .9f), 3, .2f);
        }
        vel.y -= Gravity * dt;
        Vector3 move = vel + (Dashing ? dashDir * DashSpeed : Vector3.zero);
        if (cc.enabled) cc.Move(move * dt);
        if (want.sqrMagnitude > .05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Dashing ? dashDir : want), dt * 14f);
        Vector3 p = transform.position;
        if (p.y < -3f) Teleport(Director.SpawnPoint(Slot), true);
        if (Director.State == RepartoDirector.Phase.Playing) OwnerRules(p);
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
        if (m != null) InDash |= m.rightButton.wasPressedThisFrame || m.leftButton.wasPressedThisFrame;
        if (g != null)
        {
            var s = g.leftStick.ReadValue(); if (s.sqrMagnitude > axis.sqrMagnitude) axis = s;
            InJump |= g.buttonSouth.wasPressedThisFrame; InDash |= g.buttonEast.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame;
        }
        float yaw = Director.View != null ? Director.View.Yaw : 0f;
        InMove = Quaternion.Euler(0, yaw, 0) * new Vector3(axis.x, 0, axis.y);
    }

    /// <summary>Owner: pick up at either end, deliver at the far end, get run over in a lane.</summary>
    void OwnerRules(Vector3 p)
    {
        if (Carry == 0 && p.z < RepartoRoad.SouthEdge) { Carry = 1; RepartoAudio.Play("pick", IsHuman ? .8f : .2f); }
        else if (Carry == 0 && p.z > RepartoRoad.NorthEdge) { Carry = -1; RepartoAudio.Play("pick", IsHuman ? .8f : .2f); }
        else if (Carry == 1 && p.z > RepartoRoad.NorthEdge) { Director.OwnerDelivered(Slot); Carry = -1; }
        else if (Carry == -1 && p.z < RepartoRoad.SouthEdge) { Director.OwnerDelivered(Slot); Carry = 1; }
        if (Safe) return;
        int lane = Director.Road.HitAt(p, Director.TrafficTime, .32f, out var hitCar);
        if (lane < 0) return;
        var l = Director.Road.Lanes[lane];
        if (RepartoDirector.LogHits) Debug.LogWarning($"Reparto: {DisplayName} atropellado · carril {lane} (z {l.Z}) · yo x={p.x:0.00} z={p.z:0.00} · coche x={RepartoRoad.CarX(l, hitCar, Director.TrafficTime):0.00} largo {hitCar.Length} · t={Director.TrafficTime:0.00} · vel {Velocity}");
        float side = Mathf.Sign(p.z - l.Z); if (side == 0f) side = 1f;
        Knock(new Vector3(l.Dir * Mathf.Max(7f, RepartoRoad.LaneSpeed(l, Director.TrafficTime) * 1.3f), 6.5f, side * 2.5f));
        Director.OwnerHit(Slot, Carry != 0);
        Carry = 0;
    }

    /// <summary>Thrown aside by a vehicle: flies a bit, wobbles, and is safe for a moment.</summary>
    public void Knock(Vector3 push) { stunUntil = Time.time + KnockStun; safeUntil = Time.time + Invulnerable; vel = push; dashUntil = 0f; }

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
    public void ShowStun() { stunUntil = Time.time + KnockStun; }

    void LateUpdate()
    {
        if (parcel == null)
        {
            parcel = new GameObject("Pedido").transform; parcel.SetParent(transform, false);
            EscapeKit.Block(parcel, "Caja", Vector3.zero, new Vector3(.55f, .4f, .45f), EscapeKit.Paint(new Color(.78f, .58f, .36f), .1f), .04f, false);
            EscapeKit.Block(parcel, "Cinta", Vector3.zero, new Vector3(.56f, .41f, .1f), EscapeKit.Paint(new Color(.9f, .85f, .7f), .3f), .01f, false);
            EscapeKit.Block(parcel, "Etiqueta", new Vector3(0, .205f, .12f), new Vector3(.2f, .01f, .14f), EscapeKit.Paint(Color, .2f), .005f, false);
        }
        bool show = Carry != 0 && Director.State != RepartoDirector.Phase.Finished;
        if (parcel.gameObject.activeSelf != show) parcel.gameObject.SetActive(show);
        if (show) { parcel.position = Head.position + Vector3.up * .05f; parcel.rotation = transform.rotation * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 9f) * 4f); }
        // Blink while safe after a hit.
        if (visual != null) visual.gameObject.SetActive(!Safe || Mathf.Repeat(Time.time * 8f, 1f) > .3f);
    }
    public void ShowDash() { if (!Dashing) dashUntil = Time.time + DashTime; }
}
