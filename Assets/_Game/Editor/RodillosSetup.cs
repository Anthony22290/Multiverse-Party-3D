#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Crea la escena del minijuego Rodillos del Calabozo (Assets/001_Scenes/RodillosDelCalabozo.unity) y la añade a Build Settings.
/// La escena solo lleva cámara, luz y el RodillosDirector: todo lo demás se construye al jugar.
/// Menú: Multiverse → Rodillos del Calabozo → Crear escena.
/// </summary>
public static class RodillosSetup
{
    public const string ScenePath = "Assets/001_Scenes/RodillosDelCalabozo.unity";
    const string Character = "Assets/_Game/ToyStyle/Fiestero_Juguete.prefab";

    [MenuItem("Multiverse/Rodillos del Calabozo/Crear escena")]
    static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "FAIL detén Play primero";
        if (EditorSceneManager.GetActiveScene().isDirty) return "FAIL la escena abierta tiene cambios sin guardar";
        if (Resources.Load<GameObject>("RodillosArt/Rodillo_Bajo") == null) Debug.Log(ZonasSetup.Build());
        var character = AssetDatabase.LoadAssetAtPath<GameObject>(Character);
        if (character == null) return "FAIL falta " + Character + " (Multiverse → Probador de assets → Crear Sala de Escape estilo juguete)";

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cam.tag = "MainCamera";
        cam.transform.SetPositionAndRotation(new Vector3(0, 14, -20), Quaternion.Euler(35, 0, 0));
        var c = cam.GetComponent<Camera>(); c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(.08f, .03f, .03f);
        var sun = new GameObject("Luz de la mazmorra", typeof(Light));
        var l = sun.GetComponent<Light>(); l.type = LightType.Directional; l.color = new Color(1f, .78f, .62f); l.intensity = 1.05f; l.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(62, -35, 0);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.32f, .2f, .18f);
        var dir = new GameObject("Rodillos del Calabozo");
        var d = dir.AddComponent<RodillosDirector>(); d.characterPrefab = character;
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);

        var list = EditorBuildSettings.scenes.ToList();
        if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
        AssetDatabase.SaveAssets();
        return "OK " + ScenePath + " · en Build Settings: " + EditorBuildSettings.scenes.Count(s => s.path == ScenePath);
    }
}
#endif
