#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Convierte los FBX de Tools/Blender/zonas.py en prefabs listos para cargar con Resources (2026-11-17):
///  · Assets/002_Modelos/Zonas/Zonas_Zombi.fbx       → ZombieSurvival/Resources/SurvivalLandmarks/&lt;Pieza&gt;.prefab
///  · Assets/002_Modelos/Zonas/Rodillos_Calabozo.fbx → Rodillos/Resources/RodillosArt/&lt;Pieza&gt;.prefab
/// Las partes "_Brillo" usan el material emisivo y los hijos "_ColN" se convierten en BoxCollider en la raíz.
/// Menú: Multiverse → Zonas → Preparar prefabs.
/// </summary>
public static class ZonasSetup
{
    const string Dir = "Assets/002_Modelos/Zonas/";
    static readonly (string fbx, string outDir)[] Packs =
    {
        (Dir + "Zonas_Zombi.fbx", "Assets/003_Scripts/ZombieSurvival/Resources/SurvivalLandmarks/"),
        (Dir + "Rodillos_Calabozo.fbx", "Assets/003_Scripts/Rodillos/Resources/RodillosArt/"),
        (Dir + "Nave_Piezas.fbx", "Assets/003_Scripts/Naves/Resources/NaveArt/"),
        (Dir + "Pueblo_Props.fbx", "Assets/003_Scripts/Naves/Resources/NaveArt/"),
    };

    [MenuItem("Multiverse/Zonas/Preparar prefabs (zombi y rodillos)")]
    static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        var log = new List<string>();
        string texPath = Dir + "Zonas_BaseColor.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (ti == null) return "FAIL falta " + texPath;
        if (ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Clamp) { ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear; ti.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        Material MakeMat(string name, string template)
        {
            string path = Dir + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/002_Modelos/Decoracion/" + template + ".mat");
                m = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            foreach (var prop in new[] { "_BaseMap", "_MainTex", "_EmissionMap" }) if (m.HasProperty(prop) && (prop != "_EmissionMap" || name.EndsWith("Brillo"))) m.SetTexture(prop, tex);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .12f);
            EditorUtility.SetDirty(m);
            return m;
        }
        var mat = MakeMat("Zonas", "Decoracion");
        var glow = MakeMat("Zonas_Brillo", "Decoracion_Brillo");

        foreach (var (fbx, outDir) in Packs)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (imp == null) { log.Add("falta " + fbx); continue; }
            if (imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.importAnimation || imp.isReadable == false)
            { imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.importAnimation = false; imp.isReadable = true; imp.SaveAndReimport(); }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            Directory.CreateDirectory(outDir);
            int made = 0;
            foreach (Transform src in asset.transform)
            {
                string name = src.name.Split('.')[0];
                var holder = new GameObject(name);
                var model = Object.Instantiate(src.gameObject, holder.transform, false);
                model.name = "Modelo"; model.transform.localPosition = Vector3.zero;
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = r.name.Contains("_Brillo") ? glow : mat;
                foreach (var colT in model.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("_Col")).ToList())
                {
                    var mf = colT.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var b = mf.sharedMesh.bounds;
                        // Bounds in the holder's space (the model may carry the FBX axis rotation).
                        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
                        for (int i = 0; i < 8; i++)
                        {
                            var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                            var p = holder.transform.InverseTransformPoint(colT.TransformPoint(corner));
                            min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                        }
                        var box = holder.AddComponent<BoxCollider>(); box.center = (min + max) * .5f; box.size = max - min;
                    }
                    Object.DestroyImmediate(colT.gameObject);
                }
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                PrefabUtility.SaveAsPrefabAsset(holder, outDir + name + ".prefab");
                if (made == 0 || name.Contains("Totem") || name.Contains("Rodillo_Bajo") || name == "Nave_Cabina" || name == "Pozo")
                {
                    var bb = PirateArenaVisuals.BoundsOf(holder);
                    log.Add($"{name}: tamaño {bb.size.x:0.0}×{bb.size.y:0.0}×{bb.size.z:0.0} centro {bb.center} cajas {holder.GetComponents<BoxCollider>().Length}" +
                            (holder.GetComponents<BoxCollider>().FirstOrDefault() is BoxCollider bc ? $" (1.ª en {bc.center})" : ""));
                }
                Object.DestroyImmediate(holder);
                made++;
            }
            log.Add(Path.GetFileName(fbx) + ": " + made + " prefabs");
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        return "OK " + string.Join(" · ", log);
    }
}
#endif
