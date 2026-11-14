using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Resources/SurvivalMonsters: animated prefabs for the new enemy types (monster pack + pirate skeleton),
// each with an Animator whose states use the zombie names (Idle, Walk, Run, Punch, HitReact, Death).
public static class SurvivalMonsterSetup
{
    const string Out = "Assets/003_Scripts/ZombieSurvival/Resources/SurvivalMonsters/";
    const string Mon = "Assets/002_Modelos/mounstrous/";
    static readonly (string name, string fbx, string[] map)[] Monsters =
    {
        // state order: Idle, Walk, Run, Punch, HitReact, Death (clip name suffixes)
        ("Lanzabombas", Mon + "Big-20261027T131625Z-1-001/Big/FBX/Tribal.fbx", new[] { "Idle", "Walk", "Run", "Weapon", "HitReact", "Death" }),
        ("Blob", Mon + "Blob-20261027T131626Z-1-001/Blob/FBX/GreenBlob.fbx", new[] { "Idle", "Walk", "Walk", "Bite_Front", "HitRecieve", "Death" }),
        ("Fantasma", Mon + "Flying-20261027T131626Z-1-001/Flying/FBX/Ghost.fbx", new[] { "Flying_Idle", "Flying_Idle", "Fast_Flying", "Punch", "HitReact", "Death" }),
        ("Bruto", Mon + "Big-20261027T131625Z-1-001/Big/FBX/Yeti.fbx", new[] { "Idle", "Walk", "Run", "Punch", "HitReact", "Death" }),
    };
    static readonly string[] States = { "Idle", "Walk", "Run", "Punch", "HitReact", "Death" };

    [MenuItem("Multiverse/Invasión Zombi/Preparar monstruos")]
    static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        System.IO.Directory.CreateDirectory(Out);
        var log = new List<string>();
        // Material with the monster atlas.
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Mon + "Atlas_Monsters.png");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Out + "Monstruos.mat");
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, Out + "Monstruos.mat"); }
        mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; mat.SetFloat("_Smoothness", .15f); EditorUtility.SetDirty(mat);
        foreach (var (name, fbx, map) in Monsters)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic) { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
            var clipsInfo = importer.defaultClipAnimations;
            if (importer.clipAnimations == null || importer.clipAnimations.Length == 0 || changed)
            {
                foreach (var c in clipsInfo) { string n = c.name.Split('|').Last(); c.loopTime = n == "Idle" || n == "Walk" || n == "Run" || n.Contains("Flying"); }
                importer.clipAnimations = clipsInfo; changed = true;
            }
            if (changed) importer.SaveAndReimport();
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToList();
            string ctrlPath = Out + name + ".controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            for (int i = 0; i < States.Length; i++)
            {
                var clip = clips.FirstOrDefault(c => c.name.EndsWith("|" + map[i]) || c.name == map[i]);
                var st = sm.AddState(States[i]); st.motion = clip;
                if (i == 0) sm.defaultState = st;
                if (clip == null) log.Add(name + ": missing " + map[i]);
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var animator = inst.GetComponent<Animator>(); if (animator == null) animator = inst.AddComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            foreach (var r in inst.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            PrefabUtility.SaveAsPrefabAsset(inst, Out + name + ".prefab"); Object.DestroyImmediate(inst);
            log.Add(name + " ok (" + clips.Count + " clips)");
        }
        // Pirate skeleton: reuse its own clips; the attack is a lunging jump.
        var sk = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/003_Scripts/PirateArena/Resources/PirateCharacters/Characters_Skeleton.prefab");
        var pirateCtrl = sk.GetComponentInChildren<Animator>().runtimeAnimatorController;
        var pc = pirateCtrl.animationClips;
        string skPath = Out + "Esqueleto.controller"; AssetDatabase.DeleteAsset(skPath);
        var skc = AnimatorController.CreateAnimatorControllerAtPath(skPath);
        string[] skMap = { "Idle", "Walk", "Run", "Jump", "HitReact", "Death" };
        for (int i = 0; i < States.Length; i++) { var st = skc.layers[0].stateMachine.AddState(States[i]); st.motion = pc.FirstOrDefault(c => c.name == skMap[i]); if (i == 0) skc.layers[0].stateMachine.defaultState = st; }
        var skInst = (GameObject)PrefabUtility.InstantiatePrefab(sk);
        PrefabUtility.UnpackPrefabInstance(skInst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        skInst.GetComponentInChildren<Animator>().runtimeAnimatorController = skc;
        PrefabUtility.SaveAsPrefabAsset(skInst, Out + "Esqueleto.prefab"); Object.DestroyImmediate(skInst);
        var pirateMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/003_Scripts/PirateArena/Materials/Pirate_Atlas_URP.mat");
        if (pirateMat != null) { AssetDatabase.DeleteAsset(Out + "Pirata.mat"); AssetDatabase.CopyAsset("Assets/003_Scripts/PirateArena/Materials/Pirate_Atlas_URP.mat", Out + "Pirata.mat"); }
        log.Add("Esqueleto ok, pirate material " + (pirateMat != null));
        AssetDatabase.SaveAssets();
        return string.Join(" | ", log);
    }
}
