using UnityEngine;

/// <summary>
/// Bot for Pintura Loca: heads for nearby tiles that are not its colour (rivals' first, then white), grabs paint
/// buckets that are close, and now and then dashes into a rival. Skill sets how far it looks and how well it chooses.
/// </summary>
[RequireComponent(typeof(PartyMover)), DefaultExecutionOrder(-20)]
public sealed class PinturaBot : MonoBehaviour
{
    PartyMover me; PinturaDirector d; System.Random rng; float skill, nextThink, nextPick; Vector3 move, goal; bool hasGoal;

    void Start()
    {
        me = GetComponent<PartyMover>(); d = FindAnyObjectByType<PinturaDirector>();
        rng = new System.Random(unchecked(d.Seed * 37 + me.Slot * 5));
        skill = me.IsHuman ? .85f : MultiverseSettings.BotSkill(.3f + (float)rng.NextDouble() * .6f);
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || !d.CanMove) { if (me != null) me.InMove = Vector3.zero; return; }
        if (Time.time >= nextThink) { nextThink = Time.time + Mathf.Lerp(.3f, .1f, skill); Think(); }
        me.InMove = move;
    }

    void Think()
    {
        Vector3 p = transform.position;
        // Buckets close by are worth a detour.
        foreach (var b in d.Buckets)
        {
            Vector3 to = b.Pos - p; to.y = 0;
            if (to.magnitude < Mathf.Lerp(5f, 11f, skill)) { move = to.normalized; return; }
        }
        // Rival within reach: dash into them sometimes.
        foreach (var o in d.Players)
        {
            if (o == me) continue;
            Vector3 to = o.transform.position - p; to.y = 0;
            if (to.magnitude < 2.4f && me.DashReady >= 1f && R() < .12f * skill) { move = to.normalized; me.InDash = true; return; }
        }
        int here = PinturaDirector.TileAt(p);
        bool reached = !hasGoal || (goal - p).sqrMagnitude < .4f || (here >= 0 && PinturaDirector.TileAt(goal) == here);
        if (reached || Time.time > nextPick)
        {
            nextPick = Time.time + Mathf.Lerp(.8f, 1.8f, R());
            float best = float.MinValue; int pick = -1; float range = Mathf.Lerp(4f, 8f, skill);
            for (int i = 0; i < PinturaDirector.N * PinturaDirector.N; i++)
            {
                if (d.Grid[i] == me.Member + 1) continue;
                Vector3 c = PinturaDirector.TileCenter(i); Vector3 to = c - p; to.y = 0; float dist = to.magnitude;
                if (dist > range || dist < .5f) continue;
                float s = (d.Grid[i] != 0 ? 1.4f : 1f) * 3f - dist * .45f + R() * Mathf.Lerp(2.5f, .6f, skill);
                // prefer keeping direction (paint lines, not zig-zags)
                if (move.sqrMagnitude > .1f) s += Vector3.Dot(to.normalized, move.normalized) * .8f;
                if (s > best) { best = s; pick = i; }
            }
            if (pick < 0) { goal = new Vector3((R() - .5f) * 18f, 0, (R() - .5f) * 18f); }
            else goal = PinturaDirector.TileCenter(pick);
            hasGoal = true;
        }
        Vector3 dir = goal - p; dir.y = 0;
        move = dir.sqrMagnitude > .01f ? dir.normalized : Vector3.zero;
    }
}
