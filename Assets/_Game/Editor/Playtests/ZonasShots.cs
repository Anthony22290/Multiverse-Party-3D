using System; using System.Collections.Generic; using System.IO; using System.Linq; using System.Reflection; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Invasión Zombi · sectores con edificios (2026-11-17). Ejecutar en Play dentro de InvasionZombi:
///   run ZonasShots.Run  → comprueba que cada edificio cargó, que cofres y cajas quedan libres y saca fotos de cada sector.
/// </summary>
public static class ZonasShots
{
    static SurvivalDirector d;
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(15); }
    const string Dir = "Docs/Claude_Capturas/Zombi_Zonas/";

    static void Shot(string name, Vector3 from, Vector3 at, float fov = 55)
    {
        var go = new GameObject("Cámara de fotos"); var cam = go.AddComponent<Camera>();
        var main = d.View.GetComponent<Camera>(); cam.CopyFrom(main); cam.fieldOfView = fov; cam.farClipPlane = 400;
        go.transform.position = from; go.transform.LookAt(at);
        Directory.CreateDirectory(Dir); ClaudeBridge.CaptureCamera(cam, Path.GetFullPath(Dir + name + ".png"), 1280, 720);
        Object.DestroyImmediate(go);
    }

    public static async Task<string> Run()
    {
        await Until(() => (d = Object.FindAnyObjectByType<SurvivalDirector>()) != null && d.HUD != null && d.Level != null, 10);
        var sb = new StringBuilder();
        string[] expected = { "Gasolinera_Techo", "Gasolinera_Surtidores", "Gasolinera_Tienda", "Gasolinera_Totem", "Gasolinera_Cisterna", "Desguace_Grua", "Desguace_Pila", "Desguace_Caseta",
                              "Almacen_Nave", "Almacen_Estanteria", "Almacen_Carretilla", "Bomberos_Cuartel", "Bomberos_Torre", "Bomberos_Camion" };
        var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        var missing = expected.Where(n => !all.Any(t => t.name == n)).ToList();
        sb.AppendLine((missing.Count == 0 ? "PASS" : "FAIL") + " edificios cargados" + (missing.Count > 0 ? ": faltan " + string.Join(", ", missing) : ""));

        // Nothing solid may sit on a cache, a crate or the area where the guards spawn.
        bool Blocked(Vector3 p, float r, Transform self, out string by)
        {
            by = "";
            foreach (var c in Physics.OverlapSphere(p + Vector3.up * 1f, r, ~0, QueryTriggerInteraction.Ignore))
            {
                if (self != null && c.transform.IsChildOf(self)) continue;
                if (c.GetComponentInParent<SurvivalCache>() != null || c.GetComponentInParent<SurvivalLootCrate>() != null || c.GetComponentInParent<CharacterController>() != null) continue;
                if (c.transform.name == "Suelo" || c.GetComponentInParent<SurvivalPlayer>() != null || c.GetComponentInParent<SurvivalZombie>() != null) continue;
                by = c.transform.root == c.transform ? c.name : c.transform.parent != null ? c.transform.parent.name + "/" + c.name : c.name; return true;
            }
            return false;
        }
        foreach (var cache in d.Level.Caches)
        {
            var issues = new List<string>();
            if (Blocked(cache.Position, 1.6f, cache.transform, out var by)) issues.Add("cofre tapado por " + by);
            int ring = 0; for (int i = 0; i < 12; i++) { var p = cache.Position + Quaternion.Euler(0, i * 30, 0) * Vector3.forward * 3.2f; if (Blocked(p, .5f, null, out _)) ring++; }
            sb.AppendLine((issues.Count == 0 && ring <= 4 ? "PASS" : "FAIL") + $" {cache.SectorName}: {(issues.Count == 0 ? "cofre libre" : string.Join(", ", issues))} · {12 - ring}/12 huecos libres alrededor");
        }
        int crateBad = 0; foreach (var c in d.Level.Crates) if (Blocked(c.transform.position, .9f, c.transform, out var by)) { crateBad++; sb.AppendLine("  caja tapada por " + by + " en " + c.transform.position); }
        sb.AppendLine((crateBad == 0 ? "PASS" : "FAIL") + $" cajas de suministros libres {d.Level.Crates.Count - crateBad}/{d.Level.Crates.Count}");

        // Photos: overview and one per sector, plus one from the player's camera height.
        Shot("0_Vista_general", new Vector3(0, 70, -62), new Vector3(0, 0, 4), 60);
        Shot("1_Gasolinera", new Vector3(14, 9, -12), new Vector3(30, 2, -30));
        Shot("2_Desguace", new Vector3(14, 10, 14), new Vector3(31, 3, 31));
        Shot("3_Almacen", new Vector3(-14, 9, 16), new Vector3(-31, 2.5f, 31));
        Shot("4_Bomberos", new Vector3(-16, 9, -14), new Vector3(-31, 3, -32));
        Shot("5_Gasolinera_suelo", new Vector3(19, 2.2f, -18), new Vector3(30, 2.2f, -32), 62);
        Shot("6_Bomberos_suelo", new Vector3(-22, 2.2f, -18), new Vector3(-31, 3f, -34), 62);
        sb.AppendLine("fotos en " + Dir);
        return sb.ToString();
    }
}
