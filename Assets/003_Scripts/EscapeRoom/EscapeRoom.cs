using System.Collections.Generic;
using System.Linq;
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
    public const float ClueDelay = 40f, CluePenalty = 0f, ClueMinimum = 20f;   // race: clues at 0:40 and 1:20, helper at 1:30
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
    /// <summary>Players who must cross / stand somewhere to solve a room (the helper bot does not count).</summary>
    protected int TeamSize => D.Actors.Count(a => !a.Helper);
    protected IEnumerable<EscapeActor> Runners => D.Actors.Where(a => !a.Helper);

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
    /// <summary>Race: the helper ran out of time, the room opens anyway.</summary>
    public void ForceSolve(EscapeActor by) => Solve(by);
    /// <summary>Online client: clues the host has revealed.</summary>
    public void ForceClues(int shown) => requestedClues = Mathf.Max(requestedClues, shown);

    protected void Solve(EscapeActor by)
    {
        if (Solved) return;
        if (D.IsClient && !D.NetSolving) return;   // online: only the host decides (EscapeNet)
        Solved = true; SolvedBy = by;
        if (LocalBlind) foreach (var r in secrets) if (r != null) r.enabled = true;
        OnSolved();
        D.OnRoomSolved(this, by);
    }
    protected virtual void OnSolved() { }

    /// <summary>Waiting spot near the exit, along the right wall, out of the way.</summary>
    public EscapeOrder IdleOrder(EscapeActor a) => Wait(a);
    protected EscapeOrder Wait(EscapeActor a) { var o = EscapeOrder.Go(W(WaitSpot(a.Role)), .5f); o.idle = true; return o; }
    protected virtual Vector3 WaitSpot(int slot) => new Vector3(Width * .5f - 2.4f - (slot % 2) * 1.2f, 0f, Length * .5f - 2.4f - (slot / 2) * 1.2f);
    /// <summary>Last actor who touched this room's puzzle (credit for team solves).</summary>
    protected EscapeActor lastTouch;
    protected void Touch(EscapeActor a) { if (a != null) lastTouch = a; }

    protected bool InRoom(EscapeActor a) => D.RoomIndexAt(a.transform.position) == Index;

    // ------------------------------------------------------------------ co-op (2026-11-16)
    // Every puzzle station belongs to one player (a coloured light over it shows whose it is). Only its owner can use it;
    // the others must ask (E on a teammate's station asks that bot to do it) or tell them what to do. In some rooms the
    // owner of the key station (the keypad, the clock lever) cannot read the clues: the team has to read them aloud.
    readonly List<EscapeInteractable> stations = new();
    readonly List<Renderer> secrets = new();
    public IReadOnlyList<EscapeInteractable> Stations => stations;
    /// <summary>Whose owner cannot see the room's secret clues (null = everybody sees everything).</summary>
    protected virtual EscapeInteractable BlindStation => null;
    /// <summary>What teammates read aloud when the local player is the blind one.</summary>
    public virtual string[] ReadAloud => null;
    public bool LocalBlind { get; private set; }
    /// <summary>One line about the local player's job in this room (shown under the objective).</summary>
    public string RoleText { get; private set; } = "";
    protected void Secret(GameObject go) { if (go != null) secrets.AddRange(go.GetComponentsInChildren<Renderer>(true)); }
    protected void Secret(Component c) { if (c != null) Secret(c.gameObject); }
    /// <summary>Extra hiding for a blind local player (e.g. books that stop glowing).</summary>
    protected virtual void OnBlind() { }
    /// <summary>A bot plan needs this station: if it is the local player's, a teammate tells them.</summary>
    protected void Want(EscapeInteractable it) => D.Want(it);

    /// <summary>Hands the room's stations out: neighbouring stations go to the same player and, when there are enough, everybody gets some.
    /// Same result on every computer (positions and party seats are the same everywhere).</summary>
    public void AssignStations()
    {
        stations.Clear();
        foreach (var it in GetComponentsInChildren<EscapeInteractable>(true))
            if (!it.Shared) stations.Add(it);   // carried items too: only their owner can pick them up
        var seats = D.Actors.Select(a => D.SeatOf(a)).Distinct().OrderBy(s => s).ToList();
        if (stations.Count > 0 && seats.Count > 0)
        {
            var sorted = stations.OrderBy(it => Mathf.Round(L(it.Point).x * 4f)).ThenBy(it => Mathf.Round(L(it.Point).z * 4f))
                .ThenBy(it => Mathf.Round(L(it.Point).y * 4f)).ThenBy(it => it.NetId).ToList();
            int groups = Mathf.Min(seats.Count, sorted.Count);
            int shift = (Index * 5 + 1) % seats.Count;   // a different player gets the first group in every room
            for (int k = 0; k < sorted.Count; k++) sorted[k].Owner = seats[(k * groups / sorted.Count + shift) % seats.Count];
        }
        var human = D.Human;
        int me = human != null ? D.SeatOf(human) : -99;
        bool person = human != null && !human.DrivenByBot;
        if (person && BlindStation != null && BlindStation.Owner == me && D.Actors.Count > 1)
        {
            LocalBlind = true;
            foreach (var r in secrets) if (r != null) r.enabled = false;
            OnBlind();
        }
        int mine = stations.Count(s => s.Owner == me);
        RoleText = !person ? "" : LocalBlind ? "Tu papel: manejas el puesto clave pero NO puedes leer las pistas. ¡Que te las dicten!"
            : mine > 0 ? $"Tus puestos: {mine} (luz de tu color). Los demás son de tus compañeros: apunta y E para pedírselo."
            : stations.Count > 0 ? "Aquí no tienes puestos: observa, lee y dirige a tu equipo." : "";
        if (stations.Count > 0) gameObject.AddComponent<EscapeStationMarks>().Init(this);
    }

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

