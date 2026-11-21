using System.IO;
using UnityEngine;

/// <summary>
/// Arma tu Nave online (PartyLink). Every computer sends the pose of the players it moves; clients ask the host to
/// pick, drop and shove; the host owns the pieces and the ships and sends the whole world (clock, every piece, result).
/// </summary>
public sealed class NaveNet : MonoBehaviour
{
    public const byte ChPose = 60, ChRequest = 61, ChWorld = 62, ChEvent = 63;
    const byte ReqPick = 1, ReqDrop = 2, ReqShove = 3, EvKnock = 1, EvShove = 2;
    NaveDirector d; float nextPose, nextWorld;

    public void Init(NaveDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, OnPose); PartyLink.Listen(ChRequest, OnRequest); PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChEvent, OnEvent);
        PartyLink.MarkReady();
    }
    void OnDestroy() => PartyLink.Reset();
    NavePlayer Of(int member) { int slot = PartyLink.SlotOfMember(member); return slot >= 0 && slot < d.Players.Count ? d.Players[slot] : null; }

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextPose)
        {
            nextPose = Time.unscaledTime + 1f / PartyLink.SendRate;
            int n = 0; foreach (var p in d.Players) if (!p.Puppet) n++;
            var w = PartyLink.Begin(ChPose); w.Write((byte)n);
            foreach (var p in d.Players) { if (p.Puppet) continue; w.Write((sbyte)p.Member); w.Write(p.transform.position); w.Write(p.transform.eulerAngles.y); w.Write((byte)((p.Stunned ? 1 : 0) | (p.Celebrating ? 2 : 0))); }
            PartyLink.SendToAll();
        }
        if (PartyLink.IsHost && (d.Dirty || Time.unscaledTime >= nextWorld)) SendWorld();
    }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .2f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        w.Write((byte)d.Pieces.Count);
        foreach (var p in d.Pieces)
        {
            w.Write((byte)p.Is); w.Write((sbyte)(p.HolderSlot >= 0 ? PartyLink.MemberOfSlot(p.HolderSlot) : -1));
            if (p.Is == NavePiece.State.Free) w.Write(p.transform.position);
        }
        PartyLink.SendToAll();
    }

    public void SendPick(int pieceId) { var w = PartyLink.Begin(ChRequest); w.Write(ReqPick); w.Write((byte)pieceId); PartyLink.SendToHost(); }
    public void SendDrop() { var w = PartyLink.Begin(ChRequest); w.Write(ReqDrop); PartyLink.SendToHost(); }
    public void SendShove(int targetMember, Vector3 dir) { var w = PartyLink.Begin(ChRequest); w.Write(ReqShove); w.Write((sbyte)targetMember); w.Write(dir); PartyLink.SendToHost(); }
    public void SendKnock(int member, Vector3 dir) { var w = PartyLink.Begin(ChEvent); w.Write(EvKnock); w.Write((sbyte)member); w.Write(dir); PartyLink.SendToAll(); }
    public void SendShoveFx(int member) { var w = PartyLink.Begin(ChEvent); w.Write(EvShove); w.Write((sbyte)member); w.Write(Vector3.zero); PartyLink.SendToAll(); }

    void OnPose(int from, BinaryReader r)
    {
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); var pos = r.ReadVector3(); float yaw = r.ReadSingle(); byte flags = r.ReadByte();
            var p = Of(member); if (p == null || !p.Puppet) continue;
            p.Net.Add(pos, Quaternion.Euler(0, yaw, 0));
            if ((flags & 1) != 0 && !p.Stunned) p.ShowStun();
            p.Celebrating = (flags & 2) != 0;
        }
    }

    void OnRequest(int from, BinaryReader r)
    {
        if (!PartyLink.IsHost) return;
        int slot = PartyLink.SlotOfMember(from); if (slot < 0) return;
        byte kind = r.ReadByte();
        if (kind == ReqPick) d.HostPick(slot, r.ReadByte());
        else if (kind == ReqDrop) d.HostDrop(slot, Vector3.zero);
        else if (kind == ReqShove) { int target = PartyLink.SlotOfMember(r.ReadSByte()); var dir = r.ReadVector3(); d.HostShove(slot, target, dir); }
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (NaveDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(), elapsed = r.ReadSingle();
        int winner = r.ReadSByte(); string result = r.ReadString();
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            var state = (NavePiece.State)r.ReadByte(); int holder = r.ReadSByte(); Vector3 pos = state == NavePiece.State.Free ? r.ReadVector3() : Vector3.zero;
            d.ApplyPiece(i, state, holder >= 0 ? PartyLink.SlotOfMember(holder) : -1, pos);
        }
        d.ApplyWorld(phase, countdown, elapsed, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }

    void OnEvent(int from, BinaryReader r)
    {
        byte kind = r.ReadByte(); int member = r.ReadSByte(); var dir = r.ReadVector3();
        var p = Of(member); if (p == null) return;
        if (kind == EvKnock && !p.Puppet) p.Knock(dir);
        else if (kind == EvShove && p.Puppet) p.PlayShoveAnim();
    }
}
