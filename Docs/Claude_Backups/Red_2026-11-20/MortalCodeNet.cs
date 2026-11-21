using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Código Mortal online (2026-11-12). Every computer runs the same plaza; each player moves their own pirate and
/// sends its position. The host owns the bots, the codes, the timers, pickups, eliminations and the winner, and
/// streams them to everybody. Clients ask the host to read a code, use a terminal or raise the protector.
/// </summary>
public sealed class MortalCodeNet : MonoBehaviour
{
    const byte ChPose = 10, ChState = 11, ChRequest = 12, ChEvent = 13;
    const byte ReqObserve = 1, ReqSubmit = 2, ReqProtector = 3;
    const byte EvSubmit = 1, EvNotice = 2, EvFlash = 3;

    MortalCodeDirector d;
    float nextSend;
    readonly Dictionary<int, MortalCodePickup> pickups = new();
    int nextPickupId = 1;

    bool Host => PartyLink.IsHost;

    public void Init(MortalCodeDirector director)
    {
        d = director;
        PartyLink.Listen(ChPose, OnPose);
        PartyLink.Listen(ChState, OnState);
        PartyLink.Listen(ChRequest, OnRequest);
        PartyLink.Listen(ChEvent, OnEvent);
        PartyLink.MarkReady();
    }

    void OnDestroy() => PartyLink.Reset();

    MortalCodeActor ActorOf(int member)
    {
        int slot = PartyLink.SlotOfMember(member);
        return slot >= 0 && slot < d.Actors.Count ? d.Actors[slot] : null;
    }
    int MemberOf(MortalCodeActor a) => PartyLink.MemberOfSlot(d.Actors.IndexOf(a));

    // ------------------------------------------------------------------ sending

    void Update()
    {
        if (d == null || !PartyLink.Active || Time.unscaledTime < nextSend) return;
        nextSend = Time.unscaledTime + 1f / PartyLink.SendRate;
        if (Host) SendState();
        else if (d.Human != null)
        {
            var w = PartyLink.Begin(ChPose);
            w.Write(d.Human.transform.position); w.Write(d.Human.transform.eulerAngles.y);
            PartyLink.SendToHost();
        }
    }

    void SendState()
    {
        var w = PartyLink.Begin(ChState);
        w.Write(d.Countdown); w.Write(d.Remaining); w.Write(d.Finished); w.Write(d.Result ?? "");
        w.Write((byte)d.Actors.Count);
        for (int i = 0; i < d.Actors.Count; i++)
        {
            var a = d.Actors[i];
            w.Write((sbyte)PartyLink.MemberOfSlot(i));
            w.Write(a.transform.position); w.Write(a.transform.eulerAngles.y);
            w.Write(a.Alive); w.Write(a.ProtectionRemaining); w.Write(a.ProtectorAvailable);
            w.Write(a.BoostRemaining); w.Write((short)a.Eliminations); w.Write(Mathf.Max(0, a.LockedUntil - Time.time));
            w.Write(d.CodeOf(a) ?? "");
            int mask = 0;
            foreach (var (target, _) in d.KnownCodes(a)) { int m = MemberOf(target); if (m >= 0) mask |= 1 << m; }
            w.Write(mask);
        }
        w.Write((byte)d.Feed.Count); foreach (var f in d.Feed) w.Write(f);
        var live = FindObjectsByType<MortalCodePickup>(FindObjectsSortMode.None);
        w.Write((byte)live.Length);
        foreach (var p in live)
        {
            if (p.NetId == 0) p.NetId = nextPickupId++;
            w.Write(p.NetId); w.Write((byte)p.kind); w.Write(p.transform.position);
        }
        PartyLink.SendToAll();
    }

    // ------------------------------------------------------------------ receiving

    void OnPose(int from, BinaryReader r)
    {
        if (!Host) return;
        var a = ActorOf(from);
        Vector3 p = r.ReadVector3(); float yaw = r.ReadSingle();
        if (a != null && a.Puppet) a.NetPose.Add(p, Quaternion.Euler(0, yaw, 0));
    }

