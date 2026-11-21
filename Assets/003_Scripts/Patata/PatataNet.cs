using System.IO;
using UnityEngine;

/// <summary>
/// Patata Caliente online (PartyLink). Every computer sends the pose of the players it moves; the host decides the
/// fuse, the passes and the explosions and sends the state (phase, round, clock, holder, who is still in).
/// </summary>
public sealed class PatataNet : MonoBehaviour
{
    public const byte ChPose = 90, ChWorld = 91;
    PatataDirector d; float nextPose, nextWorld;

    public void Init(PatataDirector director) { d = director; PartyLink.Listen(ChPose, OnPose); PartyLink.Listen(ChWorld, OnWorld); PartyLink.Fast(ChPose); PartyLink.MarkReady(); }
    void OnDestroy() => PartyLink.Reset();
    PatataPlayer Of(int member) { int slot = PartyLink.SlotOfMember(member); return slot >= 0 && slot < d.Players.Count ? d.Players[slot] : null; }

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextPose)
        {
            nextPose = Time.unscaledTime + 1f / PartyLink.SendRate;
            int n = 0; foreach (var p in d.Players) if (!p.Puppet && p.Alive) n++;
            var w = PartyLink.Begin(ChPose); w.Write((byte)n);
            foreach (var p in d.Players)
            {
                if (p.Puppet || !p.Alive) continue;
                w.Write((sbyte)p.Member); w.Write(p.transform.position); w.Write(p.transform.eulerAngles.y);
                w.Write((byte)((p.Stunned ? 1 : 0) | (p.Celebrating ? 2 : 0) | (p.Dashing ? 4 : 0)));
            }
            PartyLink.SendToAll();
        }
        if (PartyLink.IsHost && (d.Dirty || Time.unscaledTime >= nextWorld)) SendWorld();
    }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .2f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write((byte)d.Round); w.Write(d.RoundTime);
        w.Write((sbyte)(d.HolderSlot >= 0 ? PartyLink.MemberOfSlot(d.HolderSlot) : -1));
        w.Write((byte)d.Players.Count);
        foreach (var p in d.Players) { w.Write((sbyte)p.Member); w.Write(p.Alive); }
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnPose(int from, BinaryReader r)
    {
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); var pos = r.ReadVector3(); float yaw = r.ReadSingle(); byte flags = r.ReadByte();
            var p = Of(member); if (p == null || !p.Puppet || !p.Alive) continue;
            p.Net.Add(pos, Quaternion.Euler(0, yaw, 0));
            if ((flags & 1) != 0 && !p.Stunned) p.ShowStun();
            if ((flags & 4) != 0) p.ShowDash();
            p.Celebrating = (flags & 2) != 0;
        }
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (PatataDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); int round = r.ReadByte(); float roundTime = r.ReadSingle();
        int holder = r.ReadSByte();
        int n = r.ReadByte(); var alive = new bool[d.Players.Count]; for (int i = 0; i < alive.Length; i++) alive[i] = d.Players[i].Alive;
        for (int i = 0; i < n; i++) { int member = r.ReadSByte(); bool a = r.ReadBoolean(); int slot = PartyLink.SlotOfMember(member); if (slot >= 0 && slot < alive.Length) alive[slot] = a; }
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, round, roundTime, holder >= 0 ? PartyLink.SlotOfMember(holder) : -1, alive, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
