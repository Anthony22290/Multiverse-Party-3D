using System.IO;
using UnityEngine;

/// <summary>
/// Rey de la Colina online (PartyLink). Poses; bananas eaten by a client's player go to the host; the host sends the
/// clock, the points, the bananas, knock-backs (with their push) and the result. The zone is computed from the seed
/// and the clock on every computer.
/// </summary>
public sealed class ColinaNet : MonoBehaviour
{
    public const byte ChPose = 150, ChWorld = 151, ChBanana = 152;
    ColinaDirector d; float nextPose, nextWorld;

    public void Init(ColinaDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, (f, r) => PartyMover.ReadPoses(r, d.Players));
        PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChBanana, OnBanana);
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

    public void SendBanana(int id) { var w = PartyLink.Begin(ChBanana); w.Write((short)id); PartyLink.SendToHost(); d.RemoveBanana(id); }
    void OnBanana(int from, BinaryReader r) { int id = r.ReadInt16(); if (PartyLink.IsHost) d.HostRemoveBanana(id); }

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
        w.Write((byte)d.Bananas.Count); foreach (var b in d.Bananas) { w.Write((short)b.Id); w.Write(b.Pos); }
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
        int nb = r.ReadByte(); var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < nb; i++) { int id = r.ReadInt16(); var pos = r.ReadVector3(); seen.Add(id); d.AddBanana(id, pos); }
        foreach (var b in d.Bananas.ToArray()) if (!seen.Contains(b.Id)) d.RemoveBanana(b.Id);
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, pts, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
