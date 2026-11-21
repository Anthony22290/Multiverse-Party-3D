using System.IO;
using UnityEngine;

/// <summary>
/// Pintura Loca online (PartyLink). Poses; each client's fresh paint goes to the host; the host sends the whole floor
/// four times a second (one byte per tile), the buckets, splashes, knock-backs, the clock and the result.
/// </summary>
public sealed class PinturaNet : MonoBehaviour
{
    public const byte ChPose = 140, ChPaint = 141, ChWorld = 142, ChBucket = 143, ChSplash = 144;
    PinturaDirector d; float nextPose, nextWorld;

    public void Init(PinturaDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, (f, r) => PartyMover.ReadPoses(r, d.Players));
        PartyLink.Listen(ChPaint, OnPaint); PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChBucket, OnBucket); PartyLink.Listen(ChSplash, OnSplash);
        PartyLink.Fast(ChPose); PartyLink.MarkReady();
    }
    void OnDestroy() => PartyLink.Reset();

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextPose)
        {
            nextPose = Time.unscaledTime + 1f / PartyLink.SendRate;
            var w = PartyLink.Begin(ChPose); PartyMover.WritePoses(w, d.Players); PartyLink.SendToAll();
            if (!PartyLink.IsHost && d.PendingPaint.Count > 0)
            {
                var pw = PartyLink.Begin(ChPaint); pw.Write((short)d.PendingPaint.Count);
                foreach (var (tile, member) in d.PendingPaint) { pw.Write((short)tile); pw.Write((sbyte)member); }
                PartyLink.SendToHost(); d.PendingPaint.Clear();
            }
        }
        if (PartyLink.IsHost && (Time.unscaledTime >= nextWorld || (d.Dirty && Time.unscaledTime >= nextWorld - .15f))) SendWorld();
    }

    void OnPaint(int from, BinaryReader r)
    {
        if (!PartyLink.IsHost) return;
        int n = r.ReadInt16();
        for (int i = 0; i < n; i++) { int t = r.ReadInt16(); int m = r.ReadSByte(); if (d.State == PinturaDirector.Phase.Playing) d.SetTile(t, m); }
        d.Dirty = true;
    }

    public void SendBucket(int id, int member) { var w = PartyLink.Begin(ChBucket); w.Write((short)id); w.Write((sbyte)member); PartyLink.SendToHost(); }
    void OnBucket(int from, BinaryReader r)
    {
        int id = r.ReadInt16(); int member = r.ReadSByte(); if (!PartyLink.IsHost) return;
        int slot = PartyLink.SlotOfMember(member); d.HostTakeBucket(id, slot);
    }

    public void SendSplash(Vector3 at, int member) { if (!PartyLink.Active || !PartyLink.IsHost) return; var w = PartyLink.Begin(ChSplash); w.Write(at); w.Write((sbyte)member); PartyLink.SendToAll(); }
    void OnSplash(int from, BinaryReader r)
    {
        var at = r.ReadVector3(); int member = r.ReadSByte(); if (PartyLink.IsHost) return;
        var p = d.OwnerOf(member); if (p != null) d.SplashFx(at, p);
    }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .25f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write(d.Grid);
        w.Write((byte)d.Buckets.Count); foreach (var b in d.Buckets) { w.Write((short)b.Id); w.Write(b.Pos); }
        w.Write((byte)d.Players.Count); foreach (var p in d.Players) { w.Write((sbyte)p.Member); w.Write((short)d.KnockSeq[p.Slot]); }
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (PinturaDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); float elapsed = r.ReadSingle();
        var grid = r.ReadBytes(PinturaDirector.N * PinturaDirector.N); d.SetGrid(grid);
        int nb = r.ReadByte(); var seen = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < nb; i++) { int id = r.ReadInt16(); var pos = r.ReadVector3(); seen.Add(id); d.AddBucket(id, pos); }
        foreach (var b in d.Buckets.ToArray()) if (!seen.Contains(b.Id)) d.RemoveBucket(b.Id);
        int np = r.ReadByte();
        for (int i = 0; i < np; i++) { int m = r.ReadSByte(); int seq = r.ReadInt16(); int s = PartyLink.SlotOfMember(m); if (s >= 0 && s < d.Players.Count) d.ApplyKnock(s, seq); }
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
