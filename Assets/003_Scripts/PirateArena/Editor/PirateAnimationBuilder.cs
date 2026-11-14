using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Rebuildable, weapon-free character prefabs from the project's original rigged glTF assets.</summary>
public static class PirateAnimationBuilder
{
    const string Source = "Assets/002_Modelos/piratas/glTF/";
    const string Output = "Assets/003_Scripts/PirateArena/Animations";
    const string Prefabs = "Assets/003_Scripts/PirateArena/Resources/PirateCharacters";
    public static readonly string[] Characters = { "Characters_Anne", "Characters_Captain_Barbarossa",
        "Characters_Henry", "Characters_Mako", "Characters_Skeleton" };
    static readonly string[] Clips = { "Idle", "Walk", "Run", "Jump", "Jump_Idle", "Jump_Land", "HitReact", "Death", "Wave", "Duck" };

    [MenuItem("Multiverse/Piratas/Preparar personajes animados")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Detén Play antes de preparar los assets.");
        EnsureFolder(Output); EnsureFolder(Prefabs);
        Material atlas = AssetDatabase.LoadAssetAtPath<Material>("Assets/003_Scripts/PirateArena/Materials/Pirate_Atlas_URP.mat");
        if (atlas == null) throw new InvalidOperationException("Falta Pirate_Atlas_URP.mat");
        foreach (string character in Characters) BuildCharacter(character, atlas);
        AssetDatabase.SaveAssets();
        Debug.Log("[PirateAnimation] Listos 5 piratas con rig, 10 clips cada uno y sin armas.");
    }

    static void BuildCharacter(string character, Material atlas)
    {
        string sourcePath = Source + character + ".gltf";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null) throw new InvalidOperationException("glTF aún no importado: " + sourcePath);
        AnimationClip[] imported = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().ToArray();
        if (imported.Length == 0) throw new InvalidOperationException("No hay clips: " + character);
        string directory = Output + "/" + character;
        EnsureFolder(directory);
        string controllerPath = directory + "/Pirate.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (string name in Clips)
        {
            AnimationClip original = imported.FirstOrDefault(c => c.name == name);
            if (original == null) throw new InvalidOperationException(character + " no tiene " + name);
            string clipPath = directory + "/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            EditorUtility.CopySerialized(original, clip);
            clip.name = name; clip.legacy = false;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Idle" || name == "Walk" || name == "Run" || name == "Jump_Idle" || name == "Wave";
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            // Translation of the visual rig's root must never move the gameplay body or drift away from its capsule.
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if ((binding.path.EndsWith("/Root") || binding.path == "Root") &&
                    (binding.propertyName == "m_LocalPosition.x" || binding.propertyName == "m_LocalPosition.z"))
                {
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float start = curve.Evaluate(0f);
                    AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, clip.length, start));
                }
            }
            AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
            if (state == null) state = machine.AddState(name);
            state.motion = clip; state.writeDefaultValues = true;
            if (name == "Idle") machine.defaultState = state;
            EditorUtility.SetDirty(clip);
        }
        EditorUtility.SetDirty(controller);

        GameObject instance = UnityEngine.Object.Instantiate(source);
        try
        {
            instance.name = character;
            // The source includes swords/axes parented to fingers. Remove their visual objects from this minigame only.
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
                if (child != null && child.name.StartsWith("Weapon_", StringComparison.OrdinalIgnoreCase))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (Animation legacy in instance.GetComponentsInChildren<Animation>()) UnityEngine.Object.DestroyImmediate(legacy);
            Animator animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            AnimationClip idle = machine.defaultState.motion as AnimationClip;
            idle.SampleAnimation(animator.gameObject, 0f);
            PirateArenaVisuals.ApplyMaterial(instance, atlas);
            foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                renderer.updateWhenOffscreen = true;
            PrefabUtility.SaveAsPrefabAsset(instance, Prefabs + "/" + character + ".prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
