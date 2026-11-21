#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Crea la escena del minijuego Atrápalo (Assets/001_Scenes/Atrapalo.unity) y la añade a Build Settings.
/// La escena solo lleva cámara, luz y el AtrapaDirector: todo lo demás se construye al jugar.
/// Menú: Multiverse → Atrápalo → Crear escena.
/// </summary>
public static class AtrapaSetup
{
    public const string ScenePath = "Assets/001_Scenes/Atrapalo.unity";
    const string Character = "Assets/_Game/ToyStyle/Fiestero_Juguete.prefab";

    [MenuItem("Multiverse/Atrápalo/Crear escena")]
    static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "FAIL detén Play primero";
        if (EditorSceneManager.GetActiveScene().isDirty) return "FAIL la escena abierta tiene cambios sin guardar";
        var character = AssetDatabase.LoadAssetAtPath<GameObject>(Character);
        if (character == null) return "FAIL falta " + Character + " (Multiverse → Probador de assets → Crear Sala de Escape estilo juguete)";

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cam.tag = "MainCamera";
        cam.transform.SetPositionAndRotation(new Vector3(0, 1, -12), Quaternion.identity);
        var c = cam.GetComponent<Camera>(); c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(.08f, .03f, .03f);
        var sun = new GameObject("Sol", typeof(Light));
        var l = sun.GetComponent<Light>(); l.type = LightType.Directional; l.color = new Color(1f, .78f, .62f); l.intensity = 1.1f; l.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(55, -30, 0);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.62f, .66f, .7f);
        var dir = new GameObject("Atrápalo");
        var d = dir.AddComponent<AtrapaDirector>(); d.characterPrefab = character;
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);

        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
        AssetDatabase.SaveAssets();
        return "OK " + ScenePath + " · en Build Settings: " + EditorBuildSettings.scenes.Count(s => s.path == ScenePath);
    }
}
#endif
