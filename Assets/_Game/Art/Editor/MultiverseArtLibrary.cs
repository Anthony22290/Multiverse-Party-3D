using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Generates the original low-poly prop library: palette materials, mesh assets and prefabs,
/// organized by category under Assets/_Game/Art. Re-running updates assets in place (GUIDs kept),
/// so scenes that already use the prefabs pick up the changes.
/// Menu: Multiverse > Arte > 1 · Generar biblioteca de arte
/// </summary>
public static class MultiverseArtLibrary
{
    public const string Root = "Assets/_Game/Art";
    static readonly Dictionary<string, Material> mats = new();
    static readonly List<string> report = new();

    // ---------------------------------------------------------------- palette
    static Material M(string name) => mats[name];

    static Material Make(string name, Color color, float smooth = .12f, Color? emission = null, string folder = "Paleta")
    {
        string dir = $"{Root}/Materials/{folder}"; Ensure(dir);
        string path = $"{dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color); m.color = color;
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(m);
        mats[name] = m; return m;
    }

    /// <summary>Reuse the Arena's own materials for pirate props, so new pieces match the ship exactly.</summary>
    static Material Existing(string key, string path, Color fallback)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        mats[key] = m != null ? m : Make(key, fallback);
        return mats[key];
    }

    static void Palette()
    {
        mats.Clear();
        Make("Madera clara", new Color(.78f, .55f, .32f));
        Make("Madera oscura", new Color(.42f, .26f, .16f));
        Make("Piedra", new Color(.66f, .66f, .7f));
        Make("Piedra oscura", new Color(.42f, .43f, .5f));
        Make("Crema", new Color(.97f, .91f, .78f));
        Make("Blanco", new Color(.96f, .96f, .94f));
        Make("Negro suave", new Color(.12f, .12f, .15f));
        Make("Dorado", new Color(1f, .74f, .25f), .45f);
        Make("Hierro", new Color(.22f, .24f, .29f), .3f);
        Make("Tejado rojo", new Color(.86f, .3f, .24f));
        Make("Tejado azul", new Color(.24f, .45f, .82f));
        Make("Tejado turquesa", new Color(.15f, .68f, .66f));
        Make("Paja", new Color(.86f, .7f, .38f));
        Make("Tela roja", new Color(.93f, .25f, .3f));
        Make("Tela amarilla", new Color(1f, .82f, .25f));
        Make("Tela turquesa", new Color(.2f, .78f, .76f));
        Make("Tela rosa", new Color(1f, .4f, .65f));
        Make("Tela morada", new Color(.55f, .32f, .85f));
        Make("Tela verde", new Color(.4f, .78f, .3f));
        Make("Hoja", new Color(.35f, .72f, .32f));
        Make("Hoja oscura", new Color(.2f, .52f, .3f));
        Make("Tronco", new Color(.5f, .33f, .2f));
        Make("Tierra", new Color(.4f, .27f, .17f));
        Make("Flor rosa", new Color(1f, .5f, .72f));
        Make("Flor amarilla", new Color(1f, .86f, .3f));
        Make("Agua", new Color(.35f, .8f, .95f), .8f, new Color(.05f, .22f, .3f));
        Make("Cristal", new Color(.6f, .85f, 1f), .9f, new Color(.08f, .18f, .25f));
        Make("Luz cálida", new Color(1f, .9f, .6f), .2f, new Color(2.2f, 1.6f, .7f));
        Make("Neón cian", new Color(.3f, 1f, .9f), .2f, new Color(.4f, 2.2f, 2f));
        Make("Neón rosa", new Color(1f, .35f, .7f), .2f, new Color(2.2f, .5f, 1.3f));
        Make("Neón ámbar", new Color(1f, .7f, .2f), .2f, new Color(2.2f, 1.3f, .2f));
        Make("Goma", new Color(.13f, .13f, .15f), .05f);
        Make("Hormigón", new Color(.78f, .8f, .84f));
        Make("Ventana noche", new Color(.25f, .35f, .55f), .7f, new Color(.25f, .45f, .7f));
        Make("Ventana cálida", new Color(1f, .85f, .55f), .3f, new Color(1.1f, .8f, .4f));
        // Código Mortal colors, matched to its existing scene materials.
        Make("Azul noche", new Color(.085f, .12f, .22f));
        Make("Menta", new Color(.18f, .75f, .67f));
        Make("Coral", new Color(.88f, .3f, .46f));
        Make("Ámbar", new Color(1f, .68f, .2f));
        Make("Marfil", new Color(.94f, .92f, .82f));
        // Arena pirate set: the ship's own materials.
        const string P = "Assets/003_Scripts/PirateArena/Materials/";
        Existing("Cubierta", P + "Deck_WarmWood.mat", new Color(.75f, .47f, .27f));
        Existing("Casco", P + "Hull_DarkWood.mat", new Color(.35f, .2f, .12f));
        Existing("Latón", P + "Brass_Trim.mat", new Color(.9f, .7f, .3f));
        Existing("Hierro pirata", P + "Iron_Black.mat", new Color(.16f, .16f, .18f));
        Existing("Cuerda", P + "Rope_Sand.mat", new Color(.85f, .74f, .5f));
        Existing("Farol", P + "Lantern_Cream.mat", new Color(1f, .93f, .75f));
        Existing("Atlas pirata", P + "Pirate_Atlas_URP.mat", Color.white);
        Make("Bandera negra", new Color(.1f, .09f, .1f));
        Make("Hueso", new Color(.96f, .93f, .85f));
        Make("Fuego", new Color(1f, .55f, .15f), .1f, new Color(2.6f, 1.1f, .2f));
    }

    // ---------------------------------------------------------------- saving
    static void Ensure(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
    }

    static GameObject temp;

    /// <summary>Builds the mesh asset + prefab. `setup` adds colliders, labels or child models on the root.</summary>
    static GameObject Save(string category, string name, LowPolyBuilder b, Action<GameObject> setup = null, bool isStatic = true)
    {
        string meshDir = $"{Root}/Meshes/{category}", prefabDir = $"{Root}/Prefabs/{category}";
        Ensure(meshDir); Ensure(prefabDir);
        var mesh = b.ToMesh(name);
        string meshPath = $"{meshDir}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing == null) AssetDatabase.CreateAsset(mesh, meshPath);
        else { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = b.Materials;
        setup?.Invoke(go);
        if (isStatic) foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        string prefabPath = $"{prefabDir}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        report.Add($"{category}/{name}: {b.TriangleCount} tris");
        return prefab;
    }

    static void BoxCol(GameObject go, Vector3 center, Vector3 size) { var c = go.AddComponent<BoxCollider>(); c.center = center; c.size = size; }
    static void Capsule(GameObject go, Vector3 center, float radius, float height) { var c = go.AddComponent<CapsuleCollider>(); c.center = center; c.radius = radius; c.height = height; }

    static void Label(GameObject go, string text, Vector3 local, Vector3 euler, float size, Color color)
    {
        var t = new GameObject("Texto · " + text.Replace("\n", " "), typeof(TextMesh));
        t.transform.SetParent(go.transform, false); t.transform.localPosition = local; t.transform.localRotation = Quaternion.Euler(euler);
        var tm = t.GetComponent<TextMesh>();
        var font = TextFont();
        if (font != null) { tm.font = font; t.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(); }
        tm.text = text; tm.fontSize = 96; tm.characterSize = size; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
    }

    static GameObject Model(GameObject parent, string fbx, Vector3 local, float yaw, float height, Material mat)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/002_Modelos/piratas/FBX/" + fbx + ".fbx");
        if (src == null) return null;
        var holder = new GameObject(fbx); holder.transform.SetParent(parent.transform, false);
        holder.transform.localPosition = local; holder.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, holder.transform);
        var bounds = new Bounds(); bool first = true;
        foreach (var r in inst.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); r.sharedMaterial = mat; }
        if (!first && bounds.size.y > .001f)
        {
            float s = height / bounds.size.y; inst.transform.localScale *= s;
            bounds.center = holder.transform.position + (bounds.center - holder.transform.position) * s; bounds.size *= s;
            inst.transform.position += new Vector3(holder.transform.position.x - bounds.center.x, holder.transform.position.y - bounds.min.y, holder.transform.position.z - bounds.center.z);
        }
        foreach (var c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        return holder;
    }

    public static Font TextFont()
    {
        var skin = AssetDatabase.LoadAssetAtPath<MultiverseUISkin>("Assets/_Game/Resources/MultiverseUISkin.asset");
        return skin != null ? skin.display : null;
    }

    /// <summary>Depth-tested material for TextMesh signs using the shared display font.</summary>
    public static Material TextMaterial()
    {
        string dir = $"{Root}/Materials/Texto"; Ensure(dir);
        string path = dir + "/Texto 3D.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Multiverse/Text3D");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        var font = TextFont();
        if (font != null && font.material != null) m.mainTexture = font.material.mainTexture;
        EditorUtility.SetDirty(m); return m;
    }

    /// <summary>Switches every TextMesh in the open scene to the display font with depth testing.</summary>
    public static int UpgradeSceneText(float sizeFactor = .82f)
    {
        var font = TextFont(); if (font == null) return 0;
        var mat = TextMaterial(); int n = 0;
        foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tm.font == font) continue;
            Undo.RecordObject(tm, "Texto"); tm.font = font; tm.characterSize *= sizeFactor;
            var r = tm.GetComponent<MeshRenderer>(); Undo.RecordObject(r, "Texto"); r.sharedMaterial = mat; n++;
        }
        return n;
    }

    // ---------------------------------------------------------------- menu
    [MenuItem("Multiverse/Arte/1 · Generar biblioteca de arte", priority = 1)]
    public static void GenerateMenu() { Debug.Log(Generate()); }

    public static string Generate()
    {
        if (Application.isPlaying) throw new Exception("Stop Play first");
        report.Clear();
        MultiverseUISetup.EnsureSkin();
        Palette();
        Common(); Hub(); Pirates(); Race(); Mortal();
        AssetDatabase.SaveAssets();
        BuildArenaSet();
        string txt = "Biblioteca generada: " + report.Count + " prefabs\n" + string.Join("\n", report);
        File.WriteAllText(Path.Combine(Application.dataPath, "../Docs/Claude_Biblioteca.txt"), txt);
        return txt;
    }

    // ================================================================ COMÚN
    static void Common()
    {
        const string C = "Comun";
        // Street lamp: tapered base, ringed pole, curved arm, cage lantern with a glowing core.
        var b = new LowPolyBuilder();
        b.Frustum(M("Hierro"), Vector3.zero, .26f, .18f, .32f, 8);
        b.Cylinder(M("Hierro"), new Vector3(0, .32f, 0), .07f, 3f, 8);
        b.Torus(M("Dorado"), new Vector3(0, 1.2f, 0), .085f, .03f, 8, 4);
        b.Torus(M("Dorado"), new Vector3(0, 3.1f, 0), .085f, .03f, 8, 4);
        b.Frustum(M("Hierro"), new Vector3(0, 3.3f, 0), .22f, .3f, .08f, 6);
        b.Bevel(M("Luz cálida"), new Vector3(0, 3.6f, 0), new Vector3(.36f, .5f, .36f), .06f);
        foreach (var x in new[] { -1, 1 }) foreach (var z in new[] { -1, 1 })
            b.Box(M("Hierro"), new Vector3(x * .18f, 3.6f, z * .18f), new Vector3(.05f, .54f, .05f));
        b.Frustum(M("Hierro"), new Vector3(0, 3.86f, 0), .3f, .06f, .3f, 6);
        b.Ball(M("Dorado"), new Vector3(0, 4.22f, 0), Vector3.one * .07f, 0);
        Save(C, "Farola", b, g => Capsule(g, new Vector3(0, 2f, 0), .22f, 4f));

        // Bench: three seat slats, two back slats, cast-iron side frames with curled arms.
        b = new LowPolyBuilder();
        for (int i = 0; i < 3; i++) b.Bevel(M("Madera clara"), new Vector3(0, .46f, -.18f + i * .16f), new Vector3(1.7f, .06f, .13f), .02f);
        for (int i = 0; i < 2; i++) b.Bevel(M("Madera clara"), new Vector3(0, .68f + i * .18f, .16f + i * .03f), new Vector3(1.7f, .12f, .05f), .02f, new Vector3(-12, 0, 0));
        foreach (var x in new[] { -.72f, .72f })
        {
            b.Box(M("Hierro"), new Vector3(x, .22f, -.2f), new Vector3(.07f, .44f, .07f));
            b.Box(M("Hierro"), new Vector3(x, .45f, .16f), new Vector3(.07f, .92f, .07f), new Vector3(-12, 0, 0));
            b.Box(M("Hierro"), new Vector3(x, .62f, -.04f), new Vector3(.07f, .06f, .46f));
            b.Ball(M("Hierro"), new Vector3(x, .62f, -.28f), Vector3.one * .06f, 0);
        }
        Save(C, "Banco", b, g => BoxCol(g, new Vector3(0, .45f, 0), new Vector3(1.75f, .9f, .6f)));

        // 2 m railing segment with turned posts and balusters.
        b = new LowPolyBuilder();
        foreach (var x in new[] { -1f, 1f })
        {
            b.Bevel(M("Madera oscura"), new Vector3(x, .5f, 0), new Vector3(.14f, 1f, .14f), .03f);
            b.Frustum(M("Dorado"), new Vector3(x, 1f, 0), .1f, .04f, .12f, 4, default, .125f);
        }
        b.Bevel(M("Madera clara"), new Vector3(0, .92f, 0), new Vector3(2f, .1f, .14f), .03f);
        b.Bevel(M("Madera clara"), new Vector3(0, .2f, 0), new Vector3(2f, .08f, .1f), .02f);
        for (int i = 0; i < 5; i++) b.Cylinder(M("Madera clara"), new Vector3(-.66f + i * .33f, .24f, 0), .035f, .64f, 6);
        Save(C, "Barandilla", b, g => BoxCol(g, new Vector3(0, .5f, 0), new Vector3(2.1f, 1f, .2f)));

        // Picket fence 2 m.
        b = new LowPolyBuilder();
        b.Box(M("Blanco"), new Vector3(0, .35f, 0), new Vector3(2f, .07f, .05f));
        b.Box(M("Blanco"), new Vector3(0, .7f, 0), new Vector3(2f, .07f, .05f));
        for (int i = 0; i < 8; i++)
        {
            float x = -.875f + i * .25f;
            b.Box(M("Blanco"), new Vector3(x, .45f, .04f), new Vector3(.12f, .9f, .03f));
            b.Frustum(M("Blanco"), new Vector3(x, .9f, .04f), .085f, 0f, .12f, 4, new Vector3(0, 0, 0), .125f);
        }
        Save(C, "Valla", b, g => BoxCol(g, new Vector3(0, .5f, 0), new Vector3(2f, 1f, .15f)));

        // Planter with bush and flowers.
        b = new LowPolyBuilder();
        b.Bevel(M("Piedra"), new Vector3(0, .3f, 0), new Vector3(1.6f, .6f, .8f), .08f);
        b.Box(M("Tierra"), new Vector3(0, .6f, 0), new Vector3(1.4f, .04f, .6f));
        b.Ball(M("Hoja"), new Vector3(-.4f, .82f, 0), new Vector3(.42f, .36f, .36f));
        b.Ball(M("Hoja oscura"), new Vector3(.3f, .8f, .05f), new Vector3(.45f, .34f, .34f));
        b.Ball(M("Hoja"), new Vector3(.02f, .98f, -.05f), new Vector3(.32f, .3f, .3f));
        for (int i = 0; i < 6; i++) b.Ball(M(i % 2 == 0 ? "Flor rosa" : "Flor amarilla"), new Vector3(-.6f + i * .24f, 1f + (i % 3) * .06f, (i % 2 == 0 ? -.22f : .2f)), Vector3.one * .08f, 0);
        Save(C, "Jardinera", b, g => BoxCol(g, new Vector3(0, .5f, 0), new Vector3(1.6f, 1f, .8f)));

        // Round-canopy tree and pine.
        b = new LowPolyBuilder();
        b.Frustum(M("Tronco"), Vector3.zero, .28f, .17f, 2.2f, 6);
        b.Box(M("Tronco"), new Vector3(.3f, 1.6f, 0), new Vector3(.6f, .12f, .12f), new Vector3(0, 0, 35));
        b.Ball(M("Hoja"), new Vector3(0, 3.1f, 0), new Vector3(1.5f, 1.25f, 1.5f));
        b.Ball(M("Hoja oscura"), new Vector3(.8f, 2.6f, .3f), new Vector3(.9f, .8f, .9f));
        b.Ball(M("Hoja"), new Vector3(-.7f, 2.7f, -.4f), new Vector3(.85f, .75f, .85f));
        Save(C, "Arbol redondo", b, g => Capsule(g, new Vector3(0, 1.1f, 0), .3f, 2.2f));

        b = new LowPolyBuilder();
        b.Frustum(M("Tronco"), Vector3.zero, .22f, .15f, 1.2f, 6);
        b.Cone(M("Hoja oscura"), new Vector3(0, .9f, 0), 1.5f, 2f, 7);
        b.Cone(M("Hoja"), new Vector3(0, 2f, 0), 1.15f, 1.8f, 7, new Vector3(0, 20, 0));
        b.Cone(M("Hoja oscura"), new Vector3(0, 3f, 0), .8f, 1.5f, 7, new Vector3(0, 40, 0));
        Save(C, "Pino", b, g => Capsule(g, new Vector3(0, .9f, 0), .3f, 1.8f));

        // Bush.
        b = new LowPolyBuilder();
        b.Ball(M("Hoja"), new Vector3(0, .45f, 0), new Vector3(.7f, .55f, .6f));
        b.Ball(M("Hoja oscura"), new Vector3(.45f, .35f, .1f), new Vector3(.45f, .38f, .45f));
        b.Ball(M("Hoja"), new Vector3(-.4f, .32f, -.1f), new Vector3(.42f, .35f, .42f));
        Save(C, "Arbusto", b, g => BoxCol(g, new Vector3(0, .45f, 0), new Vector3(1.5f, .9f, 1.1f)));

        // Signpost with three arrows (labels editable in the prefab).
        b = new LowPolyBuilder();
        b.Bevel(M("Madera oscura"), new Vector3(0, 1.4f, 0), new Vector3(.16f, 2.8f, .16f), .03f);
        b.Frustum(M("Dorado"), new Vector3(0, 2.8f, 0), .14f, 0, .2f, 4, default, .125f);
        string[] arrowMats = { "Tela roja", "Tela turquesa", "Tela amarilla" };
        for (int i = 0; i < 3; i++)
        {
            float y = 2.4f - i * .45f, dir = i % 2 == 0 ? 1 : -1;
            using (b.At(new Vector3(0, y, 0), new Vector3(0, i * 25 - 20, 0)))
            {
                b.Bevel(M(arrowMats[i]), new Vector3(dir * .65f, 0, 0), new Vector3(1.1f, .3f, .07f), .03f);
                b.Box(M(arrowMats[i]), new Vector3(dir * 1.2f, 0, 0), new Vector3(.21f, .21f, .07f), new Vector3(0, 0, 45));
            }
        }
        Save(C, "Poste indicador", b, g => Capsule(g, new Vector3(0, 1.4f, 0), .15f, 2.8f));

        // Flag poles in five colors (each mode uses its own banner color).
        foreach (var (key, mat) in new[] { ("Roja", "Tela roja"), ("Turquesa", "Tela turquesa"), ("Amarilla", "Tela amarilla"), ("Rosa", "Tela rosa"), ("Morada", "Tela morada"), ("Verde", "Tela verde") })
        {
            b = new LowPolyBuilder();
            b.Frustum(M("Piedra oscura"), Vector3.zero, .3f, .22f, .3f, 6);
            b.Cylinder(M("Blanco"), new Vector3(0, .3f, 0), .05f, 4.6f, 6);
            b.Ball(M("Dorado"), new Vector3(0, 4.95f, 0), Vector3.one * .1f, 0);
            // Waving flag from three hinged plates.
            for (int i = 0; i < 3; i++)
                b.Plate(M(mat), new Vector3(.2f + i * .4f, 4.4f - i * .03f, Mathf.Sin(i * 1.4f) * .08f), new Vector2(.42f, .8f - i * .05f), new Vector3(0, (i % 2 == 0 ? 12 : -12), 0));
            Save(C, "Bandera " + key, b, g => Capsule(g, new Vector3(0, 2.5f, 0), .12f, 5f));
        }

        // 10 m string of pennants with sag, no collision (always above head height when placed).
        b = new LowPolyBuilder();
        string[] pennant = { "Tela roja", "Tela amarilla", "Tela turquesa", "Tela rosa", "Tela morada", "Tela verde" };
        int n = 16;
        for (int i = 0; i < n; i++)
        {
            float t0 = i / (float)n, t1 = (i + 1) / (float)n;
            Vector3 p0 = new(-5 + t0 * 10, -Mathf.Sin(t0 * Mathf.PI) * .7f, 0), p1 = new(-5 + t1 * 10, -Mathf.Sin(t1 * Mathf.PI) * .7f, 0);
            float ang = Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg;
            b.Box(M("Cuerda"), (p0 + p1) * .5f, new Vector3(Vector3.Distance(p0, p1), .025f, .025f), new Vector3(0, 0, ang));
            Vector3 c = (p0 + p1) * .5f;
            b.Face(M(pennant[i % pennant.Length]), c + new Vector3(0, -.1f, .05f), c + new Vector3(-.22f, 0, 0), c + new Vector3(.22f, 0, 0), c + new Vector3(0, -.42f, 0));
            b.Face(M(pennant[i % pennant.Length]), c + new Vector3(0, -.1f, -.05f), c + new Vector3(-.22f, 0, 0), c + new Vector3(.22f, 0, 0), c + new Vector3(0, -.42f, 0));
        }
        Save(C, "Guirnalda", b);

        // Crate stack.
        b = new LowPolyBuilder();
        void Crate(Vector3 p, float s, float yaw)
        {
            using (b.At(p, new Vector3(0, yaw, 0)))
            {
                b.Bevel(M("Madera clara"), new Vector3(0, s * .5f, 0), Vector3.one * s, .04f);
                foreach (var y in new[] { .08f, .92f }) foreach (var z in new[] { -1, 1 })
                    b.Box(M("Madera oscura"), new Vector3(0, s * y, z * s * .505f), new Vector3(s * 1.01f, s * .1f, .02f));
                foreach (var x in new[] { -1, 1 }) b.Box(M("Madera oscura"), new Vector3(x * s * .505f, s * .5f, 0), new Vector3(.02f, s * .1f, s * 1.01f));
                b.Box(M("Madera oscura"), new Vector3(0, s * .5f, -s * .505f), new Vector3(s * 1.2f, s * .1f, .02f), new Vector3(0, 0, 45));
            }
        }
        Crate(new Vector3(-.45f, 0, 0), .9f, 0); Crate(new Vector3(.5f, 0, .1f), .8f, 14); Crate(new Vector3(0, .85f, .05f), .7f, -10);
        Save(C, "Cajas", b, g => BoxCol(g, new Vector3(0, .75f, 0), new Vector3(1.9f, 1.5f, 1f)));

        // Cloud for skies (no collision).
        b = new LowPolyBuilder();
        b.Ball(M("Blanco"), Vector3.zero, new Vector3(3f, 1.6f, 2f));
        b.Ball(M("Blanco"), new Vector3(2.4f, -.3f, .3f), new Vector3(2f, 1.2f, 1.6f));
        b.Ball(M("Blanco"), new Vector3(-2.3f, -.4f, -.2f), new Vector3(1.8f, 1.1f, 1.5f));
        b.Ball(M("Blanco"), new Vector3(.6f, .9f, 0), new Vector3(1.6f, 1.2f, 1.4f));
        Save(C, "Nube", b, isStatic: false);

        // Round pot with flowers.
        b = new LowPolyBuilder();
        b.Frustum(M("Tejado rojo"), Vector3.zero, .3f, .42f, .55f, 8);
        b.Torus(M("Tejado rojo"), new Vector3(0, .55f, 0), .42f, .05f, 8, 4);
        b.Ball(M("Hoja"), new Vector3(0, .75f, 0), new Vector3(.42f, .3f, .42f));
        for (int i = 0; i < 5; i++) { float a = i * 1.256f; b.Ball(M(i % 2 == 0 ? "Flor amarilla" : "Flor rosa"), new Vector3(Mathf.Cos(a) * .25f, .95f, Mathf.Sin(a) * .25f), Vector3.one * .08f, 0); }
        Save(C, "Maceta", b, g => Capsule(g, new Vector3(0, .5f, 0), .42f, 1f));
    }

    // ================================================================ HUB
    static void Hub()
    {
        const string C = "Hub";
        // Market stall: counter, posts, striped awning, back board and produce.
        foreach (var (key, a, bb) in new[] { ("Rojo", "Tela roja", "Blanco"), ("Turquesa", "Tela turquesa", "Blanco"), ("Amarillo", "Tela amarilla", "Tela rosa") })
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Madera clara"), new Vector3(0, .5f, -.4f), new Vector3(2.8f, 1f, .8f), .05f);
            b.Box(M("Madera oscura"), new Vector3(0, 1.03f, -.4f), new Vector3(3f, .06f, 1f));
            for (int i = 0; i < 4; i++) b.Box(M("Madera oscura"), new Vector3(-1.05f + i * .7f, .5f, -.82f), new Vector3(.08f, .8f, .02f));
            foreach (var x in new[] { -1.4f, 1.4f }) foreach (var z in new[] { -.9f, .7f })
                b.Bevel(M("Madera oscura"), new Vector3(x, 1.25f, z), new Vector3(.12f, 2.5f, .12f), .03f);
            b.Bevel(M("Madera clara"), new Vector3(0, 1.4f, .72f), new Vector3(2.8f, 1.6f, .08f), .03f);
            b.Awning(M(a), M(bb), new Vector3(0, 2.55f, .75f), 3.1f, 1.9f, .55f, 7);
            b.Bevel(M("Madera oscura"), new Vector3(0, 2.95f, .72f), new Vector3(1.8f, .5f, .08f), .04f);
            // Produce crates on the counter.
            for (int i = 0; i < 3; i++)
            {
                b.Box(M("Madera oscura"), new Vector3(-.85f + i * .85f, 1.12f, -.45f), new Vector3(.7f, .14f, .5f));
                string fruit = i == 0 ? "Tela roja" : i == 1 ? "Flor amarilla" : "Hoja";
                for (int k = 0; k < 4; k++) b.Ball(M(fruit), new Vector3(-.85f + i * .85f + (k % 2 - .5f) * .28f, 1.26f, -.45f + (k / 2 - .5f) * .2f), Vector3.one * .11f, 0);
            }
            Save(C, "Puesto mercado " + key, b, g => { BoxCol(g, new Vector3(0, .55f, -.4f), new Vector3(3f, 1.1f, 1f)); BoxCol(g, new Vector3(0, 1.5f, .72f), new Vector3(3f, 3f, .2f)); Label(g, "MERCADO", new Vector3(0, 2.95f, .66f), new Vector3(0, 0, 0), .05f, new Color(1, .95f, .8f)); });
        }

        // Plaza house: bevelled walls, overhanging gable roof, framed windows with shutters, door, chimney.
        foreach (var (key, wall, roof) in new[] { ("Roja", "Crema", "Tejado rojo"), ("Azul", "Crema", "Tejado azul"), ("Turquesa", "Blanco", "Tejado turquesa") })
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Piedra oscura"), new Vector3(0, .2f, 0), new Vector3(5.4f, .4f, 4.4f), .06f);
            b.Bevel(M(wall), new Vector3(0, 2f, 0), new Vector3(5f, 3.4f, 4f), .08f);
            foreach (var x in new[] { -2.5f, 2.5f }) foreach (var z in new[] { -2f, 2f })
                b.Box(M("Madera oscura"), new Vector3(x, 2f, z), new Vector3(.22f, 3.45f, .22f));
            b.Box(M("Madera oscura"), new Vector3(0, 3.65f, 0), new Vector3(5.2f, .18f, 4.2f));
            b.GableFill(M(wall), new Vector3(0, 3.7f, 0), 4.2f, 4.9f, 1.7f, new Vector3(0, 90, 0));
            b.Gable(M(roof), new Vector3(0, 3.6f, 0), 4.9f, 6f, 1.95f, new Vector3(0, 90, 0), .16f);
            b.Bevel(M("Piedra oscura"), new Vector3(1.4f, 5.1f, .7f), new Vector3(.55f, 1.4f, .55f), .05f);
            b.Box(M("Negro suave"), new Vector3(1.4f, 5.82f, .7f), new Vector3(.62f, .08f, .62f));
            // Door on the front (-Z).
            b.Bevel(M("Madera oscura"), new Vector3(0, 1.45f, -2.03f), new Vector3(1.3f, 2.2f, .12f), .03f);
            b.Bevel(M(roof), new Vector3(0, 1.4f, -2.08f), new Vector3(1f, 1.95f, .06f), .02f);
            b.Ball(M("Dorado"), new Vector3(.35f, 1.35f, -2.13f), Vector3.one * .06f, 0);
            b.Box(M("Madera oscura"), new Vector3(0, 2.7f, -2.3f), new Vector3(1.6f, .1f, .6f));
            void Window(Vector3 p, float yaw)
            {
                using (b.At(p, new Vector3(0, yaw, 0)))
                {
                    b.Box(M("Madera oscura"), Vector3.zero, new Vector3(1f, 1.1f, .08f));
                    b.Box(M("Ventana cálida"), new Vector3(0, 0, -.04f), new Vector3(.8f, .9f, .02f));
                    b.Box(M("Madera oscura"), new Vector3(0, 0, -.06f), new Vector3(.06f, .9f, .02f));
                    b.Box(M("Madera oscura"), new Vector3(0, 0, -.06f), new Vector3(.8f, .06f, .02f));
                    foreach (var s in new[] { -1, 1 }) b.Bevel(M(roof), new Vector3(s * .68f, 0, -.04f), new Vector3(.34f, 1.05f, .05f), .02f);
                    b.Box(M("Madera clara"), new Vector3(0, -.6f, -.12f), new Vector3(1.1f, .12f, .26f));
                    b.Box(M("Tierra"), new Vector3(0, -.5f, -.14f), new Vector3(.9f, .1f, .18f));
                    for (int i = 0; i < 4; i++) b.Ball(M(i % 2 == 0 ? "Flor rosa" : "Hoja"), new Vector3(-.33f + i * .22f, -.4f, -.15f), Vector3.one * .1f, 0);
                }
            }
            Window(new Vector3(-1.6f, 2.1f, -2.04f), 0); Window(new Vector3(1.6f, 2.1f, -2.04f), 0);
            Window(new Vector3(2.54f, 2.1f, 0), -90); Window(new Vector3(-2.54f, 2.1f, 0), 90); Window(new Vector3(0, 2.1f, 2.04f), 180);
            Save(C, "Casa " + key, b, g => BoxCol(g, new Vector3(0, 2.6f, 0), new Vector3(5.4f, 5.2f, 4.4f)));
        }

        // Multiverse lighthouse: striped octagonal tower, balcony, glowing lantern, cap and flag.
        {
            var b = new LowPolyBuilder();
            b.Frustum(M("Piedra oscura"), Vector3.zero, 2.4f, 2.2f, .8f, 8, default, .0625f);
            for (int i = 0; i < 5; i++)
                b.Frustum(M(i % 2 == 0 ? "Blanco" : "Tejado rojo"), new Vector3(0, .8f + i * 1.8f, 0), 1.9f - i * .14f, 1.9f - (i + 1) * .14f, 1.8f, 8, default, .0625f);
            b.Frustum(M("Madera oscura"), new Vector3(0, 9.8f, 0), 1.6f, 1.65f, .25f, 8, default, .0625f);
            for (int i = 0; i < 16; i++) { float a = i * Mathf.PI / 8; b.Cylinder(M("Dorado"), new Vector3(Mathf.Cos(a) * 1.5f, 10.05f, Mathf.Sin(a) * 1.5f), .04f, .7f, 4); }
            b.Torus(M("Dorado"), new Vector3(0, 10.75f, 0), 1.5f, .05f, 16, 4);
            b.Frustum(M("Cristal"), new Vector3(0, 10.05f, 0), .95f, .95f, 1.5f, 8, default, .0625f);
            b.Ball(M("Luz cálida"), new Vector3(0, 10.8f, 0), Vector3.one * .55f);
            b.Frustum(M("Tejado rojo"), new Vector3(0, 11.55f, 0), 1.2f, .1f, 1.3f, 8, default, .0625f);
            b.Cylinder(M("Hierro"), new Vector3(0, 12.8f, 0), .04f, 1.2f, 4);
            b.Plate(M("Tela turquesa"), new Vector3(.35f, 13.7f, 0), new Vector2(.7f, .45f));
            b.Bevel(M("Madera oscura"), new Vector3(0, 1.1f, -1.88f), new Vector3(1f, 1.9f, .2f), .04f);
            for (int i = 0; i < 3; i++) b.Box(M("Ventana cálida"), new Vector3(0, 3.5f + i * 2.2f, -1.72f + i * .14f), new Vector3(.45f, .7f, .1f), new Vector3(-4, 0, 0));
            Save(C, "Faro del multiverso", b, g => { Capsule(g, new Vector3(0, 5.5f, 0), 2.1f, 11f); });
        }

        // Fountain: octagonal basin, water, pillar, upper bowl and glowing spout.
        {
            var b = new LowPolyBuilder();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + Mathf.PI / 8f;
                b.Bevel(M("Piedra"), new Vector3(Mathf.Cos(a) * 2.25f, .35f, Mathf.Sin(a) * 2.25f), new Vector3(.4f, .7f, 1.9f), .06f, new Vector3(0, -a * Mathf.Rad2Deg, 0));
            }
            b.Frustum(M("Agua"), new Vector3(0, .02f, 0), 2.15f, 2.15f, .5f, 8, default, .0625f);
            b.Frustum(M("Piedra"), new Vector3(0, .5f, 0), .45f, .32f, 1.4f, 8);
            b.Frustum(M("Piedra"), new Vector3(0, 1.9f, 0), .35f, 1.1f, .35f, 8);
            b.Frustum(M("Agua"), new Vector3(0, 2.22f, 0), .95f, .95f, .06f, 8);
            b.Frustum(M("Piedra"), new Vector3(0, 2.25f, 0), .18f, .1f, .7f, 6);
            b.Ball(M("Agua"), new Vector3(0, 3.05f, 0), new Vector3(.25f, .35f, .25f), 0);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; b.Ball(M("Agua"), new Vector3(Mathf.Cos(a) * .85f, 1.5f, Mathf.Sin(a) * .85f), new Vector3(.08f, .5f, .08f), 0); }
            Save(C, "Fuente", b, g => Capsule(g, new Vector3(0, .9f, 0), 2.4f, 1.8f));
        }

        // Info kiosk: hexagonal booth with a hip roof and a sign.
        {
            var b = new LowPolyBuilder();
            b.Frustum(M("Piedra oscura"), Vector3.zero, 1.25f, 1.25f, .25f, 6);
            b.Frustum(M("Crema"), new Vector3(0, .25f, 0), 1.05f, 1.05f, 1.1f, 6);
            for (int i = 0; i < 6; i++) { float a = i * Mathf.PI / 3; b.Bevel(M("Madera oscura"), new Vector3(Mathf.Cos(a) * 1.05f, 1.6f, Mathf.Sin(a) * 1.05f), new Vector3(.12f, 2.7f, .12f), .03f); }
            b.Frustum(M("Madera clara"), new Vector3(0, 1.35f, 0), 1.12f, 1.12f, .1f, 6);
            b.Frustum(M("Tejado azul"), new Vector3(0, 2.9f, 0), 1.7f, .1f, 1.2f, 6);
            b.Ball(M("Dorado"), new Vector3(0, 4.15f, 0), Vector3.one * .15f, 0);
            b.Bevel(M("Tejado azul"), new Vector3(0, 2.6f, -1.02f), new Vector3(1.3f, .45f, .08f), .03f, new Vector3(0, 30, 0));
            Save(C, "Kiosco info", b, g => { Capsule(g, new Vector3(0, 1.2f, 0), 1.2f, 2.4f); Label(g, "INFO", new Vector3(-.03f, 2.6f, -1.08f), new Vector3(0, 30, 0), .045f, Color.white); });
        }

        // Portal banner pylons, one per mode color.
        foreach (var (key, mat) in new[] { ("Piratas", "Tela roja"), ("Zombis", "Tela verde"), ("Criaturas", "Tela morada"), ("Carreras", "Tela amarilla"), ("Codigo", "Tela turquesa") })
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Piedra oscura"), new Vector3(0, .3f, 0), new Vector3(.8f, .6f, .8f), .08f);
            b.Bevel(M("Madera oscura"), new Vector3(0, 2.4f, 0), new Vector3(.24f, 3.6f, .24f), .05f);
            b.Bevel(M("Madera oscura"), new Vector3(0, 4.05f, -.35f), new Vector3(.12f, .12f, .9f), .03f);
            b.Box(M(mat), new Vector3(0, 3.1f, -.6f), new Vector3(.04f, 1.7f, .7f));
            b.Face(M(mat), new Vector3(0, 2.1f, -.6f), new Vector3(.02f, 2.25f, -.25f), new Vector3(.02f, 2.25f, -.95f), new Vector3(.02f, 1.95f, -.6f));
            b.Face(M(mat), new Vector3(0, 2.1f, -.6f), new Vector3(-.02f, 2.25f, -.25f), new Vector3(-.02f, 2.25f, -.95f), new Vector3(-.02f, 1.95f, -.6f));
            b.Box(M("Dorado"), new Vector3(0, 3.95f, -.6f), new Vector3(.06f, .08f, .8f));
            b.Bevel(M("Luz cálida"), new Vector3(0, 4.45f, 0), new Vector3(.3f, .4f, .3f), .05f);
            b.Frustum(M("Hierro"), new Vector3(0, 4.65f, 0), .26f, .05f, .25f, 4, default, .125f);
            Save(C, "Estandarte " + key, b, g => Capsule(g, new Vector3(0, 2.3f, 0), .4f, 4.6f));
        }
    }

    // ================================================================ PIRATAS (Arena + Hub)
    static void Pirates()
    {
        const string C = "Piratas";
        // Jolly Roger flag pole: black flag, skull and crossed bones.
        {
            var b = new LowPolyBuilder();
            b.Cylinder(M("Casco"), Vector3.zero, .09f, 6f, 6);
            b.Ball(M("Latón"), new Vector3(0, 6.1f, 0), Vector3.one * .14f, 0);
            for (int i = 0; i < 3; i++)
                b.Plate(M("Bandera negra"), new Vector3(.35f + i * .6f, 5.25f - i * .04f, Mathf.Sin(i * 1.5f) * .1f), new Vector2(.62f, 1.3f - i * .06f), new Vector3(0, i % 2 == 0 ? 10 : -10, 0), .04f);
            b.Ball(M("Hueso"), new Vector3(.9f, 5.4f, -.07f), new Vector3(.28f, .26f, .05f), 0);
            b.Box(M("Bandera negra"), new Vector3(.8f, 5.42f, -.12f), new Vector3(.08f, .08f, .02f));
            b.Box(M("Bandera negra"), new Vector3(1f, 5.42f, -.12f), new Vector3(.08f, .08f, .02f));
            b.Box(M("Hueso"), new Vector3(.9f, 5f, -.07f), new Vector3(.7f, .08f, .04f), new Vector3(0, 0, 35));
            b.Box(M("Hueso"), new Vector3(.9f, 5f, -.07f), new Vector3(.7f, .08f, .04f), new Vector3(0, 0, -35));
            Save(C, "Bandera pirata", b, g => Capsule(g, new Vector3(0, 3f, 0), .12f, 6f));
        }
        // Beach hut on stilts: deck, woven walls with frame, layered thatch, ladder.
        {
            var b = new LowPolyBuilder();
            foreach (var x in new[] { -1.4f, 1.4f }) foreach (var z in new[] { -1.4f, 1.4f }) b.Cylinder(M("Casco"), new Vector3(x, -1.5f, z), .14f, 2.6f, 6);
            b.Box(M("Cubierta"), new Vector3(0, 1.1f, -.3f), new Vector3(3.4f, .16f, 3.8f));
            b.Bevel(M("Paja"), new Vector3(0, 2.2f, .2f), new Vector3(2.8f, 2f, 2.6f), .05f);
            foreach (var x in new[] { -1.4f, 1.4f }) foreach (var z in new[] { -1.1f, 1.5f }) b.Box(M("Casco"), new Vector3(x, 2.2f, z), new Vector3(.14f, 2.05f, .14f));
            b.Box(M("Negro suave"), new Vector3(0, 1.95f, -1.11f), new Vector3(.9f, 1.5f, .04f));
            b.Box(M("Negro suave"), new Vector3(1.41f, 2.3f, .2f), new Vector3(.04f, .6f, .8f));
            b.Hip(M("Paja"), new Vector3(0, 3.15f, .2f), 3.9f, 3.7f, 1.3f, .6f);
            b.Hip(M("Cuerda"), new Vector3(0, 3.9f, .2f), 2.2f, 2.1f, .9f, 0f);
            for (int i = 0; i < 4; i++) b.Box(M("Casco"), new Vector3(0, .35f + i * .25f, -2.35f + i * .12f), new Vector3(.8f, .06f, .14f));
            foreach (var x in new[] { -.42f, .42f }) b.Box(M("Casco"), new Vector3(x, .7f, -2.2f), new Vector3(.07f, 1.2f, .07f), new Vector3(-25, 0, 0));
            Save(C, "Choza de playa", b, g => BoxCol(g, new Vector3(0, 1.5f, 0), new Vector3(3.5f, 3f, 3.8f)));
        }
        // Lookout tower: splayed legs, braces, platform with railing, little roof, Jolly Roger.
        {
            var b = new LowPolyBuilder();
            foreach (var x in new[] { -1, 1 }) foreach (var z in new[] { -1, 1 })
                b.Box(M("Casco"), new Vector3(x * 1.05f, 2.5f, z * 1.05f), new Vector3(.2f, 5.1f, .2f), new Vector3(z * -5, 0, x * 5));
            for (int i = 0; i < 2; i++) foreach (var r in new[] { 0, 90, 180, 270 })
                using (b.At(new Vector3(0, 1.3f + i * 1.9f, 0), new Vector3(0, r, 0)))
                    b.Box(M("Cuerda"), new Vector3(0, 0, -1.1f + i * .05f), new Vector3(2.6f, .08f, .08f), new Vector3(0, 0, 32));
            b.Box(M("Cubierta"), new Vector3(0, 5.1f, 0), new Vector3(2.8f, .18f, 2.8f));
            foreach (var r in new[] { 0, 90, 180, 270 })
                using (b.At(new Vector3(0, 5.1f, 0), new Vector3(0, r, 0)))
                {
                    b.Box(M("Casco"), new Vector3(0, .85f, -1.35f), new Vector3(2.8f, .1f, .1f));
                    for (int k = 0; k < 4; k++) b.Box(M("Casco"), new Vector3(-1.05f + k * .7f, .45f, -1.35f), new Vector3(.08f, .8f, .08f));
                }
            foreach (var x in new[] { -1.3f, 1.3f }) foreach (var z in new[] { -1.3f, 1.3f }) b.Box(M("Casco"), new Vector3(x, 6.2f, z), new Vector3(.12f, 2.2f, .12f));
            b.Hip(M("Paja"), new Vector3(0, 7.25f, 0), 3.3f, 3.3f, 1.1f, 0f);
            b.Cylinder(M("Casco"), new Vector3(0, 8.3f, 0), .05f, 1.5f, 4);
            b.Plate(M("Bandera negra"), new Vector3(.4f, 9.45f, 0), new Vector2(.8f, .5f));
            b.Ball(M("Hueso"), new Vector3(.4f, 9.48f, -.03f), new Vector3(.12f, .11f, .02f), 0);
            Save(C, "Torre vigía", b, g => BoxCol(g, new Vector3(0, 3.7f, 0), new Vector3(3f, 7.4f, 3f)));
        }
        // Stone lighthouse for the archipelago.
        {
            var b = new LowPolyBuilder();
            b.Frustum(M("Piedra oscura"), Vector3.zero, 2.3f, 2f, 1f, 9);
            for (int i = 0; i < 4; i++)
                b.Frustum(M(i % 2 == 0 ? "Hueso" : "Tela roja"), new Vector3(0, 1f + i * 2f, 0), 1.75f - i * .15f, 1.75f - (i + 1) * .15f, 2f, 9);
            b.Frustum(M("Casco"), new Vector3(0, 9f, 0), 1.5f, 1.5f, .22f, 9);
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; b.Cylinder(M("Hierro pirata"), new Vector3(Mathf.Cos(a) * 1.4f, 9.2f, Mathf.Sin(a) * 1.4f), .04f, .7f, 4); }
            b.Torus(M("Hierro pirata"), new Vector3(0, 9.9f, 0), 1.4f, .05f, 12, 4);
            b.Frustum(M("Farol"), new Vector3(0, 9.2f, 0), .85f, .85f, 1.4f, 9);
            b.Ball(M("Fuego"), new Vector3(0, 9.9f, 0), Vector3.one * .5f);
            b.Frustum(M("Tela roja"), new Vector3(0, 10.6f, 0), 1.1f, .12f, 1.2f, 9);
            b.Ball(M("Latón"), new Vector3(0, 11.9f, 0), Vector3.one * .16f, 0);
            b.Bevel(M("Casco"), new Vector3(0, 1f, -1.95f), new Vector3(1f, 1.8f, .2f), .05f);
            Save(C, "Faro pirata", b, g => Capsule(g, new Vector3(0, 5f, 0), 2.1f, 10f));
        }
        // Rowboat (floats, no collision needed far away).
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Casco"), new Vector3(0, .15f, 0), new Vector3(1.1f, .3f, 2.8f), .1f);
            foreach (var s in new[] { -1, 1 }) b.Bevel(M("Cubierta"), new Vector3(s * .62f, .42f, 0), new Vector3(.12f, .5f, 2.6f), .04f, new Vector3(0, 0, s * -14));
            b.Hip(M("Casco"), new Vector3(0, .05f, 1.6f), 1.2f, .9f, .62f, .9f, new Vector3(-90, 0, 0));
            for (int i = 0; i < 2; i++) b.Box(M("Cubierta"), new Vector3(0, .5f, -.6f + i * 1f), new Vector3(1.2f, .08f, .3f));
            foreach (var s in new[] { -1, 1 })
            {
                b.Cylinder(M("Cuerda"), new Vector3(s * .7f, .6f, -.1f), .035f, 2.2f, 4, new Vector3(0, 0, s * 75));
                b.Bevel(M("Cuerda"), new Vector3(s * 2.65f, .05f, -.1f), new Vector3(.4f, .05f, .18f), .02f, new Vector3(0, 0, s * -15));
            }
            Save(C, "Bote de remos", b, isStatic: false);
        }
        // Buoy with a tiny lantern.
        {
            var b = new LowPolyBuilder();
            b.Frustum(M("Tela roja"), new Vector3(0, -.3f, 0), .55f, .45f, .6f, 8);
            b.Frustum(M("Hueso"), new Vector3(0, .3f, 0), .45f, .2f, .5f, 8);
            b.Cylinder(M("Hierro pirata"), new Vector3(0, .8f, 0), .04f, .6f, 4);
            b.Bevel(M("Fuego"), new Vector3(0, 1.45f, 0), new Vector3(.18f, .22f, .18f), .03f);
            Save(C, "Boya", b, isStatic: false);
        }
        // Tiki torch.
        {
            var b = new LowPolyBuilder();
            b.Frustum(M("Casco"), Vector3.zero, .08f, .06f, 2f, 5);
            b.Torus(M("Cuerda"), new Vector3(0, 1.2f, 0), .075f, .03f, 6, 3);
            b.Frustum(M("Paja"), new Vector3(0, 1.95f, 0), .1f, .2f, .35f, 6);
            b.Cone(M("Fuego"), new Vector3(0, 2.3f, 0), .16f, .45f, 5);
            b.Cone(M("Luz cálida"), new Vector3(0, 2.3f, 0), .09f, .3f, 5, new Vector3(0, 36, 0));
            Save(C, "Antorcha tiki", b, g => Capsule(g, new Vector3(0, 1.2f, 0), .12f, 2.4f));
        }
        // Treasure pile: original chest model + coins + a few gems from the pirate pack.
        {
            var b = new LowPolyBuilder();
            for (int i = 0; i < 9; i++) { float a = i * 2.4f, r = .25f + (i % 3) * .22f; b.Frustum(M("Latón"), new Vector3(Mathf.Cos(a) * r, (i % 4) * .03f, Mathf.Sin(a) * r - .35f), .12f, .12f, .03f, 8); }
            b.Ball(M("Latón"), new Vector3(0, 0, -.3f), new Vector3(.55f, .18f, .4f), 0);
            Save(C, "Tesoro", b, g =>
            {
                Model(g, "Prop_Chest_Gold", new Vector3(0, 0, .25f), 15, .9f, M("Atlas pirata"));
                Model(g, "Prop_GoldBag", new Vector3(.75f, 0, -.1f), -30, .55f, M("Atlas pirata"));
                BoxCol(g, new Vector3(0, .45f, .1f), new Vector3(1.6f, .9f, 1.3f));
            });
        }
        // Pier section with pilings, bollards and a lantern — built to sit on the islands' shore.
        {
            var b = new LowPolyBuilder();
            for (int i = 0; i < 9; i++) b.Box(M("Cubierta"), new Vector3(0, .9f, -3.6f + i * .9f), new Vector3(2.8f, .12f, .82f), new Vector3(0, (i % 3 - 1) * 1.2f, 0));
            foreach (var x in new[] { -1.3f, 1.3f }) for (int i = 0; i < 4; i++) b.Cylinder(M("Casco"), new Vector3(x, -2f, -3.6f + i * 2.4f), .16f, 3.2f, 6);
            foreach (var x in new[] { -1.3f, 1.3f }) b.Box(M("Casco"), new Vector3(x, .78f, 0), new Vector3(.16f, .12f, 8.2f));
            foreach (var z in new[] { -3.4f, 3.4f }) { b.Cylinder(M("Hierro pirata"), new Vector3(1.1f, .96f, z), .12f, .4f, 6); b.Ball(M("Hierro pirata"), new Vector3(1.1f, 1.38f, z), Vector3.one * .15f, 0); }
            b.Box(M("Casco"), new Vector3(-1.2f, 1.9f, 3.6f), new Vector3(.14f, 2f, .14f));
            b.Box(M("Casco"), new Vector3(-1f, 2.85f, 3.6f), new Vector3(.5f, .1f, .1f));
            b.Bevel(M("Farol"), new Vector3(-.8f, 2.55f, 3.6f), new Vector3(.25f, .34f, .25f), .04f);
            b.Torus(M("Tela roja"), new Vector3(1.45f, 1.4f, -1f), .32f, .09f, 12, 4, new Vector3(0, 0, 90), M("Hueso"));
            Save(C, "Muelle pirata", b, g => BoxCol(g, new Vector3(0, .9f, 0), new Vector3(2.9f, .3f, 8.2f)));
        }
    }

    // ================================================================ CARRERAS
    static void Race()
    {
        const string C = "Carreras";
        // Grandstand: four seat tiers in team colors, back wall, roof on posts, flags.
        {
            var b = new LowPolyBuilder();
            string[] seat = { "Tela roja", "Tela amarilla", "Tela turquesa", "Tela rosa" };
            for (int i = 0; i < 4; i++)
            {
                b.Bevel(M("Hormigón"), new Vector3(0, .35f + i * .55f, -1.6f + i * .8f), new Vector3(12f, .7f + i * 1.1f, .8f), .04f, default);
                for (int k = 0; k < 12; k++) b.Bevel(M(seat[(i + k / 3) % 4]), new Vector3(-5.5f + k, .77f + i * 1.1f, -1.75f + i * .8f), new Vector3(.75f, .14f, .45f), .04f);
            }
            b.Bevel(M("Hormigón"), new Vector3(0, 2.6f, 1.75f), new Vector3(12.2f, 5.2f, .3f), .05f);
            foreach (var x in new[] { -5.9f, -2f, 2f, 5.9f }) b.Bevel(M("Hierro"), new Vector3(x, 3.4f, -2.1f), new Vector3(.2f, 6.8f, .2f), .04f);
            b.Bevel(M("Tejado azul"), new Vector3(0, 6.6f, -.2f), new Vector3(13f, .25f, 4.6f), .08f, new Vector3(-6, 0, 0));
            b.Box(M("Tela amarilla"), new Vector3(0, 6.4f, -2.45f), new Vector3(13f, .45f, .06f), new Vector3(-6, 0, 0));
            for (int i = 0; i < 6; i++)
            {
                float x = -5.5f + i * 2.2f;
                b.Cylinder(M("Blanco"), new Vector3(x, 6.7f, 1.6f), .04f, 1.6f, 4);
                b.Plate(M(seat[i % 4]), new Vector3(x + .3f, 8f, 1.6f), new Vector2(.6f, .4f));
            }
            Save(C, "Grada", b, g => { BoxCol(g, new Vector3(0, 2.6f, 0), new Vector3(12.4f, 5.2f, 3.8f)); Label(g, "COSTA DEL CAOS", new Vector3(0, 6.4f, -2.5f), new Vector3(-6, 0, 0), .07f, new Color(.2f, .1f, .35f)); });
        }
        // Tire stack.
        {
            var b = new LowPolyBuilder();
            for (int s = 0; s < 3; s++)
                for (int i = 0; i < 4; i++)
                {
                    float y = .15f + i * .3f;
                    Vector3 p = new Vector3((s - 1) * .85f, y + (s == 1 ? 0 : 0), 0);
                    if (s != 1 && i == 3) continue;
                    b.Torus(M((i + s) % 2 == 0 ? "Tela roja" : "Blanco"), p, .3f, .14f, 10, 4, new Vector3(0, i * 18, 0));
                    b.Frustum(M("Goma"), p + Vector3.down * .13f, .17f, .17f, .26f, 8);
                }
            Save(C, "Pila de neumáticos", b, g => BoxCol(g, new Vector3(0, .6f, 0), new Vector3(2.6f, 1.2f, .9f)));
        }
        // Billboard with two legs, framed panel and a label.
        foreach (var (key, panel, stripe, text) in new[] { ("Turbo", "Tela rosa", "Tela amarilla", "¡TURBO\nTOTAL!"), ("Helados", "Tela turquesa", "Blanco", "HELADOS\nCOSTA"), ("Derrape", "Tela amarilla", "Tela roja", "¡DERRAPA!") })
        {
            var b = new LowPolyBuilder();
            foreach (var x in new[] { -2.2f, 2.2f }) b.Bevel(M("Hierro"), new Vector3(x, 2f, .1f), new Vector3(.25f, 4f, .25f), .04f);
            b.Bevel(M("Blanco"), new Vector3(0, 4.3f, 0), new Vector3(6.2f, 2.8f, .25f), .08f);
            b.Bevel(M(panel), new Vector3(0, 4.3f, -.1f), new Vector3(5.8f, 2.4f, .1f), .04f);
            for (int i = 0; i < 5; i++) b.Box(M(stripe), new Vector3(-2.4f + i * 1.2f, 3.3f, -.16f), new Vector3(.6f, .25f, .02f));
            for (int i = 0; i < 3; i++) b.Bevel(M("Luz cálida"), new Vector3(-2f + i * 2f, 5.85f, -.35f), new Vector3(.3f, .14f, .3f), .03f);
            Save(C, "Valla publicitaria " + key, b, g => { BoxCol(g, new Vector3(0, 3f, 0), new Vector3(6.2f, 6f, .4f)); Label(g, text, new Vector3(0, 4.45f, -.2f), Vector3.zero, .085f, Color.white); });
        }
        // Race-control tower with glass cab and checkered flag.
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Hormigón"), new Vector3(0, 2.2f, 0), new Vector3(3f, 4.4f, 3f), .08f);
            for (int i = 0; i < 3; i++) b.Box(M("Ventana noche"), new Vector3(0, 1.2f + i * 1.2f, -1.52f), new Vector3(1.8f, .6f, .05f));
            b.Bevel(M("Tela roja"), new Vector3(0, 4.55f, 0), new Vector3(3.6f, .3f, 3.6f), .06f);
            b.Bevel(M("Cristal"), new Vector3(0, 5.5f, 0), new Vector3(3.2f, 1.6f, 3.2f), .05f);
            foreach (var x in new[] { -1.6f, 1.6f }) foreach (var z in new[] { -1.6f, 1.6f }) b.Box(M("Blanco"), new Vector3(x, 5.5f, z), new Vector3(.14f, 1.65f, .14f));
            b.Hip(M("Tejado azul"), new Vector3(0, 6.3f, 0), 4f, 4f, .7f, 0f);
            b.Cylinder(M("Hierro"), new Vector3(0, 7f, 0), .04f, 1.8f, 4);
            for (int x = 0; x < 4; x++) for (int y = 0; y < 3; y++) b.Box(M((x + y) % 2 == 0 ? "Negro suave" : "Blanco"), new Vector3(.12f + x * .2f, 8.5f - y * .2f, 0), new Vector3(.2f, .2f, .02f));
            Save(C, "Torre de control", b, g => BoxCol(g, new Vector3(0, 3.2f, 0), new Vector3(3.6f, 6.4f, 3.6f)));
        }
        // Lifeguard tower for the coast.
        {
            var b = new LowPolyBuilder();
            foreach (var x in new[] { -.9f, .9f }) foreach (var z in new[] { -.9f, .9f }) b.Box(M("Blanco"), new Vector3(x, 1.2f, z), new Vector3(.14f, 2.4f, .14f));
            b.Box(M("Madera clara"), new Vector3(0, 2.45f, 0), new Vector3(2.4f, .15f, 2.4f));
            b.Bevel(M("Tejado turquesa"), new Vector3(0, 3.25f, .2f), new Vector3(2f, 1.5f, 1.6f), .05f);
            b.Box(M("Ventana noche"), new Vector3(0, 3.35f, -.62f), new Vector3(1.4f, .6f, .04f));
            b.Gable(M("Tela roja"), new Vector3(0, 4f, .2f), 2.4f, 2.1f, .7f, default, .1f);
            for (int i = 0; i < 7; i++) b.Box(M("Madera clara"), new Vector3(0, .25f + i * .32f, -1.6f + i * .12f), new Vector3(.9f, .06f, .3f));
            b.Torus(M("Tela roja"), new Vector3(1.05f, 3.2f, .2f), .32f, .08f, 12, 4, new Vector3(0, 0, 90), M("Blanco"));
            b.Cylinder(M("Blanco"), new Vector3(-.9f, 4.4f, -.7f), .03f, 1.4f, 4);
            b.Plate(M("Tela amarilla"), new Vector3(-.6f, 5.5f, -.7f), new Vector2(.6f, .4f));
            Save(C, "Torre socorrista", b, g => BoxCol(g, new Vector3(0, 2.2f, 0), new Vector3(2.4f, 4.4f, 2.4f)));
        }
        // Beach umbrella with striped canopy + towel.
        foreach (var (key, a, bb) in new[] { ("Roja", "Tela roja", "Blanco"), ("Turquesa", "Tela turquesa", "Tela amarilla") })
        {
            var b = new LowPolyBuilder();
            b.Cylinder(M("Blanco"), new Vector3(0, 0, 0), .04f, 2.4f, 4, new Vector3(0, 0, 8));
            Vector3 apex = new Vector3(-.33f, 2.55f, 0);
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * Mathf.PI / 4, a1 = (i + 1) * Mathf.PI / 4;
                Vector3 p0 = apex + new Vector3(Mathf.Cos(a0) * 1.5f, -.5f, Mathf.Sin(a0) * 1.5f), p1 = apex + new Vector3(Mathf.Cos(a1) * 1.5f, -.5f, Mathf.Sin(a1) * 1.5f);
                b.Face(M(i % 2 == 0 ? a : bb), apex + Vector3.down * .4f, apex, p0, p1);
                b.Face(M(i % 2 == 0 ? a : bb), apex + Vector3.up * .4f, apex + Vector3.down * .02f, p0 + Vector3.down * .02f, p1 + Vector3.down * .02f);
            }
            b.Box(M(bb == "Blanco" ? "Tela amarilla" : "Tela rosa"), new Vector3(1.2f, .02f, .2f), new Vector3(.9f, .03f, 1.8f), new Vector3(0, 15, 0));
            Save(C, "Sombrilla " + key, b, g => Capsule(g, new Vector3(0, 1.2f, 0), .1f, 2.4f));
        }
        // Pit garage with three roll-up doors.
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Hormigón"), new Vector3(0, 2f, 0), new Vector3(11f, 4f, 5f), .1f);
            string[] door = { "Tela roja", "Tela amarilla", "Tela turquesa" };
            for (int i = 0; i < 3; i++)
            {
                b.Box(M("Negro suave"), new Vector3(-3.6f + i * 3.6f, 1.55f, -2.52f), new Vector3(3f, 3.1f, .05f));
                for (int k = 0; k < 6; k++) b.Box(M(door[i]), new Vector3(-3.6f + i * 3.6f, .3f + k * .5f, -2.56f), new Vector3(2.9f, .42f, .04f));
            }
            b.Bevel(M("Tela roja"), new Vector3(0, 4.2f, -.2f), new Vector3(11.6f, .4f, 5.8f), .06f);
            b.Bevel(M("Blanco"), new Vector3(0, 4.9f, -2.4f), new Vector3(5f, 1f, .2f), .05f);
            Save(C, "Boxes", b, g => { BoxCol(g, new Vector3(0, 2.2f, 0), new Vector3(11.6f, 4.4f, 5.8f)); Label(g, "BOXES", new Vector3(0, 4.9f, -2.52f), Vector3.zero, .08f, new Color(.85f, .1f, .3f)); });
        }
        // Checkered flag pole.
        {
            var b = new LowPolyBuilder();
            b.Frustum(M("Piedra oscura"), Vector3.zero, .3f, .22f, .3f, 6);
            b.Cylinder(M("Blanco"), new Vector3(0, .3f, 0), .05f, 4.4f, 6);
            for (int x = 0; x < 5; x++) for (int y = 0; y < 4; y++)
                b.Box(M((x + y) % 2 == 0 ? "Negro suave" : "Blanco"), new Vector3(.13f + x * .24f, 4.45f - y * .24f, Mathf.Sin(x * 1.1f) * .05f), new Vector3(.24f, .24f, .02f), new Vector3(0, Mathf.Cos(x * 1.1f) * 14, 0));
            Save(C, "Bandera a cuadros", b, g => Capsule(g, new Vector3(0, 2.4f, 0), .12f, 4.8f));
        }
        // Beach ice-cream cart.
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Blanco"), new Vector3(0, .85f, 0), new Vector3(1.8f, .9f, 1f), .1f);
            b.Box(M("Tela rosa"), new Vector3(0, .7f, -.51f), new Vector3(1.6f, .3f, .02f));
            foreach (var x in new[] { -.6f, .6f }) { b.Torus(M("Goma"), new Vector3(x, .32f, -.52f), .22f, .08f, 10, 4, new Vector3(90, 0, 0)); b.Torus(M("Goma"), new Vector3(x, .32f, .52f), .22f, .08f, 10, 4, new Vector3(90, 0, 0)); }
            b.Cylinder(M("Blanco"), new Vector3(0, 1.3f, 0), .035f, 1.3f, 4);
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * Mathf.PI / 4, a1 = (i + 1) * Mathf.PI / 4; Vector3 ap = new(0, 2.85f, 0);
                b.Face(M(i % 2 == 0 ? "Tela rosa" : "Blanco"), ap + Vector3.down * .3f, ap, ap + new Vector3(Mathf.Cos(a0) * 1.2f, -.4f, Mathf.Sin(a0) * 1.2f), ap + new Vector3(Mathf.Cos(a1) * 1.2f, -.4f, Mathf.Sin(a1) * 1.2f));
            }
            for (int i = 0; i < 3; i++) { b.Cone(M("Madera clara"), new Vector3(-.5f + i * .5f, 1.6f, 0), .1f, -.3f, 5); b.Ball(M(i == 0 ? "Flor rosa" : i == 1 ? "Crema" : "Tela turquesa"), new Vector3(-.5f + i * .5f, 1.65f, 0), Vector3.one * .13f, 0); }
            Save(C, "Carrito de helados", b, g => BoxCol(g, new Vector3(0, .8f, 0), new Vector3(1.9f, 1.6f, 1.2f)));
        }
    }

    // ================================================================ CÓDIGO MORTAL
    static void Mortal()
    {
        const string C = "CodigoMortal";
        // Skyline buildings: lit window grids, rooftop machinery, antenna and a neon board.
        var variants = new[] { ("A", "Azul noche", "Ventana cálida", 6, 4, "Neón rosa"), ("B", "Menta", "Ventana noche", 8, 3, "Neón ámbar"), ("C", "Coral", "Ventana cálida", 5, 5, "Neón cian"), ("D", "Marfil", "Ventana noche", 10, 3, "Neón rosa") };
        foreach (var (key, wall, window, floors, cols, neon) in variants)
        {
            var b = new LowPolyBuilder();
            float fw = 2.4f, fh = 3f, w = cols * fw + 1.2f, h = floors * fh + 1f;
            b.Bevel(M(wall), new Vector3(0, h * .5f, 0), new Vector3(w, h, w), .25f);
            b.Bevel(M("Azul noche"), new Vector3(0, .6f, 0), new Vector3(w + .3f, 1.2f, w + .3f), .1f);
            b.Bevel(M("Hormigón"), new Vector3(0, h + .2f, 0), new Vector3(w + .4f, .4f, w + .4f), .1f);
            foreach (var r in new[] { 0, 90, 180, 270 })
                using (b.At(Vector3.zero, new Vector3(0, r, 0)))
                    for (int f = 0; f < floors; f++)
                    {
                        b.Box(M("Hormigón"), new Vector3(0, 1.6f + f * fh, -w * .5f - .05f), new Vector3(w - .2f, .14f, .14f));
                        for (int c = 0; c < cols; c++)
                        {
                            bool lit = ((f * 7 + c * 3 + r) % 5) != 0;
                            b.Box(M(lit ? window : "Negro suave"), new Vector3(-w * .5f + .6f + fw * (c + .5f), 2.6f + f * fh, -w * .5f - .03f), new Vector3(1.4f, 1.5f, .06f));
                        }
                    }
            // Rooftop: AC units, water tank on legs, antenna with beacon.
            b.Bevel(M("Hormigón"), new Vector3(-w * .25f, h + .9f, -w * .2f), new Vector3(1.6f, 1f, 1.2f), .08f);
            b.Cylinder(M("Hierro"), new Vector3(-w * .25f, h + 1.41f, -w * .2f), .4f, .05f, 8);
            b.Bevel(M("Hormigón"), new Vector3(w * .05f, h + .8f, -w * .28f), new Vector3(1.2f, .8f, 1f), .08f);
            foreach (var x in new[] { -.6f, .6f }) foreach (var z in new[] { -.6f, .6f }) b.Box(M("Hierro"), new Vector3(w * .25f + x, h + 1.2f, w * .2f + z), new Vector3(.1f, 2f, .1f));
            b.Cylinder(M("Madera oscura"), new Vector3(w * .25f, h + 2.2f, w * .2f), 1f, 1.6f, 10);
            b.Cone(M("Madera oscura"), new Vector3(w * .25f, h + 3.8f, w * .2f), 1.1f, .6f, 10);
            b.Frustum(M("Hierro"), new Vector3(-w * .3f, h + .4f, w * .3f), .12f, .03f, 5f, 4);
            b.Ball(M("Neón rosa"), new Vector3(-w * .3f, h + 5.5f, w * .3f), Vector3.one * .18f, 0);
            // Roof neon board facing the plaza (-Z).
            b.Box(M("Hierro"), new Vector3(0, h + 1.8f, -w * .45f), new Vector3(w * .7f, 2.6f, .18f));
            b.Box(M(neon), new Vector3(0, h + 1.8f, -w * .45f - .1f), new Vector3(w * .7f - .3f, .12f, .04f));
            b.Box(M(neon), new Vector3(0, h + 3f, -w * .45f - .1f), new Vector3(w * .7f - .3f, .12f, .04f));
            foreach (var x in new[] { -1, 1 }) b.Box(M(neon), new Vector3(x * (w * .35f - .2f), h + 2.4f, -w * .45f - .1f), new Vector3(.12f, 1.3f, .04f));
            string[] words = { "VÉRTIGO", "CÓDIGO", "PLAZA", "4 CIFRAS" };
            Save(C, "Edificio " + key, b, g => { BoxCol(g, new Vector3(0, h * .5f, 0), new Vector3(w, h, w)); Label(g, words["ABCD".IndexOf(key)], new Vector3(0, h + 2.4f, -w * .45f - .14f), Vector3.zero, .09f, Color.white); });
        }
        // Modern street lamp (curved head, cool light).
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Azul noche"), new Vector3(0, .25f, 0), new Vector3(.5f, .5f, .5f), .08f);
            b.Frustum(M("Hierro"), new Vector3(0, .5f, 0), .09f, .06f, 4.2f, 6);
            b.Bevel(M("Hierro"), new Vector3(0, 4.8f, -.45f), new Vector3(.14f, .14f, 1f), .04f, new Vector3(-14, 0, 0));
            b.Bevel(M("Hierro"), new Vector3(0, 4.62f, -1f), new Vector3(.4f, .14f, .7f), .05f);
            b.Box(M("Neón cian"), new Vector3(0, 4.53f, -1f), new Vector3(.3f, .03f, .6f));
            Save(C, "Farola moderna", b, g => Capsule(g, new Vector3(0, 2.4f, 0), .25f, 4.8f));
        }
        // Concrete bench with wooden top.
        {
            var b = new LowPolyBuilder();
            foreach (var x in new[] { -.75f, .75f }) b.Bevel(M("Hormigón"), new Vector3(x, .22f, 0), new Vector3(.35f, .44f, .55f), .06f);
            b.Bevel(M("Madera clara"), new Vector3(0, .5f, 0), new Vector3(2f, .12f, .6f), .04f);
            Save(C, "Banco moderno", b, g => BoxCol(g, new Vector3(0, .3f, 0), new Vector3(2f, .6f, .6f)));
        }
        // Kiosk dressing kit (no colliders): striped awning + framed sign board + roof trim, sized for 6 m kiosks.
        foreach (var (key, a, bb) in new[] { ("Coral", "Coral", "Marfil"), ("Menta", "Menta", "Marfil") })
        {
            var b = new LowPolyBuilder();
            b.Awning(M(a), M(bb), new Vector3(0, 3.2f, -2.3f), 6.2f, 1.2f, .45f, 9);
            b.Bevel(M("Ámbar"), new Vector3(0, 3.62f, -2.36f), new Vector3(6.7f, .18f, .12f), .04f);
            b.Bevel(M("Azul noche"), new Vector3(0, 4.25f, -1.6f), new Vector3(3.8f, .9f, .16f), .06f);
            b.Box(M("Neón ámbar"), new Vector3(0, 4.68f, -1.69f), new Vector3(3.6f, .06f, .03f));
            b.Box(M("Neón ámbar"), new Vector3(0, 3.82f, -1.69f), new Vector3(3.6f, .06f, .03f));
            for (int i = 0; i < 3; i++) b.Bevel(M("Hormigón"), new Vector3(-1.8f + i * 1.8f, 3.85f, 1.2f), new Vector3(1f, .55f, .8f), .06f);
            b.Cylinder(M("Hierro"), new Vector3(-1.8f, 4.13f, 1.2f), .32f, .04f, 8);
            Save(C, "Kit kiosco " + key, b, g => Label(g, "KIOSCO", new Vector3(0, 4.25f, -1.7f), Vector3.zero, .055f, Color.white));
        }
        // Giant screen for the observatory backboard.
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Azul noche"), Vector3.zero, new Vector3(12f, 4.2f, .4f), .12f);
            b.Box(M("Ventana noche"), new Vector3(0, 0, -.21f), new Vector3(11.2f, 3.5f, .04f));
            foreach (var y in new[] { -1.9f, 1.9f }) b.Box(M("Neón cian"), new Vector3(0, y, -.23f), new Vector3(11.6f, .1f, .04f));
            foreach (var x in new[] { -5.85f, 5.85f }) b.Box(M("Neón rosa"), new Vector3(x, 0, -.23f), new Vector3(.1f, 3.9f, .04f));
            Save(C, "Pantalla gigante", b, g => Label(g, "CÓDIGO MORTAL\n· · · ·", new Vector3(0, .1f, -.26f), Vector3.zero, .16f, new Color(.4f, 1f, .9f)));
        }
        // Modern planter with a cube hedge and flowers.
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Hormigón"), new Vector3(0, .35f, 0), new Vector3(1.2f, .7f, 1.2f), .08f);
            b.Bevel(M("Menta"), new Vector3(0, .95f, 0), new Vector3(1f, .55f, 1f), .2f);
            for (int i = 0; i < 4; i++) b.Ball(M("Flor rosa"), new Vector3((i % 2 - .5f) * .6f, 1.25f, (i / 2 - .5f) * .6f), Vector3.one * .1f, 0);
            Save(C, "Maceta moderna", b, g => BoxCol(g, new Vector3(0, .6f, 0), new Vector3(1.2f, 1.2f, 1.2f)));
        }
        // Neon arrow sign (wall-mounted, no collision).
        {
            var b = new LowPolyBuilder();
            b.Bevel(M("Azul noche"), Vector3.zero, new Vector3(2.4f, 1f, .14f), .06f);
            b.Box(M("Neón rosa"), new Vector3(-.2f, 0, -.09f), new Vector3(1.4f, .14f, .03f));
            b.Box(M("Neón rosa"), new Vector3(.55f, .2f, -.09f), new Vector3(.6f, .14f, .03f), new Vector3(0, 0, -40));
            b.Box(M("Neón rosa"), new Vector3(.55f, -.2f, -.09f), new Vector3(.6f, .14f, .03f), new Vector3(0, 0, 40));
            Save(C, "Cartel neón flecha", b);
        }
    }

    // ================================================================ arena runtime set
    /// <summary>The Arena builds its map at Play; it loads these references from Resources.</summary>
    static void BuildArenaSet()
    {
        Ensure("Assets/_Game/Resources");
        const string path = "Assets/_Game/Resources/ArenaArtSet.asset";
        var set = AssetDatabase.LoadAssetAtPath<MultiverseArtSet>(path);
        if (set == null) { set = ScriptableObject.CreateInstance<MultiverseArtSet>(); AssetDatabase.CreateAsset(set, path); }
        GameObject P(string n) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Piratas/{n}.prefab");
        GameObject F(string n) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/002_Modelos/piratas/FBX/" + n + ".fbx");
        set.entries = new[]
        {
            new MultiverseArtSet.Entry { key = "flag", prefab = P("Bandera pirata") },
            new MultiverseArtSet.Entry { key = "hut", prefab = P("Choza de playa") },
            new MultiverseArtSet.Entry { key = "tower", prefab = P("Torre vigía") },
            new MultiverseArtSet.Entry { key = "lighthouse", prefab = P("Faro pirata") },
            new MultiverseArtSet.Entry { key = "rowboat", prefab = P("Bote de remos") },
            new MultiverseArtSet.Entry { key = "buoy", prefab = P("Boya") },
            new MultiverseArtSet.Entry { key = "torch", prefab = P("Antorcha tiki") },
            new MultiverseArtSet.Entry { key = "treasure", prefab = P("Tesoro") },
            new MultiverseArtSet.Entry { key = "pier", prefab = P("Muelle pirata") },
            new MultiverseArtSet.Entry { key = "shipSmall", prefab = F("Ship_Small"), atlas = true, height = 7f },
            new MultiverseArtSet.Entry { key = "house1", prefab = F("Environment_House1"), atlas = true, height = 5f },
            new MultiverseArtSet.Entry { key = "house3", prefab = F("Environment_House3"), atlas = true, height = 5.5f },
            new MultiverseArtSet.Entry { key = "cliff", prefab = F("Environment_Cliff2"), atlas = true, height = 7f },
            new MultiverseArtSet.Entry { key = "dockPole", prefab = F("Environment_Dock_Pole"), atlas = true, height = 2.2f },
            new MultiverseArtSet.Entry { key = "skulls", prefab = F("Environment_Skulls"), atlas = true, height = .8f },
        };
        set.atlas = M("Atlas pirata");
        EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
    }
}
