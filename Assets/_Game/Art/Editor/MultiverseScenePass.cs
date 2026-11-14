using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Non-destructive art passes. Each pass adds one root ("ArtPass · Claude") to its scene and rebuilds only that root,
/// so re-running is safe and the existing map, portals, colliders and gameplay objects stay untouched.
/// Every placement checks: ground underneath, no overlap with existing colliders (paths, roads, barriers, triggers,
/// other art) and scene-specific keep-out rules. Rejected placements are logged.
/// </summary>
public static class MultiverseScenePass
{
    public const string RootName = "ArtPass · Claude";
    static Transform root;
    static int placed, skipped;
    static readonly List<string> log = new();
    static string P(string cat, string name) => $"{MultiverseArtLibrary.Root}/Prefabs/{cat}/{name}.prefab";

    static GameObject Prefab(string cat, string name)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(P(cat, name));
        if (p == null) throw new Exception("Falta el prefab " + cat + "/" + name + " (ejecuta Generar biblioteca)");
        return p;
    }

    static void Begin(string scenePath)
    {
        if (Application.isPlaying) throw new Exception("Sal de Play primero");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != scenePath)
        {
            if (scene.isDirty) throw new Exception("La escena activa tiene cambios sin guardar");
            scene = EditorSceneManager.OpenScene(scenePath);
        }
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old != null) Object.DestroyImmediate(old);
        root = new GameObject(RootName).transform;
        placed = skipped = 0; log.Clear();
        Physics.SyncTransforms();
    }

    static Transform Group(string name)
    {
        var t = root.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(root, false); }
        return t;
    }

    static string End(string label)
    {
        int texts = MultiverseArtLibrary.UpgradeSceneText();
        Physics.SyncTransforms();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        int tris = 0, renderers = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) { tris += mf.sharedMesh.triangles.Length / 3; renderers++; }
        string msg = $"{label}: {placed} piezas colocadas, {skipped} descartadas por seguridad, {texts} textos mejorados, {renderers} renderers, {tris} triángulos añadidos";
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Claude_Capturas")); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Pase_" + label + ".txt"), msg + "\n" + string.Join("\n", log));
        return msg;
    }

    static bool UnderRoot(Transform t) => t == root || t.IsChildOf(root);

    /// <summary>Ground under (x,z), ignoring art already placed. Returns false if nothing solid is below.</summary>
    static bool Ground(Vector3 xz, out RaycastHit hit, Func<Collider, bool> allowed)
    {
        var hits = Physics.RaycastAll(new Vector3(xz.x, 300f, xz.z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
        foreach (var h in hits)
        {
            if (UnderRoot(h.transform)) continue;
            hit = h; return allowed == null || allowed(h.collider);
        }
        hit = default; return false;
    }

    /// <summary>
    /// Safe placement. `collide=false` strips colliders (pure decoration). `extraCheck` lets a pass apply keep-out rules.
    /// </summary>
    static GameObject Place(string group, GameObject prefab, Vector3 xz, float yaw, Func<Collider, bool> groundOk = null,
        bool collide = true, float margin = .35f, float scale = 1f, Func<Bounds, bool> keepOut = null, float? fixedY = null)
    {
        float y;
        if (fixedY.HasValue) y = fixedY.Value;
        else if (Ground(xz, out var hit, groundOk)) y = hit.point.y;
        else { Reject(prefab, xz, "sin suelo válido"); return null; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Group(group));
        inst.transform.SetPositionAndRotation(new Vector3(xz.x, y, xz.z), Quaternion.Euler(0, yaw, 0));
        inst.transform.localScale = Vector3.one * scale;
        Physics.SyncTransforms();
        var rs = inst.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        if (keepOut != null && keepOut(b)) { Object.DestroyImmediate(inst); Reject(prefab, xz, "zona reservada (ruta/pista/juego)"); return null; }
        // Footprint test from just above the ground up to the top of the piece.
        var cols = inst.GetComponentsInChildren<Collider>();
        Bounds fb;
        if (cols.Length > 0) { fb = cols[0].bounds; foreach (var c in cols) fb.Encapsulate(c.bounds); }
        else fb = b;
        float bottom = y + .03f, top = Mathf.Max(fb.max.y, bottom + .2f);
        var center = new Vector3(fb.center.x, (bottom + top) * .5f, fb.center.z);
        var half = new Vector3(fb.extents.x + margin, (top - bottom) * .5f, fb.extents.z + margin);
        foreach (var c in Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
        {
            if (c.transform.IsChildOf(inst.transform)) continue;
            Object.DestroyImmediate(inst); Reject(prefab, xz, "choca con " + c.name); return null;
        }
        if (!collide) foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        placed++;
        return inst;
    }

    /// <summary>Overhead/background decoration: no ground test, no colliders.</summary>
    static GameObject Free(string group, GameObject prefab, Vector3 pos, Vector3 euler, Vector3? scale = null)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Group(group));
        inst.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        inst.transform.localScale = scale ?? Vector3.one;
        foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        placed++; return inst;
    }

    static void Reject(GameObject prefab, Vector3 p, string why) { skipped++; log.Add($"descartado {prefab.name} en ({p.x:0.#}, {p.z:0.#}): {why}"); }

    static float YawToward(Vector3 from, Vector3 target)
    {
        // Prefab fronts face local -Z.
        var d = from - target; d.y = 0;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    static void SetLabel(GameObject go, string text)
    {
        if (go == null) return;
        var tm = go.GetComponentInChildren<TextMesh>(); if (tm != null) { tm.text = text; tm.gameObject.name = "Texto · " + text.Replace("\n", " "); }
    }

    // ===================================================================== MUNDO DE INICIO
    [MenuItem("Multiverse/Arte/2 · Decorar MundoDeInicio", priority = 2)] static void HubMenu() => Debug.Log(Hub());

    public static string Hub()
    {
        Begin("Assets/001_Scenes/MundoDeInicio.unity");
        var hub = Object.FindAnyObjectByType<MultiverseHub>();
        if (hub == null) throw new Exception("No encuentro MultiverseHub");
        // Walkable ground only: the continuous plaza floor (paths, plazas and bridges are rejected by the overlap test).
        bool Lawn(Collider c) => c.name.StartsWith("Suelo continuo");
        bool Deck(Collider c) => c.name.StartsWith("Isla") || c.name.StartsWith("Plataforma") || c.name.StartsWith("Carreras - plataforma") || c.name.StartsWith("Isla del código") || c.name.StartsWith("Plaza de las partidas") || Lawn(c);

        // Market street north of the north promenade: houses with stalls between them.
        string[] houses = { "Casa Roja", "Casa Azul", "Casa Turquesa" };
        string[] stalls = { "Puesto mercado Rojo", "Puesto mercado Turquesa", "Puesto mercado Amarillo" };
        for (int i = 0; i < 5; i++)
        {
            float x = -14f + i * 10f;
            Place("Calle del mercado", Prefab("Hub", houses[i % 3]), new Vector3(x, 0, 41.6f), 0, Lawn);
            Place("Calle del mercado", Prefab("Comun", "Farola"), new Vector3(x, 0, 36.2f), 0, Lawn);
            if (i < 4)
            {
                var s = Place("Calle del mercado", Prefab("Hub", stalls[i % 3]), new Vector3(x + 5f, 0, 40.2f), 0, Lawn, margin: .2f);
                SetLabel(s, new[] { "FRUTAS", "MAPAS", "DULCES", "RECUERDOS" }[i]);
                Free("Calle del mercado", Prefab("Comun", "Guirnalda"), new Vector3(x + 5f, 4.15f, 36.2f), Vector3.zero);
            }
        }
        // Landmark lighthouse in the north-east corner, visible from the spawn.
        Place("Hitos", Prefab("Hub", "Faro del multiverso"), new Vector3(32.5f, 0, 40.5f), -30, Lawn, margin: .2f);
        Place("Hitos", Prefab("Hub", "Fuente"), new Vector3(-31.5f, 0, -24f), 0, Lawn);
        var info = Place("Hitos", Prefab("Hub", "Kiosco info"), new Vector3(31.5f, 0, -24.5f), YawToward(new Vector3(31.5f, 0, -24.5f), Vector3.zero), Lawn);
        SetLabel(info, "INFO");

        // Promenade lamps on the lawn edge, between the existing palms; lamps along the main east–west walk.
        foreach (int side in new[] { -1, 1 })
        {
            foreach (float z in new[] { -14f, -2f, 10f, 22f, 34f }) Place("Paseos", Prefab("Comun", "Farola"), new Vector3(side * 31.3f, 0, z), 0, Lawn);
            foreach (float x in new[] { 12f, 20f })
            {
                Place("Paseos", Prefab("Comun", "Farola"), new Vector3(side * x, 0, -9f), 0, Lawn);
                Place("Paseos", Prefab("Comun", "Farola"), new Vector3(side * x, 0, -1f), 0, Lawn);
            }
            foreach (float z in new[] { -13f, 3f, 19f }) Place("Paseos", Prefab("Comun", "Banco"), new Vector3(side * 31.6f, 0, z), side * -90f, Lawn);
            foreach (float z in new[] { 1f, 13f, 25f }) Place("Paseos", Prefab("Comun", "Jardinera"), new Vector3(side * 24.4f, 0, z), 90, Lawn);
        }
        // Garlands over the east–west walk, hung between facing lamp pairs.
        foreach (float x in new[] { -20f, -12f, 12f, 20f })
            if (root.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Farola") && Mathf.Abs(t.position.x - x) < .1f && t.position.z < 0 && t.position.z > -10) == 2)
                Free("Paseos", Prefab("Comun", "Guirnalda"), new Vector3(x, 4.15f, -5f), new Vector3(0, 90, 0), new Vector3(.8f, 1, 1));

        // Portal banners at the front corners of every portal island/platform, in each mode's color.
        var gates = new (Vector3 p, string key, float w, float d)[] {
            (new Vector3(-15, 1, 14), "Piratas", 12, 10), (new Vector3(0, 1, 21), "Zombis", 12, 10), (new Vector3(15, 1, 14), "Criaturas", 12, 10),
            (new Vector3(24, 1, -10), "Carreras", 11, 9), (new Vector3(-24, 1, -10), "Codigo", 12, 12) };
        foreach (var g in gates)
            foreach (int s in new[] { -1, 1 })
            {
                // Big portal islands: mid-side (the bridges reach their front corners); platforms: front corners.
                var at = g.w >= 12 && g.d == 10 ? g.p + new Vector3(s * (g.w * .5f - .7f), 0, 0) : g.p + new Vector3(s * (g.w * .5f - .7f), 0, -(g.d * .5f - 1.3f));
                Place("Portales", Prefab("Hub", "Estandarte " + g.key), at, 0, Deck, margin: .1f);
            }

        // Gardens: trees and bushes in the open lawn corners, away from routes.
        var trees = new[] { new Vector3(-34, 0, 43), new Vector3(-34, 0, -27), new Vector3(34, 0, -27.5f), new Vector3(-20, 0, -27.6f), new Vector3(20, 0, -27.6f), new Vector3(6, 0, -27.8f), new Vector3(-6, 0, -27.8f) };
        for (int i = 0; i < trees.Length; i++) Place("Jardines", Prefab("Comun", i % 3 == 2 ? "Pino" : "Arbol redondo"), trees[i], i * 47, Lawn);
        var bushes = new[] { new Vector3(-9, 0, -28.2f), new Vector3(9, 0, -28.2f), new Vector3(-28, 0, -28.2f), new Vector3(28, 0, -28.2f), new Vector3(-36.2f, 0, 30), new Vector3(36.2f, 0, 30), new Vector3(-36.2f, 0, 0), new Vector3(36.2f, 0, 0) };
        for (int i = 0; i < bushes.Length; i++) Place("Jardines", Prefab("Comun", "Arbusto"), bushes[i], i * 33, Lawn, margin: .1f);
        // A signpost at the spawn plaza pointing to the portals.
        var sign = Place("Hitos", Prefab("Comun", "Poste indicador"), new Vector3(-7.4f, 0, -7.4f), 20, c => c.name.StartsWith("Plaza de las partidas"), margin: .1f);
        foreach (var (p, yaw) in new[] { (new Vector3(-7.6f, 0, -1.5f), 90f), (new Vector3(7.6f, 0, -1.5f), -90f) })
            Place("Hitos", Prefab("Comun", "Maceta"), p, yaw, c => c.name.StartsWith("Plaza de las partidas"), margin: .1f);

        // Sky: a few slow clouds high above the play area.
        for (int i = 0; i < 9; i++)
        {
            float a = i * 40f * Mathf.Deg2Rad, r = 62f + (i % 3) * 16f;
            var cloud = Free("Cielo", Prefab("Comun", "Nube"), new Vector3(Mathf.Cos(a) * r, 34f + (i % 4) * 5f, 8 + Mathf.Sin(a) * r), new Vector3(0, i * 23, 0), Vector3.one * (1.2f + (i % 3) * .5f));
            GameObjectUtility.SetStaticEditorFlags(cloud, 0);
            foreach (var mr in cloud.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return End("MundoDeInicio");
    }

    // ===================================================================== CARRERAS LOCAS
    [MenuItem("Multiverse/Arte/4 · Decorar CarrerasLocas", priority = 4)] static void RaceMenu() => Debug.Log(Race());

    public static string Race()
    {
        Begin("Assets/001_Scenes/CarrerasLocas.unity");
        var roads = Object.FindObjectsByType<CrazyKartRoadSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => r.centers != null && r.centers.Length > 1).ToArray();
        if (roads.Length == 0) throw new Exception("No encuentro superficies de pista");
        // Distance from a point to the nearest road edge (negative = on the road).
        float RoadClearance(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var r in roads)
            {
                int n = r.centers.Length, seg = r.loop ? n : n - 1;
                for (int i = 0; i < seg; i++)
                {
                    Vector3 a = r.centers[i], b = r.centers[(i + 1) % n]; a.y = b.y = 0; var q = new Vector3(p.x, 0, p.z);
                    float t = Mathf.Clamp01(Vector3.Dot(q - a, b - a) / Mathf.Max(.0001f, (b - a).sqrMagnitude));
                    float half = Mathf.Lerp(r.halfWidths[i], r.halfWidths[(i + 1) % n], t);
                    best = Mathf.Min(best, Vector3.Distance(q, Vector3.Lerp(a, b, t)) - half);
                }
            }
            return best;
        }
        Func<Bounds, bool> Clear(float min) => b =>
        {
            foreach (var c in new[] { b.center, new Vector3(b.min.x, 0, b.min.z), new Vector3(b.max.x, 0, b.max.z), new Vector3(b.min.x, 0, b.max.z), new Vector3(b.max.x, 0, b.min.z) })
                if (RoadClearance(c) < min) return true;
            return false;
        };
        bool NotRoad(Collider c) => c.GetComponentInParent<CrazyKartRoadSurface>() == null && !c.isTrigger;
        var main = roads.OrderByDescending(r => r.centers.Length).First();
        var path = main.centers; int count = path.Length;
        Vector3 Side(int i) { var f = path[(i + 1) % count] - path[(i + count - 1) % count]; f.y = 0; return Vector3.Cross(Vector3.up, f.normalized); }
        Vector3 Center() { var c = Vector3.zero; foreach (var p in path) c += p; return c / count; }
        var mid = Center();
        // Outside of the loop = side pointing away from the circuit's centroid.
        float Outward(int i) { var s = Side(i); return Vector3.Dot(s, path[i] - mid) > 0 ? 1 : -1; }
        Vector3 Spot(int i, float side, float off) { float half = main.halfWidths[i % count]; var p = path[i % count] + Side(i) * side * (half + off); p.y = 0; return p; }

        // Start straight: grandstand outside, pit garage + control tower inside, checkered flags at the line.
        {
            float o = Outward(0);
            var at = Spot(1, o, 9.5f);
            Place("Recta de meta", Prefab("Carreras", "Grada"), at, YawToward(at, path[1]), NotRoad, keepOut: Clear(3.5f));
            var pit = Spot(4, -o, 9f);
            Place("Recta de meta", Prefab("Carreras", "Boxes"), pit, YawToward(pit, path[4]), NotRoad, keepOut: Clear(3.5f));
            var tower = Spot(count - 4, -o, 8f);
            Place("Recta de meta", Prefab("Carreras", "Torre de control"), tower, YawToward(tower, path[count - 4]), NotRoad, keepOut: Clear(3f));
            foreach (int s in new[] { -1, 1 }) { var f = Spot(0, s, 2.6f); Place("Recta de meta", Prefab("Carreras", "Bandera a cuadros"), f, YawToward(f, path[0]), NotRoad, keepOut: Clear(1.5f), margin: .15f); }
        }
        // Corners: tyre walls on the outside of every bend, billboards further out facing the drivers.
        string[] boards = { "Valla publicitaria Turbo", "Valla publicitaria Helados", "Valla publicitaria Derrape" };
        int boardIndex = 0;
        for (int i = 6; i < count - 6; i += 6)
        {
            Vector3 a = path[(i + count - 3) % count] - path[(i + count - 6) % count], b = path[(i + 3) % count] - path[i];
            a.y = b.y = 0;
            float turn = Vector3.SignedAngle(a, b, Vector3.up);
            float outside = turn > 0 ? -1 : 1; // sideways away from the turning direction (right-hand side is +Side)
            if (Mathf.Abs(turn) > 12f)
            {
                var t = Spot(i, outside, 5f);
                Place("Curvas", Prefab("Carreras", "Pila de neumáticos"), t, YawToward(t, path[i]), NotRoad, keepOut: Clear(3f), margin: .2f);
            }
            if (i % 18 == 0)
            {
                var bb = Spot(i, Outward(i), 12f);
                Place("Curvas", Prefab("Carreras", boards[boardIndex++ % boards.Length]), bb, YawToward(bb, path[i]), NotRoad, keepOut: Clear(5f));
            }
        }
        // Beach life on the outer coast: lifeguard towers, umbrellas and an ice-cream cart.
        for (int k = 0; k < 4; k++)
        {
            int i = (count / 8) + k * count / 4;
            float o = Outward(i);
            var lg = Spot(i, o, 18f); Place("Playa", Prefab("Carreras", "Torre socorrista"), lg, YawToward(lg, mid), NotRoad, keepOut: Clear(8f));
            for (int u = 0; u < 3; u++)
            {
                var sp = Spot(i + 2 + u * 2, o, 14f + (u % 2) * 3f);
                Place("Playa", Prefab("Carreras", u % 2 == 0 ? "Sombrilla Roja" : "Sombrilla Turquesa"), sp, u * 50, NotRoad, keepOut: Clear(6f), margin: .2f);
            }
            if (k % 2 == 0) { var cart = Spot(i - 3, o, 13f); Place("Playa", Prefab("Carreras", "Carrito de helados"), cart, YawToward(cart, path[(i - 3 + count) % count]), NotRoad, keepOut: Clear(6f)); }
        }
        // Infield garden: trees and bushes around the central rock, well inside the loop.
        for (int k = 0; k < 10; k++)
        {
            float a = k * 36f * Mathf.Deg2Rad;
            var p = mid + new Vector3(Mathf.Cos(a) * 16f, 0, Mathf.Sin(a) * 26f);
            Place("Jardín central", Prefab("Comun", k % 2 == 0 ? "Arbol redondo" : "Arbusto"), p, k * 31, NotRoad, keepOut: Clear(6f));
        }
        for (int i = 0; i < 7; i++)
        {
            float a = i * 51f * Mathf.Deg2Rad, r = 110f + (i % 3) * 25f;
            var cloud = Free("Cielo", Prefab("Comun", "Nube"), mid + new Vector3(Mathf.Cos(a) * r, 34f + (i % 3) * 6f, Mathf.Sin(a) * r), new Vector3(0, i * 29, 0), Vector3.one * 2.4f);
            GameObjectUtility.SetStaticEditorFlags(cloud, 0);
        }
        return End("CarrerasLocas");
    }

    // ===================================================================== CÓDIGO MORTAL
    [MenuItem("Multiverse/Arte/5 · Decorar CodigoMortal", priority = 5)] static void MortalMenu() => Debug.Log(Mortal());

    public static string Mortal()
    {
        Begin("Assets/001_Scenes/CodigoMortal.unity");
        var world = GameObject.Find("Plaza Vértigo · mapa");
        if (world == null) throw new Exception("No encuentro el mapa de Código Mortal");
        // Everything inside the walls is collider-free: the baked NavMesh and bot routes stay valid as they are.

        // Skyline ring outside the perimeter walls (x ±55, z ±51), on a street slab that hides the void.
        var street = GameObject.CreatePrimitive(PrimitiveType.Cube); street.name = "Calle exterior (decorado)";
        street.transform.SetParent(Group("Ciudad"), false); street.transform.position = new Vector3(0, -.62f, 0); street.transform.localScale = new Vector3(190, 1.2f, 180);
        Object.DestroyImmediate(street.GetComponent<Collider>());
        street.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MultiverseArtLibrary.Root + "/Materials/Paleta/Piedra oscura.mat");
        string[] variants = { "Edificio A", "Edificio B", "Edificio C", "Edificio D" };
        int v = 0;
        for (float x = -60; x <= 60; x += 15)
            foreach (int s in new[] { -1, 1 })
            {
                var p = new Vector3(x + (s * 3), 0, s * 66f);
                Free("Ciudad", Prefab("CodigoMortal", variants[v++ % 4]), p, new Vector3(0, s > 0 ? 0 : 180, 0));
            }
        for (float z = -45; z <= 45; z += 15)
            foreach (int s in new[] { -1, 1 })
            {
                var p = new Vector3(s * 71f, 0, z + s * 2);
                Free("Ciudad", Prefab("CodigoMortal", variants[v++ % 4]), p, new Vector3(0, s > 0 ? 90 : -90, 0));
            }

        // Kiosk dressing: awning, roof trim, lit sign and rooftop units on each existing kiosk (visual only).
        int kiosk = 0;
        var labels = new[] { "ZUMOS", "TACOS", "RADIO", "BOLLOS", "LIBROS", "CAFÉ", "PIZZA", "FLORES", "JUEGOS", "SUSHI", "HELADO", "PAN" };
        foreach (var roof in world.GetComponentsInChildren<Transform>().Where(t => t.name == "Kiosco · cubierta").ToArray())
        {
            Vector3 basePos = roof.position - Vector3.up * 3.4f;
            var wall = roof.parent.GetComponentsInChildren<Transform>().Where(t => t.name == "Kiosco · pared").OrderBy(t => Vector3.Distance(t.position, basePos + new Vector3(0, 1.6f, 1.7f))).FirstOrDefault();
            bool coral = wall != null && wall.GetComponent<Renderer>().sharedMaterial != null && wall.GetComponent<Renderer>().sharedMaterial.name.Contains("Coral");
            var kit = Free("Kioscos", Prefab("CodigoMortal", coral ? "Kit kiosco Menta" : "Kit kiosco Coral"), basePos, Vector3.zero);
            SetLabel(kit, labels[kiosk++ % labels.Length]);
        }
        // Wall lamps hugging the perimeter, arms toward the plaza; neon arrows on the walls.
        for (float z = -42; z <= 42; z += 12)
            foreach (int s in new[] { -1, 1 })
                Free("Muros", Prefab("CodigoMortal", "Farola moderna"), new Vector3(s * 54.2f, 0, z), new Vector3(0, s > 0 ? 90 : -90, 0));
        for (float x = -44; x <= 44; x += 12)
            foreach (int s in new[] { -1, 1 })
                Free("Muros", Prefab("CodigoMortal", "Farola moderna"), new Vector3(x, 0, s * 50.2f), new Vector3(0, s > 0 ? 0 : 180, 0));
        foreach (var (p, yaw) in new[] { (new Vector3(-54.4f, 2.6f, -20), -90f), (new Vector3(54.4f, 2.6f, 20), 90f), (new Vector3(-54.4f, 2.6f, 30), -90f), (new Vector3(54.4f, 2.6f, -30), 90f), (new Vector3(20, 2.6f, -50.4f), 180f), (new Vector3(-20, 2.6f, 50.4f), 0f) })
            Free("Muros", Prefab("CodigoMortal", "Cartel neón flecha"), p, new Vector3(0, yaw, 0));
        // Observatory: giant screen on top of the backboard.
        Free("Observatorio", Prefab("CodigoMortal", "Pantalla gigante"), new Vector3(0, 17.4f, 47.4f), Vector3.zero);
        // Garlands between the facing kiosks across the central promenade (above head height).
        foreach (float z in new[] { -30f, -9f, 40f })
            for (int i = 0; i < 4; i++)
                Free("Guirnaldas", Prefab("Comun", "Guirnalda"), new Vector3(-13.5f + i * 9f, 4.4f, z), Vector3.zero, new Vector3(.9f, 1, 1));
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            var cloud = Free("Cielo", Prefab("Comun", "Nube"), new Vector3(Mathf.Cos(a) * 150, 85 + (i % 3) * 8, Mathf.Sin(a) * 150), new Vector3(0, i * 37, 0), Vector3.one * 3f);
            GameObjectUtility.SetStaticEditorFlags(cloud, 0);
        }
        return End("CodigoMortal");
    }

    // ===================================================================== CAPTURES
    /// <summary>Edit-mode overview captures from fixed viewpoints (Play captures are taken separately).</summary>
    public static string Overview(string label, Vector3 eye, Vector3 target, float fov = 50)
    {
        var go = new GameObject("Cámara de captura (temporal)"); var cam = go.AddComponent<Camera>();
        try
        {
            cam.fieldOfView = fov; cam.farClipPlane = 800; go.transform.position = eye; go.transform.LookAt(target);
            var main = Camera.main;
            if (main != null) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; }
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Claude_Capturas")); Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, label + ".png");
            ClaudeBridge.CaptureCamera(cam, file, 1600, 900);
            return file;
        }
        finally { Object.DestroyImmediate(go); }
    }

    public static string ArenaShots() =>
        Overview("Arena_Archipielago", new Vector3(10, 34, -78), new Vector3(0, 0, 8), 55) + "\n" +
        Overview("Arena_Isla_Faro", new Vector3(30, 12, 22), new Vector3(48, 4, 48), 50) + "\n" +
        Overview("Arena_Isla_Pueblo", new Vector3(-24, 9, 8), new Vector3(-44, 2, 24), 50);

    public static string RaceShots()
    {
        var road = Object.FindObjectsByType<CrazyKartRoadSurface>(FindObjectsSortMode.None).OrderByDescending(r => r.centers.Length).First();
        var c = road.centers; Vector3 f = (c[3] - c[0]).normalized;
        return Overview("Carreras_Vista_General", new Vector3(0, 120, -150), Vector3.zero, 50) + "\n" +
            Overview("Carreras_Salida", c[0] - f * 14 + Vector3.up * 5, c[0] + f * 20, 60) + "\n" +
            Overview("Carreras_Curva", c[c.Length / 3] + Vector3.up * 6 - (c[c.Length / 3 + 3] - c[c.Length / 3]).normalized * 18, c[c.Length / 3 + 4], 60);
    }

    public static string MortalShots() =>
        Overview("CodigoMortal_Vista_General", new Vector3(80, 70, -100), new Vector3(0, 5, 0), 50) + "\n" +
        Overview("CodigoMortal_Paseo", new Vector3(0, 3, -46), new Vector3(0, 4, 0), 60) + "\n" +
        Overview("CodigoMortal_Kioscos", new Vector3(-6, 4, -20), new Vector3(-18, 2.5f, -30), 60);

    public static string HubShots() =>
        Overview("Hub_Vista_General", new Vector3(0, 62, -62), new Vector3(0, 0, 10)) + "\n" +
        Overview("Hub_Calle_Mercado", new Vector3(0, 8, 24), new Vector3(0, 3, 42)) + "\n" +
        Overview("Hub_Desde_Inicio", new Vector3(0, 5, -16), new Vector3(0, 2, 10), 60);
}
