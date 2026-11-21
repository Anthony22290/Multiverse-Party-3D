using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum PartyPhase { Lobby = 0, Voting = 1, Reveal = 2, Playing = 3, Results = 4, Champion = 5 }

[Serializable]
public sealed class PartyMember
{
    public string Name = "";
    public bool Bot = true;
    public PlayerRef Owner = PlayerRef.None;
    public int Score;
    public int Vote = -1;
    public int Character;
    public int ColorIndex;
    public Vector3 Pose;
    public float Yaw;
    public bool Moving;
    // Match statistics (replicated): wins per minigame and plaza antics.
    public int[] Wins = new int[PartyGames.Count];
    public int Pushes, Pushed, Emotes, VoteHits, Chats;
    // Plaza actions: a sequence number changes every time the action happens.
    public int EmoteId, EmoteSeq, ChatId, ChatSeq, PushSeq;
    public Vector3 PushDir;
    [NonSerialized] public float BotVoteAt;
    public int TotalWins => Wins.Sum();
    public void ResetStats() { Wins = new int[PartyGames.Count]; Pushes = Pushed = Emotes = VoteHits = Chats = 0; }
}

/// <summary>Who plays a minigame, in slot order. Slot 0 is always the person at this computer.</summary>
public sealed class PartyRoster
{
    public int Count;
    public string[] Names;
    public int[] Members;
    public Color[] Colors;
    public int[] Characters;
    public bool[] Humans;
}

/// <summary>
/// The match that wraps every minigame: players, room, votes and points (first to 5 wins).
/// Offline it is the source of truth; online the host's copy is replicated through MultiversePartyNet
/// and every client follows the phase (hub ↔ minigame) on its own.
/// </summary>
public sealed class MultiverseParty : MonoBehaviour
{
    public static MultiverseParty Instance { get; private set; }
    public const int DefaultWinScore = 5, MinGoal = 1, MaxGoal = 15, MaxPlaylist = 20, MinSize = 4, MaxSize = 6;
    /// <summary>Points needed to be champion in the current match (chosen in the main menu; 5 by default).</summary>
    public static int WinScore => Instance != null ? Instance.Goal : ChosenGoal;

    // 2026-11-18 · main menu "SELECCIONAR MAPAS": the host may fix which minigames are played and in what order
    // (no voting; the list loops if nobody has won yet) and how many points win. Empty list = normal voting.
    public static readonly List<int> ChosenPlaylist = new List<int>();
    public static int ChosenGoal = DefaultWinScore;
    public static bool CustomChoice => ChosenPlaylist.Count > 0 || ChosenGoal != DefaultWinScore;
    public const string HubScene = "MundoDeInicio", MenuScene = "MenuPrincipal";
    public const float VoteSeconds = 22f, FirstVoteSeconds = 30f, RevealSeconds = 4f, ResultSeconds = 9f, ReportTimeout = 45f;
    public const string NameKey = "Multiverse_Nombre";

    public static readonly Color[] Colors =
    {
        new Color(1f, .42f, .36f), new Color(.36f, .9f, .66f), new Color(1f, .84f, .24f),
        new Color(.72f, .52f, 1f), new Color(1f, .6f, .2f), new Color(.38f, .72f, 1f)
    };
    static readonly string[] BotNames = { "Coral", "Menta", "Rayo", "Lila", "Ámbar", "Nube", "Brisa", "Trueno" };

    public static string SavedName
    {
        get { var n = PlayerPrefs.GetString(NameKey, ""); return string.IsNullOrWhiteSpace(n) ? "Jugador" : n; }
        set { PlayerPrefs.SetString(NameKey, Clean(value)); PlayerPrefs.Save(); }
    }

