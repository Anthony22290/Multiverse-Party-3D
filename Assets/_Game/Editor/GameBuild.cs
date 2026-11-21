#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Build del juego para Windows (2026-11-18). Deja la ventana en pantalla completa (sin bordes, a la resolución del
/// monitor), redimensionable si se pasa a ventana, y con F11 / Alt+Enter para cambiar. Crea la carpeta
/// Builds/MultiverseParty3D_AAAA-MM-DD y un .zip al lado para pasarlo a otros PCs.
/// Menú: Multiverse → Build → Crear build de Windows.
/// </summary>
public static class GameBuild
{
    [MenuItem("Multiverse/Build/Crear build de Windows")]
    static void Menu() => Debug.Log(Build());

    public static string Build()
    {
        // 2026-11-21 · URP prepares the shader prefiltering of the ACTIVE platform before building: building for
        // Windows while the editor is on Android would strip the PC shaders (same bug that left the first APK without 3D).
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            return "FAIL el editor estaba en otra plataforma: cambiando a Windows. Cuando termine, vuelve a crear la build.";
        }
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.runInBackground = true;
        PlayerSettings.visibleInBackground = true;
        AssetDatabase.SaveAssets();

        string name = "MultiverseParty3D_" + DateTime.Now.ToString("yyyy-MM-dd");
        string folder = Path.Combine("Builds", name), exe = Path.Combine(folder, "MultiverseParty3D.exe");
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = exe, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        string line = $"{r.summary.result} · {r.summary.totalTime.TotalSeconds:0} s · errores {r.summary.totalErrors} · {scenes.Length} escenas · {r.summary.totalSize / 1048576f:0} MB";
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) return "FAIL " + line;
        foreach (var junk in Directory.GetDirectories(folder, "*DoNotShip")) Directory.Delete(junk, true);
        string zip = folder + ".zip"; if (File.Exists(zip)) File.Delete(zip);
        ZipFile.CreateFromDirectory(folder, zip, System.IO.Compression.CompressionLevel.Optimal, true);
        return "OK " + line + " · " + Path.GetFullPath(exe) + " · zip " + new FileInfo(zip).Length / 1048576 + " MB";
    }
}
#endif
