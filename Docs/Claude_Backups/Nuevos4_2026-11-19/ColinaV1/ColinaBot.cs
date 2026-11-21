using UnityEngine;

/// <summary>
/// Bot for Rey de la Colina: runs to the crown zone (going round bananas and away from the island's edge), and inside
/// it dashes rivals out; when the zone is about to move it already heads for the middle of the island.
/// </summary>
[RequireComponent(typeof(PartyMover)), DefaultExecutionOrder(-20)]
public sealed class ColinaBot : MonoBehaviour
{
    PartyMover me; ColinaDirector d; System.Random rng; float skill, nextThink; Vector3 move, offset;

    void Start()
    {
        me = GetComponent<PartyMover>(); d = FindAnyObjectByType<ColinaDirector>();
        rng = new System.Random(unchecked(d.Seed * 41 + me.Slot * 3));
        skill = me.IsHuman ? .85f : MultiverseSettings.BotSkill(.3f + (float)rng.NextDouble() * .6f);
        offset = Random.insideUnitSphere * 1.2f; offset.y = 0;
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || me.Out || !d.CanMove) { if (me != null) me.InMove = Vector3.zero; return; }
        if (Time.time >= nextThink) { nextThink = Time.time + Mathf.Lerp(.3f, .1f, skill); Think(); }
        me.InMove = move;
    }

    void Think()
    {
        Vector3 p = transform.position, z = d.ZonePos;
        Vector3 target = z + offset * (d.InZone(me) ? .5f : 1f);
        if (d.ZoneTimeLeft < Mathf.Lerp(.5f, 2f, skill) && !d.InZone(me)) target = Vector3.Lerp(z, Vector3.zero, .5f);
        // Rival in the zone near me: dash him out.
        foreach (var o in d.Players)
        {
            if (o == me || o.Out) continue;
            Vector3 to = o.transform.position - p; to.y = 0;
            if (to.magnitude < 2.6f && d.InZone(o) && me.DashReady >= 1f && R() < Mathf.Lerp(.15f, .5f, skill)) { move = to.normalized; me.InDash = true; return; }
        }
        Vector3 dir = target - p; dir.y = 0;
        if (dir.magnitude < .4f) { move = Vector3.zero; return; }
        dir.Normalize();
        // Avoid bananas ahead.
        foreach (var b in d.Bananas)
        {
            Vector3 to = b.Pos - p; to.y = 0; float dist = to.magnitude;
            if (dist > 2.5f || Vector3.Dot(to, dir) <= 0f || R() > Mathf.Lerp(.4f, .95f, skill)) continue;
            Vector3 side = Vector3.Cross(Vector3.up, to.normalized); if (Vector3.Dot(side, dir) < 0f) side = -side;
            dir += side * (2.5f - dist);
        }
        // Keep off the edge.
        Vector3 flat = new Vector3(p.x, 0, p.z); float r = flat.magnitude;
        if (r > ColinaDirector.IslandRadius - 2.5f) dir += -flat / r * (r - (ColinaDirector.IslandRadius - 2.5f)) * 1.5f;
        move = dir.normalized;
        if (me.Grounded && Physics.Raycast(p + Vector3.up * .5f + move * .7f, move, .6f)) me.InJump = R() < .5f;
    }
}
