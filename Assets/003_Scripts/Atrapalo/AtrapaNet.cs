using System.IO;
using UnityEngine;

/// <summary>
/// ¡Atrápalo! online (PartyLink). The host decides each throw (what and when, on the shared clock) and who caught it;
/// every computer times its own player's reaction from the moment the throw appears on its own screen and sends it
/// to the host, so the network delay is not counted against anybody.
/// </summary>
public sealed class AtrapaNet : MonoBehaviour
{
    public const byte ChWorld = 120, ChPress = 121;
    AtrapaDirector d; float nextWorld;

    public void Init(AtrapaDirector director) { d = director; PartyLink.Listen(ChWorld, OnWorld); PartyLink.Listen(ChPress, OnPress); PartyLink.MarkReady(); }
    void OnDestroy() => PartyLink.Reset();

    void Update()
    {
        if (d == null || !PartyLink.Active || !PartyLink.IsHost) return;
        if (d.Dirty || Time.unscaledTime >= nextWorld) SendWorld();
    }

    public void SendPress(int member, int throwIndex, float reaction)
    {
        var w = PartyLink.Begin(ChPress); w.Write((sbyte)member); w.Write((short)throwIndex); w.Write(reaction); PartyLink.SendToHost();
    }

    void OnPress(int from, BinaryReader r)
    {
        if (!PartyLink.IsHost) return;
        int member = r.ReadSByte(); int throwIndex = r.ReadInt16(); float reaction = r.ReadSingle();
        int slot = PartyLink.SlotOfMember(member); if (slot < 0 || slot >= d.Players.Count || !d.Players[slot].Puppet) return;
        d.Players[slot].Act("Jump", .5f);
        d.HostPress(slot, throwIndex, reaction);
    }

    public void SendWorld()
    {
        if (!PartyLink.Active || !PartyLink.IsHost) return;
        d.Dirty = false; nextWorld = Time.unscaledTime + .2f;
        var w = PartyLink.Begin(ChWorld);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed);
        w.Write((short)d.ThrowIndex); w.Write((byte)d.ThrowState); w.Write((byte)d.Item); w.Write(d.LaunchAt);
        w.Write((sbyte)(d.CatcherSlot >= 0 ? PartyLink.MemberOfSlot(d.CatcherSlot) : -1)); w.Write(d.CatcherReaction);
        w.Write((byte)d.Players.Count);
        foreach (var p in d.Players) { w.Write((sbyte)p.Member); w.Write((byte)p.Score); w.Write((byte)p.Lives); }
        w.Write((sbyte)(d.WinnerSlot >= 0 ? PartyLink.MemberOfSlot(d.WinnerSlot) : -1)); w.Write(d.Result ?? "");
        PartyLink.SendToAll();
    }

    void OnWorld(int from, BinaryReader r)
    {
        if (PartyLink.IsHost) return;
        var phase = (AtrapaDirector.Phase)r.ReadByte(); float countdown = r.ReadSingle(); float elapsed = r.ReadSingle();
        int throwIndex = r.ReadInt16(); var ts = (AtrapaDirector.Throw)r.ReadByte(); var kind = (AtrapaItem.Kind)r.ReadByte(); float launchAt = r.ReadSingle();
        int catcher = r.ReadSByte(); float reaction = r.ReadSingle();
        int n = r.ReadByte(); var scores = new int[d.Players.Count]; var lives = new int[d.Players.Count];
        for (int s = 0; s < d.Players.Count; s++) { scores[s] = d.Players[s].Score; lives[s] = d.Players[s].Lives; }
        for (int i = 0; i < n; i++)
        {
            int member = r.ReadSByte(); int sc = r.ReadByte(); int lv = r.ReadByte();
            int slot = PartyLink.SlotOfMember(member); if (slot < 0 || slot >= scores.Length) continue;
            scores[slot] = sc; lives[slot] = lv;
        }
        int winner = r.ReadSByte(); string result = r.ReadString();
        d.ApplyWorld(phase, countdown, elapsed, throwIndex, ts, kind, launchAt, catcher >= 0 ? PartyLink.SlotOfMember(catcher) : -1, reaction, scores, lives, winner >= 0 ? PartyLink.SlotOfMember(winner) : -1, result);
    }
}
