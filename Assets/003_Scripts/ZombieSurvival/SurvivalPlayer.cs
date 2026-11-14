using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One survivor (the local player or a bot). Same CharacterController movement and soft-toy crew look as the
/// other modes, plus a melee weapon from the zombie pack (3-hit combo, light auto-aim), 100 health with slow
/// regeneration, carrying truck parts (no attacking while carrying), and being downed / revived by a teammate.
/// </summary>
public sealed class SurvivalPlayer : MonoBehaviour
{
    public enum UseKind { None, Install, Revive }

    public SurvivalDirector Director { get; private set; }
    public bool IsHuman { get; private set; }
    public string DisplayName { get; private set; }
    public int Slot { get; private set; }
    public Color Tint { get; private set; }
    public CharacterController Controller { get; private set; }
    public float Health { get; private set; } = MaxHealth;
    public const float MaxHealth = 100f;
    public bool Downed { get; private set; }
    public float DownedAt { get; private set; }
    public SurvivalPart Carried { get; private set; }
    public UseKind Focus { get; private set; }
    public SurvivalPlayer FocusPlayer { get; private set; }
    public float UseProgress { get; private set; }
    public float Speed { get; private set; }
    public bool Grounded => Controller != null && Controller.isGrounded;
    public bool DrivenByBot => !IsHuman || (Director != null && Director.AutoHuman);
    public float LastHurt { get; private set; } = -99f;
    public string LastHitBy { get; private set; } = "";
    public int Kills, Parts, Revives, Downs;
    public int Score => Kills + Parts * 6 + Revives * 4;
    public const float AutoReviveSeconds = 16f, SwingRange = 2.2f;

    // ---- online party (2026-11-12)
    /// <summary>Host: another person's survivor. It moves with their computer's position and acts with their controls.</summary>
    public bool NetDriven { get; private set; }
    /// <summary>Client: someone else's survivor, shown from the host's state.</summary>
    public bool Puppet { get; private set; }
    /// <summary>Client: this computer's own survivor (moves here, the host decides everything else).</summary>
    public bool ClientOwned => IsHuman && Director != null && Director.IsClient;
    public NetPose NetPose { get; } = new NetPose();
    public int SwingSeq { get; private set; }
    public int HurtSeq { get; private set; }
    public int Combo => combo;
    public float RageRemaining => Mathf.Max(0f, rageUntil - Time.time);
    [System.NonSerialized] public bool NetAttack, NetUse;
    int netSwingSeq = -1, netHurtSeq = -1;
    public void SetNetMode(bool netDriven, bool puppet) { NetDriven = netDriven; Puppet = puppet; }

    // Written by SurvivalBot every frame.
    [System.NonSerialized] public Vector3 BotMove;
    [System.NonSerialized] public bool BotJump, BotAttack, BotUse;

    const float RunSpeed = 6f, CarrySpeed = 4.9f, Gravity = 22f, JumpSpeed = 7f, SwingTime = .26f, SwingCooldown = .42f;
    float vertical, stepAt, airTime, nextSwing, swingStart = -9f, invulnerableUntil, hurtAnimUntil;
    bool swingResolved = true;
    int combo;
    Vector3 previous, push;
    Animator animator;
    string animState;
    TextMesh nameTag, helpTag;
    Transform ring, weaponHolder, weaponTip;
    TrailRenderer trail;

