using System.IO;
using UnityEngine;

/// <summary>
/// Rodillos del Calabozo online (PartyLink). The rollers and the falling floor are a pure function of the party seed and
/// the clock, so the host only shares the clock and the result. Every computer simulates its own player (and the host
/// its bots), detects its own hits and tells the others; everybody else is a smoothed puppet.
/// </summary>
public sealed class RodillosNet : MonoBehaviour
{
    public const byte ChPose = 40, ChState = 41, ChEvent = 42;
    public const byte EvHit = 1, EvOut = 2, EvLava = 3;
    RodillosDirector d;
    float nextPose, nextState;

    public void Init(RodillosDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, OnPose);
        PartyLink.Listen(ChState, OnState);
        PartyLink.Listen(ChEvent, OnEvent);
        PartyLink.MarkReady();
    }

    void OnDestroy() => PartyLink.Reset();

    RodillosRunner Of(int member)
    {
        int slot = PartyLink.SlotOfMember(member);
        return slot >= 0 && slot < d.Runners.Count ? d.Runners[slot] : null;
    }

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextPose)
        {
            nextPose = Time.unscaledTime + 1f / PartyLink.SendRate;
            int n = 0; foreach (var r in d.Runners) if (!r.Puppet) n++;
            var w = PartyLink.Begin(ChPose); w.Write((byte)n);
            foreach (var r in d.Runners)
            {
                if (r.Puppet) continue;
                w.Write((sbyte)r.Member); w.Write(r.transform.position); w.Write(r.transform.eulerAngles.y);
                byte flags = (byte)((r.Alive ? 1 : 0) | (r.Ducking ? 2 : 0) | (r.Grounded ? 4 : 0) | (r.Stunned ? 8 : 0) | (r.Celebrating ? 16 : 0));
                w.Write(flags); w.Write((byte)Mathf.Max(0, r.Lives)); w.Write((byte)r.Hits);
            }
            PartyLink.SendToAll();
        }
        if (PartyLink.IsHost && Time.unscaledTime >= nextState)
        {
            nextState = Time.unscaledTime + .2f;
            SendState();
        }
    }

    public void SendState()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        var w = PartyLink.Begin(ChState);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    public void SendEvent(RodillosRunner r, byte kind, Vector3 knock)
    {
        if (!PartyLink.Active) return;
        var w = PartyLink.Begin(ChEvent);
        w.Write(kind); w.Write((sbyte)r.Member); w.Write((byte)Mathf.Max(0, r.Lives)); w.Write(d.Elapsed); w.Write(knock);
        PartyLink.SendToAll();
    }

    void OnPose(int from, BinaryReader rd)
    {
        int n = rd.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = rd.ReadSByte(); var pos = rd.ReadVector3(); float yaw = rd.ReadSingle(); byte flags = rd.ReadByte(); int lives = rd.ReadByte(); int hits = rd.ReadByte();
            var r = Of(member); if (r == null || !r.Puppet) continue;
            r.ApplyNet(pos, yaw, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0, lives, hits, (flags & 16) != 0);
        }
    }

    void OnState(int from, BinaryReader rd)
    {
        if (PartyLink.IsHost) return;
        var phase = (RodillosDirector.Phase)rd.ReadByte(); float countdown = rd.ReadSingle(), elapsed = rd.ReadSingle();
        int winner = rd.ReadSByte(); string result = rd.ReadString();
        d.ApplyNet(phase, countdown, elapsed, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }

    void OnEvent(int from, BinaryReader rd)
    {
        byte kind = rd.ReadByte(); int member = rd.ReadSByte(); int lives = rd.ReadByte(); float at = rd.ReadSingle(); var knock = rd.ReadVector3();
        var r = Of(member); if (r == null || !r.Puppet) return;
        r.NetEvent(kind, lives, at, knock);
    }
}
