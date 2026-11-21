using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PirateFighter : MonoBehaviour
{
    // Evento de diagnóstico para pruebas automatizadas; null en juego normal.
    public static System.Action<string, string> TraceHook;
    private void Trace(string message) { if (TraceHook != null) TraceHook(message, name); }

    private PirateArenaDirector director;
    internal PirateArenaDirector Director => director;
    private PirateArenaSettings tuning;
    private Rigidbody body;
    private CapsuleCollider capsule;
    private SphereCollider leftHandSensor, rightHandSensor;
    private ConfigurableJoint grabJoint;
    private Rigidbody heldBody;
    private PirateFighter heldFighter;
    private GrabbableProp heldProp;
    private PirateFighter holder;
    // A second hand has its own joint and target. Primary is promoted when released.
    private ConfigurableJoint otherJoint;
    private Rigidbody otherBody;
    private PirateFighter otherFighter;
    private GrabbableProp otherProp;
    private bool otherLedge, primaryRight;
    private float otherStarted;
    private Vector3 spawn, move, facing = Vector3.forward;
    private float stunnedUntil, pushReady, jumpBufferedUntil, lastGrounded = -100f, grabStarted, botThink, botThrowAt;
    private float jumpReady, escapeReady;
    private float hitStrain, lastCombatHit = -100f, knockoutUntil, recoveryProtectedUntil;
    private float gripStamina = 1f;
    public float GripStamina => gripStamina;
    public bool IsKnockedOut => Time.time < knockoutUntil;
    public float KnockoutRemaining => Mathf.Max(0f, knockoutUntil - Time.time);
    public bool IsRecovering => !IsStunned && Time.time < recoveryProtectedUntil;
    public float HitStrain => tuning == null ? 0f : Mathf.Clamp01(hitStrain / Mathf.Max(1f, tuning.knockoutThreshold));
    public bool GripNeedsRelease => blockGrabUntilRelease;
    public int EscapeProgress => escapeTaps;
    private bool grounded, groundedOnArena, grabPressed, grabReleased, pushPressed, lifting, holdingLedge;
    private Vector3 groundNormal = Vector3.up;
    private int escapeTaps;
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly RaycastHit[] interactionHits = new RaycastHit[24];
    private readonly Collider[] grabHits = new Collider[32];
    private MaterialPropertyBlock tint;
    private Renderer[] visuals;
    private PirateFighterAnimation motion;
    private PirateActivePuppet activePuppet;
    public bool NetworkDriven { get; set; }
    public bool IsProxy { get; private set; }
    private PirateBodyState remote;
    private bool remotePositioned;
    private int remoteActionSerial;
    private bool previousGrab, grabHeld, leftReach, rightReach, blockGrabUntilRelease;
    private float lookPitch = 26f;
    private Vector3 lookDirection = Vector3.forward;
    public Vector3 VisualVelocity => IsProxy ? remote.Velocity : body.linearVelocity;
    public bool IsHuman { get; private set; }
    public bool IsAlive { get; private set; } = true;
    public bool IsHeld => IsProxy ? (remote.Flags & 4) != 0 : holder != null;
    public bool IsGrabbing => IsProxy ? (remote.Flags & 8) != 0 : grabJoint != null;
    public string DisplayName { get; private set; }
    public Rigidbody Body => body;
    public bool IsGrounded => grounded;
    public bool IsStunned => IsProxy ? (remote.Flags & 16) != 0 : Time.time < stunnedUntil;
    public bool IsLifting => IsProxy ? (remote.Flags & 32) != 0 : lifting && heldBody != null;
    public bool LeftReach => IsProxy ? (remote.Flags & 128) != 0 : leftReach;
    public bool RightReach => IsProxy ? (remote.Flags & 256) != 0 : rightReach;
    public float LookPitch => IsProxy ? remote.LookPitch : lookPitch;
    public bool IsHoldingLedge => IsProxy ? (remote.Flags & 64) != 0 : holdingLedge || otherLedge;
    public bool RoundPlaying => director != null && director.IsPlaying;
    public bool HasWon => director != null && director.Winner == this;
    public Vector3 GripWorldPosition => IsProxy ? remote.Grip : heldBody != null ? heldBody.worldCenterOfMass :
        grabJoint != null ? transform.TransformPoint(grabJoint.anchor) : transform.position + transform.forward;
    private static Vector3 JointPoint(ConfigurableJoint joint, Vector3 fallback)
        => joint == null ? fallback : joint.connectedBody != null
            ? joint.connectedBody.transform.TransformPoint(joint.connectedAnchor) : joint.connectedAnchor;
    public bool LeftGripped => IsProxy ? (remote.Flags & 512) != 0 : (primaryRight ? otherJoint : grabJoint) != null;
    public bool RightGripped => IsProxy ? (remote.Flags & 1024) != 0 : (primaryRight ? grabJoint : otherJoint) != null;
    public Vector3 LeftGripPosition => IsProxy ? remote.LeftGrip : JointPoint(primaryRight ? otherJoint : grabJoint, GripWorldPosition);
    public Vector3 RightGripPosition => IsProxy ? remote.RightGrip : JointPoint(primaryRight ? grabJoint : otherJoint, GripWorldPosition);

    public void Initialize(PirateArenaDirector arena, bool human, GameObject prefab, Vector3 position, int index)
    {
        tint = new MaterialPropertyBlock();
        director = arena; tuning = arena.Settings; IsHuman = human; spawn = position;
        DisplayName = human ? "Tú" : "Pirata " + index;
        body = GetComponent<Rigidbody>(); capsule = GetComponent<CapsuleCollider>();
        leftHandSensor = CreateHandSensor("Sensor mano izquierda");
        rightHandSensor = CreateHandSensor("Sensor mano derecha");
        // More contact iterations keep six colliding fighters stable during pushes and grabs.
        body.solverIterations = 16;
        body.solverVelocityIterations = 8;
        GameObject animated = prefab != null ? Resources.Load<GameObject>("PirateCharacters/" + prefab.name) : null;
        GameObject visual = PirateArenaVisuals.AddModel(animated != null ? animated : prefab, transform, 1.7f, arena.Art.atlas, true);
        if (visual != null) visual.transform.position += Vector3.down * 0.02f;
        if (visual != null)
        {
            motion = gameObject.AddComponent<PirateFighterAnimation>();
            motion.Initialize(this, visual);
            activePuppet = GetComponent<PirateActivePuppet>();
        }
        visuals = GetComponentsInChildren<Renderer>();
        if (human) CreatePlayerMarker();
    }

    private SphereCollider CreateHandSensor(string label)
    {
        var sensor = new GameObject(label).AddComponent<SphereCollider>();
        sensor.transform.SetParent(transform, false);
        sensor.radius = 0.42f;
        sensor.isTrigger = true;
        PirateCameraObstacle.IgnoreFor(sensor);
        return sensor;
    }

    private void CreatePlayerMarker()
    {
        GameObject marker = new GameObject("Indicador del jugador", typeof(LineRenderer));
        marker.transform.SetParent(transform, false);
        LineRenderer line = marker.GetComponent<LineRenderer>();
        line.sharedMaterial = director.Art.foam;
        line.useWorldSpace = false;
        line.widthMultiplier = 0.055f;
        line.positionCount = 33;
        for (int i = 0; i <= 32; i++)
        {
            float a = i * Mathf.PI / 16f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.52f, -0.82f, Mathf.Sin(a) * 0.52f));
        }
    }

    private void Update()
    {
        if (NetworkDriven) return;
        if (!IsAlive)
        {
            if (!body.isKinematic && transform.position.y < -12f) body.isKinematic = true;
            return;
        }
        if (!director.IsPlaying) return;
        if (IsHuman) ReadInput();
        else ReadBot();
    }

    private void ReadInput()
    {
        if (PirateSession.Instance != null && PirateSession.Instance.MenuOpen) { move = Vector3.zero; grabHeld = leftReach = rightReach = false; ReleaseGrab(false); return; }
        Keyboard keys = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keys == null) return;
        if (IsStunned) { move = Vector3.zero; return; }
        if (IsHeld)
        {
            if (keys.spaceKey.wasPressedThisFrame && Time.time >= escapeReady)
            {
                escapeReady = Time.time + 0.18f;
                if (++escapeTaps >= 3) holder.ReleaseGrab(false);
            }
            return;
        }
        float x = (keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0);
        float z = (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0);
        Transform camera = director.ArenaCamera.transform;
        move = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized * z +
            Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized * x, 1f);
        if (keys.spaceKey.wasPressedThisFrame) jumpBufferedUntil = Time.time + 0.13f;
        lifting = keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed;
        var orbit = director.ArenaCamera.GetComponent<PirateArenaCamera>();
        lookPitch = orbit.Pitch;
        lookDirection = Quaternion.Euler(0f, orbit.Yaw, 0f) * Vector3.forward;
        bool wasHeld = grabHeld;
        leftReach = mouse != null && mouse.leftButton.isPressed;
        rightReach = mouse != null && mouse.rightButton.isPressed;
        grabHeld = leftReach || rightReach;
        grabPressed |= grabHeld && !wasHeld;
        grabReleased |= !grabHeld && wasHeld;
        pushPressed |= keys.eKey.wasPressedThisFrame;
        if (!grabHeld) blockGrabUntilRelease = false;
        if (keys.fKey.wasPressedThisFrame) { blockGrabUntilRelease = true; grabPressed = false; ReleaseGrab(true); grabReleased = false; }
    }

    private void ReadBot()
    {
        if (IsStunned) { move = Vector3.zero; return; }
        if (!IsGrabbing && gripStamina >= .4f) blockGrabUntilRelease = false;
        if (IsHeld)
        {
            if (Time.time >= escapeReady)
            {
                escapeReady = Time.time + tuning.botEscapeInterval;
                if (++escapeTaps >= 3) holder.ReleaseGrab(false);
            }
            return;
        }
        if (IsGrabbing)
        {
            if (holdingLedge || otherLedge) { lifting = true; move = Vector3.zero; return; }
            lifting = true;
            Vector3 exit = director.Art.Map.CarryExit(body.position, out Vector3 outward);
            Vector3 carryRoute = director.Art.Map.NavigationTarget(body.position, exit);
            Vector3 carryDelta = Vector3.ProjectOnPlane(exit - body.position, Vector3.up);
            bool atExit = carryDelta.magnitude < .65f;
            move = atExit ? Vector3.zero : Vector3.ProjectOnPlane(carryRoute - body.position, Vector3.up).normalized * .7f;
            if (director.DangerDirection(body.position, out Vector3 carryDodge)) move = carryDodge;
            if ((atExit && Time.time - grabStarted > .65f) || Time.time > botThrowAt)
            {
                facing = atExit ? outward : Vector3.ProjectOnPlane(heldBody != null ? heldBody.position - body.position : transform.forward, Vector3.up).normalized;
                ReleaseGrab(true);
                grabHeld = leftReach = rightReach = grabPressed = false;
                lifting = false; move = -outward * .5f;
                botThink = Time.time + .3f;
            }
            return;
        }
        lifting = false;
        grabHeld = leftReach = rightReach = false;
        if (Time.time < botThink) return;
        botThink = Time.time + Random.Range(0.2f, 0.4f) * MultiverseSettings.BotDelay;
        PirateFighter target = director.NearestLivingFighter(transform.position, this);
        if (target == null) { move = Vector3.zero; return; }
        Vector3 delta = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up);
        Vector3 destination = director.Art.Map.NavigationTarget(transform.position, target.transform.position);
        Vector3 route = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up);
        move = delta.magnitude > 1.35f ? route.normalized * 0.78f : Vector3.zero;
        // Escape the marked cannon zone before pursuing another fighter.
        if (director.DangerDirection(transform.position, out Vector3 dodge)) move = dodge;
        else if (director.IsNearEdge(this, 0.65f)) move = Vector3.ProjectOnPlane(-transform.position, Vector3.up).normalized;
        if (delta.magnitude < 1.8f && Time.time >= pushReady)
        {
            facing = delta.normalized;
            if (!IsGrabbing && !blockGrabUntilRelease && gripStamina > .4f && (target.IsStunned || Random.value < 0.3f))
            {
                grabPressed = true; lookDirection = facing;
                botThrowAt = Time.time + tuning.botCarrySeconds;
            }
            else pushPressed = true;
        }
    }

    private void FixedUpdate() { if (!NetworkDriven) Simulate(Time.fixedDeltaTime); }

    private float eliminatedAt = -100f;

    private void FloatWhileEliminated()
    {
        if (body == null || body.isKinematic) return;
        float since = Time.time - eliminatedAt;
        if (since > 3.2f || body.position.y > .25f) return;
        float depth = .25f - body.position.y;
        Vector3 v = body.linearVelocity;
        body.AddForce(Vector3.up * Mathf.Min(depth * 34f, 30f) - new Vector3(v.x * 1.6f, v.y * 3f, v.z * 1.6f), ForceMode.Acceleration);
    }

    public void Simulate(float deltaTime)
    {
        if (!IsAlive) { FloatWhileEliminated(); return; }
        if (!director.IsPlaying) return;
        // Bound unexpected upward and horizontal spikes without slowing natural falls.
        Vector3 v = body.linearVelocity;
        Vector3 flat = Vector3.ClampMagnitude(new Vector3(v.x, 0f, v.z), 16f);
        // Preserve normal jumps and throws, but prevent a joint/contact spike from
        // repeatedly feeding a much larger upward launch into the next physics step.
        float maxUpwardSpeed = Mathf.Max(tuning.jumpSpeed, tuning.throwUpSpeed) + .75f;
        body.linearVelocity = new Vector3(flat.x, Mathf.Min(v.y, maxUpwardSpeed), flat.z);
        ProbeGround();
        UpdateCondition(deltaTime);
        if (IsHeld) { move = Vector3.zero; return; }
        if (grabReleased && !leftReach && !rightReach) { ReleaseGrab(false); grabReleased = false; }
        // Release each hand independently. Bots use the existing timed single grip.
        if (grabJoint != null && (leftReach || rightReach) && !(primaryRight ? rightReach : leftReach))
        {
            ReleaseCurrentGrab(false);
            SwapHands();
        }
        if (otherJoint != null && !(primaryRight ? leftReach : rightReach))
        {
            SwapHands(); ReleaseCurrentGrab(false); SwapHands();
        }
        if (Time.time >= stunnedUntil)
        {
            // A reaching hand can catch something entered after the button was pressed.
            if (!blockGrabUntilRelease && grabJoint == null && (grabPressed || grabHeld))
            {
                primaryRight = rightReach && !leftReach;
                TryGrab();
            }
            if (!blockGrabUntilRelease && grabJoint != null && otherJoint == null &&
                (primaryRight ? leftReach : rightReach))
            {
                SwapHands(); TryGrab(); SwapHands();
            }
            if (pushPressed) PushNearby();
            Drive();
        }
        grabPressed = pushPressed = false;
        if (grabJoint != null)
        {
            if ((!grabHeld && Time.time - grabStarted > tuning.maxCarrySeconds) || (!holdingLedge && heldBody == null))
                ReleaseGrab(false);
            else if (!holdingLedge)
                grabJoint.anchor = Vector3.MoveTowards(grabJoint.anchor,
                    CarryHandAnchor(), 2.4f * deltaTime);
        }
        if (otherJoint != null)
        {
            SwapHands();
            if (!holdingLedge && heldBody == null) ReleaseCurrentGrab(false);
            else if (!holdingLedge && grabJoint != null)
                grabJoint.anchor = Vector3.MoveTowards(grabJoint.anchor,
                    CarryHandAnchor(), 2.4f * deltaTime);
            SwapHands();
        }
        if (groundedOnArena && lifting && (holdingLedge || otherLedge))
        {
            // Once the feet reach a walkable surface, release only the ledge hands.
            ReleaseLedgeHands();
            blockGrabUntilRelease = true;
        }
        if ((holdingLedge || otherLedge) && Time.time >= stunnedUntil)
        {
            // Steering gives a hanging player momentum without bypassing the joints.
            body.AddForce(Vector3.ProjectOnPlane(move, Vector3.up) * 8f, ForceMode.Acceleration);
            if (lifting)
            {
                // The moving hand anchor alone cannot reliably raise a heavy body against gravity.
                // Apply a bounded pull while the joint constrains the hand to the surface.
                float pull = Mathf.Clamp(20f - body.linearVelocity.y * 4f, 0f, 28f);
                body.AddForce(Vector3.up * pull, ForceMode.Acceleration);
                if (holdingLedge) PullLedge(grabJoint, deltaTime);
                if (otherLedge) PullLedge(otherJoint, deltaTime);
            }
        }
        if (jumpBufferedUntil > Time.time && Time.time >= jumpReady && Time.time >= stunnedUntil)
        {
            if (holdingLedge || otherLedge)
            {
                ReleaseGrab(false);
                body.AddForce(facing * 2.3f + Vector3.up * tuning.jumpSpeed, ForceMode.VelocityChange);
                jumpReady = Time.time + 0.3f; jumpBufferedUntil = 0f;
                motion?.Trigger(PirateFighterAnimation.Action.Jump);
            }
            else if (Time.time - lastGrounded < 0.1f)
            {
                body.AddForce(Vector3.up * Mathf.Max(0f, tuning.jumpSpeed - body.linearVelocity.y), ForceMode.VelocityChange);
                jumpReady = Time.time + 0.3f; jumpBufferedUntil = 0f;
                motion?.Trigger(PirateFighterAnimation.Action.Jump);
            }
        }
    }

    private void ReleaseLedgeHands()
    {
        if (holdingLedge) { ReleaseCurrentGrab(false); SwapHands(); }
        if (otherLedge) { SwapHands(); ReleaseCurrentGrab(false); SwapHands(); }
    }

    private Vector3 CarryHandAnchor()
    {
        Vector3 anchor = lifting ? tuning.liftAnchor : tuning.carryAnchor;
        // Separate the hand anchors instead of pulling two contact points to one spot.
        if (otherJoint != null) anchor.x += primaryRight ? .2f : -.2f;
        return anchor;
    }

    private void UpdateCondition(float deltaTime)
    {
        if (Time.time - lastCombatHit >= tuning.hitRecoveryDelay)
            hitStrain = Mathf.MoveTowards(hitStrain, 0f, tuning.hitRecoveryPerSecond * deltaTime);
        bool gripping = grabJoint != null || otherJoint != null;
        if (!gripping)
        {
            gripStamina = Mathf.MoveTowards(gripStamina, 1f, tuning.gripRecoveryPerSecond * deltaTime);
            return;
        }
        // Conscious opponents resist. Two hands distribute the effort; lifting costs more.
        float effort = lifting ? 1.5f : 1f;
        if ((heldFighter != null && !heldFighter.IsStunned) || (otherFighter != null && !otherFighter.IsStunned)) effort *= 1.5f;
        if (grabJoint != null && otherJoint != null) effort *= .7f;
        gripStamina = Mathf.Max(0f, gripStamina - effort * deltaTime / Mathf.Max(.1f, tuning.gripEnduranceSeconds));
        if (gripStamina > 0f) return;
        ReleaseGrab(false);
        blockGrabUntilRelease = true;
        if (IsHuman) director.ShowEvent("Sin fuerza: suelta las manos para recuperar el agarre");
    }

    private static void PullLedge(ConfigurableJoint joint, float deltaTime)
    {
        if (joint == null) return;
        Vector3 anchor = joint.anchor;
        anchor.y = Mathf.MoveTowards(anchor.y, -1.35f, deltaTime * 0.8f);
        joint.anchor = anchor;
    }

    private void ProbeGround()
    {
        grounded = groundedOnArena = false; groundNormal = Vector3.up;
        int count = Physics.SphereCastNonAlloc(body.position + Vector3.up * 0.1f, 0.29f, Vector3.down,
            groundHits, 0.78f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider.GetComponent<PirateCameraObstacle>() != null) continue;
            // Active puppet limbs have their own Rigidbodies but belong to this fighter.
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (hit.rigidbody == body || (otherBody != null && hit.rigidbody == otherBody) || (heldBody != null && hit.rigidbody == heldBody) || hit.normal.y < 0.5f || hit.distance >= best) continue;
            best = hit.distance; grounded = true; groundNormal = hit.normal;
            groundedOnArena = hit.collider.TryGetComponent(out PirateArenaSurface surface) && surface.walkable;
        }
        if (grounded) lastGrounded = Time.time;
    }

    private void Drive()
    {
        if (holdingLedge || otherLedge) return;
        float carry = heldBody != null ? 0.62f : 1f;
        Vector3 desired = Vector3.ProjectOnPlane(move, groundNormal) * tuning.moveSpeed * carry;
        Vector3 velocity = grounded ? Vector3.ProjectOnPlane(body.linearVelocity, groundNormal)
            : Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
        // Acceleration only: never overwrite momentum from a push, impact, or throw.
        Vector3 acceleration = Vector3.ClampMagnitude((desired - velocity) * 5f, tuning.acceleration);
        if (!grounded) acceleration = move.sqrMagnitude > 0.01f ? Vector3.ProjectOnPlane(acceleration, Vector3.up) * tuning.airControl : Vector3.zero;
        if (grounded && activePuppet != null && activePuppet.DriveLegs(acceleration))
            body.AddForce(acceleration * .8f, ForceMode.Acceleration);
        else body.AddForce(acceleration, ForceMode.Acceleration);
        // Gentle stick keeps feet on steps and ramps instead of launching off seams.
        if (grounded && body.linearVelocity.y < 2f && jumpBufferedUntil < Time.time)
            body.AddForce(Vector3.down * 4.5f, ForceMode.Acceleration);
        if (move.sqrMagnitude > 0.08f) facing = Vector3.ProjectOnPlane(move, Vector3.up).normalized;
        else if (Vector3.ProjectOnPlane(transform.forward, Vector3.up).sqrMagnitude > 0.01f) facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
        if (Vector3.Dot(transform.up, Vector3.up) < -0.85f) axis = transform.right;
        Vector3 tiltVelocity = Vector3.ProjectOnPlane(body.angularVelocity, Vector3.up);
        // A captive's mass exerts torque through the hands. Counter it while carrying, without locking rotation.
        float balance = heldBody != null ? 1f + tuning.carryBalance * Mathf.Clamp01(heldBody.mass / body.mass) : 1f;
        float recovery = Mathf.Clamp01((Time.time - stunnedUntil) / Mathf.Max(.1f, tuning.standUpBlendSeconds));
        float support = grounded ? 1f : .25f;
        Vector3 balanceAcceleration = axis * tuning.uprightSpring * balance - tiltVelocity * tuning.uprightDamping * Mathf.Sqrt(balance);
        body.AddTorque(Vector3.ClampMagnitude(balanceAcceleration, tuning.maxBalanceAcceleration) * recovery * support, ForceMode.Acceleration);
        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (flatForward.sqrMagnitude > 0.01f)
        {
            float turn = Vector3.SignedAngle(flatForward, facing, Vector3.up) * Mathf.Deg2Rad;
            body.AddTorque(Vector3.up * Mathf.Clamp(turn * 30f - body.angularVelocity.y * 8f, -35f, 35f), ForceMode.Acceleration);
        }
    }

    private void TryGrab()
    {
        if (IsGrabbing || Time.time < stunnedUntil || gripStamina < .2f) return;
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (grabPressed) motion?.Trigger(PirateFighterAnimation.Action.Reach);
        Vector3 flatAim = (leftReach || rightReach) ? lookDirection : facing;
        if (flatAim.sqrMagnitude < 0.001f) flatAim = transform.forward;
        Vector3 aim = Quaternion.AngleAxis(lookPitch - 26f, Vector3.Cross(Vector3.up, flatAim)) * flatAim;
        Vector3 handSide = Vector3.Cross(Vector3.up, flatAim).normalized * (primaryRight ? 0.24f : -0.24f);
        Vector3 origin = body.position + Vector3.up * 0.25f + aim * 0.9f + handSide;
        SphereCollider sensor = primaryRight ? rightHandSensor : leftHandSensor;
        sensor.transform.position = origin;
        // Search only colliders intersecting this hand's contact volume.
        int count = Physics.OverlapSphereNonAlloc(origin, sensor.radius, grabHits, ~0, QueryTriggerInteraction.Ignore);
        Rigidbody best = null;
        Vector3 bestPoint = Vector3.zero;
        float nearest = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Rigidbody candidate = grabHits[i].attachedRigidbody;
            PirateFighter candidateFighter = candidate != null ? candidate.GetComponentInParent<PirateFighter>() : null;
            if (candidateFighter != null) candidate = candidateFighter.Body;
            if (candidate == null || candidate == body || candidate.isKinematic) continue;
            PirateFighter fighter = candidateFighter;
            GrabbableProp prop = candidate.GetComponent<GrabbableProp>();
            if (fighter == null && prop == null) continue;
            if (fighter != null && (!fighter.IsAlive || (fighter.IsHeld && fighter.holder != this))) continue;
            if (prop != null && prop.Holder != null && prop.Holder != this) continue;
            Vector3 delta = candidate.worldCenterOfMass - body.worldCenterOfMass;
            if (delta.magnitude > tuning.grabReach || Vector3.Dot(delta.normalized, aim) < 0.05f) continue;
            if (!CanReach(candidate)) continue;
            Vector3 point = grabHits[i].ClosestPoint(origin);
            float distance = (point - origin).sqrMagnitude;
            if (distance < nearest) { nearest = distance; best = candidate; bestPoint = point; }
        }
        if (best == null) { TryLedge(origin, count); return; }
        heldBody = best;
        heldFighter = best.GetComponent<PirateFighter>();
        heldProp = best.GetComponent<GrabbableProp>();
        if (heldFighter != null && heldFighter.holder != this) { heldFighter.ReleaseGrab(false); heldFighter.holder = this; heldFighter.escapeTaps = 0; }
        if (heldProp != null) heldProp.Holder = this;
        grabJoint = MakeJoint(best, transform.InverseTransformPoint(bestPoint),
            best.transform.InverseTransformPoint(bestPoint));
        grabStarted = Time.time;
        if (IsHuman) director.ShowEvent("Shift: levantar · F: lanzar · suelta los botones para soltar");
    }

    private void TryLedge(Vector3 origin, int count)
    {
        if (grounded) return;
        Collider best = null; Vector3 anchor = Vector3.zero; float nearest = tuning.grabReach;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = grabHits[i];
            if (candidate.GetComponent<PirateCameraObstacle>() != null) continue;
            if (candidate.GetComponent<PirateArenaSurface>() == null) continue;
            Vector3 point = candidate.ClosestPoint(origin);
            point.y = Mathf.Min(candidate.bounds.max.y, body.position.y + 0.7f);
            float distance = Vector3.Distance(point, origin);
            if (point.y < body.position.y || distance >= nearest) continue;
            nearest = distance; best = candidate; anchor = point;
        }
        if (best == null) return;
        holdingLedge = true;
        grabJoint = MakeJoint(null, transform.InverseTransformPoint(anchor), anchor);
        // Ledge contacts can briefly spike when the capsule brushes a wall during a pull-up.
        grabJoint.breakForce = Mathf.Infinity;
        grabJoint.breakTorque = Mathf.Infinity;
        grabStarted = Time.time;
        facing = Vector3.ProjectOnPlane(best.bounds.center - body.position, Vector3.up).normalized;
        if (IsHuman) director.ShowEvent("¡Agarrado! Shift para subir · WASD para balancearte · Espacio para impulsarte");
    }

    private ConfigurableJoint MakeJoint(Rigidbody target, Vector3 anchor, Vector3 connectedAnchor)
    {
        ConfigurableJoint joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = target;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = anchor; joint.connectedAnchor = connectedAnchor;
        joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
        joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
        joint.linearLimit = new SoftJointLimit { limit = 0.12f };
        JointDrive drive = new JointDrive { positionSpring = tuning.carrySpring, positionDamper = tuning.carryDamping, maximumForce = 4500f };
        joint.xDrive = joint.yDrive = joint.zDrive = drive;
        joint.breakForce = 5500f; joint.breakTorque = 5500f;
        joint.enableCollision = false;
        joint.enablePreprocessing = false;
        return joint;
    }

    private void SwapHands()
    {
        (grabJoint, otherJoint) = (otherJoint, grabJoint);
        (heldBody, otherBody) = (otherBody, heldBody);
        (heldFighter, otherFighter) = (otherFighter, heldFighter);
        (heldProp, otherProp) = (otherProp, heldProp);
        (holdingLedge, otherLedge) = (otherLedge, holdingLedge);
        (grabStarted, otherStarted) = (otherStarted, grabStarted);
        primaryRight = !primaryRight;
    }

    public void ReleaseGrab(bool throwObject)
    {
        if (throwObject && IsHuman && lookDirection.sqrMagnitude > .01f)
            facing = Vector3.ProjectOnPlane(lookDirection, Vector3.up).normalized;
        bool sameTarget = heldBody != null && heldBody == otherBody;
        ReleaseCurrentGrab(throwObject);
        SwapHands();
        ReleaseCurrentGrab(throwObject && !sameTarget);
        SwapHands();
    }

    private void ReleaseCurrentGrab(bool throwObject)
    {
        Trace("ReleaseGrab throw=" + throwObject + " objetivo=" + (heldBody != null ? heldBody.name : "nulo"));
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Rigidbody target = heldBody;
        PirateFighter fighter = heldFighter;
        GrabbableProp prop = heldProp;
        if (grabJoint != null)
        {
            grabJoint.xMotion = grabJoint.yMotion = grabJoint.zMotion = ConfigurableJointMotion.Free;
            grabJoint.xDrive = grabJoint.yDrive = grabJoint.zDrive = new JointDrive();
            Destroy(grabJoint);
        }
        grabJoint = null; heldBody = null; heldFighter = null; heldProp = null; holdingLedge = false;
        if (fighter != null && fighter.holder == this && otherFighter != fighter) fighter.holder = null;
        if (prop != null && prop.Holder == this && otherProp != prop) prop.Holder = null;
        if (!throwObject || target == null || target.isKinematic) return;
        motion?.Trigger(PirateFighterAnimation.Action.Throw);
        float massFactor = Mathf.Clamp(Mathf.Sqrt(22f / target.mass), 0.65f, 1.15f);
        Vector3 launch = facing * tuning.throwSpeed * massFactor + Vector3.up * tuning.throwUpSpeed;
        target.AddForce(launch, ForceMode.VelocityChange);
        target.AddTorque(transform.right * 2f, ForceMode.VelocityChange);
        if (fighter != null) fighter.KnockDown(0.85f);
        if (prop != null) prop.MarkThrown(this);
        StartCoroutine(TemporarilyIgnore(target));
    }

    private IEnumerator TemporarilyIgnore(Rigidbody target)
    {
        Collider[] others = target.GetComponentsInChildren<Collider>();
        foreach (Collider other in others) if (other != null) Physics.IgnoreCollision(capsule, other, true);
        yield return new WaitForSeconds(0.25f);
        foreach (Collider other in others) if (other != null && capsule != null) Physics.IgnoreCollision(capsule, other, false);
    }

    private void PushNearby()
    {
        if (Time.time < pushReady || holdingLedge || otherLedge) return;
        if (IsHuman && lookDirection.sqrMagnitude > .01f) facing = Vector3.ProjectOnPlane(lookDirection, Vector3.up).normalized;
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        pushReady = Time.time + tuning.pushCooldown;
        motion?.Trigger(PirateFighterAnimation.Action.Push);
        Vector3 origin = body.position + Vector3.up * 0.25f + facing * 0.85f;
        int count = Physics.OverlapSphereNonAlloc(origin, 1.15f, grabHits, ~0, QueryTriggerInteraction.Ignore);
        Rigidbody nearest = null; float distance = 4f;
        for (int i = 0; i < count; i++)
        {
            Rigidbody other = grabHits[i].attachedRigidbody;
            PirateFighter targetFighter = other != null ? other.GetComponentInParent<PirateFighter>() : null;
            if (targetFighter != null) other = targetFighter.Body;
            if (other == null || other == body || other == heldBody || other == otherBody) continue;
            if (other.isKinematic || !CanReach(other)) continue;
            Vector3 delta = other.position - body.position;
            if (Vector3.Dot(delta.normalized, facing) < 0.15f || delta.sqrMagnitude >= distance) continue;
            nearest = other; distance = delta.sqrMagnitude;
        }
        if (nearest == null) return;
        // A running start adds up to 30% to the hit; the attacker feels a small recoil (equal and opposite, scaled down).
        float runUp = Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up), facing) / Mathf.Max(1f, tuning.moveSpeed));
        Vector3 impulse = facing * tuning.pushSpeed * (1f + tuning.pushRunUpBonus * runUp) + Vector3.up * 1.7f;
        PirateFighter fighter = nearest.GetComponent<PirateFighter>();
        Vector3 contact = Vector3.Lerp(body.position, nearest.position, .6f) + Vector3.up * .55f;
        if (fighter != null)
        {
            fighter.TakeCombatHit(impulse);
            if (tuning.pushRecoil > 0f) body.AddForce(-facing * tuning.pushRecoil * (1f + 1.3f * runUp), ForceMode.VelocityChange);
            PirateArenaAudio.Play(PirateArenaAudio.Sound.Punch, contact, .8f + .2f * runUp);
        }
        else
        {
            nearest.AddForce(impulse * Mathf.Clamp(18f / nearest.mass, 0.3f, 1f), ForceMode.VelocityChange);
            PirateArenaAudio.Play(PirateArenaAudio.Sound.WoodHit, contact, .9f);
        }
    }

    private bool CanReach(Rigidbody target)
    {
        Vector3 origin = body.worldCenterOfMass + Vector3.up * 0.2f;
        Vector3 delta = target.worldCenterOfMass - origin;
        int count = Physics.RaycastNonAlloc(origin, delta.normalized, interactionHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (interactionHits[i].rigidbody != body && interactionHits[i].rigidbody != target &&
                interactionHits[i].collider.GetComponent<PirateArenaSurface>() != null &&
                interactionHits[i].collider.GetComponent<PirateCameraObstacle>() == null) return false;
        return true;
    }

    public void TakePush(Vector3 velocityChange)
    {
        if (!IsAlive) return;
        Trace("TakePush v=" + velocityChange.ToString("F1") + (holder != null ? " (libera de su portador)" : ""));
        // Cap runaway impulses so stacked pushes and cannon blasts cannot launch fighters at absurd speeds.
        velocityChange = Vector3.ClampMagnitude(velocityChange, 18f);
        if (holder != null) holder.ReleaseGrab(false);
        ReleaseGrab(false);
        body.AddForce(velocityChange, ForceMode.VelocityChange);
        body.AddTorque(new Vector3(velocityChange.z, 0f, -velocityChange.x) * 0.32f, ForceMode.VelocityChange);
        motion?.PhysicalImpact(velocityChange);
        KnockDown(0.75f);
    }

    public void TakeCombatHit(Vector3 velocityChange)
    {
        if (!IsAlive || IsProxy || body.isKinematic) return;
        // Hits still displace a recovering player, but cannot extend a KO indefinitely.
        bool protectedRecovery = Time.time < recoveryProtectedUntil;
        if (!protectedRecovery) { hitStrain += 1f; lastCombatHit = Time.time; }
        float oldStun = stunnedUntil;
        TakePush(velocityChange * .65f);
        stunnedUntil = protectedRecovery ? oldStun : Mathf.Max(oldStun, Time.time + .22f);
        if (protectedRecovery || hitStrain < tuning.knockoutThreshold) return;
        hitStrain = 0f;
        knockoutUntil = Time.time + tuning.knockoutSeconds;
        recoveryProtectedUntil = knockoutUntil + tuning.recoveryProtectionSeconds;
        KnockDown(tuning.knockoutSeconds);
        director.ShowEvent(DisplayName + " cae KO: ¡agarra, levanta y lanza!");
    }

    public void KnockDown(float seconds)
    {
        stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        move = Vector3.zero;
        jumpBufferedUntil = 0f;
        grabPressed = pushPressed = false;
        ReleaseGrab(false);
    }
    public void Eliminate()
    {
        if (!IsAlive) return;
        if (holder != null) holder.ReleaseGrab(false);
        ReleaseGrab(false);
        IsAlive = false; capsule.enabled = false; eliminatedAt = Time.time;
        SetTint(new Color(0.5f, 0.6f, 0.65f));
    }

    public void Respawn()
    {
        if (holder != null) holder.ReleaseGrab(false);
        ReleaseGrab(false);
        body.isKinematic = false;
        // Move the transform as well as the Rigidbody before resetting the animated reference and puppet.
        transform.SetPositionAndRotation(spawn, Quaternion.identity);
        body.position = spawn; body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
        capsule.enabled = true; IsAlive = true;
        PirateCameraObstacle.IgnoreFor(capsule);
        move = Vector3.zero; facing = Vector3.forward;
        grabPressed = grabReleased = pushPressed = grabHeld = leftReach = rightReach = false;
        blockGrabUntilRelease = false;
        escapeTaps = 0; escapeReady = 0f; botThrowAt = 0f;
        stunnedUntil = pushReady = jumpBufferedUntil = jumpReady = 0f;
        hitStrain = knockoutUntil = recoveryProtectedUntil = 0f;
        lastCombatHit = -100f; gripStamina = 1f;
        lastGrounded = -100f;
        grounded = lifting = false;
        motion?.ResetPose();
        SetTint(Color.white);
    }

    private void SetTint(Color color)
    {
        if (tint == null) tint = new MaterialPropertyBlock();
        if (visuals == null) visuals = GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in visuals)
        {
            if (renderer == null) continue;
            // Soft-crew pieces keep their own colour; the tint only multiplies it.
            PirateCrewPart part = renderer.GetComponent<PirateCrewPart>();
            tint.SetColor("_BaseColor", part != null ? part.baseColor * color : color);
            renderer.SetPropertyBlock(tint);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Live fighters bumping into each other is ordinary running contact; pushes and throws govern their knockdowns.
        PirateFighter otherFighter = collision.rigidbody != null ? collision.rigidbody.GetComponentInParent<PirateFighter>() : null;
        bool liveFighterContact = otherFighter != null && otherFighter.IsAlive && !object.ReferenceEquals(otherFighter, this);
        if (!IsProxy && IsAlive && !liveFighterContact && collision.relativeVelocity.magnitude > 7.5f)
        {
            motion?.PhysicalImpact(collision.relativeVelocity * .25f);
            KnockDown(0.55f);
        }
    }

    public void SetControl(bool local, string label)
    {
        IsHuman = local; DisplayName = label;
        Transform marker = transform.Find("Indicador del jugador");
        if (local && marker == null) { CreatePlayerMarker(); marker = transform.Find("Indicador del jugador"); }
        if (marker != null) marker.gameObject.SetActive(local);
    }

    public void SetNetworkMode(bool driven, bool proxy)
    {
        ReleaseGrab(false); move = Vector3.zero; previousGrab = grabHeld = leftReach = rightReach = false;
        blockGrabUntilRelease = false;
        NetworkDriven = driven; IsProxy = proxy; remotePositioned = false;
        body.interpolation = driven ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = proxy ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
        body.isKinematic = proxy;
    }

    public void NetworkBot() { if (IsAlive && director.IsPlaying) ReadBot(); }

    public void ApplyInput(PirateNetworkInput input, PirateNetworkInput previous)
    {
        if (!IsAlive || !director.IsPlaying) { move = Vector3.zero; return; }
        if (IsStunned) { move = Vector3.zero; return; }
        bool jump = input.Jump != previous.Jump;
        if (IsHeld)
        {
            if (jump && Time.time >= escapeReady)
            {
                escapeReady = Time.time + 0.18f;
                if (++escapeTaps >= 3) holder.ReleaseGrab(false);
            }
            return;
        }
        Vector2 axis = input.Move;
        if (!float.IsFinite(axis.x) || !float.IsFinite(axis.y) || !float.IsFinite(input.Yaw)) axis = Vector2.zero;
        axis = Vector2.ClampMagnitude(axis, 1f);
        float yaw = float.IsFinite(input.Yaw) ? Mathf.Repeat(input.Yaw, 360f) : 0f;
        move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(axis.x, 0f, axis.y);
        lifting = input.Lift;
        if (jump) jumpBufferedUntil = Time.time + 0.13f;
        leftReach = input.Grab;
        rightReach = input.RightGrab;
        lookPitch = float.IsFinite(input.LookPitch) ? Mathf.Clamp(input.LookPitch, -55f, 65f) : 26f;
        lookDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        grabHeld = leftReach || rightReach;
        grabPressed |= grabHeld && !previousGrab;
        grabReleased |= !grabHeld && previousGrab;
        previousGrab = grabHeld;
        pushPressed |= input.Push != previous.Push;
        if (!grabHeld) blockGrabUntilRelease = false;
        if (input.Drop != previous.Drop) { blockGrabUntilRelease = true; grabPressed = false; ReleaseGrab(false); grabReleased = false; }
        else if (input.Throw != previous.Throw) { blockGrabUntilRelease = true; grabPressed = false; ReleaseGrab(true); grabReleased = false; }
    }

    public PirateBodyState CaptureState() => new PirateBodyState {
        Position = body.position, Rotation = body.rotation, Velocity = body.linearVelocity,
        Grip = GripWorldPosition, LeftGrip = LeftGripPosition, RightGrip = RightGripPosition,
        Flags = (IsAlive ? 1 : 0) | (grounded ? 2 : 0) | (IsHeld ? 4 : 0) | (IsGrabbing ? 8 : 0) |
            (IsStunned ? 16 : 0) | (IsLifting ? 32 : 0) | ((holdingLedge || otherLedge) ? 64 : 0) |
            (leftReach ? 128 : 0) | (rightReach ? 256 : 0) |
            (LeftGripped ? 512 : 0) | (RightGripped ? 1024 : 0),
        LookPitch = lookPitch,
        Action = motion != null ? (int)motion.LastAction : 0, ActionSerial = motion != null ? motion.ActionSerial : 0
    };

    public void ApplyState(PirateBodyState value)
    {
        bool alive = (value.Flags & 1) != 0;
        if (IsAlive && !alive && value.Position.y < 1.5f)
            PirateArenaAudio.Play(PirateArenaAudio.Sound.Splash, value.Position);
        if (alive != IsAlive) { SetTint(alive ? Color.white : new Color(0.5f, 0.6f, 0.65f)); if (alive) motion?.ResetPose(); }
        IsAlive = alive; capsule.enabled = alive; grounded = (value.Flags & 2) != 0; remote = value;
        float blend = !remotePositioned || Vector3.Distance(transform.position, value.Position) > 6f ? 1f : 1f - Mathf.Exp(-22f * Time.deltaTime);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, value.Position, blend), Quaternion.Slerp(transform.rotation, value.Rotation, blend));
        if (remotePositioned && remoteActionSerial != value.ActionSerial) motion?.Trigger((PirateFighterAnimation.Action)value.Action);
        remoteActionSerial = value.ActionSerial; remotePositioned = true;
    }

    private void OnJointBreak(float force) { Trace("OnJointBreak fuerza=" + force.ToString("F0")); ReleaseGrab(false); }
    private void OnDisable() { ReleaseGrab(false); }
}
