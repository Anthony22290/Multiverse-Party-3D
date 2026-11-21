using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One zombie (or boss). Walks in through a gate, chases the nearest standing survivor with simple steering
/// (no NavMesh: the plaza is open), punches with a short wind-up, staggers when hit and falls in a cartoon puff.
/// Marked zombies and bosses carry a truck part and drop it when defeated.
/// Bosses: El Gordo (ground slam), Brazos Largos (charge), Costillas (summons runners), Rey Zombi (all three, enrages at half health).
/// </summary>
public sealed class SurvivalZombie : MonoBehaviour
{
    public enum ZombieKind { Basic, Runner, Chubby, Arm, Skeleton, Bomber, Blob, MiniBlob, Ghost, Brute }
    public enum BossKind { None, Gordo, Brazos, Costillas, Rey }

    public struct Spec { public ZombieKind kind; public BossKind boss; public bool elite; public int wave, part; public bool revived, guard; }

    public static readonly List<SurvivalZombie> All = new();

    //                         Basic Runner Chubby Arm | Skeleton Bomber Blob MiniBlob Ghost Brute
    static readonly float[] Hp = { 60f, 40f, 170f, 95f, 45f, 70f, 85f, 26f, 40f, 330f };
    static readonly float[] Spd = { 2.5f, 4.3f, 1.8f, 2.9f, 3.4f, 2.7f, 2.2f, 3.9f, 3.8f, 2.1f };
    static readonly float[] Dmg = { 10f, 7f, 18f, 13f, 9f, 8f, 12f, 6f, 8f, 24f };
    static readonly float[] Reach = { 1.4f, 1.3f, 1.65f, 2.2f, 1.4f, 1.4f, 1.5f, 1.1f, 1.4f, 2.3f };
    static readonly float[] Hgt = { 1.8f, 1.75f, 1.95f, 1.85f, 1.85f, 1.95f, 1.5f, .85f, 1.9f, 3.1f };
    static readonly float[] Rad = { .36f, .34f, .55f, .4f, .34f, .4f, .6f, .35f, .42f, .9f };
    static string MonsterKey(ZombieKind k) => k switch { ZombieKind.Skeleton => "Esqueleto", ZombieKind.Bomber => "Lanzabombas", ZombieKind.Blob or ZombieKind.MiniBlob => "Blob", ZombieKind.Ghost => "Fantasma", ZombieKind.Brute => "Bruto", _ => null };
    // 2026-11-10 roguelike enemies: skeletons reassemble, bombers lob bombs, blobs split, ghosts phase out, brutes hit hard.
    public bool Guard { get; private set; }
    public bool Revived { get; private set; }
    bool awake, lastHitHard; float assembleUntil, nextThrow, phaseOffset; Transform visualRoot; Vector3 visualBase;
    // Jefes con modelo Meshy: solo traen caminar/correr, así que golpe, aturdimiento y muerte se animan por código.
    bool procAnim; string procState; float procStart; Quaternion visualRotBase;
    public bool Intangible => Puppet ? netIntangible && !Dead : Kind == ZombieKind.Ghost && !Dead && Mathf.Repeat(Time.time + phaseOffset, 5f) > 3.7f;

    // ---- online (2026-11-12): the host numbers every zombie; clients show copies that only follow its state.
    public int NetId;
    public bool Puppet { get; private set; }
    public NetPose NetPose { get; } = new NetPose();
    public ZombieKind SpecKind { get; private set; }
    public int SpecWave { get; private set; }
    public int ActionState => action;
    public int AnimSeq { get; private set; }
    public byte AnimCode { get; private set; }
    public int HitSeq { get; private set; }
    bool netIntangible, netAnimInit, netHitInit; int netAnimSeq, netHitSeq; float netSpeed, actionShownAt;
    static readonly string[] AnimCodes = { null, "Punch", "Idle_Attack", "HitReact", "Death" };
    public void MakePuppet() => Puppet = true;

    public static string BossTitle(BossKind b) => b switch
    {
        BossKind.Gordo => "EL GORDO",
        BossKind.Brazos => "BRAZOS LARGOS",
        BossKind.Costillas => "COSTILLAS",
        BossKind.Rey => "EL REY ZOMBI",
        _ => ""
    };

    public SurvivalDirector Director { get; private set; }
    public ZombieKind Kind { get; private set; }
    public BossKind Boss { get; private set; }
    public bool IsBoss => Boss != BossKind.None;
    public bool Elite { get; private set; }
    public int PartIndex { get; private set; } = -1;
    public float MaxHealth { get; private set; }
    public float Health { get; private set; }
    public float Radius { get; private set; }
    public float Height { get; private set; }
    public string Title { get; private set; }
    public bool Dead { get; private set; }
    public bool Enraged => Boss == BossKind.Rey && Health < MaxHealth * .5f;
    public bool Slamming => action == 1;
    public bool Charging => action == 2 || action == 3;
    public Vector3 ChargeDirection => chargeDir;
    public const float SlamRadius = 4.8f;

