using UnityEngine;

/// <summary>
/// Bot for Gallinas Locas. Picks a chicken (golden ones first, then close ones near its own pen), runs it down,
/// grabs it and carries it to its pen's opening, then drops it in. If a trap starts ticking in its pen it runs to
/// grab it and throws it out. Now and then it dashes into a rival carrying a chicken. Skill sets reaction and aim.
/// </summary>
[RequireComponent(typeof(GranjaPlayer)), DefaultExecutionOrder(-20)]
public sealed class GranjaBot : MonoBehaviour
{
    GranjaPlayer me; GranjaDirector d; System.Random rng; float skill, nextThink, stuckSince, nextGrabTry; Vector3 move, lastPos;
    GranjaChicken target; int targetId = -1;

    void Start()
    {
        me = GetComponent<GranjaPlayer>(); d = me.Director;
        rng = new System.Random(unchecked(d.Seed * 31 + me.Slot * 17));
        skill = me.IsHuman ? .85f : MultiverseSettings.BotSkill(.35f + (float)rng.NextDouble() * .55f);
        lastPos = transform.position;
    }
    float R() => (float)rng.NextDouble();
    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    void Update()
    {
        if (me == null || !d.CanMove) { if (me != null) me.InMove = Vector3.zero; return; }
        if (Time.time >= nextThink) { nextThink = Time.time + Mathf.Lerp(.3f, .1f, skill) + R() * .06f; Think(); }
        Vector3 pos = transform.position;
        if (move.sqrMagnitude > .1f && (pos - lastPos).magnitude < .012f) { if (stuckSince <= 0f) stuckSince = Time.time; } else stuckSince = 0f;
        if (stuckSince > 0f && Time.time - stuckSince > .6f) { move = Quaternion.Euler(0, R() < .5f ? 80f : -80f, 0) * move; me.InJump = R() < .3f; stuckSince = Time.time; }
        lastPos = pos;
        me.InMove = move;
    }

    void Think()
    {
        Vector3 pos = transform.position;
        var held = d.CarriedBy(me.Slot);
        // 1) Carrying: a ticking trap goes far from our pen; anything else goes into our pen.
        if (held != null)
        {
            if (held.Mode == GranjaChicken.State.Carried && held.Pen >= 0)
            {
                Vector3 away = Flat(pos - d.Arena.PenCenter[me.Pen]);
                Go(pos + away.normalized * 3f);
                if (away.magnitude > 4.8f) Grab();
                return;
            }
            Vector3 mouth = d.Arena.PenMouth(me.Pen, .9f);
            Vector3 to = Flat(mouth - pos);
            if (to.magnitude > .7f) { Go(mouth); return; }
            // At the opening: face the pen and drop.
            move = Flat(d.Arena.PenCenter[me.Pen] - pos).normalized * .25f;
            if (Vector3.Angle(transform.forward, Flat(d.Arena.PenCenter[me.Pen] - pos)) < 35f) Grab();
            return;
        }
        // 2) Trap ticking in our pen: rush to it.
        foreach (var c in d.Chickens)
            if (c.Mode == GranjaChicken.State.Ticking && c.Pen == me.Pen && R() < Mathf.Lerp(.5f, .95f, skill))
            {
                Vector3 to = Flat(c.transform.position - pos);
                if (to.magnitude < GranjaDirector.GrabRange * .85f) Grab();
                Go(c.transform.position); return;
            }
        // 3) Bump a rival carrying a chicken that is close.
        foreach (var o in d.Players)
        {
            if (o == me || d.CarriedBy(o.Slot) == null) continue;
            float dist = Flat(o.transform.position - pos).magnitude;
            if (dist < 4f && R() < .35f * skill)
            {
                Go(o.transform.position + Flat(o.Velocity) * .2f);
                if (dist < 2.6f && me.DashReady >= 1f) me.InDash = true;
                return;
            }
        }
        // 4) Choose a chicken.
        if (target == null || target.Mode != GranjaChicken.State.Free || R() < .08f) Pick();
        if (target == null) { Go(Vector3.zero); return; }
        Vector3 tp = target.transform.position + Flat(target.Vel) * Mathf.Lerp(.05f, .3f, skill);
        float td = Flat(target.transform.position - pos).magnitude;
        if (td < GranjaDirector.GrabRange * .8f) { Grab(); move = Vector3.zero; return; }
        Go(tp);
        if (td < 2.6f && td > 1.4f && me.DashReady >= 1f && R() < .15f * skill) me.InDash = true;
    }

    void Pick()
    {
        Vector3 pos = transform.position; Vector3 home = d.Arena.PenCenter[me.Pen];
        GranjaChicken best = null; float bs = float.MaxValue;
        foreach (var c in d.Chickens)
        {
            if (c.Mode != GranjaChicken.State.Free) continue;
            float s = Flat(c.transform.position - pos).magnitude + Flat(c.transform.position - home).magnitude * .45f;
            if (c.Type == GranjaChicken.Kind.Gold) s -= 9f;
            // Others are chasing it already? a little less interesting.
            foreach (var o in d.Players) if (o != me && Flat(o.transform.position - c.transform.position).magnitude < 2f) s += 3f;
            s += R() * Mathf.Lerp(6f, 1.5f, skill);
            if (s < bs) { bs = s; best = c; }
        }
        target = best;
    }

    void Grab() { if (Time.time < nextGrabTry) return; nextGrabTry = Time.time + .35f; me.InGrab = true; }

    void Go(Vector3 at)
    {
        Vector3 pos = transform.position; Vector3 dir = Flat(at - pos);
        if (dir.sqrMagnitude < .04f) { move = Vector3.zero; return; }
        dir.Normalize();
        // Steer round obstacles and pen fences.
        foreach (var o in d.Arena.Obstacles)
        {
            Vector3 to = Flat(o - pos); float dist = to.magnitude; float r = (o.sqrMagnitude < 1f ? 3.2f : 1.4f);
            if (dist > r + 1.5f || Vector3.Dot(to, dir) <= 0f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized); if (Vector3.Dot(side, dir) < 0f) side = -side;
            dir += side * (r + 1.5f - dist) / 1.5f;
        }
        for (int i = 0; i < d.Arena.Pens; i++)
        {
            if (d.Arena.Inside(i, at, -.5f)) continue;                       // going into this pen: fine
            Vector3 to = Flat(d.Arena.PenCenter[i] - pos); float dist = to.magnitude;
            if (dist > 5.5f || Vector3.Dot(to, dir) <= 0f) continue;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized); if (Vector3.Dot(side, dir) < 0f) side = -side;
            dir += side * (5.5f - dist) / 2f;
        }
        move = dir.normalized;
    }
}
