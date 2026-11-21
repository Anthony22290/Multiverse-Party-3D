using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Rey de la Colina · pruebas. En Play dentro de ReyColina:
///   run ColinaPlaytest.Run    → partida completa con 6 bots (también en tu hueco), fotos; se lee con Report.
///   run ColinaPlaytest.Report → resultado de la última Run.
///   run ColinaPlaytest.Card   → carta de la fiesta (PartyCards/ReyColina.png).
/// </summary>
public static class ColinaPlaytest
{
    const string Dir = "Docs/Claude_Capturas/ReyColina/";
    static ColinaDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != ColinaDirector.Phase.Finished ? " · ahora " + Now() : "");

    static async Task<string> RunAll()
    {
        ColinaDirector.AutoHuman = true;
        await Until(() => (d = Object.FindAnyObjectByType<ColinaDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human != null && d.Human.GetComponent<ColinaBot>() == null) { d.Human.gameObject.AddComponent<ColinaBot>(); d.Human.BotControlled = true; }
        var sb = new StringBuilder();
        await Until(() => d.State != ColinaDirector.Phase.Waiting && d.State != ColinaDirector.Phase.Countdown, 15);
        var start = DateTime.UtcNow; int shots = 0;
        while (d.State != ColinaDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 110)
        {
            double el = (DateTime.UtcNow - start).TotalSeconds;
            if (shots < 3 && el > 6 + shots * 25) { shots++; Shot("Partida_" + shots); }
            await Task.Delay(40);
        }
        bool done = d.State == ColinaDirector.Phase.Finished;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} {(done ? d.Result : "no terminó")} · {(DateTime.UtcNow - start).TotalSeconds:0} s");
        sb.AppendLine("  " + Now());
        sb.AppendLine((d.Points.Sum() > 30 ? "PASS" : "FAIL") + " se suman puntos (" + d.Points.Sum().ToString("0") + ")");
        await Task.Delay(1200); Shot("Final");
        ColinaDirector.AutoHuman = false;
        return sb.ToString();
    }

    static string Now()
    {
        return $"{d.Elapsed:0} s · bananas {d.Bananas.Count} · " + string.Join(" · ", d.Ranking().Select(p => p.DisplayName + " " + Mathf.FloorToInt(d.Points[p.Slot]) + (p.Out ? " (agua)" : "")));
    }

    public static async Task<string> Card()
    {
        d = Object.FindAnyObjectByType<ColinaDirector>(); if (d == null) return "FAIL no hay ColinaDirector";
        await Until(() => d.State == ColinaDirector.Phase.Playing && d.Elapsed > 8f && d.Players.Count(p => d.InZone(p)) >= 2, 90);
        string path = "Assets/_Game/Party/Resources/PartyCards/ReyColina.png";
        ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
