using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime building helpers for the escape tower: palette, soft bevelled blocks, primitives, 3D text and props.</summary>
public static class EscapeKit
{
    static EscapeArtSet art;
    static bool artLoaded;
    static readonly Dictionary<string, Material> mats = new();
    static readonly Dictionary<string, Mesh> meshes = new();

    public static EscapeArtSet Art
    {
        get { if (!artLoaded) { art = Resources.Load<EscapeArtSet>("EscapeArtSet"); artLoaded = true; } return art; }
    }

    static Shader lit;
    public static Shader Lit => lit != null ? lit : (lit = Shader.Find("Universal Render Pipeline/Lit"));

    /// <summary>Shared palette material by name (art set), or a flat colour fallback. Never modify the result.</summary>
    public static Material Mat(string key, Color fallback)
    {
        if (mats.TryGetValue("p" + key, out var m) && m != null) return m;
        m = Art != null ? Art.FindMaterial(key) : null;
        if (m == null) m = Paint(fallback);
        mats["p" + key] = m;
        return m;
    }

    /// <summary>Shared flat colour material, cached by colour. Never modify the result.</summary>
    public static Material Paint(Color c, float smooth = .18f)
    {
        string id = "c" + ColorUtility.ToHtmlStringRGBA(c) + smooth.ToString("0.00");
        if (mats.TryGetValue(id, out var m) && m != null) return m;
        m = new Material(Lit) { name = "Escape " + ColorUtility.ToHtmlStringRGB(c), enableInstancing = true };
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
        mats[id] = m;
        return m;
    }

    /// <summary>A new emissive material that the caller owns and may animate with SetGlow.</summary>
    public static Material Glow(Color c, float intensity)
    {
        var m = new Material(Lit) { name = "Escape brillo" };
        m.SetFloat("_Smoothness", .55f); m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        SetGlow(m, c, intensity);
        return m;
    }

    public static void SetGlow(Material m, Color c, float intensity)
    {
        if (m == null) return;
        m.SetColor("_BaseColor", Color.Lerp(c * .55f, c, Mathf.Clamp01(intensity)));
        m.SetColor("_EmissionColor", c * intensity);
    }

    /// <summary>A soft bevelled block (cartoon look) with an optional box collider of the same size.</summary>
    public static GameObject Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, float bevel = .06f, bool solid = true, Vector3 euler = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        float limit = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * .45f;
        go.AddComponent<MeshFilter>().sharedMesh = Beveled(size, Mathf.Min(bevel, limit));
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (solid) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    public static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool solid = true, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!solid)
        {
            var col = go.GetComponent<Collider>();
            col.enabled = false;
            Object.Destroy(col);
        }
        return go;
    }

    public static TextMesh Text(Transform parent, string text, Vector3 pos, Vector3 euler, float size, Color color)
    {
        var go = new GameObject("Texto · " + text.Replace("\n", " "), typeof(TextMesh));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        var tm = go.GetComponent<TextMesh>();
        var a = Art;
        if (a != null && a.font != null)
        {
            tm.font = a.font;
            go.GetComponent<MeshRenderer>().sharedMaterial = a.textMaterial != null ? a.textMaterial : a.font.material;
        }
        tm.text = text; tm.fontSize = 96; tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = color;
        return tm;
    }

    public static Light PointLight(Transform parent, Vector3 pos, Color c, float range, float intensity)
    {
        var go = new GameObject("Luz");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.range = range; l.intensity = intensity; l.shadows = LightShadows.None;
        return l;
    }

    /// <summary>Instantiates a decorative prefab from the art set; null if the key is missing.</summary>
    public static GameObject Prop(string key, Transform parent, Vector3 pos, float yaw, float scale = 1f)
    {
        var prefab = Art != null ? Art.FindPrefab(key) : null;
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, parent, false);
        go.name = prefab.name;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = prefab.transform.localScale * scale;
        return go;
    }

    // ------------------------------------------------------------------ meshes

    /// <summary>Chamfered box: 6 faces, 12 edge bevels and 8 corner triangles, flat shaded.</summary>
    static Mesh Beveled(Vector3 size, float b)
    {
        string key = $"b{size.x:0.###}/{size.y:0.###}/{size.z:0.###}/{b:0.###}";
        if (meshes.TryGetValue(key, out var cached) && cached != null) return cached;
        Vector3 h = size * .5f;
        b = Mathf.Max(b, .004f);
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();

        Vector3 P(int axis, int sx, int sy, int sz)
        {
            var p = new Vector3(sx * (h.x - b), sy * (h.y - b), sz * (h.z - b));
            if (axis == 0) p.x = sx * h.x; else if (axis == 1) p.y = sy * h.y; else p.z = sz * h.z;
            return p;
        }

        void Poly(params Vector3[] pts)
        {
            Vector3 c = Vector3.zero;
            foreach (var p in pts) c += p;
            c /= pts.Length;
            Vector3 normal = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]);
            if (normal.sqrMagnitude < 1e-12f && pts.Length > 3) normal = Vector3.Cross(pts[2] - pts[0], pts[3] - pts[0]);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal.Normalize();
            bool flip = Vector3.Dot(normal, c) < 0f;
            if (flip) normal = -normal;
            int start = v.Count;
            foreach (var p in pts) { v.Add(p); n.Add(normal); }
            for (int i = 1; i < pts.Length - 1; i++)
            {
                if (!flip) { t.Add(start); t.Add(start + i); t.Add(start + i + 1); }
                else { t.Add(start); t.Add(start + i + 1); t.Add(start + i); }
            }
        }

        for (int s = -1; s <= 1; s += 2)
        {
            Poly(P(0, s, -1, -1), P(0, s, 1, -1), P(0, s, 1, 1), P(0, s, -1, 1));
            Poly(P(1, -1, s, -1), P(1, 1, s, -1), P(1, 1, s, 1), P(1, -1, s, 1));
            Poly(P(2, -1, -1, s), P(2, 1, -1, s), P(2, 1, 1, s), P(2, -1, 1, s));
        }
        for (int a = -1; a <= 1; a += 2)
            for (int c = -1; c <= 1; c += 2)
            {
                Poly(P(0, a, c, -1), P(0, a, c, 1), P(1, a, c, 1), P(1, a, c, -1));
                Poly(P(0, a, -1, c), P(0, a, 1, c), P(2, a, 1, c), P(2, a, -1, c));
                Poly(P(1, -1, a, c), P(1, 1, a, c), P(2, 1, a, c), P(2, -1, a, c));
            }
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Poly(P(0, sx, sy, sz), P(1, sx, sy, sz), P(2, sx, sy, sz));

        var mesh = new Mesh { name = "Bloque suave" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
        mesh.RecalculateBounds();
        meshes[key] = mesh;
        return mesh;
    }
}

/// <summary>Marks the static room shell (walls/floors) so the camera can see over it instead of zooming in.</summary>
public sealed class EscapeShell : MonoBehaviour { }

/// <summary>Slow decorative spin/bob for floating crystals and portal rings.</summary>
public sealed class EscapeSpin : MonoBehaviour
{
    public Vector3 axis = Vector3.up;
    public float speed = 20f, bob, bobSpeed = 1f;
    Vector3 start; float phase;
    void Start() { start = transform.localPosition; phase = Random.value * 10f; }
    void Update()
    {
        transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
        if (bob > 0f) transform.localPosition = start + Vector3.up * Mathf.Sin((Time.time + phase) * bobSpeed) * bob;
    }
}