    void OnState(int from, BinaryReader r)
    {
        if (Host) return;
        float countdown = r.ReadSingle(), remaining = r.ReadSingle(); bool finished = r.ReadBoolean(); string result = r.ReadString();
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte();
            Vector3 pos = r.ReadVector3(); float yaw = r.ReadSingle();
            bool alive = r.ReadBoolean(); float prot = r.ReadSingle(); bool protAvail = r.ReadBoolean();
            float boost = r.ReadSingle(); int elims = r.ReadInt16(); float locked = r.ReadSingle();
            string code = r.ReadString(); int mask = r.ReadInt32();
            var a = ActorOf(member);
            if (a == null) continue;
            if (a.Puppet) a.NetPose.Add(pos, Quaternion.Euler(0, yaw, 0));
            a.ApplyNet(alive, prot, protAvail, boost, elims, locked);
            d.SetCode(a, code);
            for (int m = 0; m < MultiverseParty.MaxSize; m++)
                if ((mask & (1 << m)) != 0) { var t = ActorOf(m); if (t != null) d.MarkSeen(a, t); }
        }
        d.Feed.Clear();
        int feeds = r.ReadByte(); for (int i = 0; i < feeds; i++) d.Feed.Add(r.ReadString());
        int count = r.ReadByte();
        var keep = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            int id = r.ReadInt32(); var kind = (MortalCodePickup.Kind)r.ReadByte(); Vector3 pos = r.ReadVector3();
            keep.Add(id);
            if (!pickups.TryGetValue(id, out var p) || p == null)
            {
                p = MortalCodePickup.Create(d, kind, pos); p.NetId = id; p.Remote = true; pickups[id] = p;
            }
        }
        foreach (var id in pickups.Keys.ToList())
            if (!keep.Contains(id)) { var p = pickups[id]; if (p != null) { MultiverseFX.Burst(p.transform.position + Vector3.up, Color.white, 10, 4); Destroy(p.gameObject); } pickups.Remove(id); }
        d.ApplyNet(countdown, remaining, finished, result);
    }

    void OnRequest(int from, BinaryReader r)
    {
        if (!Host) return;
        var a = ActorOf(from);
        if (a == null || !a.Puppet) return;
        byte type = r.ReadByte();
        if (type == ReqObserve)
        {
            var target = ActorOf(r.ReadSByte());
            if (target != null && target != a && target.Alive && d.Running) d.MarkSeen(a, target);
        }
        else if (type == ReqSubmit)
        {
            int index = r.ReadByte(); string code = r.ReadString();
            var terminal = d.terminals != null && index < d.terminals.Length ? d.terminals[index] : null;
            bool ok = d.Submit(a, terminal, code, out var message);
            var w = PartyLink.Begin(ChEvent); w.Write(EvSubmit); w.Write(ok); w.Write(message ?? "");
            PartyLink.SendToMember(from);
        }
        else if (type == ReqProtector) a.ActivateProtector();
    }

    void OnEvent(int from, BinaryReader r)
    {
        if (Host) return;
        byte type = r.ReadByte();
        if (type == EvSubmit)
        {
            bool ok = r.ReadBoolean(); string message = r.ReadString();
            d.HUD?.ShowResponse(message);
            MortalCodeAudio.UI(ok ? "access" : "denied", ok ? 1f : .8f);
        }
        else if (type == EvNotice)
        {
            string text = r.ReadString(), sound = r.ReadString();
            d.Notify(text);
            if (sound.Length > 0) MortalCodeAudio.UI(sound);
        }
        else if (type == EvFlash)
        {
            var t = ActorOf(r.ReadSByte());
            if (t != null) d.HUD?.Flash(t);
        }
    }

    // ------------------------------------------------------------------ requests (client) and personal events (host)

    public void RequestObserve(MortalCodeActor target)
    {
        var w = PartyLink.Begin(ChRequest); w.Write(ReqObserve); w.Write((sbyte)MemberOf(target)); PartyLink.SendToHost();
    }
    public void RequestSubmit(int terminal, string code)
    {
        var w = PartyLink.Begin(ChRequest); w.Write(ReqSubmit); w.Write((byte)terminal); w.Write(code ?? ""); PartyLink.SendToHost();
    }
    public void RequestProtector()
    {
        var w = PartyLink.Begin(ChRequest); w.Write(ReqProtector); PartyLink.SendToHost();
    }

    /// <summary>Host: a notice for one remote player (or everybody when <paramref name="to"/> is null).</summary>
    public void SendNotice(MortalCodeActor to, string text, string sound = "")
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvNotice); w.Write(text ?? ""); w.Write(sound ?? "");
        if (to == null) PartyLink.SendToAll(); else PartyLink.SendToMember(MemberOf(to));
    }
    public void SendFlash(MortalCodeActor to, MortalCodeActor target)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvFlash); w.Write((sbyte)MemberOf(target)); PartyLink.SendToMember(MemberOf(to));
    }
}
