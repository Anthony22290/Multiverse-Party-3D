using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Invasión Zombi online (2026-11-12). The host runs the whole battle: waves, zombies, bosses, parts, power-ups,
/// caches, crates, damage and the truck. Every player moves their own survivor on their own computer (no input
/// lag) and sends position plus "attacking / holding E"; on the host that survivor swings, installs and revives
/// with those controls. Clients rebuild the zombies, the part and the pickups from the host's snapshots and only
/// show them. Knock-backs and teleports for a remote survivor are sent to its owner.
/// </summary>
public sealed class SurvivalNet : MonoBehaviour
{
    const byte ChPose = 40, ChState = 41, ChEvent = 42, ChRequest = 43;
    const byte EvNotice = 1, EvKnock = 2, EvTeleport = 3, EvBomb = 4;
    const byte ReqDrop = 1, ReqPerk = 2;

    SurvivalDirector d;
    bool host;
    float nextSend;
    readonly Dictionary<int, SurvivalZombie> zombies = new();
    readonly Dictionary<int, SurvivalPowerUp> powerups = new();
    SurvivalPart part;
    int nextPowerId = 1;

    public void Init(SurvivalDirector director)
    {
        d = director; host = PartyLink.IsHost;
        PartyLink.Listen(ChPose, OnPose);
        PartyLink.Listen(ChState, OnState);
        PartyLink.Listen(ChEvent, OnEvent);
        PartyLink.Listen(ChRequest, OnRequest);
        PartyLink.Fast(ChPose, ChState); PartyLink.MarkReady();
    }

    void OnDestroy() => PartyLink.Reset();

    SurvivalPlayer PlayerOf(int member)
    {
        int slot = PartyLink.SlotOfMember(member);
        return slot >= 0 && slot < d.Team.Count ? d.Team[slot] : null;
    }
    static int MemberOf(SurvivalPlayer p) => p == null ? -1 : PartyLink.MemberOfSlot(p.Slot);

    void Update()
    {
        if (d == null || !PartyLink.Active) return;
        if (host)
        {
            // A player who left the party is replaced by a bot.
            foreach (var p in d.Team)
                if (p != null && p.NetDriven && !PartyLink.IsRemoteHuman(p.Slot)) { p.SetNetMode(false, false); if (p.GetComponent<SurvivalBot>() == null) p.gameObject.AddComponent<SurvivalBot>(); }
        }
        if (Time.unscaledTime < nextSend) return;
        nextSend = Time.unscaledTime + 1f / PartyLink.SendRate;
        if (host) SendState();
        else if (d.Human != null)
        {
            var me = d.Human;
            var w = PartyLink.Begin(ChPose);
            w.Write(me.transform.position); w.Write(me.transform.eulerAngles.y);
            w.Write((byte)((me.NetAttack ? 1 : 0) | (me.NetUse ? 2 : 0)));
            PartyLink.SendToHost();
        }
    }

    // ------------------------------------------------------------------ host → clients

    void SendState()
    {
        var w = PartyLink.Begin(ChState);
        w.Write((byte)d.State); w.Write(d.Countdown); w.Write(d.Elapsed); w.Write((byte)d.Wave); w.Write((byte)d.PartsInstalled);
        w.Write(d.BreakLeft); w.Write(d.EscapeProgress); w.Write((short)d.ZombiesLeft); w.Write(d.SpeedMod); w.Write(d.Modifier ?? "");
        w.Write(d.NightOn); w.Write(d.DriveTime);
        w.Write(d.Boss != null ? d.Boss.NetId : 0); w.Write(d.Carrier != null ? d.Carrier.NetId : 0);

        w.Write((byte)d.Team.Count);
        foreach (var p in d.Team)
        {
            w.Write((sbyte)MemberOf(p));
            w.Write(p.transform.position); w.Write(p.transform.eulerAngles.y); w.Write(p.Health);
            w.Write((byte)((p.Downed ? 1 : 0) | (p.Carried != null ? 2 : 0)));
            w.Write((short)p.Kills); w.Write((short)p.Parts); w.Write((short)p.Revives); w.Write((short)p.Downs);
            for (int k = 0; k < SurvivalPerks.Count; k++) w.Write((byte)p.Perks[k]);
            w.Write(p.RageRemaining); w.Write((byte)p.SwingSeq); w.Write((byte)p.Combo); w.Write((byte)p.HurtSeq);
        }

        var all = SurvivalZombie.All.Where(z => z != null && !z.Dead).ToList();
        w.Write((short)all.Count);
        foreach (var z in all)
        {
            w.Write(z.NetId);
            w.Write((byte)z.SpecKind); w.Write((byte)z.Boss); w.Write((sbyte)z.PartIndex); w.Write((byte)z.SpecWave);
            w.Write((byte)((z.Elite ? 1 : 0) | (z.Revived ? 2 : 0) | (z.Guard ? 4 : 0) | (z.Intangible ? 8 : 0)));
            w.Write(z.transform.position); w.Write(z.transform.eulerAngles.y); w.Write(z.Health); w.Write(z.MaxHealth);
            w.Write((byte)z.ActionState); w.Write(Mathf.Atan2(z.ChargeDirection.x, z.ChargeDirection.z) * Mathf.Rad2Deg);
            w.Write((byte)z.AnimSeq); w.Write(z.AnimCode); w.Write((byte)z.HitSeq);
        }

        var loose = d.LoosePart;
        w.Write(loose != null);
        if (loose != null) { w.Write((byte)loose.Index); w.Write(loose.GroundPosition); w.Write((sbyte)MemberOf(loose.Carrier)); }

        var pickups = FindObjectsByType<SurvivalPowerUp>(FindObjectsSortMode.None);
        w.Write((byte)pickups.Length);
        foreach (var p in pickups) { if (p.NetId == 0) p.NetId = nextPowerId++; w.Write(p.NetId); w.Write((byte)p.kind); w.Write(p.transform.position); }

        var caches = d.Level.Caches;
        w.Write((sbyte)(d.ActiveCache != null ? caches.IndexOf(d.ActiveCache) : -1));
        w.Write((byte)caches.Count);
        foreach (var c in caches) { w.Write(c.Armed); w.Write(c.Opened); w.Write(c.Progress); w.Write((byte)c.GuardCount); }

        int mask = 0;
        for (int i = 0; i < d.Level.Crates.Count && i < 31; i++) if (d.Level.Crates[i].IsOpen) mask |= 1 << i;
        w.Write(mask);
        PartyLink.SendToAll();
    }

