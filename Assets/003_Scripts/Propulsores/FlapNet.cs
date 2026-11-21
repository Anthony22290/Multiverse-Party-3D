using System.IO;
using UnityEngine;

/// <summary>Propulsores online (PartyLink): the course comes from the seed and the clock; each computer shares its own flyers' height and score, the host keeps the clock and decides the winner.</summary>
public sealed class FlapNet : MonoBehaviour
{
    public const byte ChPose = 70, ChState = 71;
    FlapDirector d; float nextPose, nextState;

    public void Init(FlapDirector director) { d = director; PartyLink.Listen(ChPose, OnPose); PartyLink.Listen(ChState, OnState); PartyLink.Fast(ChPose); PartyLink.MarkReady(); }
    void OnDestroy() => PartyLink.Reset();
    FlapPlayer Of(int member) { int slot = PartyLink.SlotOfMember(member); return slot >= 0 && slot < d.Players.Count ? d.Players[slot] : null; }

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
                w.Write((sbyte)p.Member); w.Write(p.transform.position); w.Write((short)p.Score); w.Write((short)p.Crashes); w.Write(p.ReachedAt);
                w.Write((byte)((p.Down ? 1 : 0) | (p.Shielded ? 2 : 0) | (p.Celebrating ? 4 : 0) | (p.Dead ? 8 : 0))); w.Write((byte)p.Lives); w.Write(p.DeadAt);
            }
            PartyLink.SendToAll();
        }
        if (PartyLink.IsHost && Time.unscaledTime >= nextState) { nextState = Time.unscaledTime + .2f; SendState(); }
    }

    public void SendState()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        var w = PartyLink.Begin(ChState);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnPose(int from, BinaryReader r)
    {
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); var pos = r.ReadVector3(); int score = r.ReadInt16(), crashes = r.ReadInt16(); float reached = r.ReadSingle(); byte f = r.ReadByte(); int lives = r.ReadByte(); float deadAt = r.ReadSingle();
            var p = Of(member); if (p == null || !p.Puppet) continue;
            p.ApplyNet(pos, score, crashes, (f & 1) != 0, (f & 2) != 0, reached, (f & 4) != 0, lives, (f & 8) != 0, deadAt);
        }
    }

    void OnState(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (FlapDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(), elapsed = r.ReadSingle();
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyNet(phase, countdown, elapsed, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
