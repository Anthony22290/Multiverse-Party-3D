using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Estilo "juguete" para la Sala de Escape SIN tocar su código: va en la escena copia (Pruebas/SalaDeEscape_Juguete).
/// 1. Antes de que el director construya la torre, cambia el Art Set de EscapeKit por el de juguete (props y paleta nuevos).
/// 2. Con la torre ya construida, cambia cada bloque recto por uno redondeado (misma medida y colisión) y los colores planos
///    por materiales con degradado.
/// 3. Cambia los piratas por Fiesteros con disfraz al azar (el Animator usa los mismos estados Idle/Walk).
/// Al salir de la escena restaura EscapeKit para que el juego normal siga igual.
/// </summary>
[DefaultExecutionOrder(-10000)]
public sealed class EscapeToyStyle : MonoBehaviour
{
    public EscapeArtSet toyArt;
    public GameObject character;
    [Range(.02f, .4f)] public float maxRound = .24f;

    static readonly BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly BindingFlags I = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly HashSet<Object> done = new();
    readonly Dictionary<string, Mesh> meshes = new();
    readonly Dictionary<Material, Material> toyMats = new();
    Texture2D gradient, tiles, bricks;
    readonly Dictionary<(Material, int), Material> surfaceMats = new();
    public int MeshesRounded { get; private set; }
    public int MaterialsSwapped { get; private set; }
    public int ActorsSwapped { get; private set; }

    void Awake()
    {
        var kit = typeof(EscapeKit);
        kit.GetField("art", S)?.SetValue(null, toyArt);
        kit.GetField("artLoaded", S)?.SetValue(null, true);
        ClearKitCaches();
        gradient = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Degradado juguete" };
        var px = new Color[4 * 64];
        for (int y = 0; y < 64; y++) { float k = Mathf.Lerp(.62f, 1f, Mathf.SmoothStep(0, 1, y / 63f)); for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(k, k, k, 1); }
        gradient.SetPixels(px); gradient.Apply(false);
        tiles = Pattern(false); bricks = Pattern(true);
    }

    void OnDestroy()
    {
        var kit = typeof(EscapeKit);
        kit.GetField("art", S)?.SetValue(null, null);
        kit.GetField("artLoaded", S)?.SetValue(null, false);
        ClearKitCaches();
    }

