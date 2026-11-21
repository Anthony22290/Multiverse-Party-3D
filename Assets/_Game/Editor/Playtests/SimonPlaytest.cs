using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Memoria Musical · pruebas. En Play dentro de MemoriaMusical:
///   run SimonPlaytest.Run    → partida completa con 6 bots (también en tu hueco), fotos; se lee con Report.
///   run SimonPlaytest.Report → resultado de la última Run.
///   run SimonPlaytest.Card   → carta de la fiesta (PartyCards/MemoriaMusical.png).
/// </summary>
public static class SimonPlaytest
{
    const string Dir = "Docs/Claude_Capturas/MemoriaMusical/";
    static SimonDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != SimonDirector.Phase.Finished ? " · ahora " + Now() : "");

    static async Task<string> RunAll()
    {
        SimonDirector.AutoHuman = true;
        await Until(() => (d = Object.FindAnyObjectByType<SimonDirector>()) != null && d.Players.Count > 0, 10);
        d.Human.IsBot = true;
        var sb = new StringBuilder();
        await Until(() => d.State != SimonDirector.Phase.Waiting && d.State != SimonDirector.Phase.Countdown, 15);
        var start = DateTime.UtcNow; int shots = 0;
        while (d.State != SimonDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 230)
        {
            double el = (DateTime.UtcNow - start).TotalSeconds;
            if (shots < 3 && el > 3 + shots * 15) { shots++; Shot("Partida_" + shots); }
            await Task.Delay(40);
        }
        bool done = d.State == SimonDirector.Phase.Finished;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} {(done ? d.Result : "no terminó")} · {(DateTime.UtcNow - start).TotalSeconds:0} s");
        sb.AppendLine("  " + Now());
        sb.AppendLine((d.Round >= 4 ? "PASS" : "FAIL") + " llega a la ronda 4 (" + d.Round + ")");
        sb.AppendLine((d.Players.Count(p => p.Lives < SimonDirector.StartLives) > 0 ? "PASS" : "FAIL") + " alguien falla");
        await Task.Delay(1200); Shot("Final");
        SimonDirector.AutoHuman = false;
        return sb.ToString();
    }

    static string Now()
    {
        return $"ronda {d.Round} ({d.Length} notas) {d.State} · " + string.Join(" · ", d.Ranking().Select(p => p.DisplayName + " " + p.Lives + "v " + d.TotalTime[p.Slot].ToString("0.0") + "s"));
    }

    public static async Task<string> Card()
    {
        d = Object.FindAnyObjectByType<SimonDirector>(); if (d == null) return "FAIL no hay SimonDirector";
        await Until(() => d.State == SimonDirector.Phase.Show && d.Round >= 2 && d.Elapsed > d.ShowStart + d.Step * 1.2f, 90);
        string path = "Assets/_Game/Party/Resources/PartyCards/MemoriaMusical.png";
        ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
