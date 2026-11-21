using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Mundo de Inicio: the plaza where the party meets between minigames. Everyone walks around, votes one of
/// three random minigames (walk onto a portal or click its card) and watches the score tower; first to 5 wins.
/// </summary>
public sealed class MultiverseHub : MonoBehaviour
{
    public GameObject pirate, ship, shark, barrel, chest, palm, rock, zombie, monster;
    public Material wood, darkWood, brass, rope, cloth, foam, iron, water, atlas, zombieAtlas, monsterAtlas;
    [Header("Biblioteca de arte (la asigna Multiverse/Partida/Construir Mundo de Inicio)")]
    public GameObject[] houses = new GameObject[0];
    public GameObject[] flags = new GameObject[0];
    public GameObject[] banners = new GameObject[0];
    public GameObject fountain, lighthouse, kiosk, lamp, garland, bench, tree, pine, bush, planter, cloud, signpost;

    public const float PlazaRadius = 15f, PortalRadius = 23f;
    static readonly float[] PortalAngles = { -42f, 0f, 42f };
    static readonly string[] Models = { "Characters_Anne", "Characters_Captain_Barbarossa", "Characters_Henry", "Characters_Mako", "Characters_Skeleton" };
    public const string WorldRoot = "Plaza del Multiverso";

    MultiverseParty party;
    CharacterController walker;
    HubAvatar localAvatar;
    Transform orbit;
    CinemachineThirdPersonFollow cameraFollow;
    float yaw = 0, pitch = 18, cameraDistance = 11, verticalSpeed, safeCameraDistance = 11;
    readonly RaycastHit[] cameraObstacles = new RaycastHit[32];
    readonly Dictionary<int, HubAvatar> avatars = new Dictionary<int, HubAvatar>();
    readonly Vector3[] botTargets = new Vector3[MultiverseParty.MaxSize];
    readonly float[] botRetarget = new float[MultiverseParty.MaxSize];
    PartyPhase seenPhase = (PartyPhase)(-1);
    // Plaza actions.
    readonly int[] seenEmote = Enumerable.Repeat(int.MinValue, MultiverseParty.MaxSize).ToArray();
    readonly int[] seenChat = Enumerable.Repeat(int.MinValue, MultiverseParty.MaxSize).ToArray();
    readonly int[] seenPush = Enumerable.Repeat(int.MinValue, MultiverseParty.MaxSize).ToArray();
    readonly Vector3[] botKnock = new Vector3[MultiverseParty.MaxSize];
    readonly float[] botNextAct = new float[MultiverseParty.MaxSize];
    Vector3 localKnock;
    float actionCooldown, pushCooldown, lastTick = -1;
    GameObject chatPanel;
    Text chatFeed;
    readonly List<(string text, float at)> feed = new List<(string, float)>();
    Transform uiRoot;
    int seenRound = -1;

    // World pieces that react to the party.
    readonly Portal[] portals = new Portal[3];
    readonly ScorePillar[] pillars = new ScorePillar[MultiverseParty.MaxSize];
    Transform crown, podium;
    TextMesh centerSign;
    Vector3[] podiumSpots = { PodiumSpot(0), PodiumSpot(1), PodiumSpot(2) };

    sealed class Portal
    {
        public Vector3 Pad, Dir;
        public Renderer Screen, PadGlow, Frame;
        public TextMesh Title, Tagline;
        public Renderer[] Orbs;
        public Transform Root;
        public int Game = -1;
    }

    sealed class ScorePillar
    {
        public Transform Root;
        public Renderer[] Discs;
        public Renderer Cap;
        public TextMesh Name;
    }

    // UI
    Text phaseTitle, phaseInfo, roomInfo, scoreText, hint, lobbyText, championTitle, championText;
    GameObject cardsRoot, lobbyPanel, championPanel;
    readonly GameObject[] cards = new GameObject[3];
    readonly RawImage[] cardImages = new RawImage[3];
    readonly Text[] cardTitles = new Text[3], cardBodies = new Text[3], cardVotes = new Text[3];
    readonly Button[] cardButtons = new Button[3];
    Button startButton, newMatchButton;
    readonly Button[] sizeButtons = new Button[3];
    float uiRefresh;
    Material glowBase;

    // =====================================================================
    void Start()
    {
        Application.targetFrameRate = 60;
        Time.timeScale = 1f; AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        party = MultiverseParty.EnsureForHub();
        if (transform.Find(WorldRoot) == null) BuildWorld();
        else CollectWorld(transform.Find(WorldRoot));
        BuildWalker(); BuildCamera(); BuildUI();
        PartyMusic.Play(gameObject, "plaza", .4f);
        party.Changed += OnPartyChanged;
        OnPartyChanged();
    }

    void OnDestroy() { if (party != null) party.Changed -= OnPartyChanged; }

    // =====================================================================
    // World
    // =====================================================================
    Material Tint(Color c, float smooth = .2f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = c; m.SetFloat("_Smoothness", smooth); return m;
    }

    Material Glow(Color c, float power = 1f)
    {
        var m = Tint(c, .35f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * power * .45f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; return m;
    }

    GameObject Shape(PrimitiveType type, Transform parent, string label, Vector3 at, Vector3 size, Material material, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(type); go.name = label;
        go.transform.SetParent(parent, false); go.transform.position = at; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid) Kill(go.GetComponent<Collider>());
        else if (type == PrimitiveType.Cylinder) { Kill(go.GetComponent<Collider>()); var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh; mc.convex = true; }
        return go;
    }

