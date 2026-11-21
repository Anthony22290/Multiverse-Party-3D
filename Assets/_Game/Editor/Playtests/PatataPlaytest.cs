using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Patata Caliente · pruebas. En Play dentro de PatataCaliente:
///   run PatataPlaytest.Run    → partida completa con bots (también en tu hueco), fotos de cada ronda; se lee con Report.
///   run PatataPlaytest.Report → resultado de la última Run.
///   run PatataPlaytest.Card   → carta de la fiesta (PartyCards/Patata.png).
/// </summary>
public static class PatataPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Patata/";
    static PatataDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(30); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != PatataDirector.Phase.Finished ? $" · ahora ronda {d.Round + 1} fase {d.State} quedan {d.AliveCount}" : "");

    static async Task<string> RunAll()
    {
        await Until(() => (d = Object.FindAnyObjectByType<PatataDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human.GetComponent<PatataBot>() == null) d.Human.gameObject.AddComponent<PatataBot>();
        var sb = new StringBuilder();
        await Until(() => d.State == PatataDirector.Phase.Playing, 15);
        var start = DateTime.UtcNow; int shotRound = -1, passes = 0, lastHolder = -1; float maxRound = 0f;
        var rounds = new StringBuilder();
        int roundPasses = 0; float roundStart = 0f;
        while (d.State != PatataDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 400)
        {
            if (d.State == PatataDirector.Phase.Playing)
            {
                if (d.Round != shotRound && d.RoundTime > 4f) { shotRound = d.Round; Shot("Ronda_" + (d.Round + 1)); }
                if (d.HolderSlot != lastHolder && lastHolder >= 0 && d.HolderSlot >= 0) { passes++; roundPasses++; }
                lastHolder = d.HolderSlot; maxRound = Mathf.Max(maxRound, d.RoundTime);
                // Nobody may stand outside the fence.
                foreach (var p in d.Players) if (p.Alive && new Vector2(p.transform.position.x, p.transform.position.z).magnitude > d.Arena.Radius + .3f) sb.AppendLine($"FAIL {p.DisplayName} fuera de la valla ({new Vector2(p.transform.position.x, p.transform.position.z).magnitude:0.0} > {d.Arena.Radius:0.0})");
            }
            else if (d.State == PatataDirector.Phase.Boom && lastHolder >= 0)
            {
                rounds.AppendLine($"  ronda {d.Round}: mecha {d.Fuse:0.0} s · {roundPasses} pases · explota {d.Players[d.Out.Last()].DisplayName} · valla {d.Arena.Radius:0.0} m");
                roundPasses = 0; lastHolder = -1;
            }
            await Task.Delay(50);
        }
        sb.AppendLine($"{(d.State == PatataDirector.Phase.Finished ? "PASS" : "FAIL")} {(d.State == PatataDirector.Phase.Finished ? d.Result : "no terminó: fase " + d.State)} · {(DateTime.UtcNow - start).TotalSeconds:0} s · {passes} pases en total");
        sb.Append(rounds);
        sb.AppendLine((passes >= d.Rounds ? "PASS" : "FAIL") + $" la patata pasa de mano ({passes} pases en {d.Rounds} rondas)");
        sb.AppendLine((d.Out.Count == d.Rounds && d.Out.Distinct().Count() == d.Out.Count ? "PASS" : "FAIL") + $" eliminados {d.Out.Count} de {d.Players.Count} (uno por ronda)");
        int i = 1; foreach (var p in d.Ranking()) sb.AppendLine($"  {i++}. {p.DisplayName}");
        await Task.Delay(1500); Shot("Final");
        return sb.ToString();
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<PatataDirector>(); if (d == null) return "FAIL no hay PatataDirector";
        var go = new GameObject("Cámara carta"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>()); cam.fieldOfView = 46;
        go.transform.position = new Vector3(0f, 9.5f, -14.5f); go.transform.LookAt(new Vector3(0, .5f, -1.5f));
        string path = "Assets/_Game/Party/Resources/PartyCards/Patata.png";
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(path), 1600, 900); Object.DestroyImmediate(go);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path + " · fase " + d.State;
    }
}