/// <summary>Coloured light over every station of the active room: the owner's colour (bigger for your own, flashing when needed).</summary>
public sealed class EscapeStationMarks : MonoBehaviour
{
    sealed class Mark { public EscapeInteractable it; public Transform orb; public Material mat; public Color color; public bool mine; }
    EscapeRoom room;
    readonly List<Mark> marks = new();

    public void Init(EscapeRoom r)
    {
        room = r;
        var human = r.D.Human;
        int me = human != null && !human.DrivenByBot ? r.D.SeatOf(human) : -99;
        foreach (var it in r.Stations)
        {
            var owner = r.D.ActorOfSeat(it.Owner);
            var m = new Mark { it = it, color = owner != null ? owner.Tint : Color.white, mine = it.Owner == me };
            m.mat = EscapeKit.Glow(m.color, 1.6f);
            m.orb = EscapeKit.Shape(PrimitiveType.Sphere, transform, "Puesto de " + (owner != null ? owner.DisplayName : "?"), Vector3.zero, Vector3.one * .1f, m.mat, false).transform;
            m.orb.gameObject.SetActive(false);
            marks.Add(m);
        }
    }

    void LateUpdate()
    {
        if (room == null || room.D == null) return;
        bool show = room.D.ActiveRoom == room && !room.Solved && room.D.State != EscapeDirector.Phase.Lobby;
        foreach (var m in marks)
        {
            bool on = show && m.it != null && m.it.isActiveAndEnabled;
            if (m.orb.gameObject.activeSelf != on) m.orb.gameObject.SetActive(on);
            if (!on) continue;
            bool wanted = room.D.WantedRecently(m.it);
            float s = (m.mine ? .15f : .1f) * (wanted ? 1.35f + Mathf.Sin(Time.time * 12f) * .3f : 1f + Mathf.Sin(Time.time * 3f + m.it.NetId) * .08f);
            m.orb.position = m.it.Point + m.it.markOffset;
            m.orb.localScale = Vector3.one * s;
            EscapeKit.SetGlow(m.mat, m.color, wanted ? 4f : m.mine ? 2.6f : 1.5f);
        }
    }
}
