using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Builds the single race circuit into Assets/001_Scenes/CarrerasLocas.unity (~1.8 km):
// beach start, pirate village with a narrow barrel chicane, wooden lagoon bridge, mountain switchbacks,
// tunnel, cliff drop, viaduct over a canyon, jungle, river jump, boulder straight and the NW hairpin.
// Shortcuts: plank over the ravine, dirt path (pays off with a turbo) and an unmarked treasure cave.
// Re-runnable: everything except the reused karts, camera, lights and start gantry is rebuilt.
public static class GranPremioBuilder
{
    const string ScenePath = "Assets/001_Scenes/CarrerasLocas.unity", OldScene = "Assets/001_Scenes/GranPremioMultiverso.unity";
    const string Folder = "Assets/003_Scripts/CrazyRacers/Generated/GranPremio/", Mats = "Assets/003_Scripts/CrazyRacers/Generated/";
    const float Step = 2.5f, Scale = 1.15f;
    enum Zone { Beach, Village, Bridge, Mountain, Tunnel, Cliff, Viaduct, Jungle, West }

    // x, y, z, half width (unscaled layout; x/z are scaled about the start line).
    static readonly Vector4[] Raw =
    {
        new Vector4(0,0,102,7.5f), new Vector4(60,0,104,7.5f), new Vector4(120,0,100,7),
        new Vector4(165,0,82,6.5f), new Vector4(192,0,60,6), new Vector4(220,0,48,6), new Vector4(248,0,50,6), new Vector4(275,0,35,6),
        new Vector4(292,1.2f,0,4.5f), new Vector4(296,1.4f,-40,4.5f), new Vector4(288,1.2f,-78,4.5f),
        new Vector4(262,3,-108,6), new Vector4(225,6,-118,6), new Vector4(205,8,-92,6), new Vector4(178,11,-100,6), new Vector4(175,14,-140,6), new Vector4(200,17,-165,6), new Vector4(180,19,-195,5.5f),
        new Vector4(140,19,-205,5), new Vector4(100,18.5f,-208,5), new Vector4(70,17,-205,5.5f),
        new Vector4(40,12,-203,6), new Vector4(10,10,-200,6),
        new Vector4(-30,10,-196,5), new Vector4(-70,9,-185,4.5f), new Vector4(-110,7,-170,5),
        new Vector4(-150,5,-150,6), new Vector4(-172,3,-118,6), new Vector4(-168,2,-96,6), new Vector4(-191,2.2f,-70,6),
        new Vector4(-195,.6f,-12,6.5f),
        new Vector4(-198,0,30,7), new Vector4(-195,0,80,7),
        new Vector4(-175,0,130,6.5f), new Vector4(-130,0,158,6.5f), new Vector4(-85,0,150,6.5f),
        new Vector4(-50,0,118,7), new Vector4(-20,0,104,7.5f),
    };
    static readonly Zone[] Zones =
    {
        Zone.Beach, Zone.Beach, Zone.Beach, Zone.Village, Zone.Village, Zone.Village, Zone.Village, Zone.Village,
        Zone.Bridge, Zone.Bridge, Zone.Mountain, Zone.Mountain, Zone.Mountain, Zone.Mountain, Zone.Mountain, Zone.Mountain, Zone.Mountain, Zone.Tunnel,
        Zone.Tunnel, Zone.Tunnel, Zone.Cliff, Zone.Cliff, Zone.Cliff, Zone.Viaduct, Zone.Viaduct, Zone.Jungle,
        Zone.Jungle, Zone.Jungle, Zone.Jungle, Zone.Jungle, Zone.West, Zone.West, Zone.West, Zone.West, Zone.West, Zone.West, Zone.Beach, Zone.Beach,
    };
    static Vector3 S(float x, float y, float z) => new Vector3(x * Scale, y, 102 + (z - 102) * Scale);
    static Vector2 S2(float x, float z) { var v = S(x, 0, z); return new Vector2(v.x, v.z); }
    static readonly Vector2[] RiverRaw = { new Vector2(-330,-40), new Vector2(-193,-26), new Vector2(-120,-70), new Vector2(-95,-140), new Vector2(-88,-178), new Vector2(-80,-330) };
    static readonly Vector2[] RavineRaw = { new Vector2(60,-120), new Vector2(45,-180), new Vector2(43,-203), new Vector2(30,-260), new Vector2(25,-330) };
    static Vector2[] river, ravine;

