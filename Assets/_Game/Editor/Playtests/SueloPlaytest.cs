using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Suelo que se Cae · pruebas. En Play dentro de SueloQueCae:
///   run SueloPlaytest.Run    → partida completa con 6 bots (también en tu hueco), fotos; se lee con Report.
///   run SueloPlaytest.Report → resultado de la última Run.
///   run SueloPlaytest.Card   → carta de la fiesta (PartyCards/SueloQueCae.png).
/// </summary>
public static class SueloPlaytest
{
    const string Dir = "Docs/Claude_Capturas/SueloQueCae/";
    static SueloDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != SueloDirector.Phase.Finished ? " · ahora " + Now() : "");

    static async Task<string> RunAll()
    {
        SueloDirector.AutoHuman = true;
        await Until(() => (d = Object.FindAnyObjectByType<SueloDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human != null && d.Human.GetComponent<SueloBot>() == null) { d.Human.gameObject.AddComponent<SueloBot>(); d.Human.BotControlled = true; }
        var sb = new StringBuilder();
        await Until(() => d.State != SueloDirector.Phase.Waiting && d.State != SueloDirector.Phase.Countdown, 15);
        var start = DateTime.UtcNow; int shots = 0;
        while (d.State != SueloDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 170)
        {
            double el = (DateTime.UtcNow - start).TotalSeconds;
            if (shots < 3 && el > 4 + shots * 9) { shots++; Shot("Partida_" + shots); }
            await Task.Delay(40);
        }
        bool done = d.State == SueloDirector.Phase.Finished;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} {(done ? d.Result : "no terminó")} · {(DateTime.UtcNow - start).TotalSeconds:0} s");
        sb.AppendLine("  " + Now());
        sb.AppendLine((d.Tiles.Count(t => t.Gone) > 20 ? "PASS" : "FAIL") + " se rompen baldosas");
        sb.AppendLine((d.OutOrder.Count > 0 ? "PASS" : "FAIL") + " alguien cae a la lava (" + d.OutOrder.Count + ")");
        await Task.Delay(1200); Shot("Final");
        SueloDirector.AutoHuman = false;
        return sb.ToString();
    }

    static string Now()
    {
        return "rotas por piso " + string.Join("/", Enumerable.Range(0, 3).Select(f => d.Tiles.Count(t => t.Floor == f && t.CrackedAt >= 0f))) + $" · vivos {d.AliveCount}/{d.Players.Count} · baldosas caídas {d.Tiles.Count(t => t.Gone)}/{d.Tiles.Count} · {d.Elapsed:0} s · " + string.Join(" · ", d.Ranking().Select(p => p.DisplayName + (p.Out ? " fuera" : " piso " + (d.FloorOf(p.transform.position.y) + 1) + " y=" + p.transform.position.y.ToString("0.0") + " g=" + p.Grounded)));
    }

    public static async Task<string> Card()
    {
        d = Object.FindAnyObjectByType<SueloDirector>(); if (d == null) return "FAIL no hay SueloDirector";
        await Until(() => d.State == SueloDirector.Phase.Playing && d.Tiles.Count(t => t.Gone) > 25, 90);
        string path = "Assets/_Game/Party/Resources/PartyCards/SueloQueCae.png";
        ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