    void OnState(int from, BinaryReader r)
    {
        if (host) return;
        var state = (SurvivalDirector.Phase)r.ReadByte();
        float countdown = r.ReadSingle(), elapsed = r.ReadSingle(); int wave = r.ReadByte(), parts = r.ReadByte();
        float breakLeft = r.ReadSingle(), escape = r.ReadSingle(); int left = r.ReadInt16(); float speedMod = r.ReadSingle(); string modifier = r.ReadString();
        bool night = r.ReadBoolean(); float drive = r.ReadSingle();
        int bossId = r.ReadInt32(), carrierId = r.ReadInt32();

        // Players (the team exists once the HUD has started the game here).
        int players = r.ReadByte();
        for (int i = 0; i < players; i++)
        {
            int member = r.ReadSByte();
            Vector3 pos = r.ReadVector3(); float yaw = r.ReadSingle(); float health = r.ReadSingle(); byte flags = r.ReadByte();
            int kills = r.ReadInt16(), partsBy = r.ReadInt16(), revives = r.ReadInt16(), downs = r.ReadInt16();
            var perks = r.ReadBytes(SurvivalPerks.Count);
            float rage = r.ReadSingle(); int swing = r.ReadByte(), combo = r.ReadByte(), hurt = r.ReadByte();
            var p = PlayerOf(member);
            if (p == null) continue;
            if (p.Puppet) p.NetPose.Add(pos, Quaternion.Euler(0, yaw, 0));
            p.ApplyNet(health, (flags & 1) != 0, kills, partsBy, revives, downs, perks, rage, swing, combo, hurt);
        }

        // Zombies.
        int count = r.ReadInt16();
        var seen = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            int id = r.ReadInt32();
            var spec = new SurvivalZombie.Spec { kind = (SurvivalZombie.ZombieKind)r.ReadByte(), boss = (SurvivalZombie.BossKind)r.ReadByte(), part = r.ReadSByte(), wave = r.ReadByte() };
            byte zf = r.ReadByte(); spec.elite = (zf & 1) != 0; spec.revived = (zf & 2) != 0; spec.guard = (zf & 4) != 0;
            Vector3 pos = r.ReadVector3(); float yaw = r.ReadSingle(); float health = r.ReadSingle(), max = r.ReadSingle();
            int action = r.ReadByte(); float chargeYaw = r.ReadSingle(); int animSeq = r.ReadByte(); byte animCode = r.ReadByte(); int hitSeq = r.ReadByte();
            seen.Add(id);
            if (!zombies.TryGetValue(id, out var z) || z == null)
            {
                if (d.Level == null) continue;
                z = d.SpawnPuppet(spec, id, pos, yaw);
                zombies[id] = z;
            }
            z.ApplyNet(pos, yaw, health, max, action, chargeYaw, animSeq, animCode, hitSeq, (zf & 8) != 0);
        }
        foreach (var id in zombies.Keys.ToList())
            if (!seen.Contains(id)) { var z = zombies[id]; if (z != null) z.PuppetDie(); zombies.Remove(id); }

        // The truck part.
        bool hasPart = r.ReadBoolean();
        if (!hasPart)
        {
            if (part != null) { if (part.Carrier != null) part.Carrier.ReleaseCarried(); Destroy(part.gameObject); part = null; }
            d.SetLoosePart(null);
        }
        else
        {
            int index = r.ReadByte(); Vector3 pos = r.ReadVector3(); var carrier = PlayerOf(r.ReadSByte());
            if (part == null || part.Index != index)
            {
                if (part != null) { if (part.Carrier != null) part.Carrier.ReleaseCarried(); Destroy(part.gameObject); }
                part = new GameObject("Pieza · " + SurvivalDirector.PartNames[index]).AddComponent<SurvivalPart>();
                part.Initialize(d, index); part.DropAt(pos);
                EscapeAudio.PlayUI("key");
            }
            d.SetLoosePart(part);
            if (carrier != part.Carrier)
            {
                if (part.Carrier != null) part.Carrier.ReleaseCarried();
                if (carrier != null) { part.PickUp(carrier); carrier.Carry(part); EscapeAudio.Play("grab_0", carrier.transform.position, .6f); }
                else part.DropAt(pos);
            }
            else if (carrier == null && (part.GroundPosition - new Vector3(pos.x, 0, pos.z)).sqrMagnitude > 1f) part.DropAt(pos);
        }

