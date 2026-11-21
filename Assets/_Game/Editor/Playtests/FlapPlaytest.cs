using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Propulsores · prueba con bots (también en tu hueco). En Play dentro de Propulsores:
///   run FlapPlaytest.Run  → carrera completa con fotos y resumen.
///   run FlapPlaytest.Card → imagen de la carta de la fiesta (PartyCards/Propulsores.png).
/// </summary>
public static class FlapPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Propulsores/";
    static FlapDirector d;
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(d.Cam, Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static async Task<string> Run()
    {
        await Until(() => (d = Object.FindAnyObjectByType<FlapDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human.GetComponent<FlapBot>() == null) d.Human.gameObject.AddComponent<FlapBot>();
        var sb = new StringBuilder(); FlapPlayer.Debugging = true;
        await Until(() => d.State == FlapDirector.Phase.Countdown, 6); await Task.Delay(1200); Shot("1_Salida");
        await Until(() => d.State == FlapDirector.Phase.Running, 8);
        await Until(() => d.Elapsed > 8f, 15); Shot("2_Volando");
        await Until(() => d.Elapsed > 25f || d.State == FlapDirector.Phase.Finished, 25); Shot("3_Carrera");
        await Until(() => d.State == FlapDirector.Phase.Finished, 160);
        await Task.Delay(1500); Shot("4_Final");
        sb.AppendLine($"{(d.State == FlapDirector.Phase.Finished && d.WinnerSlot >= 0 ? "PASS" : "FAIL")} terminó a los {d.Elapsed:0} s · {d.Result}");
        int crashes = d.Players.Sum(p => p.Crashes);
        bool livesOk = d.Players.All(p => p.Crashes <= FlapDirector.StartLives && (p.Crashes < FlapDirector.StartLives || p.Dead));
        sb.AppendLine($"{(livesOk ? "PASS" : "FAIL")} vidas: nadie choca más de {FlapDirector.StartLives} veces · eliminados {d.Players.Count(p => p.Dead)} · choques {crashes}");
        foreach (var p in d.Ranking()) sb.AppendLine($"  {p.DisplayName}: {p.Score} pts · choques {p.Crashes} · {(p.Dead ? "fuera a los " + p.DeadAt.ToString("0") + " s" : "en el aire")}");
        return sb.ToString();
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<FlapDirector>(); if (d == null) return "FAIL no hay FlapDirector";
        string path = "Assets/_Game/Party/Resources/PartyCards/Propulsores.png";
        ClaudeBridge.CaptureCamera(d.Cam, Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
