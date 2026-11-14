using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Ropa y colores intercambiables del personaje modular hecho en Blender (Tools/Blender/aventurero.py).
/// Las piezas son hijos llamados Categoria_Opcion (Pelo_Corto, Torso_Armadura…); "Cuerpo_*" siempre visible.
/// Los colores se pintan en un atlas 4x4 generado aquí (mismo orden de celdas que el script de Blender).
/// </summary>
public class CharacterLook : MonoBehaviour
{
    public static readonly string[] Slots = { "Pelo", "Cabeza", "Cara", "Torso", "Cintura", "Piernas", "Pies", "Espalda", "Mano", "Kart" };
    public static readonly string[] SlotNames = { "Pelo", "Cabeza", "Cara", "Ropa", "Cintura", "Piernas", "Calzado", "Espalda", "En la mano", "Vehículo" };
    static readonly bool[] Optional = { true, true, true, false, true, false, false, true, true, false };
    // Disfraces que tapan toda la cabeza (el pelo se oculta) y gorros normales (el pelo grande pasa a corto).
    static readonly string[] Covering = { "Dino", "Pollo", "Casco" };
    static readonly string[] BigHair = { "Afro", "Cresta" };

    // Celdas del atlas (fila 0 abajo): igual que CELLS en aventurero.py
    static readonly string[] Cells = { "skin", "skin_shade", "dark", "white", "hair", "primary", "secondary", "accent",
                                       "leather", "metal", "metal_dark", "wood", "gold", "boots", "cheek", "magic" };
    static readonly Dictionary<string, Color> Fixed = new()
    {
        ["dark"] = C(.16f, .12f, .14f), ["white"] = C(.97f, .96f, .93f), ["leather"] = C(.55f, .34f, .2f), ["metal"] = C(.72f, .76f, .82f),
        ["metal_dark"] = C(.38f, .41f, .47f), ["wood"] = C(.62f, .42f, .26f), ["gold"] = C(.98f, .78f, .26f), ["cheek"] = C(.98f, .56f, .55f), ["magic"] = C(.45f, .95f, 1f),
    };
    public static readonly string[] ColorSlots = { "Piel", "Pelo", "Ropa", "Ropa 2", "Detalle", "Calzado" };
    static readonly string[] ColorCell = { "skin", "hair", "primary", "secondary", "accent", "boots" };
    public static readonly Color[][] ColorOptions =
    {
        new[] { C(.98f, .78f, .62f), C(1f, .86f, .72f), C(.86f, .64f, .46f), C(.68f, .46f, .3f), C(.47f, .3f, .2f), C(.62f, .86f, .52f), C(.62f, .78f, .98f) },
        new[] { C(.45f, .27f, .16f), C(.16f, .12f, .12f), C(.96f, .8f, .38f), C(.86f, .36f, .18f), C(.92f, .92f, .95f), C(.3f, .55f, .95f), C(.95f, .45f, .7f), C(.4f, .8f, .45f) },
        new[] { C(.22f, .52f, .86f), C(.86f, .26f, .26f), C(.3f, .7f, .35f), C(.62f, .36f, .82f), C(.96f, .66f, .2f), C(.18f, .2f, .28f), C(.95f, .95f, .95f), C(.2f, .75f, .75f) },
        new[] { C(.93f, .86f, .68f), C(.36f, .3f, .26f), C(.3f, .38f, .6f), C(.55f, .2f, .22f), C(.42f, .55f, .3f), C(.85f, .85f, .88f) },
        new[] { C(.92f, .35f, .24f), C(.98f, .78f, .26f), C(.3f, .75f, .95f), C(.65f, .4f, .85f), C(.4f, .85f, .45f), C(.95f, .5f, .75f) },
        new[] { C(.42f, .27f, .18f), C(.18f, .16f, .18f), C(.62f, .2f, .2f), C(.25f, .3f, .5f), C(.85f, .82f, .76f) },
    };
    static Color C(float r, float g, float b) => new(r, g, b);

    public int[] sel = new int[10];
    public int[] colorSel = new int[6];
    readonly Dictionary<string, List<GameObject>> options = new();
    Texture2D atlas; Material mat; bool ready;

    void Awake() => Init();

