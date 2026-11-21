using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Pintura Loca · pruebas. En Play dentro de PinturaLoca:
///   run PinturaPlaytest.Run    → partida completa con 6 bots (también en tu hueco), fotos; se lee con Report.
///   run PinturaPlaytest.Report → resultado de la última Run.
///   run PinturaPlaytest.Card   → carta de la fiesta (PartyCards/PinturaLoca.png).
/// </summary>
public static class PinturaPlaytest
{
    const string Dir = "Docs/Claude_Capturas/PinturaLoca/";
    static PinturaDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != PinturaDirector.Phase.Finished ? " · ahora " + Now() : "");

    static async Task<string> RunAll()
    {
        PinturaDirector.AutoHuman = true;
        await Until(() => (d = Object.FindAnyObjectByType<PinturaDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human != null && d.Human.GetComponent<PinturaBot>() == null) { d.Human.gameObject.AddComponent<PinturaBot>(); d.Human.BotControlled = true; }
        var sb = new StringBuilder();
        await Until(() => d.State != PinturaDirector.Phase.Waiting && d.State != PinturaDirector.Phase.Countdown, 15);
        var start = DateTime.UtcNow; int shots = 0;
        while (d.State != PinturaDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 110)
        {
            double el = (DateTime.UtcNow - start).TotalSeconds;
            if (shots < 3 && el > 8 + shots * 25) { shots++; Shot("Partida_" + shots); }
            await Task.Delay(40);
        }
        bool done = d.State == PinturaDirector.Phase.Finished;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} {(done ? d.Result : "no terminó")} · {(DateTime.UtcNow - start).TotalSeconds:0} s");
        sb.AppendLine("  " + Now());
        sb.AppendLine((d.Counts.Sum() > 200 ? "PASS" : "FAIL") + " se pinta el suelo (" + d.Counts.Sum() + ")");
        sb.AppendLine((d.Counts.Count(c => c > 10) >= Mathf.Min(4, d.Players.Count) ? "PASS" : "FAIL") + " todos pintan");
        await Task.Delay(1200); Shot("Final");
        PinturaDirector.AutoHuman = false;
        return sb.ToString();
    }

    static string Now()
    {
        int total = d.Counts.Sum();
        return $"{d.Elapsed:0} s · pintadas {total}/{PinturaDirector.N * PinturaDirector.N} · cubos {d.Buckets.Count} · " + string.Join(" · ", d.Ranking().Select(p => p.DisplayName + " " + d.Counts[p.Slot]));
    }

    public static async Task<string> Card()
    {
        d = Object.FindAnyObjectByType<PinturaDirector>(); if (d == null) return "FAIL no hay PinturaDirector";
        await Until(() => d.State == PinturaDirector.Phase.Playing && d.Elapsed > 30f, 90);
        string path = "Assets/_Game/Party/Resources/PartyCards/PinturaLoca.png";
        ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
