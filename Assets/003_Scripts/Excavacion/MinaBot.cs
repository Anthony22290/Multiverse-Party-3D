using UnityEngine;

/// <summary>
/// Bot for Pico, Pala y Tijera: looks at the next block, takes a human-ish moment to react, switches tool if needed and
/// clicks. Sometimes it grabs the wrong tool (and gets stunned like anyone else). Speed varies per bot.
/// </summary>
[RequireComponent(typeof(MinaMiner)), DefaultExecutionOrder(-20)]
public sealed class MinaBot : MonoBehaviour
{
    MinaMiner me; MinaDirector d; System.Random rng;
    float skill, readyAt = -1f; int plannedDepth = -1, plannedHits = -1; bool willErr;

    void Start()
    {
        me = GetComponent<MinaMiner>(); d = me.Director;
        rng = new System.Random(unchecked(d.Seed * 131 + me.Slot * 7));
        skill = me.IsHuman ? .9f : MultiverseSettings.BotSkill(.25f + (float)rng.NextDouble() * .6f);
    }
    float R() => (float)rng.NextDouble();

    void Update()
    {
        if (me == null || d.State != MinaDirector.Phase.Running || me.Finished || me.Stunned || me.Falling) return;
        if (me.Depth != plannedDepth || me.Hits != plannedHits)
        {
            plannedDepth = me.Depth; plannedHits = me.Hits;
            bool switching = MinaKit.Needed(me.Next) != me.Tool && me.Next != MinaKit.Block.Oro;
            float react = Mathf.Lerp(.36f, .17f, skill) + R() * .12f + (switching ? Mathf.Lerp(.16f, .07f, skill) : 0f);
            if (me.Hits > 0) react *= .45f;   // second hit on hard stone: just keep swinging
            readyAt = Time.time + react;
            willErr = switching && R() < Mathf.Lerp(.16f, .05f, skill);
        }
        if (Time.time < readyAt) return;
        var need = MinaKit.Needed(me.Next);
        if (willErr) { me.InTool = ((int)need + 1 + rng.Next(2)) % 3; willErr = false; }
        else if (me.Next != MinaKit.Block.Oro) me.InTool = (int)need;
        me.InMine = true;
        readyAt = Time.time + MinaMiner.Cooldown + .02f;
    }
}
