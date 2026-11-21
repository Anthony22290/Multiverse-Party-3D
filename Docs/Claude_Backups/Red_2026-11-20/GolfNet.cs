using System.IO;
using UnityEngine;

/// <summary>
/// Minigolf Loco online (PartyLink). Each computer rolls its own ball and shares where it is, the aim, the strokes and
/// whether it is in the cup; the host keeps the phase, the hole clock and the score card.
/// </summary>
public sealed class GolfNet : MonoBehaviour
{
    public const byte ChPose = 80, ChState = 81;
    GolfDirector d; float nextPose, nextState;

    public void Init(GolfDirector director) { d = director; PartyLink.Listen(ChPose, OnPose); PartyLink.Listen(ChState, OnState); PartyLink.MarkReady(); }
    void OnDestroy() => PartyLink.Reset();
    GolfPlayer Of(int member) { int slot = PartyLink.SlotOfMember(member); return slot >= 0 && slot < d.Players.Count ? d.Players[slot] : null; }

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextPose)
        {
            nextPose = Time.unscaledTime + 1f / PartyLink.SendRate;
            int n = 0; foreach (var p in d.Players) if (!p.Puppet) n++;
            var w = PartyLink.Begin(ChPose); w.Write((byte)n); w.Write((sbyte)d.HoleIndex);
            foreach (var p in d.Players) { if (p.Puppet) continue; w.Write((sbyte)p.Member); w.Write(p.Ball.position); w.Write(p.Aim); w.Write((byte)p.State); w.Write((byte)p.Strokes); }
            PartyLink.SendToAll();
        }
        if (PartyLink.IsHost && Time.unscaledTime >= nextState) { nextState = Time.unscaledTime + .25f; SendState(); }
    }

    public void SendState()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        var w = PartyLink.Begin(ChState);
        w.Write((byte)d.State); w.Write((sbyte)d.HoleIndex); w.Write(d.PhaseTime); w.Write(d.FirstHoledAt);
        w.Write((byte)d.Players.Count);
        // Card by member so every computer can map it to its own slots.
        for (int s = 0; s < d.Players.Count; s++) { w.Write((sbyte)d.Players[s].Member); for (int h = 0; h < GolfDirector.Holes; h++) w.Write((sbyte)d.Card[s, h]); }
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnPose(int from, BinaryReader r)
    {
        int n = r.ReadByte(); int hole = r.ReadSByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); var pos = r.ReadVector3(); float aim = r.ReadSingle(); var st = (GolfPlayer.Phase)r.ReadByte(); int strokes = r.ReadByte();
            if (hole != d.HoleIndex) continue;
            var p = Of(member); if (p == null || !p.Puppet) continue;
            p.ApplyNet(pos, aim, st, strokes);
        }
    }

    void OnState(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (GolfDirector.Phase)r.ReadByte(); int hole = r.ReadSByte(); float t = r.ReadSingle(), first = r.ReadSingle();
        int n = r.ReadByte(); var card = new int[d.Players.Count * GolfDirector.Holes];
        for (int i = 0; i < card.Length; i++) card[i] = -1;
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); int slot = PartyLink.SlotOfMember(member);
            for (int h = 0; h < GolfDirector.Holes; h++) { int v = r.ReadSByte(); if (slot >= 0 && slot < d.Players.Count) card[slot * GolfDirector.Holes + h] = v; }
        }
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyNet(phase, hole, t, first, card, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
