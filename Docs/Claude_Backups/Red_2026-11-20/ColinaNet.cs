using System.IO;
using UnityEngine;

/// <summary>
/// Rey de la Colina online (PartyLink). Poses; bombs are sent by the computer that throws them and simulated on every
/// computer (each one pushes only its own players); the host sends the clock, points, flag owner, shoves (with their
/// push) and the result.
/// </summary>
public sealed class ColinaNet : MonoBehaviour
{
    public const byte ChPose = 150, ChWorld = 151, ChBomb = 152;
    ColinaDirector d; float nextPose, nextWorld;

    public void Init(ColinaDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, (f, r) => PartyMover.ReadPoses(r, d.Players));
        PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChBomb, OnBomb);
        PartyLink.MarkReady();
    }
    void OnDestroy() => PartyLink.Reset();

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextPose)
        {
            nextPose = Time.unscaledTime + 1f / PartyLink.SendRate;
            var w = PartyLink.Begin(ChPose); PartyMover.WritePoses(w, d.Players); PartyLink.SendToAll();
        }
        if (PartyLink.IsHost && (d.Dirty || Time.unscaledTime >= nextWorld)) SendWorld();
    }

    public void SendBomb(int id, int ownerSlot, Vector3 origin, Vector3 vel)
    {
        var w = PartyLink.Begin(ChBomb); w.Write(id); w.Write((sbyte)PartyLink.MemberOfSlot(ownerSlot)); w.Write(origin); w.Write(vel); PartyLink.SendToAll();
    }
    void OnBomb(int from, BinaryReader r)
    {
        int id = r.ReadInt32(); int member = r.ReadSByte(); var origin = r.ReadVector3(); var vel = r.ReadVector3();
        d.SpawnBomb(id, PartyLink.SlotOfMember(member), origin, vel);
    }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .2f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((byte)d.Players.Count);
        foreach (var p in d.Players)
        {
            w.Write((sbyte)p.Member); w.Write(d.Points[p.Slot]); w.Write((short)d.KnockSeq[p.Slot]);
            d.pendingKnocks.TryGetValue(p.Slot, out var push); w.Write(push);
        }
        w.Write((sbyte)(d.FlagOwner >= 0 ? PartyLink.MemberOfSlot(d.FlagOwner) : -1));
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (ColinaDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); float elapsed = r.ReadSingle();
        int n = r.ReadByte(); var pts = (float[])d.Points.Clone();
        for (int i = 0; i < n; i++)
        {
            int m = r.ReadSByte(); float pt = r.ReadSingle(); int seq = r.ReadInt16(); var push = r.ReadVector3();
            int s = PartyLink.SlotOfMember(m); if (s < 0 || s >= pts.Length) continue;
            pts[s] = pt; d.ApplyKnock(s, seq, push);
        }
        int owner = r.ReadSByte(); int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, pts, owner >= 0 ? PartyLink.SlotOfMember(owner) : -1, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