    public void Initialize(SurvivalDirector director, int slot, bool human, string displayName, string model, Color tint, string weapon)
    {
        Director = director; Slot = slot; IsHuman = human; DisplayName = displayName; Tint = tint;
        previous = transform.position;
        Controller = gameObject.AddComponent<CharacterController>();
        Controller.height = 1.7f; Controller.center = Vector3.up * .85f; Controller.radius = .34f;
        Controller.stepOffset = .3f; Controller.slopeLimit = 50f; Controller.skinWidth = .03f;

        var prefab = Resources.Load<GameObject>("PirateCharacters/Characters_" + model);
        var atlas = EscapeKit.Art != null ? EscapeKit.Art.characterAtlas : null;
        GameObject visual = prefab != null ? PirateArenaVisuals.AddModel(prefab, transform, 1.7f, atlas, false) : null;
        if (visual != null)
        {
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Destroy(c);
            animator = visual.GetComponentInChildren<Animator>();
            if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            PirateCrewStyle.Apply(this, transform, visual, slot);
        }
        else EscapeKit.Shape(PrimitiveType.Capsule, transform, "Cuerpo", Vector3.up * .85f, new Vector3(.7f, .85f, .7f), EscapeKit.Paint(tint), false);

        ring = EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Aro del equipo", Vector3.up * .03f, new Vector3(1f, .012f, 1f), EscapeKit.Glow(tint, .9f), false).transform;
        nameTag = EscapeKit.Text(transform, human ? "TÚ" : displayName.ToUpperInvariant(), Vector3.up * 2.55f, Vector3.zero, .026f, tint);
        helpTag = EscapeKit.Text(transform, "¡AYUDA!", Vector3.up * 1.6f, Vector3.zero, .04f, new Color(1f, .4f, .35f));
        helpTag.gameObject.SetActive(false);

        // Weapon held on the right side; swings are procedural (the crew rig has no attack clips).
        weaponHolder = new GameObject("Arma").transform;
        weaponHolder.SetParent(transform, false);
        weaponHolder.localPosition = new Vector3(.38f, 1.05f, .12f);
        var stick = SurvivalKit.Stick(weapon, weaponHolder, weapon == "Guitar" ? 1.05f : .95f);
        if (stick == null) EscapeKit.Block(weaponHolder, "Bate", Vector3.up * .45f, new Vector3(.12f, .9f, .12f), EscapeKit.Paint(new Color(.6f, .42f, .25f)), .03f, false);
        weaponTip = new GameObject("Punta").transform;
        weaponTip.SetParent(weaponHolder, false);
        weaponTip.localPosition = Vector3.up * .9f;
        trail = weaponTip.gameObject.AddComponent<TrailRenderer>();
        trail.time = .12f; trail.startWidth = .22f; trail.endWidth = 0f; trail.emitting = false; trail.minVertexDistance = .05f;
        trail.material = EscapeKit.Glow(Color.white, 1.2f);
        trail.startColor = new Color(1f, 1f, 1f, .9f); trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        PoseWeapon();
    }

