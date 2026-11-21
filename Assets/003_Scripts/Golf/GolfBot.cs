using UnityEngine;

/// <summary>
/// Bot for Minigolf Loco: looks along the hole's path for the farthest point it can roll to in a straight line (the
/// cup if it is in sight), works out the speed from the distance, the slope and the grass, and putts with a little
/// error that depends on its skill.
/// </summary>
[RequireComponent(typeof(GolfPlayer)), DefaultExecutionOrder(-20)]
public sealed class GolfBot : MonoBehaviour
{
    public static float SpeedFactor = 1f;   // tuning hook for the tests
    GolfPlayer me; GolfDirector d; System.Random rng; float skill, thinkUntil = -1f;

    void Start()
    {
        me = GetComponent<GolfPlayer>(); d = me.Director;
        rng = new System.Random(unchecked(d.Seed * 37 + me.Slot * 101));
        skill = me.IsHuman ? .85f : MultiverseSettings.BotSkill(.35f + (float)rng.NextDouble() * .55f);
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || d.State != GolfDirector.Phase.Playing || me.State != GolfPlayer.Phase.Ready) { thinkUntil = -1f; return; }
        if (thinkUntil < 0f) { thinkUntil = Time.time + Mathf.Lerp(1.6f, .7f, skill) + R() * .6f; return; }
        if (Time.time < thinkUntil) return;
        thinkUntil = -1f;
        Plan(out float yaw, out float power);
        me.BotAim = yaw; me.BotPower = power;
    }

    bool Clear(Vector3 from, Vector3 to, GolfCourse.Hole h)
    {
        Vector3 a = from + Vector3.up * .02f, b = new Vector3(to.x, a.y, to.z);
        Vector3 dir = b - a; float dist = dir.magnitude; if (dist < .05f) return true;
        if (Physics.SphereCast(a, GolfPlayer.BallRadius * .9f, dir / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody != me.Body && hit.collider.GetComponentInParent<GolfPlayer>() == null)
        {
            // Other balls never block; anything else does.
            if (hit.collider.attachedRigidbody == null || hit.collider.attachedRigidbody.isKinematic) return false;
        }
        for (float t = .5f; t < dist; t += .5f) if (GolfCourse.IsWater(h, a + dir / dist * t)) return false;
        return true;
    }

    void Plan(out float yaw, out float power)
    {
        var h = d.CurrentHole; Vector3 pos = me.Ball.position;
        // Closest point of the path to the ball.
        int near = 0; float nd = float.MaxValue;
        for (int i = 0; i < h.Path.Count; i++) { float dd = Flat(h.Path[i] - pos).sqrMagnitude; if (dd < nd) { nd = dd; near = i; } }
        Vector3 target; bool cup = false;
        if (Clear(pos, h.Cup, h) && Mathf.Abs(h.CupY + GolfPlayer.BallRadius - pos.y) < .3f) { target = h.Cup; cup = true; }
        else
        {
            target = h.Path[Mathf.Min(near + 1, h.Path.Count - 1)];
            for (int i = h.Path.Count - 1; i > near; i--) if (Clear(pos, h.Path[i], h)) { target = h.Path[i]; break; }
        }
        Vector3 to = Flat(target - pos); float dist = to.magnitude;
        float v = SpeedFor(dist + (cup ? .35f : .2f));
        float dy = target.y - (pos.y - GolfPlayer.BallRadius);
        if (dy > .2f) v = Mathf.Sqrt(v * v + 2f * 9.81f * dy * 1.6f) + 1f;
        else if (dy < -.2f) v = Mathf.Sqrt(Mathf.Max(.6f, v * v - 2f * 9.81f * -dy * .7f));
        v *= SpeedFactor * (1f + (R() - .5f) * 2f * Mathf.Lerp(.2f, .05f, skill));
        yaw = Quaternion.LookRotation(to.sqrMagnitude > .001f ? to : Vector3.forward).eulerAngles.y + (R() - .5f) * 2f * Mathf.Lerp(6f, 1.2f, skill);
        power = Mathf.Clamp(v / GolfPlayer.MaxSpeed, .05f, 1f);
    }

    // Measured on flat grass (GolfPlaytest.Measure): putt speed → distance rolled.
    static readonly float[] V = { 0f, 1f, 2f, 3f, 4f, 6f, 8f, 10f, 13f }, D = { 0f, .74f, 1.55f, 2.41f, 3.31f, 5.27f, 7.39f, 9.67f, 13.32f };
    public static float SpeedFor(float dist)
    {
        for (int i = 1; i < D.Length; i++) if (dist <= D[i]) return Mathf.Lerp(V[i - 1], V[i], (dist - D[i - 1]) / (D[i] - D[i - 1]));
        return GolfPlayer.MaxSpeed;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
