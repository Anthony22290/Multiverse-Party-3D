using System.IO;
using UnityEngine;

/// <summary>
/// Pico, Pala y Tijera online (PartyLink). The column is the same everywhere (party seed), so each computer only
/// shares how deep its own digger is (the host also sends its bots'), the tool in hand and whether it is dizzy.
/// The host keeps the clock and decides the winner.
/// </summary>
public sealed class MinaNet : MonoBehaviour
{
    public const byte ChProgress = 50, ChState = 51;
    MinaDirector d; float nextSend, nextState;

    public void Init(MinaDirector director)
    {
        d = director;
        PartyLink.Listen(ChProgress, OnProgress);
        PartyLink.Listen(ChState, OnState);
        PartyLink.MarkReady();
    }
    void OnDestroy() => PartyLink.Reset();

    MinaMiner Of(int member) { int slot = PartyLink.SlotOfMember(member); return slot >= 0 && slot < d.Miners.Count ? d.Miners[slot] : null; }

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (Time.unscaledTime >= nextSend)
        {
            nextSend = Time.unscaledTime + 1f / 15f;
            int n = 0; foreach (var m in d.Miners) if (!m.Puppet) n++;
            var w = PartyLink.Begin(ChProgress); w.Write((byte)n);
            foreach (var m in d.Miners)
            {
                if (m.Puppet) continue;
                w.Write((sbyte)m.Member); w.Write((short)m.Depth); w.Write((byte)m.Tool); w.Write((byte)m.Hits);
                w.Write((byte)((m.Stunned ? 1 : 0) | (m.Finished ? 2 : 0))); w.Write(m.FinishedAt); w.Write((short)m.Mistakes);
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

    void OnProgress(int from, BinaryReader r)
    {
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); int depth = r.ReadInt16(); int tool = r.ReadByte(); int hits = r.ReadByte(); byte flags = r.ReadByte(); float at = r.ReadSingle(); r.ReadInt16();
            var m = Of(member); if (m == null || !m.Puppet) continue;
            m.ApplyNet(depth, tool, (flags & 1) != 0, hits, (flags & 2) != 0, at);
        }
    }

    void OnState(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (MinaDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(), elapsed = r.ReadSingle();
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyNet(phase, countdown, elapsed, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
