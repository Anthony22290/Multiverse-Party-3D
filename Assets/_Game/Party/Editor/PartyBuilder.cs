#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Editor setup for the party flow: menu scene, hub art references, card images and the network prefab.</summary>
public static class PartyBuilder
{
    const string Root = "Assets/_Game/Party";
    const string CardsFolder = Root + "/Resources/PartyCards";
    const string MenuScene = "Assets/001_Scenes/MenuPrincipal.unity";
    const string HubScene = "Assets/001_Scenes/MundoDeInicio.unity";
    static readonly string[] GameScenes =
    {
        "Assets/001_Scenes/ArenaDelCaos.unity", "Assets/001_Scenes/InvasionZombi.unity", "Assets/001_Scenes/CarrerasLocas.unity",
        "Assets/001_Scenes/CodigoMortal.unity", "Assets/001_Scenes/SalaDeEscape.unity"
    };

    [MenuItem("Multiverse/Partida/Preparar todo")]
    public static string PrepareAll()
    {
        string a = PrepareNetwork();
        string b = CopyPlaceholderCards();
        string c = PrepareHub();
        string d = BuildMenuScene();
        string e = BuildSettings();
        return string.Join("\n", a, b, c, d, e);
    }

    [MenuItem("Multiverse/Partida/Preparar partida en línea")]
    public static string PrepareNetwork()
    {
        Directory.CreateDirectory(Root + "/Resources");
        string path = Root + "/Resources/MultiversePartyNet.prefab";
        var go = new GameObject("MultiversePartyNet");
        go.AddComponent<NetworkObject>(); go.AddComponent<MultiversePartyNet>();
        new NetworkObjectBakerEditTime().Bake(go);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SetLabels(prefab, new[] { "FusionPrefab" });
        NetworkProjectConfigImporter.RebuildPrefabHash();
        AssetDatabase.SaveAssets();
        return "Prefab de red listo: " + path;
    }

    static string CopyPlaceholderCards()
    {
        Directory.CreateDirectory(CardsFolder);
        string docs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Claude_Capturas"));
        (string name, string src)[] cards =
        {
            ("Arena", "Arena_Archipielago.png"), ("Zombie", "Minijuegos/R1_Monstruos.png"), ("Race", "Carreras/51_Pueblo.png"),
            ("Mortal", "CodigoMortal_Vista_General.png"), ("Escape", "Escape/Torre_completa.png")
        };
        int copied = 0;
        foreach (var (name, src) in cards)
        {
            string dst = CardsFolder + "/" + name + ".png";
            if (File.Exists(dst)) continue;
            string from = Path.Combine(docs, src);
            if (!File.Exists(from)) continue;
            File.Copy(from, dst); copied++;
        }
        AssetDatabase.Refresh();
        foreach (var (name, _) in cards) ConfigureCard(CardsFolder + "/" + name + ".png");
        return "Imágenes de cartas: " + copied + " copiadas";
    }

    public static void ConfigureCard(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default; importer.mipmapEnabled = true; importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    static GameObject Art(string cat, string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Prefabs/" + cat + "/" + name + ".prefab");

    [MenuItem("Multiverse/Partida/Construir Mundo de Inicio")]
    public static string PrepareHub()
    {
        var scene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
        var hub = Object.FindAnyObjectByType<MultiverseHub>();
        if (hub == null) return "ERROR: no hay MultiverseHub en MundoDeInicio";
        // The new plaza is built at runtime; drop the old saved island and stray roots.
        int removed = 0;
        for (int i = hub.transform.childCount - 1; i >= 0; i--) { Object.DestroyImmediate(hub.transform.GetChild(i).gameObject); removed++; }
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == hub.gameObject || root.GetComponent<Camera>() != null) continue;
            if (root.name == "EventSystem" || root.GetComponent<UnityEngine.Rendering.Volume>() != null) continue;
            Object.DestroyImmediate(root); removed++;
        }
        hub.houses = new[] { Art("Hub", "Casa Roja"), Art("Hub", "Casa Azul"), Art("Hub", "Casa Turquesa") }.Where(x => x != null).ToArray();
        hub.flags = new[] { "Bandera Roja", "Bandera Turquesa", "Bandera Amarilla", "Bandera Morada", "Bandera Verde", "Bandera Rosa" }.Select(n => Art("Comun", n)).Where(x => x != null).ToArray();
        hub.banners = new[] { "Estandarte Piratas", "Estandarte Zombis", "Estandarte Carreras", "Estandarte Codigo", "Estandarte Criaturas" }.Select(n => Art("Hub", n)).Where(x => x != null).ToArray();
        hub.fountain = Art("Hub", "Fuente"); hub.lighthouse = Art("Hub", "Faro del multiverso"); hub.kiosk = Art("Hub", "Kiosco info");
        hub.lamp = Art("Comun", "Farola"); hub.garland = Art("Comun", "Guirnalda"); hub.bench = Art("Comun", "Banco");
        hub.tree = Art("Comun", "Arbol redondo"); hub.pine = Art("Comun", "Pino"); hub.bush = Art("Comun", "Arbusto");
        hub.planter = Art("Comun", "Jardinera"); hub.cloud = Art("Comun", "Nube"); hub.signpost = Art("Comun", "Poste indicador");
        EditorUtility.SetDirty(hub);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "Mundo de Inicio preparado (" + removed + " objetos antiguos quitados)";
    }

    [MenuItem("Multiverse/Partida/Construir menú principal")]
    public static string BuildMenuScene()
    {
        var hubScene = EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
        var hub = Object.FindAnyObjectByType<MultiverseHub>();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cam.tag = "MainCamera";
        var cameraData = cam.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        var menu = new GameObject("MULTIVERSE PARTY 3D - Menú principal").AddComponent<MultiverseMainMenu>();
        if (hub != null)
        {
            menu.atlas = hub.atlas; menu.palm = hub.palm; menu.rock = hub.rock; menu.ship = hub.ship; menu.barrel = hub.barrel; menu.chest = hub.chest;
            menu.wood = hub.wood; menu.darkWood = hub.darkWood; menu.water = hub.water; menu.brass = hub.brass;
        }
        EditorSceneManager.SaveScene(scene, MenuScene);
        return "Menú principal guardado en " + MenuScene;
    }

    public static string BuildSettings()
    {
        var list = new[] { MenuScene, HubScene }.Concat(GameScenes).Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
        EditorBuildSettings.scenes = list;
        AssetDatabase.SaveAssets();
        return "Build Settings: " + string.Join(", ", list.Select(s => Path.GetFileNameWithoutExtension(s.path)));
    }
}
#endif
