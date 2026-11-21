using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// "Torre del Multiverso" — CARRERA (2026-11-17). Every player escapes alone from their own copy of the tower:
/// same rooms in the same order for everybody, but each copy has its own solutions (nobody can shout the code).
/// Each computer builds and plays only its own copy; online, the computers just share progress (EscapeNet).
/// Whoever reaches the end first wins the party point; everybody is told the places as players arrive, and the
/// rest keep going until they finish too. Stuck for 1:30 in a room? A helper bot walks in and solves it.
/// Party members played by the AI race as "ghosts": no body, only a simulated time per room.
/// </summary>
public sealed class EscapeDirector : MonoBehaviour
{
    public enum Phase { Lobby, Countdown, Playing, Won, Lost }

    public const float RoomLength = 22f, RoomWidth = 20f;
    /// <summary>Seconds stuck in a room before the helper bot comes in.</summary>
    public const float HelperAfter = 90f;
    /// <summary>If the helper has not solved the room this many seconds later, the door opens anyway.</summary>
    public const float HelperGiveUp = 35f;
    /// <summary>Once somebody finishes, the others have at most this long to finish too.</summary>
    public const float FinishGrace = 360f;
    /// <summary>Seconds the final ranking stays up before the party moves on.</summary>
    public const float EndDelay = 6f;
    /// <summary>Asking for a clue (H) freezes you this long.</summary>
    public const float ClueFreeze = 10f;
    /// <summary>Party members played by the AI race as ghosts (simulated times). False: only people race.</summary>
    public static bool GhostRivals = true;

    public static string[] Names = { "Tú", "Coral", "Menta", "Rayo", "Lila", "Ámbar" };
    static string[] Models = { "Anne", "Henry", "Mako", "Captain_Barbarossa", "Anne", "Henry" };
    public static Color[] Colors =
    {
        new Color(.3f, .85f, 1f), new Color(1f, .38f, .48f), new Color(.38f, 1f, .62f),
        new Color(1f, .86f, .28f), new Color(.76f, .5f, 1f), new Color(1f, .6f, .22f)
    };
    static readonly Color HelperTint = new Color(1f, .86f, .28f);

    [Tooltip("Kept for tests; the race has no time limit.")] public float totalSeconds = 3600f;
    [Tooltip("Tests: a bot also drives the local player.")] public bool AutoHuman;
    [Tooltip("Multiplies how long bots wait before finishing a puzzle (0 while the helper is in the room).")] public float AssistScale = 1f;

    public Phase State { get; private set; } = Phase.Lobby;
    public bool Running => State == Phase.Playing;
    public bool InputFree => Running && (HUD == null || HUD.InputFree);
    public float Countdown { get; private set; }
    /// <summary>Your own time (stops when you reach the end).</summary>
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
    public EscapeNet Net { get; private set; }
    /// <summary>Every computer runs its own tower: nothing waits for a host decision any more.</summary>
    public bool IsClient => false;
    public bool NetSolving => false;
    float roomStartedAt, noticeUntil, assistSaved = 1f, helperLeaveAt = -1f;

    // ------------------------------------------------------------------ race state

    public sealed class Racer
    {
        public int Slot, Member;
        public string Name;
        public Color Color;
        public bool Local, Ghost, Gone, Finished;
        public int Rooms;
        public float Time;
        public float[] Splits;   // ghosts: race time at which each room is done
    }
    public readonly List<Racer> Racers = new();
    public Racer Me { get; private set; }
    /// <summary>Time since the start for everybody (keeps running after you finish).</summary>
    public float RaceClock { get; private set; }
    public bool RaceStarted { get; private set; }
    public bool RaceOver { get; private set; }
    public EscapeActor HelperBot { get; private set; }
    /// <summary>Seconds until the helper comes into the active room (-1: already here or not applicable).</summary>
    public float HelperIn => Running && HelperBot == null && ActiveRoom != null && !ActiveRoom.Solved ? Mathf.Max(0f, HelperAfter - RoomTime) : -1f;
    float firstFinishAt = -1f, endAt = -1f;

