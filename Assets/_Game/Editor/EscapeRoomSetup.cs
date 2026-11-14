using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the escape-room art set (reusing the project's own prefabs, palette and 3D text material) and the
/// scene Assets/001_Scenes/SalaDeEscape.unity, and adds it to the build list. Safe to run again.
/// </summary>
public static class EscapeRoomSetup
{
    const string ScenePath = "Assets/001_Scenes/SalaDeEscape.unity";
    const string ResourcesDir = "Assets/003_Scripts/EscapeRoom/Resources";
    const string SetPath = ResourcesDir + "/EscapeArtSet.asset";
    const string ArtRoot = "Assets/_Game/Art";

    [MenuItem("Multiverse/Sala de escape/Crear o actualizar escena")]
    public static string Build()
    {
        var report = new List<string>();
        var set = BuildArtSet(report);

        bool existed = File.Exists(ScenePath);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var sun = new GameObject("Luz de la torre").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.05f; sun.color = new Color(1f, .93f, .84f);
        sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(58f, -30f, 0f);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        camGo.transform.SetPositionAndRotation(new Vector3(0, 14, -20), Quaternion.Euler(40, 0, 0));
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.07f, .05f, .16f);

        var director = new GameObject("Sala de Escape").AddComponent<EscapeDirector>();
        director.totalSeconds = 900f;

        EditorSceneManager.SaveScene(scene, ScenePath);
        report.Add((existed ? "Escena actualizada: " : "Escena creada: ") + ScenePath);

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            report.Add("Añadida a Build Settings");
        }
        AssetDatabase.SaveAssets();
        report.Add($"Art set: {set.props.Length} props, {set.paints.Length} materiales, fuente {(set.font != null ? set.font.name : "—")}");
        return string.Join("\n", report);
    }

    /// <summary>Lists whatever occupies the planned Hub spot for the escape portal (open MundoDeInicio first).</summary>
    public static string ProbeHub(float x, float z)
    {
        var area = new Bounds(new Vector3(x, 1f, z), new Vector3(14f, 6f, 16f));
        var hits = new List<string>();
        foreach (var r in Object.FindObjectsByType<Renderer>())
            if (r.bounds.Intersects(area)) hits.Add(r.name + " @" + r.bounds.center.ToString("0.0"));
        return hits.Count == 0 ? "libre" : string.Join("\n", hits.Distinct().Take(40));
    }

    public static string ProbeHubSouth() => ProbeHub(0f, -27f);

    public static string ProbeHubMany()
    {
        var lines = new List<string>();
        foreach (var x in new[] { -17f, -14f, 14f, 17f, 20f })
            foreach (var z in new[] { -2f, 1f, 4f })
            {
                var area = new Bounds(new Vector3(x, 2.5f, z), new Vector3(8f, 3f, 7f));
                var hits = Object.FindObjectsByType<Renderer>().Where(r => r.bounds.Intersects(area) && r.bounds.size.x < 100f).Select(r => r.name).Distinct().ToList();
                lines.Add($"({x},{z}): " + (hits.Count == 0 ? "libre" : string.Join(", ", hits.Take(6))));
            }
        return string.Join("\n", lines);
    }

    static EscapeArtSet BuildArtSet(List<string> report)
    {
        Ensure(ResourcesDir);
        var set = AssetDatabase.LoadAssetAtPath<EscapeArtSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<EscapeArtSet>(); AssetDatabase.CreateAsset(set, SetPath); }

        var props = new List<EscapeArtSet.PropEntry>();
        void Prop(string key, string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) props.Add(new EscapeArtSet.PropEntry { key = key, prefab = go });
            else report.Add("Falta prefab: " + path);
        }
        foreach (var n in new[] { "Maceta", "Jardinera", "Arbusto", "Banco", "Cajas", "Guirnalda", "Poste indicador", "Farola" })
            Prop(n, $"{ArtRoot}/Prefabs/Comun/{n}.prefab");
        foreach (var n in new[] { "Tesoro", "Antorcha tiki" })
            Prop(n, $"{ArtRoot}/Prefabs/Piratas/{n}.prefab");
        set.props = props.ToArray();

        var paints = new List<EscapeArtSet.PaintEntry>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { ArtRoot + "/Materials/Paleta" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) paints.Add(new EscapeArtSet.PaintEntry { key = Path.GetFileNameWithoutExtension(path), material = m });
        }
        set.paints = paints.ToArray();

        set.font = MultiverseArtLibrary.TextFont();
        set.textMaterial = AssetDatabase.LoadAssetAtPath<Material>(ArtRoot + "/Materials/Texto/Texto 3D.mat");
        var arena = AssetDatabase.LoadAssetAtPath<MultiverseArtSet>("Assets/_Game/Resources/ArenaArtSet.asset");
        set.characterAtlas = arena != null ? arena.atlas : null;
        EditorUtility.SetDirty(set);
        return set;
    }

    static void Ensure(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
    }
}
