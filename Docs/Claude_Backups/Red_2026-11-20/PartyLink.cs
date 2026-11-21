using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Shared online layer for the minigames of an online party (2026-11-12).
/// Every peer loads the same minigame scene; this class carries small binary messages between the host and the
/// clients over the party's Fusion runner (reliable data), gives every peer the same random seed, tells who owns
/// which roster slot and holds the start until everybody has loaded.
///
/// Ownership model used by the minigames:
///  · slot 0 is always the person at this computer (same as offline) and is simulated here;
///  · other human players are simulated on their own computer and arrive here as puppets;
///  · bots and the shared world (enemies, timers, eliminations, winner) are simulated by the host.
/// Offline, or when a minigame scene is opened straight from the editor, <see cref="Active"/> is false and the
/// minigames behave exactly as before.
/// </summary>
public static class PartyLink
{
    public delegate void Handler(int fromMember, BinaryReader reader);

    // Core channels. Minigames use their own channels from 10 up (see the constants in each minigame).
    public const byte ChReady = 1;
    public const float ReadyTimeout = 25f, SendRate = 20f;

    static MultiverseParty P => MultiverseParty.Instance;

    /// <summary>This minigame is shared with other computers.</summary>
    public static bool Active
    {
        get
        {
            var p = P;
            return p != null && MultiverseParty.InMatch && p.Online && p.Runner != null && p.Runner.IsRunning && p.Net != null;
        }
    }
    public static bool IsHost => !Active || P.IsHost;
    public static bool IsClient => Active && !P.IsHost;
    public static int Seed => P != null ? P.Seed : 0;
    public static int Round => P != null ? P.Round : 0;
    public static int LocalMember => P != null ? P.LocalIndex : 0;

    // ------------------------------------------------------------------ roster and ownership

    public static PartyRoster Roster
    {
        get
        {
            var p = P; if (p == null) return null;
            if (roster == null || rosterRound != p.Round || rosterLocal != p.LocalIndex || roster.Count != Mathf.Clamp(p.Members.Count, 1, MultiverseParty.MaxSize))
            { roster = p.Roster(); rosterRound = p.Round; rosterLocal = p.LocalIndex; }
            return roster;
        }
    }
    static PartyRoster roster; static int rosterRound = -1, rosterLocal = -1;

    public static int MemberOfSlot(int slot) { var r = Roster; return r != null && slot >= 0 && slot < r.Count ? r.Members[slot] : -1; }
    public static int SlotOfMember(int member)
    {
        var r = Roster; if (r == null) return -1;
        for (int i = 0; i < r.Count; i++) if (r.Members[i] == member) return i;
        return -1;
    }
    /// <summary>A real person plays this member right now (bots replace players who leave).</summary>
    public static bool IsHumanMember(int member) { var p = P; return p != null && member >= 0 && member < p.Members.Count && !p.Members[member].Bot; }
    /// <summary>This computer simulates the slot (its own player, or a bot when hosting).</summary>
    public static bool OwnsSlot(int slot)
    {
        if (!Active || slot == 0) return true;
        return P.IsHost && !IsHumanMember(MemberOfSlot(slot));
    }
    /// <summary>The slot is simulated elsewhere: show it from the network.</summary>
    public static bool IsPuppet(int slot) => !OwnsSlot(slot);
    /// <summary>Another person plays this slot.</summary>
    public static bool IsRemoteHuman(int slot) => Active && slot != 0 && IsHumanMember(MemberOfSlot(slot));

    // ------------------------------------------------------------------ messages

    static readonly Dictionary<byte, Handler> handlers = new Dictionary<byte, Handler>();
    static readonly List<(byte ch, int from, byte[] data)> pending = new List<(byte, int, byte[])>();
    static readonly MemoryStream outStream = new MemoryStream(4096);
    static readonly BinaryWriter writer = new BinaryWriter(outStream);
    static byte outChannel;
    static readonly ReliableKey Key = ReliableKey.FromInts(0x4D50, 0, 0, 0);

    /// <summary>Starts a message: write the payload into the returned writer, then call one of the Send methods.</summary>
    public static BinaryWriter Begin(byte channel)
    {
        outChannel = channel;
        outStream.Position = 0; outStream.SetLength(0);
        writer.Write(channel);
        writer.Write(Round);
        writer.Write((sbyte)LocalMember);
        writer.Write((byte)0); // relay flag (set by SendToAll on clients)
        return writer;
    }

    /// <summary>Client: to the host. Host: handled locally right away (same code path for both).</summary>
    public static void SendToHost()
    {
        if (!Active) return;
        if (P.IsHost) { DeliverLocal(); return; }
        try { P.Runner.SendReliableDataToServer(Key, new ReadOnlySpan<byte>(outStream.GetBuffer(), 0, (int)outStream.Length)); }
        catch (Exception e) { Debug.LogWarning("PartyLink: envío al anfitrión fallido · " + e.Message); }
    }

