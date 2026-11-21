using UnityEngine;

/// <summary>
/// Bot for Rodillos del Calabozo: watches both rollers, jumps the low one and ducks under the high one with a human-like
/// reaction time, sometimes misjudges (more often as the rollers speed up), wanders around the platform and keeps off
/// tiles that are shaking or about to fall.
/// </summary>
[RequireComponent(typeof(RodillosRunner)), DefaultExecutionOrder(-20)]
public sealed class RodillosBot : MonoBehaviour
{
    RodillosRunner me; RodillosDirector d;
    float skill, nextTarget; Vector3 target;
    readonly bool[] decided = new bool[2]; readonly bool[] willMiss = new bool[2]; readonly float[] lead = new float[2];
    float duckUntil; System.Random rng; bool lastDuck, lastLowDanger;

    void Start()
    {
        me = GetComponent<RodillosRunner>(); d = me.Director;
        rng = new System.Random(unchecked(d.Seed * 31 + me.Slot * 977));
        skill = me.IsHuman ? .95f : MultiverseSettings.BotSkill(.35f + (float)rng.NextDouble() * .55f);
        PickTarget();
    }

    float R() => (float)rng.NextDouble();

    /// <summary>
    /// How the roller approaches this point at its current speed: seconds until its front edge arrives, seconds since
    /// its centre went past, and how long it takes to sweep across a body.
    /// </summary>
    public static void Approach(RodillosDirector d, int arm, Vector3 p, out float impact, out float since, out float crossing) => Approach(d, arm, p, Vector3.zero, out impact, out since, out crossing);
    /// <param name="vel">The player's own velocity: running towards the roller brings it closer sooner.</param>
    public static void Approach(RodillosDirector d, int arm, Vector3 p, Vector3 vel, out float impact, out float since, out float crossing)
    {
        impact = since = 99f; crossing = 0f;
        var spin = d.Spin; float w = spin.YawSpeed(arm); float r = new Vector2(p.x, p.z).magnitude;
        if (Mathf.Abs(w) < 1f || r < .5f) return;
        // Angular speed of the point itself, in yaw degrees per second (yaw grows clockwise seen from above).
        float pointYawSpeed = -(p.x * vel.z - p.z * vel.x) / (r * r) * Mathf.Rad2Deg;
        // Yaw of the point: the arm with this yaw points straight at it.
        float pointYaw = -RodillosArena.AngleOf(p);
        float delta = Mathf.Repeat(pointYaw - spin.Angle(arm), 360f);
        float travel = w > 0 ? delta : 360f - delta;
        float halfWidth = ((arm == RodillosSpin.Low ? RodillosDirector.LowHitRadius : RodillosDirector.HighHitRadius) + RodillosRunner.BodyRadius) / r * Mathf.Rad2Deg;
        float aw = Mathf.Max(5f, Mathf.Abs(w) - Mathf.Sign(w) * pointYawSpeed);
        impact = (travel - halfWidth) / aw;
        // Passing and crossing depend on the roller only (running along with it must not make it look "far").
        float rw = Mathf.Abs(w);
        since = (360f - travel) / rw;
        crossing = 2f * halfWidth / rw;
    }
    public static float TimeToImpact(RodillosDirector d, int arm, Vector3 p) { Approach(d, arm, p, out var t, out _, out _); return t; }

    void PickTarget()
    {
        nextTarget = Time.time + 2.5f + R() * 4f;
        for (int tries = 0; tries < 20; tries++)
        {
            float a = R() * 360f, r = 3.2f + R() * 6.2f;
            var p = Quaternion.Euler(0, a, 0) * Vector3.right * r;
            if (Safe(p, 5f)) { target = p; return; }
        }
        target = transform.position;
    }

    bool Safe(Vector3 p, float horizon)
    {
        var tile = d.Arena.TileAt(p);
        if (tile == null) return false;
        return d.Elapsed + horizon < tile.ShakeAt;
    }

    public static int[] Planned = new int[2], Unplanned = new int[2];
    public static string LastBad = ""; public static bool Debugging;
    /// <summary>Test statistics: was this hit a planned mistake or a timing error?</summary>
    public void Report(int arm)
    {
        if (arm < 0 || arm > 1) return;
        if (decided[arm] && willMiss[arm]) Planned[arm]++;
        else
        {
            Unplanned[arm]++;
            Approach(d, arm, transform.position, out float ti, out float since, out float cross);
            LastBad = $"brazo {arm} · decidido {decided[arm]} · lead {lead[arm]:0.00} · t {ti:0.00} · desde {since:0.00} · cruce {cross:0.00} · r {new Vector2(transform.position.x, transform.position.z).magnitude:0.0} · y {transform.position.y:0.00} · agachado {me.Ducking} · suelo {me.Grounded} · salto hace {d.Elapsed - me.LastJumpAt:0.00} · vel {me.Velocity.y:0.0} · dt {Time.deltaTime:0.000} · quiere agacharse {lastDuck} · bajo cerca {lastLowDanger} · InDuck {me.InDuck}";
            if (Debugging) Debug.LogWarning("Rodillos bot: " + LastBad);
        }
    }