        // Power-ups.
        int pickups = r.ReadByte();
        var keep = new HashSet<int>();
        for (int i = 0; i < pickups; i++)
        {
            int id = r.ReadInt32(); var kind = (SurvivalPowerUp.Kind)r.ReadByte(); Vector3 pos = r.ReadVector3();
            keep.Add(id);
            if (!powerups.TryGetValue(id, out var p) || p == null) { p = SurvivalPowerUp.Spawn(d, kind, pos, true); p.NetId = id; powerups[id] = p; }
        }
        foreach (var id in powerups.Keys.ToList())
            if (!keep.Contains(id)) { var p = powerups[id]; if (p != null) p.NetTaken(); powerups.Remove(id); }

        // Caches and crates.
        int active = r.ReadSByte(); int caches = r.ReadByte();
        for (int i = 0; i < caches; i++)
        {
            bool armed = r.ReadBoolean(), opened = r.ReadBoolean(); float progress = r.ReadSingle(); int guards = r.ReadByte();
            if (i < d.Level.Caches.Count) d.Level.Caches[i].ApplyNet(d, armed, opened, progress, guards);
        }
        int mask = r.ReadInt32();
        for (int i = 0; i < d.Level.Crates.Count && i < 31; i++) d.Level.Crates[i].ApplyNet((mask & (1 << i)) != 0);

        zombies.TryGetValue(bossId, out var boss); zombies.TryGetValue(carrierId, out var carrierZ);
        d.ApplyNet(state, countdown, elapsed, wave, parts, breakLeft, escape, left, speedMod, modifier, night, drive,
            boss, carrierZ, active >= 0 && active < d.Level.Caches.Count ? d.Level.Caches[active] : null);
    }

    // ------------------------------------------------------------------ client → host

    void OnPose(int from, BinaryReader r)
    {
        if (!host) return;
        Vector3 pos = r.ReadVector3(); float yaw = r.ReadSingle(); byte flags = r.ReadByte();
        var p = PlayerOf(from);
        if (p == null || !p.NetDriven) return;
        p.NetPose.Add(pos, Quaternion.Euler(0, yaw, 0));
        p.BotAttack = (flags & 1) != 0; p.BotUse = (flags & 2) != 0;
    }

    void OnRequest(int from, BinaryReader r)
    {
        if (!host) return;
        var p = PlayerOf(from);
        byte type = r.ReadByte();
        if (p == null || !p.NetDriven) return;
        if (type == ReqDrop) p.Drop();
        else if (type == ReqPerk)
        {
            int k = r.ReadByte();
            if (k < SurvivalPerks.Count && p.Perks[k] < 4) SurvivalPerks.Give(p, k);
        }
    }

    public void RequestDrop() { var w = PartyLink.Begin(ChRequest); w.Write(ReqDrop); PartyLink.SendToHost(); }
    public void RequestPerk(int perk) { var w = PartyLink.Begin(ChRequest); w.Write(ReqPerk); w.Write((byte)perk); PartyLink.SendToHost(); }

    // ------------------------------------------------------------------ host events

    public void SendNotice(SurvivalPlayer to, string text)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvNotice); w.Write(text ?? "");
        if (to == null) PartyLink.SendToAll(); else PartyLink.SendToMember(MemberOf(to));
    }
    public void SendKnock(SurvivalPlayer to, Vector3 knock)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvKnock); w.Write(knock); PartyLink.SendToMember(MemberOf(to));
    }
    public void SendTeleport(SurvivalPlayer to, Vector3 position)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvTeleport); w.Write(position); PartyLink.SendToMember(MemberOf(to));
    }
    public void SendBomb(Vector3 from, Vector3 to, float flight)
    {
        var w = PartyLink.Begin(ChEvent); w.Write(EvBomb); w.Write(from); w.Write(to); w.Write(flight); PartyLink.SendToAll();
    }

    void OnEvent(int from, BinaryReader r)
    {
        if (host) return;
        byte type = r.ReadByte();
        switch (type)
        {
            case EvNotice: d.NotifyLocal(r.ReadString()); break;
            case EvKnock: { var v = r.ReadVector3(); d.Human?.AddPush(v); break; }
            case EvTeleport: { var p = r.ReadVector3(); d.Human?.Teleport(p); break; }
            case EvBomb: { var a = r.ReadVector3(); var b = r.ReadVector3(); float f = r.ReadSingle(); SurvivalBomb.Throw(d, a, b, null, f, true); break; }
        }
    }
}