    /// <summary>Host: to every client. Client: to the host, which applies it and forwards it to the other clients.</summary>
    public static void SendToAll()
    {
        if (!Active) return;
        if (!P.IsHost)
        {
            outStream.GetBuffer()[6] = 1; // relay
            SendToHost();
            return;
        }
        Broadcast(outStream.GetBuffer(), (int)outStream.Length, PlayerRef.None);
    }

    /// <summary>Host only: to the computer of one member.</summary>
    public static void SendToMember(int member)
    {
        if (!Active || !P.IsHost || member < 0 || member >= P.Members.Count) return;
        var owner = P.Members[member].Owner;
        if (P.Members[member].Bot || owner == PlayerRef.None) return;
        if (owner == P.Runner.LocalPlayer) { DeliverLocal(); return; }
        try { P.Runner.SendReliableDataToPlayer(owner, Key, new ReadOnlySpan<byte>(outStream.GetBuffer(), 0, (int)outStream.Length)); }
        catch (Exception e) { Debug.LogWarning("PartyLink: envío fallido · " + e.Message); }
    }

    static void Broadcast(byte[] buffer, int length, PlayerRef skip)
    {
        var runner = P.Runner;
        foreach (var player in runner.ActivePlayers)
        {
            if (player == runner.LocalPlayer || player == skip) continue;
            try { runner.SendReliableDataToPlayer(player, Key, new ReadOnlySpan<byte>(buffer, 0, length)); }
            catch (Exception e) { Debug.LogWarning("PartyLink: difusión fallida · " + e.Message); }
        }
    }

    static void DeliverLocal()
    {
        var data = new byte[outStream.Length];
        Array.Copy(outStream.GetBuffer(), data, data.Length);
        Dispatch(outChannel, LocalMember, data, 7);
    }

    /// <summary>Called by the party runner for every reliable data packet.</summary>
    public static void OnData(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data)
    {
        var p = P;
        if (p == null || data.Length < 7 || key != Key) return;
        byte ch = data[0];
        int round = BitConverter.ToInt32(data.Slice(1, 4));
        int from = (sbyte)data[5];
        bool relay = data[6] != 0;
        if (round != p.Round) return;
        var copy = data.ToArray();
        if (p.IsHost)
        {
            // The host knows who really sent it: never trust the member index written by a client.
            from = p.MemberOf(player);
            if (from < 0) return;
            copy[5] = (byte)(sbyte)from; copy[6] = 0;
            if (relay) Broadcast(copy, copy.Length, player);
        }
        if (ch == ChReady) { if (p.IsHost) AddReady(from); return; }
        Dispatch(ch, from, copy, 7);
    }

    static void Dispatch(byte ch, int from, byte[] data, int offset)
    {
        if (!handlers.TryGetValue(ch, out var h))
        {
            if (pendingRound != Round) { pending.Clear(); pendingRound = Round; }
            if (pending.Count < 512) pending.Add((ch, from, data));
            return;
        }
        using var ms = new MemoryStream(data, offset, data.Length - offset, false);
        using var r = new BinaryReader(ms);
        try { h(from, r); }
        catch (Exception e) { Debug.LogError("PartyLink: error leyendo el canal " + ch + " · " + e); }
    }

    /// <summary>Handle a channel (messages that arrived before this call are delivered now, in order).</summary>
    public static void Listen(byte channel, Handler handler)
    {
        handlers[channel] = handler;
        if (pendingRound != Round) { pending.Clear(); pendingRound = Round; }
        for (int i = 0; i < pending.Count; i++)
        {
            if (pending[i].ch != channel) continue;
            var (ch, from, data) = pending[i];
            pending.RemoveAt(i--);
            Dispatch(ch, from, data, 7);
        }
    }

    /// <summary>Forget every handler (call from the minigame's OnDestroy).</summary>
    public static void Reset()
    {
        handlers.Clear(); selfReady = false; readySince = -1;
    }

    // ------------------------------------------------------------------ start handshake

    static readonly HashSet<int> readyMembers = new HashSet<int>();
    static bool selfReady; static float readySince = -1; static int readyRound = -1, pendingRound = -1;

    static bool IsReady(int member) => readyRound == Round && readyMembers.Contains(member);

    static void AddReady(int member)
    {
        if (readyRound != Round) { readyMembers.Clear(); readyRound = Round; }
        readyMembers.Add(member);
    }