    public void Init()
    {
        if (ready) return; ready = true;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            string n = r.gameObject.name; int u = n.IndexOf('_'); if (u < 0) continue;
            string slot = n[..u];
            if (!options.TryGetValue(slot, out var list)) options[slot] = list = new List<GameObject>();
            list.Add(r.gameObject);
        }
        foreach (var l in options.Values) l.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        var src = GetComponentInChildren<Renderer>(true)?.sharedMaterial;
        mat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = "Aspecto"; mat.SetFloat("_Smoothness", .15f);
        atlas = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Atlas aspecto" };
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        // Aspecto inicial: el clásico (pelo corto, camiseta, pantalón, botas, espada).
        sel = new int[Slots.Length];
        sel[0] = Index("Pelo", "Corto"); sel[3] = Index("Torso", "Camiseta"); sel[5] = Index("Piernas", "Pantalon"); sel[6] = Index("Pies", "Zapatillas");
        Apply();
    }

    int Index(string slot, string option) { var o = Options(System.Array.IndexOf(Slots, slot)); int i = o.IndexOf(option); return Mathf.Max(0, i); }

    /// <summary>Nombres de las opciones de una categoría ("Ninguno" primero si es opcional).</summary>
    public List<string> Options(int slot)
    {
        var list = new List<string>(); if (Optional[slot]) list.Add("Ninguno");
        if (options.TryGetValue(Slots[slot], out var gos)) list.AddRange(gos.Select(g => g.name[(Slots[slot].Length + 1)..]));
        return list;
    }

    public string Current(int slot) { var o = Options(slot); return o.Count == 0 ? "-" : o[Mathf.Clamp(sel[slot], 0, o.Count - 1)]; }
    public Color CurrentColor(int c) => ColorOptions[c][Mathf.Clamp(colorSel[c], 0, ColorOptions[c].Length - 1)];

    /// <summary>Elige una opción por nombre ("Cabeza", "Casco"). Devuelve false si no existe.</summary>
    public bool Set(string slot, string option)
    {
        int s = System.Array.IndexOf(Slots, slot); if (s < 0) return false;
        int i = Options(s).IndexOf(option); if (i < 0) return false;
        sel[s] = i; Apply(); return true;
    }

    public void Step(int slot, int dir) { int n = Options(slot).Count; if (n == 0) return; sel[slot] = ((sel[slot] + dir) % n + n) % n; Apply(); }
    public void StepColor(int c, int dir) { int n = ColorOptions[c].Length; colorSel[c] = ((colorSel[c] + dir) % n + n) % n; Apply(); }

    public void Randomize()
    {
        for (int s = 0; s < Slots.Length; s++) { int n = Options(s).Count; sel[s] = n == 0 ? 0 : Random.Range(0, n); }
        if (Random.value < .45f) sel[System.Array.IndexOf(Slots, "Cara")] = 0;
        if (Random.value < .5f) sel[System.Array.IndexOf(Slots, "Cintura")] = 0;
        for (int c = 0; c < ColorSlots.Length; c++) colorSel[c] = Random.Range(0, ColorOptions[c].Length);
        Apply();
    }

    public void Apply()
    {
        Init();
        foreach (var kv in options)
        {
            int s = System.Array.IndexOf(Slots, kv.Key);
            if (s < 0) { foreach (var g in kv.Value) g.SetActive(true); continue; }  // Cuerpo_*
            string want = Current(s);
                        if (kv.Key == "Pelo")
            {
                string head = Current(System.Array.IndexOf(Slots, "Cabeza"));
                if (System.Array.IndexOf(Covering, head) >= 0) want = "";
                else if (head != "Ninguno" && System.Array.IndexOf(BigHair, want) >= 0) want = "Corto";
            }
            foreach (var g in kv.Value) g.SetActive(g.name == kv.Key + "_" + want);
        }
        PaintAtlas();
    }

    void PaintAtlas()
    {
        var col = new Dictionary<string, Color>(Fixed);
        for (int c = 0; c < ColorSlots.Length; c++) col[ColorCell[c]] = CurrentColor(c);
        var skin = col["skin"]; col["skin_shade"] = new Color(skin.r * .88f, skin.g * .74f, skin.b * .74f);
        var px = new Color32[128 * 128];
        for (int i = 0; i < Cells.Length; i++)
        {
            Color b = col[Cells[i]]; int cx = (i % 4) * 32, cy = (i / 4) * 32;
            for (int y = 0; y < 32; y++)
            {
                float k = .78f + .34f * (y / 31f);
                Color32 c = new Color(Mathf.Clamp01(b.r * k), Mathf.Clamp01(b.g * k), Mathf.Clamp01(b.b * k), 1f);
                for (int x = 0; x < 32; x++) px[(cy + y) * 128 + cx + x] = c;
            }
        }
        atlas.SetPixels32(px); atlas.Apply(false);
        mat.SetTexture("_BaseMap", atlas); mat.mainTexture = atlas;
    }

    // ------------------------------------------------ guardar / cargar
    public const string PrefKey = "Multiverse_Aventurero";
    public string Encode() => string.Join(",", sel) + "|" + string.Join(",", colorSel);
    public void Decode(string code)
    {
        if (string.IsNullOrEmpty(code)) return;
        var parts = code.Split('|');
        if (parts.Length != 2) return;
        var a = parts[0].Split(','); var b = parts[1].Split(',');
        for (int i = 0; i < sel.Length && i < a.Length; i++) int.TryParse(a[i], out sel[i]);
        for (int i = 0; i < colorSel.Length && i < b.Length; i++) int.TryParse(b[i], out colorSel[i]);
        Apply();
    }
    public void Save() { PlayerPrefs.SetString(PrefKey, Encode()); PlayerPrefs.Save(); }
    public void Load() => Decode(PlayerPrefs.GetString(PrefKey, ""));

    void OnDestroy() { if (atlas != null) Destroy(atlas); if (mat != null) Destroy(mat); }
}
