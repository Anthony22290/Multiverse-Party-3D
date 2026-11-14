using System.Linq;
using Fusion;
using UnityEngine;

/// <summary>One authority simulates the arena. Clients send controls and render replicated state.</summary>
public sealed class PirateNetworkWorld : NetworkBehaviour
{
    [Networked, Capacity(6)] public NetworkArray<PlayerRef> Owners => default;
    [Networked, Capacity(6)] public NetworkArray<PirateBodyState> Fighters => default;
    [Networked, Capacity(8)] public NetworkArray<PirateBodyState> Props => default;
    [Networked, Capacity(2)] public NetworkArray<PirateHazardState> Cannons => default;
    [Networked] public PirateHazardState Shark { get; set; }
    [Networked] public PirateRoundState Round { get; set; }
    [Networked] public NetworkBool Started { get; set; }
    [Networked] public int RoundNumber { get; set; }
    private PirateArenaDirector arena;
    private readonly PirateNetworkInput[] previous = new PirateNetworkInput[6];
    private readonly float[] lastInput = new float[6];
    private readonly PlayerRef[] appliedOwners = new PlayerRef[6];
    private bool ownersApplied, propsPositioned;

    public override void Spawned()
    {
        arena = PirateSession.Instance.Arena;
        PirateSession.Instance.AttachWorld(this);
        if (HasStateAuthority) { arena.RestartRound(); Capture(); }
    }

    public void StartRound()
    {
        if (!HasStateAuthority) return;
        arena.RestartRound(); Started = true; RoundNumber++;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || arena == null) return;
        var players = Runner.ActivePlayers.ToArray();
        for (int i = 0; i < 6; i++)
        {
            if (Owners[i] != PlayerRef.None && !players.Contains(Owners[i]))
            {
                arena.Fighters[i].ReleaseGrab(false); Owners.Set(i, PlayerRef.None); previous[i] = default;
            }
        }
        foreach (PlayerRef player in players)
        {
            bool found = false;
            for (int i = 0; i < 6; i++) if (Owners[i] == player) found = true;
            if (found) continue;
            for (int i = 0; i < 6; i++) if (Owners[i] == PlayerRef.None)
            {
                arena.Fighters[i].ReleaseGrab(false); Owners.Set(i, player); previous[i] = default; lastInput[i] = Time.time; break;
            }
        }
        ApplyOwners();
        if (Started)
        {
            arena.Tick(Runner.DeltaTime);
            for (int i = 0; i < 6; i++)
            {
                var fighter = arena.Fighters[i];
                if (Owners[i] == PlayerRef.None) fighter.NetworkBot();
                else if (Runner.TryGetInputForPlayer<PirateNetworkInput>(Owners[i], out var input))
                {
                    fighter.ApplyInput(input, previous[i]); previous[i] = input; lastInput[i] = Time.time;
                }
                else if (Time.time - lastInput[i] > 0.25f)
                {
                    var neutral = previous[i]; neutral.Move = Vector2.zero; neutral.Grab = false; neutral.RightGrab = false; neutral.Lift = false;
                    neutral.Drop++; fighter.ApplyInput(neutral, previous[i]); previous[i] = neutral;
                }
                fighter.Simulate(Runner.DeltaTime);
                if (!fighter.IsAlive && fighter.Body.position.y < -12f) fighter.Body.isKinematic = true;
            }
            foreach (var cannon in arena.Cannons) cannon.Tick();
            foreach (var shark in arena.Sharks) shark.Tick();
            foreach (var prop in arena.Props) prop.Tick();
            Physics.Simulate(Runner.DeltaTime);
        }
        Capture();
    }

    private void Capture()
    {
        for (int i = 0; i < 6; i++) Fighters.Set(i, arena.Fighters[i].CaptureState());
        for (int i = 0; i < 8; i++)
        {
            var body = arena.Props[i].GetComponent<Rigidbody>();
            Props.Set(i, new PirateBodyState { Position = body.position, Rotation = body.rotation, Velocity = body.linearVelocity });
        }
        for (int i = 0; i < 2; i++) Cannons.Set(i, arena.Cannons[i].CaptureState());
        Shark = arena.Sharks[0].CaptureState(); Round = arena.CaptureState();
    }

    private void ApplyOwners()
    {
        for (int i = 0; i < 6; i++)
        {
            if (ownersApplied && appliedOwners[i] == Owners[i]) continue;
            appliedOwners[i] = Owners[i];
            bool local = Owners[i] != PlayerRef.None && Owners[i] == Runner.LocalPlayer;
            arena.Fighters[i].SetControl(local, Owners[i] == PlayerRef.None ? "Bot " + (i + 1) : "Jugador " + (i + 1));
        }
        ownersApplied = true;
    }

    public override void Render()
    {
        if (arena == null) return;
        ApplyOwners();
        if (HasStateAuthority) return;
        for (int i = 0; i < 6; i++) arena.Fighters[i].ApplyState(Fighters[i]);
        for (int i = 0; i < 8; i++)
        {
            Transform t = arena.Props[i].transform; var state = Props[i];
            float blend = !propsPositioned || Vector3.Distance(t.position, state.Position) > 6f ? 1f : 1f - Mathf.Exp(-22f * Time.deltaTime);
            t.SetPositionAndRotation(Vector3.Lerp(t.position, state.Position, blend), Quaternion.Slerp(t.rotation, state.Rotation, blend));
        }
        propsPositioned = true;
        for (int i = 0; i < 2; i++) arena.Cannons[i].ApplyState(Cannons[i]);
        arena.Sharks[0].ApplyState(Shark); arena.ApplyState(Round);
    }
}
