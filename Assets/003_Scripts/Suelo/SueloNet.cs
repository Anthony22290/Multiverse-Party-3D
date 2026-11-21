using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Suelo que se Cae online (PartyLink). Poses of the players each computer moves; cracked tiles (each computer sends
/// its own, the host passes them on and sends the full list every second); falls into the lava; the host's clock,
/// who is out and the result.
/// </summary>
public sealed class SueloNet : MonoBehaviour
{
    public const byte ChPose = 130, ChTiles = 131, ChOut = 132, ChWorld = 133;
    SueloDirector d; float nextPose, nextWorld, nextFull;

    public void Init(SueloDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, (f, r) => PartyMover.ReadPoses(r, d.Players));
        PartyLink.Listen(ChTiles, OnTiles); PartyLink.Listen(ChOut, OnOut); PartyLink.Listen(ChWorld, OnWorld);
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
            if (d.Pending.Count > 0) SendTiles(d.Pending, false);
        }
        if (!PartyLink.IsHost) return;
        if (d.Dirty || Time.unscaledTime >= nextWorld) SendWorld();
        if (Time.unscaledTime >= nextFull) { nextFull = Time.unscaledTime + 1f; var all = new List<int>(); foreach (var t in d.Tiles) if (t.CrackedAt >= 0f) all.Add(t.Id); if (all.Count > 0) SendTiles(all, true); }
    }

    void SendTiles(List<int> ids, bool full)
    {
        var w = PartyLink.Begin(ChTiles); w.Write((short)ids.Count); foreach (var id in ids) w.Write((short)id);
        if (PartyLink.IsHost) PartyLink.SendToAll(); else PartyLink.SendToHost();
        if (!full) ids.Clear();
    }

    void OnTiles(int from, BinaryReader r)
    {
        int n = r.ReadInt16(); var got = new List<int>(n);
        for (int i = 0; i < n; i++) { int id = r.ReadInt16(); got.Add(id); d.Crack(id, false); }
        if (PartyLink.IsHost && got.Count > 0) { var w = PartyLink.Begin(ChTiles); w.Write((short)got.Count); foreach (var id in got) w.Write((short)id); PartyLink.SendToAll(); }
    }

    public void SendOut(int member) { var w = PartyLink.Begin(ChOut); w.Write((sbyte)member); PartyLink.SendToHost(); }
    void OnOut(int from, BinaryReader r)
    {
        int member = r.ReadSByte(); if (!PartyLink.IsHost) return;
        int slot = PartyLink.SlotOfMember(member); if (slot >= 0 && slot < d.Players.Count) d.HostOut(slot);
    }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .25f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((byte)d.OutOrder.Count); foreach (var s in d.OutOrder) w.Write((sbyte)PartyLink.MemberOfSlot(s));
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (SueloDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); float elapsed = r.ReadSingle();
        int n = r.ReadByte(); var outs = new List<int>();
        for (int i = 0; i < n; i++) { int s = PartyLink.SlotOfMember(r.ReadSByte()); if (s >= 0 && s < d.Players.Count) outs.Add(s); }
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, outs, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
