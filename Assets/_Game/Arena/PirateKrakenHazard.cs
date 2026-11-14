using UnityEngine;

// Kraken (2026-11-10, local games only — online rounds keep the replicated hazards unchanged).
// Every 15-22 s a tentacle rises beside the ship, a red ring marks where it will slam for 1.7 s,
// then it smashes the deck: anyone inside the ring is knocked down and flung away from the impact.
public sealed class PirateKrakenHazard : MonoBehaviour
{
    PirateArenaDirector arena;
    Transform tentacle; Transform[] segments; Material skin, spots, warn; GameObject marker;
    enum Phase { Idle, Rising, Slam, Recover }
    Phase phase; float phaseStart, nextAt; Vector3 basePoint, target; bool wasPlaying;
    AudioClip rise, slam;
    const int Segments = 22; const float Radius = 2.4f;
    // Online party (2026-11-12): the host picks the target and tells the clients, which only show the attack.
    [System.NonSerialized] public bool PartyHost, PartyClient;
    public System.Action<Vector3, Vector3> OnBegin;

    void Start()
    {
        arena = GetComponent<PirateArenaDirector>();
        rise = Resources.Load<AudioClip>("PirateAudio/v2/kraken_rise"); slam = Resources.Load<AudioClip>("PirateAudio/v2/kraken_slam");
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        skin = new Material(lit) { color = new Color(.55f, .22f, .6f) }; spots = new Material(lit) { color = new Color(.95f, .7f, .8f) };
        tentacle = new GameObject("Tentáculo del kraken").transform; segments = new Transform[Segments];
        for (int i = 0; i < Segments; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(s.GetComponent<Collider>()); s.transform.SetParent(tentacle, false);
            s.GetComponent<Renderer>().sharedMaterial = skin; segments[i] = s.transform;
            if (i % 2 == 0 && i < Segments - 2)
            {
                var sucker = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(sucker.GetComponent<Collider>());
                sucker.transform.SetParent(s.transform, false); sucker.transform.localPosition = new Vector3(0, -.38f, .2f); sucker.transform.localScale = new Vector3(.38f, .2f, .38f);
                sucker.GetComponent<Renderer>().sharedMaterial = spots;
            }
        }
        tentacle.gameObject.SetActive(false);
        marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(marker.GetComponent<Collider>()); marker.name = "Aviso del kraken";
        warn = CrazyRaceFX.Fresnel(new Color(1, .15f, .2f), false, .45f, 1.2f); warn.SetFloat("_Pulse", 1);
        marker.GetComponent<Renderer>().sharedMaterial = warn; marker.transform.localScale = new Vector3(Radius * 2, .02f, Radius * 2); marker.SetActive(false);
    }
    bool Allowed => arena != null && (!arena.NetworkDriven || PartyHost || PartyClient) && arena.IsPlaying && !arena.Finished;
    void Update()
    {
        if (arena == null) return;
        if (!Allowed) { if (phase != Phase.Idle) Hide(); wasPlaying = false; return; }
        if (!wasPlaying) { wasPlaying = true; nextAt = Time.time + Random.Range(11f, 15f); }
        float t = Time.time - phaseStart;
        switch (phase)
        {
            case Phase.Idle: if (Time.time >= nextAt && !PartyClient) Begin(); break;
            case Phase.Rising:
                Pose(Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 1.2f)), 0);
                marker.transform.localScale = new Vector3(Radius * 2, .02f, Radius * 2) * (1 + Mathf.Sin(Time.time * 14) * .04f);
                if (t > 1.7f) { phase = Phase.Slam; phaseStart = Time.time; }
                break;
            case Phase.Slam:
                Pose(1, Mathf.Clamp01(t / .22f));
                if (t > .22f) { Impact(); phase = Phase.Recover; phaseStart = Time.time; }
                break;
            case Phase.Recover:
                Pose(1 - Mathf.Clamp01((t - .6f) / .9f), 1 - Mathf.Clamp01((t - .3f) / .5f) * .3f);
                if (t > 1.5f) { Hide(); nextAt = Time.time + Random.Range(15f, 22f); }
                break;
        }
    }
    void Begin()
    {
        PirateFighter victim = null; int alive = 0;
        foreach (var f in arena.Fighters) if (f != null && f.IsAlive && Random.Range(0, ++alive) == 0) victim = f;
        if (victim == null) { nextAt = Time.time + 5; return; }
        float hw = arena.DeckWidth * .5f, hl = arena.DeckLength * .5f;
        Vector3 p = victim.transform.position + new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
        p.x = Mathf.Clamp(p.x, -hw + 1.2f, hw - 1.2f); p.z = Mathf.Clamp(p.z, -hl + 1.5f, hl - 1.5f);
        p.y = arena.SurfaceHeight(p); target = p;
        float side = p.x >= 0 ? 1 : -1; basePoint = new Vector3(side * (hw + 3.2f), -1.2f, p.z);
        OnBegin?.Invoke(target, basePoint);
        Attack();
    }
    /// <summary>Client: the host's kraken attacks here.</summary>
    public void NetBegin(Vector3 at, Vector3 from)
    {
        if (marker == null || tentacle == null) return;
        target = at; basePoint = from; wasPlaying = true;
        Attack();
    }
    void Attack()
    {
        marker.transform.position = target + Vector3.up * .05f; marker.SetActive(true);
        tentacle.gameObject.SetActive(true); phase = Phase.Rising; phaseStart = Time.time;
        PirateArenaFx.Spray(new Vector3(basePoint.x, .05f, basePoint.z));
        if (rise != null) PirateArenaAudio.PlayClip(rise, basePoint, .9f);
        arena.ShowEvent("¡KRAKEN! Sal del círculo rojo");
    }
    // rise: 0 under water .. 1 fully raised; slam: 0 raised .. 1 lying across the target.
    void Pose(float riseK, float slamK)
    {
        Vector3 up = basePoint + Vector3.up * (1.2f + 9 * riseK);
        Vector3 tip = Vector3.Lerp(up + (target - basePoint).normalized * 1.5f, target + Vector3.up * .45f, slamK);
        Vector3 ctrl = Vector3.Lerp(basePoint + Vector3.up * 6 * riseK, basePoint + Vector3.up * 7 + (target - basePoint) * .35f, slamK);
        for (int i = 0; i < Segments; i++)
        {
            float u = i / (float)(Segments - 1), w = 1 - u;
            Vector3 pos = w * w * basePoint + 2 * w * u * ctrl + u * u * tip;
            pos += new Vector3(Mathf.Sin(Time.time * 3 + u * 5), 0, Mathf.Cos(Time.time * 2.4f + u * 4)) * .25f * (1 - slamK) * u;
            segments[i].position = pos; segments[i].localScale = Vector3.one * Mathf.Lerp(2.3f, .45f, Mathf.Pow(u, .8f));
            if (i > 0) segments[i].rotation = Quaternion.LookRotation(segments[i].position - segments[i - 1].position);
        }
    }
    void Impact()
    {
        if (slam != null) PirateArenaAudio.PlayClip(slam, target, 1);
        PirateArenaFx.Dust(target, 3); PirateArenaFx.Splash(new Vector3(basePoint.x, .05f, basePoint.z), 1.4f);
        MultiverseFX.Ring(target, Radius + 1.2f, new Color(1, .4f, .5f), .5f);
        Vector3 fromSea = target - basePoint; fromSea.y = 0; fromSea.Normalize();
        foreach (var f in arena.Fighters)
        {
            if (f == null || !f.IsAlive || PartyClient) continue;   // online: the host applies the knock-back
            Vector3 d = f.transform.position - target; float dy = d.y; d.y = 0;
            if (d.magnitude > Radius || Mathf.Abs(dy) > 2.2f) continue;
            Vector3 away = (d.magnitude > .2f ? d.normalized : fromSea) * .6f + fromSea * .4f;
            f.TakePush(away.normalized * 9 + Vector3.up * 5); f.KnockDown(1.2f);
            PirateArenaFx.HitStars(f.transform.position + Vector3.up * 1.4f);
        }
        marker.SetActive(false);
    }
    void Hide() { phase = Phase.Idle; if (tentacle != null) tentacle.gameObject.SetActive(false); if (marker != null) marker.SetActive(false); }
    void OnDestroy() { foreach (var m in new[] { skin, spots, warn }) if (m != null) Destroy(m); if (tentacle != null) Destroy(tentacle.gameObject); if (marker != null) Destroy(marker); }
}

