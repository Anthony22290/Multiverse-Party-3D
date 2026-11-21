using Fusion;
using UnityEngine;

/// <summary>Replicates the host's MultiverseParty to every client and carries their requests back.</summary>
public sealed class MultiversePartyNet : NetworkBehaviour
{
    [Networked] public int Size { get; set; }
    [Networked, Capacity(6)] public NetworkArray<NetworkString<_16>> Names => default;
    [Networked, Capacity(6)] public NetworkArray<PlayerRef> Owners => default;
    [Networked, Capacity(6)] public NetworkArray<int> Scores => default;
    [Networked, Capacity(6)] public NetworkArray<int> Votes => default;
    [Networked, Capacity(6)] public NetworkArray<int> Characters => default;
    [Networked, Capacity(6)] public NetworkArray<int> ColorIdx => default;
    [Networked, Capacity(72)] public NetworkArray<int> Wins => default;
    [Networked, Capacity(6)] public NetworkArray<int> Pushes => default;
    [Networked, Capacity(6)] public NetworkArray<int> Pusheds => default;
    [Networked, Capacity(6)] public NetworkArray<int> EmoteCounts => default;
    [Networked, Capacity(6)] public NetworkArray<int> VoteHits => default;
    [Networked, Capacity(6)] public NetworkArray<int> ChatCounts => default;
    [Networked, Capacity(6)] public NetworkArray<int> EmoteIds => default;
    [Networked, Capacity(6)] public NetworkArray<int> EmoteSeqs => default;
    [Networked, Capacity(6)] public NetworkArray<int> ChatIds => default;
    [Networked, Capacity(6)] public NetworkArray<int> ChatSeqs => default;
    [Networked, Capacity(6)] public NetworkArray<int> PushSeqs => default;
    [Networked, Capacity(6)] public NetworkArray<Vector3> PushDirs => default;
    [Networked, Capacity(6)] public NetworkArray<Vector3> Poses => default;
    [Networked, Capacity(6)] public NetworkArray<float> Yaws => default;
    [Networked, Capacity(6)] public NetworkArray<NetworkBool> Moving => default;
    [Networked, Capacity(3)] public NetworkArray<int> Choices => default;
    [Networked] public int Phase { get; set; }
    [Networked] public int Selected { get; set; }
    [Networked] public int Round { get; set; }
    [Networked] public float Countdown { get; set; }
    [Networked] public int LastWinner { get; set; }
    [Networked] public int LastGame { get; set; }
    [Networked] public NetworkString<_64> ResultText { get; set; }
    [Networked] public int TargetSize { get; set; }
    [Networked] public int ReportsIn { get; set; }
    [Networked] public int ReportsNeeded { get; set; }
    [Networked] public int Seed { get; set; }
    [Networked] public int Goal { get; set; }

    float nextPose;

    MultiverseParty Party => MultiverseParty.Instance;

    public override void Spawned()
    {
        if (Party != null) Party.AttachNet(this);
        if (HasStateAuthority && Party != null) Party.WriteTo(this);
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && Party != null) Party.WriteTo(this);
    }

    public override void Render()
    {
        if (!HasStateAuthority && Party != null) Party.ReadFrom(this);
    }

    public void SendPose(Vector3 pos, float yaw, bool moving)
    {
        if (Time.unscaledTime < nextPose) return;
        nextPose = Time.unscaledTime + 1f / 15f;
        RPC_Pose(pos, yaw, moving);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Join(NetworkString<_16> name, sbyte character, sbyte color, RpcInfo info = default)
    {
        if (Party != null) Party.HostJoin(info.Source, name.ToString(), character, color);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Emote(sbyte id, RpcInfo info = default) { if (Party != null) Party.ApplyEmote(Party.MemberOf(info.Source), id); }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Chat(sbyte id, RpcInfo info = default) { if (Party != null) Party.ApplyChat(Party.MemberOf(info.Source), id); }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Push(sbyte target, Vector3 dir, RpcInfo info = default) { if (Party != null) Party.ApplyPush(Party.MemberOf(info.Source), target, dir); }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Vote(sbyte choice, RpcInfo info = default)
    {
        if (Party == null) return;
        Party.ApplyVote(Party.MemberOf(info.Source), choice);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Unreliable)]
    public void RPC_Pose(Vector3 pos, float yaw, NetworkBool moving, RpcInfo info = default)
    {
        if (Party == null) return;
        int i = Party.MemberOf(info.Source);
        if (i < 0) return;
        var m = Party.Members[i]; m.Pose = pos; m.Yaw = yaw; m.Moving = moving;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_Report(int round, NetworkBool claimedWin, sbyte winner, RpcInfo info = default)
    {
        if (Party == null) return;
        int member = Party.MemberOf(info.Source);
        if (member >= 0) Party.RegisterReport(member, round, claimedWin, winner);
    }
}
