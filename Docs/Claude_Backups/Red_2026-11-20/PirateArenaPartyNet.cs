using System.IO;
using UnityEngine;

/// <summary>
/// Arena del Caos inside an online party (2026-11-12). Same authority model as the arena's own rooms
/// (PirateNetworkWorld): the host simulates every pirate, prop, cannon, shark and the kraken; clients send their
/// controls and show the replicated state. It travels over the party connection (PartyLink) instead of a second
/// Photon room, so the whole party plays the same deck.
/// </summary>
public sealed class PirateArenaPartyNet : MonoBehaviour
{
    const byte ChInput = 30, ChState = 31, ChEvent = 32;
    const byte EvKraken = 1;
    const float StateRate = 30f;

    PirateArenaDirector arena;
    PirateSession session;
    PirateKrakenHazard kraken;
    bool host, started, reported, physicsOwned;
    SimulationMode previousPhysics;
    readonly PirateNetworkInput[] inputs = new PirateNetworkInput[6], previous = new PirateNetworkInput[6];
    readonly float[] lastInput = new float[6];
    float nextSend;

    // Client: latest state from the host.
    PirateBodyState[] fighterStates = new PirateBodyState[6];
    readonly bool[] fighterHas = new bool[6];
    PirateBodyState[] propStates = new PirateBodyState[0];
    PirateHazardState[] cannonStates = new PirateHazardState[0];
    PirateHazardState sharkState; PirateRoundState roundState;
    bool hasState, propsPositioned;

    public void Init(PirateSession s, PirateArenaDirector a)
    {
        session = s; arena = a; host = PartyLink.IsHost;
        bool proxy = !host;
        arena.NetworkDriven = true;
        foreach (var f in arena.Fighters) f.SetNetworkMode(true, proxy);
        foreach (var prop in arena.Props)
        {
            prop.NetworkDriven = true; prop.IsProxy = proxy;
            var body = prop.GetComponent<Rigidbody>();
            body.isKinematic = proxy; body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = proxy ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
        }
        foreach (var c in arena.Cannons) c.NetworkDriven = true;
        foreach (var sh in arena.Sharks) sh.NetworkDriven = true;
        if (host) { previousPhysics = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script; physicsOwned = true; }
        kraken = arena.GetComponent<PirateKrakenHazard>();
        if (kraken == null) kraken = FindAnyObjectByType<PirateKrakenHazard>();
        if (kraken != null) { kraken.PartyHost = host; kraken.PartyClient = !host; if (host) kraken.OnBegin = SendKraken; }
        PartyLink.Listen(ChInput, OnInput);
        PartyLink.Listen(ChState, OnState);
        PartyLink.Listen(ChEvent, OnEvent);
        PartyLink.MarkReady();
    }

    void OnDestroy()
    {
        if (physicsOwned) Physics.simulationMode = previousPhysics;
        PartyLink.Reset();
    }

    // ------------------------------------------------------------------ host simulation

    void FixedUpdate()
    {
        if (!host || arena == null) return;
        float dt = Time.fixedDeltaTime;
        if (!started)
        {
            if (!PartyLink.AllReady) { Physics.Simulate(dt); return; }
            started = true; arena.RestartRound();
        }
        arena.Tick(dt);
        for (int i = 0; i < arena.Fighters.Count; i++)
        {
            var fighter = arena.Fighters[i];
            if (i == 0)
            {
                var input = session != null ? session.LatestInput : default;
                fighter.ApplyInput(input, previous[0]); previous[0] = input;
            }
            else if (PartyLink.IsRemoteHuman(i))
            {
                var input = inputs[i];
                if (Time.time - lastInput[i] > .4f)
                {   // no news from that player: stand still and let go
                    input = previous[i]; input.Move = Vector2.zero; input.Grab = input.RightGrab = input.Lift = false;
                    if (previous[i].Grab || previous[i].RightGrab) input.Drop++;
                    inputs[i] = input;
                }
                fighter.ApplyInput(input, previous[i]); previous[i] = input;
            }
            else fighter.NetworkBot();
            fighter.Simulate(dt);
            if (!fighter.IsAlive && fighter.Body.position.y < -12f) fighter.Body.isKinematic = true;
        }
        foreach (var cannon in arena.Cannons) cannon.Tick();
        foreach (var shark in arena.Sharks) shark.Tick();
        foreach (var prop in arena.Props) prop.Tick();
        Physics.Simulate(dt);
        if (arena.Finished && !reported)
        {
            reported = true;
            PartyLink.Finish(arena.Winner != null ? IndexOf(arena.Winner) : -1);
        }
    }

