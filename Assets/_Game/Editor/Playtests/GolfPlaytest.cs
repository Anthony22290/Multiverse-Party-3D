using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Minigolf Loco · pruebas. En Play dentro de MinigolfLoco:
///   run GolfPlaytest.Run  → los 10 hoyos con bots (también en tu hueco) a x3, tarjeta y fotos.
///   run GolfPlaytest.Card → carta de la fiesta (PartyCards/Minigolf.png).
/// </summary>
public static class GolfPlaytest
{
    const string Dir = "Docs/Claude_Capturas/Minigolf/";
    static GolfDirector d;
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(30); }
    static void Shot(string name) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath(Dir + name + ".png"), 1280, 720); }

    static string report = "";
    /// <summary>Starts the full run in the background (it takes several minutes); read it with Report.</summary>
    public static string Run() { report = "en marcha"; _ = RunAll().ContinueWith(t => report = t.IsFaulted ? "ERROR " + t.Exception.GetBaseException() : t.Result); return "iniciado"; }
    public static string Report() => report + (d != null && d.State != GolfDirector.Phase.Finished ? $" · ahora hoyo {d.HoleIndex + 1} fase {d.State} t={d.PhaseTime:0}" : "");

    static async Task<string> RunAll()
    {
        await Until(() => (d = Object.FindAnyObjectByType<GolfDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human.GetComponent<GolfBot>() == null) d.Human.gameObject.AddComponent<GolfBot>();
        var sb = new StringBuilder();
        foreach (var h in d.Course.Holes) if (h.Path.Count < 2) sb.AppendLine("FAIL hoyo " + h.Name + " sin camino de la salida al hoyo");
        await Until(() => d.State == GolfDirector.Phase.Playing, 15); Time.timeScale = 5f;
        int shotHole = -1;
        var start = DateTime.UtcNow;
        while (d.State != GolfDirector.Phase.Finished && (DateTime.UtcNow - start).TotalSeconds < 900)
        {
            if (d.State == GolfDirector.Phase.Playing && d.HoleIndex != shotHole && d.PhaseTime > 4f) { shotHole = d.HoleIndex; Time.timeScale = 1f; await Task.Delay(100); Shot((d.HoleIndex + 1).ToString("00") + "_" + d.CurrentHole.Name.Replace(" ", "_")); Time.timeScale = 5f; }
            await Task.Delay(100);
        }
        Time.timeScale = 1f;
        sb.AppendLine($"{(d.State == GolfDirector.Phase.Finished ? "PASS" : "FAIL")} {(d.State == GolfDirector.Phase.Finished ? d.Result : "no terminó: hoyo " + (d.HoleIndex + 1) + " fase " + d.State)}");
        for (int h = 0; h < GolfDirector.Holes; h++)
        {
            var row = Enumerable.Range(0, d.Players.Count).Select(s => d.Card[s, h]).ToArray();
            int finished = row.Count(v => v > 0 && v <= GolfPlayer.MaxStrokes);
            sb.AppendLine($"  hoyo {h + 1} ({GolfCourse.Layouts[h].name}, par {GolfCourse.Layouts[h].par}): {string.Join(" ", row)} · media {(row.Where(v => v > 0).DefaultIfEmpty(0).Average()):0.0}");
        }
        foreach (var p in d.Ranking()) sb.AppendLine($"  {p.DisplayName}: total {d.Total(d.Players.IndexOf(p))}");
        return sb.ToString();
    }

    /// <summary>Rolls test balls on flat grass at several speeds and reports how far they go (to tune the bots).</summary>
    public static async Task<string> Measure()
    {
        d = Object.FindAnyObjectByType<GolfDirector>(); var src = d.Players[0];
        var floorMat = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Select(c => c.sharedMaterial).First(m => m != null && m.name.StartsWith("Césped"));
        var root = new GameObject("Medida"); var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform);
        floor.transform.position = new Vector3(0, -60.5f, 30); floor.transform.localScale = new Vector3(60, 1, 120); floor.GetComponent<Collider>().sharedMaterial = floorMat;
        float[] speeds = { 1f, 2f, 3f, 4f, 6f, 8f, 10f, 13f };
        var balls = new Rigidbody[speeds.Length]; var col0 = src.Ball.GetComponent<SphereCollider>();
        for (int i = 0; i < speeds.Length; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.transform.SetParent(root.transform); b.transform.localScale = Vector3.one * GolfPlayer.BallRadius * 2f;
            b.transform.position = new Vector3(-25 + i * 6, -60f + GolfPlayer.BallRadius, 0); b.GetComponent<Collider>().sharedMaterial = col0.sharedMaterial;
            var rb = b.AddComponent<Rigidbody>(); rb.mass = src.Body.mass; rb.linearDamping = src.Body.linearDamping; rb.angularDamping = src.Body.angularDamping; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            balls[i] = rb;
        }
        await Task.Delay(300);
        var start = balls.Select(b => b.position).ToArray();
        for (int i = 0; i < speeds.Length; i++) balls[i].linearVelocity = Vector3.forward * speeds[i];
        await Task.Delay(1000); var at1 = balls.Select(b => b.linearVelocity.magnitude).ToArray();
        await Until(() => balls.All(b => b.linearVelocity.magnitude < .03f), 25);
        var sb = new StringBuilder("medida (v0 → distancia, v a 1 s):\n");
        for (int i = 0; i < speeds.Length; i++) sb.AppendLine($"  {speeds[i]:0.0} → {(balls[i].position - start[i]).z:0.00} m · v1s={at1[i]:0.00}");
        Object.Destroy(root); return sb.ToString();
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<GolfDirector>(); if (d == null) return "FAIL no hay GolfDirector";
        var h = d.Course.Holes[3];
        var go = new GameObject("Cámara carta"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>()); cam.fieldOfView = 50;
        go.transform.position = h.Origin + new Vector3(-6f, 6f, -4f); go.transform.LookAt(h.Origin + new Vector3(0, 0, 6f));
        string path = "Assets/_Game/Party/Resources/PartyCards/Minigolf.png";
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(path), 1600, 900); Object.DestroyImmediate(go);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