    void Update()
    {
        if (me == null || !me.Alive || d.State != RodillosDirector.Phase.Running) { if (me != null) { me.InMove = Vector3.zero; me.InDuck = false; } return; }
        float t = d.Elapsed; Vector3 p = transform.position;
        float pressure = Mathf.Clamp01(t / 150f);
        float missChance = Mathf.Lerp(.06f, .015f, skill) + pressure * .05f;
        float noise = Mathf.Lerp(.07f, .025f, skill);

        // --- low roller: jump.
        Vector3 v = me.Velocity; v.y = 0f;
        Approach(d, RodillosSpin.Low, p, v, out float tLow, out float sinceLow, out float crossLow);
        if (tLow > 1.2f) decided[0] = false;
        else if (!decided[0])
        {
            // Best moment: the jump keeps the feet high for ~0.46 s centred 0.37 s after take-off.
            decided[0] = true; willMiss[0] = R() < missChance;
            lead[0] = Mathf.Clamp(.37f - crossLow * .5f, .17f, .3f) + (R() - .5f) * 2f * noise;
        }
        bool jump = decided[0] && !willMiss[0] && tLow <= lead[0] && tLow > -.05f && me.Grounded;

        // --- high roller: duck (only once it is up).
        bool duck = false; float tHighNow = 99f; bool highPassing = false;
        if (t >= RodillosDirector.HighArrives - .5f)
        {
            Approach(d, RodillosSpin.High, p, v, out float tHigh, out float sinceHigh, out float crossHigh);
            tHighNow = tHigh; highPassing = sinceHigh < crossHigh * .7f;
            if (tHigh > 1.5f && sinceHigh > crossHigh) decided[1] = false;
            else if (!decided[1]) { decided[1] = true; willMiss[1] = R() < missChance * .8f; lead[1] = .32f + (R() - .5f) * 2f * noise * 1.5f; }
            if (decided[1] && !willMiss[1] && tHigh <= lead[1]) duckUntil = Time.time + .1f;
            // Stay down until the roller has gone past (time to impact jumps to a large value).
            if (Time.time < duckUntil || (decided[1] && !willMiss[1] && (tHigh < lead[1] + .05f || sinceHigh < crossHigh * .5f + .1f))) duck = true;
            // The low roller is on top of us (or about to be): jumping it matters first. Once it is past, ducking in
            // the air is a fast fall, so the bot can land already crouched for the ice roller.
            bool lowDanger = decided[0] && !willMiss[0] && ((tLow < .3f && tLow > -.05f) || sinceLow < crossLow * .5f + .04f);
            lastLowDanger = lowDanger;
            if (duck && lowDanger) duck = false;
            if (!me.Grounded && decided[1] && !willMiss[1] && tHigh < .6f && !lowDanger) duck = true;   // fast fall in time
            if (jump && decided[1] && !willMiss[1] && tHigh < .2f) jump = false;   // jumping now would go straight into the ice roller
        }

        // --- moving around.
        var here = d.Arena.TileAt(p);
        bool danger = here != null && t + 1.2f >= here.ShakeAt;
        if (Time.time > nextTarget || danger || !Safe(target, 2.5f)) PickTarget();
        Vector3 to = target - p; to.y = 0;
        Vector3 move = to.magnitude > .6f ? to.normalized * Mathf.Clamp01(to.magnitude / 2f) : Vector3.zero;
        // Keep away from the edge and the hub.
        float r = new Vector2(p.x, p.z).magnitude; Vector3 outDir = new Vector3(p.x, 0, p.z) / Mathf.Max(.01f, r);
        if (r > 9.8f) move -= outDir * 1.2f;
        if (r < 2.6f) move += outDir;
        if (danger) move *= 1.4f;
        // Like a person: stand still and get ready while a roller is about to arrive.
        else if ((tLow < .9f && tLow > -.2f) || sinceLow < crossLow * .7f || (tHighNow < .9f && tHighNow > -.2f) || highPassing) move *= .15f;
        me.InMove = Vector3.ClampMagnitude(move, 1f) * (duck ? 0f : 1f);
        me.InDuck = duck; lastDuck = duck;
        if (jump) me.InJump = true;
    }
}