// Wins carried across rematches in this session, shown under the arena HUD.
public sealed class PirateArenaScoreboard : MonoBehaviour
{
    static readonly System.Collections.Generic.Dictionary<string, int> wins = new();
    PirateArenaDirector arena; bool counted; GUIStyle style, box;
    void Start() { arena = GetComponent<PirateArenaDirector>(); }
    void Update()
    {
        if (arena == null) return;
        if (arena.Finished && !counted) { counted = true; if (arena.Winner != null) { string n = arena.Winner.IsHuman ? "Tú" : arena.Winner.DisplayName; wins[n] = (wins.TryGetValue(n, out var w) ? w : 0) + 1; } }
        if (!arena.Finished) counted = false;
    }
    void OnGUI()
    {
        if (wins.Count == 0) return;
        if (style == null) { style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold }; style.normal.textColor = new Color(1, .92f, .7f); box = new GUIStyle(GUI.skin.box); }
        var lines = new System.Text.StringBuilder("VICTORIAS\n");
        foreach (var kv in wins) lines.Append(kv.Key).Append(": ").Append(kv.Value).Append('\n');
        float h = 26 + wins.Count * 20;
        GUI.Box(new Rect(14, Screen.height - h - 90, 190, h), GUIContent.none, box);
        GUI.Label(new Rect(24, Screen.height - h - 86, 180, h), lines.ToString(), style);
    }
}
