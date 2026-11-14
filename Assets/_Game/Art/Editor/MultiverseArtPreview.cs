using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Lays out every generated prefab of a category in a temporary scene and saves review captures.</summary>
public static class MultiverseArtPreview
{
    [MenuItem("Multiverse/Arte/Capturar catálogo de prefabs", priority = 20)]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        var previous = EditorSceneManager.GetActiveScene().path;
        if (EditorSceneManager.GetActiveScene().isDirty) throw new System.Exception("Guarda la escena actual antes de capturar el catálogo");
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Claude_Capturas"));
        Directory.CreateDirectory(outDir);
        var cats = AssetDatabase.GetSubFolders(MultiverseArtLibrary.Root + "/Prefabs");
        string result = "";
        foreach (var cat in cats)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.7f, .74f, .8f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.transform.localScale = Vector3.one * 20;
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); gm.color = new Color(.55f, .78f, .5f); ground.GetComponent<Renderer>().sharedMaterial = gm;
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { cat }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToList();
            int cols = Mathf.CeilToInt(Mathf.Sqrt(prefabs.Count * 1.6f));
            float x = 0, z = 0, rowDepth = 0, maxX = 0; int c = 0;
            foreach (var p in prefabs)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(p);
                var b = inst.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, bb) => { a.Encapsulate(bb); return a; });
                float w = Mathf.Max(2f, b.size.x) + 1.5f, d = Mathf.Max(2f, b.size.z) + 1.5f;
                inst.transform.position = new Vector3(x + w * .5f - b.center.x, 0, z - d * .5f - b.center.z);
                inst.transform.rotation = Quaternion.identity;
                x += w; rowDepth = Mathf.Max(rowDepth, d); maxX = Mathf.Max(maxX, x);
                if (++c % cols == 0) { x = 0; z -= rowDepth; rowDepth = 0; }
            }
            float depth = -z + rowDepth;
            var cam = new GameObject("Cam").AddComponent<Camera>(); cam.fieldOfView = 40; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .8f, .92f);
            var center = new Vector3(maxX * .5f, 1.5f, -depth * .5f);
            float dist = Mathf.Max(maxX, depth) * 1.15f + 6f;
            cam.transform.position = center + new Vector3(0, dist * .55f, -dist * .85f); cam.transform.LookAt(center); cam.farClipPlane = 1000;
            string name = Path.GetFileName(cat);
            string file = Path.Combine(outDir, "Biblioteca_" + name + ".png");
            ClaudeBridge.CaptureCamera(cam, file, 1920, 1080);
            result += name + ": " + prefabs.Count + " prefabs -> " + file + "\n";
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        return result;
    }
}
