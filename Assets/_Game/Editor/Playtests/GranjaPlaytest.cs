using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Gallinas Locas · pruebas. En Play dentro de GallinasLocas:
///   run GranjaPlaytest.Run    → partida completa con bots (también en tu hueco), fotos; se lee con Report.
///   run GranjaPlaytest.Report → resultado de la última Run.
///   run GranjaPlaytest.Card   → carta de la fiesta (PartyCards/Gallinas.png).
/// </summary>
public static class GranjaPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Gallinas/";
    static GranjaDirector d; static string report = "";
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(30); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != GranjaDirector.Phase.Finished ? $" · ahora {d.State} t={d.Elapsed:0} s gallinas={d.Chickens.Count} puntos={string.Join(",", d.Scores ?? new int[0])}" : "");

    static async Task<string> RunAll()
    {
        await Until(() => (d = Object.FindAnyObjectByType<GranjaDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human.GetComponent<GranjaBot>() == null) d.Human.gameObject.AddComponent<GranjaBot>();
        var sb = new StringBuilder();
        await Until(() => d.State == GranjaDirector.Phase.Playing, 15);
        var start = DateTime.UtcNow; bool[] shots = new bool[4]; int maxChickens = 0, golds = 0, traps = 0, grabs = 0, lastCarried = 0, booms = 0, lastBoom = d.BoomSeq; bool escaped = false;
        var seen = new System.Collections.Generic.HashSet<int>();
        while (d.State != GranjaDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 140)
        {
            float t = d.Elapsed;
            for (int i = 0; i < shots.Length; i++) if (!shots[i] && t > 8f + i * 22f) { shots[i] = true; Shot($"Partida_{i + 1}_{t:0}s"); }
            maxChickens = Mathf.Max(maxChickens, d.Chickens.Count);
            foreach (var c in d.Chickens) if (seen.Add(c.Id)) { if (c.Type == GranjaChicken.Kind.Gold) golds++; if (c.Type == GranjaChicken.Kind.Trap) traps++; }
            int carried = d.Chickens.Count(c => c.Mode == GranjaChicken.State.Carried); if (carried > lastCarried) grabs += carried - lastCarried; lastCarried = carried;
            if (d.BoomSeq != lastBoom) { booms += d.BoomSeq - lastBoom; lastBoom = d.BoomSeq; }
            foreach (var c in d.Chickens)
            {
                if ((c.Mode == GranjaChicken.State.Penned || c.Mode == GranjaChicken.State.Ticking) && !d.Arena.Inside(c.Pen, c.transform.position, -.3f)) escaped = true;
                if (new Vector2(c.transform.position.x, c.transform.position.z).magnitude > GranjaArena.FieldRadius + 1f) escaped = true;
            }
            await Task.Delay(60);
        }
        bool done = d.State == GranjaDirector.Phase.Finished;
        sb.AppendLine($"{(done ? "PASS" : "FAIL")} {(done ? d.Result : "no terminó: " + d.State)} · {(DateTime.UtcNow - start).TotalSeconds:0} s");
        sb.AppendLine($"  gallinas a la vez: máx {maxChickens} · vistas {seen.Count} · doradas {golds} · trampa {traps} · cogidas ~{grabs} · explosiones {booms}");
        sb.AppendLine((d.Scores.Sum() > 0 ? "PASS" : "FAIL") + " se meten gallinas en los corrales: " + string.Join(" · ", d.Ranking().Select(p => $"{p.DisplayName} {d.Scores[p.Slot]}{(d.Golds[p.Slot] > 0 ? " ★" + d.Golds[p.Slot] : "")}{(d.Booms[p.Slot] > 0 ? " boom" + d.Booms[p.Slot] : "")}")));
        sb.AppendLine((golds == GranjaDirector.GoldTimes.Length ? "PASS" : "FAIL") + $" salen {golds} doradas");
        sb.AppendLine((!escaped ? "PASS" : "FAIL") + " ninguna gallina se escapa de un corral ni del patio");
        await Task.Delay(1200); Shot("Final");
        return sb.ToString();
    }

    /// <summary>Desde MenuPrincipal: fiesta sin conexión de 4 con solo Gallinas Locas; foto de los 4 corrales a los 25 s.</summary>
    public static async Task<string> Four()
    {
        MultiverseParty.ChosenPlaylist.Clear(); MultiverseParty.ChosenPlaylist.Add(PartyGames.Farm);
        GranjaDirector.AutoHuman = true;
        MultiverseParty.StartOffline(4);
        await Until(() => (d = Object.FindAnyObjectByType<GranjaDirector>()) != null && d.State == GranjaDirector.Phase.Playing, 40);
        if (d == null) return "FAIL no empezó";
        await Task.Delay(25000);
        var go = new GameObject("Cámara 4"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>()); cam.fieldOfView = 50;
        go.transform.position = new Vector3(0, 34f, -26f); go.transform.LookAt(Vector3.zero);
        Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(Dir + "Cuatro_corrales.png"), 1280, 720); Object.DestroyImmediate(go);
        string r = $"OK {d.Players.Count} jugadores · corrales {d.Arena.Pens} · puntos {string.Join(",", d.Scores)}";
        MultiverseParty.ChosenPlaylist.Clear(); GranjaDirector.AutoHuman = false;
        return r;
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<GranjaDirector>(); if (d == null) return "FAIL no hay GranjaDirector";
        var go = new GameObject("Cámara carta"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>()); cam.fieldOfView = 44;
        var pen = d.Arena.PenCenter[0];
        go.transform.position = pen * .45f + new Vector3(0, 9f, 0) - pen.normalized * 4f + Vector3.Cross(Vector3.up, pen.normalized) * 7f;
        go.transform.LookAt(pen * .7f);
        string path = "Assets/_Game/Party/Resources/PartyCards/Gallinas.png";
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(path), 1600, 900); Object.DestroyImmediate(go);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path + " · fase " + d.State;
    }
}
