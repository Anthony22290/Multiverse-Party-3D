using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Pico, Pala y Tijera · prueba con bots (también en tu hueco). En Play dentro de PicoPalaTijera:
///   run MinaPlaytest.Run   → carrera completa, fotos y resumen por jugador.
///   run MinaPlaytest.Card  → imagen de la carta de la fiesta (PartyCards/Mina.png).
///   run MinaPlaytest.Wrong → comprueba que la herramienta equivocada aturde y no rompe el bloque.
/// </summary>
public static class MinaPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Mina/";
    static MinaDirector d;
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static async Task<string> Wrong()
    {
        await Until(() => (d = Object.FindAnyObjectByType<MinaDirector>()) != null && d.Miners.Count > 0, 10);
        await Until(() => d.State == MinaDirector.Phase.Running, 12);
        var h = d.Human; var sb = new StringBuilder();
        var need = MinaKit.Needed(h.Next); var wrong = (MinaKit.Tool)(((int)need + 1) % 3);
        if (h.Next == MinaKit.Block.Oro) return "SKIP el primer bloque es oro";
        h.SetTool(wrong); h.TryMine();
        bool ok1 = h.Stunned && h.Depth == 0 && h.Mistakes == 1;
        sb.AppendLine($"{(ok1 ? "PASS" : "FAIL")} {wrong} sobre {h.Next}: aturdido={h.Stunned} profundidad={h.Depth}");
        await Until(() => !h.Stunned, 2); await Task.Delay(200);
        h.SetTool(need); int before = h.Depth; h.TryMine(); if (h.Hits > 0) { await Task.Delay(200); h.TryMine(); }
        sb.AppendLine($"{(h.Depth == before + 1 ? "PASS" : "FAIL")} {need}: rompe el bloque ({before}→{h.Depth})");
        return sb.ToString();
    }

    public static async Task<string> Run()
    {
        await Until(() => (d = Object.FindAnyObjectByType<MinaDirector>()) != null && d.Miners.Count > 0, 10);
        if (d == null) return "FAIL no hay MinaDirector";
        if (d.Human.GetComponent<MinaBot>() == null) d.Human.gameObject.AddComponent<MinaBot>();
        var sb = new StringBuilder();
        sb.AppendLine($"INFO jugadores {d.Miners.Count} · bloques {d.Seq.Length}: " + string.Join(" ", Enum.GetValues(typeof(MinaKit.Block)).Cast<MinaKit.Block>().Select(b => b + "×" + d.Seq.Count(x => x == b))));
        await Until(() => d.State == MinaDirector.Phase.Countdown, 5); await Task.Delay(1200); Shot("1_Superficie");
        await Until(() => d.State == MinaDirector.Phase.Running, 8);
        await Until(() => d.Elapsed > 5f, 10); Shot("2_Cavando");
        await Until(() => d.Elapsed > 14f || d.State == MinaDirector.Phase.Finished, 15); Shot("3_Profundo");
        await Until(() => d.State == MinaDirector.Phase.Finished, 140);
        await Task.Delay(1500); Shot("4_Tesoro");
        bool done = d.State == MinaDirector.Phase.Finished && d.WinnerSlot >= 0;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} terminó a los {d.Elapsed:0.0} s · {d.Result}");
        int clicks = d.Miners.Sum(m => m.Clicks), mistakes = d.Miners.Sum(m => m.Mistakes);
        sb.AppendLine($"{(mistakes > 0 && clicks > d.Seq.Length ? "PASS" : "FAIL")} clics {clicks} · fallos de herramienta {mistakes}");
        foreach (var m in d.Ranking()) sb.AppendLine($"  {m.DisplayName}: {(m.Finished ? "tesoro a los " + m.FinishedAt.ToString("0.0") + " s" : m.Depth + " m")} · fallos {m.Mistakes} · clics {m.Clicks}");
        return sb.ToString();
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<MinaDirector>(); if (d == null) return "FAIL no hay MinaDirector";
        string path = "Assets/_Game/Party/Resources/PartyCards/Mina.png";
        ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
