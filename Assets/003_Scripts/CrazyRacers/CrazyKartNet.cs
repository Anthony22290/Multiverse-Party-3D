using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Carreras Locas online (2026-11-12). Each computer drives its own kart (no input lag) and sends its state;
/// the host drives the bots. Everybody sees the others as smooth puppets. Items cross the network as events:
/// pulses and traps are spawned on every computer, a storm strikes the karts each computer owns, and a box
/// taken by one kart disappears for all. The host starts the countdown once everybody has loaded and decides
/// when the race ends and who wins.
/// </summary>
public sealed class CrazyKartNet : MonoBehaviour
{
    const byte ChKarts = 20, ChEvent = 21;
    const byte EvStart = 1, EvEnd = 2, EvHazard = 3, EvStorm = 4, EvBox = 5;
    public const float EndAfterFirst = 30f;

    CrazyKartDirector d;
    float nextSend, firstFinishAt = -1;
    CrazyKartItemBox[] boxes = new CrazyKartItemBox[0];
    public bool Started { get; private set; }
    public bool Ended { get; private set; }

    public void Init(CrazyKartDirector director)
    {
        d = director;
        boxes = FindObjectsByType<CrazyKartItemBox>(FindObjectsSortMode.None)
            .OrderBy(b => Mathf.Round(b.transform.position.x * 10)).ThenBy(b => Mathf.Round(b.transform.position.z * 10)).ThenBy(b => b.transform.position.y).ToArray();
        PartyLink.Listen(ChKarts, OnKarts);
        PartyLink.Listen(ChEvent, OnEvent);
        PartyLink.Fast(ChKarts); PartyLink.MarkReady();
    }

    void OnDestroy() => PartyLink.Reset();

    CrazyKartController KartOf(int member) => d.KartOfSlot(PartyLink.SlotOfMember(member));
    int MemberOf(CrazyKartController k) => PartyLink.MemberOfSlot(d.SlotOf(k));

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (PartyLink.IsHost)
        {
            if (!Started && PartyLink.AllReady)
            {
                var w = PartyLink.Begin(ChEvent); w.Write(EvStart); PartyLink.SendToAll();
                Started = true;
            }
            if (Started && !Ended) CheckEnd();
        }
        if (Time.unscaledTime >= nextSend) { nextSend = Time.unscaledTime + 1f / PartyLink.SendRate; SendOwned(); }
    }

    /// <summary>Host: the race ends when every human has crossed the line, every kart has, or 30 s after the first finisher.</summary>
    void CheckEnd()
    {
        var finished = d.Karts.Where(k => k.HasFinished).ToList();
        if (finished.Count == 0) return;
        if (firstFinishAt < 0) firstFinishAt = Time.time;
        bool humansDone = d.Karts.Where(k => k.IsHuman || (k.Puppet && PartyLink.IsRemoteHuman(d.SlotOf(k)))).All(k => k.HasFinished);
        if (humansDone || finished.Count == d.Karts.Count || Time.time - firstFinishAt > EndAfterFirst)
        {
            var winner = d.Finishers.Count > 0 ? d.Finishers[0] : null;
            int member = winner != null ? MemberOf(winner) : -1;
            var w = PartyLink.Begin(ChEvent); w.Write(EvEnd); w.Write((sbyte)member); PartyLink.SendToAll();
            End(member);
        }
    }

    void End(int winnerMember)
    {
        if (Ended) return;
        Ended = true;
        d.EndRace();
        PartyLink.FinishMember(winnerMember);
    }

    // ------------------------------------------------------------------ kart states

    void SendOwned()
    {
        var owned = d.Karts.Where(k => !k.Puppet && d.SlotOf(k) >= 0).ToList();
        if (owned.Count == 0) return;
        var w = PartyLink.Begin(ChKarts);
        w.Write((byte)owned.Count);
        foreach (var k in owned)
        {
            w.Write((sbyte)MemberOf(k));
            k.WriteNet(w);
        }
        PartyLink.SendToAll();
    }

    void OnKarts(int from, BinaryReader r)
    {
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte();
            var k = KartOf(member);
            if (k == null || !k.Puppet) { CrazyKartController.SkipNet(r); continue; }
            bool wasFinished = k.HasFinished;
            k.ReadNet(r);
            if (!wasFinished && k.HasFinished) d.Finish(k);
        }
    }

    // ------------------------------------------------------------------ item events

    public void SendHazard(CrazyKartController k, bool trap, Vector3 position, Vector3 forward)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvHazard); w.Write((sbyte)MemberOf(k)); w.Write(trap); w.Write(position); w.Write(forward);
        PartyLink.SendToAll();
    }
    public void SendStorm(CrazyKartController k)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvStorm); w.Write((sbyte)MemberOf(k)); w.Write(d.Progress(k));
        PartyLink.SendToAll();
    }
    public void SendBox(CrazyKartItemBox box)
    {
        int index = System.Array.IndexOf(boxes, box);
        if (index < 0) return;
        var w = PartyLink.Begin(ChEvent); w.Write(EvBox); w.Write((short)index);
        PartyLink.SendToAll();
    }

    void OnEvent(int from, BinaryReader r)
    {
        byte type = r.ReadByte();
        switch (type)
        {
            case EvStart:
                Started = true;
                break;
            case EvEnd:
                End(r.ReadSByte());
                break;
            case EvHazard:
            {
                var k = KartOf(r.ReadSByte()); bool trap = r.ReadBoolean(); Vector3 p = r.ReadVector3(), f = r.ReadVector3();
                if (k != null && k.Puppet) CrazyKartHazard.Spawn(k, trap, p, f);
                break;
            }
            case EvStorm:
            {
                var caster = KartOf(r.ReadSByte()); float progress = r.ReadSingle();
                if (caster == null || !caster.Puppet) break;
                caster.MarkStorm();
                foreach (var k in d.Karts)
                    if (k != caster && !k.Puppet && !k.HasFinished && d.Progress(k) > progress) k.StruckByLightning();
                break;
            }
            case EvBox:
            {
                int index = r.ReadInt16();
                if (index >= 0 && index < boxes.Length && boxes[index] != null) boxes[index].Hide();
                break;
            }
        }
    }
}
