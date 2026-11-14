using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>Visual animation only. The fighter's Rigidbody remains the movement authority.</summary>
[DefaultExecutionOrder(100)]
public sealed class PirateFighterAnimation : MonoBehaviour
{
    public enum Action { None, Reach, Push, Throw, Jump, Land, Recover }
    [SerializeField, Range(0.05f, 0.4f)] float blendSeconds = 0.16f;
    [SerializeField] float walkReferenceSpeed = 2f;
    [SerializeField] float runReferenceSpeed = 5f;
    PirateFighter fighter;
    Animator animator;
    Transform rigRoot;
    Arm left, right;
    RigBuilder rigBuilder;
    PirateFighterRagdoll ragdoll;
    PirateActivePuppet puppet;
    TwoBoneIKConstraint leftIK, rightIK;
    Transform leftTarget, rightTarget, leftHint, rightHint;
    readonly Dictionary<string, int> states = new Dictionary<string, int>();
    string state;
    Action action;
    float actionStarted, actionDuration, armWeight, airborneTime;
    float nextStep;
    bool wasGrounded, wasStunned;
    // Procedural layer on top of the clips: lean into speed and turns, head tracks the nearest rival.
    Quaternion rootBase; Transform head;
    Vector3 lastVelocity, lastForward; float leanForward, leanSide, headWeight;
    public string CurrentPose { get; private set; }
    public Action LastAction { get; private set; }
    public int ActionSerial { get; private set; }
    public bool Ready => animator != null && animator.runtimeAnimatorController != null;

    sealed class Arm
    {
        public Transform upper, lower, hand;
        public bool Valid => upper != null && lower != null && hand != null;
    }

    public void Initialize(PirateFighter owner, GameObject model)
    {
        fighter = owner;
        animator = model.GetComponentInChildren<Animator>();
        if (!Ready) { enabled = false; Debug.LogWarning("Falta el rig animado de " + model.name, this); return; }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        rigRoot = animator.transform;
        rootBase = rigRoot.localRotation;
        left = MakeArm("L"); right = MakeArm("R");
        head = FindBone("Head");
        puppet = gameObject.AddComponent<PirateActivePuppet>();
        if (!puppet.Initialize(fighter, model)) { Destroy(puppet); puppet = null; }
        else if (fighter.Director == null || fighter.Director.Settings == null || fighter.Director.Settings.softCrew)
            PirateCrewStyle.Apply(fighter, puppet.Visual, PirateCrewStyle.IndexFor(fighter.DisplayName));
        SetupRig();
        ragdoll = gameObject.AddComponent<PirateFighterRagdoll>();
        ragdoll.Initialize(animator, fighter.Body, fighter.GetComponent<CapsuleCollider>());
        foreach (string clip in new[] { "Idle", "Walk", "Run", "Jump", "Jump_Idle", "Jump_Land", "HitReact", "Death", "Wave", "Duck" })
        {
            int hash = Animator.StringToHash("Base Layer." + clip);
            if (animator.HasState(0, hash)) states[clip] = hash;
        }
        ResetPose();
    }

    Transform FindBone(string bone)
    {
        foreach (Transform t in rigRoot.GetComponentsInChildren<Transform>())
            if (t.name == bone) return t;
        return null;
    }

    Arm MakeArm(string side)
    {
        Transform lower = FindBone("LowerArm." + side);
        return new Arm { upper = FindBone("UpperArm." + side), lower = lower,
            hand = FindBone("Middle1." + side) ?? FindBone("Index1." + side) ?? FindBone("Hand." + side) };
    }

    void SetupRig()
    {
        if (left == null || right == null || !left.Valid || !right.Valid)
        {
            Debug.LogWarning("No se pudo crear IK de brazos para " + name, this);
            return;
        }
        var root = new GameObject("Pirate Arm Rig").transform;
        root.SetParent(animator.transform, false);
        var rig = root.gameObject.AddComponent<Rig>();
        leftIK = MakeConstraint(root, left, "Left Arm IK", out leftTarget, out leftHint);
        rightIK = MakeConstraint(root, right, "Right Arm IK", out rightTarget, out rightHint);
        rigBuilder = animator.gameObject.AddComponent<RigBuilder>();
        rigBuilder.layers.Add(new RigLayer(rig));
        if (!rigBuilder.Build()) Debug.LogWarning("Animation Rigging no pudo construir el rig de " + name, this);
    }

