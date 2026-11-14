#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Prepara los personajes generados con Meshy (Assets/002_Modelos/Meshy/<Nombre>/):
/// importación Generic, material URP con su textura, Animator con Idle/Walk/Run y prefab en Resources.
/// Jefes de Invasión Zombi -> ZombieSurvival/Resources/SurvivalBosses/<Gordo|Brazos|Costillas|Rey>.prefab
/// Héroe -> _Game/Resources/MeshyCharacters/Heroe.prefab
/// Golpes, aturdimiento y muerte de los jefes son procedurales (SurvivalZombie), porque Meshy solo da caminar/correr.
/// </summary>
public static class MeshyCharacterSetup
{
    const string Src = "Assets/002_Modelos/Meshy/";
    static readonly (string name, string outDir)[] Chars =
    {
        ("Heroe", "Assets/_Game/Resources/MeshyCharacters/"),
        ("Gordo", "Assets/003_Scripts/ZombieSurvival/Resources/SurvivalBosses/"),
        ("Brazos", "Assets/003_Scripts/ZombieSurvival/Resources/SurvivalBosses/"),
        ("Costillas", "Assets/003_Scripts/ZombieSurvival/Resources/SurvivalBosses/"),
        ("Rey", "Assets/003_Scripts/ZombieSurvival/Resources/SurvivalBosses/"),
    };

    [MenuItem("Multiverse/Personajes Meshy/Preparar prefabs")]
    static void Menu() => Debug.Log(Run());

    static AnimationClip Clip(string fbx, bool loop)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        bool changed = false;
        if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; changed = true; }
        if (!imp.importAnimation) { imp.importAnimation = true; changed = true; }
        if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; changed = true; }
        var clips = imp.clipAnimations != null && imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        foreach (var c in clips) if (c.loopTime != loop || !c.lockRootPositionXZ) { c.loopTime = loop; c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true; changed = true; }
        if (clips.Length > 0) imp.clipAnimations = clips;
        if (changed) imp.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).OrderByDescending(c => c.length).FirstOrDefault();
    }

    public static string Run()
    {
        var log = new List<string>();
        // Limpia restos de una ejecución fallida en la escena abierta.
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var go in active.GetRootGameObjects())
            if (Chars.Any(c => c.name == go.name) && go.GetComponentInChildren<SkinnedMeshRenderer>() != null) { log.Add("limpiado " + go.name); Object.DestroyImmediate(go); }
        if (log.Count > 0) EditorSceneManager.SaveScene(active);
        foreach (var (name, outDir) in Chars)
        {
            string dir = Src + name + "/";
            System.IO.Directory.CreateDirectory(outDir);
            // Textura y material.
            string texPath = dir + name + "_BaseColor.jpg";
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            if (ti != null && ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string matPath = dir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(mat);

            // Modelo base sin animaciones propias; clips desde @Walk y @Run.
            string fbx = dir + name + ".fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (imp.animationType != ModelImporterAnimationType.Generic || imp.importAnimation || imp.materialImportMode != ModelImporterMaterialImportMode.None)
            { imp.animationType = ModelImporterAnimationType.Generic; imp.importAnimation = false; imp.materialImportMode = ModelImporterMaterialImportMode.None; imp.SaveAndReimport(); }
            var walk = Clip(dir + name + "@Walk.fbx", true);
            var run = Clip(dir + name + "@Run.fbx", true);

            string ctrlPath = dir + name + ".controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            // Idle: el paso muy lento, como balanceo (no hay clip de reposo).
            var idle = sm.AddState("Idle"); idle.motion = walk; idle.speed = .22f; sm.defaultState = idle;
            var w = sm.AddState("Walk"); w.motion = walk;
            var r = sm.AddState("Run"); r.motion = run;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = name;
            var animator = inst.GetComponent<Animator>(); if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl; animator.applyRootMotion = false;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            foreach (var rend in inst.GetComponentsInChildren<Renderer>()) rend.sharedMaterials = Enumerable.Repeat(mat, rend.sharedMaterials.Length).ToArray();
            try { PrefabUtility.SaveAsPrefabAsset(inst, outDir + name + ".prefab"); }
            finally { Object.DestroyImmediate(inst); }
            log.Add($"{name} ok walk={(walk ? walk.name + " " + walk.length.ToString("0.0") + "s" : "FALTA")} run={(run ? run.name : "FALTA")} tex={(tex != null)}");
        }
        AssetDatabase.SaveAssets();
        return string.Join(" | ", log);
    }

    /// <summary>Foto de comprobación: los 5 prefabs en fila, a mitad del ciclo de caminar.</summary>
    public static string Lineup()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var made = new List<GameObject>();
        float x = -6f;
        foreach (var (name, outDir) in Chars)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outDir + name + ".prefab");
            var holder = new GameObject(name); EditorSceneManager.MoveGameObjectToScene(holder, scene);
            holder.transform.position = new Vector3(x, 0, 0); x += 3f;
            float h = name == "Heroe" ? 1.7f : name == "Rey" ? 2.6f : 2.4f;
            var v = PirateArenaVisuals.AddModel(prefab, holder.transform, h, null, false);
            var clip = v.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips.FirstOrDefault();
            if (clip != null) clip.SampleAnimation(v, clip.length * .3f);
            made.Add(holder);
        }
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); EditorSceneManager.MoveGameObjectToScene(ground, scene); ground.transform.localScale = Vector3.one * 4f;
        var lg = new GameObject("Luz"); EditorSceneManager.MoveGameObjectToScene(lg, scene);
        var light = lg.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; lg.transform.rotation = Quaternion.Euler(40, 150, 0);
        var cg = new GameObject("Cam"); EditorSceneManager.MoveGameObjectToScene(cg, scene);
        var cam = cg.AddComponent<Camera>(); cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .62f, .7f);
        cam.fieldOfView = 32f; cg.transform.position = new Vector3(0, 2.2f, 16f); cg.transform.LookAt(new Vector3(0, 1.1f, 0));
        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Docs/Claude_Capturas/Meshy/lineup.png"));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        ClaudeBridge.CaptureCamera(cam, path, 1600, 700);
        EditorSceneManager.ClosePreviewScene(scene);
        return path;
    }
}
#endif
