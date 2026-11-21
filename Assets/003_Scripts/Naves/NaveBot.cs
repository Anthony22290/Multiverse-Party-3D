using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Bot for Arma tu Nave: explores the hiding spots (it does not know where the pieces are until it sees them, or
/// until the radar), picks the pieces its ship still needs, carries them home along the NavMesh, drops duplicates
/// and now and then shoves a rival who carries something it wants.
/// </summary>
[RequireComponent(typeof(NavePlayer)), DefaultExecutionOrder(-20)]
public sealed class NaveBot : MonoBehaviour
{
    NavePlayer me; NaveDirector d; System.Random rng;
    float skill, sight, nextLook, nextPath, stuckSince, lastProgress;
    readonly HashSet<NavePiece> seen = new(); readonly HashSet<int> visited = new();
    NavMeshPath path; Vector3[] corners = new Vector3[0]; int corner; Vector3 dest, pathDest = new Vector3(9999, 0, 9999), lastPos;
    int exploreSpot = -1;

    void Start()
    {
        me = GetComponent<NavePlayer>(); d = me.Director; path = new NavMeshPath();
        rng = new System.Random(unchecked(d.Seed * 53 + me.Slot * 11));
        skill = me.IsHuman ? .9f : MultiverseSettings.BotSkill(.3f + (float)rng.NextDouble() * .6f);
        sight = Mathf.Lerp(7f, 12f, skill);
        lastPos = transform.position;
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || d.State != NaveDirector.Phase.Running) { if (me != null) me.InMove = Vector3.zero; return; }
        Vector3 pos = transform.position;
        if (Time.time >= nextLook) { nextLook = Time.time + .25f; Look(pos); }
        var ship = d.Ships[me.Slot];

        // Decide where to go.
        if (me.Carrying != null)
        {
            if (ship.Has[me.Carrying.Type]) { me.InDrop = true; dest = pos; }
            else dest = ship.transform.position + (pos - ship.transform.position).normalized * 2.4f;
        }
        else
        {
            var want = d.Pieces.Where(p => p.Is == NavePiece.State.Free && !ship.Has[p.Type] && (d.Radar || seen.Contains(p)))
                                .OrderBy(p => (p.transform.position - pos).sqrMagnitude).FirstOrDefault();
            if (want != null)
            {
                dest = want.transform.position;
                if (Vector3.Distance(pos, want.transform.position) < NavePlayer.PickRange - .2f) me.InPick = true;
                exploreSpot = -1;
            }
            else
            {
                // A rival carrying something we need? Chase and shove.
                var thief = d.Players.FirstOrDefault(o => o != me && o.Carrying != null && !ship.Has[o.Carrying.Type] && Vector3.Distance(o.transform.position, pos) < 9f);
                if (thief != null && skill > .45f) dest = thief.transform.position;
                else
                {
                    if (exploreSpot < 0 || visited.Contains(exploreSpot)) exploreSpot = NextSpot(pos);
                    dest = exploreSpot >= 0 ? d.Village.Spots[exploreSpot] : Vector3.zero;
                }
            }
        }
        // Mark the spots we walked past.
        for (int i = 0; i < d.Village.Spots.Count; i++) if (!visited.Contains(i) && (d.Village.Spots[i] - pos).sqrMagnitude < 2.6f * 2.6f) visited.Add(i);
        if (visited.Count >= d.Village.Spots.Count) visited.Clear();

        // Shove anyone in front carrying a piece we need.
        foreach (var o in d.Players)
        {
            if (o == me || o.Carrying == null || ship.Has[o.Carrying.Type]) continue;
            Vector3 to = o.transform.position - pos; to.y = 0;
            if (to.magnitude < NavePlayer.ShoveRange - .2f && Vector3.Dot(transform.forward, to.normalized) > .5f && R() < .08f) { me.InShove = true; break; }
        }

        Steer(pos);
    }

    int NextSpot(Vector3 pos)
    {
        int best = -1; float bd = float.MaxValue;
        for (int i = 0; i < d.Village.Spots.Count; i++)
        {
            if (visited.Contains(i)) continue;
            float dist = (d.Village.Spots[i] - pos).magnitude + R() * 8f;   // a little randomness so bots spread out
            if (dist < bd) { bd = dist; best = i; }
        }
        return best;
    }

    void Look(Vector3 pos)
    {
        Vector3 eye = pos + Vector3.up * 1.4f;
        foreach (var p in d.Pieces)
        {
            if (p.Is != NavePiece.State.Free || seen.Contains(p)) continue;
            Vector3 target = p.transform.position + Vector3.up * .8f;
            float dist = Vector3.Distance(eye, target); if (dist > sight) continue;
            if (!Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore) || hit.distance > dist - .6f) seen.Add(p);
        }
    }

    void Steer(Vector3 pos)
    {
        if ((dest - pathDest).sqrMagnitude > 1f || Time.time > nextPath)
        {
            nextPath = Time.time + .8f; pathDest = dest;
            if (NavMesh.CalculatePath(NaveVillage.OnMesh(pos, 2f), NaveVillage.OnMesh(dest, 3f), NavMesh.AllAreas, path) && path.corners.Length > 0) { corners = path.corners; corner = 0; }
            else { corners = new[] { dest }; corner = 0; }
        }
        while (corner < corners.Length - 1 && Flat(corners[corner] - pos).magnitude < .6f) corner++;
        Vector3 to = corners.Length > 0 ? Flat(corners[Mathf.Min(corner, corners.Length - 1)] - pos) : Vector3.zero;
        Vector3 move = to.magnitude > .3f ? to.normalized : Vector3.zero;
        // Unstick: if not moving for a while, sidestep and jump.
        if (move != Vector3.zero && (pos - lastPos).magnitude < .02f) { if (stuckSince <= 0f) stuckSince = Time.time; }
        else stuckSince = 0f;
        if (stuckSince > 0f && Time.time - stuckSince > 1.2f) { move = Quaternion.Euler(0, R() < .5f ? 90f : -90f, 0) * move; me.InJump = true; nextPath = 0f; stuckSince = Time.time - .6f; }
        lastPos = pos;
        me.InMove = move;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
