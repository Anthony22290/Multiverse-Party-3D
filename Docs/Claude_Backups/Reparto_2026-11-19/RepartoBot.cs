using UnityEngine;

/// <summary>
/// Bot for Reparto Loco, Frogger-style: it waits on the sidewalk or a median until it can cross the WHOLE next road
/// without any vehicle covering its spot while it is in each lane (plus a safety margin that depends on its skill),
/// slides sideways to look for a better gap, then runs straight across. If it misjudged, it dashes or steps back.
/// </summary>
[RequireComponent(typeof(RepartoPlayer)), DefaultExecutionOrder(-20)]
public sealed class RepartoBot : MonoBehaviour
{
    RepartoPlayer me; RepartoDirector d; System.Random rng; float skill, nextThink, slide = 1f, waitSince; Vector3 move; bool crossing;

    void Start()
    {
        me = GetComponent<RepartoPlayer>(); d = me.Director;
        rng = new System.Random(unchecked(d.Seed * 29 + me.Slot * 11));
        skill = me.IsHuman ? .8f : .3f + (float)rng.NextDouble() * .6f;
        if (rng.NextDouble() < .5) slide = -1f;
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || !d.CanMove || me.Stunned) { if (me != null) me.InMove = Vector3.zero; crossing = false; return; }
        if (Time.time >= nextThink) { nextThink = Time.time + Mathf.Lerp(.12f, .04f, skill); Think(); }
        me.InMove = move;
    }

    void Think()
    {
        Vector3 p = transform.position; float t = d.TrafficTime; var road = d.Road; float v = RepartoPlayer.Speed * .92f;
        int dir = me.Carry != 0 ? me.Carry : (p.z < 14f ? -1 : 1);
        int on = RepartoRoad.RoadOf(p.z);
        if (on >= 0)
        {
            // In the middle of a road: keep going, but react if the lane I am in is about to be hit.
            int lane = RepartoRoad.LaneOf(p.z);
            move = new Vector3(0, 0, dir);
            if (lane >= 0 && road.ClearFor(lane, p.x, t, .45f, .7f) < Mathf.Lerp(.25f, .45f, skill))
            {
                int next = lane + dir; bool fwdOk = next < 0 || next >= RepartoRoad.LaneZ.Length || road.ClearFor(next, p.x, t, .7f, .7f) >= .7f;
                if (!fwdOk && R() < skill) move = new Vector3(0, 0, -dir);
                if (me.DashReady >= 1f && R() < .6f * skill) me.InDash = true;
            }
            return;
        }
        // On a safe strip: find the next road in my direction.
        int target = -1;
        for (int r = 0; r < RepartoRoad.Roads.Length; r++)
        {
            var rd = RepartoRoad.Roads[r];
            if (dir > 0 ? rd.a > p.z - .1f : rd.b < p.z + .1f) { if (target < 0 || (dir > 0 ? rd.a < RepartoRoad.Roads[target].a : rd.b > RepartoRoad.Roads[target].b)) target = r; }
        }
        if (target < 0) { move = new Vector3(0, 0, dir); return; }   // just the sidewalk left
        var road0 = RepartoRoad.Roads[target];
        float edge = dir > 0 ? road0.a - .45f : road0.b + .45f;
        float toEdge = (edge - p.z) * dir;
        if (toEdge > 1.6f) { move = new Vector3(0, 0, dir); return; }   // walk up to the kerb first
        float margin = Mathf.Lerp(.5f, .18f, skill);
        // Judge from the kerb (where I will start), counting the time to get there.
        float startZ = dir > 0 ? Mathf.Max(p.z, edge) : Mathf.Min(p.z, edge);
        if (CanCross(target, p.x, startZ, dir, t + Mathf.Max(0f, toEdge) / v, v, margin))
        {
            move = new Vector3(0, 0, dir); waitSince = 0f;
            if (R() < .25f * skill && me.DashReady >= 1f && (road0.b - road0.a) > 8f) me.InDash = true;
            return;
        }
        // Wait; try a spot a little to the side if it opens sooner.
        if (waitSince <= 0f) waitSince = Time.time;
        float alt = p.x + slide * 2.2f;
        if (Mathf.Abs(alt) > RepartoRoad.HalfWidth - 1.2f) { slide = -slide; alt = p.x + slide * 2.2f; }
        bool altOk = CanCross(target, alt, p.z, dir, t + .35f, v, margin);
        // Stop at the kerb (never drift onto the road while waiting).
        move = new Vector3(altOk || Time.time - waitSince > 2.5f ? slide * .9f : 0f, 0, dir * Mathf.Clamp(toEdge * 1.5f, -.6f, .6f));
        if (Time.time - waitSince > 4f) { slide = -slide; waitSince = Time.time; }
    }

    bool CanCross(int r, float x, float z, int dir, float t, float v, float margin)
    {
        var road = d.Road;
        for (int i = 0; i < RepartoRoad.LaneZ.Length; i++)
        {
            float lz = RepartoRoad.LaneZ[i];
            if (RepartoRoad.RoadOf(lz) != r) continue;
            float dist = Mathf.Abs(lz - z);
            float enter = (dist - RepartoRoad.LaneHalf) / v, exit = (dist + RepartoRoad.LaneHalf) / v;
            if (!road.ClearBetween(i, x, t + Mathf.Max(0f, enter - margin), t + exit + margin, .75f)) return false;
        }
        return true;
    }
}
