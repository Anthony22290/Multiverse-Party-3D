using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using UnityEngine.AI; using Object = UnityEngine.Object;

/// <summary>
/// Arma tu Nave · prueba con bots (también en tu hueco). En Play dentro de ArmaTuNave:
///   run NavePlaytest.Map  → escondites alcanzables por la NavMesh y fotos del pueblo.
///   run NavePlaytest.Run  → partida completa a x2 con resumen.
///   run NavePlaytest.Card → imagen de la carta de la fiesta (PartyCards/Nave.png).
/// </summary>
public static class NavePlaytest
{
    const string Dir = "Docs/Claude_Capturas/Nave/";
    static NaveDirector d;
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(20); }
    static void Shot(string name, Camera cam = null) { Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(cam != null ? cam : d.View.GetComponent<Camera>(), Path.GetFullPath(Dir + name + ".png"), 1280, 720); }
    static void Free(string name, Vector3 from, Vector3 at)
    {
        var go = new GameObject("Cámara de fotos"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>()); cam.fieldOfView = 55;
        go.transform.position = from; go.transform.LookAt(at); Shot(name, cam); Object.DestroyImmediate(go);
    }

    public static async Task<string> Map()
    {
        await Until(() => (d = Object.FindAnyObjectByType<NaveDirector>()) != null && d.Players.Count > 0, 10);
        var sb = new StringBuilder();
        var start = NaveVillage.OnMesh(d.SpawnPoint(0), 3f); var path = new NavMeshPath();
        int ok = 0; var bad = new System.Collections.Generic.List<string>();
        foreach (var p in d.Pieces)
        {
            bool reach = NavMesh.CalculatePath(start, p.transform.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            if (reach) ok++; else bad.Add(p.name + " " + p.transform.position);
        }
        sb.AppendLine($"{(ok == d.Pieces.Count ? "PASS" : "FAIL")} piezas alcanzables {ok}/{d.Pieces.Count} · escondites {d.Village.Spots.Count}" + (bad.Count > 0 ? " · " + string.Join(" | ", bad.Take(5)) : ""));
        float minD = float.MaxValue; for (int a = 0; a < d.Pieces.Count; a++) for (int b = a + 1; b < d.Pieces.Count; b++) minD = Mathf.Min(minD, Vector3.Distance(d.Pieces[a].transform.position, d.Pieces[b].transform.position));
        float minSame = float.MaxValue; for (int a = 0; a < d.Pieces.Count; a++) for (int b = a + 1; b < d.Pieces.Count; b++) if (d.Pieces[a].Type == d.Pieces[b].Type) minSame = Mathf.Min(minSame, Vector3.Distance(d.Pieces[a].transform.position, d.Pieces[b].transform.position));
        sb.AppendLine($"INFO separación mínima entre piezas {minD:0.0} m · entre piezas iguales {minSame:0.0} m · primera pieza en {d.Pieces[0].transform.position}");
        int padsOk = d.Ships.Count(s => NavMesh.CalculatePath(start, NaveVillage.OnMesh(s.transform.position + (Vector3.zero - s.transform.position).normalized * 2.6f, 3f), NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete);
        sb.AppendLine($"{(padsOk == d.Ships.Count ? "PASS" : "FAIL")} plataformas alcanzables {padsOk}/{d.Ships.Count}");
        Free("1_Pueblo", new Vector3(0, 48, -46), new Vector3(0, 0, 2));
        Free("2_Plaza_naves", new Vector3(-12, 7, -14), new Vector3(0, 1, 0));
        var h = d.Village.Houses[1];
        d.Village.FadeRoofs(h.Center);
        Free("3_Casa_por_dentro", h.Center + Vector3.up * 9f + (Vector3.zero - h.Center).normalized * 6f, h.Center);
        d.Village.FadeRoofs(Vector3.one * 999f);
        Free("4_Calle", new Vector3(14, 2.2f, -12), new Vector3(24, 1.5f, -24));
        return sb.ToString();
    }

    public static async Task<string> Run()
    {
        await Until(() => (d = Object.FindAnyObjectByType<NaveDirector>()) != null && d.Players.Count > 0, 10);
        if (d.Human.GetComponent<NaveBot>() == null) d.Human.gameObject.AddComponent<NaveBot>();
        var sb = new StringBuilder();
        await Until(() => d.State == NaveDirector.Phase.Running, 12);
        Time.timeScale = 2f;
        await Until(() => d.Elapsed > 20f, 30); Shot("5_Buscando");
        int at60 = 0; await Until(() => d.Elapsed > 60f || d.State == NaveDirector.Phase.Finished, 40); at60 = d.Ships.Sum(s => s.Count); Shot("6_Radar");
        await Until(() => d.State == NaveDirector.Phase.Finished, 120);
        Time.timeScale = 1f; await Task.Delay(2500); Shot("7_Despegue");
        sb.AppendLine($"{(d.State == NaveDirector.Phase.Finished && d.WinnerSlot >= 0 ? "PASS" : "FAIL")} terminó a los {d.Elapsed:0} s · {d.Result} · piezas montadas a los 60 s: {at60}");
        sb.AppendLine($"INFO empujones {d.Players.Sum(p => p.Shoves)} · piezas sueltas {d.Pieces.Count(p => p.Is == NavePiece.State.Free)} · llevadas {d.Pieces.Count(p => p.Is == NavePiece.State.Carried)}");
        foreach (var p in d.Ranking()) sb.AppendLine($"  {p.DisplayName}: {d.Ships[p.Slot].Count}/5 · empujones {p.Shoves}");
        return sb.ToString();
    }

    public static string Card()
    {
        d = Object.FindAnyObjectByType<NaveDirector>(); if (d == null) return "FAIL no hay NaveDirector";
        var go = new GameObject("Cámara carta"); var cam = go.AddComponent<Camera>(); cam.CopyFrom(d.View.GetComponent<Camera>()); cam.fieldOfView = 50;
        var s = d.Ships[0].transform.position; go.transform.position = s * 1.9f + Vector3.up * 5f + Vector3.Cross(Vector3.up, s.normalized) * 4f; go.transform.LookAt(s * .9f + Vector3.up * 1f);
        string path = "Assets/_Game/Party/Resources/PartyCards/Nave.png";
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(path), 1600, 900); Object.DestroyImmediate(go);
        Directory.CreateDirectory(Dir); File.Copy(Path.GetFullPath(path), Path.GetFullPath(Dir + "Carta.png"), true);
        return "OK " + path;
    }
}
