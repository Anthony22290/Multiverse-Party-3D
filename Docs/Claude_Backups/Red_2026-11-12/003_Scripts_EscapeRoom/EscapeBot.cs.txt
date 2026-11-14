using UnityEngine;

/// <summary>
/// Teammate brain: asks the director (and through it the active room) for an order, then steers there with
/// simple obstacle avoidance, crowd separation, step jumping and stuck recovery. No NavMesh needed: rooms are open.
/// </summary>
public sealed class EscapeBot : MonoBehaviour
{
    EscapeActor actor;
    Vector3 lastPos, sidestep;
    float stuckTimer, sidestepUntil;
    static readonly float[] Angles = { 35f, -35f, 70f, -70f, 110f, -110f };

    void Start()
    {
        actor = GetComponent<EscapeActor>();
        lastPos = transform.position;
    }

    void Update()
    {
        if (actor == null || actor.Director == null) return;
        actor.BotMove = Vector3.zero;
        actor.BotUse = null;
        var d = actor.Director;
        if (!d.Running || actor.Stunned) return;
        Steer(d.OrderFor(actor));
    }

    void Steer(EscapeOrder o)
    {
        Vector3 pos = transform.position;
        Vector3 to = o.target - pos; to.y = 0f;
        float dist = to.magnitude;

        if (o.use != null && o.use.CanUse(actor) && o.use.InRange(actor) && dist <= Mathf.Max(o.arrive, o.use.range * .8f))
        {
            actor.BotUse = o.use;
            stuckTimer = 0f; lastPos = pos;
            return;
        }
        if (dist <= o.arrive)
        {
            stuckTimer = 0f; lastPos = pos;
            return;
        }

        Vector3 dir = to / dist;
        if (!o.precise)
        {
            dir = Avoid(dir);
            dir += Separation() * .9f;
        }
        if (Time.time < sidestepUntil) dir = sidestep;
        float scale = o.precise ? Mathf.Clamp01(dist / .9f) * .8f + .2f : 1f;
        if (dir.sqrMagnitude > .0001f) actor.BotMove = dir.normalized * scale;

        if (actor.Grounded && LowObstacle(dir)) actor.BotJump = true;

        stuckTimer += Time.deltaTime;
        if (stuckTimer > 1.4f)
        {
            if ((pos - lastPos).sqrMagnitude < .1f && dist > 1f)
            {
                sidestep = Quaternion.Euler(0, Random.value < .5f ? 80f : -80f, 0) * dir;
                sidestepUntil = Time.time + .6f;
                actor.BotJump = true;
            }
            lastPos = pos; stuckTimer = 0f;
        }
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
        Vector3 origin = transform.position + Vector3.up * .95f;
        foreach (var h in Physics.SphereCastAll(origin, .3f, dir, 1.1f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<EscapeActor>() != null) continue;
            if (h.rigidbody != null && !h.rigidbody.isKinematic) continue;
            if (h.distance <= 0f && h.point == Vector3.zero) continue; // started inside: let the controller sort it out
            if (h.collider.bounds.max.y < transform.position.y + .62f) continue; // low step: jump it instead
            return true;
        }
        return false;
    }

    bool LowObstacle(Vector3 dir)
    {
        Vector3 p = transform.position;
        if (!Physics.Raycast(p + Vector3.up * .22f, dir, out var low, .75f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (low.collider.GetComponentInParent<EscapeActor>() != null) return false;
        if (low.rigidbody != null && !low.rigidbody.isKinematic) return false;
        if (low.collider.bounds.max.y > p.y + .65f) return false;
        return !Physics.Raycast(p + Vector3.up * 1.3f, dir, .95f, ~0, QueryTriggerInteraction.Ignore);
    }

    Vector3 Separation()
    {
        Vector3 s = Vector3.zero;
        foreach (var other in actor.Director.Actors)
        {
            if (other == actor) continue;
            Vector3 d = transform.position - other.transform.position; d.y = 0f;
            float m = d.magnitude;
            if (m < .95f && m > .001f) s += d / m * (.95f - m);
        }
        return s;
    }
}
