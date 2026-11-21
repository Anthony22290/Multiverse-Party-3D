using UnityEngine;

/// <summary>
/// Bot for Rey de la Colina (volcano): walks round its terrace to a ramp, climbs it on the centre line, takes the
/// summit; up there it guards the flag and shoves whoever comes close. It throws bombs at rivals 3.5–8 m away
/// (first the flag owner). Weaker bots aim worse, throw less and hesitate more.
/// </summary>
[RequireComponent(typeof(PartyMover)), DefaultExecutionOrder(-20)]
public sealed class ColinaBot : MonoBehaviour
{
    PartyMover me; ColinaDirector d; System.Random rng; float skill, nextThink, nextBomb; int path; bool wasOut = true; Vector3 move; Vector2 wander;

    void Start()
    {
        me = GetComponent<PartyMover>(); d = FindAnyObjectByType<ColinaDirector>();
        rng = new System.Random(unchecked(d.Seed * 29 + me.Slot * 13));
        skill = me.IsHuman ? .85f : MultiverseSettings.BotSkill(.3f + (float)rng.NextDouble() * .6f);
    }
    float R() => (float)rng.NextDouble();
    static float AngleOf(Vector3 p) => Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg;

    int NearestPath(Vector3 p)
    {
        int best = 0; float bd = 999f; float a = AngleOf(p);
        for (int i = 0; i < 3; i++) { float dd = Mathf.Abs(Mathf.DeltaAngle(a, ColinaDirector.PathAngles[i])); if (dd < bd) { bd = dd; best = i; } }
        return best;
    }

    void Update()
    {
        if (me == null) return;
        if (me.Out || !d.CanMove) { me.InMove = Vector3.zero; wasOut = true; return; }
        if (wasOut) { wasOut = false; path = R() < .7f ? NearestPath(transform.position) : rng.Next(3); }
        if (Time.time >= nextThink) { nextThink = Time.time + Mathf.Lerp(.3f, .1f, skill); Think(); }
        me.InMove = move;
        if (Time.time >= nextBomb) { nextBomb = Time.time + Mathf.Lerp(1.1f, .35f, skill); Bomb(); }
    }

    void Think()
    {
        Vector3 p = transform.position; float r = new Vector2(p.x, p.z).magnitude; float a = AngleOf(p);
        Vector3 target;
        if (ColinaDirector.OnSummit(p))
        {
            // Guard the summit: stay near the middle, shove whoever comes close.
            PartyMover rival = null; float rd = 99f;
            foreach (var o in d.Players) { if (o == me || o.Out || !d.AtTop(o)) continue; float dd = Vector3.Distance(o.transform.position, p); if (dd < rd) { rd = dd; rival = o; } }
            if (Time.time > wanderAt) { wanderAt = Time.time + 1.2f; wander = Random.insideUnitCircle * .9f; }
            target = new Vector3(wander.x, p.y, wander.y);
            if (rival != null && rd < 2.6f)
            {
                target = rival.transform.position;
                if (rd < 1.6f && me.DashReady >= 1f && R() < Mathf.Lerp(.25f, .8f, skill)) me.InDash = true;
            }
        }
        else if (p.y > ColinaDirector.MidY - .6f && r > ColinaDirector.MidIn - .2f && r < ColinaDirector.MidOut + .2f)
            target = Climb(p, a, 7.5f, 6.9f, 1f, ColinaDirector.MidY);              // middle terrace
        else if (p.y < .9f && r > ColinaDirector.BaseIn - .2f)
            target = Climb(p, a, 15.6f, 14.4f, 8f, 0f);                             // ground ring
        else
        {
            // On a ramp: keep to its centre line, upwards.
            int k = NearestPath(p); float ang = ColinaDirector.PathAngles[k];
            target = ColinaDirector.Polar(ang, p.y > ColinaDirector.MidY - .2f ? .8f : 7.8f, p.y);
            if (r < 4f && p.y < ColinaDirector.MidY) target = ColinaDirector.Polar(ang, 6.5f, p.y);   // fell on the middle? (rare)
        }
        Vector3 dir = target - p; dir.y = 0;
        float m = dir.magnitude;
        move = m > .15f ? dir / m * (m < .6f ? .4f : 1f) : Vector3.zero;
        // Slight wobble for weaker bots (they sometimes step off a ramp).
        if (R() > skill + .25f) move = Quaternion.Euler(0, (R() - .5f) * 40f, 0) * move;
    }
    float wanderAt;

    /// <summary>Walk round the terrace (radius <paramref name="ringR"/>) to the ramp foot, then straight up it.</summary>
    Vector3 Climb(Vector3 p, float a, float ringR, float footR, float aimR, float y)
    {
        float ang = ColinaDirector.PathAngles[path];
        var foot = ColinaDirector.Polar(ang, footR, y);
        float diff = Mathf.DeltaAngle(a, ang);
        Vector3 flatToFoot = foot - p; flatToFoot.y = 0;
        float r = new Vector2(p.x, p.z).magnitude;
        if (flatToFoot.magnitude < .7f || (Mathf.Abs(diff) < 4f && r < footR + .3f)) return ColinaDirector.Polar(ang, aimR, y);
        if (Mathf.Abs(diff) > 18f) return ColinaDirector.Polar(a + Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff), 30f), ringR, y);
        return foot;
    }

    void Bomb()
    {
        if (d.BombReady(me) < 1f || me.Stunned) return;
        PartyMover best = null; float bs = 99f;
        foreach (var o in d.Players)
        {
            if (o == me || o.Out) continue;
            Vector3 to = o.transform.position - transform.position; float dy = to.y; to.y = 0; float dist = to.magnitude;
            if (dist < 3.5f || dist > 8f || dy > 3.5f || dy < -4f) continue;
            float s = dist - (o.Slot == d.FlagOwner ? 4f : 0f) - (d.AtTop(o) ? 2f : 0f);
            if (s < bs) { bs = s; best = o; }
        }
        if (best == null || R() > Mathf.Lerp(.35f, .85f, skill)) return;
        Vector3 aim = best.transform.position + best.Velocity * .5f - transform.position; aim.y = 0;
        aim = Quaternion.Euler(0, (R() - .5f) * Mathf.Lerp(40f, 6f, skill), 0) * aim;
        transform.rotation = Quaternion.LookRotation(aim.normalized);
        d.TryThrow(me);
    }
}
