#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Construye Assets/001_Scenes/Pruebas/ProbadorDeAssets.unity con todos los modelos de las carpetas de prueba.
/// Cada subcarpeta <Nombre>/ con <Nombre>.fbx es un modelo; sus clips salen de <Nombre>.fbx y de <Nombre>@<Clip>.fbx.
/// Para añadir un asset nuevo: déjalo en Assets/002_Modelos/Meshy/<Nombre>/ y vuelve a usar el menú.
/// La escena no está en Build Settings: es solo para revisar antes de integrar.
/// </summary>
public static class AssetShowroomBuilder
{
    static readonly string[] Roots = { "Assets/002_Modelos/Personajes", "Assets/002_Modelos/Meshy" };
    const string ScenePath = "Assets/001_Scenes/Pruebas/ProbadorDeAssets.unity";
    const string Gen = "Assets/_Game/Showroom/Generated/";

    [MenuItem("Multiverse/Probador de assets/Reconstruir escena")]
    static void Menu() => Debug.Log(Build());

    [MenuItem("Multiverse/Probador de assets/Abrir escena")]
    static void Open() { if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath); }

    static void PrepareImport(string fbx, bool isAnim)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
        if (imp == null) return;
        bool ch = false;
        if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; ch = true; }
        if (imp.materialImportMode != ModelImporterMaterialImportMode.None) { imp.materialImportMode = ModelImporterMaterialImportMode.None; ch = true; }
        if (isAnim)
        {
            var clips = imp.clipAnimations != null && imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
            string n = Path.GetFileNameWithoutExtension(fbx).ToLowerInvariant();
            bool loop = !(n.Contains("death") || n.Contains("muerte") || n.Contains("attack") || n.Contains("hit") || n.Contains("jump"));
            foreach (var c in clips) if (c.loopTime != loop || !c.lockRootPositionXZ) { c.loopTime = loop; c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true; ch = true; }
            if (clips.Length > 0) imp.clipAnimations = clips;
        }
        if (ch) imp.SaveAndReimport();
    }

    static Material MaterialFor(string dir, string name)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(dir + name + ".mat");
        if (mat != null) return mat;
        var texPath = Directory.GetFiles(dir).Select(p => p.Replace('\\', '/')).FirstOrDefault(p => p.Contains("_BaseColor") && !p.EndsWith(".meta"));
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (texPath != null) { var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath); mat.SetTexture("_BaseMap", t); mat.mainTexture = t; }
        mat.SetFloat("_Smoothness", .12f);
        AssetDatabase.CreateAsset(mat, dir + name + ".mat");
        return mat;
    }

    static Material Solid(string file, Color c)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Gen + file);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, Gen + file); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .2f); EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------ dioramas de cada mapa
    const string DecoDir = "Assets/002_Modelos/Decoracion/";
    static readonly Dictionary<string, (string model, Vector3 pos, float yaw, string state, string outfit)[]> Actors = new()
    {
        ["Arena"] = new[] { ("Fiestero", new Vector3(-.8f, .13f, .2f), 120f, "Dance", ""), ("Fiestero", new Vector3(.9f, .13f, -.3f), -70f, "Punch", "Mano=Pescado"),
                             ("Fiestero", new Vector3(.2f, .13f, 1.4f), 180f, "Victory", "Cintura=Flotador,Cara=GafasSol") },
        ["Carreras"] = new[] { ("Piloto", new Vector3(.8f, .08f, 3.2f), 0f, "Kart_Idle", "Kart=Clasico,Cabeza=Casco,Torso=Piloto"), ("Piloto", new Vector3(-.8f, .08f, -3.2f), 0f, "Kart_Boost", "Kart=Banera"),
                               ("Fiestero", new Vector3(-3.4f, .0f, 3.5f), 150f, "Wave", "") },
        ["Zombis"] = new[] { ("Gordo", new Vector3(1.4f, .08f, .3f), -110f, "Walk", ""), ("Fiestero", new Vector3(-.8f, .08f, -.4f), 70f, "Run", "Mano=Baguette") },
        ["Codigo"] = new[] { ("Fiestero", new Vector3(-.6f, .08f, .3f), 160f, "Idle", "Cabeza=Casco,Torso=Piloto"), ("Fiestero", new Vector3(.9f, .08f, -.5f), -120f, "Wave", "") },
        ["Escape"] = new[] { ("Fiestero", new Vector3(.6f, .12f, .4f), 200f, "Victory", ""), ("Fiestero", new Vector3(-1.4f, .1f, -.9f), 40f, "Idle", "Cabeza=Chef") },
    };

    static Material GlowFrom(Material baseMat)
    {
        string path = DecoDir + "Decoracion_Brillo.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, path); }
        m.CopyPropertiesFromMaterial(baseMat);
        m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetTexture("_EmissionMap", baseMat.GetTexture("_BaseMap")); m.SetColor("_EmissionColor", Color.white * 1.6f);
        EditorUtility.SetDirty(m); return m;
    }

    static void ApplyDecoMaterials(GameObject go, Material deco, Material glow)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = Enumerable.Repeat(r.name.EndsWith("_Brillo") ? glow : deco, r.sharedMaterials.Length).ToArray();
    }

    static GameObject Actor(string model, Transform parent, Vector3 pos, float yaw, string state, string outfit, int seed)
    {
        string dir = Roots.Select(r => r + "/" + model + "/").FirstOrDefault(d => File.Exists(d + model + ".fbx"));
        if (dir == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dir + model + ".fbx"));
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = model + " (extra)"; go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0) * go.transform.localRotation;
        var anim = go.GetComponent<Animator>(); if (anim == null) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Gen + model + "_Probador.controller");
        anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var mat = MaterialFor(dir, model);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        if (go.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Torso_"))) go.AddComponent<CharacterLook>();
        var actor = go.AddComponent<ShowroomActor>(); actor.state = state; actor.outfit = outfit; actor.seed = seed;
        return go;
    }

    static void BuildDioramas(AssetShowroom show, List<string> log)
    {
        if (!Directory.Exists(DecoDir)) return;
        var files = Directory.GetFiles(DecoDir, "Diorama_*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
        if (files.Count == 0) return;
        var deco = MaterialFor(DecoDir, "Decoracion");
        var glow = GlowFrom(deco);
        foreach (var f in Directory.GetFiles(DecoDir, "*.fbx")) PrepareImport(f.Replace('\\', '/'), false);
        float spacing = 13f, start = -(files.Count - 1) * spacing * .5f;
        int seed = 11;
        for (int i = 0; i < files.Count; i++)
        {
            string mapa = Path.GetFileNameWithoutExtension(files[i]).Substring("Diorama_".Length);
            var holder = new GameObject("Diorama " + mapa); holder.transform.position = new Vector3(start + i * spacing, 0, -16f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(files[i]));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Escenario " + mapa; model.transform.SetParent(holder.transform, false);
            ApplyDecoMaterials(model, deco, glow);
            var label = new GameObject("Nombre").AddComponent<TextMesh>(); label.transform.SetParent(holder.transform, false);
            label.text = mapa.ToUpperInvariant(); label.anchor = TextAnchor.MiddleCenter; label.characterSize = .3f; label.fontSize = 60; label.color = Color.white;
            label.transform.localPosition = new Vector3(0, .05f, 6.2f); label.transform.localRotation = Quaternion.Euler(90, 180, 0);
            int extras = 0;
            if (Actors.TryGetValue(mapa, out var list))
                foreach (var a in list) if (Actor(a.model, holder.transform, a.pos, a.yaw, a.state, a.outfit, seed++) != null) extras++;
            int tris = model.GetComponentsInChildren<MeshFilter>().Sum(m => m.sharedMesh ? m.sharedMesh.triangles.Length / 3 : 0);
            show.items.Add(new AssetShowroom.Item { name = "Mapa " + mapa, folder = DecoDir, animator = null, root = model.transform, states = new string[0], triangles = tris, bones = 0, height = 4.2f });
            log.Add($"Diorama {mapa}: {tris} tris, {extras} personajes");
        }
    }

    public static string Build()
    {
        if (EditorApplication.isPlaying) return "FAIL detén Play primero";
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) return "FAIL la escena abierta tiene cambios sin guardar: " + active.path;
        Directory.CreateDirectory(Gen); Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

        var entries = new List<(string name, string dir)>();
        foreach (var root in Roots)
            foreach (var d in Directory.GetDirectories(root).Select(p => p.Replace('\\', '/') + "/").OrderBy(p => p))
            {
                string n = Path.GetFileName(d.TrimEnd('/'));
                if (File.Exists(d + n + ".fbx")) entries.Add((n, d));
            }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.62f, .68f, .78f); RenderSettings.ambientEquatorColor = new Color(.5f, .5f, .52f); RenderSettings.ambientGroundColor = new Color(.28f, .26f, .25f);

        var sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(42, 150, 0);
        var rim = new GameObject("Contraluz").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = .45f; rim.color = new Color(.75f, .85f, 1f);
        rim.transform.rotation = Quaternion.Euler(25, -30, 0);

        var camGo = new GameObject("Camara"); var cam = camGo.AddComponent<Camera>(); camGo.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.2f, .23f, .28f); cam.fieldOfView = 40f;
        camGo.AddComponent<AudioListener>();

        float spacing = 4.5f, startX = -(entries.Count - 1) * spacing * .5f;
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder); floor.name = "Suelo";
        floor.transform.localScale = new Vector3(Mathf.Max(entries.Count * spacing + 10f, 80f), .05f, 50f); floor.transform.position = new Vector3(0, -.05f, -10f);
        floor.GetComponent<Renderer>().sharedMaterial = Solid("Suelo.mat", new Color(.36f, .38f, .42f));
        var pedMat = Solid("Pedestal.mat", new Color(.55f, .5f, .45f));

        var show = new GameObject("Probador").AddComponent<AssetShowroom>(); show.cam = cam;
        var log = new List<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            var (name, dir) = entries[i];
            string main = dir + name + ".fbx";
            PrepareImport(main, false);
            var animFiles = Directory.GetFiles(dir, name + "@*.fbx").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            foreach (var a in animFiles) PrepareImport(a, true);

            // Un estado por clip, con el nombre del archivo (Nombre@Walk -> Walk), y "Pose T" para ver el modelo sin animar.
            string ctrlPath = Gen + name + "_Probador.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            var names = new List<string>();
            void Add(string st, Motion clip) { string s = st; int k = 2; while (names.Contains(s)) s = st + " " + k++; var state = sm.AddState(s); state.motion = clip; names.Add(s); }
            foreach (var c in AssetDatabase.LoadAllAssetsAtPath(main).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))) Add(c.name.Split('|').Last(), c);
            foreach (var a in animFiles)
            {
                string label = Path.GetFileNameWithoutExtension(a).Split('@').Last();
                var clip = AssetDatabase.LoadAllAssetsAtPath(a).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).OrderByDescending(c => c.length).FirstOrDefault();
                if (clip != null) Add(label, clip);
            }
            Add("Pose T", null);
            var idle = sm.states.Select(s => s.state).FirstOrDefault(s => s.name.ToLowerInvariant().Contains("idle"))
                    ?? sm.states.Select(s => s.state).FirstOrDefault(s => s.name.ToLowerInvariant().Contains("walk")) ?? sm.states[0].state;
            sm.defaultState = idle;
            names.Remove(idle.name); names.Insert(0, idle.name);

            var ped = new GameObject("Pedestal " + name); ped.transform.position = new Vector3(startX + i * spacing, 0, 0);
            var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder); top.name = "Base"; top.transform.SetParent(ped.transform, false);
            top.transform.localScale = new Vector3(2.4f, .08f, 2.4f); top.transform.localPosition = Vector3.up * .08f;
            top.GetComponent<Renderer>().sharedMaterial = pedMat; Object.DestroyImmediate(top.GetComponent<Collider>());
            var label3d = new GameObject("Nombre").AddComponent<TextMesh>(); label3d.transform.SetParent(ped.transform, false);
            label3d.text = name; label3d.anchor = TextAnchor.MiddleCenter; label3d.characterSize = .12f; label3d.fontSize = 48; label3d.color = Color.white;
            label3d.transform.localPosition = new Vector3(0, .2f, 1.35f); label3d.transform.localRotation = Quaternion.Euler(90, 180, 0);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(main));
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = name; model.transform.SetParent(ped.transform, false); model.transform.localPosition = Vector3.up * .16f;
            var anim = model.GetComponent<Animator>(); if (anim == null) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var mat = MaterialFor(dir, name);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();

            // Personaje modular (piezas Categoria_Opcion): ropa y colores intercambiables en Play.
            CharacterLook look = model.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Torso_")) ? model.AddComponent<CharacterLook>() : null;
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            int tris = smrs.Sum(s => s.sharedMesh ? s.sharedMesh.triangles.Length / 3 : 0) + model.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh ? f.sharedMesh.triangles.Length / 3 : 0);
            var b = PirateArenaVisuals.BoundsOf(model);
            if (look != null) { var body = model.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Cuerpo_") || r.name.StartsWith("Pies_")).ToArray(); b = body[0].bounds; foreach (var r in body) b.Encapsulate(r.bounds); }
            show.items.Add(new AssetShowroom.Item { name = name, folder = dir, animator = anim, root = model.transform, states = names.ToArray(), look = look, triangles = tris, bones = smrs.Length > 0 ? smrs.Max(s => s.bones.Length) : 0, height = b.size.y });
            log.Add($"{name}: {names.Count - 1} clips, {tris} tris, {b.size.y:0.00} m");
        }
        BuildDioramas(show, log);
        camGo.transform.position = new Vector3(0, 2, 8); camGo.transform.LookAt(Vector3.up);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        return $"OK {entries.Count} modelos → {ScenePath}\n" + string.Join("\n", log);
    }
}
#endif
