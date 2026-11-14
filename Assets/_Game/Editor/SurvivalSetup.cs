using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Invasión Zombi: prepares the animated zombie prefabs (from the project's own zombie pack FBX files), the art set
/// with the pack's props/vehicles/weapons, and the scene Assets/001_Scenes/InvasionZombi.unity (added to the build list).
/// Safe to run again. Menu: Multiverse → Invasión Zombi → Crear o actualizar escena.
/// </summary>
public static class SurvivalSetup
{
    const string ScenePath = "Assets/001_Scenes/InvasionZombi.unity";
    const string Root = "Assets/003_Scripts/ZombieSurvival";
    const string ResourcesDir = Root + "/Resources";
    const string ZombiePrefabs = ResourcesDir + "/SurvivalZombies";
    const string AnimDir = Root + "/Animations";
    const string SetPath = ResourcesDir + "/SurvivalArtSet.asset";
    const string Pack = "Assets/002_Modelos/zombies/";
    const string Chars = Pack + "Characters-20261027T131325Z-1-001/Characters/FBX/";
    const string Env = Pack + "Environment-20261027T131325Z-1-001/Environment/FBX/";
    const string Veh = Pack + "Vehicles-20261027T131326Z-1-001/Vehicles/FBX/";
    const string Wpn = Pack + "Weapons-20261027T131327Z-1-001/Weapons/FBX/";
    const string AtlasPath = "Assets/003_Scripts/PirateArena/Materials/Hub_ZombieAtlas.mat";

    public static readonly string[] ZombieModels = { "Zombie_Basic", "Zombie_Ribcage", "Zombie_Chubby", "Zombie_Arm" };
    static readonly string[] Clips = { "Idle", "Walk", "Run", "Punch", "HitReact", "Death", "Idle_Attack", "Run_Attack", "Crawl", "Wave" };
    static readonly HashSet<string> Looping = new() { "Idle", "Walk", "Run", "Crawl", "Idle_Attack" };

    [MenuItem("Multiverse/Invasión Zombi/Crear o actualizar escena")]
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Detén Play antes de preparar la escena.");
        var report = new List<string>();
        Ensure(ResourcesDir); Ensure(ZombiePrefabs); Ensure(AnimDir);
        var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasPath);
        if (atlas == null) throw new InvalidOperationException("Falta " + AtlasPath);

        var set = AssetDatabase.LoadAssetAtPath<SurvivalArtSet>(SetPath);
        if (set == null) { set = ScriptableObject.CreateInstance<SurvivalArtSet>(); AssetDatabase.CreateAsset(set, SetPath); }
        set.atlas = atlas;
        set.zombies = ZombieModels.Select(m => BuildZombie(m, atlas, report)).ToArray();

        var props = new List<SurvivalArtSet.PropEntry>();
        void Prop(string key, string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null) props.Add(new SurvivalArtSet.PropEntry { key = key, prefab = go });
            else report.Add("Falta modelo: " + path);
        }
        foreach (var n in new[] { "Barrel", "Chest", "CinderBlock", "Container_Green", "Container_Red", "Couch", "FireHydrant", "Pallet",
                     "Pallet_Broken", "Pipes", "PlasticBarrier", "StreetLights", "TownSign", "TrafficBarrier_1", "TrafficBarrier_2", "TrafficCone_1",
                     "TrafficCone_2", "TrashBag_1", "TrashBag_2", "WaterTower", "Wheel", "Wheels_Stack" })
            Prop(n, Env + n + ".fbx");
        foreach (var n in new[] { "Vehicle_Pickup", "Vehicle_Pickup_Armored", "Vehicle_Sports", "Vehicle_Sports_Armored", "Vehicle_Truck", "Vehicle_Truck_Armored" })
            Prop(n, Veh + n + ".fbx");
        foreach (var n in new[] { "WoodenBat_Barbed", "WoodenBat_Saw", "Guitar", "Axe" })
            Prop(n, Wpn + n + ".fbx");
        set.props = props.ToArray();
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        report.Add($"Art set: {set.zombies.Count(z => z != null)} zombis animados, {set.props.Length} modelos");

        bool existed = File.Exists(ScenePath);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var moon = new GameObject("Luz de luna").AddComponent<Light>();
        moon.type = LightType.Directional; moon.intensity = .9f; moon.color = new Color(.78f, .86f, 1f);
        moon.shadows = LightShadows.Soft; moon.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
        camGo.transform.SetPositionAndRotation(new Vector3(0, 22, -30), Quaternion.Euler(38, 0, 0));
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .09f, .1f);
        new GameObject("Invasión Zombi").AddComponent<SurvivalDirector>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        report.Add((existed ? "Escena actualizada: " : "Escena creada: ") + ScenePath);

        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            report.Add("Añadida a Build Settings");
        }
        return string.Join("\n", report);
    }

    static GameObject BuildZombie(string model, Material atlas, List<string> report)
    {
        string fbx = Chars + model + ".fbx";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (source == null) { report.Add("Falta " + fbx); return null; }
        var imported = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        string dir = AnimDir + "/" + model;
        Ensure(dir);
        string controllerPath = dir + "/Zombie.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var machine = controller.layers[0].stateMachine;
        int found = 0;
        foreach (string name in Clips)
        {
            var original = imported.FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name));
            if (original == null) continue;
            string clipPath = dir + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            EditorUtility.CopySerialized(original, clip);
            clip.name = name; clip.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = Looping.Contains(name);
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            // The rig's root translation must never drag the mesh away from its capsule.
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                string last = binding.path.Contains("/") ? binding.path.Substring(binding.path.LastIndexOf('/') + 1) : binding.path;
                if (last == "Root" && (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, curve.Evaluate(0f)));
                }
            }
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
            state.motion = clip; state.writeDefaultValues = true;
            if (name == "Idle") machine.defaultState = state;
            EditorUtility.SetDirty(clip);
            found++;
        }
        EditorUtility.SetDirty(controller);
        if (found < 6) report.Add($"{model}: solo {found} clips (importados: {string.Join(", ", imported.Select(c => c.name))})");

        var instance = UnityEngine.Object.Instantiate(source);
        try
        {
            instance.name = model;
            foreach (var legacy in instance.GetComponentsInChildren<Animation>()) UnityEngine.Object.DestroyImmediate(legacy);
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (machine.defaultState != null && machine.defaultState.motion is AnimationClip idle) idle.SampleAnimation(animator.gameObject, 0f);
            PirateArenaVisuals.ApplyMaterial(instance, atlas);
            foreach (var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
            return PrefabUtility.SaveAsPrefabAsset(instance, ZombiePrefabs + "/" + model + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    static void Ensure(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
        Ensure(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
    }
}
