using System.Collections.Generic;
using UnityEngine;

/// <summary>Physical visual skeleton driven toward a separate animated reference pose.</summary>
public sealed class PirateActivePuppet : MonoBehaviour
{
    private struct Part
    {
        public Rigidbody body;
        public Collider collider;
        public Transform target;
        public Transform targetParent;
        public ConfigurableJoint joint;
        public Quaternion startLocal;      // bone rotation relative to its connected body at build time
        public Quaternion jointSpace;      // world-to-joint-space for this joint's axes
        public float stiffness, damping;   // per kg, tuned by body region
    }

    private readonly List<Part> parts = new List<Part>();
    private PirateFighter fighter;
    private GameObject puppet;
    private bool proxyCollidersDisabled;
    public bool Ready { get; private set; }
    public GameObject Visual => puppet;
    private float tone = 1f;

    private static Transform Find(Transform root, string bone)
    {
        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            if (item.name == bone) return item;
        return null;
    }

    public bool Initialize(PirateFighter owner, GameObject animatedModel)
    {
        fighter = owner;
        puppet = Instantiate(animatedModel, animatedModel.transform.parent, false);
        puppet.name = animatedModel.name + " - cuerpo físico";
        var clonedAnimator = puppet.GetComponentInChildren<Animator>();
        if (clonedAnimator != null) clonedAnimator.enabled = false;
        var root = clonedAnimator != null ? clonedAnimator.transform : puppet.transform;
        var referenceAnimator = animatedModel.GetComponentInChildren<Animator>();
        if (referenceAnimator == null || Find(root, "Torso") == null)
        {
            Destroy(puppet);
            return false;
        }
        var targets = referenceAnimator.transform;
        // Stiffness per region: a firm spine and neck keep the silhouette upright and readable;
        // arms stay looser so impacts and grabs still look physical; legs track the walk cycle.
        Add(root, targets, "Torso", null, .30f, .48f, 6f, 70f, 70f, 1400f);
        Add(root, targets, "Head", "Torso", .20f, .30f, 2f, 55f, 55f, 1100f);
        foreach (string side in new[] { "L", "R" })
        {
            Add(root, targets, "UpperArm." + side, "Torso", .11f, .32f, 1.4f, 120f, 95f, 650f);
            Add(root, targets, "LowerArm." + side, "UpperArm." + side, .10f, .30f, 1f, 130f, 70f, 500f);
            Add(root, targets, "UpperLeg." + side, null, .15f, .40f, 2.5f, 95f, 55f, 2600f);
            Add(root, targets, "LowerLeg." + side, "UpperLeg." + side, .12f, .38f, 1.5f, 125f, 35f, 2200f);
        }
        if (parts.Count < 6)
        {
            Destroy(puppet);
            parts.Clear();
            return false;
        }
        var coreCollider = owner.GetComponent<CapsuleCollider>();
        foreach (var part in parts)
        {
            if (coreCollider != null) Physics.IgnoreCollision(coreCollider, part.collider, true);
            foreach (var other in parts)
                if (part.collider != other.collider) Physics.IgnoreCollision(part.collider, other.collider, true);
        }
        foreach (var renderer in animatedModel.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        Ready = true;
        return true;
    }

    private void Add(Transform root, Transform targets, string boneName, string parentName,
        float radius, float length, float mass, float swing, float twist, float stiffness)
    {
        Transform bone = Find(root, boneName), target = Find(targets, boneName);
        if (bone == null || target == null) return;
        var body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.solverIterations = 12; body.solverVelocityIterations = 6;
        body.maxAngularVelocity = 12f;
        body.maxDepenetrationVelocity = 1.5f;
        var collider = bone.gameObject.AddComponent<CapsuleCollider>();
        collider.radius = radius * .7f; collider.height = Mathf.Max(length * .75f, collider.radius * 2.1f);
        collider.center = Vector3.up * length * .35f;
        PirateCameraObstacle.IgnoreFor(collider);
        Transform parentBone = parentName != null ? Find(root, parentName) : null;
        Rigidbody parentBody = parentBone != null ? parentBone.GetComponent<Rigidbody>() : null;
        if (parentBody == null) parentBody = fighter.Body;
        var joint = bone.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = parentBody; joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero; joint.connectedAnchor = parentBody.transform.InverseTransformPoint(body.position);
        joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
        // Limits are relative to the build pose; wide enough for the clips and IK reaches.
        joint.lowAngularXLimit = new SoftJointLimit { limit = -Mathf.Min(swing, 177f) };
        joint.highAngularXLimit = new SoftJointLimit { limit = Mathf.Min(swing, 177f) };
        joint.angularYLimit = new SoftJointLimit { limit = Mathf.Min(twist, 177f) };
        joint.angularZLimit = new SoftJointLimit { limit = Mathf.Min(swing, 177f) };
        joint.enableCollision = false; joint.enablePreprocessing = false;
        // Implicit PhysX muscle: Slerp drive toward the animated pose (internal, no net thrust).
        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.configuredInWorldSpace = false;
        Vector3 right = joint.axis.normalized, forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized, up = Vector3.Cross(forward, right).normalized;
        parts.Add(new Part { body = body, collider = collider, target = target,
            targetParent = parentName != null ? Find(targets, parentName) : fighter.transform, joint = joint,
            startLocal = Quaternion.Inverse(parentBody.rotation) * body.rotation,
            jointSpace = Quaternion.LookRotation(forward, up),
            stiffness = stiffness, damping = stiffness * .055f });
    }

    private void FixedUpdate()
    {
        if (!Ready || fighter == null) return;
        bool disableColliders = fighter.IsProxy || !fighter.IsAlive;
        if (proxyCollidersDisabled != disableColliders)
        {
            proxyCollidersDisabled = disableColliders;
            foreach (var part in parts) if (part.collider != null) part.collider.enabled = !proxyCollidersDisabled;
        }
        // Proxies and a frozen core must not be pulled around by unsimulated limbs.
        bool kinematic = fighter.IsProxy || fighter.Body.isKinematic;
        float target = !fighter.IsAlive ? 0f : fighter.IsStunned ? .22f : 1f;
        // Muscles regain tone over ~0.35 s after a stun instead of snapping back instantly.
        tone = Mathf.MoveTowards(tone, target, Time.fixedDeltaTime * (target > tone ? 2.4f : 12f));
        float strength = tone;
        foreach (var part in parts)
        {
            if (part.body == null || part.target == null) continue;
            if (part.body.isKinematic != kinematic)
            {
                part.body.isKinematic = kinematic;
                part.body.position = part.target.position;
                part.body.rotation = part.target.rotation;
                if (!kinematic)
                {
                    part.body.linearVelocity = fighter.Body.GetPointVelocity(part.body.position);
                    part.body.angularVelocity = fighter.Body.angularVelocity;
                }
            }
            if (kinematic)
            {
                part.body.MovePosition(part.target.position);
                part.body.MoveRotation(part.target.rotation);
                continue;
            }
            Rigidbody parent = part.joint != null ? part.joint.connectedBody : null;
            if (parent == null || part.targetParent == null) continue;
            SetDrive(part, strength);
            if (strength <= 0f) continue;
            // Target = the animated bone's rotation relative to its animated parent (IK included).
            Quaternion targetLocal = Quaternion.Inverse(part.targetParent.rotation) * part.target.rotation;
            part.joint.targetRotation = Quaternion.Inverse(part.jointSpace) * (Quaternion.Inverse(targetLocal) * part.startLocal) * part.jointSpace;
        }
    }

    private void SetDrive(Part part, float strength)
    {
        // Rebuild drives only when the strength band changes (cheap per-frame check).
        float k = part.stiffness * part.body.mass * strength * strength;
        var drive = part.joint.slerpDrive;
        if (Mathf.Abs(drive.positionSpring - k) < .5f) return;
        drive.positionSpring = k;
        drive.positionDamper = part.damping * part.body.mass * Mathf.Max(strength, .25f);
        drive.maximumForce = float.MaxValue;
        part.joint.slerpDrive = drive;
    }

    public void ApplyImpact(Vector3 impulse)
    {
        if (!Ready || fighter.IsProxy || fighter.Body.isKinematic) return;
        // The core already receives the full impact. A small visual impulse is
        // enough here; applying a large impulse to every connected limb adds energy.
        Vector3 change = Vector3.ClampMagnitude(impulse, 8f) * .05f;
        foreach (var part in parts)
            if (part.body != null && !part.body.isKinematic)
            {
                Vector3 impulsePart = change * part.body.mass;
                Vector3 point = part.body.worldCenterOfMass;
                part.body.AddForceAtPosition(impulsePart, point, ForceMode.Impulse);
                fighter.Body.AddForceAtPosition(-impulsePart, point, ForceMode.Impulse);
            }
    }

    // Share a bounded portion of ground propulsion with the physical upper legs.
    public bool DriveLegs(Vector3 acceleration)
    {
        if (!Ready || fighter.IsProxy || fighter.Body.isKinematic || !fighter.IsAlive || fighter.IsStunned) return false;
        int count = 0;
        foreach (var part in parts)
            if (part.body != null && part.body.name.StartsWith("UpperLeg.")) count++;
        if (count == 0) return false;
        Vector3 force = Vector3.ClampMagnitude(acceleration * (fighter.Body.mass * .2f / count), 180f);
        foreach (var part in parts)
            if (part.body != null && !part.body.isKinematic && part.body.name.StartsWith("UpperLeg."))
                part.body.AddForce(force, ForceMode.Force);
        return true;
    }

    public void ResetPose()
    {
        if (!Ready) return;
        foreach (var part in parts)
        {
            if (part.body == null || part.target == null) continue;
            part.body.position = part.target.position; part.body.rotation = part.target.rotation;
            if (!part.body.isKinematic)
            {
                part.body.linearVelocity = fighter.Body.GetPointVelocity(part.body.position);
                part.body.angularVelocity = fighter.Body.angularVelocity;
            }
        }
    }
}