    /// <summary>This computer finished loading the minigame.</summary>
    public static void MarkReady()
    {
        if (!Active) return;
        // 2026-11-19: while the "how to play" card is open, say we are ready only once it is closed.
        if (PartyGameIntro.Holding) { PartyGameIntro.PendingReady = true; return; }
        selfReady = true; readySince = Time.unscaledTime;
        if (P.IsHost) AddReady(LocalMember);
        else { Begin(ChReady); SendToHost(); }
    }

    /// <summary>Host: every human has loaded (or the wait timed out). Always true offline.</summary>
    public static bool AllReady
    {
        get
        {
            if (!Active) return true;
            if (!P.IsHost) return selfReady;
            if (!selfReady) return false;
            if (Time.unscaledTime - readySince > ReadyTimeout) return true;
            for (int i = 0; i < P.Members.Count; i++) if (!P.Members[i].Bot && !IsReady(i)) return false;
            return true;
        }
    }

    public static string WaitingText
    {
        get
        {
            if (!Active || P == null) return "";
            if (!P.IsHost) return "Esperando al anfitrión…";
            int need = 0, have = 0;
            for (int i = 0; i < P.Members.Count; i++) if (!P.Members[i].Bot) { need++; if (IsReady(i)) have++; }
            return "Esperando a los jugadores… " + have + "/" + need;
        }
    }

    // ------------------------------------------------------------------ result

    /// <summary>
    /// The minigame ended. Offline: the usual local report. Online: only the host's result counts (it simulated the
    /// shared game); clients just show the "waiting" card.
    /// </summary>
    public static void Finish(int winnerSlot)
    {
        if (!Active) { MultiverseParty.ReportMinigame(winnerSlot); return; }
        if (P.IsHost) P.HostResolve(winnerSlot >= 0 ? MemberOfSlot(winnerSlot) : -1);
        else P.MarkReported();
    }

    /// <summary>Same as <see cref="Finish"/> with a member index (used when the winner arrives from the network).</summary>
    public static void FinishMember(int winnerMember)
    {
        if (!Active) { MultiverseParty.ReportMinigame(SlotOfMember(winnerMember)); return; }
        if (P.IsHost) P.HostResolve(winnerMember);
        else P.MarkReported();
    }

    // ------------------------------------------------------------------ helpers

    public static void Write(this BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
    public static Vector3 ReadVector3(this BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    public static void Write(this BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
    public static Quaternion ReadQuaternion(this BinaryReader r) => new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    public static void WriteColor(this BinaryWriter w, Color c) { w.Write(c.r); w.Write(c.g); w.Write(c.b); }
    public static Color ReadColor(this BinaryReader r) => new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

    /// <summary>Seeds Unity's random generator from the party seed (plus a salt) so every peer builds the same thing.</summary>
    public static void SeedRandom(int salt)
    {
        if (Active) UnityEngine.Random.InitState(unchecked(Seed * 31 + salt));
    }
}

/// <summary>
/// Smooths a remote object: keeps the last few network samples and plays them back a little in the past
/// (interpolation delay), so 20 Hz updates look continuous.
/// </summary>
public sealed class NetPose
{
    struct Sample { public float t; public Vector3 p; public Quaternion r; }
    readonly Sample[] buf = new Sample[8];
    int count;
    public const float Delay = .11f;
    public bool HasData => count > 0;
    public float LastReceived { get; private set; } = -99f;

    public void Add(Vector3 position, Quaternion rotation)
    {
        float now = Time.time;
        if (count == buf.Length) { Array.Copy(buf, 1, buf, 0, buf.Length - 1); count--; }
        buf[count++] = new Sample { t = now, p = position, r = rotation };
        LastReceived = now;
    }

    public void Clear() => count = 0;

    public bool Get(out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        if (count == 0) return false;
        float t = Time.time - Delay;
        if (count == 1 || t >= buf[count - 1].t) { position = buf[count - 1].p; rotation = buf[count - 1].r; return true; }
        if (t <= buf[0].t) { position = buf[0].p; rotation = buf[0].r; return true; }
        for (int i = count - 1; i > 0; i--)
        {
            if (t < buf[i - 1].t) continue;
            var a = buf[i - 1]; var b = buf[i];
            float k = b.t - a.t > 1e-4f ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 1f;
            position = Vector3.LerpUnclamped(a.p, b.p, k); rotation = Quaternion.Slerp(a.r, b.r, k);
            return true;
        }
        position = buf[count - 1].p; rotation = buf[count - 1].r;
        return true;
    }

    /// <summary>Latest sample (no delay).</summary>
    public Vector3 LatestPosition => count > 0 ? buf[count - 1].p : Vector3.zero;
    public Quaternion LatestRotation => count > 0 ? buf[count - 1].r : Quaternion.identity;
}