    public static int SavedCharacter
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("Multiverse_Personaje", 0), 0, PartyGames.Characters.Length - 1);
        set { PlayerPrefs.SetInt("Multiverse_Personaje", value); PlayerPrefs.Save(); }
    }

    public static int SavedColor
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt("Multiverse_Color", 0), 0, Colors.Length - 1);
        set { PlayerPrefs.SetInt("Multiverse_Color", value); PlayerPrefs.Save(); }
    }

    public Color ColorOf(int member) => member >= 0 && member < Members.Count ? Colors[Mathf.Clamp(Members[member].ColorIndex, 0, Colors.Length - 1)] : Color.white;

    public static readonly string[] ChatPhrases = { "¡Hola!", "¡Vota esta!", "¡Esa no!", "¡Buena suerte!", "¡Te voy a ganar!", "¡Jajaja!", "¡GG!", "¡Ups!" };
    public static readonly string[] EmoteNames = { "Saludar", "Saltar", "Burla", "Desmayo" };

    public static string Clean(string value)
    {
        value = (value ?? "").Trim();
        var chars = value.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-').ToArray();
        value = new string(chars).Trim();
        if (value.Length > 14) value = value.Substring(0, 14);
        return value.Length == 0 ? "Jugador" : value;
    }

    // ---------------- State (authority writes, everyone reads) ----------------
    public bool Online { get; private set; }
    public bool IsHost { get; private set; } = true;
    public bool IsAuthority => !Online || IsHost;
    public string RoomCode { get; private set; } = "";
    public int TargetSize = MinSize;
    public int Goal = DefaultWinScore;
    public readonly List<int> Playlist = new List<int>();
    public int PlayIndex;   // next position in Playlist (host)
    public bool UsesPlaylist => Playlist.Count > 0;
    public readonly List<PartyMember> Members = new List<PartyMember>();
    public PartyPhase Phase = PartyPhase.Voting;
    public readonly int[] Choices = { -1, -1, -1 };
    public int Selected = -1;
    public int Round;
    public float Countdown;
    public int LastWinner = -1;
    public int LastGame = -1;
    public string ResultText = "";
    public int ReportsIn, ReportsNeeded;
    /// <summary>Random seed of the current round: every peer builds the same minigame layout from it.</summary>
    public int Seed;
    public int LocalIndex { get; private set; }
    public PartyMember Local => LocalIndex >= 0 && LocalIndex < Members.Count ? Members[LocalIndex] : null;
    public NetworkRunner Runner { get; private set; }
    public MultiversePartyNet Net { get; private set; }
    public static string LastStatus = "";
    public event Action Changed;

    // ---------------- Authority bookkeeping ----------------
    float phaseEnds;
    float firstReportAt = -1;
    int reportRound = -1;
    readonly List<(int member, bool claimed, int winner)> reports = new List<(int, bool, int)>();
    bool reported;
    int voteSent = -1; float voteSentAt;
    int loadedRound = -1;
    bool leaving;

    /// <summary>A real party match is running (false when a minigame scene was opened straight from the editor).</summary>
    public static bool InMatch => Instance != null && !Instance.devDirect;
    /// <summary>False when a minigame scene was opened straight from the editor (it then keeps its own start screen).</summary>
    public static bool AutoStart => InMatch;
    bool devDirect;

    // =====================================================================
    // Creation
    // =====================================================================
    static MultiverseParty Create()
    {
        if (Instance != null) Destroy(Instance.gameObject);
        var go = new GameObject("Multiverse Party");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MultiverseParty>();
        Instance.Goal = Mathf.Clamp(ChosenGoal, MinGoal, MaxGoal);
        Instance.Playlist.AddRange(ChosenPlaylist.Where(g => g >= 0 && g < PartyGames.Count).Take(MaxPlaylist));
        Instance.gameObject.AddComponent<MultiversePartyOverlay>();
        return Instance;
    }

    /// <summary>Local match with bots: you plus bots up to <paramref name="size"/>.</summary>
    public static MultiverseParty StartOffline(int size, bool loadHub = true)
    {
        var p = Create();
        p.Online = false; p.IsHost = true; p.TargetSize = Mathf.Clamp(size, MinSize, MaxSize);
        p.Members.Add(new PartyMember { Name = SavedName, Bot = false, Character = SavedCharacter, ColorIndex = SavedColor });
        p.FillBots();
        p.LocalIndex = 0;
        p.BeginVoting();
        if (loadHub) SceneManager.LoadScene(HubScene);
        return p;
    }

    /// <summary>When a minigame scene is opened directly in the editor there is no match: make one so it plays.</summary>
    public static MultiverseParty EnsureForScene(string scene)
    {
        if (Instance != null) return Instance;
        var p = StartOffline(MaxSize, false);
        p.devDirect = true;
        int game = PartyGames.FromScene(scene);
        if (game >= 0)
        {
            p.Selected = game; p.Round = 1; p.Phase = PartyPhase.Playing; p.loadedRound = 1;
        }
        return p;
    }

    public static MultiverseParty EnsureForHub()
    {
        if (Instance != null) return Instance;
        return StartOffline(MinSize, false);
    }

    public static async Task<bool> StartOnline(bool host, string code, int size)
    {
        code = (code ?? "").Trim().ToUpperInvariant();
        if (host && code.Length == 0) code = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        if (code.Length < 4 || code.Length > 12 || code.Any(c => !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9')))
        { LastStatus = "El código debe tener de 4 a 12 letras o números."; return false; }

        var p = Create();
        p.Online = true; p.IsHost = host; p.RoomCode = code; p.TargetSize = Mathf.Clamp(size, MinSize, MaxSize);
        p.Phase = PartyPhase.Lobby;
        var runnerGo = new GameObject("Party Runner");
        DontDestroyOnLoad(runnerGo);
        var runner = runnerGo.AddComponent<NetworkRunner>();
        runner.ProvideInput = false;
        p.Runner = runner;
        var callbacks = new NetworkDelegates();
        callbacks.OnShutdown = (r, reason) => { if (Instance == p) p.OnRunnerShutdown(reason); };
        callbacks.OnPlayerLeft = (r, player) => { if (Instance == p) p.OnPlayerLeft(player); };
        callbacks.OnReliableDataReceived = (NetworkRunner r, PlayerRef player, Fusion.Sockets.ReliableKey key, ReadOnlySpan<byte> data) => { if (Instance == p) PartyLink.OnData(r, player, key, data); };
        runner.AddCallbacks(callbacks);
        try
        {
            var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global.AppSettings.GetCopy();
            settings.FixedRegion = "sa"; settings.AppVersion = "multiverse-party-1";
            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = host ? GameMode.Host : GameMode.Client, SessionName = code, PlayerCount = MaxSize,
                IsVisible = false, IsOpen = true, EnableClientSessionCreation = false, CustomPhotonAppSettings = settings
            });
            if (!result.Ok)
            {
                LastStatus = host ? "No se pudo crear la sala (" + result.ShutdownReason + ")."
                                  : "No se encontró la sala " + code + " o está llena (" + result.ShutdownReason + ").";
                p.leaving = true; await p.ShutdownRunner(); Destroy(p.gameObject); return false;
            }
            if (host)
            {
                p.Members.Add(new PartyMember { Name = SavedName, Bot = false, Owner = runner.LocalPlayer, Character = SavedCharacter, ColorIndex = SavedColor });
                p.LocalIndex = 0;
                var prefab = Resources.Load<NetworkObject>("MultiversePartyNet");
                if (prefab == null) throw new InvalidOperationException("Falta el prefab MultiversePartyNet (menú Multiverse/Preparar Partida en línea).");
                runner.Spawn(prefab);
            }
            LastStatus = host ? "Sala " + code + " creada." : "Conectado a la sala " + code + ".";
            SceneManager.LoadScene(HubScene);
            return true;
        }
        catch (Exception e)
        {
            LastStatus = "Conexión fallida: " + e.Message;
            Debug.LogError("MultiverseParty: " + e);
            p.leaving = true; await p.ShutdownRunner(); if (p != null) Destroy(p.gameObject);
            return false;
        }
    }

    async Task ShutdownRunner()
    {
        var r = Runner; Runner = null;
        if (r != null) { try { await r.Shutdown(); } catch (Exception) { } if (r != null) Destroy(r.gameObject); }
    }

    public async void LeaveToMenu(string reason = null)
    {
        if (leaving) return;
        leaving = true;
        if (!string.IsNullOrEmpty(reason)) LastStatus = reason;
        Time.timeScale = 1f; AudioListener.pause = false;
        await ShutdownRunner();
        if (Instance == this) Instance = null;
        Destroy(gameObject);
        if (Application.CanStreamedLevelBeLoaded(MenuScene)) SceneManager.LoadScene(MenuScene);
    }

    void OnRunnerShutdown(ShutdownReason reason)
    {
        if (leaving) return;
        Runner = null;
        LeaveToMenu(IsHost ? "La sala se cerró (" + reason + ")." : "Se perdió la conexión con el anfitrión (" + reason + ").");
    }

    void OnPlayerLeft(PlayerRef player)
    {
        if (!IsHost) return;
        foreach (var m in Members)
            if (!m.Bot && m.Owner == player)
            {
                string old = m.Name;
                m.Bot = true; m.Owner = PlayerRef.None; m.Name = FreeBotName();
                Toast(old + " salió · lo sustituye " + m.Name);
            }
        if (Phase == PartyPhase.Lobby) Members.RemoveAll(m => m.Bot);
        Changed?.Invoke();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // =====================================================================
    // Network glue (called by MultiversePartyNet)
    // =====================================================================
    public void AttachNet(MultiversePartyNet net)
    {
        Net = net;
        if (Runner != null) Runner.MakeDontDestroyOnLoad(net.gameObject);
        if (!IsHost) net.RPC_Join(SavedName, (sbyte)SavedCharacter, (sbyte)SavedColor);
    }

    public void HostJoin(PlayerRef player, string name, int character, int color)
    {
        if (!IsHost) return;
        name = Clean(name);
        if (Members.Any(m => !m.Bot && m.Owner == player)) return;
        if (Members.Any(m => m.Name == name)) name = name.Length > 12 ? name.Substring(0, 12) + "2" : name + " 2";
        var slot = Members.FirstOrDefault(m => m.Bot);
        if (slot != null) { slot.Bot = false; slot.Owner = player; slot.Name = name; slot.Vote = -1; }
        else if (Members.Count < TargetSize) { slot = new PartyMember { Name = name, Bot = false, Owner = player, Pose = SpawnPoint(Members.Count) }; Members.Add(slot); }
        if (slot == null)
        {   // The match has exactly the chosen number of players (4, 5 or 6): nobody extra gets in.
            Toast(name + " no pudo entrar: la sala está llena (" + TargetSize + " jugadores)");
            try { if (Runner != null && Runner.IsRunning) Runner.Disconnect(player); } catch (Exception e) { Debug.LogWarning("MultiverseParty: " + e.Message); }
            return;
        }
        if (slot != null)
        {
            slot.Character = Mathf.Clamp(character, 0, PartyGames.Characters.Length - 1);
            slot.ColorIndex = Mathf.Clamp(color, 0, Colors.Length - 1);
            ResolveColors(slot);
        }
        Toast(name + " se unió a la sala");
        Changed?.Invoke();
    }

    /// <summary>Two players can't share a colour: the newcomer keeps it if the other one is a bot, otherwise picks the first free one.</summary>
    void ResolveColors(PartyMember keep)
    {
        foreach (var other in Members)
        {
            if (other == keep || other.ColorIndex != keep.ColorIndex) continue;
            var target = other.Bot ? other : keep;
            for (int c = 0; c < Colors.Length; c++)
                if (!Members.Any(m => m != target && m.ColorIndex == c)) { target.ColorIndex = c; break; }
        }
    }

    /// <summary>Client side: refresh the local copy from replicated state.</summary>
    public void ReadFrom(MultiversePartyNet net)
    {
        var prevPhase = Phase; int prevRound = Round; int prevWinner = LastWinner;
        int size = Mathf.Clamp(net.Size, 0, MaxSize);
        while (Members.Count < size) Members.Add(new PartyMember());
        while (Members.Count > size) Members.RemoveAt(Members.Count - 1);
        LocalIndex = 0;
        for (int i = 0; i < size; i++)
        {
            var m = Members[i];
            m.Name = net.Names[i].ToString(); m.Owner = net.Owners[i]; m.Bot = m.Owner == PlayerRef.None;
            m.Score = net.Scores[i]; m.Vote = net.Votes[i]; m.Character = net.Characters[i]; m.ColorIndex = net.ColorIdx[i];
            for (int g = 0; g < PartyGames.Count; g++) m.Wins[g] = net.Wins[i * PartyGames.Count + g];
            m.Pushes = net.Pushes[i]; m.Pushed = net.Pusheds[i]; m.Emotes = net.EmoteCounts[i]; m.VoteHits = net.VoteHits[i]; m.Chats = net.ChatCounts[i];
            m.EmoteId = net.EmoteIds[i]; m.EmoteSeq = net.EmoteSeqs[i]; m.ChatId = net.ChatIds[i]; m.ChatSeq = net.ChatSeqs[i];
            m.PushSeq = net.PushSeqs[i]; m.PushDir = net.PushDirs[i];
            if (Runner != null && m.Owner == Runner.LocalPlayer) LocalIndex = i;
        }
        for (int i = 0; i < size; i++)
            if (i != LocalIndex) { Members[i].Pose = net.Poses[i]; Members[i].Yaw = net.Yaws[i]; Members[i].Moving = net.Moving[i]; }
        for (int i = 0; i < 3; i++) Choices[i] = net.Choices[i];
        Phase = (PartyPhase)net.Phase; Selected = net.Selected; Round = net.Round; Countdown = net.Countdown;
        LastWinner = net.LastWinner; LastGame = net.LastGame; ResultText = net.ResultText.ToString(); TargetSize = net.TargetSize;
        ReportsIn = net.ReportsIn; ReportsNeeded = net.ReportsNeeded; Seed = net.Seed;
        Goal = net.Goal > 0 ? net.Goal : DefaultWinScore;
        if (prevPhase != Phase || prevRound != Round || prevWinner != LastWinner) Changed?.Invoke();
    }

    public void WriteTo(MultiversePartyNet net)
    {
        net.Size = Members.Count;
        for (int i = 0; i < MaxSize; i++)
        {
            bool has = i < Members.Count; var m = has ? Members[i] : null;
            net.Names.Set(i, has ? m.Name : "");
            net.Owners.Set(i, has ? m.Owner : PlayerRef.None);
            net.Scores.Set(i, has ? m.Score : 0);
            net.Votes.Set(i, has ? m.Vote : -1);
            net.Characters.Set(i, has ? m.Character : 0);
            net.ColorIdx.Set(i, has ? m.ColorIndex : 0);
            for (int g = 0; g < PartyGames.Count; g++) net.Wins.Set(i * PartyGames.Count + g, has ? m.Wins[g] : 0);
            net.Pushes.Set(i, has ? m.Pushes : 0); net.Pusheds.Set(i, has ? m.Pushed : 0); net.EmoteCounts.Set(i, has ? m.Emotes : 0);
            net.VoteHits.Set(i, has ? m.VoteHits : 0); net.ChatCounts.Set(i, has ? m.Chats : 0);
            net.EmoteIds.Set(i, has ? m.EmoteId : 0); net.EmoteSeqs.Set(i, has ? m.EmoteSeq : 0);
            net.ChatIds.Set(i, has ? m.ChatId : 0); net.ChatSeqs.Set(i, has ? m.ChatSeq : 0);
            net.PushSeqs.Set(i, has ? m.PushSeq : 0); net.PushDirs.Set(i, has ? m.PushDir : Vector3.zero);
            net.Poses.Set(i, has ? m.Pose : Vector3.zero);
            net.Yaws.Set(i, has ? m.Yaw : 0);
            net.Moving.Set(i, has && m.Moving);
        }
        for (int i = 0; i < 3; i++) net.Choices.Set(i, Choices[i]);
        net.Phase = (int)Phase; net.Selected = Selected; net.Round = Round; net.Countdown = Countdown;
        net.LastWinner = LastWinner; net.LastGame = LastGame; net.ResultText = ResultText; net.TargetSize = TargetSize;
        net.ReportsIn = ReportsIn; net.ReportsNeeded = ReportsNeeded; net.Seed = Seed; net.Goal = Goal;
    }

    public int MemberOf(PlayerRef player)
    {
        for (int i = 0; i < Members.Count; i++) if (!Members[i].Bot && Members[i].Owner == player) return i;
        return -1;
    }

    // =====================================================================
    // Actions from the UI / hub / minigames
    // =====================================================================
    public void HostStartMatch()
    {
        if (!IsAuthority || Phase != PartyPhase.Lobby) return;
        FillBots(); TrimBots();   // exactly the chosen size (4, 5 or 6) for every minigame of the match
        foreach (var m in Members) { m.Score = 0; m.ResetStats(); }
        Round = 0; PlayIndex = 0;
        BeginVoting();
    }

    public void SetTargetSize(int size)
    {
        if (!IsAuthority) return;
        TargetSize = Mathf.Clamp(Mathf.Max(size, Members.Count(m => !m.Bot)), MinSize, MaxSize);   // never below the people already in
        if (Phase != PartyPhase.Lobby) { FillBots(); TrimBots(); }
        Changed?.Invoke();
    }

    public void Vote(int choice)
    {
        if (Phase != PartyPhase.Voting || choice < 0 || choice > 2) return;
        if (Local != null && Local.Vote == choice) return;
        if (IsAuthority) ApplyVote(LocalIndex, choice);
        else if (Net != null)
        {
            if (voteSent == choice && Time.unscaledTime - voteSentAt < .6f) return;
            voteSent = choice; voteSentAt = Time.unscaledTime;
            Net.RPC_Vote((sbyte)choice); if (Local != null) Local.Vote = choice;
        }
    }

    public void ApplyVote(int member, int choice)
    {
        if (Phase != PartyPhase.Voting || member < 0 || member >= Members.Count) return;
        Members[member].Vote = Mathf.Clamp(choice, 0, 2);
        Changed?.Invoke();
    }

    public void SetLocalPose(Vector3 pos, float yaw, bool moving)
    {
        var m = Local; if (m == null) return;
        m.Pose = pos; m.Yaw = yaw; m.Moving = moving;
        if (!IsAuthority && Net != null) Net.SendPose(pos, yaw, moving);
    }

    public void NewMatch()
    {
        if (!IsAuthority || Phase != PartyPhase.Champion) return;
        foreach (var m in Members) { m.Score = 0; m.ResetStats(); }
        LastWinner = -1; ResultText = ""; LastGame = -1; PlayIndex = 0;
        BeginVoting();
    }

    /// <summary>The players for this minigame. Slot 0 = this computer's player, others follow member order.</summary>
    public PartyRoster Roster()
    {
        int n = Mathf.Clamp(Members.Count, 1, MaxSize);
        var r = new PartyRoster { Count = n, Names = new string[n], Members = new int[n], Colors = new Color[n], Characters = new int[n], Humans = new bool[n] };
        int slot = 0;
        void Put(int i)
        {
            r.Members[slot] = i; r.Names[slot] = Members[i].Name; r.Colors[slot] = ColorOf(i);
            r.Characters[slot] = Members[i].Character; r.Humans[slot] = !Members[i].Bot; slot++;
        }
        Put(LocalIndex);
        for (int i = 0; i < Members.Count && slot < n; i++) if (i != LocalIndex) Put(i);
        return r;
    }

    public static PartyRoster CurrentRoster()
    {
        var p = EnsureForScene(SceneManager.GetActiveScene().name);
        return p.Roster();
    }

    /// <summary>
    /// A minigame ended on this computer. <paramref name="winnerSlot"/> is the roster slot of the winner,
    /// or -1 if nobody earns the point (for example a co-op team that lost).
    /// </summary>
    public static void ReportMinigame(int winnerSlot)
    {
        var p = Instance; if (p == null || p.devDirect || p.reported || p.Phase != PartyPhase.Playing) return;
        p.reported = true;
        var roster = p.Roster();
        int winner = winnerSlot >= 0 && winnerSlot < roster.Count ? roster.Members[winnerSlot] : -1;
        bool claimed = winnerSlot == 0;
        if (p.IsAuthority) p.RegisterReport(p.LocalIndex, p.Round, claimed, winner);
        else if (p.Net != null) p.Net.RPC_Report(p.Round, claimed, (sbyte)winner);
        Debug.Log("MultiverseParty: minijuego terminado, ganador " + (winner >= 0 ? p.Members[winner].Name : "ninguno"));
    }

    public static bool Reported => Instance != null && Instance.reported;

    public void RegisterReport(int member, int round, bool claimed, int winner)
    {
        if (!IsAuthority || round != Round || Phase != PartyPhase.Playing) return;
        if (reportRound != Round) { reports.Clear(); reportRound = Round; firstReportAt = -1; }
        if (reports.Any(r => r.member == member)) return;
        reports.Add((member, claimed, winner));
        if (firstReportAt < 0) firstReportAt = Time.unscaledTime;
    }

    // =====================================================================
    // Authority loop
    // =====================================================================
    void Update()
    {
        if (leaving) return;
        if (IsAuthority) Simulate();
        Follow();
    }

    void Simulate()
    {
        float now = Time.unscaledTime;
        switch (Phase)
        {
            case PartyPhase.Lobby:
                Countdown = 0; break;
            case PartyPhase.Voting:
                foreach (var m in Members)
                    if (m.Bot && m.Vote < 0 && now >= m.BotVoteAt) { m.Vote = BotChoice(); Changed?.Invoke(); }
                bool allVoted = Members.All(m => m.Vote >= 0);
                if (allVoted && phaseEnds - now > 2f) phaseEnds = now + 2f;
                Countdown = Mathf.Max(0, phaseEnds - now);
                if (now >= phaseEnds) EndVoting();
                break;
            case PartyPhase.Reveal:
                Countdown = Mathf.Max(0, phaseEnds - now);
                if (now >= phaseEnds) { Seed = UnityEngine.Random.Range(1, int.MaxValue); Phase = PartyPhase.Playing; Round++; reported = false; ReportsIn = 0; Changed?.Invoke(); }
                break;
            case PartyPhase.Playing:
                if (reportRound != Round) { reports.Clear(); reportRound = Round; firstReportAt = -1; }
                ReportsNeeded = Members.Count(m => !m.Bot);
                ReportsIn = reports.Count(r => !Members[r.member].Bot);
                Countdown = firstReportAt < 0 ? 0 : Mathf.Max(0, ReportTimeout - (now - firstReportAt));
                if (ReportsNeeded > 0 && ReportsIn >= ReportsNeeded || firstReportAt >= 0 && now - firstReportAt > ReportTimeout) Resolve();
                break;
            case PartyPhase.Results:
                Countdown = Mathf.Max(0, phaseEnds - now);
                if (now >= phaseEnds)
                {
                    if (Members.Any(m => m.Score >= Goal)) { Phase = PartyPhase.Champion; Countdown = 0; Changed?.Invoke(); }
                    else BeginVoting();
                }
                break;
        }
    }

    /// <summary>
    /// Online minigames (2026-11-12): the host simulated the shared game, so its result is final for everybody.
    /// <paramref name="winnerMember"/> is a member index, or -1 when nobody earns the point.
    /// </summary>
    public void HostResolve(int winnerMember)
    {
        if (!IsAuthority || Phase != PartyPhase.Playing || devDirect) return;
        reported = true;
        Resolve(winnerMember >= 0 && winnerMember < Members.Count ? winnerMember : -1, true);
    }

    /// <summary>A client finished an online minigame: show the waiting card until the host resolves the round.</summary>
    public void MarkReported() { if (Phase == PartyPhase.Playing) reported = true; }

    void Resolve() => Resolve(-1, false);

    void Resolve(int forced, bool useForced)
    {
        int winner = forced;
        if (!useForced) foreach (var r in reports) if (r.claimed && r.winner >= 0 && !Members[r.member].Bot) { winner = r.winner; break; }
        if (winner < 0 && !useForced)
        {
            // Nobody won on their own screen: trust a result only if it went to an actual bot
            // (a human never gets a point for a game their bot double played elsewhere).
            var own = reports.FirstOrDefault(r => r.member == LocalIndex);
            var source = reports.Any(r => r.member == LocalIndex) ? new[] { own } : reports.ToArray();
            foreach (var r in source)
                if (r.winner >= 0 && r.winner < Members.Count && (Members[r.winner].Bot || r.winner == r.member)) { winner = r.winner; break; }
        }
        LastWinner = winner; LastGame = Selected;
        string game = Selected >= 0 ? PartyGames.All[Selected].Title : "";
        if (winner >= 0)
        {
            Members[winner].Score = Mathf.Min(Goal, Members[winner].Score + 1);
            if (Selected >= 0 && Selected < PartyGames.Count) Members[winner].Wins[Selected]++;
            ResultText = Members[winner].Score >= Goal ? "¡" + Members[winner].Name + " llega a " + Goal + (Goal == 1 ? " punto!" : " puntos!") : Members[winner].Name + " gana " + game + " · +1 punto";
        }
        else ResultText = "Nadie gana " + game + " · sin puntos esta ronda";
        Phase = PartyPhase.Results; phaseEnds = Time.unscaledTime + ResultSeconds;
        reports.Clear(); firstReportAt = -1;
        Changed?.Invoke();
    }

    void BeginVoting()
    {
        if (UsesPlaylist) { BeginPlaylistGame(); return; }
        var pool = Enumerable.Range(0, PartyGames.Count).Where(g => g != LastGame).OrderBy(_ => UnityEngine.Random.value).ToList();
        for (int i = 0; i < 3; i++) Choices[i] = pool[i % pool.Count];
        float duration = Round == 0 ? FirstVoteSeconds : VoteSeconds;
        phaseEnds = Time.unscaledTime + duration;
        foreach (var m in Members) { m.Vote = -1; m.BotVoteAt = Time.unscaledTime + UnityEngine.Random.Range(4f, duration * .7f); }
        Selected = -1; Phase = PartyPhase.Voting; reported = false; devDirect = false;
        Changed?.Invoke();
    }

    /// <summary>Chosen order: no vote. The portals show this game and the next two; the chosen one opens after a short reveal.</summary>
    void BeginPlaylistGame()
    {
        int n = Playlist.Count;
        for (int i = 0; i < 3; i++) Choices[i] = Playlist[(PlayIndex + i) % n];
        Selected = Playlist[PlayIndex % n]; PlayIndex++;
        foreach (var m in Members) m.Vote = -1;
        Phase = PartyPhase.Reveal; phaseEnds = Time.unscaledTime + (Round == 0 ? 8f : 6f);
        reported = false; devDirect = false;
        Changed?.Invoke();
    }

    void EndVoting()
    {
        int[] counts = new int[3];
        foreach (var m in Members) if (m.Vote >= 0) counts[m.Vote]++;
        int best = counts.Max();
        var tied = Enumerable.Range(0, 3).Where(i => counts[i] == best).ToList();
        int pick = tied[UnityEngine.Random.Range(0, tied.Count)];
        Selected = Choices[pick];
        foreach (var m in Members) if (m.Vote == pick) m.VoteHits++;
        Phase = PartyPhase.Reveal; phaseEnds = Time.unscaledTime + RevealSeconds;
        Changed?.Invoke();
    }

    int BotChoice()
    {
        // Bots like to follow the crowd a little.
        int[] counts = new int[3];
        foreach (var m in Members) if (m.Vote >= 0) counts[m.Vote]++;
        float total = 3 + counts.Sum() * 1.5f, r = UnityEngine.Random.value * total;
        for (int i = 0; i < 3; i++) { r -= 1 + counts[i] * 1.5f; if (r <= 0) return i; }
        return 2;
    }

    void FillBots()
    {
        while (Members.Count < TargetSize)
        {
            int color = Enumerable.Range(0, Colors.Length).FirstOrDefault(c => !Members.Any(m => m.ColorIndex == c));
            var chars = Enumerable.Range(0, PartyGames.Characters.Length).OrderBy(c => Members.Count(m => m.Character == c)).ThenBy(_ => UnityEngine.Random.value);
            Members.Add(new PartyMember { Name = FreeBotName(), Bot = true, Character = chars.First(), ColorIndex = color, Pose = SpawnPoint(Members.Count) });
        }
    }

    void TrimBots()
    {
        for (int i = Members.Count - 1; i >= 0 && Members.Count > TargetSize; i--)
            if (Members[i].Bot && Members[i].Score == 0) Members.RemoveAt(i);
    }

    string FreeBotName()
    {
        foreach (var n in BotNames) if (!Members.Any(m => m.Name == n)) return n;
        return "Bot " + (Members.Count + 1);
    }

    public static Vector3 SpawnPoint(int i)
    {
        return new Vector3(-5f + (i % MaxSize) * 2f, .4f, -8.6f);
    }

    // =====================================================================
    // Scene following (every peer)
    // =====================================================================
    void Follow()
    {
        string active = SceneManager.GetActiveScene().name;
        bool hubPhase = Phase == PartyPhase.Lobby || Phase == PartyPhase.Voting || Phase == PartyPhase.Reveal || Phase == PartyPhase.Champion;
        if (hubPhase)
        {
            reported = Phase == PartyPhase.Reveal ? false : reported;
            if (!loading && active != HubScene && active != MenuScene && Application.CanStreamedLevelBeLoaded(HubScene))
                StartCoroutine(Load(HubScene, -1));
            return;
        }
        if (Phase == PartyPhase.Playing && loadedRound != Round && Selected >= 0 && !loading)
        {
            loadedRound = Round; reported = false;
            StartCoroutine(Load(PartyGames.All[Selected].Scene, Selected));
        }
    }

    bool loading;
    public bool Loading => loading;
    public const float MinLoadSeconds = 4.5f, MinHubLoadSeconds = 1.2f;

    /// <summary>Loads a scene in the background behind the loading card (controls and a tip for minigames).</summary>
    System.Collections.IEnumerator Load(string scene, int game)
    {
        loading = true;
        Time.timeScale = 1f; AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        var overlay = GetComponent<MultiversePartyOverlay>();
        overlay?.ShowLoading(game);
        yield return null;
        float start = Time.unscaledTime, min = game >= 0 ? MinLoadSeconds : MinHubLoadSeconds;
        var op = SceneManager.LoadSceneAsync(scene);
        if (op == null) { overlay?.HideLoading(); loading = false; yield break; }
        op.allowSceneActivation = false;
        while (op.progress < .9f || Time.unscaledTime - start < min)
        {
            overlay?.SetProgress(Mathf.Min(op.progress / .9f, (Time.unscaledTime - start) / min));
            yield return null;
        }
        overlay?.SetProgress(1f);
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;
        overlay?.HideLoading();
        loading = false;
    }

    // =====================================================================
    // Plaza actions: emotes, quick chat, pushes (authority applies, everyone reads the sequence numbers)
    // =====================================================================
    public void Emote(int id) { if (IsAuthority) ApplyEmote(LocalIndex, id); else Net?.RPC_Emote((sbyte)id); }
    public void Chat(int id) { if (IsAuthority) ApplyChat(LocalIndex, id); else Net?.RPC_Chat((sbyte)id); }
    public void Push(int target, Vector3 dir) { if (IsAuthority) ApplyPush(LocalIndex, target, dir); else Net?.RPC_Push((sbyte)target, dir); }

    public void ApplyEmote(int member, int id)
    {
        if (member < 0 || member >= Members.Count) return;
        var m = Members[member]; m.EmoteId = Mathf.Clamp(id, 0, EmoteNames.Length - 1); m.EmoteSeq++; m.Emotes++;
    }

    public void ApplyChat(int member, int id)
    {
        if (member < 0 || member >= Members.Count) return;
        var m = Members[member]; m.ChatId = Mathf.Clamp(id, 0, ChatPhrases.Length - 1); m.ChatSeq++; m.Chats++;
    }

    public void ApplyPush(int from, int target, Vector3 dir)
    {
        if (from < 0 || target < 0 || from >= Members.Count || target >= Members.Count || from == target) return;
        dir.y = 0f;
        var t = Members[target]; t.PushDir = dir.normalized; t.PushSeq++; t.Pushed++;
        Members[from].Pushes++;
    }

    // =====================================================================
    // End-of-match titles
    // =====================================================================
    static readonly string[] GameTitles = { "Rey de la cubierta", "Cazazombis", "As del volante", "Hacker sigiloso", "Mente maestra", "Saltarrodillos", "Topo de oro", "Ingeniero espacial", "As del propulsor", "Rey del green", "Manos de hielo", "Rey del corral", "Repartidor estrella", "Manos rápidas" };

    /// <summary>One fun title per player, from the match statistics.</summary>
    public string[] Titles()
    {
        var titles = new string[Members.Count];
        void Give(Func<PartyMember, int> stat, int minimum, string title)
        {
            int best = -1, bestValue = minimum - 1; bool tie = false;
            for (int i = 0; i < Members.Count; i++)
            {
                int v = stat(Members[i]);
                if (v > bestValue) { best = i; bestValue = v; tie = false; }
                else if (v == bestValue && best >= 0) tie = true;
            }
            if (best >= 0 && !tie && titles[best] == null) titles[best] = title;
        }
        for (int g = 0; g < PartyGames.Count; g++) { int game = g; Give(m => m.Wins[game], 2, GameTitles[g]); }
        Give(m => m.Pushes, 3, "Rey del empujón");
        Give(m => m.Pushed, 3, "Saco de boxeo");
        Give(m => m.Emotes, 3, "Alma de la fiesta");
        Give(m => m.VoteHits, 2, "El que elige");
        Give(m => m.Chats, 4, "Charlatán");
        for (int g = 0; g < PartyGames.Count; g++) { int game = g; Give(m => m.Wins[game], 1, GameTitles[g]); }
        for (int i = 0; i < titles.Length; i++)
            if (titles[i] == null) titles[i] = Members[i].TotalWins == 0 ? "Turista del multiverso" : "Competidor tenaz";
        return titles;
    }

    /// <summary>Minigames no longer have their own menus: hide restart / back-to-hub buttons while a match runs.</summary>
    public static void StripButtons(GameObject root, params string[] labels)
    {
        if (!InMatch || root == null) return;
        foreach (var b in root.GetComponentsInChildren<Button>(true))
        {
            var t = b.GetComponentInChildren<Text>(true);
            if (t == null) continue;
            string label = t.text.Trim();
            if (labels.Any(l => string.Equals(label, l, StringComparison.OrdinalIgnoreCase))) b.gameObject.SetActive(false);
        }
    }

    static readonly string[] MenuLabels =
    {
        "REINICIAR", "MUNDO DE INICIO", "REVANCHA · ENTER", "OTRA CARRERA", "VOLVER A PLAZA", "VOLVER AL HUB", "JUGAR OTRA VEZ",
        "REINICIAR LA TORRE", "VOLVER AL MUNDO", "OTRA RONDA"
    };

    /// <summary>Hide every restart / leave button in the open minigame (the party decides when to go back).</summary>
    public static void StripMenus()
    {
        if (!InMatch) return;
        foreach (var b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var t = b.GetComponentInChildren<Text>(true);
            if (t != null && MenuLabels.Any(l => string.Equals(t.text.Trim(), l, StringComparison.OrdinalIgnoreCase))) b.gameObject.SetActive(false);
        }
    }

    public static void Toast(string text)
    {
        if (Instance != null && Instance.TryGetComponent<MultiversePartyOverlay>(out var o)) o.Toast(text);
    }

    public int Leader()
    {
        int best = -1;
        for (int i = 0; i < Members.Count; i++) if (best < 0 || Members[i].Score > Members[best].Score) best = i;
        return best;
    }
}

