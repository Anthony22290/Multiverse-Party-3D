using UnityEngine;

/// <summary>Bot for Propulsores: aims for the middle of the next gap and taps when it is about to sink below it.</summary>
[RequireComponent(typeof(FlapPlayer)), DefaultExecutionOrder(-20)]
public sealed class FlapBot : MonoBehaviour
{
    FlapPlayer me; FlapDirector d; System.Random rng; float skill, nextTap, wobble, wobbleUntil;

    void Start()
    {
        me = GetComponent<FlapPlayer>(); d = me.Director;
        rng = new System.Random(unchecked(d.Seed * 61 + me.Slot * 13));
        skill = me.IsHuman ? .92f : MultiverseSettings.BotSkill(.35f + (float)rng.NextDouble() * .55f);
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || d.State != FlapDirector.Phase.Running || me.Down) return;
        float y = transform.position.y;
        int k = d.PipeAhead(me.X - FlapDirector.PipeHalf - FlapPlayer.Radius);
        float px = d.PipeX(k), target = d.GapY(k) - .35f;
        // Between two towers, drift towards the next gap early.
        if (px - me.X > FlapDirector.Spacing * .6f && k > 0) target = Mathf.Lerp(d.GapY(k - 1), d.GapY(k), .6f) - .35f;
        // Mistakes: now and then the bot misjudges the height for a moment (more often when it is fast).
        if (Time.time > wobbleUntil && R() < (1f - skill) * .7f * Time.deltaTime * (1f + d.Elapsed / 60f)) { wobble = (R() - .5f) * 2.6f; wobbleUntil = Time.time + .6f; }
        if (Time.time < wobbleUntil) target += wobble;
        // Predict where we will be shortly and tap if that is too low.
        float look = Mathf.Lerp(.06f, .03f, skill);
        float future = y + me.VelY * look - .5f * FlapPlayer.Gravity * look * look;
        if (future < target && Time.time >= nextTap && me.VelY < .5f)
        {
            me.InFlap = true;
            nextTap = Time.time + Mathf.Lerp(.2f, .13f, skill) + R() * .05f;
        }
    }
}
