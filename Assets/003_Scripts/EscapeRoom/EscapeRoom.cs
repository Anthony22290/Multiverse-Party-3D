using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One room of the tower. Rooms build their puzzle relative to their own transform (room centre on the floor,
/// +Z towards the exit), tick every frame, tell bots what to do and call Solve() when the puzzle is done.
/// </summary>
public abstract class EscapeRoom : MonoBehaviour
{
    public EscapeDirector D { get; private set; }
    public int Index { get; private set; }
    public bool Solved { get; private set; }
    public static float Length => EscapeDirector.RoomLength;
    public static float Width => EscapeDirector.RoomWidth;

    public abstract string Title { get; }
    public abstract string Objective { get; }
    /// <summary>Legacy single hint (used as the only clue when Clues is not overridden).</summary>
    public virtual string Hint => "";
    /// <summary>Clues revealed one by one: automatically after ClueDelay seconds each, or earlier on request (H, time penalty).</summary>
    public virtual string[] Clues => string.IsNullOrEmpty(Hint) ? new string[0] : new[] { Hint };
    public const float ClueDelay = 100f, CluePenalty = 30f, ClueMinimum = 40f;
    int requestedClues;
    public int CluesShown => Mathf.Min(Clues.Length, Mathf.Max(requestedClues, Mathf.FloorToInt(ActiveTime / ClueDelay)));
    public float NextClueIn => CluesShown >= Clues.Length ? -1f : Mathf.Max(0f, (CluesShown + 1) * ClueDelay - ActiveTime);
    public bool CanRequestClue => CluesShown < Clues.Length && ActiveTime >= ClueMinimum;
    public bool RequestClue() { if (!CanRequestClue) return false; requestedClues = CluesShown + 1; return true; }
    /// <summary>Thing a bot (or the player who solved it) gets credit for: set by Solve(by).</summary>
    public EscapeActor SolvedBy { get; private set; }
    public virtual Color Floor => new Color(.86f, .8f, .7f);
    public virtual Color Wall => new Color(.95f, .9f, .8f);
    public virtual Color Accent => new Color(1f, .7f, .3f);
    public virtual Color Glow => new Color(1f, .9f, .75f);
    /// <summary>Floor pieces as (zMin, zMax) in room space; null = full floor.</summary>
    public virtual Vector2[] FloorSegments => null;

    public Vector3 Entry => W(new Vector3(0f, 0f, -Length * .5f + 2.5f));
    public Vector3 W(Vector3 local) => transform.TransformPoint(local);
    public Vector3 L(Vector3 world) => transform.InverseTransformPoint(world);
    protected bool Active => D != null && D.Running && D.ActiveRoom == this && !Solved;
    protected float ActiveTime => D != null && D.ActiveRoom == this ? D.RoomTime : 0f;
    protected List<EscapeActor> Team => D.Actors;
    /// <summary>Helper mode: bots leave the key step to the player for a while and only finish it after `delay` seconds.</summary>
    protected bool Assist(float delay) => D.AutoHuman || ActiveTime > delay * D.AssistScale;
    protected int TeamSize => D.Actors.Count;

    public void Setup(EscapeDirector director, int index) { D = director; Index = index; }
    public abstract void Build();
    public virtual void OnTeamReady() { }
    public virtual void OnActivated() { }
    public virtual void Tick() { }
    public abstract EscapeOrder Order(EscapeActor a);

    /// <summary>Online: the keypad code typed on some computer (rooms with a keypad override this).</summary>
    public virtual bool KeypadSubmit(string code, EscapeActor a) => false;
    /// <summary>Online client: the host says this room is solved.</summary>
    public void NetSolve(EscapeActor by) => Solve(by);
    /// <summary>Online client: clues the host has revealed.</summary>
    public void ForceClues(int shown) => requestedClues = Mathf.Max(requestedClues, shown);

    protected void Solve(EscapeActor by)
    {
        if (Solved) return;
        if (D.IsClient && !D.NetSolving) return;   // online: only the host decides (EscapeNet)
        Solved = true; SolvedBy = by;
        OnSolved();
        D.OnRoomSolved(this, by);
    }
    protected virtual void OnSolved() { }

    /// <summary>Waiting spot near the exit, along the right wall, out of the way.</summary>
    protected EscapeOrder Wait(EscapeActor a) => EscapeOrder.Go(W(WaitSpot(a.Role)), .5f);
    protected virtual Vector3 WaitSpot(int slot) => new Vector3(Width * .5f - 2.4f - (slot % 2) * 1.2f, 0f, Length * .5f - 2.4f - (slot / 2) * 1.2f);
    /// <summary>Last actor who touched this room's puzzle (credit for team solves).</summary>
    protected EscapeActor lastTouch;
    protected void Touch(EscapeActor a) { if (a != null) lastTouch = a; }

    protected bool InRoom(EscapeActor a) => D.RoomIndexAt(a.transform.position) == Index;

    protected static Material M(string key, Color c) => EscapeKit.Mat(key, c);
    protected static Material P(Color c) => EscapeKit.Paint(c);

    protected GameObject Block(string name, Vector3 local, Vector3 size, Material mat, float bevel = .06f, bool solid = true, Vector3 euler = default) =>
        EscapeKit.Block(transform, name, local, size, mat, bevel, solid, euler);

    protected GameObject Prop(string key, Vector3 local, float yaw, float scale = 1f) => EscapeKit.Prop(key, transform, local, yaw, scale);

    /// <summary>Colour names used by several puzzles.</summary>
    protected static readonly Color[] Hues = { new Color(1f, .3f, .32f), new Color(.3f, .55f, 1f), new Color(.35f, .9f, .4f), new Color(1f, .85f, .2f) };
    protected static readonly string[] HueNames = { "roja", "azul", "verde", "amarilla" };
    protected static readonly string[] HueWords = { "ROJO", "AZUL", "VERDE", "AMARILLO" };

    protected static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>Little confetti burst (pooled particle system built once per room).</summary>
    ParticleSystem burst;
    protected void Confetti(Vector3 world, Color color, int count = 40)
    {
        if (burst == null)
        {
            var go = new GameObject("Confeti");
            go.transform.SetParent(transform, false);
            burst = go.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = burst.main;
            main.loop = false; main.playOnAwake = false; main.startLifetime = 1.6f; main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            main.startSize = new ParticleSystem.MinMaxCurve(.07f, .16f); main.gravityModifier = 1.2f; main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = burst.emission; emission.rateOverTime = 0f;
            var shape = burst.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .25f;
            var rot = burst.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            var fx = Resources.Load<Material>("ArenaFx/Particula suave");
            r.sharedMaterial = fx != null ? fx : EscapeKit.Glow(Color.white, 1.4f);
            r.renderMode = ParticleSystemRenderMode.Billboard;
        }
        burst.transform.position = world;
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            p.startColor = Color.Lerp(color, Hues[i % Hues.Length], .5f);
            burst.Emit(p, 1);
        }
    }
}