    static List<Vector3> path; static List<float> half, dist; static List<Zone> zone;
    static int cliffLip, cliffLand, riverLip, riverLand;
    struct Cut
    {
        public string name; public int a, b; public float half; public List<Vector3> pts;
        public Vector3 start => pts[0]; public Vector3 end => pts[pts.Count - 1];
        public float Length { get { float l = 0; for (int i = 1; i < pts.Count; i++) l += Vector3.Distance(pts[i - 1], pts[i]); return l; } }
        public Vector3 At(float f, out Vector3 dir)
        {
            float target = f * Length, acc = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]); dir = pts[i] - pts[i - 1]; dir.y = 0; dir.Normalize();
                if (acc + seg >= target || i == pts.Count - 1) return Vector3.Lerp(pts[i - 1], pts[i], Mathf.Clamp01((target - acc) / Mathf.Max(.01f, seg)));
                acc += seg;
            }
            dir = Vector3.forward; return pts[0];
        }
        public float Distance(float x, float z, out float y)
        {
            float best = float.MaxValue; y = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                float d = SegDist(new Vector2(x, z), new Vector2(pts[i - 1].x, pts[i - 1].z), new Vector2(pts[i].x, pts[i].z), out float t);
                if (d < best) { best = d; y = Mathf.Lerp(pts[i - 1].y, pts[i].y, t); }
            }
            return best;
        }
    }
    static Cut plank, dirtCut, cave;
    static Transform root;
    static Material asphalt, terrainMat, stripe, dirt, wood, stone, island, textMat, lamp, rock; static Font textFont;

    [MenuItem("Multiverse/Carreras Locas/Reconstruir circuito")]
    static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (Application.isPlaying) throw new Exception("Stop Play first");
        System.IO.Directory.CreateDirectory(Folder);
        var active = EditorSceneManager.GetActiveScene();
        if (active.path != ScenePath) { if (active.isDirty) throw new Exception("Save the active scene first"); EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); }
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var f in System.IO.Directory.GetFiles(Folder, "*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        LoadMaterials();
        river = RiverRaw.Select(p => S2(p.x, p.y)).ToArray(); ravine = RavineRaw.Select(p => S2(p.x, p.y)).ToArray();
        BuildPath();
        var karts = StripOldTrack(scene, out var boxTemplate, out var padTemplate);
        root = new GameObject("Gran Premio Multiverso").transform;
        BuildRoad(); BuildTerrain(); BuildRails(); BuildShortcuts(); BuildTunnels();
        var gates = BuildGates();
        PlaceKarts(karts);
        PlaceItems(boxTemplate, padTemplate);
        PlaceObstacles();
        Decorate();
        Object.DestroyImmediate(boxTemplate); Object.DestroyImmediate(padTemplate);
        var d = Object.FindAnyObjectByType<CrazyKartDirector>();
        var so = new SerializedObject(d); var wp = so.FindProperty("waypoints"); wp.ClearArray();
        for (int i = 0; i < gates.Count; i++) { wp.InsertArrayElementAtIndex(i); wp.GetArrayElementAtIndex(i).vector3Value = gates[i] - Vector3.up; }
        so.FindProperty("roadHalfWidth").floatValue = 6.5f; so.FindProperty("laps").intValue = 3;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        // Only one circuit: retire the separate Gran Premio scene and the old Costa del Caos meshes.
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != OldScene).ToArray(); EditorBuildSettings.scenes = scenes;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OldScene) != null) AssetDatabase.DeleteAsset(OldScene);
        foreach (var f in System.IO.Directory.GetFiles(Mats, "CostaXL_*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        float lap = 0; for (int i = 0; i < path.Count; i++) lap += Vector3.Distance(path[i], path[(i + 1) % path.Count]);
        string cuts = string.Join("; ", new[] { plank, dirtCut, cave }.Select(c => $"{c.name} {c.Length:F0} m vs {dist[c.b] - dist[c.a]:F0} m"));
        return $"Saved {ScenePath}. Lap {lap:F0} m, {gates.Count} gates. Cliff gap {dist[cliffLand] - dist[cliffLip]:F1} m ({path[cliffLip].y:F1}->{path[cliffLand].y:F1}). River gap {dist[riverLand] - dist[riverLip]:F1} m ({path[riverLip].y:F1}->{path[riverLand].y:F1}). {cuts}. Obstacles {Object.FindObjectsByType<CrazyKartObstacle>(FindObjectsSortMode.None).Length}.";
    }

    // ---------- materials
    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Mats + name + ".mat");
    static Material NewMat(string name, Color c, Texture tex = null, float smooth = .2f, string shader = "Universal Render Pipeline/Lit")
    {
        string p = Folder + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null || m.shader.name != shader) { if (m != null) AssetDatabase.DeleteAsset(p); m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, p); }
        m.color = c; if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (tex != null) { m.mainTexture = tex; m.SetTexture("_BaseMap", tex); }
        EditorUtility.SetDirty(m); return m;
    }
    static void LoadMaterials()
    {
        asphalt = Mat("Asfalto"); island = Mat("Atlas isla");
        string texPath = Folder + "Franjas.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(texPath) == null)
        {
            var t = new Texture2D(2, 2); t.SetPixels(new[] { new Color(.92f, .16f, .3f), new Color(.92f, .16f, .3f), Color.white, Color.white }); t.Apply();
            System.IO.File.WriteAllBytes(texPath, t.EncodeToPNG()); AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath); imp.filterMode = FilterMode.Point; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = false; imp.SaveAndReimport();
        }
        stripe = NewMat("Franjas rojas", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        dirt = NewMat("Tierra", new Color(.62f, .44f, .27f), null, .05f);
        wood = NewMat("Tablones", new Color(.55f, .36f, .2f), null, .1f);
        stone = NewMat("Piedra", new Color(.62f, .6f, .58f), null, .1f);
        rock = NewMat("Roca cueva", new Color(.36f, .32f, .3f), null, .05f);
        terrainMat = NewMat("Terreno pintado", Color.white, null, 0, "RaceFX/Terrain");
        lamp = NewMat("Farol", new Color(1, .7f, .3f), null, 0, "RaceFX/Fresnel"); lamp.SetFloat("_Alpha", .95f); lamp.SetFloat("_Rim", 1.5f); lamp.SetColor("_Color", new Color(1, .7f, .3f));
        var q = Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "?");
        if (q != null) { textFont = q.font; textMat = q.GetComponent<Renderer>().sharedMaterial; }
    }

    // ---------- path
    static Vector4 CR(Vector4 p0, Vector4 p1, Vector4 p2, Vector4 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return .5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }
    static int Nearest(float x, float z) { var p = S(x, 0, z); int best = 0; float bd = float.MaxValue; for (int i = 0; i < path.Count; i++) { var d = path[i] - p; d.y = 0; if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = i; } } return best; }
    static void BuildPath()
    {
        var c = Raw.Select(r => { var p = S(r.x, r.y, r.z); return new Vector4(p.x, p.y, p.z, r.w); }).ToArray();
        int n = c.Length; var dense = new List<Vector4>(); var denseZone = new List<Zone>();
        for (int i = 0; i < n; i++) for (int k = 0; k < 200; k++) { dense.Add(CR(c[(i - 1 + n) % n], c[i], c[(i + 1) % n], c[(i + 2) % n], k / 200f)); denseZone.Add(Zones[i]); }
        path = new List<Vector3>(); half = new List<float>(); zone = new List<Zone>(); dist = new List<float>();
        float acc = 0, next = 0;
        for (int i = 0; i < dense.Count; i++)
        {
            if (i > 0) acc += Vector3.Distance((Vector3)dense[i], (Vector3)dense[i - 1]);
            if (acc >= next) { var p = (Vector3)dense[i]; if (p.y < 0) p.y = 0; path.Add(p); half.Add(dense[i].w); zone.Add(denseZone[i]); dist.Add(acc); next += Step; }
        }
        // Cliff drop: flat lip high on the cliff, landing 6 m lower across the ravine.
        cliffLip = Nearest(50, -203); cliffLand = Nearest(36, -202);
        for (int k = 0; k <= 8; k++) { var p = path[cliffLip - 8 + k]; p.y = Mathf.Lerp(path[cliffLip - 8].y, 16.5f, k / 8f); path[cliffLip - 8 + k] = p; }
        for (int k = 0; k <= 10; k++) { var p = path[cliffLand + k]; p.y = Mathf.Lerp(10.5f, p.y, k / 10f); path[cliffLand + k] = p; }
        // River jump: a kicked ramp heading north, landing low across the water.
        riverLip = Nearest(-193, -40); riverLand = Nearest(-194.5f, -26);
        float baseY = path[riverLip - 4].y;
        for (int k = 0; k <= 4; k++) { float t = k / 4f; var p = path[riverLip - 4 + k]; p.y = baseY + 1f * t * t; path[riverLip - 4 + k] = p; }
        for (int k = -6; k <= 14; k++) { int i = riverLand + k; if (InGap(i)) continue; half[i] = Mathf.Max(half[i], Mathf.Lerp(9, 7, Mathf.Abs(k) / 14f)); }
        for (int k = 0; k < 6; k++) { var p = path[riverLand + k]; p.y = Mathf.Lerp(.6f, p.y, k / 6f); path[riverLand + k] = p; }
        plank = MakeCut("Tablón del barranco", Nearest(205, -92), Nearest(200, -165), 1.6f, true);
        dirtCut = MakeCut("Camino de tierra", Nearest(-200, 30), Nearest(-85, 150), 3f, false);
        cave = MakeCut("Cueva del tesoro", Nearest(165, 82), Nearest(275, 35), 2.6f, true, S(195, 0, 80), S(222, 0, 76), S(250, 0, 70));
    }
    static Cut MakeCut(string name, int a, int b, float h, bool edge, params Vector3[] mids)
    {
        var c = new Cut { name = name, a = a, b = b, half = h, pts = new List<Vector3>() };
        Vector3 first = mids.Length > 0 ? mids[0] : path[b], last = mids.Length > 0 ? mids[mids.Length - 1] : path[a];
        c.pts.Add(edge ? Edge(a, first) : path[a]);
        foreach (var m in mids) c.pts.Add(new Vector3(m.x, 0, m.z));
        c.pts.Add(edge ? Edge(b, last) : path[b]);
        return c;
    }
    static Vector3 Edge(int i, Vector3 towards)
    {
        Vector3 side = Side(i); Vector3 d = towards - path[i]; d.y = 0;
        return path[i] + side * Mathf.Sign(Vector3.Dot(side, d)) * (half[i] - .6f) - Vector3.up * .01f;
    }
    static bool InGap(int i) => (i > cliffLip && i < cliffLand) || (i > riverLip && i < riverLand);
    static Vector3 Fwd(IList<Vector3> c, int i, bool loop)
    {
        int n = c.Count; Vector3 f = c[loop ? (i + 1) % n : Mathf.Min(i + 1, n - 1)] - c[loop ? (i - 1 + n) % n : Mathf.Max(0, i - 1)]; f.y = 0; return f.normalized;
    }
    static Vector3 Side(int i) => Vector3.Cross(Vector3.up, Fwd(path, i, true));

    // ---------- meshes
    static Mesh Volume(string name, IList<Vector3> centers, IList<float> halfW, bool loop, float thickness, float uvScale = 1)
    {
        int n = centers.Count, segments = loop ? n : n - 1, top = n * 2;
        var v = new Vector3[top * 2]; var uv = new Vector2[v.Length]; var tris = new List<int>(); float run = 0;
        for (int i = 0; i < n; i++)
        {
            var side = Vector3.Cross(Vector3.up, Fwd(centers, i, loop));
            if (i > 0) run += Vector3.Distance(centers[i], centers[i - 1]);
            v[i * 2] = centers[i] - side * halfW[i] + Vector3.up * .035f; v[i * 2 + 1] = centers[i] + side * halfW[i] + Vector3.up * .035f;
            v[top + i * 2] = v[i * 2] - Vector3.up * thickness; v[top + i * 2 + 1] = v[i * 2 + 1] - Vector3.up * thickness;
            uv[i * 2] = new Vector2(0, run * uvScale); uv[i * 2 + 1] = new Vector2(1, run * uvScale); uv[top + i * 2] = uv[i * 2]; uv[top + i * 2 + 1] = uv[i * 2 + 1];
        }
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = ((i + 1) % n) * 2, A = a + top, B = b + top;
            tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1, A, A + 1, B, A + 1, B + 1, B, a, A, B, a, B, b, a + 1, b + 1, B + 1, a + 1, B + 1, A + 1 });
        }
        if (!loop) { int e = top - 2; tris.AddRange(new[] { 0, 1, top + 1, 0, top + 1, top, e, e + top, e + top + 1, e, e + top + 1, e + 1 }); }
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.vertices = v; mesh.triangles = tris.ToArray(); mesh.uv = uv; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Folder + name + ".asset"); return mesh;
    }
    static int meshCount;
    static GameObject MeshObject(string name, Mesh mesh, Material mat, bool collider, Transform parent = null)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent ?? root, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        go.isStatic = true; return go;
    }
    static void Surface(GameObject go, IList<Vector3> c, IList<float> h, bool loop)
    { var s = go.AddComponent<CrazyKartRoadSurface>(); s.centers = c.ToArray(); s.halfWidths = h.ToArray(); s.loop = loop; }
    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Quaternion rot, Material mat, bool collider)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name = name; o.transform.SetParent(parent ?? root, false);
        o.transform.SetPositionAndRotation(pos, rot); o.transform.localScale = size; o.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(o.GetComponent<Collider>()); o.isStatic = true; return o;
    }

    static void BuildRoad()
    {
        // Road pieces split at the two jump gaps and wherever the surface material changes.
        var order = new List<int>(); int start = cliffLand;
        for (int k = 0; k < path.Count; k++) order.Add((start + k) % path.Count);
        var piece = new List<int>(); int pieceNo = 0;
        void Flush()
        {
            if (piece.Count >= 2)
            {
                var c = piece.Select(i => path[i]).ToList(); var h = piece.Select(i => half[i]).ToList();
                bool isWood = zone[piece[piece.Count / 2]] == Zone.Bridge;
                var go = MeshObject(isWood ? "Puente de madera" : "Carretera sólida", Volume("GP_Carretera_" + (pieceNo++), c, h, false, .7f, .1f), isWood ? wood : asphalt, true);
                Surface(go, c, h, false);
                foreach (int s in new[] { -1, 1 })
                {
                    if (isWood) continue;
                    var cc = new List<Vector3>(); for (int k = 0; k < c.Count; k++) { var side = Vector3.Cross(Vector3.up, Fwd(c, k, false)); cc.Add(c[k] + side * s * (h[k] - .45f) + Vector3.up * .03f); }
                    MeshObject("Bordillo", Volume("GP_Bordillo_" + (pieceNo) + "_" + s, cc, Enumerable.Repeat(.45f, cc.Count).ToList(), false, .06f, .25f), stripe, false, go.transform);
                }
            }
            piece.Clear();
        }
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k];
            if (InGap(i)) { Flush(); continue; }
            bool wood = zone[i] == Zone.Bridge;
            if (piece.Count > 0 && (zone[piece[piece.Count - 1]] == Zone.Bridge) != wood) { int last = piece[piece.Count - 1]; Flush(); piece.Add(last); }
            piece.Add(i);
            if (i == cliffLip || i == riverLip) Flush();
        }
        Flush();
        for (int i = 0; i < path.Count; i += 6)
        {
            if (InGap(i) || path[i].y < 2.5f) continue;
            float ground = Height(path[i].x, path[i].z); float topY = path[i].y - .7f;
            if (topY - ground < 1.2f) continue;
            Box("Pilar", root, new Vector3(path[i].x, (topY + ground) * .5f, path[i].z), new Vector3(zone[i] == Zone.Bridge ? .8f : 1.8f, topY - ground, zone[i] == Zone.Bridge ? .8f : 1.8f), Quaternion.LookRotation(Fwd(path, i, true)), zone[i] == Zone.Bridge ? wood : stone, true);
        }
    }

    // ---------- terrain
    static float SegDist(Vector2 p, Vector2 a, Vector2 b, out float t) { var ab = b - a; t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(.01f, ab.sqrMagnitude)); return Vector2.Distance(p, a + ab * t); }
    static float LineDistance(Vector2[] line, float x, float z) { float best = float.MaxValue; for (int i = 0; i < line.Length - 1; i++) best = Mathf.Min(best, SegDist(new Vector2(x, z), line[i], line[i + 1], out _)); return best; }
    static float Blob(float x, float z, Vector2 c, float r, float h) { float d = Vector2.Distance(new Vector2(x, z), c) / r; return h * Mathf.Exp(-d * d); }
    static bool Elevated(int i) => !(InGap(i) || zone[i] == Zone.Viaduct || zone[i] == Zone.Bridge);
    static float Base(float x, float z)
    {
        float h = 0;
        h = Mathf.Max(h, Blob(x, z, S2(200, -160), 105, 26));       // mountain massif
        h = Mathf.Max(h, Blob(x, z, S2(118, -218), 48, 36));        // tunnel peak
        h = Mathf.Max(h, Blob(x, z, S2(222, 80), 36, 17));          // treasure-cave hill (the cave runs under it)
        h = Mathf.Max(h, Blob(x, z, S2(-150, 200), 50, 14));        // NW hills
        h = Mathf.Max(h, Blob(x, z, S2(-60, 20), 40, 4));           // infield knoll
        h += Mathf.PerlinNoise(x * .02f + 7, z * .02f + 3) * 1.6f - .4f;
        return h;
    }
    static float Height(float x, float z)
    {
        float h = Base(x, z);
        for (int i = 0; i < path.Count; i += 2)
        {
            if (!Elevated(i) || path[i].y < 1.2f) continue;
            float d = new Vector2(path[i].x - x, path[i].z - z).magnitude - half[i] - 1;
            h = Mathf.Max(h, path[i].y - 1.4f - Mathf.Max(0, d) * .45f);
        }
        // Water: lagoon under the bridge, river (jump + viaduct), ravine (cliff), plank ravine.
        h = Mathf.Lerp(h, -5, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(52, 30, Vector2.Distance(new Vector2(x, z), S2(300, -40)))));
        h = Mathf.Lerp(h, -6, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(19, 10, LineDistance(river, x, z))));
        h = Mathf.Lerp(h, -6, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(16, 8, LineDistance(ravine, x, z))));
        float pd = SegDist(new Vector2(x, z), new Vector2(plank.start.x, plank.start.z), new Vector2(plank.end.x, plank.end.z), out float pt);
        if (pt > .12f && pt < .88f) h = Mathf.Lerp(h, -4, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(16, 6, pd)));
        // Keep the asphalt (and ground-level shortcuts) clear; terrain may rise steeply beyond.
        for (int i = 0; i < path.Count; i += 2)
        {
            if (InGap(i)) continue;
            float d = new Vector2(path[i].x - x, path[i].z - z).magnitude - half[i];
            if (Covered(i)) { if (d < 9) h = Mathf.Min(h, path[i].y - .3f + Mathf.Max(0, d - .9f) * 5f); }   // mountain swallows the tunnel roof
            else if (d < 9) h = Mathf.Min(h, path[i].y - .3f + Mathf.Max(0, d - 3) * 1.6f);
        }
        foreach (var c in new[] { dirtCut, cave })
        {
            float d = c.Distance(x, z, out float y) - c.half;
            bool isCave = c.name.StartsWith("Cueva");
            if (d < 9) h = Mathf.Min(h, y - .3f + Mathf.Max(0, d - (isCave ? .7f : 2)) * (isCave ? 4.2f : 1.8f));
        }
        float edge = Mathf.Min(Mathf.Min(x + 330, 450 - x), Mathf.Min(z + 340, 270 - z));
        h = Mathf.Lerp(-4, h, Mathf.Clamp01(edge / 45));
        return h;
    }
    static Color Paint(float x, float z, float h, float up)
    {
        Color sand = new Color(.9f, .78f, .52f), wet = new Color(.7f, .6f, .42f), grass = new Color(.33f, .6f, .24f), jungle = new Color(.18f, .42f, .17f), rockC = new Color(.48f, .44f, .4f), peak = new Color(.82f, .82f, .85f);
        float n = Mathf.PerlinNoise(x * .05f, z * .05f);
        Color c = Color.Lerp(grass, grass * .85f, n);
        if (x < S(-100, 0, 0).x && z < S(0, 0, -40).z) c = Color.Lerp(jungle, jungle * 1.15f, n);
        float edge = Mathf.Min(Mathf.Min(x + 330, 450 - x), Mathf.Min(z + 340, 270 - z));
        float water = Mathf.Min(LineDistance(river, x, z) - 10, Mathf.Min(LineDistance(ravine, x, z) - 8, Vector2.Distance(new Vector2(x, z), S2(300, -40)) - 34));
        if (edge < 85 || water < 10) c = Color.Lerp(sand, c, Mathf.Clamp01(Mathf.Min((edge - 55) / 30, (water - 4) / 6)));
        if (h < -.6f) c = wet;
        if (up < .78f || h > 15) c = Color.Lerp(c, rockC, Mathf.Clamp01(Mathf.Max((.85f - up) * 6, (h - 13) / 4)));
        if (h > 28) c = Color.Lerp(c, peak, Mathf.Clamp01((h - 28) / 4));
        return c;
    }
    static void BuildTerrain()
    {
        const float x0 = -330, z0 = -340, size = 4; const int nx = 196, nz = 153;
        var v = new Vector3[nx * nz];
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++) { float x = x0 + i * size, z = z0 + j * size; v[j * nx + i] = new Vector3(x, Height(x, z), z); }
        var tris = new int[(nx - 1) * (nz - 1) * 6]; int t = 0;
        for (int j = 0; j < nz - 1; j++) for (int i = 0; i < nx - 1; i++) { int a = j * nx + i; tris[t++] = a; tris[t++] = a + nx; tris[t++] = a + 1; tris[t++] = a + 1; tris[t++] = a + nx; tris[t++] = a + nx + 1; }
        var mesh = new Mesh { name = "GP_Terreno", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.vertices = v; mesh.triangles = tris; mesh.RecalculateNormals();
        var normals = mesh.normals; var colors = new Color[v.Length];
        for (int k = 0; k < v.Length; k++) colors[k] = Paint(v[k].x, v[k].z, v[k].y, normals[k].y);
        mesh.colors = colors; mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Folder + "GP_Terreno.asset");
        MeshObject("Terreno", mesh, terrainMat, true);
    }

    // ---------- rails: striped on asphalt, low wooden on the bridge, none on the viaduct
    static bool RailOpen(int i, int s)
    {
        if (InGap(i) || zone[i] == Zone.Viaduct) return true;
        foreach (var c in new[] { plank, dirtCut, cave })
            foreach (var (j, other) in new[] { (c.a, c.pts[1]), (c.b, c.pts[c.pts.Count - 2]) })
            {
                if (Mathf.Abs(i - j) > 7) continue;
                Vector3 towards = other - path[j]; towards.y = 0;
                if (Vector3.Dot(Side(i) * s, towards.normalized) > .15f) return true;
            }
        return false;
    }
    static void BuildRails()
    {
        int part = 0;
        foreach (int s in new[] { -1, 1 })
        {
            var run = new List<Vector3>(); Zone kind = Zone.Beach;
            void Flush()
            {
                if (run.Count >= 3)
                {
                    bool bridge = kind == Zone.Bridge, tunnel = kind == Zone.Tunnel;
                    var m = Volume("GP_Barrera_" + (part++), run, Enumerable.Repeat(bridge ? .15f : tunnel ? .6f : .3f, run.Count).ToList(), false, bridge ? .8f : tunnel ? 7.2f : 1.25f, .2f);
                    var go = MeshObject(bridge ? "Baranda de madera" : tunnel ? "Pared del túnel" : "Barrera", m, bridge ? wood : tunnel ? rock : stripe, true);
                }
                run.Clear();
            }
            for (int k = 0; k <= path.Count; k++)
            {
                int i = (cliffLand + k) % path.Count;
                if (k == path.Count || RailOpen(i, s)) { Flush(); continue; }
                Zone z = zone[i] == Zone.Bridge ? Zone.Bridge : Covered(i) ? Zone.Tunnel : Zone.Beach;
                if (run.Count > 0 && z != kind) { var last = run[run.Count - 1]; Flush(); run.Add(last); }
                kind = z;
                float lift = z == Zone.Bridge ? .65f : z == Zone.Tunnel ? 7f : 1.1f;
                run.Add(path[i] + Side(i) * s * (half[i] + (z == Zone.Tunnel ? .6f : .35f)) + Vector3.up * lift);
            }
            Flush();
        }
    }

    // ---------- shortcuts
    static List<Vector3> Line(Vector3 a, Vector3 b, float step) { var l = new List<Vector3>(); int n = Mathf.CeilToInt(Vector3.Distance(a, b) / step); for (int i = 0; i <= n; i++) l.Add(Vector3.Lerp(a, b, i / (float)n)); return l; }
    static void BuildShortcuts()
    {
        var pl = Line(plank.start, plank.end, 2.5f); var ph = Enumerable.Repeat(plank.half, pl.Count).ToList();
        var go = MeshObject("Atajo - tablón del barranco", Volume("GP_Tablon", pl, ph, false, .35f, .5f), wood, true);
        Surface(go, pl, ph, false);
        var sc = go.AddComponent<CrazyKartShortcut>(); sc.start = plank.start; sc.end = plank.end; sc.halfWidth = plank.half;
        for (int i = 4; i < pl.Count - 4; i += 6)
        {
            float ground = Height(pl[i].x, pl[i].z);
            Box("Poste del tablón", go.transform, new Vector3(pl[i].x, (pl[i].y - .35f + ground) * .5f, pl[i].z), new Vector3(.5f, Mathf.Max(.2f, pl[i].y - .35f - ground), .5f), Quaternion.identity, wood, false);
        }
        foreach (var c in new[] { dirtCut, cave })
        {
            var l = new List<Vector3>(); for (int q = 1; q < c.pts.Count; q++) { var seg = Line(c.pts[q - 1] + Vector3.up * .015f, c.pts[q] + Vector3.up * .015f, 2.5f); if (q > 1) seg.RemoveAt(0); l.AddRange(seg); }
            var h = Enumerable.Repeat(c.half, l.Count).ToList();
            var o = MeshObject("Atajo - " + c.name, Volume("GP_" + c.name.Replace(' ', '_'), l, h, false, .3f, .3f), c.name.StartsWith("Cueva") ? rock : dirt, true);
            Surface(o, l, h, false);
            if (c.name.StartsWith("Cueva"))   // smooth rock floor: no slowdown
                for (int q = 1; q < c.pts.Count; q++) { var s2 = o.AddComponent<CrazyKartShortcut>(); s2.start = c.pts[q - 1]; s2.end = c.pts[q]; s2.halfWidth = c.half; }
        }
    }
    static void BuildTunnels()
    {
        var parent = new GameObject("Túneles").transform; parent.SetParent(root, false);
        for (int i = 0; i < path.Count; i += 2)
        {
            if (!Covered(i)) continue;
            var f = Fwd(path, i, true);
            Box("Techo del túnel", parent, path[i] + Vector3.up * 7.4f, new Vector3(half[i] * 2 + 4, .8f, Step * 2 + .2f), Quaternion.LookRotation(f), rock, false);
            if (i % 8 == 0) foreach (int s in new[] { -1, 1 })
                {
                    var l = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(l.GetComponent<Collider>()); l.name = "Farol";
                    l.transform.SetParent(parent, false); l.transform.position = path[i] + Side(i) * s * (half[i] + .1f) + Vector3.up * 4.2f; l.transform.localScale = Vector3.one * .45f;
                    l.GetComponent<Renderer>().sharedMaterial = lamp;
                }
        }
        // Treasure cave: a roofed passage through the hill, lit by lanterns.
        float len = cave.Length, stepF = 3f / len; int n = 0;
        for (float t = stepF; t < 1 - stepF; t += stepF, n++)
        {
            var p = cave.At(t, out var d); var rot = Quaternion.LookRotation(d); var side = Vector3.Cross(Vector3.up, d);
            if (!FarFromRoad(p, cave.half + 4) || Base(p.x, p.z) < 6.5f) continue;
            Box("Techo de la cueva", parent, p + Vector3.up * 5.2f, new Vector3(cave.half * 2 + 4, .9f, 3.3f), rot, rock, false);
            foreach (int s in new[] { -1, 1 }) if (FarFromRoad(p + side * s * (cave.half + .5f), 2.5f)) Box("Pared de la cueva", parent, p + side * s * (cave.half + .5f) + Vector3.up * 2.4f, new Vector3(.8f, 5f, 3.3f), rot, rock, true);
            if (n % 4 == 0)
            {
                var l = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(l.GetComponent<Collider>()); l.name = "Farol";
                l.transform.SetParent(parent, false); l.transform.position = p + side * cave.half + Vector3.up * 3.5f; l.transform.localScale = Vector3.one * .4f;
                l.GetComponent<Renderer>().sharedMaterial = lamp;
            }
        }
    }

    // A tunnel section is roofed only where the mountain really rises above it on both sides.
    static bool Covered(int i) => zone[i] == Zone.Tunnel && Base(path[i].x + Side(i).x * (half[i] + 5), path[i].z + Side(i).z * (half[i] + 5)) > path[i].y + 6
                                                         && Base(path[i].x - Side(i).x * (half[i] + 5), path[i].z - Side(i).z * (half[i] + 5)) > path[i].y + 6;
    static bool FarFromRoad(Vector3 p, float margin)
    {
        for (int i = 0; i < path.Count; i++) { var d = path[i] - p; d.y = 0; if (d.magnitude < half[i] + margin) return false; }
        return true;
    }
    // ---------- checkpoints (main + alternates on shortcuts with the same indices)
    static List<Vector3> BuildGates()
    {
        var gates = new List<Vector3>(); var samples = new List<int>();
        var parent = new GameObject("Controles").transform; parent.SetParent(root, false);
        bool Near(int s, int lip, int land) => (s > lip - 3 && s < land + 3);
        for (int i = 0; i < path.Count; i += 10)
        {
            int s = i;
            if (Near(s, cliffLip, cliffLand)) s = cliffLand + 4;
            if (Near(s, riverLip, riverLand)) s = riverLand + 4;
            if (!samples.Contains(s)) samples.Add(s);
        }
        samples.Sort();
        for (int g = 0; g < samples.Count; g++)
        {
            int i = samples[g]; Vector3 p = path[i] + Vector3.up;
            Gate(parent, "Puerta de vuelta " + g, g, p, Fwd(path, i, true), half[i] * 2 + 3); gates.Add(p);
        }
        foreach (var c in new[] { plank, dirtCut, cave })
            for (int g = 0; g < samples.Count; g++)
            {
                int i = samples[g]; if (i <= c.a || i >= c.b) continue;
                float f = (dist[i] - dist[c.a]) / (dist[c.b] - dist[c.a]);
                Vector3 p = c.At(f, out var dir) + Vector3.up;
                Gate(parent, "Control " + c.name + " " + g, g, p, dir, c.half * 2 + 5);
            }
        return gates;
    }
    static void Gate(Transform parent, string name, int index, Vector3 p, Vector3 fwd, float width)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(p, Quaternion.LookRotation(fwd));
        var c = go.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = new Vector3(width, 6, 1.5f);
        go.AddComponent<CrazyKartCheckpointIndex>().index = index;
    }

    // ---------- reuse karts, camera, light and the start gantry; remove everything else
    static List<CrazyKartController> StripOldTrack(UnityEngine.SceneManagement.Scene scene, out GameObject box, out GameObject pad)
    {
        var keepRoot = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Carrera (reutilizado)")?.transform ?? new GameObject("Carrera (reutilizado)").transform;
        var karts = Object.FindObjectsByType<CrazyKartController>(FindObjectsSortMode.None).ToList();
        var keep = new List<Transform>();
        keep.AddRange(karts.Select(k => k.transform));
        keep.Add(Object.FindAnyObjectByType<CrazyKartDirector>().transform);
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) keep.Add(c.transform);
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) keep.Add(l.transform);
        foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)) keep.Add(v.transform);
        foreach (var name in new[] { "Arco de salida", "Columna meta", "Cuadros de meta", "COSTA DEL CAOS", "GRAN PREMIO", "Mar tropical", "Cielo" })
        { var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == name); if (t != null) keep.Add(t); }
        box = Object.Instantiate(Object.FindAnyObjectByType<CrazyKartItemBox>().gameObject); box.name = "Caja sorpresa";
        pad = Object.Instantiate(Object.FindAnyObjectByType<CrazyKartBoostPad>().gameObject); pad.name = "Turbo de pista";
        foreach (var t in keep.Distinct()) if (t != null && t != keepRoot && t.parent != keepRoot) t.SetParent(keepRoot, true);
        foreach (var r in scene.GetRootGameObjects()) if (r.transform != keepRoot && r != box && r != pad) Object.DestroyImmediate(r);
        var sea = keepRoot.Find("Mar tropical"); if (sea != null) { var s = sea.localScale; sea.localScale = new Vector3(Mathf.Max(s.x, 1400), s.y, Mathf.Max(s.z, 1400)); sea.position = new Vector3(60, -1.6f, -40); }
        var sign = keepRoot.Find("COSTA DEL CAOS") ?? keepRoot.Find("GRAN PREMIO"); if (sign != null) { sign.name = "GRAN PREMIO"; var tm = sign.GetComponent<TextMesh>(); if (tm != null) tm.text = "GRAN PREMIO\nMULTIVERSO"; }
        // Faster karts: the circuit is long, so give every kart more top speed and punch.
        foreach (var k in karts)
        {
            var so = new SerializedObject(k);
            so.FindProperty("maxSpeed").floatValue = 30; so.FindProperty("acceleration").floatValue = 26; so.FindProperty("turnSpeed").floatValue = 112;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return karts;
    }
    static void PlaceKarts(List<CrazyKartController> karts)
    {
        var order = karts.OrderBy(k => k.IsHuman ? 1 : 0).ToList();   // human starts at the back of the grid
        for (int k = 0; k < order.Count; k++)
        {
            int row = k / 2, lane = k % 2 == 0 ? -1 : 1;
            int i = Mathf.RoundToInt((13 - row * 6) / Step);
            var t = order[k].transform; t.position = path[i] + Side(i) * lane * 2.6f + Vector3.up * .6f; t.rotation = Quaternion.LookRotation(Fwd(path, i, true));
        }
    }
    static void PlaceItems(GameObject box, GameObject pad)
    {
        var parent = new GameObject("Objetos").transform; parent.SetParent(root, false);
        void One(Vector3 p, Vector3 f) { var b = Object.Instantiate(box, parent); b.name = "Caja sorpresa"; b.transform.position = p + Vector3.up * 1.35f; b.transform.rotation = Quaternion.LookRotation(f); }
        void Row(int i, int count)
        {
            for (int k = 0; k < count; k++) One(path[i] + Side(i) * (k - (count - 1) * .5f) * Mathf.Min(3.2f, half[i] * 2 / count), Fwd(path, i, true));
        }
        Row(Nearest(80, 103), 4); Row(Nearest(270, -100), 4); Row(Nearest(186, -188), 3); Row(cliffLand + 22, 4);
        Row(riverLand + 18, 4); Row(Nearest(-150, 155), 4); Row(Nearest(-130, -160), 3);
        foreach (float t in new[] { .4f, .5f, .6f }) { var p = cave.At(t, out var cdir); One(p, cdir); }
        One(Vector3.Lerp(plank.start, plank.end, .5f), (plank.end - plank.start).normalized);
        void Pad(Vector3 p, Vector3 fwd) { var o = Object.Instantiate(pad, parent); o.name = "Turbo de pista"; o.transform.position = p + Vector3.up * .08f; o.transform.rotation = Quaternion.LookRotation(fwd); }
        foreach (int i in new[] { cliffLip - 16, riverLip - 16, Nearest(-50, -190), Nearest(70, -205), Nearest(-198, 50), Nearest(30, 103) })
            Pad(path[i], Fwd(path, i, true));
        Vector3 dd = dirtCut.end - dirtCut.start; dd.y = 0; Pad(Vector3.Lerp(dirtCut.start, dirtCut.end, .1f), dd.normalized);
    }

    // ---------- obstacles on the road
    static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/002_Modelos/piratas/FBX/" + name + ".fbx");
    static void Obstacle(Transform parent, string model, Vector3 pos, float height, float radius, bool sphere, Vector3 axis = default, float amp = 0, float speed = 1, float phase = 0)
    {
        var go = new GameObject("Obstáculo " + model); go.transform.SetParent(parent, false); go.transform.position = pos;
        Collider col;
        if (sphere) { var s = go.AddComponent<SphereCollider>(); s.radius = radius; s.center = Vector3.up * radius; col = s; }
        else { var c = go.AddComponent<CapsuleCollider>(); c.radius = radius; c.height = Mathf.Max(height, radius * 2); c.center = Vector3.up * c.height * .5f; col = c; }
        var o = go.AddComponent<CrazyKartObstacle>(); o.radius = radius; o.axis = axis; o.amplitude = amp; o.speed = speed; o.phase = phase; o.rolls = sphere && amp > 0;
        var vis = new GameObject("Visual").transform; vis.SetParent(go.transform, false); vis.localPosition = sphere ? Vector3.up * radius : Vector3.zero;
        if (sphere && model == "Peñasco")
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(ball.GetComponent<Collider>()); ball.name = "Peñasco";
            ball.transform.SetParent(vis, false); ball.transform.localScale = Vector3.one * radius * 2; ball.GetComponent<Renderer>().sharedMaterial = rock;
            for (int k = 0; k < 6; k++) { var chip = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(chip.GetComponent<Collider>()); chip.transform.SetParent(vis, false); chip.transform.localPosition = UnityEngine.Random.onUnitSphere * radius * .8f; chip.transform.localRotation = UnityEngine.Random.rotation; chip.transform.localScale = Vector3.one * radius * .7f; chip.GetComponent<Renderer>().sharedMaterial = rock; }
        }
        else if (Prefab(model) != null)
        {
            PirateArenaVisuals.AddModel(Prefab(model), vis, height, island, false);
            foreach (var c in vis.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }
        o.visual = vis;
    }
    static void PlaceObstacles()
    {
        var parent = new GameObject("Obstáculos").transform; parent.SetParent(root, false);
        // Village chicane: barrel stacks alternating sides.
        int[] barrels = { Nearest(198, 56), Nearest(224, 48), Nearest(252, 49) };
        for (int k = 0; k < barrels.Length; k++)
        {
            int i = barrels[k]; float s = k % 2 == 0 ? 1 : -1;
            Obstacle(parent, "Prop_Barrel", path[i] + Side(i) * s * 1.2f, 1.3f, .7f, false);
            Obstacle(parent, "Prop_Barrel", path[i] + Side(i) * s * 1.2f + Fwd(path, i, true) * 1.6f, 1.3f, .7f, false);
        }
        // Mountain: fallen rocks on the climb.
        foreach (var (x, z, s) in new[] { (176f, -124f, -1f), (190f, -180f, 1.2f) })
        { int i = Nearest(x, z); Obstacle(parent, "Environment_Rock_" + (s > 0 ? 2 : 4), path[i] + Side(i) * s * 2.3f, 1.8f, 1.3f, false); }
        // Jungle: rock in the S-curve.
        { int i = Nearest(-166, -100); Obstacle(parent, "Environment_Rock_3", path[i] + Side(i) * 2.5f, 1.8f, 1.3f, false); }
        // West straight: boulders rolling across the road.
        foreach (var (z, ph) in new[] { (5f, 0f), (40f, 2.1f), (68f, 4.2f) })
        {
            int i = Nearest(-199, z); var side = Side(i);
            Obstacle(parent, "Peñasco", path[i] - Vector3.up * .02f, 3.2f, 1.6f, true, side, half[i] - 4f, 1.1f, ph);
        }
    }

    // ---------- scenery
    static void Model(string name, Vector3 p, float height, float yaw, bool collide = true, bool onGround = true)
    {
        var asset = Prefab(name); if (asset == null) return;
        var go = new GameObject(name); go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(new Vector3(p.x, onGround ? Height(p.x, p.z) : p.y, p.z), Quaternion.Euler(0, yaw, 0));
        PirateArenaVisuals.AddModel(asset, go.transform, height, island, false);
        if (collide) foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
    }
    static bool Clear(Vector3 p, float margin)
    {
        for (int i = 0; i < path.Count; i += 2) { var d = path[i] - p; d.y = 0; if (d.magnitude < half[i] + margin) return false; }
        foreach (var c in new[] { plank, dirtCut, cave }) if (c.Distance(p.x, p.z, out _) < c.half + 5) return false;
        float h = Height(p.x, p.z); if (h < .2f) return false;
        return true;
    }
    static void Sign(string text, Vector3 p, Vector3 fwd)
    {
        var o = new GameObject(text.Replace('\n', ' '), typeof(TextMesh)); o.transform.SetParent(root, false); o.transform.SetPositionAndRotation(p, Quaternion.LookRotation(fwd));
        var t = o.GetComponent<TextMesh>(); t.text = text; t.fontSize = 90; t.characterSize = .075f; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = new Color(1, .86f, .2f);
        if (textFont != null) { t.font = textFont; o.GetComponent<Renderer>().sharedMaterial = textMat; }
    }
    static void Decorate()
    {
        var rng = new System.Random(23);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        // Palms along beach, village, west and jungle (denser in the jungle).
        for (int i = 0; i < path.Count; i += 5)
        {
            var z = zone[i]; if (z == Zone.Mountain || z == Zone.Tunnel || z == Zone.Cliff || z == Zone.Viaduct || z == Zone.Bridge) continue;
            int tries = z == Zone.Jungle ? 3 : 1;
            for (int t = 0; t < tries; t++)
                foreach (int s in new[] { -1, 1 })
                {
                    var p = path[i] + Side(i) * s * (half[i] + R(4, z == Zone.Jungle ? 22 : 12)) + Fwd(path, i, true) * R(-4, 4);
                    if (!Clear(p, 3.5f)) continue;
                    Model("Environment_PalmTree_" + (1 + rng.Next(3)), p, R(6, 10), R(0, 360));
                }
        }
        // Rocks and cliffs in the mountains and along the ravines.
        for (int k = 0; k < 60; k++)
        {
            int i = rng.Next(path.Count); if (zone[i] != Zone.Mountain && zone[i] != Zone.Cliff && zone[i] != Zone.Viaduct && zone[i] != Zone.Jungle) continue;
            var p = path[i] + Side(i) * (rng.Next(2) == 0 ? -1 : 1) * (half[i] + R(7, 20)); if (!Clear(p, 5)) continue;
            Model("Environment_Rock_" + (1 + rng.Next(5)), p, R(4, 10), R(0, 360));
        }
        // Pirate village south of the chicane, port and ships by the lagoon.
        for (int k = 0; k < 6; k++)
        {
            int i = Nearest(170 + k * 20, 74 - (k % 3) * 10); var p = path[i] - Side(i) * (half[i] + 12 + (k % 2) * 6);
            if (Clear(p, 8)) Model("Environment_House" + (1 + k % 3), p, 8, R(0, 360));
        }
        Model("Environment_Sawmill", S(240, 0, -150), 10, 30);
        foreach (var (x, z) in new[] { (175f, 92f), (250f, 72f), (205f, 28f) }) { var p = S(x, 0, z); if (Clear(p, 3)) Model("Prop_Cannon", p, 1.6f, R(0, 360)); }
        foreach (var (x, z) in new[] { (180f, 25f), (232f, 20f), (262f, 65f), (150f, 60f) }) { var p = S(x, 0, z); if (Clear(p, 3)) Model("Prop_Barrel", p, 1.3f, R(0, 360)); }
        Model("Ship_Large", S(330, -2, -40) + Vector3.up * -1.2f, 16, 100, false, false);
        Model("Environment_Dock", S(268, -1, -35) + Vector3.up * -1.6f, 3, 90, false, false);
        Model("Ship_Small", S(-260, -2, -40) + Vector3.up * -1.2f, 8, 80, false, false);
        // Sea stacks for a skyline.
        foreach (var (x, z, h) in new[] { (380f, 160f, 40f), (420f, -120f, 55f), (-300f, 220f, 45f), (-310f, -250f, 60f), (100f, -330f, 50f), (300f, -300f, 38f) })
            Model("Environment_Cliff" + (1 + rng.Next(4)), S(x, 0, z) + Vector3.down * 3, h, R(0, 360), false, false);
        // Treasure cave dressing (against the walls, out of the driving line).
        Vector3 CaveSide(float t, out Vector3 at) { at = cave.At(t, out var dd); return Vector3.Cross(Vector3.up, dd); }
        var cs = CaveSide(.5f, out var c50); Model("Prop_Chest_Gold", c50 - cs * (cave.half - .6f), 1.2f, 90, false, false);
        cs = CaveSide(.46f, out var c46); Model("Prop_Coins", c46 - cs * (cave.half - .5f), .5f, 0, false, false);
        cs = CaveSide(.32f, out var c30); Model("Environment_LargeBones", c30 + cs * (cave.half - .4f), 2.5f, 0, false, false);
        cs = CaveSide(.68f, out var c70); Model("Environment_Skulls", c70 - cs * (cave.half - .4f), 1f, 0, false, false);
        // Entrance hidden behind palms.
        cs = CaveSide(.1f, out var c06); foreach (int s in new[] { -1, 1 }) { var pp = c06 + cs * s * (cave.half + 2.5f); if (FarFromRoad(pp, 3)) Model("Environment_PalmTree_2", pp, 8, R(0, 360), true); }
        // Signs (the cave is deliberately unmarked).
        Sign("¡CAÍDA DEL ACANTILADO!\nNO FRENES", path[cliffLip - 26] + Vector3.up * 5, Fwd(path, cliffLip - 26, true));
        Sign("¡SALTO DEL RÍO!", path[riverLip - 22] + Vector3.up * 4.5f, Fwd(path, riverLip - 22, true));
        int via = Nearest(-25, -196); Sign("VIADUCTO\nSIN BARANDILLAS", path[via] + Vector3.up * 4f, Fwd(path, via, true));
        int vil = Nearest(170, 76); Sign("PUEBLO PIRATA\n¡CUIDADO CON LOS BARRILES!", path[vil] + Vector3.up * 4.5f, Fwd(path, vil, true));
        int tun = Nearest(178, -190); Sign("TÚNEL DE LA CUMBRE", path[tun] + Vector3.up * 5f, Fwd(path, tun, true));
        int wst = Nearest(-199, -2); Sign("¡PEÑASCOS RODANTES!", path[wst] + Vector3.up * 4.5f, Fwd(path, wst, true));
        Vector3 pf = plank.end - plank.start; pf.y = 0; Sign("ATAJO\nTABLÓN ESTRECHO", plank.start + pf.normalized * 4 + Vector3.up * 4.5f, pf.normalized);
        Vector3 df = dirtCut.end - dirtCut.start; df.y = 0; Sign("ATAJO DE TIERRA\n¡USA UN TURBO!", dirtCut.start + df.normalized * 7 + Vector3.up * 3.5f, df.normalized);
    }
}
