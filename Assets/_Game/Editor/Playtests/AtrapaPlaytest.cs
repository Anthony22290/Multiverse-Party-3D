using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// ¡Atrápalo! · pruebas. En Play dentro de Atrapalo:
///   run AtrapaPlaytest.Run    → partida completa con bots (también en tu hueco), fotos; se lee con Report.
///   run AtrapaPlaytest.Report → resultado de la última Run.
///   run AtrapaPlaytest.Card   → carta de la fiesta (PartyCards/Atrapalo.png), con una piña en el aire.
/// </summary>
public static class AtrapaPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Atrapalo/";
    static AtrapaDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != AtrapaDirector.Phase.Finished ? $" · ahora tiro {d.ThrowIndex + 1} {d.ThrowState}" : "");

    static async Task<string> RunAll()
    {
        AtrapaDirector.AutoHuman = true;
        await Until(() => (d = Object.FindAnyObjectByType<AtrapaDirector>()) != null && d.Players.Count > 0, 10);
        d.Human.IsBot = true;
        var sb = new StringBuilder();
        await Until(() => d.State == AtrapaDirector.Phase.Playing, 15);
        var start = DateTime.UtcNow; int lastThrow = -1, caught = 0, bad = 0, nobody = 0, badDodged = 0, shots = 0; bool counted = true;
        while (d.State != AtrapaDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 200)
        {
            if (d.ThrowIndex != lastThrow) { lastThrow = d.ThrowIndex; counted = false; }
            if (!counted && d.ThrowState == AtrapaDirector.Throw.Done)
            {
                counted = true;
                if (d.CatcherSlot >= 0) { caught++; if (AtrapaItem.Bad(d.Item)) bad++; } else { nobody++; if (AtrapaItem.Bad(d.Item)) badDodged++; }
            }
            if (shots < 3 && d.ThrowState != AtrapaDirector.Throw.Done && d.Elapsed > d.LaunchAt + .55f && d.ThrowIndex >= shots * 4 + 1) { shots++; Shot($"Tiro_{d.ThrowIndex + 1}_{AtrapaItem.Name(d.Item).Replace(" ", "_")}"); }
            await Task.Delay(30);
        }
        bool done = d.State == AtrapaDirector.Phase.Finished;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} {(done ? d.Result : "no terminó")} · {(DateTime.UtcNow - start).TotalSeconds:0} s · {d.ThrowIndex + 1} lanzamientos");
        sb.AppendLine($"  atrapados {caught} (malos {bad}) · nadie {nobody} (malos esquivados {badDodged})");
        sb.AppendLine("  " + string.Join(" · ", d.Ranking().Select(p => $"{p.DisplayName} {p.Score} pts {p.Lives}♥")));
        sb.AppendLine((caught > 5 ? "PASS" : "FAIL") + " se atrapan cosas");
        await Task.Delay(1200); Shot("Final");
        AtrapaDirector.AutoHuman = false;
        return sb.ToString();
    }

    public static async Task<string> Card()
    {
        d = Object.FindAnyObjectByType<AtrapaDirector>(); if (d == null) return "FAIL no hay AtrapaDirector";
        await Until(() => d.State == AtrapaDirector.Phase.Playing && d.ThrowState != AtrapaDirector.Throw.Done && d.Elapsed > d.LaunchAt + .45f && d.Elapsed < d.LaunchAt + .8f, 60);
        string path = "Assets/_Game/Party/Resources/PartyCards/Atrapalo.png";
        ClaudeBridge.CaptureCamera(Camera.main, Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path + " · objeto " + AtrapaItem.Name(d.Item);
    }
}
