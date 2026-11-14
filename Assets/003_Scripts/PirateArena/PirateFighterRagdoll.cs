using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>Brief physical limb reaction. The main capsule remains authoritative for gameplay.</summary>
public sealed class PirateFighterRagdoll : MonoBehaviour
{
    struct Bone
    {
        public Transform transform, parent;
        public float radius, length, mass;
    }
    struct Part
    {
        public Rigidbody body;
        public Collider collider;
        public ConfigurableJoint joint;
        public Quaternion restRelativeToCore;
    }
    readonly List<Bone> bones = new List<Bone>();
    readonly List<Part> parts = new List<Part>();
    Animator animator;
    RigBuilder rig;
    Rigidbody core;
    CapsuleCollider coreCollider;
    Vector3 pendingImpact;
    float recoverUntil;
    public bool Active { get; private set; }
    public bool Recovering { get; private set; }

    public void Initialize(Animator modelAnimator, Rigidbody fighterBody, CapsuleCollider fighterCollider)
    {
        animator = modelAnimator; rig = animator.GetComponent<RigBuilder>();
        core = fighterBody; coreCollider = fighterCollider;
        var transforms = animator.GetComponentsInChildren<Transform>();
        Add(transforms, "Torso", null, .36f, .5f, 7f);
        Add(transforms, "Head", "Torso", .23f, .35f, 3f);
        foreach (string side in new[] { "L", "R" })
        {
            Add(transforms, "UpperArm." + side, "Torso", .13f, .36f, 2f);
            Add(transforms, "LowerArm." + side, "UpperArm." + side, .11f, .34f, 1.5f);
            Add(transforms, "UpperLeg." + side, null, .18f, .46f, 4f);
            Add(transforms, "LowerLeg." + side, "UpperLeg." + side, .14f, .42f, 2.5f);
        }
    }

    static Transform Find(Transform[] transforms, string name)
    {
        foreach (var candidate in transforms) if (candidate.name == name) return candidate;
        return null;
    }

    void Add(Transform[] transforms, string name, string parentName, float radius, float length, float mass)
    {
        var bone = Find(transforms, name);
        if (bone == null) return;
        bones.Add(new Bone { transform = bone, parent = parentName == null ? null : Find(transforms, parentName),
            radius = radius, length = length, mass = mass });
    }

    public void SetImpact(Vector3 impulse) { pendingImpact = Vector3.ClampMagnitude(impulse, 12f); }

    public void Activate()
    {
        if (Active) { Recovering = false; return; }
        if (bones.Count == 0) return;
        Active = true;
        Recovering = false;
        if (rig != null) rig.enabled = false;
        animator.enabled = false;
        foreach (var bone in bones)
        {
            var body = bone.transform.gameObject.AddComponent<Rigidbody>();
            body.mass = bone.mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 8; body.solverVelocityIterations = 4;
            var collider = bone.transform.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = bone.radius;
            collider.height = Mathf.Max(bone.length, bone.radius * 2.1f);
            collider.center = Vector3.up * bone.length * .35f;
            PirateCameraObstacle.IgnoreFor(collider);
            var parent = bone.parent != null ? bone.parent.GetComponent<Rigidbody>() : null;
            if (parent == null) parent = core;
            var joint = bone.transform.gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = parent;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = parent.transform.InverseTransformPoint(body.position);
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit = new SoftJointLimit { limit = -38f };
            joint.highAngularXLimit = new SoftJointLimit { limit = 38f };
            joint.angularYLimit = joint.angularZLimit = new SoftJointLimit { limit = 32f };
            joint.enableCollision = false; joint.enablePreprocessing = false;
            parts.Add(new Part { body = body, collider = collider, joint = joint,
                restRelativeToCore = Quaternion.Inverse(core.rotation) * body.rotation });
        }
        foreach (var part in parts)
        {
            Physics.IgnoreCollision(coreCollider, part.collider, true);
            foreach (var other in parts)
                if (part.collider != other.collider) Physics.IgnoreCollision(part.collider, other.collider, true);
            part.body.linearVelocity = core.linearVelocity;
            part.body.angularVelocity = core.angularVelocity;
            if (pendingImpact.sqrMagnitude > .01f)
                part.body.AddForce(pendingImpact * .25f, ForceMode.VelocityChange);
        }
        pendingImpact = Vector3.zero;
    }

    public void BeginRecovery()
    {
        if (!Active || Recovering) return;
        Recovering = true;
        recoverUntil = Time.time + 0.45f;
    }

    void FixedUpdate()
    {
        if (!Active || !Recovering || core == null) return;
        foreach (var part in parts)
        {
            if (part.body == null || part.body.isKinematic) continue;
            Quaternion target = core.rotation * part.restRelativeToCore;
            Quaternion error = target * Quaternion.Inverse(part.body.rotation);
            error.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (axis.sqrMagnitude < 0.001f || !float.IsFinite(angle)) continue;
            Vector3 torque = axis.normalized * (angle * Mathf.Deg2Rad * 24f) - part.body.angularVelocity * 5f;
            part.body.AddTorque(Vector3.ClampMagnitude(torque, 30f), ForceMode.Acceleration);
        }
        if (Time.time >= recoverUntil) Deactivate();
    }

    public void Deactivate()
    {
        if (!Active) return;
        Active = false;
        Recovering = false;
        foreach (var part in parts)
        {
            part.joint.xMotion = part.joint.yMotion = part.joint.zMotion = ConfigurableJointMotion.Free;
            part.joint.angularXMotion = part.joint.angularYMotion = part.joint.angularZMotion = ConfigurableJointMotion.Free;
            part.joint.connectedBody = null;
            part.collider.enabled = false;
            part.body.linearVelocity = Vector3.zero;
            part.body.angularVelocity = Vector3.zero;
            part.body.isKinematic = true;
            Destroy(part.joint); Destroy(part.collider); Destroy(part.body);
        }
        parts.Clear();
        animator.enabled = true;
        animator.Rebind(); animator.Update(0f);
        if (rig != null) rig.enabled = true;
    }

    void OnDisable() { Deactivate(); }
}
