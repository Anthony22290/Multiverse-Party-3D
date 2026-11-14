#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Crea la escena de comparación Assets/001_Scenes/Pruebas/SalaDeEscape_Juguete.unity: copia de SalaDeEscape con el mismo
/// código, más el componente EscapeToyStyle (Art Set de juguete, bloques redondeados, degradados y Fiesteros).
/// Menú: Multiverse → Probador de assets → Crear Sala de Escape estilo juguete.
/// </summary>
public static class ToyStyleSetup
{
    const string Root = "Assets/_Game/ToyStyle/";
    const string Original = "Assets/001_Scenes/SalaDeEscape.unity";
    const string Copy = "Assets/001_Scenes/Pruebas/SalaDeEscape_Juguete.unity";
    const string Pack = "Assets/002_Modelos/Decoracion/Deco_EscapeJuguete.fbx";

    [MenuItem("Multiverse/Probador de assets/Crear Sala de Escape estilo juguete")]
    static void Menu() => Debug.Log(Build());

    static float Height(GameObject prefab)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var b = PirateArenaVisuals.BoundsOf(inst); Object.DestroyImmediate(inst); return b.size.y;
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "FAIL detén Play primero";
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) return "FAIL la escena abierta tiene cambios sin guardar: " + active.path;
        Directory.CreateDirectory(Root + "Props");
        var log = new List<string>();

        var deco = AssetDatabase.LoadAssetAtPath<Material>("Assets/002_Modelos/Decoracion/Decoracion.mat");
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/002_Modelos/Decoracion/Decoracion_Brillo.mat");
        if (deco == null || glow == null) return "FAIL faltan los materiales de Decoracion (reconstruye el Probador primero)";
        var imp = (ModelImporter)AssetImporter.GetAtPath(Pack);
        if (imp.materialImportMode != ModelImporterMaterialImportMode.None || imp.importAnimation) { imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.importAnimation = false; imp.SaveAndReimport(); }
        var packAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Pack);

        // Art Set de juguete: copia del original con los props cambiados.
        var orig = AssetDatabase.LoadAssetAtPath<EscapeArtSet>("Assets/003_Scripts/EscapeRoom/Resources/EscapeArtSet.asset");
        string setPath = Root + "EscapeArtSet_Juguete.asset";
        AssetDatabase.DeleteAsset(setPath);
        var toy = Object.Instantiate(orig); AssetDatabase.CreateAsset(toy, setPath);
        var props = new List<EscapeArtSet.PropEntry>();
        foreach (var p in orig.props)
        {
            // Blender añade ".001" si el nombre ya existía en otro mapa.
            var src = packAsset.transform.Cast<Transform>().FirstOrDefault(t => t.name == p.key || t.name.StartsWith(p.key + "."));
            if (src == null || p.prefab == null) { props.Add(p); log.Add("sin versión juguete: " + p.key); continue; }
            var holder = new GameObject(p.key);
            var model = Object.Instantiate(src.gameObject, holder.transform, false); model.name = "Modelo";
            model.transform.localPosition = Vector3.zero;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = r.name.EndsWith("_Brillo") ? glow : deco;
            float want = Height(p.prefab) * p.prefab.transform.localScale.y, have = PirateArenaVisuals.BoundsOf(holder).size.y;
            if (have > .01f && want > .01f) holder.transform.localScale = Vector3.one * (want / have) / Mathf.Max(.001f, p.prefab.transform.localScale.y);
            var prefab = PrefabUtility.SaveAsPrefabAsset(holder, Root + "Props/" + p.key + ".prefab"); Object.DestroyImmediate(holder);
            props.Add(new EscapeArtSet.PropEntry { key = p.key, prefab = prefab });
        }
        toy.props = props.ToArray(); EditorUtility.SetDirty(toy);
        log.Add($"Art Set: {props.Count(p => p.prefab != null && AssetDatabase.GetAssetPath(p.prefab).StartsWith(Root))}/{props.Count} props de juguete");

        // Personaje para los bots (mismo estado Idle/Walk que pide EscapeActor).
        string fbx = "Assets/002_Modelos/Personajes/Fiestero/Fiestero.fbx";
        var ch = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        PrefabUtility.UnpackPrefabInstance(ch, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        ch.name = "Fiestero";
        var anim = ch.GetComponent<Animator>(); if (anim == null) anim = ch.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Game/Showroom/Generated/Fiestero_Probador.controller");
        var fmat = AssetDatabase.LoadAssetAtPath<Material>("Assets/002_Modelos/Personajes/Fiestero/Fiestero.mat");
        foreach (var r in ch.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(fmat, r.sharedMaterials.Length).ToArray();
        ch.AddComponent<CharacterLook>();
        var chPrefab = PrefabUtility.SaveAsPrefabAsset(ch, Root + "Fiestero_Juguete.prefab"); Object.DestroyImmediate(ch);

        // Escena copia con el componente de estilo.
        Directory.CreateDirectory(Path.GetDirectoryName(Copy));
        AssetDatabase.DeleteAsset(Copy);
        if (!AssetDatabase.CopyAsset(Original, Copy)) return "FAIL no se pudo copiar la escena";
        var scene = EditorSceneManager.OpenScene(Copy, OpenSceneMode.Single);
        var go = new GameObject("Estilo juguete (comparación)");
        var style = go.AddComponent<EscapeToyStyle>(); style.toyArt = toy; style.character = chPrefab;
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "OK " + Copy + " · " + string.Join(" · ", log);
    }
}
#endif
