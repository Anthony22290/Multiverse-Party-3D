#if UNITY_EDITOR
using System.IO; using System.Threading.Tasks; using UnityEngine;
public static class ShowroomShots
{
    public static async Task<string> Run()
    {
        var s = Object.FindAnyObjectByType<AssetShowroom>();
        if (s == null || !Application.isPlaying) return "FAIL ProbadorDeAssets no está en Play";
        string dir = Path.GetFullPath("Docs/Claude_Capturas/Probador"); Directory.CreateDirectory(dir);
        var t = typeof(AssetShowroom); var f = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Sel(int i) => t.GetMethod("Select", f).Invoke(s, new object[] { i });
        await Task.Delay(1500);
        for (int i = 0; i < s.items.Count; i++) { Sel(i); await Task.Delay(1400); ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{i}_{s.items[i].name}.png")); await Task.Delay(300); }
        // Personajes modulares: varios disfraces al azar con distintas animaciones.
        int shots = 0; string[] it_anim;
        foreach (var it in s.items)
        {
            if (it.look == null) continue;
            Sel(s.items.IndexOf(it));
            it_anim = it.name == "Piloto" ? new[] { "Kart_Idle", "Kart_Left", "Kart_Boost", "Kart_Spin", "Kart_Throw", "Kart_Win" } : new[] { "Dance", "Victory", "Punch", "Run", "Wave", "Jump" };
            for (int k = 0; k < 6; k++)
            {
                it.look.Randomize();
                int st = System.Array.IndexOf(it.states, it_anim[k]); if (st >= 0) t.GetMethod("PlayState", f).Invoke(s, new object[] { it, st });
                await Task.Delay(900); ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{it.name}_disfraz_{k}.png")); shots++; await Task.Delay(300);
            }
        }
        return $"PASS {s.items.Count} modelos fotografiados, {shots} disfraces aleatorios, en {dir}";
    }
}
#endif
