#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MultiverseHubBuilder
{
    [MenuItem("Multiverse/Crear mundo de inicio")]
    public static void Build()
    {
        const string arena="Assets/001_Scenes/ArenaDelCaos.unity";
        const string menu="Assets/001_Scenes/MundoDeInicio.unity";
        var source=EditorSceneManager.OpenScene(arena,OpenSceneMode.Single);
        var art=Object.FindFirstObjectByType<PirateArenaBootstrap>();
        if(art==null)throw new System.InvalidOperationException("ArenaDelCaos no tiene PirateArenaBootstrap.");
        var refs=new GameObject[]{art.anneModel,art.shipLarge,art.sharkModel,art.barrelModel,art.chestModel,art.palmModel,art.rockModel};
        var mats=new Material[]{art.wood,art.darkWood,art.brass,art.rope,art.cloth,art.foam,art.iron,art.water,art.atlas};
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var hub=new GameObject("MULTIVERSE PARTY 3D - Mundo de inicio").AddComponent<MultiverseHub>();
        hub.pirate=refs[0];hub.ship=refs[1];hub.shark=refs[2];hub.barrel=refs[3];hub.chest=refs[4];hub.palm=refs[5];hub.rock=refs[6];
        hub.wood=mats[0];hub.darkWood=mats[1];hub.brass=mats[2];hub.rope=mats[3];hub.cloth=mats[4];hub.foam=mats[5];hub.iron=mats[6];hub.water=mats[7];hub.atlas=mats[8];
        hub.zombie=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/002_Modelos/zombies/Characters-20261027T131325Z-1-001/Characters/FBX/Zombie_Basic.fbx");
        hub.monster=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/002_Modelos/mounstrous/Big-20261027T131625Z-1-001/Big/FBX/Dino.fbx");
        hub.zombieAtlas=AtlasMaterial("Hub_ZombieAtlas", "Assets/002_Modelos/zombies/Zombie_Atlas.png");
        hub.monsterAtlas=AtlasMaterial("Hub_MonsterAtlas", "Assets/002_Modelos/mounstrous/Atlas_Monsters.png");
        hub.BuildWorld();
        EditorSceneManager.SaveScene(scene,menu);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(menu,true),new EditorBuildSettingsScene(arena,true)};
        AssetDatabase.SaveAssets();
        Debug.Log("Mundo de inicio listo; escena inicial del proyecto actualizada.");
    }

    private static Material AtlasMaterial(string name, string texturePath)
    {
        string path="Assets/003_Scripts/PirateArena/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        EditorUtility.SetDirty(material);
        return material;
    }
}
#endif
