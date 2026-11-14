using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Play-mode checks for the art passes: the real explorer walks every main route after decoration,
/// and no decorative collider sits on a path, road or gameplay volume. Run through ClaudeBridge while in Play.
/// Reports go to Docs/Claude_Pruebas/.
/// </summary>
public static class ArtPassPlaytest
{
    static string Out(string name) { Directory.CreateDirectory("Docs/Claude_Pruebas"); return "Docs/Claude_Pruebas/" + name; }

    public static string[] Hub()
    {
        var results = new List<string>();
        void Check(bool ok, string s) => results.Add((ok ? "PASS " : "FAIL ") + s);
        var h = Object.FindAnyObjectByType<MultiverseHub>();
        var cc = GameObject.Find("Explorador").GetComponent<CharacterController>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Place(Vector3 p) { cc.enabled = false; cc.transform.position = p; cc.enabled = true; typeof(MultiverseHub).GetField("verticalSpeed", flags).SetValue(h, 0f); Physics.SyncTransforms(); }
        h.enabled = false;
        try
        {
            var routes = new (string name, Vector3 a, Vector3 b)[]
            {
                ("Paseo este-oeste", new Vector3(-33, 1.1f, -3), new Vector3(33, 1.1f, -3)),
                ("Paseo este-oeste (vuelta)", new Vector3(33, 1.1f, -3.5f), new Vector3(-33, 1.1f, -3.5f)),
                ("Paseo norte (desde la exhibición espacial, que ya ocupaba el oeste)", new Vector3(-14, 1.1f, 33), new Vector3(33, 1.1f, 33)),
                ("Paseo lateral oeste (hasta la exhibición espacial)", new Vector3(-28, 1.1f, -6), new Vector3(-28, 1.1f, 26.5f)),
                ("Paseo lateral este", new Vector3(28, 1.1f, 33), new Vector3(28, 1.1f, -6)),
                ("Calle del mercado", new Vector3(-17, 1.1f, 37.6f), new Vector3(29, 1.1f, 37.6f)),
                ("Hacia portal Carreras", new Vector3(12, 1.1f, -3), new Vector3(22, 1.1f, -7.5f)),
                ("Hacia portal Código Mortal", new Vector3(-6.5f, 1.1f, -4.5f), new Vector3(-22, 1.1f, -9.5f)),
                ("Plaza de inicio a portal pirata por su pasarela", new Vector3(0, 1.1f, 0), new Vector3(-13.2f, 1.1f, 12.3f)),
                ("Plaza de inicio a portal criaturas (hasta las rocas originales)", new Vector3(0, 1.1f, 0), new Vector3(10.5f, 1.1f, 9.8f)),
                ("Plaza de inicio a isla zombi", new Vector3(0, 1.1f, 0), new Vector3(0, 1.1f, 16.5f)),
                ("Jardín sur", new Vector3(-30, 1.1f, -26), new Vector3(30, 1.1f, -26.5f)),
            };
            foreach (var r in routes)
            {
                Place(r.a);
                float length = Vector3.Distance(new Vector3(r.a.x, 0, r.a.z), new Vector3(r.b.x, 0, r.b.z));
                int frames = Mathf.CeilToInt(length / 5f * 60f * 1.35f) + 30;
                for (int i = 0; i < frames; i++)
                {
                    var d = r.b - cc.transform.position; d.y = 0;
                    if (d.magnitude < .3f) break;
                    h.MoveExplorer(d.normalized, 1f / 60f, false);
                }
                var end = cc.transform.position; float miss = Vector2.Distance(new Vector2(end.x, end.z), new Vector2(r.b.x, r.b.z));
                Check(miss < 1.2f && end.y > .7f, $"{r.name}: recorrido {length:0.0} m, distancia final al destino {miss:0.00} m, pies y={end.y:0.00}");
            }
            var art = GameObject.Find(MultiverseArtNames.Root);
            int onPath = 0; var offenders = new List<string>();
            if (art != null)
                foreach (var c in art.GetComponentsInChildren<Collider>())
                {
                    var b = c.bounds;
                    foreach (var o in Physics.OverlapBox(b.center, b.extents * .98f, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
                        if (o.name.StartsWith("Paseo") || o.name.StartsWith("Pasarela") || o.name.StartsWith("Franja")) { onPath++; offenders.Add(c.name + "→" + o.name); }
                }
            Check(art != null, "Raíz de decorado presente: " + (art != null ? art.GetComponentsInChildren<Collider>().Length + " colisionadores" : "no"));
            Check(onPath == 0, "Ningún colisionador decorativo invade paseos o pasarelas" + (onPath > 0 ? ": " + string.Join(", ", offenders.Distinct()) : ""));
            Check(Tris(art) < 60000, "Coste geométrico del decorado: " + Tris(art) + " triángulos (límite de revisión 60k)");
        }
        finally { Place(new Vector3(0, 1.05f, -5)); h.enabled = true; File.WriteAllLines(Out("Hub_ArtPass.txt"), results); }
        return results.ToArray();
    }

    /// <summary>Generic: no decorative collider overlaps colliders tagged as gameplay by name or component.</summary>
    public static string[] NoArtOverlap(string label, string[] blockedPrefixes)
    {
        var results = new List<string>();
        var art = GameObject.Find(MultiverseArtNames.Root);
        int cols = art == null ? 0 : art.GetComponentsInChildren<Collider>().Length;
        int bad = 0; var names = new HashSet<string>();
        if (art != null)
            foreach (var c in art.GetComponentsInChildren<Collider>())
                foreach (var o in Physics.OverlapBox(c.bounds.center, c.bounds.extents * .98f, Quaternion.identity, ~0, QueryTriggerInteraction.Collide))
                {
                    if (o.transform.IsChildOf(art.transform)) continue;
                    if (blockedPrefixes.Any(p => o.name.StartsWith(p)) || o.GetComponentInParent<CrazyKartRoadSurface>() != null || o.isTrigger) { bad++; names.Add(c.name + "→" + o.name); }
                }
        results.Add((art != null ? "PASS " : "FAIL ") + label + ": decorado presente, " + cols + " colisionadores");
        results.Add((bad == 0 ? "PASS " : "FAIL ") + label + ": sin solapes con pista, rutas o volúmenes de juego" + (bad > 0 ? " → " + string.Join(", ", names) : ""));
        results.Add("INFO " + label + ": " + Tris(art) + " triángulos decorativos (mallas de origen, antes del batching estático)");
        File.WriteAllLines(Out(label + "_ArtPass.txt"), results);
        return results.ToArray();
    }

    /// <summary>Counts source-mesh triangles per instance (static batching swaps sharedMesh for the combined mesh in Play).</summary>
    static int Tris(GameObject art)
    {
        if (art == null) return 0; int n = 0;
        foreach (var inst in art.GetComponentsInChildren<Transform>())
        {
            var src = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(inst.gameObject);
            var mf = (src != null ? src.GetComponent<MeshFilter>() : null) ?? inst.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null && !mf.sharedMesh.name.StartsWith("Combined")) n += mf.sharedMesh.triangles.Length / 3;
        }
        return n;
    }

    /// <summary>Closes the Arena's start menu so the in-round HUD can be captured.</summary>
    public static string ArenaCloseMenu()
    {
        var s = PirateSession.Instance; if (s == null) return "sin sesión";
        typeof(PirateSession).GetMethod("SetMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(s, new object[] { false });
        return "menú cerrado";
    }

    public static string[] Race() => NoArtOverlap("CarrerasLocas", new[] { "Barrera", "Puerta de vuelta", "Caja sorpresa", "Turbo" });
    public static string[] Mortal() => NoArtOverlap("CodigoMortal", new[] { "Plaza", "Rampa", "Puente" });

    /// <summary>Arena: the archipelago dressing exists and contributes no colliders.</summary>
    public static string[] Arena()
    {
        var results = new List<string>();
        var d = GameObject.Find("Decorado pirata · archipiélago (visual)");
        results.Add((d != null ? "PASS " : "FAIL ") + "Arena: decorado del archipiélago creado al construir el mapa");
        if (d != null)
        {
            int cols = d.GetComponentsInChildren<Collider>().Length;
            results.Add((cols == 0 ? "PASS " : "FAIL ") + "Arena: el decorado no añade colisionadores (" + cols + ")");
            results.Add("INFO Arena: " + d.transform.childCount + " piezas, " + d.GetComponentsInChildren<Renderer>().Length + " renderers");
        }
        File.WriteAllLines(Out("Arena_ArtPass.txt"), results);
        return results.ToArray();
    }
}

public static class MultiverseArtNames { public const string Root = "ArtPass · Claude"; }
