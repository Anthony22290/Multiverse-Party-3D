using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class PirateToolPackages
{
    private static readonly string[] Packages =
    {
        "com.unity.cinemachine",
        "com.unity.animation.rigging",
        "com.unity.probuilder",
        "com.unity.memoryprofiler"
    };

    private static AddAndRemoveRequest request;
    private static double deadline;

    [MenuItem("Multiverse/Install Recommended Tools")]
    public static void Install()
    {
        Debug.Log("[PirateToolPackages] Adding: " + string.Join(", ", Packages));
        request = Client.AddAndRemove(Packages, new string[0]);
        deadline = EditorApplication.timeSinceStartup + 600;
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (request == null) return;
        if (!request.IsCompleted)
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= Poll;
                Debug.LogError("[PirateToolPackages] Timed out waiting for Package Manager.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
            return;
        }
        EditorApplication.update -= Poll;
        if (request.Status == StatusCode.Success)
        {
            Debug.Log("[PirateToolPackages] Installed: " +
                string.Join(", ", request.Result.Select(p => p.name + "@" + p.version)));
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("[PirateToolPackages] Failed: " + request.Error?.message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
        request = null;
    }
}