    void Start()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        PlayerCount = Mathf.Clamp(PlayerPrefs.GetInt("EscapePlayers", 4), 1, 6);
        EscapeAudio.Ensure();
        if (GetComponent<EscapeAtmosphere>() == null) gameObject.AddComponent<EscapeAtmosphere>();
        SetupEnvironment();

        EscapeInteractable.ResetIds();
        EscapeInteractable.SeatOf = null;   // no owned stations: everything is yours
        PartyLink.SeedRandom(21);           // online: same rooms in the same order on every computer…
        Level = gameObject.AddComponent<EscapeLevel>();
        Rooms = Level.Build(this);          // …but SeedPuzzles() gives each player their own solutions
        SolvedAt = new float[Rooms.Length];

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

    /// <summary>Called by EscapeLevel once the room types are drawn: from here on the randomness is this player's own.</summary>
    public void SeedPuzzles() => Seed(21);

    static void Seed(int salt)
    {
        if (PartyLink.Active) Random.InitState(unchecked(PartyLink.Seed * 31 + salt + 7919 * (PartyLink.LocalMember + 1)));
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

    /// <param name="players">Outside a party match: you + (players − 1) ghost rivals. In a match: the party roster.</param>
    public void StartGame(int players)
    {
        if (State != Phase.Lobby) return;
        BuildRacers(players);

        var go = new GameObject("Jugador (tú)");
        go.transform.SetPositionAndRotation(SpawnPoint(Rooms[0], 1), Quaternion.identity);
        var a = go.AddComponent<EscapeActor>();
        a.Initialize(this, 0, true, Names[0], Models[0], Colors[0]);
        if (AutoHuman) go.AddComponent<EscapeBot>();
        Actors.Add(a);
        Human = a;
        Human.Role = AutoHuman ? 1 : 0;   // tests: the automatic player takes the job bots plans give to role 1

        Seed(22);
        foreach (var r in Rooms) r.OnTeamReady();
        State = Phase.Countdown;
        Countdown = 3.5f;
        View?.Snap();
    }

    void BuildRacers(int players)
    {
        Racers.Clear();
        if (MultiverseParty.InMatch)
        {
            var roster = MultiverseParty.CurrentRoster();
            PlayerCount = roster.Count;
            for (int s = 0; s < roster.Count; s++)
            {
                bool person = s == 0 || (roster.Humans != null && s < roster.Humans.Length && roster.Humans[s]);
                if (s < Names.Length) { Names[s] = roster.Names[s]; Colors[s] = roster.Colors[s]; Models[s] = PartyGames.ModelName(roster.Characters[s]); }
                if (!person && !GhostRivals) continue;
                // Offline every other member is a bot. Online other people race on their own computers.
                Racers.Add(new Racer { Slot = s, Member = roster.Members[s], Name = roster.Names[s], Color = roster.Colors[s], Local = s == 0, Ghost = !person });
            }
        }
        else
        {
            PlayerCount = Mathf.Clamp(players, 1, 6);
            PlayerPrefs.SetInt("EscapePlayers", PlayerCount);
            for (int s = 0; s < PlayerCount; s++)
                Racers.Add(new Racer { Slot = s, Member = s, Name = s == 0 ? "Tú" : Names[s], Color = Colors[s], Local = s == 0, Ghost = s != 0 });
        }
        Me = Racers.FirstOrDefault(r => r.Local);

        // Ghost times: same on every computer online (party seed), 65–110 s per room, the portal is quicker.
        int baseSeed = PartyLink.Active ? PartyLink.Seed : System.Environment.TickCount;
        foreach (var r in Racers)
        {
            if (!r.Ghost) continue;
            var rng = new System.Random(unchecked(baseSeed * 17 + r.Member * 101 + 5));
            r.Splits = new float[Rooms.Length];
            float t = 0f;
            for (int i = 0; i < Rooms.Length; i++)
            {
                bool last = i == Rooms.Length - 1;
                t += last ? 35f + (float)rng.NextDouble() * 25f : 65f + (float)rng.NextDouble() * 45f;
                r.Splits[i] = t;
            }
        }
    }

    public Vector3 SpawnPoint(EscapeRoom room, int slot) =>
        room.Entry + new Vector3((slot % 3 - 1) * 1.3f, .08f, -(slot / 3) * 1.15f);

    void Update()
    {
        if (State == Phase.Countdown)
        {
            if (Net != null && !PartyLink.IsHost) { if (Countdown > 3.4f) { Notice = PartyLink.WaitingText; noticeUntil = Time.time + .5f; } }   // the host starts everybody
            else if (Net != null && !PartyLink.AllReady) { Notice = PartyLink.WaitingText; noticeUntil = Time.time + .5f; }
            else
            {
                float before = Countdown;
                Countdown -= Time.deltaTime;
                if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f) EscapeAudio.PlayUI("count");
                if (Countdown <= 0f) StartRunning();
            }
        }
        if (RaceStarted && !RaceOver) { RaceClock += Time.deltaTime; GhostTick(); }
        if (State == Phase.Playing)
        {
            Elapsed += Time.deltaTime;
            for (int i = Actors.Count - 1; i >= 0; i--)
                if (Actors[i] != null && Actors[i].transform.position.y < -3f) Respawn(Actors[i], Actors[i] == Human ? "¡Al vacío! Vuelves a la entrada de la sala" : null);
            HelperTick();
        }
        if (helperLeaveAt > 0f && Time.time >= helperLeaveAt) RemoveHelper();
        RaceTick();
        if (Rooms != null) foreach (var r in Rooms) r.Tick();
        if (Notice.Length > 0 && Time.time > noticeUntil) Notice = "";

        var k = UnityEngine.InputSystem.Keyboard.current;
        if (k != null && k.mKey.wasPressedThisFrame && (HUD == null || !HUD.KeypadOpen)) EscapeAudio.ToggleMusic();
    }

