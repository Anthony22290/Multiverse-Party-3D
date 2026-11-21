using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// "Torre del Multiverso": cooperative first-person escape room for 4–6 players (local player + bot teammates).
/// Six rooms drawn from a pool of ten (the portal is always last); solving a room opens the door to the next one.
/// Clues are not given away: each room reveals vague clues over time, or earlier with H at a time penalty.
/// Flow: countdown → play (16 min) → results. In a party match the most useful player earns the point if the team escapes.
/// </summary>
public sealed class EscapeDirector : MonoBehaviour
{
    public enum Phase { Lobby, Countdown, Playing, Won, Lost }

    public const float RoomLength = 22f, RoomWidth = 20f;
    public static string[] Names = { "Tú", "Coral", "Menta", "Rayo", "Lila", "Ámbar" };
    static string[] Models = { "Anne", "Henry", "Mako", "Captain_Barbarossa", "Anne", "Henry" };
    public static Color[] Colors =
    {
        new Color(.3f, .85f, 1f), new Color(1f, .38f, .48f), new Color(.38f, 1f, .62f),
        new Color(1f, .86f, .28f), new Color(.76f, .5f, 1f), new Color(1f, .6f, .22f)
    };

    [Tooltip("Total time for the whole tower.")] public float totalSeconds = 960f;
    [Tooltip("Tests: a bot also drives the local player.")] public bool AutoHuman;
    [Tooltip("Multiplies how long bots wait before finishing a puzzle for the player.")] public float AssistScale = 1f;

    public Phase State { get; private set; } = Phase.Lobby;
    public bool Running => State == Phase.Playing;
    public bool InputFree => Running && (HUD == null || HUD.InputFree);
    public float Countdown { get; private set; }
    public float Elapsed { get; private set; }
    public bool NewRecord { get; private set; }
    public float BestTime { get; private set; }
    public float Remaining => Mathf.Max(0f, totalSeconds - Elapsed);
    public int PlayerCount { get; private set; } = 4;
    public readonly List<EscapeActor> Actors = new();
    public EscapeActor Human { get; private set; }
    public EscapeRoom[] Rooms { get; private set; }
    public int ActiveRoomIndex { get; private set; }
    public EscapeRoom ActiveRoom => Rooms != null && ActiveRoomIndex < Rooms.Length ? Rooms[ActiveRoomIndex] : null;
    public float[] SolvedAt { get; private set; }
    public string Notice { get; private set; } = "";
    public float NoticeAt { get; private set; }
    public EscapeCamera View { get; private set; }
    public EscapeHUD HUD { get; private set; }
    public EscapeLevel Level { get; private set; }
    public float RoomTime => Elapsed - roomStartedAt;
    // ---- online party (2026-11-12): same tower everywhere, uses replayed on every computer (EscapeNet).
    public EscapeNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    /// <summary>True while a client applies a solve decided by the host.</summary>
    public bool NetSolving { get; private set; }
    float roomStartedAt, noticeUntil;

    void Start()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        PlayerCount = Mathf.Clamp(PlayerPrefs.GetInt("EscapePlayers", 4), 4, 6);
        EscapeAudio.Ensure();
        if (GetComponent<EscapeAtmosphere>() == null) gameObject.AddComponent<EscapeAtmosphere>();
        SetupEnvironment();

        EscapeInteractable.ResetIds();
        PartyLink.SeedRandom(21);   // online: every computer draws and builds the same rooms
        Level = gameObject.AddComponent<EscapeLevel>();
        Rooms = Level.Build(this);
        SolvedAt = new float[Rooms.Length];
        totalSeconds = Mathf.Max(totalSeconds, Rooms.Length * 165f);   // ~2¾ min per room

        var cam = Camera.main;
        if (cam == null)
        {
            var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            cam = c.GetComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.07f, .05f, .16f);
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        View = cam.gameObject.AddComponent<EscapeCamera>();
        View.Director = this;

