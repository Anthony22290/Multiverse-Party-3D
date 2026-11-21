using UnityEngine;
using UnityEngine.InputSystem;
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class CrazyKartController : MonoBehaviour
{
    [SerializeField] bool isHuman = true;
    [SerializeField] string displayName = "Corredor";
    [SerializeField] Color kartColor = Color.cyan;
    [SerializeField] float maxSpeed = 22, acceleration = 16, turnSpeed = 95;
    [SerializeField, Range(2, 14)] float steeringResponse = 7f;
    Rigidbody body;
    CrazyKartDirector director;
    float boostUntil, shieldUntil, stunUntil, driftCharge, stuckTime, lastUse;
    bool wasDrifting;
    float smoothedSteer, hitProtectionUntil, finishCoastUntil;
    // Start boost, slipstream, lap timing and bot personality.
    float botReverseUntil = -10, throttlePressedAt = -1, stallUntil, slipCharge, lapStartTime, itemHeldSince, botDriftUntil, botSkill = -1;
    bool pressedTooEarly, startResolved;
    public int StartResult { get; private set; }          // 1 perfect start, -1 stalled, 0 normal
    public float StartResultTime { get; private set; } = -100;
    public float SlipstreamCharge01 => Mathf.Clamp01(slipCharge / SlipstreamTime);
    public float LastSlipstreamBoost { get; private set; } = -100;
    public float LastLapTime { get; private set; }
    public float BestLapTime { get; private set; }
    public float LapRecordTime { get; set; } = -100;        // Time.time when this kart set a saved lap record
    public float EstimatedFinish { get; set; }
    public float CurrentLapTime => director == null ? 0 : director.RoundTime - lapStartTime;
    const float SlipstreamTime = 1.1f;
    public float LastRecoveryTime { get; private set; } = -100;
    public float Steering => smoothedSteer;
    public bool Drifting => wasDrifting;
    public bool WrongWay => director != null && Speed > 3 && Vector3.Dot(transform.forward, (director.GetWaypointPosition(NextCheckpoint) - director.GetWaypointPosition(CurrentCheckpoint)).normalized) < -.3f;
    static readonly string[] ItemNames = { "SIN OBJETO", "TURBO", "PULSO", "TRAMPA", "ESCUDO", "TRIPLE TURBO", "TORMENTA" };
    public const int ItemCount = 6;
    public bool IsHuman { get => isHuman; set => isHuman = value; }
    public string DisplayName { get => displayName; set => displayName = value; }
    public Color KartColor { get => kartColor; set => kartColor = value; }
    public int CurrentLap { get; set; } = 1;
    public int CurrentCheckpoint { get; set; }
    public int NextCheckpoint => director == null || director.Waypoints.Count == 0 ? 0 : (CurrentCheckpoint + 1) % director.Waypoints.Count;
    public int HeldItem { get; private set; }
    public bool HasFinished { get; private set; }
    public float FinishTime { get; private set; }
    public float Speed => Puppet ? netSpeed : body == null ? 0 : Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up).magnitude;
    /// <summary>Online (2026-11-12): driven on another computer and moved from the network.</summary>
    public bool Puppet { get; private set; }
    public NetPose NetPose { get; } = new NetPose();
    float netSpeed;
    public void SetPuppet() { Puppet = true; isHuman = false; }
    public void MarkStorm() => LastStormTime = Time.time;
    // Drift charge tiers (blue, orange, pink sparks) like classic kart racers.
    public const float Tier1 = .8f, Tier2 = 1.5f, Tier3 = 2.3f;
    public float NitroAmount => Mathf.Clamp01(driftCharge / Tier3) * 100;
    public int DriftTier => !wasDrifting ? 0 : driftCharge >= Tier3 ? 3 : driftCharge >= Tier2 ? 2 : driftCharge >= Tier1 ? 1 : 0;
    public bool Grounded { get; private set; } = true;
    public float LastHitTime { get; private set; } = -100;
    public float LastBoostStart { get; private set; } = -100;
    public int LastBoostTier { get; private set; }
    public float RouletteUntil { get; private set; }
    public bool RouletteSpinning => Time.time < RouletteUntil;
    public int ItemCharges { get; private set; }
    public float ShrunkUntil { get; private set; }
    public bool Shrunk => Time.time < ShrunkUntil;
    public float LastStormTime { get; private set; } = -100;   // this kart summoned a storm
    public float LastStruckTime { get; private set; } = -100;  // this kart was hit by lightning
    public bool Revving => isHuman && throttlePressedAt >= 0 && director != null && director.Countdown > 0;
    public bool Boosting => Time.time < boostUntil;
    public bool Shielded => Time.time < shieldUntil;
    public bool Stunned => Time.time < stunUntil;
    public string ItemName => ItemNames[HeldItem] + (HeldItem == 5 && ItemCharges > 1 ? " x" + ItemCharges : "");
    void Start()
    {
        body = GetComponent<Rigidbody>();
        body.mass = 180; body.linearDamping = .12f;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.solverIterations = 12;
        if (Puppet) { body.isKinematic = true; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; }
        director = FindAnyObjectByType<CrazyKartDirector>();
        if (director != null) director.Register(this);
        if (GetComponent<CrazyKartRecovery>() == null) gameObject.AddComponent<CrazyKartRecovery>();
        if (GetComponent<CrazyKartFeedback>() == null) gameObject.AddComponent<CrazyKartFeedback>();
    }
    void Update()
    {
        if (!isHuman || director == null || !director.Playing || director.Paused) return;
        var k = Keyboard.current; var g = Gamepad.current;
        if ((k != null && (k.spaceKey.wasPressedThisFrame || k.fKey.wasPressedThisFrame)) || (g != null && (g.buttonWest.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame))) UseItem();
        if ((k != null && k.rKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame)) Recover();
    }
    void FixedUpdate()
    {
        if (Puppet) { if (NetPose.Get(out var np, out var nr)) { body.MovePosition(np); body.MoveRotation(nr); } return; }
        if (HasFinished) { body.linearVelocity = director != null && !director.Finished && Time.time < finishCoastUntil ? transform.forward * 5 : Vector3.zero; body.angularVelocity = Vector3.zero; return; }
        if (director == null || !director.Playing)
        {
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            if (isHuman && director != null && !director.Finished && director.Countdown > 0)
            {
                ReadHumanInput(out float t, out _, out _);
                if (t > .5f) { if (throttlePressedAt < 0) throttlePressedAt = director.Countdown; if (director.Countdown > 2.2f) pressedTooEarly = true; }
                else throttlePressedAt = -1;
            }
            return;
        }
        float throttle = 0, steer = 0;
        bool drift = false;
        if (isHuman) ReadHumanInput(out throttle, out steer, out drift);
        if (!startResolved) ResolveStart(throttle);
        if (!isHuman)
        {
            Vector3 target = director.GetWaypointPosition(NextCheckpoint);
            Vector3 tangent = (director.GetWaypointPosition(NextCheckpoint + 1) - target).normalized;
            int lane = 0, slot = 0; for (int i=0;i<director.Karts.Count;i++) if(director.Karts[i]==this) { lane=i%3-1; slot=i; }
            if (botSkill < 0) botSkill = new[] { .965f, .985f, 1f, .975f }[slot % 4] + (MultiverseSettings.BotLevel == 0 ? -.06f : MultiverseSettings.BotLevel == 2 ? .02f : 0f);
            Vector3 sideDir = Vector3.Cross(Vector3.up, tangent); sideDir.y = 0; sideDir.Normalize();
            target += sideDir * lane * 1.5f;
            // Obstacle on the line to the target (moving ones judged where they will be): aim beside it.
            {
                Vector3 line = target - body.position; line.y = 0; float lineLen = line.magnitude; Vector3 dirLine = line / Mathf.Max(.01f, lineLen);
                CrazyKartObstacle threat = null; float bestAhead = float.MaxValue; Vector3 threatPos = Vector3.zero;
                foreach (var o in CrazyKartObstacle.All)
                {
                    if (o == null) continue;
                    Vector3 rel = o.Position - body.position; if (Mathf.Abs(rel.y) > 3) continue; rel.y = 0;
                    float ahead = Vector3.Dot(rel, dirLine); if (ahead < -1 || ahead > Mathf.Min(28, lineLen + 4)) continue;
                    float eta = Mathf.Clamp(ahead / Mathf.Max(8, Speed), 0, 1.2f);
                    Vector3 future = o.Moving ? o.Position + o.axis.normalized * Mathf.Cos(Time.time * o.speed + o.phase) * o.amplitude * o.speed * eta : o.Position;
                    Vector3 fr = future - body.position; fr.y = 0;
                    float perp = Vector3.Dot(fr, Vector3.Cross(Vector3.up, dirLine));
                    if (Mathf.Abs(perp) < o.radius + 1.7f && ahead < bestAhead) { bestAhead = ahead; threat = o; threatPos = future; }
                }
                if (threat != null)
                {
                    Vector3 wpLine = director.GetWaypointPosition(NextCheckpoint);
                    float obsLat = Vector3.Dot(threatPos - wpLine, sideDir), myLat = Vector3.Dot(body.position - wpLine, sideDir);
                    float clear = threat.radius + 2.3f, dir = myLat >= obsLat ? 1 : -1;
                    if (Mathf.Abs(obsLat + dir * clear) > 4.6f) dir = -dir;
                    float lat = Mathf.Clamp(obsLat + dir * clear, -4.6f, 4.6f);
                    Vector3 beside = wpLine + sideDir * lat + Vector3.Project(threatPos - wpLine, tangent) + tangent * 3;
                    if (bestAhead > 2) target = beside;
                }
            }
            Vector3 to = target - body.position; to.y = 0;
            float angle = Vector3.SignedAngle(transform.forward, to, Vector3.up);
            steer = Mathf.Clamp(angle / 35, -1, 1);
            throttle = Mathf.Abs(angle) > 65 && Speed > 10 ? -.35f : 1;
            {   // ease off before tight corners so faster karts keep the line
                Vector3 inc = director.GetWaypointPosition(NextCheckpoint) - director.GetWaypointPosition(CurrentCheckpoint); inc.y = 0;
                Vector3 outg = tangent; outg.y = 0;
                float bend = Vector3.Angle(inc, outg);
                float safe = Mathf.Lerp(maxSpeed, maxSpeed * .62f, Mathf.InverseLerp(25, 80, bend));
                if (to.magnitude < 22 && Speed > safe && !Boosting) throttle = Speed > safe + 3 ? -.4f : 0;
            }
            // Drift through sharp corners for a mini turbo, like a player would.
            Vector3 incoming = director.GetWaypointPosition(NextCheckpoint) - director.GetWaypointPosition(CurrentCheckpoint); incoming.y = 0;
            float corner = Vector3.Angle(incoming, new Vector3(tangent.x, 0, tangent.z));
            if (botSkill >= .975f && corner > 28 && to.magnitude < 14 && Speed > 13 && Time.time > botDriftUntil + 1.5f) botDriftUntil = Time.time + .95f;
            drift = Time.time < botDriftUntil;
            if (HeldItem != 0 && Time.time - lastUse > 2 && BotWantsItem(Mathf.Abs(angle))) UseItem();
            stuckTime = Speed < 2 ? stuckTime + Time.fixedDeltaTime : 0;
            // Wedged against something: back off with the wheel turned the other way, then retry.
            if (stuckTime > 1.2f && Time.time > botReverseUntil + 1.5f) botReverseUntil = Time.time + .9f;
            if (Time.time < botReverseUntil) { throttle = -1; steer = -Mathf.Sign(angle == 0 ? 1 : angle); drift = false; }
            if (stuckTime > 6) Recover();
        }
        if (Time.time < stallUntil) throttle = Mathf.Min(throttle, 0);
        if (Stunned) { throttle = 0; steer = 0; drift = false; }
        smoothedSteer = Mathf.MoveTowards(smoothedSteer, steer, steeringResponse * Time.fixedDeltaTime);
        steer = smoothedSteer;
        bool grounded = Physics.Raycast(body.position + Vector3.up * .15f, Vector3.down, out RaycastHit groundHit, .9f, ~0, QueryTriggerInteraction.Ignore);
        Grounded = grounded;
        if (grounded)
        {
            float offroad = !Boosting && director.DistanceToRoad(body.position) > director.roadHalfWidth ? .48f : 1;   // a turbo cuts through rough ground
            float limit = (Boosting ? maxSpeed * 1.4f : maxSpeed) * offroad * (isHuman ? 1 : BotPace()) * (Shrunk ? .78f : 1);
            var planar = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            float forwardSpeed = Vector3.Dot(planar, transform.forward);
            bool drifting = drift && Mathf.Abs(steer) > .1f && forwardSpeed > 7 && !Stunned;
            if (drifting) driftCharge = Mathf.Min(Tier3 + .2f, driftCharge + Time.fixedDeltaTime);
            if (wasDrifting && !drifting) { if (!drift && !Stunned && driftCharge >= Tier1) { int tier = driftCharge >= Tier3 ? 3 : driftCharge >= Tier2 ? 2 : 1; ApplyBoost(tier == 3 ? 1.8f : tier == 2 ? 1.2f : .75f, tier); } driftCharge = 0; }
            wasDrifting = drifting;
            UpdateSlipstream(forwardSpeed);
            float yaw = steer * turnSpeed * (drifting ? 1.2f : 1) * Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 5) * (forwardSpeed < -.5f ? -1 : 1);
            body.MoveRotation(body.rotation * Quaternion.Euler(0, yaw * Time.fixedDeltaTime, 0));
            planar += transform.forward * throttle * acceleration * Time.fixedDeltaTime;
            planar -= transform.right * Vector3.Dot(planar, transform.right) * Mathf.Clamp01((drifting ? 2.3f : 9) * Time.fixedDeltaTime);
            if (throttle == 0) planar *= 1 - (Stunned ? 3 : .7f) * Time.fixedDeltaTime;
            if (Vector3.Dot(planar, transform.forward) < -7) planar = Vector3.ClampMagnitude(planar, 7);
            planar = Vector3.ClampMagnitude(planar, limit);
            float slopeVelocity = -(planar.x*groundHit.normal.x+planar.z*groundHit.normal.z)/Mathf.Max(.35f,groundHit.normal.y);
            body.linearVelocity = new Vector3(planar.x, Mathf.Clamp(slopeVelocity,-6,6)+Mathf.Min(0,body.linearVelocity.y)*.2f, planar.z);
        }
        else
        {   // Arcade air: a little extra gravity and the kart straightens its sideways drift.
            var v = body.linearVelocity;
            Vector3 side = transform.right * Vector3.Dot(v, transform.right);
            v -= side * Mathf.Clamp01(1.4f * Time.fixedDeltaTime);
            v += Vector3.down * 6f * Time.fixedDeltaTime;
            body.linearVelocity = v;
        }
        body.angularVelocity = Vector3.zero;
        if (body.position.y < -4 || body.position.y > 60) Recover();
    }
    public void Recover()
    {
        if (Puppet || director == null || body == null || HasFinished) return;
        var p = director.GetWaypointPosition(CurrentCheckpoint);
        var f = director.GetWaypointPosition(NextCheckpoint) - p; f.y = 0;
        var rotation=Quaternion.LookRotation(f);var side=Vector3.Cross(Vector3.up,f.normalized);
        Vector3 chosen=p+Vector3.up*.85f;
        foreach(float back in new[]{0f,3.5f,7f}){
            bool found=false;
            foreach(float lane in new[]{0f,-1.8f,1.8f}){
                Vector3 candidate=p-f.normalized*back+side*lane;
                foreach(var ground in Physics.RaycastAll(candidate+Vector3.up*3,Vector3.down,7,~0,QueryTriggerInteraction.Ignore))
                    if(ground.collider.GetComponent<CrazyKartRoadSurface>()!=null){candidate.y=ground.point.y;break;}
                candidate+=Vector3.up*.85f;bool occupied=false;
                foreach(var obstacle in Physics.OverlapBox(candidate,new Vector3(.85f,.4f,1.45f),rotation,~0,QueryTriggerInteraction.Ignore))
                    if(obstacle.attachedRigidbody!=body){occupied=true;break;}
                if(!occupied){chosen=candidate;found=true;break;}
            }
            if(found)break;
        }
        body.position = chosen; body.rotation = rotation; LastRecoveryTime=Time.time;
        body.useGravity=true;body.detectCollisions=true;
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        driftCharge = stuckTime = smoothedSteer = 0; wasDrifting = false; boostUntil = 0; stunUntil = Time.time + .5f;
    }
    public void ApplyBoost(float duration) => ApplyBoost(duration, 0);
    public void ApplyBoost(float duration, int tier)
    {
        if (!Boosting) { LastBoostStart = Time.time; LastBoostTier = tier; }
        else if (tier > LastBoostTier) LastBoostTier = tier;
        boostUntil = Mathf.Max(boostUntil, Time.time + duration);
    }
    public bool GiveItem(int item) => GiveItem(item, false);
    // roulette: the HUD spins through icons for a moment before the item can be used (item boxes).
    public bool GiveItem(int item, bool roulette)
    {
        if (Puppet || HeldItem != 0 || item < 1 || item > ItemCount || HasFinished) return false;
        HeldItem = item; ItemCharges = item == 5 ? 3 : 1; itemHeldSince = Time.time;
        RouletteUntil = roulette ? Time.time + 1.1f : 0;
        return true;
    }
    public void TriggerPowerUp() => GiveItem(Random.Range(1, 5));
    public void UseItem()
    {
        if (director == null || !director.Playing || HasFinished || Stunned || RouletteSpinning || HeldItem == 0) return;
        int item = HeldItem; lastUse = Time.time;
        if (--ItemCharges <= 0) { HeldItem = 0; ItemCharges = 0; }
        if (item == 1) ApplyBoost(2.5f);
        if (item == 5) ApplyBoost(1.5f);
        if (item == 6) { Storm(); director.Net?.SendStorm(this); }
        if (item == 2 || item == 3) { var hazard = CrazyKartHazard.Spawn(this, item == 3); if (director.Net != null && hazard != null) director.Net.SendHazard(this, item == 3, hazard.transform.position, transform.forward); }
        if (item == 4) shieldUntil = Time.time + 6;
    }
    public void Hit()
    {
        if (Puppet) return;   // the owner's computer decides its own hits
        if (Shielded) { shieldUntil = 0; return; }
        if (Stunned || HasFinished || Time.time < hitProtectionUntil) return;
        LastHitTime = Time.time; stunUntil = Time.time + 1.1f; hitProtectionUntil = stunUntil + .6f; boostUntil = 0; driftCharge = 0; wasDrifting = false;
        if (body != null) body.linearVelocity *= .25f;
        CrazyRaceBurst.Spawn(transform.position+Vector3.up*.6f,new Color(1,.25f,.15f),false);
    }
    // Lightning strikes every rival ahead: a spin and a few seconds shrunk (shields block it).
    void Storm()
    {
        LastStormTime = Time.time;
        float mine = director.Progress(this);
        foreach (var k in director.Karts)
            if (k != this && !k.HasFinished && director.Progress(k) > mine) k.StruckByLightning();
    }
    public void StruckByLightning()
    {
        if (Puppet) return;
        LastStruckTime = Time.time;
        if (Shielded) { shieldUntil = 0; return; }
        Hit(); ShrunkUntil = Time.time + 4.5f;
    }
    public bool PassCheckpoint(int index, Vector3? gateForward = null)
    {
        if (Puppet || director == null || !director.Playing || HasFinished || index != NextCheckpoint) return false;
        Vector3 forward = gateForward ?? (director.GetWaypointPosition((index + 1) % director.Waypoints.Count) - director.GetWaypointPosition(index));
        if (Vector3.Dot(body.linearVelocity, forward.normalized) <= .1f) return false;
        CurrentCheckpoint = index;
        if (index == 0)
        {
            CurrentLap++;
            LastLapTime = director.RoundTime - lapStartTime; lapStartTime = director.RoundTime;
            if (BestLapTime <= 0 || LastLapTime < BestLapTime) BestLapTime = LastLapTime;
            director.ReportLap(this, LastLapTime);
        }
        if (CurrentLap > director.laps) { HasFinished = true; FinishTime = director.RoundTime; finishCoastUntil=Time.time+1.5f; body.detectCollisions=false; body.useGravity=false; director.Finish(this); }
        return true;
    }
    void ReadHumanInput(out float throttle, out float steer, out bool drift)
    {
        throttle = steer = 0; drift = false;
        var k = Keyboard.current;
        if (k != null)
        {
            throttle = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            steer = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            drift = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
        }
        var g = Gamepad.current;
        if (g != null)
        {
            float gt = Mathf.Max(g.rightTrigger.ReadValue(), g.buttonSouth.isPressed ? 1 : 0) - g.leftTrigger.ReadValue();
            if (Mathf.Abs(gt) > Mathf.Abs(throttle)) throttle = gt;
            float gs = g.leftStick.x.ReadValue(); if (Mathf.Abs(gs) < .15f) gs = 0;
            if (Mathf.Abs(gs) > Mathf.Abs(steer)) steer = gs;
            drift |= g.rightShoulder.isPressed || g.leftShoulder.isPressed;
        }
        throttle = Mathf.Clamp(throttle, -1, 1); steer = Mathf.Clamp(steer, -1, 1);
    }
    // ---------------------------------------------------------------- online state (2026-11-12)
    struct NetState
    {
        public Vector3 pos; public Quaternion rot; public float speed, finishTime, boost, stun, shield, shrink, steer, drift, sinceHit, sinceStruck;
        public int lap, checkpoint, tier; public bool finished; public byte flags;
    }
    static NetState Parse(System.IO.BinaryReader r) => new NetState
    {
        pos = r.ReadVector3(), rot = r.ReadQuaternion(), speed = r.ReadSingle(), lap = r.ReadByte(), checkpoint = r.ReadInt16(),
        finished = r.ReadBoolean(), finishTime = r.ReadSingle(), flags = r.ReadByte(), boost = r.ReadSingle(), stun = r.ReadSingle(),
        shield = r.ReadSingle(), shrink = r.ReadSingle(), steer = r.ReadSingle(), drift = r.ReadSingle(), tier = r.ReadByte(),
        sinceHit = r.ReadSingle(), sinceStruck = r.ReadSingle()
    };
    public static void SkipNet(System.IO.BinaryReader r) => Parse(r);
    public void WriteNet(System.IO.BinaryWriter w)
    {
        w.Write(body.position); w.Write(body.rotation); w.Write(Speed);
        w.Write((byte)Mathf.Clamp(CurrentLap, 0, 255)); w.Write((short)CurrentCheckpoint); w.Write(HasFinished); w.Write(FinishTime);
        w.Write((byte)((wasDrifting ? 1 : 0) | (Grounded ? 2 : 0)));
        w.Write(Mathf.Max(0, boostUntil - Time.time)); w.Write(Mathf.Max(0, stunUntil - Time.time)); w.Write(Mathf.Max(0, shieldUntil - Time.time)); w.Write(Mathf.Max(0, ShrunkUntil - Time.time));
        w.Write(smoothedSteer); w.Write(driftCharge); w.Write((byte)LastBoostTier);
        w.Write(Mathf.Min(99, Time.time - LastHitTime)); w.Write(Mathf.Min(99, Time.time - LastStruckTime));
    }
    public void ReadNet(System.IO.BinaryReader r)
    {
        var s = Parse(r);
        NetPose.Add(s.pos, s.rot); netSpeed = s.speed;
        CurrentLap = s.lap; CurrentCheckpoint = s.checkpoint;
        if (s.finished && !HasFinished) { HasFinished = true; FinishTime = s.finishTime; if (body != null) { body.detectCollisions = false; } }
        wasDrifting = (s.flags & 1) != 0; Grounded = (s.flags & 2) != 0;
        if (s.boost > 0 && !Boosting) { LastBoostStart = Time.time; LastBoostTier = s.tier; }
        boostUntil = s.boost > 0 ? Time.time + s.boost : 0; stunUntil = s.stun > 0 ? Time.time + s.stun : 0;
        shieldUntil = s.shield > 0 ? Time.time + s.shield : 0; ShrunkUntil = s.shrink > 0 ? Time.time + s.shrink : 0;
        smoothedSteer = s.steer; driftCharge = s.drift;
        if (s.sinceHit < .3f && Time.time - LastHitTime > 1f) { LastHitTime = Time.time - s.sinceHit; CrazyRaceBurst.Spawn(transform.position + Vector3.up * .6f, new Color(1, .25f, .15f), false); }
        if (s.sinceStruck < .3f && Time.time - LastStruckTime > 1f) LastStruckTime = Time.time - s.sinceStruck;
    }
    // Only a countdown that really ran down grants the start boost (tests that skip it are unaffected).
    void ResolveStart(float throttle)
    {
        startResolved = true; lapStartTime = director.RoundTime;
        if (!director.NaturalStart) return;
        if (isHuman)
        {
            if (pressedTooEarly) { StartResult = -1; stallUntil = Time.time + .9f; }
            else if (throttlePressedAt > 0 && throttlePressedAt <= 1.5f && throttle > .5f) { StartResult = 1; ApplyBoost(1.4f); }
        }
        else if (Random.value < .4f) { StartResult = 1; ApplyBoost(1.2f); }
        if (StartResult != 0) StartResultTime = Time.time;
    }
    // Drafting: stay close behind a rival for a moment to earn a short boost.
    void UpdateSlipstream(float forwardSpeed)
    {
        bool drafting = false;
        if (forwardSpeed > 12 && !Boosting && !Stunned)
            foreach (var other in director.Karts)
            {
                if (other == this || other.HasFinished) continue;
                Vector3 d = other.transform.position - transform.position; d.y = 0;
                float dist = d.magnitude;
                if (dist < 1.5f || dist > 11) continue;
                if (Vector3.Dot(transform.forward, d / dist) > .96f && Vector3.Dot(transform.forward, other.transform.forward) > .7f) { drafting = true; break; }
            }
        slipCharge = drafting ? slipCharge + Time.fixedDeltaTime : Mathf.Max(0, slipCharge - Time.fixedDeltaTime * 2);
        if (slipCharge >= SlipstreamTime) { slipCharge = 0; ApplyBoost(.9f); LastSlipstreamBoost = Time.time; }
    }
    // Each bot has its own pace, nudged towards the human so races stay close.
    float BotPace()
    {
        float pace = botSkill < 0 ? 1 : botSkill;
        foreach (var k in director.Karts)
            if (k.IsHuman && !k.HasFinished)
            {
                float gap = director.Progress(k) - director.Progress(this);   // in checkpoints, ~18 m each
                pace *= 1 + Mathf.Clamp(gap / 4f, -1, 1) * .07f;
                break;
            }
        return pace;
    }
    bool BotWantsItem(float absAngle)
    {
        float held = Time.time - itemHeldSince;
        switch (HeldItem)
        {
            case 1: case 5: return absAngle < 12 && Speed > 10 && !Boosting;
            case 6: return director.Position(this) > 1;
            case 2: return CrazyKartHazard.FindTarget(this) != null || held > 9;
            case 3: return RivalBehind(16) || held > 9;
            case 4: return RivalBehind(10) || held > 7;
        }
        return false;
    }
    bool RivalBehind(float range)
    {
        foreach (var other in director.Karts)
        {
            if (other == this || other.HasFinished) continue;
            Vector3 d = other.transform.position - transform.position;
            if (d.magnitude < range && Vector3.Dot(transform.forward, d.normalized) < -.5f) return true;
        }
        return false;
    }
    void OnTriggerEnter(Collider other)
    {
        if (Puppet) return;
        var cp = other.GetComponent<CrazyKartCheckpointIndex>(); if (cp != null) PassCheckpoint(cp.index,other.transform.forward);
        var box = other.GetComponent<CrazyKartItemBox>(); if (box != null) box.Collect(this);
        if (other.GetComponent<CrazyKartBoostPad>() != null) { if (!Boosting) CrazyRaceFX.Shards(transform.position, 14, false, new Color(1, .7f, .15f), 6); ApplyBoost(1.1f); }
    }
}





