#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// APK de Android (2026-11-21). Menú: Multiverse → Build → Crear APK de Android.
/// Horizontal, IL2CPP ARM64, internet, texturas ASTC, calidad "Mobile" (ya asignada a Android en Quality).
/// Deja Builds/MultiverseParty3D_Android_AAAA-MM-DD.apk para pasarlo por WhatsApp / Drive.
/// Construir cambia la plataforma del editor a Android; <see cref="BackToWindows"/> la devuelve.
/// Same room network as the PC build (MultiverseParty.OnlineSettings): phones and PCs play together.
/// </summary>
public static class AndroidBuild
{
    public const string PackageName = "com.anthonyrv.multiverseparty3d";

    [MenuItem("Multiverse/Build/Crear APK de Android")]
    static void Menu()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log(SwitchToAndroid());
            EditorUtility.DisplayDialog("APK de Android", "Primero hay que cambiar el editor a Android. Cuando termine, pulsa otra vez Multiverse → Build → Crear APK de Android.", "Vale");
            return;
        }
        Debug.Log(Build());
    }

    [MenuItem("Multiverse/Build/Volver a Windows")]
    static void MenuBack() => Debug.Log(BackToWindows());

    public static void Configure()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, PackageName);
        PlayerSettings.companyName = "AnthonyRV";
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Minimal);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.Android.bundleVersionCode = Mathf.Max(PlayerSettings.Android.bundleVersionCode, 1) + 1;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.runInBackground = true;
        EditorUserBuildSettings.buildAppBundle = false;                         // APK to send directly (not Google Play)
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        EditorUserBuildSettings.development = false;
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Step 1. URP rebuilds the "shader prefiltering" data of the ACTIVE platform's quality levels before a build.
    /// Building for Android while the editor was on Windows left Mobile_RPAsset with stale data and URP stripped every
    /// variant of URP/Lit ForwardLit: on the phone nothing 3D was drawn (2026-11-21). So: switch first, then build.
    /// </summary>
    public static string SwitchToAndroid()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return "Ya está en Android";
        bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        return ok ? "Cambiando a Android… (espera a que termine y vuelve a crear el APK)" : "FAIL no se pudo cambiar a Android";
    }

    /// <summary>Recomputes URP's prefiltering data for the active platform and reports what the Mobile asset keeps.</summary>
    public static string RefreshShaderPrefiltering()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Rendering.Universal.ShaderBuildPreprocessor")).FirstOrDefault(t => t != null);
        var gather = type?.GetMethod("GatherShaderFeatures", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (gather == null) return "FAIL no se encontró ShaderBuildPreprocessor.GatherShaderFeatures";
        gather.Invoke(null, new object[] { false });
        AssetDatabase.SaveAssets();
        var mobile = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Settings/Mobile_RPAsset.asset");
        if (mobile == null) return "OK prefiltrado actualizado (no se encontró Mobile_RPAsset para comprobarlo)";
        var so = new SerializedObject(mobile);
        string V(string n) { var p = so.FindProperty(n); return p != null ? p.enumValueIndex.ToString() : "?"; }
        return $"OK prefiltrado Mobile: Forward+={V("m_PrefilteringModeForwardPlus")} (0 = quitar, el renderer móvil es Forward) · luces extra={V("m_PrefilteringModeAdditionalLight")} · sombras={V("m_PrefilteringModeMainLightShadows")}";
    }

    public static string Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            return "FAIL primero cambia a Android (AndroidBuild.SwitchToAndroid / menú Crear APK) y vuelve a intentarlo";
        Configure();
        string pre = RefreshShaderPrefiltering();
        if (pre.StartsWith("FAIL")) return pre;
        Directory.CreateDirectory("Builds");
        string apk = Path.Combine("Builds", "MultiverseParty3D_Android_" + DateTime.Now.ToString("yyyy-MM-dd") + ".apk");
        if (File.Exists(apk)) File.Delete(apk);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = apk, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None
        });
        string line = $"{r.summary.result} · {r.summary.totalTime.TotalSeconds:0} s · errores {r.summary.totalErrors} · avisos {r.summary.totalWarnings} · {scenes.Length} escenas";
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            var errors = r.steps.SelectMany(s => s.messages).Where(m => m.type == LogType.Error || m.type == LogType.Exception).Select(m => m.content).Distinct().Take(8);
            return "FAIL " + line + "\n" + string.Join("\n", errors);
        }
        return "OK " + line + " · " + pre + " · " + Path.GetFullPath(apk) + " · " + new FileInfo(apk).Length / 1048576 + " MB";
    }

    public static string BackToWindows()
    {
        bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        return ok ? "Plataforma activa: Windows" : "FAIL no se pudo volver a Windows";
    }
}
#endif
