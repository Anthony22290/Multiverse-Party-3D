#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;

public static class PirateNetworkBuilder
{
    [MenuItem("Multiverse/Prepare Pirate Multiplayer")]
    public static void Prepare()
    {
        const string path = "Assets/003_Scripts/PirateArena/Resources/PirateNetworkWorld.prefab";
        var root = new GameObject("PirateNetworkWorld");
        root.AddComponent<NetworkObject>(); root.AddComponent<PirateNetworkWorld>();
        new NetworkObjectBakerEditTime().Bake(root);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        AssetDatabase.SetLabels(prefab, new[] { "FusionPrefab" });
        NetworkProjectConfigImporter.RebuildPrefabHash();
        AssetDatabase.SaveAssets();
        Debug.Log("Pirate multiplayer prefab prepared.");
    }
}
#endif