    int IndexOf(PirateFighter f) { for (int i = 0; i < arena.Fighters.Count; i++) if (arena.Fighters[i] == f) return i; return -1; }

    void Update()
    {
        if (arena == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextSend)
        {
            nextSend = Time.unscaledTime + 1f / StateRate;
            if (host) SendState(); else SendInput();
        }
        if (!host) ApplyLatest();
    }

    void SendInput()
    {
        var i = session != null ? session.LatestInput : default;
        var w = PartyLink.Begin(ChInput);
        w.Write(i.Move.x); w.Write(i.Move.y); w.Write(i.Yaw); w.Write(i.LookPitch);
        w.Write((byte)((i.Grab ? 1 : 0) | (i.RightGrab ? 2 : 0) | (i.Lift ? 4 : 0)));
        w.Write(i.Jump); w.Write(i.Push); w.Write(i.Drop); w.Write(i.Throw);
        PartyLink.SendToHost();
    }

    void OnInput(int from, BinaryReader r)
    {
        if (!host) return;
        int slot = PartyLink.SlotOfMember(from);
        var i = new PirateNetworkInput { Move = new Vector2(r.ReadSingle(), r.ReadSingle()), Yaw = r.ReadSingle(), LookPitch = r.ReadSingle() };
        byte b = r.ReadByte(); i.Grab = (b & 1) != 0; i.RightGrab = (b & 2) != 0; i.Lift = (b & 4) != 0;
        i.Jump = r.ReadInt32(); i.Push = r.ReadInt32(); i.Drop = r.ReadInt32(); i.Throw = r.ReadInt32();
        if (slot <= 0 || slot >= inputs.Length) return;
        inputs[slot] = i; lastInput[slot] = Time.time;
    }

    void SendState()
    {
        var w = PartyLink.Begin(ChState);
        w.Write((byte)arena.Fighters.Count);
        for (int i = 0; i < arena.Fighters.Count; i++) { w.Write((sbyte)PartyLink.MemberOfSlot(i)); WriteBody(w, arena.Fighters[i].CaptureState()); }
        w.Write((byte)arena.Props.Count);
        foreach (var prop in arena.Props)
        {
            var body = prop.GetComponent<Rigidbody>();
            w.Write(body.position); w.Write(body.rotation);
        }
        w.Write((byte)arena.Cannons.Count);
        foreach (var c in arena.Cannons) WriteHazard(w, c.CaptureState());
        WriteHazard(w, arena.Sharks.Count > 0 ? arena.Sharks[0].CaptureState() : default);
        var round = arena.CaptureState();
        w.Write(round.Playing); w.Write(round.Finished); w.Write(round.Countdown); w.Write(round.Elapsed);
        w.Write((sbyte)(round.Winner >= 0 ? PartyLink.MemberOfSlot(round.Winner) : -1));
        PartyLink.SendToAll();
    }

    // ------------------------------------------------------------------ client view