    static void Kill(Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

    GameObject Place(GameObject prefab, Transform parent, Vector3 at, float yawDeg, float scale = 1f)
    {
        if (prefab == null) return null;
        var go = Instantiate(prefab, parent);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yawDeg, 0));
        go.transform.localScale *= scale;
        return go;
    }

    static float Face(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

    TextMesh Sign(Transform parent, string text, Vector3 at, Quaternion rot, float size, Color color, int fontSize = 96)
    {
        var go = new GameObject("Letrero " + text, typeof(TextMesh)); go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(at, rot);
        var t = go.GetComponent<TextMesh>(); t.text = text; t.fontSize = fontSize; t.characterSize = size;
        t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = color;
        return t;
    }

    public void BuildWorld()
    {
        // Remove any previous version of the hub (the old six-gate island) saved in the scene.
        for (int i = transform.childCount - 1; i >= 0; i--) Kill(transform.GetChild(i).gameObject);
        foreach (var stray in new[] { "Portales", "Hub UI", "Explorador", "Hub Camera Target", "Hub Follow Camera" })
        {
            var g = GameObject.Find(stray); if (g != null && g.transform.parent == null && g != gameObject) Kill(g);
        }
        var root = new GameObject(WorldRoot).transform; root.SetParent(transform, false);

        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.52f, .78f, .9f);
        RenderSettings.fogStartDistance = 55; RenderSettings.fogEndDistance = 150;
        RenderSettings.ambientLight = new Color(.74f, .78f, .8f);
        var sun = new GameObject("Sol de la plaza").AddComponent<Light>(); sun.type = LightType.Directional;
        sun.transform.SetParent(root, false); sun.transform.rotation = Quaternion.Euler(48, -32, 0);
        sun.intensity = 1.3f; sun.shadows = LightShadows.Soft; sun.color = new Color(1f, .96f, .88f);

        var grass = Tint(new Color(.47f, .78f, .38f), .1f);
        var sand = Tint(new Color(.97f, .86f, .6f), .05f);
        var stone = Tint(new Color(.86f, .82f, .74f), .15f);
        var stoneDark = Tint(new Color(.6f, .56f, .52f), .15f);
        var path = Tint(new Color(.93f, .76f, .52f), .1f);

        Shape(PrimitiveType.Cube, root, "Mar", new Vector3(0, -1.6f, 0), new Vector3(260, .2f, 260), water, false);
        Shape(PrimitiveType.Cylinder, root, "Playa", new Vector3(0, -.9f, 0), new Vector3(84, .8f, 84), sand);
        Shape(PrimitiveType.Cylinder, root, "Suelo continuo · césped", new Vector3(0, -.5f, 0), new Vector3(76, .5f, 76), grass);
        Shape(PrimitiveType.Cylinder, root, "Plaza central", new Vector3(0, -.42f, 0), new Vector3(PlazaRadius * 2, .5f, PlazaRadius * 2), stone);
        Shape(PrimitiveType.Cylinder, root, "Anillo de la plaza", new Vector3(0, -.4f, 0), new Vector3(PlazaRadius * 2 - 1.2f, .5f, PlazaRadius * 2 - 1.2f), stoneDark);
        Shape(PrimitiveType.Cylinder, root, "Plaza interior", new Vector3(0, -.39f, 0), new Vector3(PlazaRadius * 2 - 2.2f, .5f, PlazaRadius * 2 - 2.2f), stone);
        // Paving rings with the six player colours.
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f + 30f;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            Shape(PrimitiveType.Cube, root, "Baldosa de color " + i, dir * 10f + Vector3.up * .12f, new Vector3(1.4f, .05f, 1.4f), Tint(MultiverseParty.Colors[i]), false)
                .transform.rotation = Quaternion.Euler(0, a + 45, 0);
        }

        BuildScoreTower(root);
        for (int i = 0; i < 3; i++) BuildPortal(root, i, path);
        BuildPodium(root);
        BuildDecor(root, path);
        Physics.SyncTransforms();
        CollectWorld(root);
    }

    void BuildScoreTower(Transform root)
    {
        var tower = new GameObject("Torre de puntos").transform; tower.SetParent(root, false);
        if (fountain != null) Place(fountain, tower, Vector3.zero, 0, 1f);
        else
        {
            Shape(PrimitiveType.Cylinder, tower, "Fuente base", new Vector3(0, .3f, 0), new Vector3(4.5f, .3f, 4.5f), stoneMat());
            Shape(PrimitiveType.Cylinder, tower, "Fuente agua", new Vector3(0, .55f, 0), new Vector3(3.8f, .05f, 3.8f), water, false);
        }
        var obelisk = Shape(PrimitiveType.Cube, tower, "Obelisco", new Vector3(0, 3.4f, 0), new Vector3(.9f, 4.6f, .9f), brass, false);
                Shape(PrimitiveType.Sphere, tower, "Orbe del multiverso", new Vector3(0, 6.3f, 0), Vector3.one * 1.3f, Glow(new Color(.5f, .85f, 1f), 2.2f), false)
            .AddComponent<HubSpin>().speed = 30;
        for (int s = 0; s < 4; s++)
        {
            var rot = Quaternion.Euler(0, s * 90f, 0);
            Sign(tower, "PRIMERO\nA " + MultiverseParty.WinScore, rot * new Vector3(0, 3.9f, -.47f), rot, .045f, new Color(.2f, .12f, .05f), 80);
        }
        for (int i = 0; i < MultiverseParty.MaxSize; i++)
        {
            float a = i * 60f - 150f;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            var p = new GameObject("Pilar de puntos " + i).transform; p.SetParent(tower, false);
            p.position = dir * 5.6f; p.rotation = Quaternion.LookRotation(-dir);
            Shape(PrimitiveType.Cube, p, "Base", p.position + Vector3.up * .25f, new Vector3(1.1f, .5f, 1.1f), darkWood);
            Shape(PrimitiveType.Cube, p, "Mástil", p.position + Vector3.up * 2.1f, new Vector3(.18f, 3.6f, .18f), iron, false);
            var discs = new Renderer[MultiverseParty.WinScore];
            for (int d = 0; d < discs.Length; d++)
                discs[d] = Shape(PrimitiveType.Cylinder, p, "Punto " + (d + 1), p.position + Vector3.up * (.78f + d * .62f), new Vector3(.8f, .13f, .8f), Tint(new Color(.55f, .58f, .66f)), false).GetComponent<Renderer>();
            var cap = Shape(PrimitiveType.Sphere, p, "Remate", p.position + Vector3.up * 4.1f, Vector3.one * .6f, Tint(Color.gray), false).GetComponent<Renderer>();
            var name = Sign(p, "", p.position + Vector3.up * 4.8f, Quaternion.identity, .05f, Color.white, 80);
            name.gameObject.AddComponent<HubBillboard>();
        }
        crown = new GameObject("Corona del líder").transform; crown.SetParent(tower, false);
        var crownMat = Glow(new Color(1f, .78f, .2f), 1.4f);
        Shape(PrimitiveType.Cylinder, crown, "Aro", Vector3.zero, new Vector3(.8f, .12f, .8f), crownMat, false);
        for (int k = 0; k < 5; k++)
        {
            float a = k * 72f * Mathf.Deg2Rad;
            Shape(PrimitiveType.Cube, crown, "Pico", new Vector3(Mathf.Sin(a) * .34f, .22f, Mathf.Cos(a) * .34f), new Vector3(.12f, .35f, .12f), crownMat, false).transform.localRotation = Quaternion.Euler(0, k * 72f, 0);
        }
        crown.gameObject.AddComponent<HubSpin>().speed = 60;
    }

    Material stoneCache;
    Material stoneMat() => stoneCache != null ? stoneCache : (stoneCache = Tint(new Color(.82f, .8f, .74f)));

    void BuildPortal(Transform root, int i, Material path)
    {
        var dir = Quaternion.Euler(0, PortalAngles[i], 0) * Vector3.forward;
        var side = Vector3.Cross(Vector3.up, dir);
        var p = new GameObject("Portal de votación " + (i + 1)).transform; p.SetParent(root, false);
        p.position = dir * PortalRadius; p.rotation = Quaternion.LookRotation(dir);
        // Path from the plaza edge to the portal deck.
        var mid = dir * ((PlazaRadius + PortalRadius - 3f) * .5f);
        var walk = Shape(PrimitiveType.Cube, p, "Camino " + (i + 1), mid + Vector3.up * -.08f, new Vector3(4.2f, .2f, PortalRadius - 3f - PlazaRadius + 1.5f), path, false);
        walk.transform.rotation = Quaternion.LookRotation(dir);
        Shape(PrimitiveType.Cylinder, p, "Plataforma del portal " + (i + 1), dir * (PortalRadius - .6f) + Vector3.up * -.2f, new Vector3(9.5f, .32f, 9.5f), wood);
        var pad = Shape(PrimitiveType.Cylinder, p, "Círculo de voto " + (i + 1), dir * (PortalRadius - 1.6f) + Vector3.up * .14f, new Vector3(4.6f, .03f, 4.6f), Glow(Color.white, 1f), false);
        var frameMat = Tint(new Color(.95f, .9f, .82f), .4f);
        Vector3 back = dir * (PortalRadius + 2.2f);
        for (int s = -1; s <= 1; s += 2)
        {
            Shape(PrimitiveType.Cube, p, "Columna", back + side * s * 3.6f + Vector3.up * 3.2f, new Vector3(.8f, 6.4f, .8f), darkWood);
            Shape(PrimitiveType.Sphere, p, "Farol de columna", back + side * s * 3.6f + Vector3.up * 6.75f, Vector3.one * .7f, Glow(new Color(1f, .85f, .5f)), false);
        }
        var lintel = Shape(PrimitiveType.Cube, p, "Dintel", back + Vector3.up * 6.5f, new Vector3(8.2f, .8f, .9f), brass, false);
        lintel.transform.rotation = p.rotation;
        var frame = Shape(PrimitiveType.Cube, p, "Marco de la pantalla", back + Vector3.up * 3.55f + dir * .08f, new Vector3(6.9f, 4.3f, .2f), frameMat, false);
        frame.transform.rotation = p.rotation;
        var screen = Shape(PrimitiveType.Quad, p, "Pantalla del minijuego", back + Vector3.up * 3.55f - dir * .05f, new Vector3(6.4f, 3.8f, 1f), Tint(Color.white), false);
        screen.transform.rotation = p.rotation;
        var title = Sign(p, "", back + Vector3.up * 7.55f - dir * .1f, p.rotation, .1f, Color.white, 96);
        var tagline = Sign(p, "", back + Vector3.up * 1.05f - dir * .2f, p.rotation, .05f, new Color(1f, .97f, .9f), 80);
        Shape(PrimitiveType.Cube, p, "Placa", back + Vector3.up * 1.05f - dir * .05f, new Vector3(6.4f, .7f, .15f), darkWood, false).transform.rotation = p.rotation;
        var orbs = new Renderer[MultiverseParty.MaxSize];
        for (int o = 0; o < orbs.Length; o++)
            orbs[o] = Shape(PrimitiveType.Sphere, p, "Voto " + (o + 1), back + Vector3.up * 8.55f + side * ((o - 2.5f) * .85f) - dir * .1f, Vector3.one * .5f, Tint(new Color(.25f, .25f, .3f)), false).GetComponent<Renderer>();
        Sign(p, (i + 1).ToString(), dir * (PortalRadius - 1.6f) + Vector3.up * .2f, Quaternion.LookRotation(Vector3.down, dir), .25f, new Color(.1f, .1f, .15f, .7f), 96);
    }

    static readonly Vector3 PodiumCenter = new Vector3(0, 0, -12.8f);
    static readonly float[] PodiumHeights = { 1.5f, 1.0f, .65f };
    static readonly float[] PodiumSide = { 0, 2.3f, -2.3f };

    void BuildPodium(Transform root)
    {
        var p = new GameObject("Podio del campeón").transform; p.SetParent(root, false);
        Vector3 c = PodiumCenter; p.position = c; p.rotation = Quaternion.LookRotation(Vector3.forward);
        Vector3 f = Vector3.forward, side = Vector3.right;
        Color[] col = { new Color(1f, .8f, .25f), new Color(.82f, .85f, .9f), new Color(.85f, .55f, .3f) };
        var faceCenter = Quaternion.LookRotation(-f);
        for (int k = 0; k < 3; k++)
        {
            float h = PodiumHeights[k];
            Shape(PrimitiveType.Cube, p, "Escalón " + (k + 1), c + side * PodiumSide[k] + Vector3.up * h * .5f, new Vector3(2.2f, h, 2.2f), Tint(col[k], .4f));
            Sign(p, (k + 1).ToString(), c + side * PodiumSide[k] + Vector3.up * h * .5f + f * 1.12f, faceCenter, .08f, new Color(.15f, .1f, .05f));
        }
        var arch = Shape(PrimitiveType.Cube, p, "Arco del podio", c - f * .9f + Vector3.up * 4.6f, new Vector3(7.5f, .5f, .5f), brass, false);
        for (int s = -1; s <= 1; s += 2) Shape(PrimitiveType.Cube, p, "Poste del podio", c - f * .9f + side * s * 3.6f + Vector3.up * 2.3f, new Vector3(.4f, 4.6f, .4f), darkWood, false);
        Sign(p, "CAMPEÓN DEL MULTIVERSO", c - f * .62f + Vector3.up * 4.6f, faceCenter, .04f, new Color(.2f, .1f, .05f), 80);
        podium = p;
    }

    static Vector3 PodiumSpot(int k) => PodiumCenter + Vector3.right * PodiumSide[k] + Vector3.up * (PodiumHeights[k] + .05f);
    static Vector3 CrowdSpot(int i) => new Vector3(-4.2f + i * 1.7f, .2f, -9.4f);

    void BuildDecor(Transform root, Material path)
    {
        var deco = new GameObject("Decorado").transform; deco.SetParent(root, false);
        // Lamps and garlands around the plaza, leaving the three portal paths and the podium open.
        var lamps = new List<Vector3>();
        for (int k = 0; k < 20; k++)
        {
            float a = k * 18f + 9f;
            bool blocked = PortalAngles.Any(pa => Mathf.Abs(Mathf.DeltaAngle(a, pa)) < 11f) || Mathf.Abs(Mathf.DeltaAngle(a, 180)) < 20f || Mathf.Abs(Mathf.DeltaAngle(a, 270)) < 11f;
            if (blocked) continue;
            var pos = Quaternion.Euler(0, a, 0) * Vector3.forward * (PlazaRadius + .6f);
            lamps.Add(pos);
            if (lamp != null) Place(lamp, deco, pos, a + 180);
            else
            {
                Shape(PrimitiveType.Cylinder, deco, "Farola", pos + Vector3.up * 1.6f, new Vector3(.2f, 1.6f, .2f), iron, false);
                Shape(PrimitiveType.Sphere, deco, "Luz", pos + Vector3.up * 3.3f, Vector3.one * .5f, Glow(MultiverseParty.Colors[k % 6]), false);
            }
        }
        for (int k = 0; k + 1 < lamps.Count; k++)
        {
            if (Vector3.Distance(lamps[k], lamps[k + 1]) > 6f) continue;
            var mid = (lamps[k] + lamps[k + 1]) * .5f;
            if (garland != null) Place(garland, deco, mid + Vector3.up * 3.6f, Face(lamps[k], lamps[k + 1]) - 90);
        }
        // Houses and stalls on the outer ring, facing the plaza.
        float[] houseAngles = { 70, 92, 114, 140, 220, 246, 268, 290 };
        for (int k = 0; k < houseAngles.Length && houses.Length > 0; k++)
        {
            var pos = Quaternion.Euler(0, houseAngles[k], 0) * Vector3.forward * 29.5f;
            Place(houses[k % houses.Length], deco, pos, Face(pos, Vector3.zero) + 180);
        }
        if (lighthouse != null) Place(lighthouse, deco, Quaternion.Euler(0, 160, 0) * Vector3.forward * 30f, 20);
        if (kiosk != null) { var kp = Quaternion.Euler(0, 125, 0) * Vector3.forward * 19.5f; Place(kiosk, deco, kp, Face(kp, Vector3.zero)); }
        if (signpost != null) Place(signpost, deco, new Vector3(3.2f, 0, -17.5f), 180);
        // Benches inside the plaza ring.
        for (int k = 0; k < 6 && bench != null; k++)
        {
            float a = k * 60f;
            if (PortalAngles.Any(pa => Mathf.Abs(Mathf.DeltaAngle(a, pa)) < 20f) || Mathf.Abs(Mathf.DeltaAngle(a, 180)) < 35f || Mathf.Abs(Mathf.DeltaAngle(a, 270)) < 35f) continue;
            var pos = Quaternion.Euler(0, a, 0) * Vector3.forward * 12.2f;
            Place(bench, deco, pos, a);
        }
        // Greenery: trees between houses, bushes and planters by the portal paths.
        var rnd = new System.Random(7);
        for (int k = 0; k < 26; k++)
        {
            float a = k * (360f / 26f) + (float)rnd.NextDouble() * 6f;
            float r = 33.5f + (float)rnd.NextDouble() * 3f;
            var pos = Quaternion.Euler(0, a, 0) * Vector3.forward * r;
            var prefab = k % 3 == 0 ? palm : k % 3 == 1 ? (tree != null ? tree : palm) : (pine != null ? pine : palm);
            if (prefab == palm && palm != null) Decor(palm, deco, pos, 4.5f + (float)rnd.NextDouble() * 1.5f, a * 3);
            else Place(prefab, deco, pos, a * 2);
        }
        foreach (float pa in PortalAngles)
            for (int s = -1; s <= 1; s += 2)
            {
                var dir = Quaternion.Euler(0, pa, 0) * Vector3.forward; var side = Vector3.Cross(Vector3.up, dir);
                var pos = dir * (PlazaRadius + 2.2f) + side * s * 3.1f;
                Place(bush != null ? bush : planter, deco, pos, pa);
                var fpos = dir * (PlazaRadius + .4f) + side * s * 2.6f;
                if (flags.Length > 0) Place(flags[(System.Array.IndexOf(PortalAngles, pa) * 2 + (s + 1) / 2) % flags.Length], deco, fpos, pa + 180);
            }
        if (planter != null) for (int s = -1; s <= 1; s += 2) Place(planter, deco, new Vector3(s * 3.6f, 0, -16.4f), 0);
        // Clouds overhead and the pirate ship sailing past.
        for (int k = 0; k < 7 && cloud != null; k++)
        {
            var c = Place(cloud, deco, Quaternion.Euler(0, k * 51f, 0) * Vector3.forward * (35 + k * 6) + Vector3.up * (22 + k % 3 * 4), k * 40, 1.4f);
            if (c != null) foreach (var col in c.GetComponentsInChildren<Collider>()) Kill(col);
        }
        Decor(ship, deco, new Vector3(-48, -1.2f, 26), 12f, 150);
        Decor(shark, deco, new Vector3(36, -1.45f, -38), 3f, 80);
        Decor(barrel, deco, new Vector3(-12.6f, 0, 4.4f), 1.1f, 20);
        Decor(chest, deco, new Vector3(-12.6f, 0, -4.4f), 1.2f, 70);
        Decor(zombie, deco, Quaternion.Euler(0, -66, 0) * Vector3.forward * 24f, 2.4f, 110, zombieAtlas);
        Decor(monster, deco, Quaternion.Euler(0, 66, 0) * Vector3.forward * 24.5f, 4.2f, 250, monsterAtlas);
        Decor(rock, deco, new Vector3(38, -.6f, 8), 3f, 40);
        Decor(rock, deco, new Vector3(-39, -.6f, -12), 2.6f, 80);
    }

    void Decor(GameObject prefab, Transform parent, Vector3 at, float height, float angle, Material material = null)
    {
        if (prefab == null) return;
        var root = new GameObject("Decorado " + prefab.name); root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(at, Quaternion.Euler(0, angle, 0));
        PirateArenaVisuals.AddModel(prefab, root.transform, height, material != null ? material : atlas, false);
        if (root.GetComponentsInChildren<Collider>().Length == 0 && height > 2f)
        {
            var bounds = PirateArenaVisuals.BoundsOf(root);
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center); collider.size = bounds.size;
        }
    }

    /// <summary>Find the reactive pieces in a world built either now or in the editor.</summary>
    void CollectWorld(Transform root)
    {
        for (int i = 0; i < 3; i++)
        {
            var t = root.Find("Portal de votación " + (i + 1));
            if (t == null) continue;
            var dir = Quaternion.Euler(0, PortalAngles[i], 0) * Vector3.forward;
            var portal = new Portal { Root = t, Dir = dir, Pad = dir * (PortalRadius - 1.6f) };
            portal.Screen = t.Find("Pantalla del minijuego").GetComponent<Renderer>();
            portal.PadGlow = t.Find("Círculo de voto " + (i + 1)).GetComponent<Renderer>();
            portal.Frame = t.Find("Marco de la pantalla").GetComponent<Renderer>();
            var signs = t.GetComponentsInChildren<TextMesh>();
            portal.Title = signs[0]; portal.Tagline = signs[1];
            portal.Orbs = Enumerable.Range(1, MultiverseParty.MaxSize).Select(o => t.Find("Voto " + o).GetComponent<Renderer>()).ToArray();
            portals[i] = portal;
        }
        var tower = root.Find("Torre de puntos");
        for (int i = 0; i < MultiverseParty.MaxSize && tower != null; i++)
        {
            var p = tower.Find("Pilar de puntos " + i); if (p == null) continue;
            pillars[i] = new ScorePillar
            {
                Root = p,
                Discs = Enumerable.Range(1, MultiverseParty.WinScore).Select(d => p.Find("Punto " + d).GetComponent<Renderer>()).ToArray(),
                Cap = p.Find("Remate").GetComponent<Renderer>(),
                Name = p.GetComponentInChildren<TextMesh>()
            };
        }
        if (tower != null) crown = tower.Find("Corona del líder");
        podium = root.Find("Podio del campeón");
        podiumSpots = new[] { PodiumSpot(0), PodiumSpot(1), PodiumSpot(2) };
    }

    // =====================================================================
    // Characters
    // =====================================================================
    static GameObject ModelFor(int character) => Resources.Load<GameObject>("PirateCharacters/" + Models[Mathf.Abs(character) % Models.Length]);

    void BuildWalker()
    {
        var me = party.Local;
        var go = new GameObject("Explorador", typeof(CharacterController));
        go.layer = 2;
        go.transform.position = MultiverseParty.SpawnPoint(party.LocalIndex);
        go.transform.rotation = Quaternion.Euler(0, 0, 0);
        walker = go.GetComponent<CharacterController>(); walker.height = 1.75f; walker.radius = .38f; walker.center = new Vector3(0, .88f, 0);
        walker.stepOffset = .45f; walker.skinWidth = .035f; walker.minMoveDistance = 0f; walker.slopeLimit = 48f;
        go.AddComponent<HubPhysicsPush>();
        localAvatar = HubAvatar.Attach(go, ModelFor(me != null ? me.Character : 0), atlas, me != null ? me.Name : "Tú",
            party.ColorOf(party.LocalIndex), true);
    }

    void SyncAvatars()
    {
        for (int i = 0; i < party.Members.Count; i++)
        {
            if (i == party.LocalIndex) continue;
            var m = party.Members[i];
            if (!avatars.TryGetValue(i, out var a) || a == null || a.Character != m.Character)
            {
                if (a != null) Destroy(a.gameObject);
                var go = new GameObject("Jugador " + m.Name); go.layer = 2;
                if (m.Pose == Vector3.zero) m.Pose = MultiverseParty.SpawnPoint(i);
                go.transform.position = m.Pose;
                a = HubAvatar.Attach(go, ModelFor(m.Character), atlas, m.Name, party.ColorOf(i), false);
                a.Character = m.Character;
                avatars[i] = a;
            }
            a.SetLabel(m.Name, party.ColorOf(i), m.Bot);
        }
        foreach (var key in avatars.Keys.ToList())
            if (key >= party.Members.Count || key == party.LocalIndex) { if (avatars[key] != null) Destroy(avatars[key].gameObject); avatars.Remove(key); }
    }

    void DriveBots(float dt)
    {
        for (int i = 0; i < party.Members.Count; i++)
        {
            var m = party.Members[i];
            if (!m.Bot || i == party.LocalIndex) continue;
            if (botKnock[i].sqrMagnitude > .01f)
            {
                Vector3 shove = m.Pose + botKnock[i] * dt;
                shove.y = GroundAt(shove);
                m.Pose = shove;
                botKnock[i] = Vector3.MoveTowards(botKnock[i], Vector3.zero, 16f * dt);
                m.Moving = false;
                continue;
            }
            Vector3 target = BotGoal(i, m);
            Vector3 flat = target - m.Pose; flat.y = 0;
            bool moving = flat.magnitude > .35f;
            if (moving)
            {
                Vector3 step = Vector3.ClampMagnitude(flat, 4.4f * dt);
                Vector3 next = m.Pose + step;
                next.y = GroundAt(next);
                m.Pose = next;
                m.Yaw = Mathf.MoveTowardsAngle(m.Yaw, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, 540 * dt);
            }
            else if (party.Phase == PartyPhase.Champion)
                m.Yaw = Mathf.MoveTowardsAngle(m.Yaw, 0f, 360 * dt);
            else if (party.Phase == PartyPhase.Voting && m.Vote >= 0 && portals[m.Vote] != null)
                m.Yaw = Mathf.MoveTowardsAngle(m.Yaw, Mathf.Atan2(portals[m.Vote].Dir.x, portals[m.Vote].Dir.z) * Mathf.Rad2Deg, 360 * dt);
            m.Moving = moving;
        }
    }

    Vector3 BotGoal(int i, PartyMember m)
    {
        float ring = (i * 137.5f) % 360f;
        var offset = Quaternion.Euler(0, ring, 0) * Vector3.forward * 1.3f;
        switch (party.Phase)
        {
            case PartyPhase.Voting:
            case PartyPhase.Reveal:
                if (m.Vote >= 0 && portals[m.Vote] != null) return portals[m.Vote].Pad + offset;
                break;
            case PartyPhase.Champion:
            {
                int rank = Ranking().IndexOf(i);
                if (rank >= 0 && rank < 3) return podiumSpots[rank];
                return CrowdSpot(i);
            }
        }
        if (Time.time > botRetarget[i] || botTargets[i] == Vector3.zero)
        {
            botRetarget[i] = Time.time + Random.Range(3f, 7f);
            var spot = MultiverseParty.SpawnPoint(i);
            botTargets[i] = spot + new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-1f, 3f));
        }
        return botTargets[i];
    }

    float GroundAt(Vector3 p)
    {
        if (Physics.Raycast(new Vector3(p.x, 12f, p.z), Vector3.down, out var hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    List<int> Ranking() => Enumerable.Range(0, party.Members.Count).OrderByDescending(i => party.Members[i].Score).ThenBy(i => i).ToList();

    // =====================================================================
    // Loop
    // =====================================================================
    void Update()
    {
        if (party == null) return;
        if (MultiverseParty.Instance != party) { party = MultiverseParty.Instance; if (party == null) return; }
        Keyboard keys = Keyboard.current; Mouse mouse = Mouse.current;
        bool typing = EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null;
        Vector3 move = Vector3.zero; bool jump = false;
        bool settingsOpen = MultiverseSettingsPanel.IsOpen, chatOpen = chatPanel != null && chatPanel.activeSelf;
        if (keys != null && keys.escapeKey.wasPressedThisFrame && !typing)
        {
            if (chatOpen) chatPanel.SetActive(false); else MultiverseSettingsPanel.Toggle(uiRoot);
            settingsOpen = MultiverseSettingsPanel.IsOpen; chatOpen = false;
        }
        if (keys != null && !typing && !settingsOpen)
        {
            float sens = MultiverseSettings.Sensitivity;
            if (mouse != null && mouse.rightButton.isPressed && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            { var d = mouse.delta.ReadValue(); yaw += d.x * .16f * sens; pitch = Mathf.Clamp(pitch - d.y * .12f * sens, 6, 62); }
            if (keys.qKey.isPressed) yaw -= 110 * Time.deltaTime;
            if (keys.eKey.isPressed) yaw += 110 * Time.deltaTime;
            Vector2 axis = new Vector2((keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1 : 0) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed || keys.upArrowKey.isPressed ? 1 : 0) - (keys.sKey.isPressed || keys.downArrowKey.isPressed ? 1 : 0));
            move = Quaternion.Euler(0, yaw, 0) * new Vector3(axis.x, 0, axis.y).normalized;
            jump = keys.spaceKey.wasPressedThisFrame;
            if (chatOpen)
            {
                for (int c = 0; c < MultiverseParty.ChatPhrases.Length; c++)
                    if (keys[Key.Digit1 + c].wasPressedThisFrame || keys[Key.Numpad1 + c].wasPressedThisFrame) SendChat(c);
                if (keys.tKey.wasPressedThisFrame) chatPanel.SetActive(false);
            }
            else
            {
                if (keys.digit1Key.wasPressedThisFrame) party.Vote(0);
                if (keys.digit2Key.wasPressedThisFrame) party.Vote(1);
                if (keys.digit3Key.wasPressedThisFrame) party.Vote(2);
                if (keys.tKey.wasPressedThisFrame) chatPanel.SetActive(true);
            }
            if (Time.time > actionCooldown)
            {
                int emote = keys.zKey.wasPressedThisFrame ? 0 : keys.xKey.wasPressedThisFrame ? 1 : keys.cKey.wasPressedThisFrame ? 2 : keys.vKey.wasPressedThisFrame ? 3 : -1;
                if (emote >= 0) { party.Emote(emote); actionCooldown = Time.time + .6f; }
            }
            if (keys.fKey.wasPressedThisFrame) TryPush();
            if (keys.enterKey.wasPressedThisFrame && party.IsHost && party.Phase == PartyPhase.Lobby) party.HostStartMatch();
        }
        if (mouse != null && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            cameraDistance = Mathf.Clamp(cameraDistance - mouse.scroll.ReadValue().y * .008f, 6, 18);
        MoveExplorer(move, Time.deltaTime, jump);
        if (move.sqrMagnitude > .05f) walker.transform.rotation = Quaternion.Slerp(walker.transform.rotation, Quaternion.LookRotation(move), Time.deltaTime * 12);
        localAvatar.Animate(move.sqrMagnitude > .05f, !walker.isGrounded);
        if (walker.transform.position.y < -1.2f) Teleport(MultiverseParty.SpawnPoint(party.LocalIndex));

        // Walking onto a portal's circle votes for it.
        if (party.Phase == PartyPhase.Voting)
            for (int i = 0; i < 3; i++)
                if (portals[i] != null && FlatDistance(walker.transform.position, portals[i].Pad) < 2.3f) party.Vote(i);

        party.SetLocalPose(walker.transform.position, walker.transform.eulerAngles.y, move.sqrMagnitude > .05f);
        if (party.IsAuthority) { DriveBots(Time.deltaTime); BotAntics(); }
        SyncAvatars();
        ProcessActions();
        CountdownTicks();
        foreach (var kv in avatars)
        {
            if (kv.Value == null || kv.Key >= party.Members.Count) continue;
            var m = party.Members[kv.Key];
            kv.Value.Drive(m.Pose, m.Yaw, m.Moving, party.Phase == PartyPhase.Champion && !m.Moving);
        }
        if (party.Phase != seenPhase || party.Round != seenRound) OnPartyChanged();
        AnimateWorld();
        if (Time.unscaledTime > uiRefresh) { uiRefresh = Time.unscaledTime + .1f; RefreshUI(); }
    }

    static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }

    // =====================================================================
    // Emotes, quick chat and pushes
    // =====================================================================
    Vector3 PositionOf(int member) => member == party.LocalIndex ? walker.transform.position : party.Members[member].Pose;
    HubAvatar AvatarOf(int member) => member == party.LocalIndex ? localAvatar : avatars.TryGetValue(member, out var a) ? a : null;

    void SendChat(int id)
    {
        if (Time.time < actionCooldown) return;
        party.Chat(id); actionCooldown = Time.time + .8f;
        chatPanel.SetActive(false);
    }

    void TryPush()
    {
        if (Time.time < pushCooldown) return;
        Vector3 me = walker.transform.position, fwd = walker.transform.forward;
        int best = -1; float bestD = 2f;
        for (int i = 0; i < party.Members.Count; i++)
        {
            if (i == party.LocalIndex) continue;
            Vector3 d = PositionOf(i) - me; d.y = 0;
            float m = d.magnitude;
            if (m < bestD && (m < .8f || Vector3.Dot(fwd, d / m) > .2f)) { best = i; bestD = m; }
        }
        pushCooldown = Time.time + .9f;
        Sfx("whoosh_" + Random.Range(0, 3), me, .5f);
        if (best < 0) return;
        Vector3 dir = PositionOf(best) - me; dir.y = 0;
        party.Push(best, dir.sqrMagnitude > .01f ? dir.normalized : fwd);
    }

    static void Sfx(string name, Vector3 at, float volume)
    {
        var clip = Resources.Load<AudioClip>("PirateAudio/v2/" + name);
        if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
    }

    /// <summary>Plays whatever happened since last frame: emotes, chat bubbles, pushes (the authority already counted them).</summary>
    void ProcessActions()
    {
        for (int i = 0; i < party.Members.Count && i < MultiverseParty.MaxSize; i++)
        {
            var m = party.Members[i];
            var avatar = AvatarOf(i);
            bool first = seenEmote[i] == int.MinValue;
            if (m.EmoteSeq != seenEmote[i])
            {
                if (!first && avatar != null) avatar.PlayEmote(m.EmoteId);
                seenEmote[i] = m.EmoteSeq;
            }
            if (m.ChatSeq != seenChat[i])
            {
                if (!first)
                {
                    string text = MultiverseParty.ChatPhrases[Mathf.Clamp(m.ChatId, 0, MultiverseParty.ChatPhrases.Length - 1)];
                    MultiverseFX.FloatText(PositionOf(i) + Vector3.up * 2.75f, text, Color.Lerp(party.ColorOf(i), Color.white, .35f), .9f, 3f);
                    feed.Add(("<color=#" + ColorUtility.ToHtmlStringRGB(party.ColorOf(i)) + ">" + m.Name + "</color>: " + text, Time.time));
                    if (feed.Count > 5) feed.RemoveAt(0);
                    Sfx("grab_" + Random.Range(0, 3), PositionOf(i), .25f);
                }
                seenChat[i] = m.ChatSeq;
            }
            if (m.PushSeq != seenPush[i])
            {
                if (!first)
                {
                    Vector3 dir = m.PushDir; dir.y = 0; if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
                    if (i == party.LocalIndex) { localKnock = dir.normalized * 9f; verticalSpeed = 4.5f; }
                    else if (m.Bot && party.IsAuthority)
                    {
                        botKnock[i] = dir.normalized * 9f;
                        if (Random.value < .35f) { botNextAct[i] = Time.time + 1.2f; pendingOops[i] = true; }
                    }
                    if (avatar != null) avatar.PlayHit();
                    Sfx("punch_" + Random.Range(0, 3), PositionOf(i), .6f);
                    MultiverseFX.Burst(PositionOf(i) + Vector3.up * 1.2f, party.ColorOf(i), 10, 4);
                }
                seenPush[i] = m.PushSeq;
            }
        }
        if (chatFeed != null)
        {
            feed.RemoveAll(f => Time.time - f.at > 9f);
            chatFeed.text = string.Join("\n", feed.Select(f => f.text));
        }
    }

    readonly bool[] pendingOops = new bool[MultiverseParty.MaxSize];

    /// <summary>Bots wave, chat and push each other (and you) every now and then.</summary>
    void BotAntics()
    {
        if (party.Phase == PartyPhase.Lobby) return;
        for (int i = 0; i < party.Members.Count; i++)
        {
            var m = party.Members[i];
            if (!m.Bot || i == party.LocalIndex) continue;
            if (botNextAct[i] == 0f) { botNextAct[i] = Time.time + Random.Range(4f, 14f); continue; }
            if (Time.time < botNextAct[i]) continue;
            botNextAct[i] = Time.time + Random.Range(7f, 18f);
            if (pendingOops[i]) { pendingOops[i] = false; party.ApplyChat(i, 7); continue; }
            float roll = Random.value;
            int near = -1; float nearD = 1.8f;
            for (int j = 0; j < party.Members.Count; j++)
            {
                if (j == i) continue;
                float d = FlatDistance(PositionOf(j), m.Pose);
                if (d < nearD) { near = j; nearD = d; }
            }
            if (near >= 0 && roll < .35f) { party.ApplyPush(i, near, PositionOf(near) - m.Pose); m.Yaw = Mathf.Atan2((PositionOf(near) - m.Pose).x, (PositionOf(near) - m.Pose).z) * Mathf.Rad2Deg; }
            else if (roll < .7f) party.ApplyEmote(i, party.Phase == PartyPhase.Champion ? (Random.value < .6f ? 1 : 0) : Random.Range(0, 4));
            else
            {
                int[] pool = party.Phase == PartyPhase.Voting ? new[] { 1, 2, 3, 4 } : party.Phase == PartyPhase.Champion ? new[] { 6, 5 } : new[] { 0, 3, 4, 5 };
                party.ApplyChat(i, pool[Random.Range(0, pool.Length)]);
            }
        }
    }

    /// <summary>Ticks during the last seconds of the vote and a fanfare when the minigame is chosen.</summary>
    void CountdownTicks()
    {
        if (party.Phase != PartyPhase.Voting) { lastTick = -1; return; }
        int sec = Mathf.CeilToInt(party.Countdown);
        if (sec <= 3 && sec > 0 && sec != (int)lastTick) { lastTick = sec; Sfx("count", walker.transform.position, .5f); }
    }

    void Teleport(Vector3 p)
    {
        verticalSpeed = 0; walker.enabled = false; walker.transform.position = p; walker.enabled = true;
    }

    public void MoveExplorer(Vector3 direction, float deltaTime, bool jump)
    {
        if (walker == null || !walker.enabled) return;
        float remaining = Mathf.Min(deltaTime, .2f);
        if (walker.isGrounded && jump) verticalSpeed = 7f;
        while (remaining > 0f)
        {
            float step = Mathf.Min(remaining, 1f / 60f); remaining -= step;
            if (walker.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed = Mathf.Max(-25f, verticalSpeed - 20f * step);
            var flags = walker.Move((Vector3.ClampMagnitude(direction, 1f) * 5.6f + localKnock + Vector3.up * verticalSpeed) * step);
            localKnock = Vector3.MoveTowards(localKnock, Vector3.zero, 16f * step);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
        }
    }

    void LateUpdate()
    {
        if (orbit == null || walker == null) return;
        orbit.SetPositionAndRotation(walker.transform.position + Vector3.up * 1.4f, Quaternion.Euler(pitch, yaw, 0));
        Vector3 back = Quaternion.Euler(pitch, yaw, 0) * Vector3.back;
        float allowed = cameraDistance;
        int count = Physics.SphereCastNonAlloc(orbit.position, .22f, back, cameraObstacles, cameraDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!cameraObstacles[i].transform.IsChildOf(walker.transform))
                allowed = Mathf.Min(allowed, Mathf.Max(1.2f, cameraObstacles[i].distance - .15f));
        safeCameraDistance = allowed < safeCameraDistance ? allowed : Mathf.MoveTowards(safeCameraDistance, allowed, Time.deltaTime * 8f);
        cameraFollow.CameraDistance = safeCameraDistance;
    }

    void BuildCamera()
    {
        Camera camera = Camera.main;
        if (camera == null) { var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cameraGo.tag = "MainCamera"; camera = cameraGo.GetComponent<Camera>(); }
        camera.fieldOfView = 55; camera.nearClipPlane = .15f; camera.farClipPlane = 220;
        camera.backgroundColor = new Color(.52f, .8f, .93f); camera.clearFlags = CameraClearFlags.SolidColor;
        if (camera.GetComponent<CinemachineBrain>() == null) camera.gameObject.AddComponent<CinemachineBrain>();
        orbit = new GameObject("Hub Camera Target").transform;
        var rig = new GameObject("Hub Follow Camera"); var vcam = rig.AddComponent<CinemachineCamera>();
        vcam.Follow = orbit; vcam.LookAt = orbit;
        cameraFollow = rig.AddComponent<CinemachineThirdPersonFollow>(); cameraFollow.CameraDistance = cameraDistance;
        cameraFollow.Damping = new Vector3(.12f, .12f, .12f);
        yaw = 0;
        orbit.position = walker.transform.position + Vector3.up * 1.4f; orbit.rotation = Quaternion.Euler(pitch, yaw, 0);
    }

    // =====================================================================
    // Reacting to the party
    // =====================================================================
    void OnPartyChanged()
    {
        if (party == null) return;
        bool phaseChanged = party.Phase != seenPhase;
        seenPhase = party.Phase; seenRound = party.Round;
        for (int i = 0; i < 3; i++)
        {
            var p = portals[i]; if (p == null) continue;
            int game = party.Choices[i];
            if (p.Game != game)
            {
                p.Game = game;
                bool has = game >= 0 && game < PartyGames.Count;
                var info = has ? PartyGames.All[game] : default;
                var tex = has ? PartyGames.Image(game) : null;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", tex); mat.color = tex != null ? Color.white : (has ? info.Color : Color.gray);
                mat.EnableKeyword("_EMISSION"); mat.SetTexture("_EmissionMap", tex); mat.SetColor("_EmissionColor", Color.white * .5f);
                mat.SetFloat("_Smoothness", .7f);
                p.Screen.sharedMaterial = mat;
                p.Title.text = has ? info.Title : "???"; p.Title.color = has ? Color.Lerp(info.Color, Color.white, .25f) : Color.white;
                p.Tagline.text = has ? info.Tagline.ToUpperInvariant() : "";
                p.PadGlow.sharedMaterial = Glow(has ? info.Color : Color.gray, 1.3f);
                p.Frame.sharedMaterial = Glow(has ? Color.Lerp(info.Color, Color.white, .5f) : Color.gray, .5f);
            }
        }
        if (phaseChanged && party.Phase == PartyPhase.Champion)
        {
            int rank = Ranking().IndexOf(party.LocalIndex);
            if (rank >= 0 && rank < 3) Teleport(podiumSpots[rank]);
            else Teleport(CrowdSpot(party.LocalIndex));
            walker.transform.rotation = Quaternion.Euler(0, rank >= 0 && rank < 3 ? 0 : 180, 0);
            yaw = 180; pitch = 14; cameraDistance = 8f;
            MultiverseFX.Burst(podiumSpots[0] + Vector3.up * 2, new Color(1f, .8f, .2f), 60);
        }
        if (phaseChanged && party.Phase == PartyPhase.Reveal && party.Selected >= 0)
        {
            int slot = System.Array.IndexOf(party.Choices, party.Selected);
            if (slot >= 0 && portals[slot] != null) MultiverseFX.Burst(portals[slot].Root.position + Vector3.up * 4 + portals[slot].Dir * 2, PartyGames.All[party.Selected].Color, 70);
        }
        if (phaseChanged && party.Phase == PartyPhase.Voting && party.LastWinner >= 0 && party.Round > 0)
            MultiverseParty.Toast(party.ResultText);
        RefreshUI();
    }

    void AnimateWorld()
    {
        float t = Time.time;
        int[] votes = new int[3];
        for (int i = 0; i < 3; i++)
        {
            var p = portals[i]; if (p == null) continue;
            int o = 0;
            for (int m = 0; m < party.Members.Count && o < p.Orbs.Length; m++)
                if (party.Members[m].Vote == i && (party.Phase == PartyPhase.Voting || party.Phase == PartyPhase.Reveal))
                {
                    SetColor(p.Orbs[o], party.ColorOf(m), true); p.Orbs[o].transform.localScale = Vector3.one * (.55f + Mathf.Sin(t * 4 + m) * .05f); o++; votes[i]++;
                }
            for (; o < p.Orbs.Length; o++) { SetColor(p.Orbs[o], new Color(.22f, .22f, .27f), false); p.Orbs[o].transform.localScale = Vector3.one * .38f; }
            bool chosen = party.Phase == PartyPhase.Reveal && party.Choices[i] == party.Selected;
            bool dim = party.Phase == PartyPhase.Reveal && !chosen;
            float pulse = chosen ? 1f + Mathf.Abs(Mathf.Sin(t * 6f)) * .25f : 1f + Mathf.Sin(t * 2f + i) * .03f;
            p.PadGlow.transform.localScale = new Vector3(4.6f * pulse, .03f, 4.6f * pulse);
            p.Screen.transform.localScale = dim ? new Vector3(6.4f, 3.8f, 1) * .82f : new Vector3(6.4f, 3.8f, 1f) * (chosen ? 1.06f : 1f);
            bool mine = party.Local != null && party.Local.Vote == i && party.Phase == PartyPhase.Voting;
            p.Title.characterSize = mine ? .115f + Mathf.Sin(t * 5) * .005f : .1f;
        }
        int leader = party.Leader();
        for (int i = 0; i < pillars.Length; i++)
        {
            var pl = pillars[i]; if (pl == null) continue;
            bool used = i < party.Members.Count;
            pl.Root.gameObject.SetActive(used);
            if (!used) continue;
            var m = party.Members[i]; var c = party.ColorOf(i);
            for (int d = 0; d < pl.Discs.Length; d++) SetColor(pl.Discs[d], d < m.Score ? c : new Color(.55f, .58f, .66f), d < m.Score);
            SetColor(pl.Cap, c, true);
            string label = m.Name + (i == party.LocalIndex ? "\n(tú)" : m.Bot ? "\nbot" : "");
            if (pl.Name.text != label) { pl.Name.text = label; pl.Name.color = Color.Lerp(c, Color.white, .35f); }
        }
        if (crown != null)
        {
            bool show = leader >= 0 && party.Members[leader].Score > 0 && pillars[leader] != null;
            crown.gameObject.SetActive(show);
            if (show) crown.position = Vector3.Lerp(crown.position, pillars[leader].Root.position + Vector3.up * (4.7f + Mathf.Sin(t * 2) * .12f), Time.deltaTime * 6);
        }
    }

    readonly Dictionary<Renderer, Color> colorCache = new Dictionary<Renderer, Color>();
    void SetColor(Renderer r, Color c, bool glow)
    {
        if (r == null) return;
        if (colorCache.TryGetValue(r, out var old) && old == c) return;
        colorCache[r] = c;
        var m = r.material; m.color = c;
        if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * .6f); }
        else { m.SetColor("_EmissionColor", Color.black); }
    }

    // =====================================================================
    // UI
    // =====================================================================
    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }

    Text Label(string value, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int point, Color color, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var t = Rect("Texto", parent, anchor, pos, size).gameObject.AddComponent<Text>();
        t.font = MultiverseUI.DisplayFont; t.fontSize = point; t.color = color; t.text = value; t.alignment = align; t.raycastTarget = false; t.supportRichText = true;
        return t;
    }

    Button MakeButton(string label, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action, int point = 20)
    {
        var go = Rect(label, parent, anchor, pos, size).gameObject;
        go.AddComponent<Image>().color = new Color(1f, .74f, .22f);
        var button = go.AddComponent<Button>(); button.onClick.AddListener(action);
        Label(label, go.transform, new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(12, 4), point, new Color(.16f, .1f, .04f));
        return button;
    }

    GameObject Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, float alpha = .92f)
    {
        var r = Rect(name, parent, anchor, pos, size);
        r.gameObject.AddComponent<Image>().color = new Color(.1f, .16f, .3f, alpha);
        return r.gameObject;
    }

    void BuildUI()
    {
        var old = GameObject.Find("Hub UI"); if (old != null) Destroy(old);
        var root = new GameObject("Hub UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var cream = new Color(1f, .97f, .9f); var gold = new Color(1f, .8f, .3f);

        var header = Panel("Sala", root.transform, new Vector2(0, 1), new Vector2(190, -44), new Vector2(350, 72));
        Label("MULTIVERSE PARTY 3D", header.transform, new Vector2(.5f, .5f), new Vector2(0, 13), new Vector2(330, 34), 22, gold);
        roomInfo = Label("", header.transform, new Vector2(.5f, .5f), new Vector2(0, -16), new Vector2(330, 26), 14, cream);

        var phase = Panel("Fase", root.transform, new Vector2(.5f, 1), new Vector2(0, -46), new Vector2(560, 78));
        phaseTitle = Label("", phase.transform, new Vector2(.5f, .5f), new Vector2(0, 14), new Vector2(540, 40), 28, gold);
        phaseInfo = Label("", phase.transform, new Vector2(.5f, .5f), new Vector2(0, -19), new Vector2(540, 26), 15, cream);

        var board = Panel("Marcador", root.transform, new Vector2(0, 1), new Vector2(150, -208), new Vector2(270, 226));
        Label("MARCADOR · A " + MultiverseParty.WinScore, board.transform, new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(250, 30), 19, gold);
        scoreText = Label("", board.transform, new Vector2(.5f, .5f), new Vector2(0, -16), new Vector2(246, 180), 15, cream, TextAnchor.UpperLeft);
        scoreText.lineSpacing = .95f;

        uiRoot = root.transform;
        MakeButton("SALIR AL MENÚ", root.transform, new Vector2(1, 1), new Vector2(-92, -32), new Vector2(160, 40), () => party.LeaveToMenu(), 15);
        MakeButton("AJUSTES", root.transform, new Vector2(1, 1), new Vector2(-92, -80), new Vector2(160, 40), () => MultiverseSettingsPanel.Toggle(uiRoot), 15);
        chatFeed = Label("", root.transform, new Vector2(0, 0), new Vector2(170, 345), new Vector2(320, 96), 14, cream, TextAnchor.LowerLeft);
        chatFeed.horizontalOverflow = HorizontalWrapMode.Wrap;
        chatPanel = Panel("Chat rápido", root.transform, new Vector2(1, .5f), new Vector2(-150, 10), new Vector2(270, 430), .95f);
        Label("CHAT RÁPIDO", chatPanel.transform, new Vector2(.5f, 1), new Vector2(0, -24), new Vector2(250, 30), 18, gold);
        for (int c = 0; c < MultiverseParty.ChatPhrases.Length; c++)
        {
            int id = c;
            MakeButton((c + 1) + "  " + MultiverseParty.ChatPhrases[c], chatPanel.transform, new Vector2(.5f, 1), new Vector2(0, -68 - c * 45), new Vector2(230, 38), () => SendChat(id), 15);
        }
        chatPanel.SetActive(false);

        // Three minigame cards.
        cardsRoot = new GameObject("Cartas", typeof(RectTransform)); var cr = cardsRoot.GetComponent<RectTransform>();
        cr.SetParent(root.transform, false); cr.anchorMin = cr.anchorMax = new Vector2(.5f, 0); cr.anchoredPosition = new Vector2(0, 146); cr.sizeDelta = new Vector2(1100, 280);
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            var card = Panel("Carta " + (i + 1), cr, new Vector2(.5f, .5f), new Vector2((i - 1) * 345, 0), new Vector2(330, 272), .95f);
            cards[i] = card;
            var imgRect = Rect("Imagen", card.transform, new Vector2(.5f, 1), new Vector2(0, -66), new Vector2(306, 116));
            cardImages[i] = imgRect.gameObject.AddComponent<RawImage>();
            Label((i + 1).ToString(), imgRect, new Vector2(0, 1), new Vector2(20, -18), new Vector2(34, 34), 24, gold);
            cardTitles[i] = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, -6), new Vector2(310, 30), 22, gold);
            cardBodies[i] = Label("", card.transform, new Vector2(.5f, .5f), new Vector2(0, -56), new Vector2(306, 70), 12, cream);
            cardVotes[i] = Label("", card.transform, new Vector2(.5f, 0), new Vector2(-70, 24), new Vector2(150, 28), 18, cream);
            cardButtons[i] = MakeButton("VOTAR", card.transform, new Vector2(.5f, 0), new Vector2(80, 24), new Vector2(140, 36), () => party.Vote(slot), 17);
        }

        // Lobby (online, before the first vote).
        lobbyPanel = Panel("Sala de espera", root.transform, new Vector2(.5f, 0), new Vector2(0, 130), new Vector2(620, 220), .95f);
        lobbyText = Label("", lobbyPanel.transform, new Vector2(.5f, .5f), new Vector2(0, 46), new Vector2(590, 110), 18, cream);
        for (int i = 0; i < 3; i++)
        {
            int s = 4 + i;
            sizeButtons[i] = MakeButton(s + " JUGADORES", lobbyPanel.transform, new Vector2(.5f, .5f), new Vector2(-190 + i * 190, -30), new Vector2(170, 40), () => party.SetTargetSize(s), 15);
        }
        startButton = MakeButton("EMPEZAR PARTIDA", lobbyPanel.transform, new Vector2(.5f, .5f), new Vector2(0, -80), new Vector2(300, 46), () => party.HostStartMatch(), 21);

        // Champion.
        championPanel = Panel("Campeón", root.transform, new Vector2(.5f, .5f), new Vector2(0, -30), new Vector2(820, 470), .95f);
        championTitle = Label("", championPanel.transform, new Vector2(.5f, .5f), new Vector2(0, 196), new Vector2(780, 56), 36, gold);
        championText = Label("", championPanel.transform, new Vector2(.5f, .5f), new Vector2(0, 2), new Vector2(780, 320), 15, cream, TextAnchor.UpperLeft);
        championText.supportRichText = true; championText.lineSpacing = 1.05f;
        newMatchButton = MakeButton("NUEVA PARTIDA", championPanel.transform, new Vector2(.5f, .5f), new Vector2(-130, -195), new Vector2(230, 46), () => party.NewMatch(), 20);
        MakeButton("MENÚ PRINCIPAL", championPanel.transform, new Vector2(.5f, .5f), new Vector2(130, -195), new Vector2(230, 46), () => party.LeaveToMenu(), 20);

        hint = Label("", root.transform, new Vector2(.5f, 0), new Vector2(0, 10), new Vector2(1000, 22), 13, cream);
        MultiverseUI.Skin(root, MultiverseUI.Mode.Hub);
        RefreshUI();
    }

    void RefreshUI()
    {
        if (phaseTitle == null || party == null) return;
        var me = party.Local;
        roomInfo.text = party.Online ? "SALA <b>" + party.RoomCode + "</b> · " + (party.IsHost ? "anfitrión" : "en línea") + " · " + party.Members.Count + " jugadores"
                                     : "Sin conexión · " + party.Members.Count + " jugadores";
        string secs = Mathf.CeilToInt(party.Countdown) + " s";
        switch (party.Phase)
        {
            case PartyPhase.Lobby:
                phaseTitle.text = "SALA DE ESPERA"; phaseInfo.text = "Comparte el código " + party.RoomCode + " con tus amigos"; break;
            case PartyPhase.Voting:
                phaseTitle.text = "¡VOTAD EL MINIJUEGO! · " + secs;
                phaseInfo.text = me != null && me.Vote >= 0 ? "Has votado " + PartyGames.All[party.Choices[me.Vote]].Title + " · puedes cambiar tu voto" : "Pisa un portal o pulsa 1, 2 o 3";
                break;
            case PartyPhase.Reveal:
                phaseTitle.text = party.Selected >= 0 ? "¡A JUGAR: " + PartyGames.All[party.Selected].Title + "!" : "¡A JUGAR!";
                phaseInfo.text = "Empieza en " + secs; break;
            case PartyPhase.Champion:
                int w = party.Leader();
                phaseTitle.text = "¡TENEMOS CAMPEÓN!"; phaseInfo.text = w >= 0 ? party.Members[w].Name + " llegó a " + MultiverseParty.WinScore + " puntos" : ""; break;
            default:
                phaseTitle.text = "CARGANDO…"; phaseInfo.text = ""; break;
        }

        var sb = new System.Text.StringBuilder();
        foreach (int i in Ranking())
        {
            var m = party.Members[i];
            string col = ColorUtility.ToHtmlStringRGB(party.ColorOf(i));
            string voted = party.Phase == PartyPhase.Voting ? (m.Vote >= 0 ? " ✓" : " …") : "";
            sb.Append("<color=#").Append(col).Append(">■ ").Append(m.Name).Append("</color>")
              .Append(i == party.LocalIndex ? " <size=11>(tú)</size>" : m.Bot ? " <size=11>bot</size>" : "").Append(voted).Append('\n')
              .Append("<size=13>   <color=#FFD54A>").Append(new string('●', m.Score)).Append("</color><color=#5A6278>").Append(new string('●', MultiverseParty.WinScore - m.Score)).Append("</color></size>\n");
        }
        if (party.Phase == PartyPhase.Lobby)
            for (int i = party.Members.Count; i < party.TargetSize; i++) sb.Append("<color=#8890A8>□ plaza libre (bot)</color>\n\n");
        scoreText.text = sb.ToString();

        bool voting = party.Phase == PartyPhase.Voting || party.Phase == PartyPhase.Reveal;
        cardsRoot.SetActive(voting);
        if (voting)
            for (int i = 0; i < 3; i++)
            {
                int game = party.Choices[i];
                bool has = game >= 0 && game < PartyGames.Count;
                var info = has ? PartyGames.All[game] : default;
                cardImages[i].texture = has ? PartyGames.Image(game) : null;
                cardImages[i].color = cardImages[i].texture != null ? Color.white : (has ? info.Color : Color.gray);
                cardTitles[i].text = has ? info.Title : "";
                cardTitles[i].color = has ? Color.Lerp(info.Color, Color.white, .3f) : Color.white;
                cardBodies[i].text = has ? "<b>" + info.Tagline + "</b>\n" + info.Description : "";
                int count = party.Members.Count(m => m.Vote == i);
                cardVotes[i].text = "Votos: " + count;
                bool mine = me != null && me.Vote == i;
                bool chosen = party.Phase == PartyPhase.Reveal && game == party.Selected;
                cardButtons[i].gameObject.SetActive(party.Phase == PartyPhase.Voting);
                cardButtons[i].GetComponentInChildren<Text>().text = mine ? "TU VOTO ✓" : "VOTAR";
                var img = cards[i].GetComponent<Image>();
                img.color = chosen ? new Color(.45f, .32f, .06f, .98f) : mine ? new Color(.2f, .3f, .55f, .98f) : new Color(.1f, .16f, .3f, party.Phase == PartyPhase.Reveal ? .6f : .95f);
                cards[i].transform.localScale = Vector3.one * (chosen ? 1.08f : party.Phase == PartyPhase.Reveal ? .9f : mine ? 1.03f : 1f);
            }

        bool lobby = party.Phase == PartyPhase.Lobby;
        lobbyPanel.SetActive(lobby);
        if (lobby)
        {
            int humans = party.Members.Count(m => !m.Bot);
            lobbyText.text = "<size=26><color=#FFD54A>CÓDIGO: " + party.RoomCode + "</color></size>\n" +
                humans + " jugador" + (humans == 1 ? "" : "es") + " conectado" + (humans == 1 ? "" : "s") + " · partida de " + party.TargetSize +
                (humans < party.TargetSize ? " (" + (party.TargetSize - humans) + " bots completarán)" : "") +
                (party.IsHost ? "\nCuando estéis listos, pulsa EMPEZAR." : "\nEsperando a que el anfitrión empiece…");
            for (int i = 0; i < 3; i++)
            {
                sizeButtons[i].gameObject.SetActive(party.IsHost);
                sizeButtons[i].transform.localScale = Vector3.one * (party.TargetSize == 4 + i ? 1.08f : .95f);
                sizeButtons[i].GetComponent<Image>().color = party.TargetSize == 4 + i ? new Color(1f, .74f, .22f) : new Color(.55f, .62f, .8f);
            }
            startButton.gameObject.SetActive(party.IsHost);
        }

        bool champ = party.Phase == PartyPhase.Champion;
        championPanel.SetActive(champ);
        if (champ)
        {
            var rank = Ranking(); int w = rank[0];
            championTitle.text = w == party.LocalIndex ? "¡ERES EL CAMPEÓN!" : "¡" + party.Members[w].Name.ToUpperInvariant() + " ES EL CAMPEÓN!";
            var cb = new System.Text.StringBuilder();
            string[] medal = { "1º", "2º", "3º", "4º", "5º", "6º" };
            string[] shortNames = { "Arena", "Zombi", "Carreras", "Código", "Escape", "Rodillos", "Cavar" };
            var titles = party.Titles();
            for (int k = 0; k < rank.Count; k++)
            {
                int i = rank[k];
                var m = party.Members[i];
                var wins = Enumerable.Range(0, PartyGames.Count).Where(g => m.Wins[g] > 0).Select(g => shortNames[g] + " ×" + m.Wins[g]).ToList();
                cb.Append("<size=19>").Append(medal[k]).Append("  <color=#").Append(ColorUtility.ToHtmlStringRGB(party.ColorOf(i))).Append(">").Append(m.Name).Append("</color>")
                  .Append(i == party.LocalIndex ? " (tú)" : "").Append("  ·  ").Append(m.Score).Append(m.Score == 1 ? " punto" : " puntos")
                  .Append("   <color=#FFD54A>«").Append(titles[i]).Append("»</color></size>\n")
                  .Append("<size=13><color=#C9D3F0>      ").Append(wins.Count > 0 ? string.Join(" · ", wins) : "sin victorias")
                  .Append("   |   empujó ").Append(m.Pushes).Append(" · recibió ").Append(m.Pushed).Append(" · gestos ").Append(m.Emotes)
                  .Append(" · votos ✓").Append(m.VoteHits).Append("</color></size>\n");
            }
            championText.text = cb.ToString();
            newMatchButton.gameObject.SetActive(party.IsAuthority);
        }

        bool nearPortal = portals.Any(p => p != null && FlatDistance(walker.transform.position, p.Pad) < 4f);
        hint.text = "WASD mover · Espacio saltar · Clic der. o Q/E cámara · Z/X/C/V gestos · T chat · F empujar · Esc ajustes · " +
            (party.Phase == PartyPhase.Voting ? (nearPortal ? "¡Pisa el círculo para votar!" : "Camina a un portal o pulsa 1-3 para votar") :
             party.Phase == PartyPhase.Lobby && party.IsHost ? "Enter: empezar partida" : "Primero a " + MultiverseParty.WinScore + " puntos gana");
    }
}