    CharacterController cc;
    Animator anim;
    string animState;
    float speed, damage, reach, vertical, staggerUntil, nextAttackAt, attackHitAt = -1f, retargetAt, deathTime, stuckTimer, sidestepUntil, avoidAt;
    Vector3 push, gateInner, lastPos, sidestep, avoidDir = Vector3.forward;
    bool entered, frozen;
    SurvivalPlayer target;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    float flashUntil;
    Transform ring;
    TextMesh floatingLabel;
    // boss
    int action; // 0 none, 1 slam wind-up, 2 charge wind-up, 3 charging
    float actionUntil, nextSlam, nextCharge, nextSummon;
    Vector3 chargeDir;
    readonly HashSet<SurvivalPlayer> chargeHits = new();
    Transform telegraph;
    Material telegraphMat;
    static readonly float[] Angles = { 30f, -30f, 60f, -60f, 95f, -95f };

    public void Initialize(SurvivalDirector director, Spec spec, Vector3 inner, bool alreadyInside)
    {
        Director = director; Kind = spec.kind; Boss = spec.boss; Elite = spec.elite; PartIndex = spec.part; SpecKind = spec.kind; SpecWave = spec.wave;
        gateInner = inner; entered = alreadyInside;
        int k = (int)Kind;
        float waveScale = 1f + .09f * (spec.wave - 1);
        MaxHealth = Hp[k] * waveScale; speed = Spd[k] * (1f + .015f * (spec.wave - 1)); damage = Dmg[k] * (1f + .06f * (spec.wave - 1)); reach = Reach[k];
        Height = Hgt[k]; Radius = Rad[k];
        Title = Elite ? "Zombi brillante" : Kind switch { ZombieKind.Runner => "Corredor", ZombieKind.Chubby => "Gordito", ZombieKind.Arm => "Brazudo", ZombieKind.Skeleton => "Esqueleto", ZombieKind.Bomber => "Lanzabombas", ZombieKind.Blob => "Blob", ZombieKind.MiniBlob => "Blobito", ZombieKind.Ghost => "Fantasma", ZombieKind.Brute => "Bruto", _ => "Caminante" };
        Guard = spec.guard; Revived = spec.revived; phaseOffset = Random.value * 5f; nextThrow = Time.time + Random.Range(1.5f, 3f);
        if (Revived) { assembleUntil = Time.time + 1.4f; MaxHealth = Hp[k] * .6f; }

        if (Elite) { MaxHealth *= 2.4f; damage *= 1.3f; Height *= 1.3f; Radius *= 1.25f; speed *= 1.05f; }
        if (IsBoss)
        {
            Kind = Boss switch { BossKind.Gordo => ZombieKind.Chubby, BossKind.Brazos => ZombieKind.Arm, BossKind.Costillas => ZombieKind.Runner, _ => ZombieKind.Basic };
            float team = .55f + .15f * director.PlayerCount;
            (MaxHealth, damage, speed, reach, Height, Radius) = Boss switch
            {
                BossKind.Gordo => (700f, 22f, 2.0f, 2.6f, 3.8f, 1.05f),
                BossKind.Brazos => (800f, 20f, 2.6f, 3.4f, 3.9f, .95f),
                BossKind.Costillas => (650f, 14f, 3.6f, 2.4f, 3.7f, .9f),
                _ => (1600f, 26f, 2.4f, 3.0f, 4.4f, 1.15f)
            };
            MaxHealth *= team;
            Title = BossTitle(Boss);
            nextSlam = Time.time + 4f; nextCharge = Time.time + 5f; nextSummon = Time.time + 6f;
        }
        Health = MaxHealth;
        damage *= director.PlayerCount <= 2 ? .8f : director.PlayerCount == 3 ? .9f : 1f; // small teams get a little slack

        cc = gameObject.AddComponent<CharacterController>();
        cc.height = Height; cc.radius = Mathf.Min(Radius, Height * .45f); cc.center = Vector3.up * Height * .5f;
        cc.stepOffset = Mathf.Min(.4f, Height * .2f); cc.slopeLimit = 50f; cc.skinWidth = .04f;
        foreach (var b in director.Level.GateBlockers) if (b != null) Physics.IgnoreCollision(cc, b);

        var art = SurvivalArtSet.Get;
        var monster = MonsterKey(Kind);
        GameObject visual;
        var bossModel = IsBoss ? Resources.Load<GameObject>("SurvivalBosses/" + Boss) : null;
        if (bossModel != null)
        {
            visual = PirateArenaVisuals.AddModel(bossModel, transform, Height, null, false);
            procAnim = visual != null;
        }
        else if (monster != null)
        {
            var mp = Resources.Load<GameObject>("SurvivalMonsters/" + monster);
            var mm = Resources.Load<Material>("SurvivalMonsters/" + (Kind == ZombieKind.Skeleton ? "Pirata" : "Monstruos"));
            visual = mp != null ? PirateArenaVisuals.AddModel(mp, transform, Height, mm, false) : null;
            if (visual != null && Kind == ZombieKind.MiniBlob) foreach (var r in visual.GetComponentsInChildren<Renderer>()) { var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b); b.SetColor("_BaseColor", new Color(.75f, 1.2f, .6f)); r.SetPropertyBlock(b); }
        }
        else
        {
            var prefab = art != null && art.zombies != null && art.zombies.Length > k ? art.zombies[(int)Kind] : null;
            visual = prefab != null ? PirateArenaVisuals.AddModel(prefab, transform, Height, art.atlas, false) : null;
        }
        if (visual != null) { visualRoot = visual.transform; visualBase = visualRoot.localPosition; visualRotBase = visualRoot.localRotation; }
        if (visual != null)
        {
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Destroy(c);
            anim = visual.GetComponentInChildren<Animator>();
            if (anim != null) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            renderers = visual.GetComponentsInChildren<Renderer>();
        }
        else
        {
            var body = EscapeKit.Shape(PrimitiveType.Capsule, transform, "Cuerpo", Vector3.up * Height * .5f, new Vector3(Radius * 2f, Height * .5f, Radius * 2f), EscapeKit.Paint(new Color(.45f, .7f, .35f)), false);
            renderers = new[] { body.GetComponent<Renderer>() };
        }
        block = new MaterialPropertyBlock();

