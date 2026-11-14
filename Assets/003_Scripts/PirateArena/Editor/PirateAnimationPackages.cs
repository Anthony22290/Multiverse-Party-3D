using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

// One-time project setup, using UPM so transitive dependencies are resolved correctly.
public static class PirateAnimationPackages
{
    static AddRequest request;

    [MenuItem("Multiverse/Piratas/Instalar importador de animaciones")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || request != null) return;
        if (System.IO.File.ReadAllText("Packages/manifest.json").Contains("com.unity.cloud.gltfast")) return;
        if (SessionState.GetBool("PirateAnimationPackageAttempted", false)) return;
        SessionState.SetBool("PirateAnimationPackageAttempted", true);
        request = Client.Add("com.unity.cloud.gltfast");
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (request == null || !request.IsCompleted) return;
        EditorApplication.update -= Poll;
        if (request.Status == StatusCode.Success)
            Debug.Log("[PirateAnimation] Importador instalado: " + request.Result.packageId);
        else Debug.LogError("[PirateAnimation] " + request.Error.message);
        request = null;
    }
}