    static TwoBoneIKConstraint MakeConstraint(Transform parent, Arm arm, string label, out Transform target, out Transform hint)
    {
        target = new GameObject(label + " Target").transform;
        target.SetParent(parent, false);
        target.position = arm.hand.position;
        hint = new GameObject(label + " Elbow").transform;
        hint.SetParent(parent, false);
        hint.position = arm.lower.position;
        var constraint = new GameObject(label, typeof(TwoBoneIKConstraint)).GetComponent<TwoBoneIKConstraint>();
        constraint.transform.SetParent(parent, false);
        var data = constraint.data;
        data.root = arm.upper; data.mid = arm.lower; data.tip = arm.hand;
        data.target = target; data.hint = hint;
        data.targetPositionWeight = 1f; data.targetRotationWeight = 0f; data.hintWeight = 0.6f;
        constraint.data = data;
        constraint.weight = 0f;
        return constraint;
    }

    public void Trigger(Action next)
    {
        LastAction = next; ActionSerial++;
        PirateArenaAudio.PlayAction(next, transform.position);
        action = next; actionStarted = Time.time;
        actionDuration = next == Action.Push ? 0.42f : next == Action.Throw ? 0.5f :
            next == Action.Recover ? 0.5f : next == Action.Land ? 0.24f : 0.32f;
    }

    public void PhysicalImpact(Vector3 impulse)
    {
        if (puppet != null) puppet.ApplyImpact(impulse);
        else ragdoll?.SetImpact(impulse);
        PirateArenaAudio.Play(PirateArenaAudio.Sound.Impact, transform.position, .75f);
    }

    public void ResetPose()
    {
        if (!Ready) return;
        ragdoll?.Deactivate();
        action = Action.None; armWeight = airborneTime = 0f;
        wasGrounded = wasStunned = false;
        state = null;
        animator.Rebind(); animator.Update(0f);
        puppet?.ResetPose();
        Play("Idle", 1f, true);
        CurrentPose = "Idle";
    }

