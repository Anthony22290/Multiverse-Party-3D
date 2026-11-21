using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Reparto Loco · pruebas. En Play dentro de RepartoLoco:
///   run RepartoPlaytest.Run    → partida completa con bots (también en tu hueco), fotos; se lee con Report.
///   run RepartoPlaytest.Report → resultado de la última Run.
///   run RepartoPlaytest.Card   → carta de la fiesta (PartyCards/Reparto.png).
/// </summary>
public static class RepartoPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Reparto/";
    static RepartoDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(30); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != RepartoDirector.Phase.Finished ? $" · ahora {d.State} t={d.Elapsed:0} s entregas={string.Join(",", d.Scores ?? new int[0])}" : "");

    static async Task<string> RunAll()
    {
        await Until(() => (d = Object.FindAnyObjectByType<RepartoDirector>()) != null && d.Players.Count > 0, 10);
        RepartoDirector.LogHits = true;
        if (d.Human.GetComponent<RepartoBot>() == null) d.Human.gameObject.AddComponent<RepartoBot>();
        var sb = new StringBuilder();
        await Until(() => d.State == RepartoDirector.Phase.Playing, 15);
        { var go = new GameObject("Cámara puesto"); var c = go.AddComponent<Camera>(); c.CopyFrom(d.View.GetComponent<Camera>()); c.fieldOfView = 50;
          go.transform.position = RepartoRoad.Stall + new Vector3(0, 6.5f, 7.5f); go.transform.LookAt(RepartoRoad.Stall + Vector3.up * .8f);
          Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(c, Path.GetFullPath(Dir + "Puesto.png"), 1280, 720); Object.DestroyImmediate(go); }
        var start = DateTime.UtcNow; bool[] shots = new bool[3]; float maxZ = 0f; bool outside = false;
        while (d.State != RepartoDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 130)
        {
            for (int i = 0; i < shots.Length; i++) if (!shots[i] && d.Elapsed > 10f + i * 28f) { shots[i] = true; Shot($"Partida_{i + 1}_{d.Elapsed:0}s"); }
            foreach (var p in d.Players)
            {
                maxZ = Mathf.Max(maxZ, p.transform.position.z);
                if (Mathf.Abs(p.transform.position.x) > RepartoRoad.HalfWidth + 1f || p.transform.position.z < -7f || p.transform.position.z > 35f) outside = true;
            }
            await Task.Delay(60);
        }
        bool done = d.State == RepartoDirector.Phase.Finished;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} {(done ? d.Result : "no terminó: " + d.State)} · {(DateTime.UtcNow - start).TotalSeconds:0} s");
        sb.AppendLine((d.Scores.Sum() > 0 ? "PASS" : "FAIL") + " entregas: " + string.Join(" · ", d.Ranking().Select(p => $"{p.DisplayName} {d.Scores[p.Slot]} (rotos {d.Breaks[p.Slot]})")));
        sb.AppendLine((d.Breaks.Sum() > 0 ? "PASS" : "AVISO") + $" hay atropellos ({d.Breaks.Sum()} pedidos rotos)");
        sb.AppendLine((!outside ? "PASS" : "FAIL") + $" nadie sale de la calle (z máx {maxZ:0.0})");
        await Task.Delay(1200); Shot("Final");
        return sb.ToString();
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<RepartoDirector>(); if (d == null) return "FAIL no hay RepartoDirector";
        var go = new GameObject("Cámara carta"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>()); cam.fieldOfView = 46;
        go.transform.position = new Vector3(-9f, 10f, -3f); go.transform.LookAt(new Vector3(1f, 0, 10f));
        string path = "Assets/_Game/Party/Resources/PartyCards/Reparto.png";
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(path), 1600, 900); Object.DestroyImmediate(go);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path + " · fase " + d.State;
    }
}
