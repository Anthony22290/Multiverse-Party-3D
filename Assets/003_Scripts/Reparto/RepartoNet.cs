using System.IO;
using UnityEngine;

/// <summary>
/// Reparto Loco online (PartyLink). Every computer sends its couriers (position, parcel, stunned); deliveries and
/// crashes go to the host, which counts them and tells everybody; the host sends the clock, the scores and the result.
/// The traffic is not sent: it is computed from the seed and the clock on every computer.
/// </summary>
public sealed class RepartoNet : MonoBehaviour
{
    public const byte ChPose = 110, ChWorld = 111, ChEvent = 112;
    RepartoDirector d; float nextPose, nextWorld;

    public void Init(RepartoDirector director) { d = director; PartyLink.Listen(ChPose, OnPose); PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChEvent, OnEvent); PartyLink.Fast(ChPose); PartyLink.MarkReady(); }
    void OnDestroy() => PartyLink.Reset();
    RepartoPlayer Of(int member) { int slot = PartyLink.SlotOfMember(member); return slot >= 0 && slot < d.Players.Count ? d.Players[slot] : null; }

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextPose)
        {
            nextPose = Time.unscaledTime + 1f / PartyLink.SendRate;
            int n = 0; foreach (var p in d.Players) if (!p.Puppet) n++;
            var w = PartyLink.Begin(ChPose); w.Write((byte)n);
            foreach (var p in d.Players)
            {
                if (p.Puppet) continue;
                w.Write((sbyte)p.Member); w.Write(p.transform.position); w.Write(p.transform.eulerAngles.y);
                w.Write((byte)((p.Stunned ? 1 : 0) | (p.Celebrating ? 2 : 0) | (p.Dashing ? 4 : 0) | (p.Carry > 0 ? 8 : 0) | (p.Carry < 0 ? 16 : 0)));
            }
            PartyLink.SendToAll();
        }
        if (PartyLink.IsHost && (d.Dirty || Time.unscaledTime >= nextWorld)) SendWorld();
    }

    public void SendEvent(int member, int kind) { var w = PartyLink.Begin(ChEvent); w.Write((sbyte)member); w.Write((byte)kind); PartyLink.SendToHost(); }
    public void Broadcast(int member, int kind) { if (!PartyLink.Active || !PartyLink.IsHost) return; var w = PartyLink.Begin(ChEvent); w.Write((sbyte)member); w.Write((byte)kind); PartyLink.SendToAll(); }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .2f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((byte)d.Players.Count);
        foreach (var p in d.Players) { w.Write((sbyte)p.Member); w.Write((short)d.Scores[p.Slot]); w.Write((short)d.Breaks[p.Slot]); }
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnPose(int from, BinaryReader r)
    {
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); var pos = r.ReadVector3(); float yaw = r.ReadSingle(); byte flags = r.ReadByte();
            var p = Of(member); if (p == null || !p.Puppet) continue;
            p.Net.Add(pos, Quaternion.Euler(0, yaw, 0));
            if ((flags & 1) != 0 && !p.Stunned) p.ShowStun();
            if ((flags & 4) != 0) p.ShowDash();
            p.Celebrating = (flags & 2) != 0;
            p.Carry = (flags & 8) != 0 ? 1 : (flags & 16) != 0 ? -1 : 0;
        }
    }

    void OnEvent(int from, BinaryReader r)
    {
        int member = r.ReadSByte(); int kind = r.ReadByte(); var p = Of(member); if (p == null) return;
        if (PartyLink.IsHost)
        {
            if (!p.Puppet) return;                       // our own events were applied already
            if (kind == 1) d.HostDelivered(p.Slot); else d.HostHit(p.Slot, kind == 2, true);
            Broadcast(member, kind);
        }
        else d.ShowRemote(p.Slot, kind);
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (RepartoDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); float elapsed = r.ReadSingle();
        int n = r.ReadByte(); var scores = (int[])d.Scores.Clone(); var breaks = (int[])d.Breaks.Clone();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); int sc = r.ReadInt16(); int br = r.ReadInt16();
            int slot = PartyLink.SlotOfMember(member); if (slot < 0 || slot >= scores.Length) continue;
            scores[slot] = sc; breaks[slot] = br;
        }
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, scores, breaks, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