    void Update()
    {
        if (!Ready || fighter == null) return;
        bool ground = fighter.IsGrounded;
        bool stun = fighter.IsStunned;
        if (fighter.IsProxy && stun && !wasStunned)
            PirateArenaAudio.Play(PirateArenaAudio.Sound.Impact, transform.position, .75f);
        if (puppet == null && stun && fighter.IsAlive) ragdoll?.Activate();
        else if (ragdoll != null && ragdoll.Active)
        {
            if (fighter.IsAlive) ragdoll.BeginRecovery();
            else ragdoll.Deactivate();
        }
        if (ragdoll != null && ragdoll.Active)
        {
            CurrentPose = ragdoll.Recovering ? "PhysicalRecovery" : "Ragdoll";
            wasStunned = stun || ragdoll.Recovering;
            return;
        }
        if (!ground) airborneTime += Time.deltaTime;
        if (ground && !wasGrounded && airborneTime > 0.18f && !stun && !fighter.IsHeld)
            Trigger(Action.Land);
        if (ground) airborneTime = 0f;
        if (wasStunned && !stun && ground && fighter.IsAlive) Trigger(Action.Recover);
        wasGrounded = ground; wasStunned = stun;
        if (Time.time - actionStarted >= actionDuration) action = Action.None;

        float speed = Vector3.ProjectOnPlane(fighter.VisualVelocity, Vector3.up).magnitude;
        if (ground && speed > .7f && fighter.RoundPlaying && fighter.IsAlive && Time.time >= nextStep)
        {
            PirateFighterAudio.PlayFootstep(transform.position, Mathf.Clamp01(speed / 6f));
            nextStep = Time.time + Mathf.Lerp(.55f, .29f, Mathf.Clamp01(speed / 6f));
        }
        string clip; float rate = 1f;
        if (!fighter.IsAlive) { clip = "Death"; CurrentPose = "Eliminated"; }
        else if (fighter.IsHeld) { clip = "Jump_Idle"; CurrentPose = "Struggle"; }
        else if (fighter.IsHoldingLedge) { clip = "Jump_Idle"; CurrentPose = "Hang"; }
        else if (stun) { clip = "HitReact"; CurrentPose = "Knockdown"; }
        else if (fighter.HasWon) { clip = "Wave"; CurrentPose = "Victory"; }
        else if (!fighter.RoundPlaying) { clip = "Idle"; CurrentPose = "Idle"; }
        else if (action == Action.Jump) { clip = "Jump"; CurrentPose = "Jump"; }
        else if (!ground && airborneTime > 0.12f) { clip = "Jump_Idle"; CurrentPose = "Fall"; }
        else if (action == Action.Land) { clip = "Jump_Land"; CurrentPose = "Land"; }
        else if (action == Action.Recover) { clip = "Jump_Land"; CurrentPose = "Recover"; rate = 0.65f; }
        else
        {
            clip = speed > 2.8f ? "Run" : speed > 0.15f ? "Walk" : "Idle";
            if (clip != "Idle") rate = Mathf.Clamp(speed / (clip == "Run" ? runReferenceSpeed : walkReferenceSpeed), 0.45f, 1.65f);
            CurrentPose = fighter.IsGrabbing ? (fighter.IsLifting ? "Lift" : "Carry") : action != Action.None ? action.ToString() : clip;
        }
        Play(clip, rate);
    }

    void Play(string clip, float speed, bool immediate = false)
    {
        if (!states.TryGetValue(clip, out int hash))
        {
            if (!states.TryGetValue("Idle", out hash)) return;
            clip = "Idle";
        }
        animator.speed = speed;
        if (state == clip) return;
        if (immediate) animator.Play(hash, 0, 0f);
        else animator.CrossFadeInFixedTime(hash, blendSeconds, 0, 0f);
        state = clip;
    }

    void ProceduralMotion()
    {
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        bool upright = fighter.IsAlive && !fighter.IsStunned && !fighter.IsHeld && !fighter.IsHoldingLedge;
        Vector3 v = Vector3.ProjectOnPlane(fighter.VisualVelocity, Vector3.up);
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 accel = (v - lastVelocity) / dt; lastVelocity = v;
        float turnRate = lastForward.sqrMagnitude > .1f ? Vector3.SignedAngle(lastForward, fwd, Vector3.up) / dt : 0f; lastForward = fwd;
        float targetForward = 0f, targetSide = 0f;
        if (upright && fighter.IsGrounded)
        {
            targetForward = Mathf.Clamp(Vector3.Dot(v, fwd) / runReferenceSpeed * 7f + Vector3.Dot(accel, fwd) * .45f, -5f, 11f);
            targetSide = Mathf.Clamp(turnRate * v.magnitude * .0035f, -9f, 9f);
            if (fighter.IsLifting) targetForward -= 6f; // brace backwards under a lifted load
        }
        leanForward = Mathf.Lerp(leanForward, targetForward, 1f - Mathf.Exp(-8f * dt));
        leanSide = Mathf.Lerp(leanSide, targetSide, 1f - Mathf.Exp(-6f * dt));
        // Pre-multiply: lean in the fighter's frame (x right, z forward), not the imported model's axes.
        rigRoot.localRotation = Quaternion.Euler(leanForward, 0f, -leanSide) * rootBase;

        // Head look-at: nearest living rival in front, within 7 m, at most 40°.
        if (head == null || fighter.Director == null) return;
        PirateFighter best = null; float bestD = 7f;
        foreach (var other in fighter.Director.Fighters)
        {
            if (other == null || other == fighter || !other.IsAlive) continue;
            Vector3 d = other.transform.position - transform.position; float m = d.magnitude;
            if (m < bestD && Vector3.Angle(fwd, Vector3.ProjectOnPlane(d, Vector3.up)) < 100f) { best = other; bestD = m; }
        }
        headWeight = Mathf.MoveTowards(headWeight, best != null && upright ? .55f : 0f, dt * 2.5f);
        if (headWeight < .01f || best == null) return;
        Vector3 to = (best.transform.position + Vector3.up * .6f) - head.position;
        Quaternion delta = Quaternion.FromToRotation(transform.forward, to.normalized);
        delta.ToAngleAxis(out float ang, out Vector3 axis);
        if (ang > 180f) ang -= 360f;
        if (axis.sqrMagnitude < .001f) return;
        head.rotation = Quaternion.AngleAxis(Mathf.Clamp(ang, -40f, 40f) * headWeight, axis) * head.rotation;
    }