    void Update()
    {
        if (Director == null) return;
        float dt = Time.deltaTime;
        if (NetDriven || Puppet) { NetUpdate(dt); return; }
        Vector3 move = Vector3.zero;
        bool jump = false, attack = false, use = false;
        bool canAct = Director.Fighting && !Downed;

        if (canAct)
        {
            if (DrivenByBot)
            {
                move = BotMove; jump = BotJump; attack = BotAttack; use = BotUse;
                BotJump = false; BotAttack = false;
            }
            else if (Director.InputFree)
            {
                var k = Keyboard.current;
                var mouse = Mouse.current;
                if (k != null)
                {
                    var axis = new Vector2(
                        (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                        (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
                    move = Quaternion.Euler(0, Director.View != null ? Director.View.Yaw : 0f, 0) * new Vector3(axis.x, 0, axis.y);
                    jump = k.spaceKey.wasPressedThisFrame;
                    use = k.eKey.isPressed;
                    attack = k.jKey.isPressed || k.fKey.isPressed;
                    if (k.qKey.wasPressedThisFrame && Carried != null) { if (ClientOwned) Director.Net.RequestDrop(); else Drop(); }
                }
                if (mouse != null && mouse.leftButton.isPressed) attack = true;
            }
        }
        NetAttack = attack; NetUse = use;

        Move(Downed ? Vector3.zero : move, dt, jump && !Downed);
        if (Downed)
        {
            if (Director.Fighting && !ClientOwned && Time.time - DownedAt > AutoReviveSeconds) { Teleport(Director.Level.SpawnPoint(Slot)); Revive(null, 50f); }
        }
        else if (canAct)
        {
            if (attack && Carried == null && Time.time >= nextSwing) Swing();
            ProcessUse(use, dt);
            Director.TryPickup(this);
            if (!ClientOwned && Time.time - LastHurt > 4f / (1f + P(SurvivalPerks.Kind.Regeneracion)) && Health < MaxHealth) Health = Mathf.Min(MaxHealth, Health + 5f * (1f + P(SurvivalPerks.Kind.Regeneracion)) * dt);
        }
        else { Focus = UseKind.None; UseProgress = 0f; }

        TickSwing();
        Animate();
    }

    void LateUpdate()
    {
        var cam = Director != null && Director.View != null ? Director.View.transform : null;
        if (cam != null)
        {
            if (nameTag != null) nameTag.transform.rotation = cam.rotation;
            if (helpTag != null) helpTag.transform.rotation = cam.rotation;
        }
        if (helpTag != null && Downed) helpTag.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 8f) * .1f);
        if (ring != null) ring.localScale = new Vector3(1f, .012f, 1f) * (Downed ? 1.6f + Mathf.Sin(Time.time * 6f) * .15f : 1f);
    }

    public void Move(Vector3 direction, float delta, bool jump)
    {
        if (Controller == null || !Controller.enabled) return;
        bool grounded = Controller.isGrounded;
        if (grounded)
        {
            if (airTime > .45f) EscapeAudio.Play("land_0", transform.position, IsHuman ? .5f : .25f);
            airTime = 0f;
            if (vertical < 0f) vertical = -2f;
            if (jump) { vertical = JumpSpeed; EscapeAudio.Play("jump_" + Random.Range(0, 3), transform.position, IsHuman ? .45f : .2f); }
        }
        else airTime += delta;

        float speed = Carried != null ? CarrySpeed : RunSpeed;
        if (Time.time - swingStart < SwingTime) speed *= .55f;
        speed *= 1f + .1f * P(SurvivalPerks.Kind.Velocidad);
        if (Raging)
        {
            speed *= 1.25f;
            if (Time.time > nextAura) { nextAura = Time.time + .2f; MultiverseFX.Burst(transform.position + Vector3.up * 1.1f, new Color(1, .25f, .15f), 3, 2.5f); }
        }
        push = Vector3.MoveTowards(push, Vector3.zero, delta * 16f);
        float remaining = Mathf.Min(delta, .2f);
        while (remaining > 0f)
        {
            float dt = Mathf.Min(remaining, 1f / 60f);
            remaining -= dt;
            vertical = Mathf.Max(-28f, vertical - Gravity * dt);
            var flags = Controller.Move((Vector3.ClampMagnitude(direction, 1f) * speed + push + Vector3.up * vertical) * dt);
            if ((flags & CollisionFlags.Above) != 0 && vertical > 0f) vertical = 0f;
            if ((flags & CollisionFlags.Below) != 0 && vertical < 0f) vertical = -2f;
        }
        if (direction.sqrMagnitude > .01f && Time.time - swingStart > SwingTime)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z)), delta * 14f);

        Vector3 flat = transform.position - previous; flat.y = 0f;
        Speed = flat.magnitude / Mathf.Max(delta, .0001f);
        previous = transform.position;
        if (grounded && Speed > 2f && Time.time > stepAt && IsHuman)
        {
            stepAt = Time.time + .3f;
            EscapeAudio.Play("step_" + Random.Range(0, 3), transform.position, .22f, .12f);
        }
    }

    // ------------------------------------------------------------------ combat

    void Swing()
    {
        SwingSeq++;
        nextSwing = Time.time + SwingCooldown;
        swingStart = Time.time;
        swingResolved = false;
        combo = (combo + 1) % 3;
        // Light auto-aim: turn toward the closest zombie roughly in front.
        SurvivalZombie best = null; float bestScore = float.MaxValue;
        foreach (var z in SurvivalZombie.All)
        {
            Vector3 d = z.transform.position - transform.position; d.y = 0f;
            float m = d.magnitude - z.Radius;
            if (m > SwingRange + 1.2f) continue;
            float angle = m > .3f ? Vector3.Angle(transform.forward, d) : 0f;
            if (angle > 110f && m > .8f) continue;
            float score = m + angle * .02f;
            if (score < bestScore) { bestScore = score; best = z; }
        }
        if (best != null)
        {
            Vector3 d = best.transform.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(d);
        }
        if (trail != null) { trail.Clear(); trail.emitting = true; }
        EscapeAudio.Play("whoosh_" + Random.Range(0, 3), transform.position, IsHuman ? .5f : .25f, .12f);
    }

    void TickSwing()
    {
        float t = Time.time - swingStart;
        if (!swingResolved && t >= .1f) { swingResolved = true; ResolveSwing(); }
        if (trail != null && trail.emitting && t > SwingTime) trail.emitting = false;
        PoseWeapon();
    }

    void ResolveSwing()
    {
        float dmg = (combo == 2 ? 46f : 30f) * (Raging ? 2f : 1f) * (1f + .2f * P(SurvivalPerks.Kind.Fuerza));
        float range = SwingRange * (1f + .2f * P(SurvivalPerks.Kind.Alcance));
        int hits = 0;
        foreach (var z in SurvivalZombie.All.ToArray())
        {
            if (z == null || z.Dead) continue;
            Vector3 d = z.transform.position - transform.position; d.y = 0f;
            float m = d.magnitude;
            if (m > range + z.Radius) continue;
            if (m > z.Radius + .4f && Vector3.Angle(transform.forward, d) > 75f) continue;
            z.TakeHit(dmg, m > .01f ? d / m * (combo == 2 ? 1.6f : 1f) : transform.forward, this);
            hits++;
        }
        if (combo == 2 && P(SurvivalPerks.Kind.Onda) > 0)
        {   // perk: shockwave on the combo finisher
            MultiverseFX.Ring(transform.position, 3.6f, SurvivalPerks.Colors[(int)SurvivalPerks.Kind.Onda], .4f);
            foreach (var z in SurvivalZombie.All.ToArray())
                if (z != null && !z.Dead && Vector3.Distance(z.transform.position, transform.position) < 3.6f) z.TakeHit(25f * P(SurvivalPerks.Kind.Onda), (z.transform.position - transform.position).normalized, this);
        }
        if (hits > 0)
        {
            EscapeAudio.Play("punch_" + Random.Range(0, 3), transform.position + transform.forward, IsHuman ? .8f : .45f, .1f);
            if (IsHuman) Director.View?.Shake(combo == 2 ? .18f : .1f);
        }
    }

    void PoseWeapon()
    {
        if (weaponHolder == null) return;
        weaponHolder.gameObject.SetActive(Carried == null && !Downed);
        float t = (Time.time - swingStart) / SwingTime;
        Quaternion rest = Quaternion.Euler(-30f, 0f, -18f);
        if (t >= 0f && t < 1f)
        {
            // Wide horizontal arc from the right to the left (the third hit of the combo goes the other way, overhead).
            float e = 1f - (1f - t) * (1f - t);
            Quaternion swing = combo == 2
                ? Quaternion.Euler(Mathf.Lerp(-40f, 120f, e), 0f, 0f)
                : Quaternion.Euler(0f, Mathf.Lerp(combo == 1 ? -80f : 80f, combo == 1 ? 80f : -80f, e), 0f) * Quaternion.Euler(85f, 0f, 0f);
            weaponHolder.localRotation = swing;
        }
        else weaponHolder.localRotation = Quaternion.Slerp(weaponHolder.localRotation, rest, Time.deltaTime * 12f);
    }

    public void Damage(float amount, Vector3 knock, SurvivalZombie from)
    {
        if (Downed || !Director.Fighting || Time.time < invulnerableUntil) return;
        amount *= Director.damageToTeam * Mathf.Pow(.85f, P(SurvivalPerks.Kind.Armadura));
        if (from != null && !from.Dead && P(SurvivalPerks.Kind.Espinas) > 0) from.TakeHit(12f * P(SurvivalPerks.Kind.Espinas), (from.transform.position - transform.position).normalized, this);
        Health -= amount;
        LastHurt = Time.time;
        HurtSeq++;
        if (NetDriven) Director.Net?.SendKnock(this, knock);   // its owner moves it
        LastHitBy = from == null ? "?" : from.Title + (from.Slamming ? " (pisotón)" : from.Charging ? " (embestida)" : "");
        push += knock;
        hurtAnimUntil = Time.time + .3f;
        PlayAnim("HitReact", true);
        EscapeAudio.Play("impact_" + Random.Range(0, 3), transform.position, IsHuman ? .6f : .3f, .1f);
        if (IsHuman) { Director.HUD?.Hurt(amount); Director.View?.Shake(.22f); }
        if (Health <= 0f) GoDown();
    }

    void GoDown()
    {
        Health = 0f;
        Downed = true;
        DownedAt = Time.time;
        Downs++;
        if (Carried != null) Drop();
        Focus = UseKind.None; UseProgress = 0f;
        helpTag.gameObject.SetActive(true);
        PlayAnim("Death", true);
        EscapeAudio.Play("ko", transform.position, .8f);
        Director.OnPlayerDowned(this);
    }

    public void Revive(SurvivalPlayer by, float health)
    {
        if (!Downed) return;
        Downed = false;
        Health = Mathf.Clamp(health, 1f, MaxHealth);
        invulnerableUntil = Time.time + 2f;
        LastHurt = Time.time;
        helpTag.gameObject.SetActive(false);
        PlayAnim("Idle", true);
        if (by != null)
        {
            by.Revives++;
            if (Director.Net != null) Director.Notify($"{by.DisplayName} reanima a {DisplayName}");
            else Director.Notify(by.IsHuman ? $"¡Has reanimado a {DisplayName}!" : $"{by.DisplayName} reanima a {(IsHuman ? "ti" : DisplayName)}");
        }
        EscapeAudio.Play("spring", transform.position, .6f);
    }

    // Furia power-up: double damage and a faster stride for a few seconds.
    float rageUntil, nextAura;
    public readonly int[] Perks = new int[SurvivalPerks.Count];
    int P(SurvivalPerks.Kind k) => Perks[(int)k];
    public bool Raging => Time.time < rageUntil;
    public void Rage(float seconds) { rageUntil = Mathf.Max(rageUntil, Time.time + seconds); }
    public void Heal(float amount) { if (!Downed) Health = Mathf.Min(MaxHealth, Health + amount); }

    // ------------------------------------------------------------------ parts and helping

    void ProcessUse(bool held, float dt)
    {
        UseKind kind = UseKind.None;
        SurvivalPlayer who = null;
        if (Carried != null && Director.Level.InTruckZone(transform.position)) kind = UseKind.Install;
        else
        {
            float best = 2.3f;
            foreach (var p in Director.Team)
            {
                if (p == this || !p.Downed) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < best) { best = d; who = p; kind = UseKind.Revive; }
            }
        }
        if (kind != Focus || who != FocusPlayer) { Focus = kind; FocusPlayer = who; UseProgress = 0f; }
        if (kind == UseKind.None) return;
        float hold = kind == UseKind.Install ? 1.1f : 1.8f;
        if (held)
        {
            UseProgress += dt / hold;
            if (UseProgress >= 1f)
            {
                UseProgress = 0f;
                Focus = UseKind.None; FocusPlayer = null;
                if (ClientOwned) { }   // online: the host completes it with this player's controls
                else if (kind == UseKind.Install) Director.InstallPart(this);
                else if (who != null) who.Revive(this, 50f);
            }
        }
        else UseProgress = Mathf.MoveTowards(UseProgress, 0f, dt * 2f);
    }

    public void Carry(SurvivalPart part) { Carried = part; }
    public void ReleaseCarried() { Carried = null; }

    public void Drop()
    {
        if (Carried == null) return;
        var part = Carried;
        Carried = null;
        part.DropAt(Director.Level.ClampInside(transform.position + transform.forward * .9f, 2f));
    }

    // ------------------------------------------------------------------ helpers

    public void Teleport(Vector3 position)
    {
        Controller.enabled = false;
        transform.position = position;
        vertical = 0f; push = Vector3.zero;
        previous = position;
        Controller.enabled = true;
        if (IsHuman) Director.View?.Snap();
        if (NetDriven) { NetPose.Clear(); Director.Net?.SendTeleport(this, position); }
    }

    // ------------------------------------------------------------------ online (2026-11-12)

    /// <summary>Host: a remote player's survivor. Client: someone else's survivor.</summary>
    void NetUpdate(float dt)
    {
        if (NetPose.Get(out var p, out var r)) transform.SetPositionAndRotation(p, r);
        Vector3 flat = transform.position - previous; flat.y = 0f;
        Speed = Mathf.Lerp(Speed, flat.magnitude / Mathf.Max(dt, .0001f), .3f);
        previous = transform.position;
        if (NetDriven)
        {
            bool canAct = Director.Fighting && !Downed;
            if (Downed) { if (Director.Fighting && Time.time - DownedAt > AutoReviveSeconds) { Teleport(Director.Level.SpawnPoint(Slot)); Revive(null, 50f); } }
            else if (canAct)
            {
                if (BotAttack && Carried == null && Time.time >= nextSwing) Swing();
                ProcessUse(BotUse, dt);
                Director.TryPickup(this);
                if (Time.time - LastHurt > 4f / (1f + P(SurvivalPerks.Kind.Regeneracion)) && Health < MaxHealth) Health = Mathf.Min(MaxHealth, Health + 5f * (1f + P(SurvivalPerks.Kind.Regeneracion)) * dt);
            }
            else { Focus = UseKind.None; UseProgress = 0f; }
        }
        TickSwing();
        Animate();
    }

    /// <summary>Client: this survivor's state from the host.</summary>
    public void ApplyNet(float health, bool downed, int kills, int parts, int revives, int downs, byte[] perks, float rage, int swingSeq, int netCombo, int hurtSeq)
    {
        Health = health; Kills = kills; Parts = parts; Revives = revives; Downs = downs;
        for (int i = 0; i < Perks.Length && i < perks.Length; i++) Perks[i] = perks[i];
        rageUntil = rage > 0f ? Time.time + rage : 0f;
        if (downed && !Downed)
        {
            Downed = true; DownedAt = Time.time; Focus = UseKind.None; UseProgress = 0f;
            helpTag.gameObject.SetActive(true); PlayAnim("Death", true); EscapeAudio.Play("ko", transform.position, .8f);
        }
        else if (!downed && Downed)
        {
            Downed = false; invulnerableUntil = Time.time + 2f; helpTag.gameObject.SetActive(false); PlayAnim("Idle", true);
            EscapeAudio.Play("spring", transform.position, .6f);
        }
        if (Puppet && netSwingSeq >= 0 && swingSeq != netSwingSeq)
        {   // show the other player's swing (the host already applied the hits)
            combo = netCombo; swingStart = Time.time; swingResolved = true;
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            EscapeAudio.Play("whoosh_" + Random.Range(0, 3), transform.position, .25f, .12f);
        }
        netSwingSeq = swingSeq;
        if (netHurtSeq >= 0 && hurtSeq != netHurtSeq && !Downed)
        {
            hurtAnimUntil = Time.time + .3f; PlayAnim("HitReact", true);
            EscapeAudio.Play("impact_" + Random.Range(0, 3), transform.position, IsHuman ? .6f : .3f, .1f);
            if (IsHuman) { Director.HUD?.Hurt(15f); Director.View?.Shake(.22f); LastHurt = Time.time; }
        }
        netHurtSeq = hurtSeq;
    }

    /// <summary>Client: a knock-back the host gave this survivor.</summary>
    public void AddPush(Vector3 knock) { push += knock; }

    /// <summary>Rides in the truck bed at the end (no physics).</summary>
    public void Board(Transform seat)
    {
        if (Carried != null) Drop();
        Controller.enabled = false;
        transform.SetParent(seat, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        Downed = false;
        helpTag.gameObject.SetActive(false);
        PlayAnim("Wave", true);
        enabled = false;
    }

    void Animate()
    {
        if (animator == null || Downed || Time.time < hurtAnimUntil) return;
        string state = Speed > 4.2f ? "Run" : Speed > .4f ? "Walk" : "Idle";
        PlayAnim(state, false);
        animator.speed = state == "Walk" ? Mathf.Clamp(Speed / 3.4f, .8f, 1.7f) : 1f;
    }

    void PlayAnim(string state, bool restart)
    {
        if (animator == null) return;
        int hash = Animator.StringToHash("Base Layer." + state);
        if (!animator.HasState(0, hash) || (!restart && state == animState)) return;
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(hash, .12f, 0, 0f);
        animState = state;
    }
}
