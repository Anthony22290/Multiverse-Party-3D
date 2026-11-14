using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class BuildPiratePlayer
{
    public static string Run() => Build(true);
    public static string Release() => Build(false);
    private static string Build(bool development)
    {
        PirateNetworkBuilder.Prepare();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        string folder = development ? "Builds/PirateArena" : "Builds/MultiverseParty3D";
        Directory.CreateDirectory(folder);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { "Assets/001_Scenes/MundoDeInicio.unity", "Assets/001_Scenes/ArenaDelCaos.unity" },
            locationPathName = folder + "/MultiverseParty3D.exe",
            target = BuildTarget.StandaloneWindows64, options = development ? BuildOptions.Development : BuildOptions.None
        });
        string summary = result.summary.result + " errors=" + result.summary.totalErrors + " warnings=" + result.summary.totalWarnings;
        File.WriteAllText(development ? "Temp/PirateBuildResult.txt" : "Temp/PirateReleaseResult.txt", summary);
        return summary;
    }
}