        if (Elite || IsBoss)
        {
            Color c = IsBoss ? new Color(1f, .35f, .3f) : new Color(.55f, 1f, .35f);
            ring = EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Aura", Vector3.up * .04f, new Vector3(Radius * 3.2f, .015f, Radius * 3.2f), EscapeKit.Glow(c, 1.6f), false).transform;
            string label = IsBoss ? Title : "LLEVA: " + SurvivalDirector.PartNames[PartIndex].ToUpperInvariant();
            floatingLabel = EscapeKit.Text(transform, label, Vector3.up * (Height + .55f), Vector3.zero, IsBoss ? .05f : .028f, c);
            if (!IsBoss) EscapeKit.PointLight(transform, Vector3.up * 1.2f, c, 5f, 2.2f);
        }
        if (Boss == BossKind.Rey && !procAnim) // el modelo Meshy ya trae corona
        {
            var crown = new GameObject("Corona").transform;
            crown.SetParent(transform, false); crown.localPosition = Vector3.up * (Height * .97f);
            var gold = EscapeKit.Glow(new Color(1f, .78f, .2f), .5f);
            EscapeKit.Shape(PrimitiveType.Cylinder, crown, "Aro", Vector3.zero, new Vector3(.8f, .12f, .8f), gold, false);
            for (int i = 0; i < 5; i++)
                EscapeKit.Shape(PrimitiveType.Cube, crown, "Pico", Quaternion.Euler(0, i * 72f, 0) * Vector3.forward * .36f + Vector3.up * .2f, new Vector3(.14f, .32f, .14f), gold, false, new Vector3(0, i * 72f, 45));
        }
        All.Add(this);
        lastPos = transform.position;
    }

    // ------------------------------------------------------------------ per frame

    void Update()
    {
        if (Director == null) return;
        float dt = Time.deltaTime;
        if (Dead)
        {
            deathTime += dt;
            TickProcAnim();
            if (deathTime > 1.3f) transform.position += Vector3.down * dt * 1.4f;
            if (deathTime > 2.2f) Destroy(gameObject);
            return;
        }
        if (Puppet) { PuppetTick(dt); return; }
        TickProcAnim();
        if (floatingLabel != null && Director.View != null) floatingLabel.transform.rotation = Director.View.transform.rotation;
        if (ring != null) ring.localScale = new Vector3(Radius * 3.2f, .015f, Radius * 3.2f) * (1f + Mathf.Sin(Time.time * 6f) * .08f);
        Flash();

        Vector3 move = Vector3.zero;
        float spd = speed * (Enraged ? 1.35f : 1f) * Director.SpeedMod;
        bool active = Director.Fighting && !frozen;
        if (visualRoot != null)
        {
            if (Kind == ZombieKind.Ghost) visualRoot.localPosition = visualBase + Vector3.up * (.55f + Mathf.Sin(Time.time * 2.2f + phaseOffset) * .18f);
            if (Time.time < assembleUntil) visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, .25f, (assembleUntil - Time.time) / 1.4f);
            else if (Revived && visualRoot.localScale.x < .99f) visualRoot.localScale = Vector3.one;
            if (Kind == ZombieKind.Ghost && renderers != null) { bool show = !Intangible || Mathf.Repeat(Time.time * 12f, 1f) > .6f; foreach (var r in renderers) if (r != null) r.enabled = show; }
        }
        if (Time.time < assembleUntil) active = false;
        if (active && Guard && !awake)
        {   // guardians wait at their post until someone comes close or hits them
            var near = Director.NearestStanding(transform.position, 13f);
            if (near != null || Health < MaxHealth) { awake = true; EscapeAudio.Play("zroar", transform.position, .5f); }
            else active = false;
        }
        if (active)
        {
            if (Time.time > retargetAt || target == null || target.Downed)
            {
                target = Director.NearestStanding(transform.position);
                retargetAt = Time.time + .35f + Random.value * .25f;
            }
            if (action != 0) move = TickBossAction(ref spd);
            else if (Time.time >= staggerUntil && attackHitAt < 0f)
            {
                Vector3 goal = target != null ? target.transform.position : Director.Level.TruckPos;
                if (!entered)
                {
                    if (Director.Level.Inside(transform.position, .5f)) entered = true;
                    else goal = gateInner;
                }
                Vector3 to = goal - transform.position; to.y = 0f;
                float dist = to.magnitude;
                float contact = reach + .34f;
                if (Kind == ZombieKind.Bomber && entered && target != null && dist > 1.8f)
                {   // keep a throwing distance and lob bombs
                    if (dist < 7f) move = Steer(-to / dist) * .8f;
                    else if (dist > 13f) move = Steer(to / dist);
                    if (dist < 17f && Time.time >= nextThrow)
                    {
                        nextThrow = Time.time + Random.Range(3.2f, 4.4f);
                        Play("Punch", true); EscapeAudio.Play("whoosh_" + Random.Range(0, 3), transform.position, .6f);
                        SurvivalBomb.Throw(Director, transform.position + Vector3.up * Height * .9f, target.transform.position, this);
                    }
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 6f);
                }
                else if (entered && target != null && dist <= contact) TryAttack();
                else if (dist > .1f) move = Steer(to / dist);
                if (entered && target != null) BossThink(dist);
                Unstick(dist);
            }
            ResolveAttack();
        }

        // Physics: gravity, knockback and the move itself.
        if (cc.isGrounded && vertical < 0f) vertical = -2f;
        vertical = Mathf.Max(-25f, vertical - 22f * dt);
        push = Vector3.MoveTowards(push, Vector3.zero, dt * 14f);
        if (cc.enabled) cc.Move((move * spd + push + Vector3.up * vertical) * dt);   // a zombie that just died may have its controller off

        Vector3 face = attackHitAt > 0f || action == 1 || action == 2 ? (target != null ? target.transform.position - transform.position : transform.forward) : move;
        if (action == 2 || action == 3) face = chargeDir;
        face.y = 0f;
        if (face.sqrMagnitude > .001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), dt * (IsBoss ? 6f : 10f));

        Animate(move.magnitude * spd);
    }

    Vector3 Steer(Vector3 dir)
    {
        // Obstacle avoidance (throttled) + crowd separation.
        if (Time.time > avoidAt)
        {
            avoidAt = Time.time + .2f + Random.value * .1f;
            avoidDir = Avoid(dir);
        }
        Vector3 d = Vector3.Angle(avoidDir, dir) < 100f ? avoidDir : dir;
        if (Time.time < sidestepUntil) d = sidestep;
        Vector3 sep = Vector3.zero;
        Vector3 p = transform.position;
        foreach (var o in All)
        {
            if (o == this || o.Dead) continue;
            Vector3 off = p - o.transform.position; off.y = 0f;
            float want = Radius + o.Radius + .25f;
            float m = off.magnitude;
            if (m < want && m > .001f) sep += off / m * (want - m) / want * (o.IsBoss ? 2.5f : 1.2f);
        }
        if (IsBoss) sep *= .2f;
        Vector3 result = d + sep;
        return result.sqrMagnitude > 1f ? result.normalized : result;
    }

    Vector3 Avoid(Vector3 dir)
    {
        if (!Blocked(dir)) return dir;
        foreach (float a in Angles)
        {
            var alt = Quaternion.Euler(0, a, 0) * dir;
            if (!Blocked(alt)) return alt;
        }
        return dir;
    }

    bool Blocked(Vector3 dir)
    {
        Vector3 origin = transform.position + Vector3.up * Mathf.Min(1f, Height * .5f);
        foreach (var h in Physics.SphereCastAll(origin, Mathf.Min(Radius, .5f), dir, 1.4f + Radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider == cc || h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponent<CharacterController>() != null) continue; // people and zombies: separation handles them
            if (h.distance <= 0f && h.point == Vector3.zero) continue;
            if (h.collider.bounds.max.y < transform.position.y + cc.stepOffset) continue;
            if (Director.Level.GateBlockers.Contains(h.collider)) continue; // zombies walk through the gates
            return true;
        }
        return false;
    }

    void Unstick(float dist)
    {
        stuckTimer += Time.deltaTime;
        if (stuckTimer < 1.5f) return;
        if ((transform.position - lastPos).sqrMagnitude < .15f && dist > reach + 1f)
        {
            sidestep = Quaternion.Euler(0, Random.value < .5f ? 75f : -75f, 0) * (avoidDir.sqrMagnitude > .01f ? avoidDir : transform.forward);
            sidestepUntil = Time.time + .7f;
            if (!entered && (transform.position - gateInner).magnitude > 4f) Teleport(gateInner);
        }
        lastPos = transform.position; stuckTimer = 0f;
    }

    void Teleport(Vector3 p)
    {
        cc.enabled = false; transform.position = p; cc.enabled = true;
    }

    // ------------------------------------------------------------------ attacks

    void TryAttack()
    {
        if (Time.time < nextAttackAt || target == null) return;
        float windup = IsBoss ? .65f : Kind == ZombieKind.Chubby ? .6f : .45f;
        attackHitAt = Time.time + windup;
        nextAttackAt = Time.time + windup + (IsBoss ? 1f : 1.05f);
        Play("Punch", true);
        if (Random.value < .35f) EscapeAudio.Play("zgroan_" + Random.Range(0, 3), transform.position, .55f, .15f);
    }

    void ResolveAttack()
    {
        if (attackHitAt < 0f || Time.time < attackHitAt) return;
        attackHitAt = -1f;
        if (target == null || target.Downed) return;
        Vector3 d = target.transform.position - transform.position; d.y = 0f;
        if (d.magnitude <= reach + .95f && (d.magnitude < .8f || Vector3.Angle(transform.forward, d) < 80f))
            target.Damage(damage, d.normalized * (IsBoss ? 8f : Kind == ZombieKind.Brute ? 11f : Kind == ZombieKind.Chubby ? 6f : 3.5f), this);
    }

    void BossThink(float dist)
    {
        if (!IsBoss || action != 0 || target == null) return;
        float cd = Enraged ? .65f : 1f;
        bool slam = Boss == BossKind.Gordo || Boss == BossKind.Rey;
        bool charge = Boss == BossKind.Brazos || (Boss == BossKind.Rey && Enraged);
        bool summon = Boss == BossKind.Costillas || Boss == BossKind.Rey;
        if (slam && Time.time > nextSlam && dist < 6.5f)
        {
            action = 1; actionUntil = Time.time + 1.1f; nextSlam = Time.time + 6.5f * cd;
            ShowTelegraph(true);
            Play("Idle_Attack", true);
            EscapeAudio.Play("zroar", transform.position, .7f);
        }
        else if (charge && Time.time > nextCharge && dist > 4.5f && dist < 18f)
        {
            action = 2; actionUntil = Time.time + .85f; nextCharge = Time.time + 7f * cd;
            chargeDir = target.transform.position - transform.position; chargeDir.y = 0f; chargeDir.Normalize();
            chargeHits.Clear();
            ShowTelegraph(false);
            Play("Idle_Attack", true);
            EscapeAudio.Play("zroar", transform.position, .7f);
        }
        else if (summon && Time.time > nextSummon)
        {
            nextSummon = Time.time + (Boss == BossKind.Rey ? 12f : 9f) * cd;
            Play("Idle_Attack", true);
            staggerUntil = Time.time + .8f;
            Director.SummonMinions(this, (Boss == BossKind.Rey ? 2 : 3) + (Director.PlayerCount >= 3 ? 1 : 0));
        }
    }

    Vector3 TickBossAction(ref float spd)
    {
        float t = Time.time;
        if (action == 1)
        {
            float k = 1f - Mathf.Clamp01((actionUntil - t) / 1.1f);
            if (telegraph != null)
            {
                telegraph.localScale = new Vector3(9.6f * k, .02f, 9.6f * k);
                EscapeKit.SetGlow(telegraphMat, new Color(1f, .25f, .2f), .6f + k * 1.6f);
            }
            if (t >= actionUntil)
            {
                foreach (var p in Director.Team)
                {
                    if (p.Downed) continue;
                    Vector3 d = p.transform.position - transform.position; d.y = 0f;
                    if (d.magnitude < SlamRadius) p.Damage(24f, (d.sqrMagnitude > .01f ? d.normalized : transform.forward) * 11f, this);
                }
                SurvivalKit.Puff(transform.position + Vector3.up * .3f, new Color(.6f, .55f, .45f), 12, 1.2f);
                Director.View?.Shake(.45f);
                EscapeAudio.Play("impact_" + Random.Range(0, 3), transform.position, 1f);
                HideTelegraph();
                action = 0; staggerUntil = t + .7f;
            }
            return Vector3.zero;
        }
        if (action == 2)
        {
            if (telegraph != null) EscapeKit.SetGlow(telegraphMat, new Color(1f, .25f, .2f), .6f + Mathf.PingPong(t * 6f, 1.4f));
            if (t >= actionUntil) { action = 3; actionUntil = t + 1.05f; HideTelegraph(); EscapeAudio.Play("whoosh_0", transform.position, 1f); }
            return Vector3.zero;
        }
        // Charging.
        spd = 15f;
        foreach (var p in Director.Team)
        {
            if (p.Downed || chargeHits.Contains(p)) continue;
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            if (d.magnitude < Radius + .9f) { chargeHits.Add(p); p.Damage(22f, (chargeDir + Vector3.Cross(Vector3.up, chargeDir) * (Vector3.Dot(d, Vector3.Cross(Vector3.up, chargeDir)) > 0 ? 1f : -1f)).normalized * 12f, this); }
        }
        bool wall = Physics.SphereCast(transform.position + Vector3.up, Radius * .8f, chargeDir, out var hit, Radius + .4f, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponent<CharacterController>() == null;
        if (t >= actionUntil || wall || !Director.Level.Inside(transform.position + chargeDir, .5f))
        {
            action = 0; staggerUntil = t + (wall ? 1.4f : .6f);
            if (wall) { Director.View?.Shake(.3f); SurvivalKit.Puff(transform.position + chargeDir * Radius + Vector3.up, new Color(.7f, .7f, .65f), 8, .9f); Play("HitReact", true); }
            return Vector3.zero;
        }
        return chargeDir;
    }

    void ShowTelegraph(bool circle)
    {
        HideTelegraph();
        telegraphMat = EscapeKit.Glow(new Color(1f, .25f, .2f), .8f);
        if (circle)
        {
            telegraph = EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Aviso de golpe", Vector3.up * .05f, new Vector3(.5f, .02f, .5f), telegraphMat, false).transform;
        }
        else
        {
            var root = new GameObject("Aviso de embestida").transform;
            root.SetParent(transform, false);
            root.localPosition = Vector3.up * .05f;
            root.rotation = Quaternion.LookRotation(chargeDir);
            EscapeKit.Shape(PrimitiveType.Cube, root, "Franja", new Vector3(0, 0, 8f), new Vector3(Radius * 2f, .02f, 15f), telegraphMat, false);
            telegraph = root;
        }
    }

    void HideTelegraph()
    {
        if (telegraph != null) Destroy(telegraph.gameObject);
        if (telegraphMat != null) Destroy(telegraphMat);
        telegraph = null; telegraphMat = null;
    }

    // ------------------------------------------------------------------ damage

    public void TakeHit(float amount, Vector3 dir, SurvivalPlayer by)
    {
        if (Dead) return;
        if (Puppet) { flashUntil = Time.time + .1f; MultiverseFX.Burst(transform.position + Vector3.up * Height * .55f - dir * .3f, new Color(.45f, .9f, .3f), 4, 4); return; }   // the host applies the damage
        HitSeq++;
        if (Intangible) { SurvivalKit.Puff(transform.position + Vector3.up * 1.4f, new Color(.7f, .8f, 1f), 3, .4f); return; }   // ghosts phase out
        if (Time.time < assembleUntil) amount *= 2f;
        lastHitHard = amount >= 45f;
        Health -= amount;
        flashUntil = Time.time + .1f;
        MultiverseFX.Burst(transform.position + Vector3.up * Height * .55f - dir * .3f, new Color(.45f, .9f, .3f), by != null && by.Raging ? 10 : 5, 4);   // goo splat
        push += dir * (IsBoss ? .8f : Kind == ZombieKind.Brute ? 1.2f : Kind == ZombieKind.Chubby ? 3f : 6.5f);
        if (!IsBoss && action == 0)
        {
            staggerUntil = Time.time + (Kind == ZombieKind.Chubby ? .2f : .32f);
            attackHitAt = -1f;
            Play("HitReact", true);
        }
        if (Health <= 0f) Die(by);
    }

    public void Stagger(float seconds) => staggerUntil = Mathf.Max(staggerUntil, Time.time + seconds);

    void Die(SurvivalPlayer by)
    {
        Dead = true;
        All.Remove(this);
        HideTelegraph();
        cc.enabled = false;
        if (floatingLabel != null) floatingLabel.gameObject.SetActive(false);
        if (ring != null) ring.gameObject.SetActive(false);
        foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
        Play("Death", true);
        SurvivalKit.Puff(transform.position + Vector3.up * Height * .4f, new Color(.5f, .85f, .4f), IsBoss ? 16 : 7, IsBoss ? 1.4f : .55f);
        EscapeAudio.Play(IsBoss ? "zroar" : "zdie", transform.position, IsBoss ? 1f : .6f, .15f);
        if (Kind == ZombieKind.Skeleton && !Revived && !lastHitHard) { Director.QueueRevive(transform.position); SurvivalKit.Puff(transform.position + Vector3.up * .5f, new Color(.9f, .88f, .8f), 8, .5f); }
        if (Kind == ZombieKind.Blob) Director.SplitBlob(this);
        Director.OnZombieKilled(this, by);
    }

    /// <summary>The truck got away: stop chasing and just stare.</summary>
    public void Freeze()
    {
        frozen = true; attackHitAt = -1f; action = 0; HideTelegraph();
    }

    void Flash()
    {
        if (renderers == null) return;
        bool on = Time.time < flashUntil;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", on ? new Color(2.2f, 2.2f, 2.2f) : Color.white);
            r.SetPropertyBlock(block);
        }
    }

    void Animate(float velocity)
    {
        if (anim == null || Dead) return;
        if (Time.time < staggerUntil || attackHitAt > 0f || action == 1 || action == 2) return;
        string state = action == 3 ? "Run_Attack" : velocity > 3.6f ? "Run" : velocity > .3f ? "Walk" : "Idle";
        Play(state, false);
        anim.speed = state == "Walk" ? Mathf.Clamp(velocity / (IsBoss ? 1.6f : 2.2f), .7f, 1.8f) : 1f;
    }

    void Play(string state, bool restart)
    {
        if (procAnim && (state == "Punch" || state == "Idle_Attack" || state == "HitReact" || state == "Death") && (restart || procState != state))
        { procState = state; procStart = Time.time; }
        if (restart) { AnimSeq++; AnimCode = (byte)System.Array.IndexOf(AnimCodes, state); if (AnimCode == 255) AnimCode = 0; }
        if (anim == null) return;
        int hash = Animator.StringToHash("Base Layer." + state);
        if (!anim.HasState(0, hash)) { if (state == "Run_Attack") state = "Run"; else if (state == "Idle_Attack") state = "Punch"; else return; hash = Animator.StringToHash("Base Layer." + state); if (!anim.HasState(0, hash)) return; }
        if (!restart && state == animState) return;
        anim.speed = 1f;
        anim.CrossFadeInFixedTime(hash, .1f, 0, 0f);
        animState = state;
    }

    /// <summary>Inclinaciones simples sobre el modelo (pivote en los pies) para los jefes sin clips de ataque o muerte.</summary>
    void TickProcAnim()
    {
        if (!procAnim || visualRoot == null || procState == null) return;
        float t = Time.time - procStart, lean = 0f, push = 0f, drop = 0f;
        switch (procState)
        {
            case "Punch": { float k = Mathf.Clamp01(t / .45f); float s = Mathf.Sin(k * Mathf.PI); lean = 20f * s; push = .35f * s; if (k >= 1f) procState = null; break; }
            case "Idle_Attack": { float k = Mathf.Clamp01(t / .7f); lean = k < .55f ? -14f * Mathf.Sin(k / .55f * Mathf.PI * .5f) : Mathf.Lerp(-14f, 24f, Mathf.Sin((k - .55f) / .45f * Mathf.PI)); push = .25f * Mathf.Max(0f, lean) / 24f; if (k >= 1f) procState = null; break; }
            case "HitReact": { float k = Mathf.Clamp01(t / .3f); lean = -12f * Mathf.Sin(k * Mathf.PI); if (k >= 1f) procState = null; break; }
            case "Death": { float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .75f)); lean = -84f * k; drop = .1f * k; break; }
        }
        visualRoot.localRotation = Quaternion.Euler(lean, 0f, 0f) * visualRotBase;
        visualRoot.localPosition = visualBase + Vector3.forward * push + Vector3.down * drop;
        if (procState == null) { visualRoot.localRotation = visualRotBase; visualRoot.localPosition = visualBase; }
    }

    // ------------------------------------------------------------------ online copy (client)

    void PuppetTick(float dt)
    {
        TickProcAnim();
        if (floatingLabel != null && Director.View != null) floatingLabel.transform.rotation = Director.View.transform.rotation;
        if (ring != null) ring.localScale = new Vector3(Radius * 3.2f, .015f, Radius * 3.2f) * (1f + Mathf.Sin(Time.time * 6f) * .08f);
        Flash();
        if (visualRoot != null)
        {
            if (Kind == ZombieKind.Ghost) visualRoot.localPosition = visualBase + Vector3.up * (.55f + Mathf.Sin(Time.time * 2.2f + phaseOffset) * .18f);
            if (Time.time < assembleUntil) visualRoot.localScale = Vector3.one * Mathf.Lerp(1f, .25f, (assembleUntil - Time.time) / 1.4f);
            else if (Revived && visualRoot.localScale.x < .99f) visualRoot.localScale = Vector3.one;
            if (Kind == ZombieKind.Ghost && renderers != null) { bool show = !Intangible || Mathf.Repeat(Time.time * 12f, 1f) > .6f; foreach (var r in renderers) if (r != null) r.enabled = show; }
        }
        if (NetPose.Get(out var p, out var rot)) transform.SetPositionAndRotation(p, rot);
        Vector3 flat = transform.position - lastPos; flat.y = 0f; lastPos = transform.position;
        netSpeed = Mathf.Lerp(netSpeed, flat.magnitude / Mathf.Max(dt, .0001f), .25f);
        if (action == 1 && telegraph != null)
        {
            float k = Mathf.Clamp01((Time.time - actionShownAt) / 1.1f);
            telegraph.localScale = new Vector3(9.6f * k, .02f, 9.6f * k);
            EscapeKit.SetGlow(telegraphMat, new Color(1f, .25f, .2f), .6f + k * 1.6f);
        }
        else if (action == 2 && telegraph != null) EscapeKit.SetGlow(telegraphMat, new Color(1f, .25f, .2f), .6f + Mathf.PingPong(Time.time * 6f, 1.4f));
        Animate(netSpeed);
    }

    /// <summary>Client: the host's state for this zombie.</summary>
    public void ApplyNet(Vector3 pos, float yaw, float health, float max, int netAction, float chargeYaw, int animSeq, byte animCode, int hitSeq, bool intangible)
    {
        NetPose.Add(pos, Quaternion.Euler(0, yaw, 0));
        Health = health; MaxHealth = max; netIntangible = intangible;
        if (netAction != action)
        {
            int old = action; action = netAction;
            if (action == 1) { ShowTelegraph(true); actionShownAt = Time.time; }
            else if (action == 2) { chargeDir = Quaternion.Euler(0, chargeYaw, 0) * Vector3.forward; ShowTelegraph(false); }
            else if (action != 3 || old != 2) HideTelegraph();
            if (action == 3) HideTelegraph();
            if (old == 1 && action != 1)
            {   // the slam landed
                SurvivalKit.Puff(transform.position + Vector3.up * .3f, new Color(.6f, .55f, .45f), 12, 1.2f);
                if (Director.Human != null && Vector3.Distance(Director.Human.transform.position, transform.position) < 12f) Director.View?.Shake(.45f);
                EscapeAudio.Play("impact_" + Random.Range(0, 3), transform.position, 1f);
            }
            if (action == 1 || action == 2) EscapeAudio.Play("zroar", transform.position, .7f);
        }
        if (netAnimInit && animSeq != netAnimSeq && animCode > 0 && animCode < AnimCodes.Length && animCode != 4)
        {
            Play(AnimCodes[animCode], true);
            staggerUntil = Time.time + (animCode == 3 ? .3f : .45f);
            if (animCode == 1 && Random.value < .35f) EscapeAudio.Play("zgroan_" + Random.Range(0, 3), transform.position, .55f, .15f);
        }
        netAnimSeq = animSeq; netAnimInit = true;
        if (netHitInit && hitSeq != netHitSeq) { flashUntil = Time.time + .1f; MultiverseFX.Burst(transform.position + Vector3.up * Height * .55f, new Color(.45f, .9f, .3f), 5, 4); }
        netHitSeq = hitSeq; netHitInit = true;
    }

    /// <summary>Client: the host's zombie died (or vanished).</summary>
    public void PuppetDie()
    {
        if (Dead) return;
        Dead = true;
        All.Remove(this);
        HideTelegraph();
        if (cc != null) cc.enabled = false;
        if (floatingLabel != null) floatingLabel.gameObject.SetActive(false);
        if (ring != null) ring.gameObject.SetActive(false);
        foreach (var l in GetComponentsInChildren<Light>()) l.enabled = false;
        Play("Death", true);
        SurvivalKit.Puff(transform.position + Vector3.up * Height * .4f, new Color(.5f, .85f, .4f), IsBoss ? 16 : 7, IsBoss ? 1.4f : .55f);
        EscapeAudio.Play(IsBoss ? "zroar" : "zdie", transform.position, IsBoss ? 1f : .6f, .15f);
        if (IsBoss) Director.View?.Shake(.6f);
    }

    void OnDestroy()
    {
        All.Remove(this);
        if (telegraphMat != null) Destroy(telegraphMat);
    }
}
