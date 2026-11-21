using UnityEngine;

/// <summary>
/// Bot for Suelo que se Cae: keeps walking (standing still is just as deadly) towards a solid tile of its floor that
/// has many solid neighbours and nobody close, jumping over small holes. Weaker bots look less far and misstep.
/// </summary>
[RequireComponent(typeof(PartyMover)), DefaultExecutionOrder(-20)]
public sealed class SueloBot : MonoBehaviour
{
    PartyMover me; SueloDirector d; System.Random rng; float skill, nextThink; Vector3 move, goal; bool hasGoal; SueloDirector.Tile goalTile;

    void Start()
    {
        me = GetComponent<PartyMover>(); d = FindAnyObjectByType<SueloDirector>();
        rng = new System.Random(unchecked(d.Seed * 23 + me.Slot * 7));
        skill = me.IsHuman ? .85f : MultiverseSettings.BotSkill(.3f + (float)rng.NextDouble() * .6f);
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || me.Out || !d.CanMove) { if (me != null) me.InMove = Vector3.zero; return; }
        if (Time.time >= nextThink) { nextThink = Time.time + Mathf.Lerp(.35f, .12f, skill); Think(); }
        me.InMove = move;
    }

    void Think()
    {
        Vector3 p = transform.position; int floor = d.FloorOf(p.y);
        if (floor >= SueloDirector.FloorY.Length) { move = Vector3.zero; return; }
        float range = Mathf.Lerp(3.5f, 7f, skill);
        // Keep the current goal while it is solid and not reached.
        Vector3 flatTo = goal - p; flatTo.y = 0f;
        bool keep = hasGoal && goalTile != null && goalTile.Floor == floor && flatTo.sqrMagnitude > .5f && GoalSolid();
        if (!keep)
        {
            SueloDirector.Tile best = null; float bs = float.MinValue;
            for (float wide = 1f; wide <= 4f && best == null; wide *= 4f)   // nothing close: look across the whole floor
            foreach (var t in d.Tiles)
            {
                if (t.Floor != floor || !d.Solid(t)) continue;
                Vector3 to = t.Pos - p; to.y = 0; float dist = to.magnitude;
                if (dist < 1.2f || dist > range * wide) continue;
                int around = 0;
                foreach (var o in t.Near) if (d.Solid(o)) around++;
                float crowd = 0f; foreach (var o in d.Players) if (o != me && !o.Out) { float od = Vector3.Distance(o.transform.position, t.Pos); if (od < 3f) crowd += 3f - od; }
                float centre = new Vector2(t.Pos.x, t.Pos.z).magnitude;
                float s = around * 1.2f - dist * .25f - crowd * 1.5f - centre * .08f + R() * Mathf.Lerp(4f, .8f, skill);
                if (s > bs) { bs = s; best = t; }
            }
            if (best == null) { move = transform.forward * .6f; hasGoal = false; return; }
            goal = best.Pos; goalTile = best; hasGoal = true;
        }
        Vector3 dir = goal - p; dir.y = 0; dir = dir.normalized;
        // Walking slowly wastes less floor: better bots stroll, unless there is a hole to jump.
        move = dir * Mathf.Lerp(.95f, .62f, skill);
        // Hole right ahead but solid ground beyond: jump.
        var ahead = d.TileUnder(p + dir * .9f + Vector3.up * .1f);
        if ((ahead == null || !d.Solid(ahead)) && GoalSolid() && me.Grounded && R() < Mathf.Lerp(.4f, .95f, skill)) { me.InJump = true; move = dir; }
    }

    bool GoalSolid()
    {
        return d.Solid(goalTile);
    }
}
