using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Rodillos del Calabozo · prueba con bots (también en tu hueco). En Play dentro de RodillosDelCalabozo:
///   run RodillosPlaytest.Run   → partida completa a velocidad x2, fotos y resumen por jugador.
///   run RodillosPlaytest.Card  → imagen de la carta de la fiesta (PartyCards/Rodillos.png).
/// </summary>
public static class RodillosPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Rodillos/";
    static RodillosDirector d;
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static async Task<string> Run()
    {
        await Until(() => (d = Object.FindAnyObjectByType<RodillosDirector>()) != null && d.Runners.Count > 0, 10);
        if (d == null) return "FAIL no hay RodillosDirector";
        if (d.Human.GetComponent<RodillosBot>() == null) d.Human.gameObject.AddComponent<RodillosBot>();
        var sb = new StringBuilder(); RodillosBot.Planned = new int[2]; RodillosBot.Unplanned = new int[2]; RodillosBot.Debugging = true;
        sb.AppendLine($"INFO jugadores {d.Runners.Count} · losas {d.Arena.Tiles.Count} · semilla {d.Seed}");
        await Until(() => d.State == RodillosDirector.Phase.Countdown, 5); await Task.Delay(1500); Shot("1_Cuenta_atras");
        await Until(() => d.State == RodillosDirector.Phase.Running, 8);
        Time.timeScale = 2f;
        await Until(() => d.Elapsed > 9f, 20); Shot("2_Rodillo_bajo");
        await Until(() => d.Elapsed > 22f, 20); Shot("3_Los_dos_rodillos");
        int aliveAt40 = 0; await Until(() => d.Elapsed > 40f || d.State == RodillosDirector.Phase.Finished, 20); aliveAt40 = d.AliveCount;
        await Until(() => d.Elapsed > 55f || d.State == RodillosDirector.Phase.Finished, 20); Shot("4_Suelo_rompiendose");
        await Until(() => d.State == RodillosDirector.Phase.Finished, 120);
        Time.timeScale = 1f;
        await Task.Delay(1500); Shot("5_Final");
        bool finished = d.State == RodillosDirector.Phase.Finished;
        sb.AppendLine($"{(finished && d.WinnerSlot >= 0 ? "PASS" : "FAIL")} partida terminada en {d.Elapsed:0} s · {d.Result} · vivos a los 40 s: {aliveAt40}");
        int fallen = d.Arena.Tiles.Count(t => t.Fallen(d.Elapsed));
        sb.AppendLine($"{(d.Elapsed < RodillosDirector.CrumbleFrom || fallen > 0 ? "PASS" : "FAIL")} losas caídas: {fallen}");
        int jumps = d.Runners.Sum(r => r.Jumps), hits = d.Runners.Sum(r => r.Hits);
        sb.AppendLine($"{(jumps > 10 && hits > 0 ? "PASS" : "FAIL")} saltos {jumps} · golpes {hits}");
        sb.AppendLine($"INFO golpes bajo: {RodillosBot.Planned[0]} fallos a propósito + {RodillosBot.Unplanned[0]} de cálculo · alto: {RodillosBot.Planned[1]} + {RodillosBot.Unplanned[1]}");
        foreach (var r in d.Ranking())
            sb.AppendLine($"  {r.DisplayName}: {(r.Alive ? "en pie, " + r.Lives + " vidas" : "fuera a los " + r.EliminatedAt.ToString("0") + " s")} · saltos {r.Jumps} · golpes {r.Hits}");
        return sb.ToString();
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<RodillosDirector>(); if (d == null) return "FAIL no hay RodillosDirector";
        var go = new GameObject("Cámara carta"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>());
        cam.fieldOfView = 50; go.transform.position = new Vector3(-9.5f, 6.2f, -11.5f); go.transform.LookAt(new Vector3(1.5f, .6f, 0));
        string path = "Assets/_Game/Party/Resources/PartyCards/Rodillos.png";
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(path), 1600, 900);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        Object.DestroyImmediate(go);
        return "OK " + path;
    }
}
