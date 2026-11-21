using UnityEngine;

/// <summary>
/// Bot for Patata Caliente. With the potato: chases the closest player it may pass it to (aiming a bit ahead of
/// them) and dashes when close. Without it: runs away from the holder, sliding along the fence instead of into it,
/// steering round the crates, and dashes when the holder gets too close. Reaction time and aim depend on its skill.
/// </summary>
[RequireComponent(typeof(PatataPlayer)), DefaultExecutionOrder(-20)]
public sealed class PatataBot : MonoBehaviour
{
    PatataPlayer me; PatataDirector d; System.Random rng; float skill, nextThink, stuckSince, wanderAngle; Vector3 move, lastPos;

    void Start()
    {
        me = GetComponent<PatataPlayer>(); d = me.Director;
        rng = new System.Random(unchecked(d.Seed * 41 + me.Slot * 13));
        skill = me.IsHuman ? .85f : MultiverseSettings.BotSkill(.35f + (float)rng.NextDouble() * .55f);
        wanderAngle = (float)rng.NextDouble() * 360f; lastPos = transform.position;
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || !me.Alive || !d.CanMove) { if (me != null) me.InMove = Vector3.zero; return; }
        if (Time.time >= nextThink) { nextThink = Time.time + Mathf.Lerp(.32f, .1f, skill) + R() * .08f; Think(); }
        // Unstick.
        Vector3 pos = transform.position;
        if (move.sqrMagnitude > .1f && (pos - lastPos).magnitude < .015f) { if (stuckSince <= 0f) stuckSince = Time.time; } else stuckSince = 0f;
        if (stuckSince > 0f && Time.time - stuckSince > .7f) { move = Quaternion.Euler(0, R() < .5f ? 90f : -90f, 0) * move; me.InJump = true; stuckSince = Time.time; }
        lastPos = pos;
        me.InMove = move;
    }

    void Think()
    {
        Vector3 pos = transform.position; Vector3 fp = Flat(pos);
        float R0 = d.Arena.Radius;
        if (d.HolderSlot == me.Slot)
        {
            // Chase.
            PatataPlayer target = null; float bd = float.MaxValue;
            foreach (var o in d.Players)
            {
                if (o == me || !o.Alive) continue;
                float dist = Flat(o.transform.position - pos).magnitude;
                if (dist < bd) { bd = dist; target = o; }
            }
            if (target == null) { move = Vector3.zero; return; }
            Vector3 lead = target.transform.position + Flat(target.Velocity) * Mathf.Lerp(.1f, .35f, skill) * Mathf.Clamp(bd / 4f, .3f, 1.5f);
            Vector3 to = Flat(lead - pos);
            move = to.sqrMagnitude > .01f ? to.normalized : Vector3.zero;
            move = Avoid(pos, move, .6f);
            if (bd < Mathf.Lerp(2.2f, 3.4f, skill) && bd > .9f && me.DashReady >= 1f && R() < Mathf.Lerp(.25f, .7f, skill)) me.InDash = true;
            if (target.transform.position.y > pos.y + 1f && R() < .3f) me.InJump = true;
            return;
        }
        if (d.HolderSlot < 0 || !d.Players[d.HolderSlot].Alive)
        {
            // Nobody holds it (between rounds): drift around the middle.
            wanderAngle += (R() - .5f) * 60f;
            Vector3 aim = Quaternion.Euler(0, wanderAngle, 0) * Vector3.forward * (R0 * .45f);
            Vector3 w = Flat(aim - pos); move = w.magnitude > 1f ? w.normalized * .6f : Vector3.zero;
            return;
        }
        // Flee from the holder.
        var h = d.Players[d.HolderSlot];
        Vector3 away = Flat(pos - h.transform.position); float hd = away.magnitude; away = hd > .01f ? away / hd : Vector3.forward;
        Vector3 flee = away;
        // Near the fence: run along it (pick the side away from the holder) instead of into it.
        float r = fp.magnitude; Vector3 outward = r > .1f ? fp / r : Vector3.forward;
        float edge = Mathf.InverseLerp(R0 - 4f, R0 - .8f, r);
        if (edge > 0f)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            if (Vector3.Dot(tangent, away) < 0f) tangent = -tangent;
            flee = Vector3.Lerp(flee, tangent - outward * .35f, edge).normalized;
        }
        // Other players are worth keeping away from too (the potato may come to them).
        foreach (var o in d.Players)
        {
            if (o == me || o == h || !o.Alive) continue;
            Vector3 dd = Flat(pos - o.transform.position); float m = dd.magnitude;
            if (m < 2.5f && m > .01f) flee += dd / m * (2.5f - m) * .25f;
        }
        float danger = Mathf.InverseLerp(Mathf.Lerp(7f, 11f, skill), 1.5f, hd);
        move = Avoid(pos, flee.normalized, .8f) * Mathf.Lerp(.55f, 1f, danger);
        if (hd < 9f && danger < .1f) move *= .6f;
        // Dash away when it gets close and comes at us.
        Vector3 closing = Flat(h.Velocity);
        if (hd < Mathf.Lerp(2f, 2.8f, skill) && Vector3.Dot(closing, -away) > 1.5f && me.DashReady >= 1f && R() < Mathf.Lerp(.25f, .75f, skill)) me.InDash = true;
        // Use a trampoline that is right under our path to jump over the holder.
        if (hd < 3f && R() < .15f * skill) me.InJump = true;
    }

    Vector3 Avoid(Vector3 pos, Vector3 dir, float weight)
    {
        if (dir.sqrMagnitude < .001f) return dir;
        foreach (var c in d.Arena.Crates)
        {
            Vector3 to = Flat(c - pos); float dist = to.magnitude;
            if (dist > 2.6f || Vector3.Dot(to, dir) <= 0f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized); if (Vector3.Dot(side, dir) < 0f) side = -side;
            dir += side * weight * (2.6f - dist) / 2.6f * 2f;
        }
        return dir.normalized;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