    const string StartText = "¡A ESCAPAR!  ·  Cada uno en su torre: ¡el primero en salir gana!";

    void StartRunning()
    {
        if (State != Phase.Countdown) return;
        State = Phase.Playing;
        RaceStarted = true;
        roomStartedAt = 0f;
        EscapeAudio.PlayUI("go");
        Notify(StartText);
        Seed(100 + ActiveRoomIndex);
        ActiveRoom?.OnActivated();
        Net?.SendProgress();
    }

    /// <summary>Client: countdown and start signal from the host.</summary>
    public void ApplyNet(bool started, float countdown)
    {
        if (State != Phase.Countdown) return;
        if (started) { StartRunning(); return; }
        if (Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown < 3.4f) EscapeAudio.PlayUI("count");
        Countdown = countdown;
    }

    public void OnRoomSolved(EscapeRoom room, EscapeActor by)
    {
        int i = room.Index;
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
        if (Me != null) Me.Rooms = RoomsSolved;
        if (HelperBot != null) helperLeaveAt = Time.time + 1.2f;   // the helper waves and leaves
        if (i < Rooms.Length - 1)
        {
            Level.OpenDoor(i);
            Notify(by != null && by.Helper ? $"El ayudante abrió la sala {i + 1}  ·  ¡Sigue!" : $"¡SALA {i + 1} SUPERADA!  ·  Adelante");
            ActiveRoomIndex = i + 1;
            roomStartedAt = Elapsed;
            Seed(100 + i + 1);
            Rooms[i + 1].OnActivated();
            Net?.SendProgress();
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
        string key = "SalaEscape_MejorCarrera_" + Rooms.Length;
        float best = PlayerPrefs.GetFloat(key, 0);
        NewRecord = best <= 0 || Elapsed < best; BestTime = NewRecord ? Elapsed : best;
        if (NewRecord) { PlayerPrefs.SetFloat(key, Elapsed); PlayerPrefs.Save(); EscapeAudio.PlayUI("record", .8f); }
        if (Me != null) { Me.Finished = true; Me.Time = Elapsed; Me.Rooms = Rooms.Length; OnFinished(Me); }
        Net?.SendProgress();
        HUD?.ShowResults();
    }

    /// <summary>H: reveal the next clue of the active room now; you stay frozen for a few seconds.</summary>
    public void RequestClue()
    {
        var room = ActiveRoom;
        if (!Running || room == null) return;
        if (room.CluesShown >= room.Clues.Length) { Notify("No quedan más pistas en esta sala"); return; }
        if (!room.CanRequestClue) { Notify("Observa la sala un poco más antes de pedir una pista"); return; }
        room.RequestClue();
        Human?.Stun(ClueFreeze);
        EscapeAudio.PlayUI("beep", .8f);
        Notify("PISTA " + room.CluesShown + "  ·  " + ClueFreeze.ToString("0") + " s sin moverte");
    }

    // ------------------------------------------------------------------ helper bot (1:30 stuck in a room)

    void HelperTick()
    {
        var room = ActiveRoom;
        if (room == null || room.Solved) return;
        if (HelperBot == null && RoomTime >= HelperAfter) SpawnHelper(room);
        else if (HelperBot != null && helperLeaveAt < 0f && RoomTime >= HelperAfter + HelperGiveUp) room.ForceSolve(HelperBot);
    }

    void SpawnHelper(EscapeRoom room)
    {
        var go = new GameObject("Ayudante");
        go.transform.SetPositionAndRotation(SpawnPoint(room, 2), Quaternion.identity);
        var a = go.AddComponent<EscapeActor>();
        a.Initialize(this, 1, false, "Ayudante", "Captain_Barbarossa", HelperTint);
        a.Helper = true;
        a.Role = Human != null && Human.Role == 1 ? 2 : 1;
        go.AddComponent<EscapeBot>();
        Actors.Add(a);
        HelperBot = a;
        helperLeaveAt = -1f;
        assistSaved = AssistScale; AssistScale = 0f;   // no waiting: it solves straight away
        MultiverseFX.Ring(go.transform.position + Vector3.up * .05f, 3, HelperTint, .6f);
        EscapeAudio.PlayUI("beep", .9f);
        Notify("1:30 en esta sala  ·  ¡Llega un AYUDANTE a resolverla!");
    }

    void RemoveHelper()
    {
        helperLeaveAt = -1f;
        if (HelperBot == null) return;
        Actors.Remove(HelperBot);
        MultiverseFX.Ring(HelperBot.transform.position + Vector3.up * .05f, 3, HelperTint, .5f);
        Destroy(HelperBot.gameObject);
        HelperBot = null;
        AssistScale = assistSaved;
    }

    // ------------------------------------------------------------------ race

    public static string Clock(float t) => $"{Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}";

    /// <summary>Place among those who finished (by time, the same on every computer).</summary>
    public int Place(Racer r) => r == null || !r.Finished ? 0 : 1 + Racers.Count(o => o != r && o.Finished && o.Time < r.Time);

    public List<Racer> Ranking => Racers.OrderBy(r => r.Finished ? 0 : 1).ThenBy(r => r.Finished ? r.Time : 0f)
        .ThenByDescending(r => r.Rooms).ThenBy(r => r.Slot).ToList();

    void GhostTick()
    {
        foreach (var r in Racers)
        {
            if (!r.Ghost || r.Finished || r.Splits == null) continue;
            int done = 0;
            while (done < r.Splits.Length && r.Splits[done] <= RaceClock) done++;
            r.Rooms = done;
            if (done >= r.Splits.Length) { r.Finished = true; r.Time = r.Splits[r.Splits.Length - 1]; OnFinished(r); }
        }
    }

    /// <summary>Online: another player's progress.</summary>
    public void OnRemoteProgress(int member, int rooms, bool finished, float time)
    {
        var r = Racers.Find(x => !x.Local && !x.Ghost && x.Member == member);
        if (r == null) return;
        r.Rooms = Mathf.Max(r.Rooms, rooms);
        if (finished && !r.Finished) { r.Finished = true; r.Time = time; r.Rooms = Rooms.Length; OnFinished(r); }
    }

    void OnFinished(Racer r)
    {
        if (firstFinishAt < 0f) firstFinishAt = RaceClock;
        int p = Place(r);
        string t = Clock(r.Time);
        if (r.Local) Notify(p == 1 ? $"¡PRIMER LUGAR!  ·  {t}" : $"¡ESCAPASTE!  ·  {p}.º lugar  ·  {t}");
        else
        {
            Notify(p == 1 ? $"¡{r.Name.ToUpperInvariant()} LLEGA EN PRIMER LUGAR!  ·  {t}" : $"{r.Name} termina en {p}.º lugar  ·  {t}");
            EscapeAudio.PlayUI(p == 1 ? "record" : "beep", .6f);
        }
    }

    void RaceTick()
    {
        if (!RaceStarted || RaceOver) return;
        if (Net != null)
            foreach (var r in Racers) if (!r.Local && !r.Ghost && !PartyLink.IsHumanMember(r.Member)) r.Gone = true;   // left the room
        if (Net != null && !PartyLink.IsHost) return;   // the host decides when the race ends
        if (endAt < 0f)
        {
            bool everybody = Racers.All(r => r.Ghost || r.Gone || r.Finished);
            bool late = firstFinishAt >= 0f && RaceClock - firstFinishAt > FinishGrace;
            if (everybody || late) endAt = RaceClock + EndDelay;
        }
        else if (RaceClock >= endAt) EndRace(Racers.Where(r => r.Finished).OrderBy(r => r.Time).FirstOrDefault());
    }

    void EndRace(Racer winner)
    {
        if (RaceOver) return;
        if (Net != null && PartyLink.IsHost) Net.SendEnd(winner != null ? winner.Member : -1);
        Finalize(winner);
    }

    /// <summary>Client: the host closed the race.</summary>
    public void OnRaceOver(int winnerMember)
    {
        if (RaceOver) return;
        Finalize(Racers.Find(r => r.Member == winnerMember));
    }

    void Finalize(Racer winner)
    {
        RaceOver = true;
        if (State == Phase.Playing || State == Phase.Countdown)
        {
            State = Phase.Lost;
            RemoveHelper();
            EscapeAudio.PlayUI("lose");
        }
        HUD?.ShowResults();
        if (!MultiverseParty.InMatch) return;
        if (Net != null) { if (PartyLink.IsHost) PartyLink.FinishMember(winner != null ? winner.Member : -1); else PartyLink.Finish(-1); }
        else PartyLink.Finish(winner != null ? winner.Slot : -1);
    }

    // ------------------------------------------------------------------ helpers used by rooms, actors and the HUD

    /// <summary>A keypad code typed (or chosen by a bot).</summary>
    public bool NetSubmit(EscapeRoom room, string code, EscapeActor a) => room.KeypadSubmit(code, a);

    public void Notify(string text)
    {
        Notice = text; NoticeAt = Time.time; noticeUntil = Time.time + 3.6f;
    }

    public void Respawn(EscapeActor a, string message)
    {
        if (a == null) return;
        var room = Rooms[Mathf.Clamp(RoomIndexAt(a.transform.position), 0, Rooms.Length - 1)];
        EscapeAudio.Play("fall", a.transform.position, .8f);
        a.Knock(SpawnPoint(room, a.Helper ? 2 : 1), .5f);
        if (a.IsHuman && message != null) Notify(message);
    }

    public int RoomIndexAt(Vector3 world) => Mathf.FloorToInt((world.z + RoomLength * .5f) / RoomLength);
    public int RoomsSolved => Rooms == null ? 0 : Rooms.Count(r => r.Solved);
    public EscapeActor MostHelpful => Actors.OrderByDescending(a => a.Actions).FirstOrDefault();
    public EscapeActor MostValuable => Human;

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

    /// <summary>Bots (the helper, or the player in tests): walk through open doors to the active room, then follow its plan.</summary>
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

    // Co-op leftovers (2026-11-16), kept as no-ops so the rooms compile unchanged: there are no owned stations now.
    public int SeatOf(EscapeActor a) => -1;
    public EscapeActor ActorOfSeat(int seat) => null;
    public bool IsBotActor(EscapeActor a) => a != null && a.DrivenByBot;
    public void AskFor(EscapeActor from, EscapeInteractable it) { }
    public void OnFired(EscapeActor a, EscapeInteractable it) { }
    public void Want(EscapeInteractable it) { }
    public bool WantedRecently(EscapeInteractable it) => false;
    public string Speech => "";
    public string ObjectiveFor(EscapeRoom room) => room == null ? "" : room.Objective;

    public void Restart() { if (!MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

    public void ReturnToHub()
    {
        if (MultiverseParty.InMatch) return;
        if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