    void OnState(int from, BinaryReader r)
    {
        if (host) return;
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int slot = PartyLink.SlotOfMember(r.ReadSByte());
            var s = ReadBody(r);
            if (slot >= 0 && slot < fighterStates.Length) { fighterStates[slot] = s; fighterHas[slot] = true; }
        }
        int props = r.ReadByte();
        if (propStates.Length != props) propStates = new PirateBodyState[props];
        for (int i = 0; i < props; i++) propStates[i] = new PirateBodyState { Position = r.ReadVector3(), Rotation = r.ReadQuaternion() };
        int cannons = r.ReadByte();
        if (cannonStates.Length != cannons) cannonStates = new PirateHazardState[cannons];
        for (int i = 0; i < cannons; i++) cannonStates[i] = ReadHazard(r);
        sharkState = ReadHazard(r);
        roundState = new PirateRoundState { Playing = r.ReadBoolean(), Finished = r.ReadBoolean(), Countdown = r.ReadSingle(), Elapsed = r.ReadSingle() };
        int winnerMember = r.ReadSByte();
        roundState.Winner = winnerMember >= 0 ? PartyLink.SlotOfMember(winnerMember) : -1;
        hasState = true;
    }

    void ApplyLatest()
    {
        if (!hasState) return;
        for (int i = 0; i < arena.Fighters.Count && i < fighterStates.Length; i++) if (fighterHas[i]) arena.Fighters[i].ApplyState(fighterStates[i]);
        for (int i = 0; i < arena.Props.Count && i < propStates.Length; i++)
        {
            Transform t = arena.Props[i].transform; var state = propStates[i];
            float blend = !propsPositioned || Vector3.Distance(t.position, state.Position) > 6f ? 1f : 1f - Mathf.Exp(-22f * Time.deltaTime);
            t.SetPositionAndRotation(Vector3.Lerp(t.position, state.Position, blend), Quaternion.Slerp(t.rotation, state.Rotation, blend));
        }
        propsPositioned = true;
        for (int i = 0; i < arena.Cannons.Count && i < cannonStates.Length; i++) arena.Cannons[i].ApplyState(cannonStates[i]);
        if (arena.Sharks.Count > 0) arena.Sharks[0].ApplyState(sharkState);
        arena.ApplyState(roundState);
        if (roundState.Finished && !reported) { reported = true; PartyLink.Finish(-1); }
    }

    // ------------------------------------------------------------------ kraken

    void SendKraken(Vector3 target, Vector3 basePoint)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvKraken); w.Write(target); w.Write(basePoint);
        PartyLink.SendToAll();
    }

    void OnEvent(int from, BinaryReader r)
    {
        if (host) return;
        if (r.ReadByte() == EvKraken) { var t = r.ReadVector3(); var b = r.ReadVector3(); if (kraken != null) kraken.NetBegin(t, b); }
    }

    // ------------------------------------------------------------------ serialization

    static void WriteBody(BinaryWriter w, PirateBodyState s)
    {
        w.Write(s.Position); w.Write(s.Velocity); w.Write(s.Grip); w.Write(s.LeftGrip); w.Write(s.RightGrip); w.Write(s.Rotation);
        w.Write(s.Flags); w.Write((byte)s.Action); w.Write(s.ActionSerial); w.Write(s.LookPitch);
    }
    static PirateBodyState ReadBody(BinaryReader r) => new PirateBodyState
    {
        Position = r.ReadVector3(), Velocity = r.ReadVector3(), Grip = r.ReadVector3(), LeftGrip = r.ReadVector3(), RightGrip = r.ReadVector3(),
        Rotation = r.ReadQuaternion(), Flags = r.ReadInt32(), Action = r.ReadByte(), ActionSerial = r.ReadInt32(), LookPitch = r.ReadSingle()
    };
    static void WriteHazard(BinaryWriter w, PirateHazardState s) { w.Write(s.Position); w.Write(s.Target); w.Write(s.Projectile); w.Write(s.Rotation); w.Write(s.Phase); }
    static PirateHazardState ReadHazard(BinaryReader r) => new PirateHazardState
    { Position = r.ReadVector3(), Target = r.ReadVector3(), Projectile = r.ReadVector3(), Rotation = r.ReadQuaternion(), Phase = r.ReadInt32() };
}