        HUD = gameObject.AddComponent<EscapeHUD>();
        HUD.Director = this;
        EscapeAudio.Music("music");
        if (PartyLink.Active) { Net = gameObject.AddComponent<EscapeNet>(); Net.Init(this); }
    }

    void SetupEnvironment()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.62f, .6f, .78f);
        RenderSettings.ambientEquatorColor = new Color(.45f, .42f, .55f);
        RenderSettings.ambientGroundColor = new Color(.22f, .18f, .28f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.09f, .06f, .2f);
        RenderSettings.fogStartDistance = 40f; RenderSettings.fogEndDistance = 120f;

        if (FindAnyObjectByType<Light>() == null)
        {
            var sun = new GameObject("Luz de la torre").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.05f; sun.color = new Color(1f, .93f, .84f);
            sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(58f, -30f, 0f);
        }

        var volume = new GameObject("Postprocesado de la torre").AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.75f); bloom.threshold.Override(.95f); bloom.scatter.Override(.6f);
        var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.24f); vignette.smoothness.Override(.45f);
        var color = profile.Add<ColorAdjustments>(true); color.saturation.Override(14f); color.contrast.Override(8f); color.postExposure.Override(.1f);
        volume.sharedProfile = profile;
    }

    // ------------------------------------------------------------------ flow

    public void StartGame(int players)
    {
        if (State != Phase.Lobby) return;
        PlayerCount = Mathf.Clamp(players, 4, 6);
        PlayerPrefs.SetInt("EscapePlayers", PlayerCount);
        for (int i = 0; i < PlayerCount; i++)
        {
            if (i == 0 && MultiverseParty.InMatch)
            {
                var roster = MultiverseParty.CurrentRoster();
                for (int r = 0; r < Mathf.Min(roster.Count, Names.Length); r++) { Names[r] = roster.Names[r]; Colors[r] = roster.Colors[r]; Models[r] = PartyGames.ModelName(roster.Characters[r]); }
            }
            var go = new GameObject(i == 0 ? "Jugador (tú)" : "Compañero " + Names[i]);
            go.transform.SetPositionAndRotation(SpawnPoint(Rooms[0], PartyLink.Active ? Mathf.Max(0, PartyLink.MemberOfSlot(i)) : i), Quaternion.identity);
            var a = go.AddComponent<EscapeActor>();
            a.Initialize(this, i, i == 0, Names[i], Models[i % Models.Length], Colors[i]);
            if (PartyLink.IsPuppet(i)) a.SetPuppet();
            else if (i != 0 || AutoHuman) go.AddComponent<EscapeBot>();
            Actors.Add(a);
            if (i == 0) Human = a;
        }
        // Bot plans hand out jobs by role ("the first bot works the jugs"); online a slot may belong to a person.
        int role = 1;
        foreach (var a in Actors) if (a != Human && !a.Puppet) a.Role = role++;
        foreach (var a in Actors) if (a.Puppet) a.Role = role++;
        if (Human != null) Human.Role = 0;
        PartyLink.SeedRandom(22);
        foreach (var r in Rooms) r.OnTeamReady();
        State = Phase.Countdown;
        Countdown = 3.5f;
        View?.Snap();
    }

    public Vector3 SpawnPoint(EscapeRoom room, int slot) =>
        room.Entry + new Vector3((slot % 3 - 1) * 1.3f, .08f, -(slot / 3) * 1.15f);

    void Update()
    {
        if (IsClient) ClientTick();
        else if (State == Phase.Countdown && Net != null && !PartyLink.AllReady) { Notice = PartyLink.WaitingText; noticeUntil = Time.time + .5f; }
        else if (State == Phase.Countdown)
        {
            float before = Countdown;
            Countdown -= Time.deltaTime;
            if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f) EscapeAudio.PlayUI("count");
            if (Countdown <= 0f)
            {
                State = Phase.Playing;
                roomStartedAt = 0f;
                EscapeAudio.PlayUI("go");
                Notify("¡A ESCAPAR DE LA TORRE!");
                PartyLink.SeedRandom(100 + ActiveRoomIndex);
                ActiveRoom?.OnActivated();
                if (Net != null) Net.SendSolved(255, null);   // tells clients the tower is running (255 = start)
            }
        }
        else if (State == Phase.Playing)
        {
            Elapsed += Time.deltaTime;
            foreach (var a in Actors)
                if (!a.Puppet && a.transform.position.y < -3f) Respawn(a, "¡Al vacío! Vuelves a la entrada de la sala");
            if (Remaining <= 0f) Lose();
        }
        if (Rooms != null) foreach (var r in Rooms) r.Tick();
        if (Notice.Length > 0 && Time.time > noticeUntil) Notice = "";

        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.mKey.wasPressedThisFrame && (HUD == null || !HUD.KeypadOpen)) EscapeAudio.ToggleMusic();
    }

    public void OnRoomSolved(EscapeRoom room, EscapeActor by)
    {
        int i = room.Index;
        if (Net != null && PartyLink.IsHost) Net.SendSolved(i, by);
        SolvedAt[i] = Elapsed;
        if (by != null) by.Solves++;
        EscapeAudio.PlayUI("success");
        EscapeAudio.PlayUI("cheer", .6f);
        EscapeAudio.Duck(2f);
        View?.Shake(.25f);
        {   // celebration at the exit of the solved room
            Vector3 exit = room.W(new Vector3(0f, 1.5f, EscapeRoom.Length * .5f - 1.5f));
            MultiverseFX.Rainbow(exit, 40, 9); MultiverseFX.Ring(room.W(new Vector3(0, .05f, EscapeRoom.Length * .5f - 2f)), 5, new Color(1, .85f, .3f), .7f);
            MultiverseFX.FloatText(exit + Vector3.up * 1.5f, "¡SALA " + (i + 1) + " SUPERADA!", new Color(1, .85f, .3f), 1.1f, 2f);
        }
        if (i < Rooms.Length - 1)
        {
            Level.OpenDoor(i);
            Notify($"¡SALA {i + 1} SUPERADA!" + (by != null ? "  ·  " + (by.IsHuman ? "¡la has resuelto tú!" : by.DisplayName) : ""));
            ActiveRoomIndex = i + 1;
            roomStartedAt = Elapsed;
            PartyLink.SeedRandom(100 + i + 1);
            Rooms[i + 1].OnActivated();
        }
        else Win();
    }

    void Win()
    {
        if (State != Phase.Playing) return;
        State = Phase.Won;
        ActiveRoomIndex = Rooms.Length;
        EscapeAudio.PlayUI("win");
        EscapeAudio.Duck(6f);
        // Best escape time is kept per team size.
        string key = "SalaEscape_Mejor_" + Actors.Count;
        float best = PlayerPrefs.GetFloat(key, 0);
        NewRecord = best <= 0 || Elapsed < best; BestTime = NewRecord ? Elapsed : best;
        if (NewRecord) { PlayerPrefs.SetFloat(key, Elapsed); PlayerPrefs.Save(); EscapeAudio.PlayUI("record", .8f); Notify("¡NUEVO RÉCORD! " + Mathf.FloorToInt(Elapsed / 60) + ":" + ((int)Elapsed % 60).ToString("00")); }
        HUD?.ShowResults();
        var mvp = MostValuable;
        PartyLink.Finish(mvp != null ? mvp.Slot : -1);
    }

    void Lose()
    {
        if (State != Phase.Playing) return;
        State = Phase.Lost;
        EscapeAudio.PlayUI("lose");
        EscapeAudio.Duck(5f);
        HUD?.ShowResults();
        PartyLink.Finish(-1);
    }

    /// <summary>H: reveal the next clue of the active room now, at a time penalty.</summary>
    public void RequestClue()
    {
        var room = ActiveRoom;
        if (!Running || room == null) return;
        if (IsClient) { Net.AskClue(); return; }   // the host reveals it for the whole team
        if (room.CluesShown >= room.Clues.Length) { Notify("No quedan más pistas en esta sala"); return; }
        if (!room.CanRequestClue) { Notify("Observa la sala un poco más antes de pedir una pista"); return; }
        room.RequestClue();
        Elapsed += EscapeRoom.CluePenalty;
        EscapeAudio.PlayUI("beep", .8f);
        Notify("PISTA " + room.CluesShown + "  ·  −" + EscapeRoom.CluePenalty.ToString("0") + " s");
        if (Net != null) Net.SendClue(room.Index, room.CluesShown);
    }

    // ------------------------------------------------------------------ online (2026-11-12)

    void ClientTick()
    {
        if (State != Phase.Playing) return;
        Elapsed += Time.deltaTime;
        if (Human != null && Human.transform.position.y < -3f) Respawn(Human, "¡Al vacío! Vuelves a la entrada de la sala");
    }

    /// <summary>Client: clock and phase from the host.</summary>
    public void ApplyNet(Phase state, float countdown, float elapsed)
    {
        if (Actors.Count == 0) return;
        if (State == Phase.Countdown && state == Phase.Countdown && Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown < 3.4f) EscapeAudio.PlayUI("count");
        Countdown = countdown;
        if (state == Phase.Countdown && State == Phase.Countdown && countdown > 3.4f) { Notice = PartyLink.WaitingText; noticeUntil = Time.time + .5f; }
        if (Mathf.Abs(Elapsed - elapsed) > .35f) Elapsed = elapsed;
        if (state == State) return;
        if (state == Phase.Playing && State == Phase.Countdown) StartRunning();
        else if (state == Phase.Lost) { if (State == Phase.Countdown) StartRunning(); Lose(); }
    }

    void StartRunning()
    {
        if (State != Phase.Countdown) return;
        State = Phase.Playing;
        roomStartedAt = 0f;
        EscapeAudio.PlayUI("go");
        Notify("¡A ESCAPAR DE LA TORRE!");
        PartyLink.SeedRandom(100 + ActiveRoomIndex);
        ActiveRoom?.OnActivated();
    }

    /// <summary>Client: the host solved room <paramref name="index"/> (255 = the tower just started).</summary>
    public void NetSolve(int index, EscapeActor by)
    {
        if (index == 255) { StartRunning(); return; }
        if (Rooms == null || index < 0 || index >= Rooms.Length || Rooms[index].Solved) return;
        if (State == Phase.Countdown) StartRunning();
        NetSolving = true;
        try { Rooms[index].NetSolve(by); }
        finally { NetSolving = false; }
    }

    public void NetClue(int index, int shown)
    {
        if (Rooms == null || index < 0 || index >= Rooms.Length) return;
        Rooms[index].ForceClues(shown);
        EscapeAudio.PlayUI("beep", .8f);
        Notify("PISTA " + shown + "  ·  −" + EscapeRoom.CluePenalty.ToString("0") + " s");
    }

    /// <summary>A keypad code typed (or chosen by a bot) here: try it here and replay it on the other computers.</summary>
    public bool NetSubmit(EscapeRoom room, string code, EscapeActor a)
    {
        if (Net != null && a != null && !a.Puppet) Net.SendSubmit(a, room.Index, code);
        return room.KeypadSubmit(code, a);
    }

    public void Notify(string text)
    {
        Notice = text; NoticeAt = Time.time; noticeUntil = Time.time + 3.6f;
    }

    public void Respawn(EscapeActor a, string message)
    {
        if (a == null || a.Puppet) return;
        var room = Rooms[Mathf.Clamp(RoomIndexAt(a.transform.position), 0, Rooms.Length - 1)];
        EscapeAudio.Play("fall", a.transform.position, .8f);
        a.Knock(SpawnPoint(room, a.Slot), .5f);
        if (a.IsHuman && message != null) Notify(message);
    }

    public int RoomIndexAt(Vector3 world) => Mathf.FloorToInt((world.z + RoomLength * .5f) / RoomLength);
    public int RoomsSolved => SolvedAt == null ? 0 : Enumerable.Range(0, Rooms.Length).Count(i => Rooms[i].Solved);
    public EscapeActor MostHelpful => Actors.OrderByDescending(a => a.Actions).FirstOrDefault();
    /// <summary>Party point: whoever solved the most rooms (ties broken by useful actions).</summary>
    public EscapeActor MostValuable => Actors.OrderByDescending(a => a.Solves).ThenByDescending(a => a.Actions).FirstOrDefault();

    public EscapeInteractable BestInteractable(EscapeActor a)
    {
        // First person: the local player uses what the crosshair points at.
        if (a.IsHuman && !a.DrivenByBot && View != null && View.FirstPerson) return View.Aimed(a);
        EscapeInteractable best = null;
        float bestScore = float.MaxValue;
        foreach (var it in EscapeInteractable.All)
        {
            if (it == null || !it.CanUse(a) || !it.InRange(a)) continue;
            Vector3 d = it.Point - a.transform.position; d.y = 0f;
            float m = d.magnitude;
            float score = m - (m > .01f ? Vector3.Dot(a.transform.forward, d / m) * .5f : 0f);
            if (score < bestScore) { bestScore = score; best = it; }
        }
        return best;
    }

    /// <summary>Bots: walk through open doors to the active room, then follow that room's plan.</summary>
    public EscapeOrder OrderFor(EscapeActor a)
    {
        int here = Mathf.Clamp(RoomIndexAt(a.transform.position), 0, Rooms.Length - 1);
        int active = Mathf.Min(ActiveRoomIndex, Rooms.Length - 1);
        if (here < active)
        {
            float boundary = here * RoomLength + RoomLength * .5f;
            Vector3 p = a.transform.position;
            bool lined = Mathf.Abs(p.x) < 1f || p.z > boundary - .7f;
            return EscapeOrder.Go(new Vector3(0f, 0f, boundary + (lined ? 2.4f : -1.6f)), .5f);
        }
        if (here > active) return EscapeOrder.Go(Rooms[active].Entry);
        return Rooms[here].Order(a);
    }

    public void Restart() { if (!MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

    public void ReturnToHub()
    {
        if (MultiverseParty.InMatch) return;
        if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