/// <summary>Always-on overlay: result card after each minigame and short notices.</summary>
public sealed class MultiversePartyOverlay : MonoBehaviour
{
    Canvas canvas;
    GameObject card, waiting, toastBox, loadingPanel;
    RawImage loadImage;
    Text loadTitle, loadSub, loadControls, loadTip;
    RectTransform loadFill;
    Text cardTitle, cardBody, cardFooter, waitingText, toastText;
    float toastUntil;
    MultiverseParty party;

    void Start() => Build();

    void Build()
    {
        if (canvas != null) return;
        party = GetComponent<MultiverseParty>();
        var font = MultiverseUI.DisplayFont;
        var root = new GameObject("Party Overlay", typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;

        card = Panel(root.transform, "Resultado de la ronda", new Vector2(.5f, .5f), Vector2.zero, new Vector2(620, 430));
        cardTitle = Label(card.transform, 36, new Vector2(0, 165), new Vector2(580, 70));
        cardBody = Label(card.transform, 22, new Vector2(0, -5), new Vector2(560, 260));
        cardFooter = Label(card.transform, 17, new Vector2(0, -180), new Vector2(560, 36));
        card.SetActive(false);

        waiting = Panel(root.transform, "Esperando", new Vector2(.5f, 0), new Vector2(0, 54), new Vector2(560, 56));
        waitingText = Label(waiting.transform, 18, Vector2.zero, new Vector2(540, 50));
        waiting.SetActive(false);

        toastBox = Panel(root.transform, "Aviso", new Vector2(.5f, 1), new Vector2(0, -122), new Vector2(560, 54));
        toastText = Label(toastBox.transform, 18, Vector2.zero, new Vector2(540, 48));
        toastBox.SetActive(false);

        // Loading card between scenes.
        loadingPanel = new GameObject("Pantalla de carga", typeof(RectTransform), typeof(Image));
        var lr = loadingPanel.GetComponent<RectTransform>(); lr.SetParent(root.transform, false);
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = lr.offsetMax = Vector2.zero;
        var frame = Panel(loadingPanel.transform, "Marco de la imagen", new Vector2(.5f, .5f), new Vector2(0, 128), new Vector2(660, 320));
        loadImage = new GameObject("Imagen", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        var ir = loadImage.rectTransform; ir.SetParent(frame.transform, false); ir.anchoredPosition = Vector2.zero; ir.sizeDelta = new Vector2(640, 300);
        loadTitle = Label(loadingPanel.transform, 44, new Vector2(0, -70), new Vector2(1000, 60));
        loadSub = Label(loadingPanel.transform, 18, new Vector2(0, -110), new Vector2(1000, 30));
        loadControls = Label(loadingPanel.transform, 17, new Vector2(0, -158), new Vector2(1060, 54));
        loadTip = Label(loadingPanel.transform, 18, new Vector2(0, -222), new Vector2(1000, 54));
        var barBack = new GameObject("Barra", typeof(RectTransform), typeof(Image));
        var br = barBack.GetComponent<RectTransform>(); br.SetParent(loadingPanel.transform, false); br.anchoredPosition = new Vector2(0, -290); br.sizeDelta = new Vector2(620, 16);
        barBack.GetComponent<Image>().color = new Color(1, 1, 1, .15f); barBack.GetComponent<Image>().sprite = MultiverseUI.Pill; barBack.GetComponent<Image>().type = Image.Type.Sliced;
        var fill = new GameObject("Progreso", typeof(RectTransform), typeof(Image));
        loadFill = fill.GetComponent<RectTransform>(); loadFill.SetParent(br, false);
        loadFill.anchorMin = new Vector2(0, 0); loadFill.anchorMax = new Vector2(0, 1); loadFill.pivot = new Vector2(0, .5f); loadFill.offsetMin = loadFill.offsetMax = Vector2.zero; loadFill.sizeDelta = new Vector2(0, 0);
        fill.GetComponent<Image>().color = new Color(1f, .8f, .3f); fill.GetComponent<Image>().sprite = MultiverseUI.Pill; fill.GetComponent<Image>().type = Image.Type.Sliced;
        loadingPanel.SetActive(false);

        MultiverseUI.Skin(root, MultiverseUI.Mode.Hub, false);
        foreach (var t in root.GetComponentsInChildren<Text>(true)) t.font = font;
        loadingPanel.GetComponent<Image>().color = new Color(.04f, .06f, .14f, 1f);
    }

    public void ShowLoading(int game)
    {
        Build();
        bool mini = game >= 0 && game < PartyGames.Count;
        loadImage.transform.parent.gameObject.SetActive(mini);
        if (mini)
        {
            var info = PartyGames.All[game];
            var tex = PartyGames.Image(game);
            loadImage.texture = tex; loadImage.color = tex != null ? Color.white : info.Color;
            loadTitle.text = info.Title; loadTitle.color = Color.Lerp(info.Color, Color.white, .25f);
            loadSub.text = info.Tagline + "  ·  ronda " + (party != null ? party.Round : 1) + "  ·  primero a " + MultiverseParty.WinScore + " puntos";
            loadControls.text = "<color=#FFD54A>CONTROLES</color>\n" + PartyGames.Controls[game];
            loadTip.text = "<color=#7FE7FF>CONSEJO:</color> " + PartyGames.Tip(game);
        }
        else
        {
            loadTitle.text = "VOLVIENDO A LA PLAZA"; loadTitle.color = new Color(1f, .8f, .3f);
            loadSub.text = party != null && party.Phase == PartyPhase.Champion ? "¡Tenemos campeón!" : "Preparad el próximo voto";
            loadControls.text = ""; loadTip.text = "";
        }
        SetProgress(0f);
        loadingPanel.SetActive(true);
    }

    public void SetProgress(float f) { if (loadFill != null) loadFill.sizeDelta = new Vector2(620f * Mathf.Clamp01(f), 0); }
    public void HideLoading() { if (loadingPanel != null) loadingPanel.SetActive(false); }

    GameObject Panel(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size;
        go.GetComponent<Image>().color = new Color(.08f, .12f, .24f, .96f); go.GetComponent<Image>().raycastTarget = false;
        return go;
    }

    Text Label(Transform parent, int size, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Texto", typeof(RectTransform), typeof(Text));
        var r = go.GetComponent<RectTransform>(); r.SetParent(parent, false); r.anchoredPosition = pos; r.sizeDelta = box;
        var t = go.GetComponent<Text>(); t.font = MultiverseUI.DisplayFont; t.fontSize = size; t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(1f, .97f, .9f); t.raycastTarget = false; t.supportRichText = true;
        return t;
    }

    public void Toast(string text)
    {
        if (toastText == null) return;
        toastText.text = text; toastBox.SetActive(true); toastUntil = Time.unscaledTime + 3.5f;
    }

    void Update()
    {
        if (party == null || card == null) return;
        bool inHub = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == MultiverseParty.HubScene;
        bool inMenu = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == MultiverseParty.MenuScene;
        bool results = party.Phase == PartyPhase.Results && !inHub && !inMenu && party.Countdown < MultiverseParty.ResultSeconds - 3.2f && !party.Loading;
        card.SetActive(results);
        if (results)
        {
            int w = party.LastWinner;
            cardTitle.text = w < 0 ? "SIN GANADOR" : w == party.LocalIndex ? "¡GANASTE LA RONDA!" : "¡" + party.Members[w].Name.ToUpperInvariant() + " GANA!";
            cardTitle.color = w >= 0 ? party.ColorOf(w) : new Color(1f, .8f, .4f);
            var sb = new System.Text.StringBuilder();
            sb.Append("<size=18>").Append(party.ResultText).Append("</size>\n\n");
            foreach (int i in Enumerable.Range(0, party.Members.Count).OrderByDescending(i => party.Members[i].Score))
            {
                var m = party.Members[i];
                string dots = new string('●', Mathf.Min(m.Score, MultiverseParty.WinScore)) + new string('○', Mathf.Max(0, MultiverseParty.WinScore - m.Score));
                string color = ColorUtility.ToHtmlStringRGB(party.ColorOf(i));
                sb.Append("<color=#").Append(color).Append(">").Append(m.Name).Append(i == party.LocalIndex ? " (tú)" : m.Bot ? " · bot" : "").Append("</color>   ")
                  .Append(dots).Append(i == w ? "  <color=#FFD54A>+1</color>" : "").Append('\n');
            }
            cardBody.text = sb.ToString();
            cardFooter.text = "Volviendo a la plaza en " + Mathf.CeilToInt(party.Countdown) + " s · primero a " + MultiverseParty.WinScore + " puntos";
        }
        bool wait = party.Phase == PartyPhase.Playing && MultiverseParty.Reported && !inHub;
        waiting.SetActive(wait);
        if (wait)
            waitingText.text = party.Online
                ? "Esperando a que terminen los demás jugadores… " + party.ReportsIn + "/" + Mathf.Max(1, party.ReportsNeeded) + (party.Countdown > 0 ? " · " + Mathf.CeilToInt(party.Countdown) + " s" : "")
                : "Contando puntos…";
        if (toastBox.activeSelf && Time.unscaledTime > toastUntil) toastBox.SetActive(false);
    }
}
