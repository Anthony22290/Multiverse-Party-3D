using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Gallinas Locas online (PartyLink). Every computer sends the pose of the farmers it moves; clients ask the host to
/// grab/drop; the host moves every chicken and sends the state (phase, clock, scores, explosions) and the chickens.
/// </summary>
public sealed class GranjaNet : MonoBehaviour
{
    public const byte ChPose = 100, ChWorld = 101, ChGrab = 102, ChFlock = 103;
    GranjaDirector d; float nextPose, nextWorld, nextFlock;

    public void Init(GranjaDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, OnPose); PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChGrab, OnGrab); PartyLink.Listen(ChFlock, OnFlock);
        PartyLink.Fast(ChPose, ChFlock); PartyLink.MarkReady();
    }
    void OnDestroy() => PartyLink.Reset();
    GranjaPlayer Of(int member) { int slot = PartyLink.SlotOfMember(member); return slot >= 0 && slot < d.Players.Count ? d.Players[slot] : null; }

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
                w.Write((byte)((p.Stunned ? 1 : 0) | (p.Celebrating ? 2 : 0) | (p.Dashing ? 4 : 0)));
            }
            PartyLink.SendToAll();
        }
        if (!PartyLink.IsHost) return;
        if (d.Dirty || Time.unscaledTime >= nextWorld) SendWorld();
        if (Time.unscaledTime >= nextFlock) SendFlock();
    }

    public void SendGrab(int member) { var w = PartyLink.Begin(ChGrab); w.Write((sbyte)member); PartyLink.SendToHost(); }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .25f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((byte)d.Players.Count);
        foreach (var p in d.Players) { w.Write((sbyte)p.Member); w.Write((short)d.Scores[p.Slot]); w.Write((byte)d.Golds[p.Slot]); w.Write((byte)d.Booms[p.Slot]); w.Write((byte)d.KnockSeq[p.Slot]); }
        w.Write((short)d.BoomSeq);
        int bp = d.LastBoomPen; w.Write((sbyte)(bp >= 0 ? bp : -1 - PartyLink.MemberOfSlot(-1 - bp)));   // pen, or -1-member for "in someone's hands"
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
        SendFlock();
    }

    void SendFlock()
    {
        nextFlock = Time.unscaledTime + .1f;
        var w = PartyLink.Begin(ChFlock);
        w.Write((short)d.Chickens.Count);
        foreach (var c in d.Chickens)
        {
            var p = c.transform.position;
            w.Write((ushort)c.Id); w.Write((byte)c.Type); w.Write((byte)c.Mode); w.Write((sbyte)c.Pen);
            w.Write((sbyte)(c.Carrier >= 0 ? PartyLink.MemberOfSlot(c.Carrier) : -1));
            w.Write((short)Mathf.RoundToInt(p.x * 100f)); w.Write((short)Mathf.RoundToInt(p.y * 100f)); w.Write((short)Mathf.RoundToInt(p.z * 100f));
            w.Write((byte)Mathf.RoundToInt(c.transform.eulerAngles.y / 360f * 255f));
        }
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
        }
    }

    void OnGrab(int from, BinaryReader r)
    {
        if (!PartyLink.IsHost) return;
        int member = r.ReadSByte(); var p = Of(member); if (p == null) return;
        d.HostGrab(p.Slot);
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (GranjaDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); float elapsed = r.ReadSingle();
        int n = r.ReadByte(); int count = d.Players.Count;
        var scores = (int[])d.Scores.Clone(); var golds = (int[])d.Golds.Clone(); var booms = (int[])d.Booms.Clone(); var knocks = (int[])d.KnockSeq.Clone();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); int sc = r.ReadInt16(); int g = r.ReadByte(); int b = r.ReadByte(); int k = r.ReadByte();
            int slot = PartyLink.SlotOfMember(member); if (slot < 0 || slot >= count) continue;
            scores[slot] = sc; golds[slot] = g; booms[slot] = b; knocks[slot] = k;
        }
        int boomSeq = r.ReadInt16(); int bp = r.ReadSByte();
        int boomPen = bp >= 0 ? bp : -1 - PartyLink.SlotOfMember(-1 - bp);
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, scores, golds, booms, knocks, boomSeq, boomPen, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }

    void OnFlock(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        int n = r.ReadInt16();
        var list = new List<(int, GranjaChicken.Kind, GranjaChicken.State, int, int, Vector3, float)>(n);
        for (int i = 0; i < n; i++)
        {
            int id = r.ReadUInt16(); var kind = (GranjaChicken.Kind)r.ReadByte(); var mode = (GranjaChicken.State)r.ReadByte(); int pen = r.ReadSByte();
            int carrierMember = r.ReadSByte();
            var pos = new Vector3(r.ReadInt16() / 100f, r.ReadInt16() / 100f, r.ReadInt16() / 100f);
            float yaw = r.ReadByte() / 255f * 360f;
            list.Add((id, kind, mode, pen, carrierMember >= 0 ? PartyLink.SlotOfMember(carrierMember) : -1, pos, yaw));
        }
        d.ApplyChickens(list);
    }
}
