using UnityEngine;

public sealed class GrabbableProp : MonoBehaviour
{
    private Rigidbody body;
    private Vector3 spawn;
    private Quaternion rotation;
    private PirateFighter thrower;
    private float dangerousUntil, nextImpact, nextThud;
    public PirateFighter Holder { get; set; }
    public void Initialize()
    {
        body = GetComponent<Rigidbody>(); spawn = body.position; rotation = body.rotation;
    }
    public void MarkThrown(PirateFighter fighter) { thrower = fighter; dangerousUntil = Time.time + 2f; }
    public void ResetProp()
    {
        if (Holder != null) Holder.ReleaseGrab(false);
        body.isKinematic = false;
        body.position = spawn; body.rotation = rotation;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
        thrower = null; dangerousUntil = nextImpact = 0f;
    }
    public bool NetworkDriven { get; set; }
    public bool IsProxy { get; set; }
    private void FixedUpdate() { if (!NetworkDriven) Tick(); }
    public void Tick()
    {
        if (body != null && !body.isKinematic && body.position.y < -12f)
        {
            if (Holder != null) Holder.ReleaseGrab(false);
            body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        float hit = collision.relativeVelocity.magnitude;
        if (!IsProxy && hit > 3.2f && Time.time >= nextThud && collision.contactCount > 0)
        {
            nextThud = Time.time + .18f;
            PirateArenaAudio.Play(PirateArenaAudio.Sound.WoodHit, collision.GetContact(0).point, Mathf.Clamp(hit / 9f, .35f, 1f));
        }
        if (IsProxy || Time.time > dangerousUntil || Time.time < nextImpact || collision.relativeVelocity.magnitude < 4f) return;
        PirateFighter fighter = collision.rigidbody != null ? collision.rigidbody.GetComponentInParent<PirateFighter>() : null;
        if (fighter == null || fighter == thrower || !fighter.IsAlive) return;
        Vector3 away = Vector3.ProjectOnPlane(fighter.transform.position - transform.position, Vector3.up).normalized;
        fighter.TakePush(away * Mathf.Clamp(collision.relativeVelocity.magnitude * 0.35f, 1f, 4f) + Vector3.up);
        nextImpact = Time.time + 0.5f;
    }
}
