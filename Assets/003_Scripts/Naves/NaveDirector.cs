using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// ARMA TU NAVE (2026-11-17) · party minigame for 2–6 players. Everybody starts in the village plaza next to the
/// ghost of their own ship. Its five pieces (cabina, motor, two wings, tail) are hidden around the village: inside
/// the houses behind the furniture, behind hedges, carts, stalls… Find one of each, carry it to your pad and it
/// snaps in. Shove a carrier to make them drop it. After a while the radar shows the pieces. First ship to take off
/// wins; when time runs out, the most complete ship wins.
/// The host owns the pieces and the ships; every computer moves its own player.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class NaveDirector : MonoBehaviour
{
    public const float RoundSeconds = 180f, RadarAt = 55f, CountdownSeconds = 4f, PadRadius = 3.1f;

    [Tooltip("Personaje (Fiestero de juguete con CharacterLook y Animator).")] public GameObject characterPrefab;
    public static bool AutoHuman;

    public enum Phase { Waiting, Countdown, Running, Finished }
    public Phase State { get; private set; } = Phase.Waiting;
    public float Countdown { get; private set; } = CountdownSeconds;
    public float Elapsed { get; private set; }
    public float Remaining => Mathf.Max(0f, RoundSeconds - Elapsed);
    public bool Radar => State == Phase.Running && Elapsed >= RadarAt;
    public readonly List<NavePlayer> Players = new();
    public readonly List<NaveShip> Ships = new();
    public readonly List<NavePiece> Pieces = new();
    public NavePlayer Human { get; private set; }
    public NaveVillage Village { get; private set; }
    public NaveCamera View { get; private set; }
    public NaveNet Net { get; private set; }
    public bool IsClient => Net != null && PartyLink.IsClient;
    public int Seed { get; private set; }
    public string Result { get; private set; } = "";
    public int WinnerSlot { get; private set; } = -1;
    public string Notice { get; private set; } = ""; float noticeUntil;
    public readonly List<string> Feed = new();
    public string WaitingText => Net != null ? PartyLink.WaitingText : "";
    bool paused, radarSaid, thirtySaid;
    public bool Paused { get => paused; set { paused = value; if (Net == null) Time.timeScale = value ? 0f : 1f; } }
    public bool InputFree => !Paused && State == Phase.Running && Human != null;
    public bool Dirty { get; set; }

    void Start()
    {
        Application.runInBackground = true; Time.timeScale = 1f;
        var roster = MultiverseParty.CurrentRoster();
        Seed = PartyLink.Active ? unchecked(PartyLink.Seed * 7121 + PartyLink.Round * 389) : Random.Range(1, 999999);
        RenderSettings.ambientLight = new Color(.62f, .62f, .66f); RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.7f, .82f, .95f); RenderSettings.fogStartDistance = 45f; RenderSettings.fogEndDistance = 110f;

        Village = gameObject.AddComponent<NaveVillage>();
        Village.Build(1);   // 2026-11-17: always the same village, so the pieces always hide in the same places

        int count = Mathf.Clamp(roster.Count, 2, MultiverseParty.MaxSize);
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            int pad = PartyLink.Active ? Mathf.Max(0, member) % 6 : i;
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            Color color = i < roster.Colors.Length ? roster.Colors[i] : Color.white;
            var shipGo = new GameObject("Nave de " + name); var padPos = NaveVillage.PadPos(pad);
            shipGo.transform.SetPositionAndRotation(padPos, Quaternion.Euler(0, NaveVillage.PadAngle(pad) + 90f, 0));
            var ship = shipGo.AddComponent<NaveShip>(); ship.Build(i, color, name); Ships.Add(ship);
        }
        // Pieces (2026-11-17): always in the same places, as far from each other as possible. One of each type per
        // player plus a spare set; the hiding spots are picked by farthest-point order starting from the far north,
        // and the types go round-robin over that order, so equal pieces end up on opposite sides of the village.
        var order = SpreadSpots(Village.Spots);
        int total = NaveKit.Types * (count + 1), perType = count + 1;
        var placed = new List<(Vector3 at, int type)>(); var used = new int[NaveKit.Types];
        for (int id = 0; id < total; id++)
        {
            var at = NaveVillage.OnMesh(Village.Spots[order[id % order.Count]] + (id >= order.Count ? new Vector3(.6f, 0, .6f) : Vector3.zero), 1.5f);
            // The type whose closest twin is farthest away (so equal pieces are spread over the whole village).
            int type = 0; float far = -1f;
            for (int t = 0; t < NaveKit.Types; t++)
            {
                if (used[t] >= perType) continue;
                float near = float.MaxValue; foreach (var (pa, pt) in placed) if (pt == t) near = Mathf.Min(near, (pa - at).sqrMagnitude);
                if (near > far) { far = near; type = t; }
            }
            used[type]++; placed.Add((at, type));
            Pieces.Add(NavePiece.Create(this, id, type, at));
        }
        for (int i = 0; i < count; i++)
        {
            int member = PartyLink.Active ? PartyLink.MemberOfSlot(i) : (i < roster.Members.Length ? roster.Members[i] : i);
            string name = i < roster.Names.Length ? roster.Names[i] : "Jugador " + (i + 1);
            var go = new GameObject(name, typeof(CharacterController));
            go.transform.SetPositionAndRotation(SpawnPoint(i), Quaternion.LookRotation(-Ships[i].transform.position.normalized));
            var p = go.AddComponent<NavePlayer>(); bool puppet = PartyLink.IsPuppet(i);
            p.Init(this, i, member, name, i < roster.Colors.Length ? roster.Colors[i] : Color.white, i == 0, puppet, characterPrefab, unchecked(Seed + member * 3301 + 5));
            Players.Add(p); if (i == 0) Human = p;
            if (!puppet && (i != 0 || AutoHuman) && !PartyLink.IsRemoteHuman(i)) go.AddComponent<NaveBot>();
        }

        var cam = Camera.main;
        if (cam == null) { var c = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); c.tag = "MainCamera"; cam = c.GetComponent<Camera>(); }
        View = cam.GetComponent<NaveCamera>(); if (View == null) View = cam.gameObject.AddComponent<NaveCamera>(); View.Director = this;
        gameObject.AddComponent<NaveHUD>().Director = this;
        NaveAudio.Music(true);
        if (PartyLink.Active) { Net = gameObject.AddComponent<NaveNet>(); Net.Init(this); }
    }

    void OnDestroy() { Time.timeScale = 1f; NaveAudio.Release(); }

    /// <summary>Farthest-point order: each next spot is the one farthest from all the spots already taken.</summary>
    static List<int> SpreadSpots(List<Vector3> spots)
    {
        var order = new List<int>(); if (spots.Count == 0) return order;
        int first = 0; for (int i = 1; i < spots.Count; i++) if (spots[i].z > spots[first].z) first = i;
        order.Add(first);
        var best = spots.Select(p => (p - spots[first]).sqrMagnitude).ToArray();
        while (order.Count < spots.Count)
        {
            int pick = -1; float far = -1f;
            for (int i = 0; i < spots.Count; i++) if (!order.Contains(i) && best[i] > far) { far = best[i]; pick = i; }
            order.Add(pick);
            for (int i = 0; i < spots.Count; i++) best[i] = Mathf.Min(best[i], (spots[i] - spots[pick]).sqrMagnitude);
        }
        return order;
    }

    public Vector3 SpawnPoint(int slot)
    {
        var pad = Ships[slot].transform.position;
        return NaveVillage.OnMesh(pad * .62f, 2f) + Vector3.up * .1f;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) Paused = !Paused;
        if (k != null && k.rKey.wasPressedThisFrame && State == Phase.Finished && !MultiverseParty.InMatch) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        float dt = Time.deltaTime;
        if (!IsClient)
        {
            switch (State)
            {
                case Phase.Waiting: if (Net == null || PartyLink.AllReady) { State = Phase.Countdown; Countdown = CountdownSeconds; } break;
                case Phase.Countdown:
                    float before = Countdown; Countdown -= dt;
                    if (Mathf.CeilToInt(before) != Mathf.CeilToInt(Countdown) && Countdown > 0f && Countdown <= 3f) NaveAudio.UI("beep", .7f);
                    if (Countdown <= 0f) { State = Phase.Running; Elapsed = 0f; NaveAudio.UI("go", .8f); Dirty = true; }
                    break;
                case Phase.Running:
                    Elapsed += dt;
                    HostInstalls();
                    if (Elapsed >= RoundSeconds) Finish(BestShip());
                    break;
            }
        }
        else if (State == Phase.Running) Elapsed += dt;
        if (State == Phase.Running && !radarSaid && Elapsed >= RadarAt) { radarSaid = true; Notify("¡RADAR! Las piezas que te faltan brillan con un haz de luz"); NaveAudio.UI("radar", .8f); }
        if (State == Phase.Running && !thirtySaid && Remaining <= 30f) { thirtySaid = true; Notify("¡Quedan 30 segundos!"); NaveAudio.UI("alarm", .6f); }
        if (Human != null) Village.FadeRoofs(Human.transform.position);
        if (Time.time > noticeUntil) Notice = "";
    }

    public void Notify(string text) { Notice = text; noticeUntil = Time.time + 3.5f; }
    void FeedLine(string t) { Feed.Insert(0, t); if (Feed.Count > 5) Feed.RemoveAt(5); }

    public bool NeedsLocal(int type) => Human != null && !Ships[Human.Slot].Has[type];
    public bool Needs(int slot, int type) => !Ships[slot].Has[type];

    public NavePiece NearestFreePiece(Vector3 from, float range)
    {
        NavePiece best = null; float bd = range;
        foreach (var p in Pieces) { if (p.Is != NavePiece.State.Free) continue; float d = Vector3.Distance(from, p.transform.position); if (d < bd) { bd = d; best = p; } }
        return best;
    }

    // ------------------------------------------------------------------ requests (any computer) → host decisions

    public void RequestPick(NavePlayer p, NavePiece piece) { if (IsClient) Net.SendPick(piece.Id); else HostPick(p.Slot, piece.Id); }
    public void RequestDrop(NavePlayer p) { if (IsClient) Net.SendDrop(); else HostDrop(p.Slot, Vector3.zero); }
    public void RequestShove(NavePlayer from, NavePlayer target, Vector3 dir) { if (IsClient) Net.SendShove(target.Member, dir); else HostShove(from.Slot, target.Slot, dir); }

    public void HostPick(int slot, int pieceId)
    {
        if (State != Phase.Running || slot < 0 || slot >= Players.Count || pieceId < 0 || pieceId >= Pieces.Count) return;
        var p = Players[slot]; var piece = Pieces[pieceId];
        if (p.Carrying != null || piece.Is != NavePiece.State.Free || Vector3.Distance(p.transform.position, piece.transform.position) > NavePlayer.PickRange + 1.2f) return;
        piece.Carry(slot); p.Carrying = piece; Dirty = true;
        NaveAudio.Play("pick", piece.transform.position, p.IsHuman ? 1f : .5f);
        if (p.IsHuman) Notify(Needs(slot, piece.Type) ? "¡" + NaveKit.Names[piece.Type] + "! Llévala a tu nave" : "Ya tienes " + NaveKit.Names[piece.Type] + ": suéltala con Q o quítasela a otro");
    }

    public void HostDrop(int slot, Vector3 fling)
    {
        if (slot < 0 || slot >= Players.Count) return;
        var p = Players[slot]; var piece = p.Carrying; if (piece == null) return;
        p.Carrying = null;
        var at = p.transform.position + (fling.sqrMagnitude > .01f ? fling : p.transform.forward * 1.1f);
        piece.Drop(NaveVillage.OnMesh(at, 3f)); Dirty = true;
        NaveAudio.Play("drop", at, .6f);
    }

    public void HostShove(int from, int target, Vector3 dir)
    {
        if (State != Phase.Running || target < 0 || target >= Players.Count) return;
        var t = Players[target];
        if (t.Carrying != null)
        {
            FeedLine(Players[from].DisplayName + " le quitó " + NaveKit.Names[t.Carrying.Type] + " a " + t.DisplayName);
            HostDrop(target, dir.normalized * 1.8f);
        }
        if (PartyLink.OwnsSlot(target)) t.Knock(dir); else Net?.SendKnock(t.Member, dir);
        if (Net != null) Net.SendShoveFx(Players[from].Member);
    }

    void HostInstalls()
    {
        foreach (var p in Players)
        {
            var piece = p.Carrying; if (piece == null) continue;
            var ship = Ships[p.Slot];
            Vector3 d = p.transform.position - ship.transform.position; d.y = 0;
            if (d.magnitude > PadRadius || ship.Has[piece.Type]) continue;
            p.Carrying = null; piece.Install(p.Slot);
            ship.Install(piece.Type, p.DisplayName, true); ship.LastInstallAt = Elapsed; Dirty = true;
            FeedLine(p.DisplayName + " montó " + NaveKit.Names[piece.Type] + " (" + ship.Count + "/5)");
            if (p.IsHuman) Notify("¡" + NaveKit.Names[piece.Type] + " montada! " + ship.Count + "/5");
            if (ship.Count >= NaveKit.Types) { Finish(p.Slot); return; }
        }
    }

    int BestShip() => Ships.OrderByDescending(s => s.Count).ThenBy(s => s.LastInstallAt < 0 ? 9999f : s.LastInstallAt).First().Slot;

    void Finish(int slot)
    {
        if (State == Phase.Finished) return;
        State = Phase.Finished; WinnerSlot = slot;
        var w = slot >= 0 ? Players[slot] : null;
        Result = w == null ? "SIN GANADOR" : Ships[slot].Count >= NaveKit.Types ? "¡LA NAVE DE " + w.DisplayName.ToUpperInvariant() + " DESPEGA!" : "¡GANA " + w.DisplayName.ToUpperInvariant() + "! (NAVE MÁS COMPLETA)";
        Celebrate(); Dirty = true; Net?.SendWorld();
        PartyLink.Finish(WinnerSlot);
    }

    void Celebrate()
    {
        if (WinnerSlot >= 0 && WinnerSlot < Players.Count)
        {
            Players[WinnerSlot].Celebrating = true;
            if (Ships[WinnerSlot].Count >= NaveKit.Types) Ships[WinnerSlot].Launch();
            MultiverseFX.Rainbow(Ships[WinnerSlot].transform.position + Vector3.up * 2f, 40, 8);
        }
        NaveAudio.UI(WinnerSlot == 0 ? "win" : "lose", .9f); NaveAudio.FadeMusic(.15f); Paused = false;
    }

    // ------------------------------------------------------------------ client side

    public void ApplyWorld(Phase phase, float countdown, float elapsed, int winnerSlot, string result)
    {
        if (State == Phase.Finished) return;
        if (phase == Phase.Countdown)
        {
            if (Mathf.CeilToInt(Countdown) != Mathf.CeilToInt(countdown) && countdown > 0f && countdown <= 3f) NaveAudio.UI("beep", .7f);
            Countdown = countdown; State = Phase.Countdown;
        }
        else if (phase == Phase.Running)
        {
            if (State != Phase.Running) { State = Phase.Running; Elapsed = elapsed; NaveAudio.UI("go", .8f); }
            float diff = elapsed - Elapsed; Elapsed = Mathf.Abs(diff) > .4f ? elapsed : Elapsed + diff * .25f;
        }
        else if (phase == Phase.Finished) { State = Phase.Finished; WinnerSlot = winnerSlot; Result = result; Celebrate(); PartyLink.Finish(-1); }
        else State = phase;
    }

    /// <summary>Client: piece state from the host.</summary>
    public void ApplyPiece(int id, NavePiece.State state, int holderSlot, Vector3 pos)
    {
        if (id < 0 || id >= Pieces.Count) return;
        var piece = Pieces[id];
        foreach (var p in Players) if (p.Carrying == piece && (state != NavePiece.State.Carried || p.Slot != holderSlot)) p.Carrying = null;
        switch (state)
        {
            case NavePiece.State.Free:
                if (piece.Is != NavePiece.State.Free || (piece.transform.position - pos).sqrMagnitude > .04f) piece.Drop(pos);
                break;
            case NavePiece.State.Carried:
                if (holderSlot < 0 || holderSlot >= Players.Count) break;
                if (piece.Is != NavePiece.State.Carried || piece.HolderSlot != holderSlot)
                {
                    piece.Carry(holderSlot);
                    if (holderSlot == 0) { NaveAudio.UI("pick", .9f); Notify(Needs(0, piece.Type) ? "¡" + NaveKit.Names[piece.Type] + "! Llévala a tu nave" : "Ya tienes " + NaveKit.Names[piece.Type] + ": suéltala con Q"); }
                }
                Players[holderSlot].Carrying = piece;
                break;
            case NavePiece.State.Installed:
                if (holderSlot < 0 || holderSlot >= Ships.Count) break;
                if (piece.Is != NavePiece.State.Installed)
                {
                    piece.Install(holderSlot);
                    Ships[holderSlot].Install(piece.Type, Players[holderSlot].DisplayName, true);
                    FeedLine(Players[holderSlot].DisplayName + " montó " + NaveKit.Names[piece.Type] + " (" + Ships[holderSlot].Count + "/5)");
                }
                break;
        }
    }

    public List<NavePlayer> Ranking() => Players.OrderByDescending(p => p.Slot == WinnerSlot).ThenByDescending(p => Ships[p.Slot].Count).ThenBy(p => Ships[p.Slot].LastInstallAt < 0 ? 9999f : Ships[p.Slot].LastInstallAt).ToList();

    public void Quit()
    {
        Paused = false;
        if (MultiverseParty.InMatch) MultiverseParty.Instance.LeaveToMenu();
        else if (Application.CanStreamedLevelBeLoaded("MundoDeInicio")) SceneManager.LoadScene("MundoDeInicio");
    }
}
