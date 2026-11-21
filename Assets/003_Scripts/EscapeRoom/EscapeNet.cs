using System.IO;
using UnityEngine;

/// <summary>
/// Sala de Escape online — CARRERA (2026-11-17). Each computer plays its own copy of the tower, so the only things
/// shared are: the start (the host waits for everybody and counts down), each player's progress (rooms solved,
/// finished and time) and the end of the race (the host's winner is final).
/// </summary>
public sealed class EscapeNet : MonoBehaviour
{
    const byte ChState = 51, ChProgress = 53, ChEnd = 54;

    EscapeDirector d;
    bool host;
    float nextState, nextProgress;

    public void Init(EscapeDirector director)
    {
        d = director; host = PartyLink.IsHost;
        PartyLink.Listen(ChState, OnState);
        PartyLink.Listen(ChProgress, OnProgress);
        PartyLink.Listen(ChEnd, OnEnd);
        PartyLink.MarkReady();
    }

    void OnDestroy() => PartyLink.Reset();

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (host && Time.unscaledTime >= nextState && !d.RaceOver)
        {
            nextState = Time.unscaledTime + .2f;
            var w = PartyLink.Begin(ChState);
            w.Write(d.RaceStarted);
            w.Write(PartyLink.AllReady ? d.Countdown : 99f);
            PartyLink.SendToAll();
        }
        if (d.RaceStarted && Time.unscaledTime >= nextProgress) SendProgress();
    }

    public void SendProgress()
    {
        if (d == null || d.Me == null || !PartyLink.Active) return;
        nextProgress = Time.unscaledTime + 1f;
        var w = PartyLink.Begin(ChProgress);
        w.Write((sbyte)PartyLink.LocalMember);
        w.Write((byte)d.Me.Rooms);
        w.Write(d.Me.Finished);
        w.Write(d.Me.Time);
        PartyLink.SendToAll();
    }

    public void SendEnd(int winnerMember)
    {
        var w = PartyLink.Begin(ChEnd); w.Write((sbyte)winnerMember); PartyLink.SendToAll();
    }

    void OnState(int from, BinaryReader r)
    {
        if (host) return;
        bool started = r.ReadBoolean(); float countdown = r.ReadSingle();
        d.ApplyNet(started, countdown);
    }

    void OnProgress(int from, BinaryReader r)
    {
        int member = r.ReadSByte(); int rooms = r.ReadByte(); bool finished = r.ReadBoolean(); float time = r.ReadSingle();
        if (member == PartyLink.LocalMember) return;
        d.OnRemoteProgress(member, rooms, finished, time);
    }

    void OnEnd(int from, BinaryReader r)
    {
        int winner = r.ReadSByte();
        if (!host) d.OnRaceOver(winner);
    }
}