/// <summary>A player shown in the plaza: model, colour ring, floating name and walk/idle animation.</summary>
public sealed class HubAvatar : MonoBehaviour
{
    public int Character;
    Animator animator;
    string clip;
    TextMesh label;
    Renderer ring;
    bool local;
    float celebrateUntil, emoteUntil;
    string emoteClip;
    Vector3 velocity;
    static readonly string[] EmoteClips = { "Base Layer.Wave", "Base Layer.Jump", "Base Layer.Duck", "Base Layer.Death" };
    static readonly float[] EmoteSeconds = { 2.2f, 1.2f, 1.6f, 2.8f };

    public void PlayEmote(int id)
    {
        id = Mathf.Clamp(id, 0, EmoteClips.Length - 1);
        emoteClip = EmoteClips[id]; emoteUntil = Time.time + EmoteSeconds[id]; clip = null;
    }

    public void PlayHit() { emoteClip = "Base Layer.HitReact"; emoteUntil = Time.time + .6f; clip = null; }

    public static HubAvatar Attach(GameObject go, GameObject model, Material atlas, string name, Color color, bool isLocal)
    {
        var a = go.AddComponent<HubAvatar>(); a.local = isLocal;
        var visual = model != null ? PirateArenaVisuals.AddModel(model, go.transform, 1.7f, atlas, false) : null;
        if (visual != null)
        {
            foreach (var c in visual.GetComponentsInChildren<Collider>()) c.enabled = false;
            a.animator = visual.GetComponentInChildren<Animator>();
            if (a.animator != null) { a.animator.applyRootMotion = false; a.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        }
        var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ringGo.name = "Aro del jugador";
        Destroy(ringGo.GetComponent<Collider>());
        ringGo.transform.SetParent(go.transform, false); ringGo.transform.localPosition = new Vector3(0, .04f, 0); ringGo.transform.localScale = new Vector3(1.15f, .02f, 1.15f);
        a.ring = ringGo.GetComponent<Renderer>();
        var tagGo = new GameObject("Nombre", typeof(TextMesh)); tagGo.transform.SetParent(go.transform, false);
        tagGo.transform.localPosition = new Vector3(0, 2.35f, 0);
        a.label = tagGo.GetComponent<TextMesh>(); a.label.fontSize = 72; a.label.characterSize = .045f; a.label.anchor = TextAnchor.MiddleCenter; a.label.alignment = TextAlignment.Center;
        tagGo.AddComponent<HubBillboard>();
        a.SetLabel(name, color, false);
        return a;
    }

    public void SetLabel(string name, Color color, bool bot)
    {
        string text = local ? name + " (tú)" : bot ? name + " · bot" : name;
        if (label.text != text) label.text = text;
        label.color = Color.Lerp(color, Color.white, .3f);
        var m = ring.material; m.color = color; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * .6f);
    }

    public void Animate(bool moving, bool air)
    {
        if (animator == null) return;
        if (moving && emoteClip != "Base Layer.HitReact") emoteUntil = 0f;
        string next = Time.time < emoteUntil ? emoteClip : Time.time < celebrateUntil ? "Base Layer.Wave" : air ? "Base Layer.Jump_Idle" : moving ? "Base Layer.Run" : "Base Layer.Idle";
        if (next != clip) { animator.CrossFadeInFixedTime(next, .14f); clip = next; }
    }

    public void Drive(Vector3 pos, float yaw, bool moving, bool celebrate)
    {
        if (celebrate && Time.time > celebrateUntil + 2.5f) celebrateUntil = Time.time + 2.2f;
        if ((transform.position - pos).sqrMagnitude > 64f) transform.position = pos;
        else transform.position = Vector3.SmoothDamp(transform.position, pos, ref velocity, .09f);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yaw, 0), Time.deltaTime * 12f);
        Animate(moving, false);
    }
}

public sealed class HubBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        var cam = Camera.main; if (cam == null) return;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}

public sealed class HubSpin : MonoBehaviour
{
    public float speed = 45;
    void Update() => transform.Rotate(0, speed * Time.deltaTime, 0, Space.World);
}
