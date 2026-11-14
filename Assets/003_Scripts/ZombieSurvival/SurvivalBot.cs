using UnityEngine;

/// <summary>
/// Teammate brain. Priorities: install a carried part → fetch a part lying on the ground (closest free teammate) →
/// revive a downed teammate → fight zombies near the team or the truck → stay close to the player.
/// During the escape everyone runs to the truck. Steering: obstacle avoidance, crowd separation, stuck recovery.
/// </summary>
public sealed class SurvivalBot : MonoBehaviour
{
    SurvivalPlayer me;
    Vector3 lastPos, sidestep;
    float stuckTimer, sidestepUntil, thinkAt, aimJitter;
    SurvivalZombie foe;
    static readonly float[] Angles = { 35f, -35f, 70f, -70f, 110f, -110f };

    void Start()
    {
        me = GetComponent<SurvivalPlayer>();
        lastPos = transform.position;
        aimJitter = Random.Range(-.6f, .6f);
    }

    void Update()
    {
        if (me == null || me.Director == null) return;
        me.BotMove = Vector3.zero; me.BotUse = false;
        var d = me.Director;
        if (!d.Fighting || me.Downed) return;
        Vector3 pos = transform.position;
        var level = d.Level;

        // 0. Get out of a boss's slam circle or charge lane.
        if (Dodge(pos)) return;

        // 1. A part in hand goes straight into the truck.
        if (me.Carried != null)
        {
            if (level.InTruckZone(pos) && Flat(pos - level.TruckPos) < SurvivalLevel.ZoneRadius - .6f) { me.BotUse = true; DefendSelf(1.6f); return; }
            Go(level.TruckPos + (pos - level.TruckPos).normalized * 3f, .4f);
            return;
        }

        // 2. Escape: everyone to the truck, swinging at whatever gets close.
        if (d.State == SurvivalDirector.Phase.Escape)
        {
            Vector3 seat = level.SpawnPoint(me.Slot);
            if (Flat(pos - seat) > .8f) Go(seat, .5f);
            DefendSelf(2.2f);
            return;
        }

        // 3. Fetch the loose part if I'm the closest free teammate (the human gets a head start).
        var part = d.LoosePart;
        if (part != null && part.OnGround && IsFetcher(part)) { Go(part.GroundPosition, .2f); DefendSelf(1.4f); return; }

        // 4. Revive whoever is down nearby.
        SurvivalPlayer down = null; float best = 22f;
        foreach (var p in d.Team) if (p != me && p.Downed) { float m = Flat(p.transform.position - pos); if (m < best && ClosestHelper(p)) { best = m; down = p; } }
        if (down != null)
        {
            if (best < 1.8f) { me.BotUse = true; DefendSelf(1.5f); return; }
            Go(down.transform.position, 1.2f); DefendSelf(1.6f);
            return;
        }

        // 4b. Guarded cache (2026-11-10): clear the guardians, then stand by the chest until it opens.
        var cache = d.ActiveCache;
        bool cacheTarget = false;
        if (cache != null && cache.Armed && !cache.Opened && d.LoosePart == null)
        {
            var guard = cache.NearestGuard(pos);
            if (guard == null) { Go(cache.Position, 1f); DefendSelf(1.8f); return; }
            var close = PickFoe();
            foe = close != null && Flat(close.transform.position - pos) < 6f ? close : guard; cacheTarget = true; thinkAt = Time.time + .3f;
        }

        // 5. Fight: pick a target near me, the team or the truck.
        if (!cacheTarget && (Time.time > thinkAt || foe == null || foe.Dead))
        {
            thinkAt = Time.time + .3f;
            foe = PickFoe();
        }
        if (foe != null)
        {
            Vector3 to = foe.transform.position - pos; to.y = 0f;
            float m = to.magnitude - foe.Radius;
            if (m < SurvivalPlayer.SwingRange - .35f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 18f);
                if (Vector3.Angle(transform.forward, to) < 40f) me.BotAttack = true;
                // Back off a little when hurt and surrounded.
                if (me.Health < 30f && CountNear(3f) >= 3) me.BotMove = -to.normalized * .8f;
                return;
            }
            Go(foe.transform.position - to.normalized * (foe.Radius + 1.3f), .3f);
            return;
        }