    void LateUpdate()
    {
        if (Ready && fighter != null && !(ragdoll != null && ragdoll.Active)) ProceduralMotion();
        if (!Ready || fighter == null || (ragdoll != null && ragdoll.Active)) return;
        bool active = fighter.IsAlive && !fighter.IsStunned;
        bool gesture = action == Action.Reach || action == Action.Push || action == Action.Throw;
        float targetWeight = active && (fighter.IsGrabbing || fighter.IsHeld || fighter.LeftReach || fighter.RightReach || gesture) ? 1f : 0f;
        armWeight = Mathf.MoveTowards(armWeight, targetWeight, Time.deltaTime * 9f);
        if (armWeight < 0.001f)
        {
            if (leftIK != null) leftIK.weight = 0f;
            if (rightIK != null) rightIK.weight = 0f;
            return;
        }
        Vector3 forward = transform.forward, up = transform.up, side = transform.right;
        Vector3 center = transform.position + up * 0.3f + forward * 0.7f;
        float spread = 0.24f;
        if (fighter.LeftReach || fighter.RightReach)
            center += up * Mathf.Clamp((26f - fighter.LookPitch) / 45f, -0.65f, 0.65f);
        float progress = Mathf.Clamp01((Time.time - actionStarted) / Mathf.Max(0.01f, actionDuration));

        if (fighter.IsHeld)
        {
            float wave = Mathf.Sin(Time.time * 14f);
            center = transform.position + up * (0.5f + wave * 0.12f) + forward * 0.25f;
            spread = 0.55f;
        }
        else if (fighter.IsGrabbing)
        {
            center = fighter.GripWorldPosition;
            // Hands meet the near surface, rather than entering the centre of a barrel or another pirate.
            if (!fighter.IsHoldingLedge)
                center -= fighter.IsLifting ? up * 0.38f + forward * 0.12f : forward * 0.3f;
            spread = fighter.IsHoldingLedge ? 0.25f : 0.22f;
        }
        else if (action == Action.Push)
        {
            float reach = Mathf.Sin(progress * Mathf.PI);
            center = transform.position + up * 0.27f + forward * Mathf.Lerp(0.2f, 0.9f, reach);
        }
        else if (action == Action.Throw)
        {
            center = transform.position + up * Mathf.Lerp(0.85f, 0.15f, progress) + forward * Mathf.Lerp(0.2f, 0.95f, progress);
        }

        if (leftIK != null && rightIK != null)
        {
            leftTarget.position = fighter.LeftGripped ? fighter.LeftGripPosition : center - side * spread;
            rightTarget.position = fighter.RightGripped ? fighter.RightGripPosition : center + side * spread;
            leftHint.position = left.lower.position - side * 0.35f - up * 0.15f;
            rightHint.position = right.lower.position + side * 0.35f - up * 0.15f;
            bool both = fighter.IsHeld || ((gesture || fighter.IsGrabbing) && !fighter.LeftReach && !fighter.RightReach);
            leftIK.weight = both || fighter.LeftReach ? armWeight : 0f;
            rightIK.weight = both || fighter.RightReach ? armWeight : 0f;
        }
    }

}
