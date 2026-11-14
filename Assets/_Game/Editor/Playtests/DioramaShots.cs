#if UNITY_EDITOR
using System.IO; using System.Linq; using System.Threading.Tasks; using UnityEngine;
/// <summary>En Play en ProbadorDeAssets: recorre los dioramas de cada mapa y saca una foto de cada uno.</summary>
public static class DioramaShots
{
    public static async Task<string> Run()
    {
        var s = Object.FindAnyObjectByType<AssetShowroom>();
        if (s == null || !Application.isPlaying) return "FAIL ProbadorDeAssets no está en Play";
        string dir = Path.GetFullPath("Docs/Claude_Capturas/Probador/Dioramas"); Directory.CreateDirectory(dir);
        var t = typeof(AssetShowroom); var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        int n = 0; await Task.Delay(1200);
        for (int i = 0; i < s.items.Count; i++)
        {
            if (!s.items[i].name.StartsWith("Mapa")) continue;
            t.GetMethod("Select", f).Invoke(s, new object[] { i });
            t.GetField("pitch", f).SetValue(s, 34f); t.GetField("yaw", f).SetValue(s, 205f); t.GetField("dist", f).SetValue(s, 13.5f);
            await Task.Delay(1600);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, s.items[i].name.Replace(' ', '_') + ".png")); n++; await Task.Delay(400);
        }
        var actors = Object.FindObjectsByType<ShowroomActor>(FindObjectsSortMode.None);
        int animated = actors.Count(a => a.GetComponent<Animator>()?.runtimeAnimatorController != null);
        return $"{(n == 5 && animated == actors.Length ? "PASS" : "FAIL")} {n} dioramas fotografiados · {animated}/{actors.Length} personajes animados";
    }
}
#endif