        // 6. Nothing to do: stay near the player (or the truck).
        var anchor = d.Human != null && !d.Human.Downed && d.Human != me ? d.Human.transform.position : level.TruckPos;
        float ang = (me.Slot * 120f + 30f) * Mathf.Deg2Rad;
        Vector3 spot = anchor + new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * 3f;
        if (Flat(spot - pos) > 1.5f) Go(spot, 1f);
    }

    bool Dodge(Vector3 pos)
    {
        foreach (var z in SurvivalZombie.All)
        {
            if (!z.IsBoss || z.Dead) continue;
            Vector3 off = pos - z.transform.position; off.y = 0f;
            if (z.Slamming && off.magnitude < SurvivalZombie.SlamRadius + .8f)
            {
                me.BotMove = off.sqrMagnitude > .01f ? off.normalized : Vector3.right;
                return true;
            }
            if (z.Charging)
            {
                Vector3 dir = z.ChargeDirection;
                float ahead = Vector3.Dot(off, dir);
                Vector3 side = off - dir * ahead;
                if (ahead > -1f && ahead < 18f && side.magnitude < z.Radius + 1.3f)
                {
                    me.BotMove = side.sqrMagnitude > .01f ? side.normalized : Vector3.Cross(Vector3.up, dir);
                    return true;
                }
            }
        }
        return false;
    }

    bool IsFetcher(SurvivalPart part)
    {
        var d = me.Director;
        Vector3 at = part.GroundPosition;
        float mine = Flat(at - transform.position);
        var human = d.Human;
        if (human != null && human != me && !human.Downed && !d.AutoHuman && human.Carried == null)
        {
            // Give the player the first few seconds if they're close enough to want it.
            float hd = Flat(at - human.transform.position);
            if (hd < mine + 4f && Time.time - part.DroppedAt < 6f) return false;
        }
        foreach (var p in d.Team)
        {
            if (p == me || p.Downed || p.Carried != null) continue;
            if (p.IsHuman && !p.DrivenByBot) continue;
            float o = Flat(at - p.transform.position);
            if (o < mine - .01f || (Mathf.Abs(o - mine) < .01f && p.Slot < me.Slot)) return false;
        }
        return true;
    }

    bool ClosestHelper(SurvivalPlayer downed)
    {
        float mine = Flat(downed.transform.position - transform.position);
        foreach (var p in me.Director.Team)
        {
            if (p == me || p == downed || p.Downed || p.Carried != null || (p.IsHuman && !p.DrivenByBot)) continue;
            if (Flat(downed.transform.position - p.transform.position) < mine - .5f) return false;
        }
        return true;
    }

    SurvivalZombie PickFoe()
    {
        var d = me.Director;
        Vector3 pos = transform.position;
        SurvivalZombie pick = null; float bestScore = float.MaxValue;
        foreach (var z in SurvivalZombie.All)
        {
            if (z.Dead || !d.Level.Inside(z.transform.position, -.5f)) continue;
            float mine = Flat(z.transform.position - pos);
            float toTeam = mine;
            foreach (var p in d.Team) if (!p.Downed) toTeam = Mathf.Min(toTeam, Flat(z.transform.position - p.transform.position));
            float score = mine * .7f + toTeam * .5f + (z.PartIndex >= 0 ? -6f : 0f) + aimJitter;
            if (score < bestScore) { bestScore = score; pick = z; }
        }
        return pick;
    }

    void DefendSelf(float range)
    {
        foreach (var z in SurvivalZombie.All)
        {
            Vector3 to = z.transform.position - transform.position; to.y = 0f;
            if (to.magnitude - z.Radius > range) continue;
            if (me.Carried == null)
            {
                if (to.sqrMagnitude > .01f && me.BotMove.sqrMagnitude < .01f) transform.rotation = Quaternion.LookRotation(to);
                me.BotAttack = true;
            }
            return;
        }
    }

    int CountNear(float r)
    {
        int n = 0;
        foreach (var z in SurvivalZombie.All) if (Flat(z.transform.position - transform.position) < r + z.Radius) n++;
        return n;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // ------------------------------------------------------------------ steering

    void Go(Vector3 target, float arrive)
    {
        Vector3 pos = transform.position;
        Vector3 to = target - pos; to.y = 0f;
        float dist = to.magnitude;
        if (dist <= arrive) { stuckTimer = 0f; lastPos = pos; return; }
        Vector3 dir = Avoid(to / dist) + Separation() * .9f;
        if (Time.time < sidestepUntil) dir = sidestep;
        if (dir.sqrMagnitude > .0001f) me.BotMove = dir.normalized * Mathf.Clamp01(dist / .8f + .3f);
        if (me.Grounded && LowObstacle(dir)) me.BotJump = true;

        stuckTimer += Time.deltaTime;
        if (stuckTimer > 1.2f)
        {
            if ((pos - lastPos).sqrMagnitude < .1f && dist > 1f)
            {
                sidestep = Quaternion.Euler(0, Random.value < .5f ? 80f : -80f, 0) * to.normalized;
                sidestepUntil = Time.time + .6f;
                me.BotJump = true;
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
        foreach (var h in Physics.SphereCastAll(origin, .3f, dir, 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponent<CharacterController>() != null) continue;
            if (h.distance <= 0f && h.point == Vector3.zero) continue;
            if (h.collider.bounds.max.y < transform.position.y + .62f) continue;
            return true;
        }
        return false;
    }

    bool LowObstacle(Vector3 dir)
    {
        Vector3 p = transform.position;
        if (!Physics.Raycast(p + Vector3.up * .22f, dir, out var low, .75f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (low.collider.GetComponent<CharacterController>() != null) return false;
        if (low.collider.bounds.max.y > p.y + .65f) return false;
        return !Physics.Raycast(p + Vector3.up * 1.3f, dir, .95f, ~0, QueryTriggerInteraction.Ignore);
    }

    Vector3 Separation()
    {
        Vector3 s = Vector3.zero;
        foreach (var other in me.Director.Team)
        {
            if (other == me) continue;
            Vector3 d = transform.position - other.transform.position; d.y = 0f;
            float m = d.magnitude;
            if (m < 1f && m > .001f) s += d / m * (1f - m);
        }
        return s;
    }
}
