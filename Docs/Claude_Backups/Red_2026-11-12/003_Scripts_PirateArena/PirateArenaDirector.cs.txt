using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PirateArenaDirector : MonoBehaviour
{
    private readonly List<PirateFighter> _fighters = new List<PirateFighter>();
    private readonly List<CannonHazard> _cannons = new List<CannonHazard>();
    private readonly List<SharkHazard> _sharks = new List<SharkHazard>();
    private readonly List<GrabbableProp> _props = new List<GrabbableProp>();
    private float _deckWidth;
    private float _deckLength;
    private float _deckY;
    private float _roundTime;
    private float _countdown = 3f;
    private Camera _camera;
    private PirateArenaBootstrap _bootstrap;
    private bool _finished;
    private string _result = string.Empty;
    private string _eventText = string.Empty;
    private float _eventTextUntil;

    public bool NetworkDriven { get; set; }
    public IReadOnlyList<CannonHazard> Cannons => _cannons;
    public IReadOnlyList<SharkHazard> Sharks => _sharks;
    public IReadOnlyList<GrabbableProp> Props => _props;
    public PirateRoundState CaptureState() => new PirateRoundState { Playing = IsPlaying, Finished = _finished, Countdown = _countdown, Elapsed = _roundTime, Winner = Winner == null ? -1 : _fighters.IndexOf(Winner) };
    public void ApplyState(PirateRoundState state)
    {
        IsPlaying = state.Playing; _finished = state.Finished; _countdown = state.Countdown; _roundTime = state.Elapsed;
        Winner = state.Winner >= 0 && state.Winner < _fighters.Count ? _fighters[state.Winner] : null;
        _result = Winner != null ? Winner.DisplayName + " gana la cubierta" : "¡Todos al agua!";
    }
    public bool IsPlaying { get; private set; }
    public float Countdown => _countdown;
    public bool Finished => _finished;
    public PirateFighter Winner { get; private set; }
    public PirateArenaSettings Settings => _bootstrap.settings;
    public PirateArenaBootstrap Art => _bootstrap;
    public Camera ArenaCamera => _camera;
    public float DeckWidth => _deckWidth;
    public float DeckLength => _deckLength;
    public float DeckY => _deckY;
    public IReadOnlyList<PirateFighter> Fighters => _fighters;

    public void Configure(float deckWidth, float deckLength, float deckY, Camera camera, PirateArenaBootstrap bootstrap)
    {
        _deckWidth = deckWidth;
        _deckLength = deckLength;
        _deckY = deckY;
        _camera = camera;
        _bootstrap = bootstrap;
        if (GetComponent<PirateKrakenHazard>() == null) gameObject.AddComponent<PirateKrakenHazard>();
        if (GetComponent<PirateArenaScoreboard>() == null) gameObject.AddComponent<PirateArenaScoreboard>();
    }

    public void Register(PirateFighter fighter) => _fighters.Add(fighter);
    public void Register(CannonHazard cannon) => _cannons.Add(cannon);
    public void Register(SharkHazard shark) => _sharks.Add(shark);
    public void Register(GrabbableProp prop) => _props.Add(prop);

    public bool DangerDirection(Vector3 position, out Vector3 direction)
    {
        foreach (CannonHazard cannon in _cannons)
            if (cannon.TryGetDanger(position, out direction)) return true;
        direction = Vector3.zero;
        return false;
    }

    public float SurfaceHeight(Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(point.x, 60f, point.z), Vector3.down, 70f,
            ~0, QueryTriggerInteraction.Ignore);
        float height = _deckY;
        foreach (RaycastHit hit in hits)
            if (hit.collider.TryGetComponent(out PirateArenaSurface surface) && surface.walkable && hit.normal.y > 0.6f)
                height = Mathf.Max(height, hit.point.y);
        return height;
    }

    public void BeginRound()
    {
        Winner = null;
        IsPlaying = false;
        _finished = false;
        _result = string.Empty;
        _roundTime = 0f;
        _countdown = 3f;
    }

    private void Update()
    {
        if (!NetworkDriven && (PirateSession.Instance == null || PirateSession.Instance.LocalReady)) Tick(Time.deltaTime);
    }

    public void Tick(float deltaTime)
    {
        if (_finished)
        {
            if (!NetworkDriven && !MultiverseParty.InMatch && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) RestartRound();
            return;
        }
        if (!IsPlaying)
        {
            _countdown -= deltaTime;
            if (_countdown <= 0f)
            {
                IsPlaying = true;
                foreach (CannonHazard cannon in _cannons) cannon.StartRound();
                foreach (SharkHazard shark in _sharks) shark.StartRound();
            }
            return;
        }

        _roundTime += deltaTime;
        foreach (PirateFighter fighter in _fighters)
        {
            if (fighter == null || !fighter.IsAlive) continue;
            Vector3 p = fighter.transform.position;
            if (p.y < 0.3f)
                Eliminate(fighter, "cayó al agua");
        }
        CheckWinner();
    }

    public bool IsNearEdge(PirateFighter fighter, float margin)
    {
        Vector3 p = fighter.transform.position;
        return Mathf.Abs(p.x) > _deckWidth * 0.5f - margin || Mathf.Abs(p.z) > _deckLength * 0.5f - margin;
    }

    public PirateFighter NearestLivingFighter(Vector3 position, PirateFighter exclude = null, bool edgeOnly = false)
    {
        PirateFighter best = null;
        float bestDistance = float.MaxValue;
        foreach (PirateFighter fighter in _fighters)
        {
            if (fighter == null || !fighter.IsAlive || fighter == exclude || fighter.IsHeld) continue;
            if (edgeOnly && !IsNearEdge(fighter, 1.4f)) continue;
            float d = (fighter.transform.position - position).sqrMagnitude;
            if (d < bestDistance) { best = fighter; bestDistance = d; }
        }
        return best;
    }

    public PirateFighter RandomLivingFighter()
    {
        List<PirateFighter> living = new List<PirateFighter>();
        foreach (PirateFighter fighter in _fighters)
            if (fighter != null && fighter.IsAlive) living.Add(fighter);
        return living.Count == 0 ? null : living[Random.Range(0, living.Count)];
    }

    public void Eliminate(PirateFighter fighter, string reason)
    {
        if (fighter == null || !fighter.IsAlive) return;
        fighter.Eliminate();
        if (reason.Contains("agua")) PirateArenaAudio.Play(PirateArenaAudio.Sound.Splash, fighter.transform.position);
        _eventText = fighter.IsHuman ? "¡ELIMINADO! " + reason : fighter.DisplayName + " " + reason;
        _eventTextUntil = Time.time + 2.4f;
        // Resolve after all eliminations in this simulation tick, including simultaneous falls.
    }

    public void Blast(Vector3 point, float radius, float strength)
    {
        foreach (PirateFighter fighter in _fighters)
        {
            if (fighter == null || !fighter.IsAlive) continue;
            Vector3 delta = fighter.transform.position - point;
            if (delta.magnitude > radius) continue;
            Vector3 impulse = (delta.normalized + Vector3.up * 0.48f) * strength *
                              Mathf.Clamp01(1f - delta.magnitude / radius);
            fighter.TakePush(impulse);
        }
        foreach (GrabbableProp prop in _props)
        {
            Rigidbody body = prop != null ? prop.GetComponent<Rigidbody>() : null;
            if (body == null || body.isKinematic) continue;
            if ((body.worldCenterOfMass - point).sqrMagnitude <= radius * radius)
                body.AddExplosionForce(strength * 0.75f, point, radius, 1.6f, ForceMode.VelocityChange);
        }
        ShowEvent("¡Cañonazo!");
    }

    public void ShowEvent(string message)
    {
        _eventText = message;
        _eventTextUntil = Time.time + 2f;
    }

    private void CheckWinner()
    {
        if (_finished || !IsPlaying) return;
        List<PirateFighter> living = new List<PirateFighter>();
        foreach (PirateFighter fighter in _fighters)
            if (fighter != null && fighter.IsAlive) living.Add(fighter);
        if (living.Count > 1) return;
        IsPlaying = false;
        _finished = true;
        Winner = living.Count == 1 ? living[0] : null;
        _result = living.Count == 1 ? living[0].DisplayName + " gana la cubierta" : "¡Todos al agua!";
        if (!NetworkDriven) MultiverseParty.ReportMinigame(Winner != null ? _fighters.IndexOf(Winner) : -1);
    }

    public void RestartRound()
    {
        foreach (PirateFighter fighter in _fighters) if (fighter != null) fighter.ReleaseGrab(false);
        foreach (PirateFighter fighter in _fighters)
            if (fighter != null) fighter.Respawn();
        foreach (GrabbableProp prop in _props) if (prop != null) prop.ResetProp();
        foreach (CannonHazard cannon in _cannons) cannon.ResetHazard();
        foreach (SharkHazard shark in _sharks) shark.ResetHazard();
        BeginRound();
    }

    private GUIStyle hudText, hudTitle, banner, statusText, tagText;

    private void OnGUI()
    {
        if (PirateSession.Instance != null && PirateSession.Instance.MenuOpen) return;
        if (hudText == null)
        {
            hudText = new GUIStyle(GUI.skin.label) { fontSize = 16, font = MultiverseUI.BodyFont };
            hudText.normal.textColor = new Color(0.91f, 0.96f, 0.96f);
            hudTitle = new GUIStyle(hudText) { fontSize = 19, font = MultiverseUI.DisplayFont };
            hudTitle.normal.textColor = new Color(1f, .85f, .22f);
            banner = new GUIStyle(hudTitle) { fontSize = 34, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            statusText = new GUIStyle(hudText) { fontSize = 16, wordWrap = true };
        }
        float scale = Mathf.Max(0.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
        float width = Screen.width / scale, height = Screen.height / scale;
        int alive = 0;
        foreach (PirateFighter fighter in _fighters) if (fighter != null && fighter.IsAlive) alive++;
        Panel(new Rect(18, 18, 265, 82));
        GUI.Label(new Rect(32, 27, 260, 30), "ARENA DEL CAOS", hudTitle);
        GUI.Label(new Rect(32, 61, 260, 28), "EN PIE  " + alive + " / " + _fighters.Count + "    ·    " + FormatTime(_roundTime), hudText);
        Panel(new Rect(18, height - 54, 1180, 36));
        GUI.Label(new Rect(32, height - 51, 1155, 30), "CLICS agarrar   ·   E golpear   ·   SHIFT levantar   ·   F lanzar   ·   " + (MultiverseParty.InMatch ? "" : "ESC menú / controles   ·   ") + "M música", statusText);
        DrawFighterStatus(width, scale);
        if (!IsPlaying && !_finished)
            GUI.Label(new Rect(width * 0.25f, height * 0.35f, width * 0.5f, 80),
                "¡PREPÁRATE!  " + Mathf.CeilToInt(Mathf.Max(0f, _countdown)), banner);
        if (_finished)
        {
            Panel(new Rect(width * 0.5f - 300f, height * 0.35f, 600f, 155f));
            GUI.Label(new Rect(width * 0.5f - 280f, height * 0.35f + 12f, 560f, 80f), _result, banner);
            GUI.Label(new Rect(width * 0.5f - 280f, height * 0.35f + 98f, 560f, 40f), (MultiverseParty.InMatch ? "Volviendo a la plaza…" : NetworkDriven ? "El anfitrión inicia otra ronda desde Esc" : "Pulsa R para otra ronda"), banner);
        }
        else if (Time.time < _eventTextUntil)
            GUI.Label(new Rect(width * 0.5f - 300f, 24f, 600f, 84f), _eventText, banner);
        GUI.matrix = previous;
    }

    private void DrawFighterStatus(float width, float scale)
    {
        PirateFighter player = null;
        foreach (var fighter in _fighters)
        {
            if (fighter == null) continue;
            if (fighter.IsHuman) player = fighter;
            if (!fighter.IsAlive || !fighter.IsStunned || _camera == null) continue;
            Vector3 screen = _camera.WorldToScreenPoint(fighter.transform.position + Vector3.up * 1.5f);
            if (screen.z <= 0f || screen.z > 20f) continue;
            GUI.Label(new Rect(screen.x / scale - 80f, (Screen.height - screen.y) / scale - 16f, 160f, 28f),
                fighter.IsKnockedOut ? "KO" : "Aturdido", tagText ??= new GUIStyle(hudTitle) { alignment = TextAnchor.MiddleCenter });
        }
        // Detailed stamina and recovery are local authority state; online HUD needs its own synchronization.
        if (player == null || player.IsProxy) return;
        if (player.IsAlive && !player.IsStunned && !player.IsHeld && !player.IsRecovering &&
            player.GripStamina > .98f && player.HitStrain <= 0f && !player.LeftReach && !player.RightReach && !player.GripNeedsRelease) return;
        float x = width - 306f;
        Panel(new Rect(x, 18f, 288f, 172f));
        string state = !player.IsAlive ? "ELIMINADO · observando" : player.IsKnockedOut ? "KO · " + player.KnockoutRemaining.ToString("0.0") + " s" :
            player.IsStunned ? "ATURDIDO" : player.IsHeld ? "ATRAPADO · Espacio " + player.EscapeProgress + "/3" :
            player.IsRecovering ? "RECUPERANDO · protegido" : "¡AGARRA Y LANZA!";
        GUI.Label(new Rect(x + 14f, 27f, 260f, 28f), state, hudTitle);
        GUI.Label(new Rect(x + 14f, 57f, 260f, 24f), "Agarre " + Mathf.RoundToInt(player.GripStamina * 100f) + "%", statusText);
        Meter(new Rect(x + 14f, 84f, 260f, 9f), player.GripStamina, player.GripStamina < .25f ? new Color(1f, .35f, .2f) : new Color(.25f, .85f, .7f));
        GUI.Label(new Rect(x + 14f, 99f, 260f, 22f), "Golpes acumulados", statusText);
        Meter(new Rect(x + 14f, 123f, 260f, 7f), player.IsKnockedOut ? 1f : player.HitStrain, new Color(1f, .65f, .2f));
        string hands = "Izq: " + (player.LeftGripped ? "AGARRADA" : player.LeftReach ? "buscando" : "libre") +
            " · Der: " + (player.RightGripped ? "AGARRADA" : player.RightReach ? "buscando" : "libre");
        GUI.Label(new Rect(x + 14f, 137f, 260f, 42f), player.GripNeedsRelease ? "Suelta los botones para volver a agarrar" : hands, statusText);
    }

    private static void Meter(Rect rect, float fill, Color color)
    {
        MultiverseUI.DrawMeter(rect, fill, new Color(.12f, .06f, .2f), color);
    }

    private static void Panel(Rect rect)
    {
        // Shared rounded party-game panel: violet fill, thick pink edge, soft drop shadow.
        MultiverseUI.DrawPanel(rect, new Color(.16f, .06f, .26f, .96f), new Color(1f, .3f, .62f));
    }

    private static string FormatTime(float seconds)
    {
        int value = Mathf.FloorToInt(seconds);
        return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
    }
}
