using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>What a <see cref="PartyMover"/> asks the minigame that owns it.</summary>
public interface IMoverHost
{
    bool CanMove { get; }
    bool InputFree { get; }
    float CameraYaw { get; }
}

/// <summary>
/// Shared party character for the 2026-11-19 minigames (Suelo que se Cae, Pintura Loca, Rey de la Colina): the toy
/// character with a CharacterController, run / jump / dash (Shift or right click), knock-backs and slips, a name tag
/// and network interpolation for the players other computers move. The minigame decides the rules; this only moves.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class PartyMover : MonoBehaviour
{
    public float Speed = 6.4f, JumpSpeed = 7f, Gravity = 21f, DashSpeed = 15f, DashTime = .17f, DashCooldown = 2.2f;
    public bool CanJump = true, CanDash = true;

    public IMoverHost Host { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    public bool BotControlled { get; set; }
    /// <summary>False when the minigame uses the left click for something else (Rey de la Colina: bombs).</summary>
    public bool LeftClickDashes = true;
    public bool Celebrating { get; set; }
    public bool Out { get; private set; }
    public bool Stunned => Time.time < stunUntil;
    public bool Slipping => Time.time < slipUntil;
    public bool Dashing => Time.time < dashUntil;
    public float DashReady => Mathf.Clamp01(1f - (nextDash - Time.time) / DashCooldown);
    public Vector3 Velocity => vel;
    public bool Grounded => cc != null && cc.enabled && cc.isGrounded;
    public Transform Head { get; private set; }

    public Vector3 InMove; public bool InJump, InDash;
    public readonly NetPose Net = new NetPose();

    CharacterController cc; Vector3 vel, dashDir; float stunUntil, dashUntil, nextDash, slipUntil; Animator anim; string animState = "";
    Transform tagRoot, visual;

    public void Init(IMoverHost host, int slot, int member, string name, Color color, bool human, bool puppet, GameObject prefab, int lookSeed)
    {
        Host = host; Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet;
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
        Head = new GameObject("Sobre la cabeza").transform; Head.SetParent(transform, false); Head.localPosition = Vector3.up * 1.75f;
        tagRoot = new GameObject("Etiqueta").transform; tagRoot.SetParent(transform, false); tagRoot.localPosition = Vector3.up * 2.55f;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var nt = new GameObject("Nombre").AddComponent<TextMesh>(); nt.transform.SetParent(tagRoot, false);
        nt.font = font; nt.GetComponent<MeshRenderer>().sharedMaterial = font.material; nt.text = human ? name + " (tú)" : name;
        nt.anchor = TextAnchor.MiddleCenter; nt.alignment = TextAlignment.Center; nt.fontSize = 48; nt.characterSize = .04f; nt.color = color; nt.fontStyle = FontStyle.Bold;
        Play("Idle");
    }

    void Update()
    {
        if (Host == null) return;
        if (Out) return;
        if (Puppet)
        {
            if (Net.Get(out var pos, out var rot)) { vel = (pos - transform.position) / Mathf.Max(Time.deltaTime, .001f); transform.SetPositionAndRotation(pos, rot); }
            Present(); return;
        }
        bool playing = Host.CanMove;
        if (IsHuman && !BotControlled) ReadInput();
        if (!playing || Stunned) { InJump = InDash = false; }
        float dt = Mathf.Min(Time.deltaTime, .05f);
        Vector3 want = playing && !Stunned ? Vector3.ClampMagnitude(new Vector3(InMove.x, 0, InMove.z), 1f) * Speed : Vector3.zero;
        float accel = Slipping ? 2.5f : cc.isGrounded ? 50f : 14f;
        Vector3 flat = Vector3.MoveTowards(new Vector3(vel.x, 0, vel.z), want, accel * dt);
        vel.x = flat.x; vel.z = flat.z;
        if (cc.isGrounded && vel.y < 0f) vel.y = -2f;
        if (CanJump && InJump && cc.isGrounded) vel.y = JumpSpeed;
        if (CanDash && InDash && Time.time >= nextDash)
        {
            nextDash = Time.time + DashCooldown; dashUntil = Time.time + DashTime;
            dashDir = want.sqrMagnitude > .1f ? want.normalized : transform.forward;
            SurvivalKit.Puff(transform.position + Vector3.up * .2f, new Color(.95f, .95f, .9f), 3, .2f);
        }
        vel.y -= Gravity * dt;
        Vector3 move = vel + (Dashing ? dashDir * DashSpeed : Vector3.zero);
        if (cc.enabled) cc.Move(move * dt);
        if (Slipping) visual.Rotate(Vector3.up, 720f * dt, Space.World);
        else
        {
            visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.identity, dt * 10f);
            if (want.sqrMagnitude > .05f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(Dashing ? dashDir : want), dt * 14f);
        }
        InJump = InDash = false;
        Present();
    }

    void ReadInput()
    {
        InMove = Vector3.zero;
        if (!Host.InputFree) return;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        Vector2 axis = Vector2.zero;
        if (k != null)
        {
            axis.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            axis.y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            InJump |= k.spaceKey.wasPressedThisFrame; InDash |= k.leftShiftKey.wasPressedThisFrame || k.rightShiftKey.wasPressedThisFrame;
        }
        if (m != null) InDash |= m.rightButton.wasPressedThisFrame || (LeftClickDashes && m.leftButton.wasPressedThisFrame);
        if (g != null)
        {
            var s = g.leftStick.ReadValue(); if (s.sqrMagnitude > axis.sqrMagnitude) axis = s;
            InJump |= g.buttonSouth.wasPressedThisFrame; InDash |= g.buttonWest.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame;
        }
        InMove = Quaternion.Euler(0, Host.CameraYaw, 0) * new Vector3(axis.x, 0, axis.y);
    }

    /// <summary>Pushed (by a dash, a bump, an explosion): flies along <paramref name="push"/> and wobbles.</summary>
    public void Knock(Vector3 push, float stun = .6f) { stunUntil = Time.time + stun; if (!Puppet) { vel = push; dashUntil = 0f; } }
    /// <summary>Stepped on a banana: slides and spins, almost no control.</summary>
    public void Slip(float seconds = 1.2f) { slipUntil = Time.time + seconds; stunUntil = Time.time + seconds * .6f; if (!Puppet && vel.sqrMagnitude < 9f) vel = transform.forward * 5f; }
    public void ShowStun(float s = .6f) { stunUntil = Mathf.Max(stunUntil, Time.time + s); }
    public void ShowDash() { if (!Dashing) dashUntil = Time.time + DashTime; }
    public void Teleport(Vector3 p, bool resetVel = true) { bool e = cc.enabled; cc.enabled = false; transform.position = p; cc.enabled = e && !Puppet; if (resetVel) vel = Vector3.zero; Net.Clear(); }
    /// <summary>Knocked out of the minigame: hidden (with a puff) until <see cref="Revive"/>.</summary>
    public void SetOut()
    {
        if (Out) return;
        Out = true; SurvivalKit.Puff(transform.position + Vector3.up, new Color(.5f, .5f, .5f), 5, .4f);
        visual.gameObject.SetActive(false); tagRoot.gameObject.SetActive(false); if (cc != null) cc.enabled = false;
    }
    public void Revive(Vector3 at) { Out = false; visual.gameObject.SetActive(true); tagRoot.gameObject.SetActive(true); Teleport(at); if (cc != null) cc.enabled = !Puppet; }

    void LateUpdate() { if (tagRoot != null && Camera.main != null) tagRoot.rotation = Camera.main.transform.rotation; }

    void Present()
    {
        string state; float sp = new Vector2(vel.x, vel.z).magnitude;
        if (Celebrating) state = "Victory";
        else if (Stunned) state = "Hit";
        else if (!Puppet && !cc.isGrounded && vel.y > 1f) state = "Jump";
        else if (Puppet && vel.y > 2f) state = "Jump";
        else state = sp > 3.5f ? "Run" : sp > .4f ? "Walk" : "Idle";
        Play(state);
    }
    void Play(string s) { if (anim == null || s == animState) return; animState = s; anim.CrossFadeInFixedTime(s, .1f); }

    // ------------------------------------------------------------------ network helpers

    /// <summary>Writes the movers this computer moves (member, position, yaw, flags).</summary>
    public static void WritePoses(BinaryWriter w, List<PartyMover> all)
    {
        int n = 0; foreach (var p in all) if (!p.Puppet) n++;
        w.Write((byte)n);
        foreach (var p in all)
        {
            if (p.Puppet) continue;
            w.Write((sbyte)p.Member); w.Write(p.transform.position); w.Write(p.transform.eulerAngles.y);
            w.Write((byte)((p.Stunned ? 1 : 0) | (p.Celebrating ? 2 : 0) | (p.Dashing ? 4 : 0) | (p.Out ? 8 : 0) | (p.Slipping ? 16 : 0)));
        }
    }

    public static void ReadPoses(BinaryReader r, List<PartyMover> all)
    {
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); var pos = r.ReadVector3(); float yaw = r.ReadSingle(); byte flags = r.ReadByte();
            int slot = PartyLink.SlotOfMember(member); if (slot < 0 || slot >= all.Count) continue;
            var p = all[slot]; if (!p.Puppet) continue;
            p.Net.Add(pos, Quaternion.Euler(0, yaw, 0));
            if ((flags & 1) != 0 && !p.Stunned) p.ShowStun(.3f);
            if ((flags & 4) != 0) p.ShowDash();
            p.Celebrating = (flags & 2) != 0;
            if ((flags & 16) != 0 && !p.Slipping) p.Slip(.6f);
            if ((flags & 8) != 0 && !p.Out) p.SetOut(); else if ((flags & 8) == 0 && p.Out) p.Revive(pos);
        }
    }
}