    /// <summary>Textura 128×128 en blanco y gris para teñir: baldosas 2×2 (suelo) o ladrillos alternos (pared), con canto redondeado.</summary>
    static Texture2D Pattern(bool brick)
    {
        const int N = 128; var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4, name = brick ? "Ladrillos juguete" : "Baldosas juguete" };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cw = brick ? 64 : 64, ch = brick ? 32 : 64;
                int row = (int)(y / ch); float ox = brick && row % 2 == 1 ? 32 : 0;
                float lx = ((x + ox) % cw) / cw, ly = (y % ch) / ch;
                float edge = Mathf.Min(Mathf.Min(lx, 1 - lx) * cw, Mathf.Min(ly, 1 - ly) * ch);   // distancia al borde en píxeles
                float k;
                if (edge < 2.2f) k = .66f;                                                          // junta
                else { float bevel = Mathf.Clamp01((edge - 2.2f) / 5f); k = Mathf.Lerp(.8f, 1f, Mathf.SmoothStep(0, 1, bevel)) * (.93f + .07f * ly); }
                if (!brick && ((x / 64 + y / 64) % 2 == 1)) k *= .93f;                               // ajedrez suave
                px[y * N + x] = new Color(k, k, k, 1);
            }
        tex.SetPixels(px); tex.Apply(true); return tex;
    }

    /// <summary>Suelo (kind 1) o pared (kind 2) grande: mismo color con baldosas o ladrillos.</summary>
    Material Surface(Material m, int kind)
    {
        if (m == null || m.IsKeywordEnabled("_EMISSION") || !m.HasProperty("_BaseColor")) return m;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m;
        if (surfaceMats.TryGetValue((m, kind), out var t)) return t;
        var baseToy = Toy(m);
        t = new Material(baseToy) { name = m.name + (kind == 1 ? " (baldosas)" : " (ladrillos)") };
        t.SetTexture("_BaseMap", kind == 1 ? tiles : bricks);
        surfaceMats[(m, kind)] = t; return t;
    }

    static void ClearKitCaches()
    {
        foreach (var name in new[] { "mats", "meshes" })
            (typeof(EscapeKit).GetField(name, S)?.GetValue(null) as System.Collections.IDictionary)?.Clear();
    }

    IEnumerator Start()
    {
        yield return null;
        while (true) { Sweep(); yield return new WaitForSeconds(.5f); }
    }

    // ------------------------------------------------------------------ pasada de estilo
    public void Sweep()
    {
        foreach (var actor in FindObjectsByType<EscapeActor>(FindObjectsSortMode.None))
            if (done.Add(actor)) SwapActor(actor);
        foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!done.Add(r)) continue;
            if (r.GetComponent<TextMesh>() != null || r.GetComponentInParent<CharacterLook>() != null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string n = mf.sharedMesh.name;
                int kind = 0;
                if (n == "Bloque suave" || n == "Cube")
                {
                    Vector3 size = n == "Cube" ? Vector3.one : mf.sharedMesh.bounds.size, scale = n == "Cube" ? Abs(r.transform.lossyScale) : Vector3.one;
                    Vector3 real = Vector3.Scale(size, scale);
                    // Superficies grandes: suelo/techo (plano) o pared (vertical) con textura de baldosas o ladrillos.
                    if (Mathf.Max(real.x, real.z) > 4f && real.y < .9f && real.y < Mathf.Min(real.x, real.z)) kind = 1;
                    else if (real.y > 2.2f && Mathf.Max(real.x, real.z) > 3f && Mathf.Min(real.x, real.z) < 1.2f) kind = 2;
                    mf.sharedMesh = Rounded(size, scale, kind != 0); MeshesRounded++;
                }
                if (kind != 0)
                {
                    var sm = r.sharedMaterials; for (int i = 0; i < sm.Length; i++) sm[i] = Surface(sm[i], kind);
                    r.sharedMaterials = sm; MaterialsSwapped++; continue;
                }
            }
            var mats = r.sharedMaterials; bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var t = Toy(mats[i]); if (t != mats[i]) { mats[i] = t; changed = true; }
            }
            if (changed) { r.sharedMaterials = mats; MaterialsSwapped++; }
        }
    }

    static Vector3 Abs(Vector3 v) => new(Mathf.Max(.001f, Mathf.Abs(v.x)), Mathf.Max(.001f, Mathf.Abs(v.y)), Mathf.Max(.001f, Mathf.Abs(v.z)));

    /// <summary>Colores planos (no emisivos y sin textura) → mismo color con degradado de juguete.</summary>
    Material Toy(Material m)
    {
        if (m == null || m.IsKeywordEnabled("_EMISSION") || !m.HasProperty("_BaseColor")) return m;
        if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) return m;
        if (toyMats.TryGetValue(m, out var t)) return t;
        Color c = m.GetColor("_BaseColor");
        Color.RGBToHSV(c, out float h, out float s, out float v);
        c = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.12f), Mathf.Clamp01(v * 1.12f + .03f)); c.a = m.GetColor("_BaseColor").a;
        t = new Material(m) { name = m.name + " (juguete)" };
        t.SetTexture("_BaseMap", gradient); t.SetColor("_BaseColor", c);
        t.SetFloat("_Smoothness", .1f); t.SetFloat("_Metallic", 0f);
        toyMats[m] = t; return t;
    }

    // ------------------------------------------------------------------ caja redondeada
    /// <summary>Caja de tamaño size*scale con bordes redondeados de radio fijo; se devuelve en el espacio sin escalar.</summary>
    Mesh Rounded(Vector3 size, Vector3 scale, bool planar = false)
    {
        Vector3 real = Vector3.Scale(size, scale);
        float minDim = Mathf.Min(real.x, Mathf.Min(real.y, real.z));
        float r = Mathf.Min(maxRound, minDim * .32f);
        string key = $"{size.x:0.###}/{size.y:0.###}/{size.z:0.###}|{scale.x:0.###}/{scale.y:0.###}/{scale.z:0.###}|{r:0.###}|{planar}";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        Vector3 h = real * .5f, inner = new(Mathf.Max(0, h.x - r), Mathf.Max(0, h.y - r), Mathf.Max(0, h.z - r));
        float[] Stops(float half) { float a = half - r; return new[] { -half, -a - r * .62f, -a - r * .2f, -a, a, a + r * .2f, a + r * .62f, half }; }
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        void Face(int axis, int sign)
        {
            int u = (axis + 1) % 3, v = (axis + 2) % 3;
            var su = Stops(h[u]); var sv = Stops(h[v]); int b = verts.Count;
            for (int i = 0; i < su.Length; i++)
                for (int j = 0; j < sv.Length; j++)
                {
                    Vector3 p = default; p[axis] = sign * h[axis]; p[u] = su[i]; p[v] = sv[j];
                    Vector3 q = new(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 d = p - q; Vector3 n = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.zero; if (n == Vector3.zero) n[axis] = sign;
                    Vector3 pos = q + n * r;
                    verts.Add(new Vector3(pos.x / scale.x, pos.y / scale.y, pos.z / scale.z));
                    norms.Add(new Vector3(n.x * scale.x, n.y * scale.y, n.z * scale.z).normalized);
                    if (!planar) uvs.Add(new Vector2(.5f, Mathf.InverseLerp(-h.y, h.y, pos.y)));
                    else uvs.Add(axis == 1 ? new Vector2(pos.x, pos.z) / 2f : axis == 0 ? new Vector2(pos.z, pos.y) / 1.6f : new Vector2(pos.x, pos.y) / 1.6f);
                }
            int w = sv.Length;
            for (int i = 0; i < su.Length - 1; i++)
                for (int j = 0; j < w - 1; j++)
                {
                    int a = b + i * w + j, c = a + w;
                    if (sign > 0) { tris.Add(a); tris.Add(c); tris.Add(a + 1); tris.Add(a + 1); tris.Add(c); tris.Add(c + 1); }
                    else { tris.Add(a); tris.Add(a + 1); tris.Add(c); tris.Add(a + 1); tris.Add(c + 1); tris.Add(c); }
                }
        }
        for (int axis = 0; axis < 3; axis++) { Face(axis, 1); Face(axis, -1); }
        var mesh = new Mesh { name = "Bloque juguete" };
        mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        meshes[key] = mesh; return mesh;
    }

    // ------------------------------------------------------------------ personajes
    void SwapActor(EscapeActor actor)
    {
        if (character == null) return;
        var field = typeof(EscapeActor).GetField("animator", I);
        var oldAnim = field?.GetValue(actor) as Animator;
        if (oldAnim == null) return;
        Transform oldVisual = oldAnim.transform; while (oldVisual.parent != null && oldVisual.parent != actor.transform) oldVisual = oldVisual.parent;
        var shadowMode = oldVisual.GetComponentInChildren<Renderer>()?.shadowCastingMode ?? UnityEngine.Rendering.ShadowCastingMode.On;
        var visual = PirateArenaVisuals.AddModel(character, actor.transform, 1.7f, null, false);
        if (visual == null) return;
        var look = visual.GetComponent<CharacterLook>();
        if (look != null)
        {
            var st = Random.state; Random.InitState(1000 + actor.Slot * 37); look.Randomize(); Random.state = st;
            look.Set("Mano", "Ninguno"); look.Set("Kart", "Ninguno");
        }
        var anim = visual.GetComponentInChildren<Animator>();
        anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        field.SetValue(actor, anim);
        typeof(EscapeActor).GetField("animState", I)?.SetValue(actor, null);
        foreach (var rr in visual.GetComponentsInChildren<Renderer>(true)) rr.shadowCastingMode = shadowMode;
        Destroy(oldVisual.gameObject);
        ActorsSwapped++;
    }
}
