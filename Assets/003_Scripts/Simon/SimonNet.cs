using System.IO;
using UnityEngine;

/// <summary>
/// Memoria Musical online (PartyLink). The sequence comes from the seed and the board plays it on the shared clock,
/// so only the round state travels: the host sends phase, clock, round, show start, each player's status/progress/
/// time/lives and the result; each computer judges its own player's presses and sends progress and the result.
/// </summary>
public sealed class SimonNet : MonoBehaviour
{
    public const byte ChWorld = 160, ChPress = 161;
    SimonDirector d; float nextWorld;

    public void Init(SimonDirector director) { d = director; PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChPress, OnPress); PartyLink.MarkReady(); }
    void OnDestroy() => PartyLink.Reset();

    void Update()
    {
        if (d == null || !PartyLink.Active || !PartyLink.IsHost) return;
        if (d.Dirty || Time.unscaledTime >= nextWorld) SendWorld();
    }

    public void SendPress(int member, int round, bool ok, int progress, float time, bool partial)
    {
        var w = PartyLink.Begin(ChPress); w.Write((sbyte)member); w.Write((byte)round); w.Write(ok); w.Write((byte)progress); w.Write(time); w.Write(partial); PartyLink.SendToHost();
    }

    void OnPress(int from, BinaryReader r)
    {
        if (!PartyLink.IsHost) return;
        int member = r.ReadSByte(); int round = r.ReadByte(); bool ok = r.ReadBoolean(); int progress = r.ReadByte(); float time = r.ReadSingle(); bool partial = r.ReadBoolean();
        int slot = PartyLink.SlotOfMember(member); if (slot < 0 || slot >= d.Players.Count || !d.Players[slot].Puppet || round != d.Round) return;
        d.Players[slot].Act(ok || partial ? "Attack" : "Hit", ok || partial ? .25f : .8f);
        if (partial) d.HostProgress(slot, progress); else d.HostResult(slot, ok, progress, time);
    }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .2f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed); w.Write((byte)d.Round); w.Write(d.ShowStart); w.Write(d.AllFailedLast);
        w.Write((byte)d.Players.Count);
        foreach (var p in d.Players)
        {
            w.Write((sbyte)p.Member); w.Write((byte)d.Status[p.Slot]); w.Write((byte)d.Progress[p.Slot]); w.Write(d.DoneTime[p.Slot]); w.Write((byte)p.Lives);
        }
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (SimonDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); float elapsed = r.ReadSingle();
        int round = r.ReadByte(); float showStart = r.ReadSingle(); bool allFailed = r.ReadBoolean();
        int c = d.Players.Count;
        var status = (int[])d.Status.Clone(); var progress = (int[])d.Progress.Clone(); var done = (float[])d.DoneTime.Clone(); var lives = new int[c];
        for (int s = 0; s < c; s++) lives[s] = d.Players[s].Lives;
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); int st = r.ReadByte(); int pr = r.ReadByte(); float dt = r.ReadSingle(); int lv = r.ReadByte();
            int slot = PartyLink.SlotOfMember(member); if (slot < 0 || slot >= c) continue;
            status[slot] = st; progress[slot] = pr; done[slot] = dt; lives[slot] = lv;
        }
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, round, showStart, allFailed, status, progress, done, lives, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
