using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Minigolf Loco (2026-11-17) · the ten holes, written as small tile maps and built at runtime.
/// Tiles (2 m): '.' fairway, ':' raised fairway (+0.8 m), '^' 'v' '<' '>' ramp rising towards the arrow,
/// 'S'/'s' tee (low/high), 'H'/'h' cup (low/high), 'B' bumper, 'M' windmill, 'X' sliding block row, '~' water,
/// ' ' nothing. Edges next to nothing get a rail; edges next to water are open (fall in = +1 stroke, back to where
/// you hit from). Each hole sits in its own spot along +X. Moving parts follow the hole clock (same on every computer).
/// </summary>
public sealed class GolfCourse : MonoBehaviour
{
    public const float Tile = 2f, High = .8f, Rail = .32f, CupRadius = .24f;

    public sealed class Hole
    {
        public string Name; public int Par; public string[] Map;
        public Vector3 Origin, Tee, Cup; public float CupY;
        public readonly List<Vector3> Path = new();     // tile centres from tee to cup (for bots)
        public readonly List<Transform> Windmills = new();
        public readonly List<(Transform t, Vector3 a, Vector3 b)> Sliders = new();
        public Bounds Area;
    }

    public static readonly (string name, int par, string[] map)[] Layouts =
    {
        ("Calentamiento", 2, new[] { "...", ".H.", "...", "...", "...", ".S." }),
        ("La ele", 3, new[] { "....H", ".....", "..   ", "..   ", "..   ", ".S   " }),
        ("Pinball", 3, new[] { ".....", "..H..", ".B.B.", "..B..", ".B.B.", ".....", "..S.." }),
        ("El molino", 3, new[] { " ... ", " .H. ", " ... ", "  M  ", "  .  ", " ... ", " .S. " }),
        ("La rampa", 3, new[] { "::h::", ":::::", "..^..", ".....", "..S.." }),
        ("Zigzag", 4, new[] { "H...  ", "   .. ", "    . ", "   .. ", "  ..  ", " ..   ", " S    " }),
        ("El puente", 3, new[] { "..H..", ".....", "~~.~~", "~~.~~", ".....", "..S.." }),
        ("Deslizadores", 3, new[] { ".....", ".H...", "XXXXX", ".....", "XXXXX", ".....", "...S." }),
        ("Cuesta abajo", 3, new[] { "..H..", ".B.B.", ".....", "..v..", ":::::", "::s::" }),
        ("Gran final", 5, new[] { "::h::", ":::::", "  ^  ", "  .  ", "~~.~~", "..M..", ".....", "..S.." }),
    };

    public readonly List<Hole> Holes = new();
    Material grass, grass2, rail, cliff, water, bumperMat, millMat, cupMat;
    PhysicsMaterial wallPhys, floorPhys, bumperPhys;

    public void Build()
    {
        grass = EscapeKit.Paint(new Color(.36f, .74f, .34f), .15f); grass2 = EscapeKit.Paint(new Color(.32f, .68f, .3f), .15f);
        rail = EscapeKit.Paint(new Color(.62f, .4f, .24f), .2f); cliff = EscapeKit.Paint(new Color(.55f, .5f, .45f), .05f);
        water = EscapeKit.Glow(new Color(.25f, .6f, .95f), .5f); bumperMat = EscapeKit.Paint(new Color(1f, .4f, .5f), .5f);
        millMat = EscapeKit.Paint(new Color(.95f, .85f, .3f), .3f); cupMat = EscapeKit.Paint(new Color(.05f, .05f, .06f));
        wallPhys = new PhysicsMaterial("Banda") { bounciness = .75f, dynamicFriction = 0f, staticFriction = 0f, bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Minimum };
        bumperPhys = new PhysicsMaterial("Seta") { bounciness = 1f, dynamicFriction = 0f, staticFriction = 0f, bounceCombine = PhysicsMaterialCombine.Maximum, frictionCombine = PhysicsMaterialCombine.Minimum };
        floorPhys = new PhysicsMaterial("Césped") { bounciness = .05f, dynamicFriction = .3f, staticFriction = .3f, bounceCombine = PhysicsMaterialCombine.Minimum };

        var ground = EscapeKit.Block(transform, "Prado", new Vector3(Layouts.Length * 9f, -1.6f, 6f), new Vector3(Layouts.Length * 22f + 40f, .4f, 60f), EscapeKit.Paint(new Color(.5f, .8f, .45f), .05f), .05f, false);
        for (int i = 0; i < Layouts.Length; i++) Holes.Add(BuildHole(i));
    }

    static bool Floor(char c) => c != ' ' && c != '~';
    static bool Raised(char c) => c == ':' || c == 'h' || c == 's';
    static bool Ramp(char c) => c == '^' || c == 'v' || c == '<' || c == '>';

    Hole BuildHole(int index)
    {
        var (name, par, map) = Layouts[index];
        var h = new Hole { Name = name, Par = par, Map = map, Origin = new Vector3(index * 18f, 0, 0) };
        var root = new GameObject("Hoyo " + (index + 1) + " · " + name).transform; root.SetParent(transform, false); root.position = h.Origin;
        int rows = map.Length, cols = map[0].Length;
        char At(int r, int c) => r < 0 || r >= rows || c < 0 || c >= map[r].Length ? ' ' : map[r][c];
        Vector3 Center(int r, int c) => h.Origin + new Vector3((c - (cols - 1) * .5f) * Tile, 0, (rows - 1 - r) * Tile);
        float TopOf(char c) => Raised(c) ? High : 0f;

        // Floors: one mesh per height (smooth for the ball), ramps as tilted boxes, cliffs under raised tiles.
        foreach (bool raisedLevel in new[] { false, true })
        {
            var verts = new List<Vector3>(); var tris = new List<int>(); var cols2 = new List<Color>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    char t = At(r, c); if (!Floor(t) || Ramp(t) || Raised(t) != raisedLevel) continue;
                    var p = Center(r, c) - h.Origin; float y = raisedLevel ? High : 0f; float s = Tile * .5f;
                    int b = verts.Count;
                    verts.Add(new Vector3(p.x - s, y, p.z - s)); verts.Add(new Vector3(p.x - s, y, p.z + s)); verts.Add(new Vector3(p.x + s, y, p.z + s)); verts.Add(new Vector3(p.x + s, y, p.z - s));
                    tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                }
            if (verts.Count == 0) continue;
            var mesh = new Mesh { name = "Green" }; mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(raisedLevel ? "Green alto" : "Green"); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = raisedLevel ? grass2 : grass;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.sharedMaterial = floorPhys;
        }
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                char t = At(r, c); if (!Floor(t)) { if (t == '~') EscapeKit.Block(root, "Agua", Center(r, c) - h.Origin + Vector3.down * .55f, new Vector3(Tile, .1f, Tile), water, .02f, false); continue; }
                var p = Center(r, c) - h.Origin;
                // Base under every tile (visual + stops the ball from falling between tiles).
                float top = Ramp(t) ? 0f : TopOf(t);
                EscapeKit.Block(root, "Base", p + Vector3.down * ((.8f - top) * .5f + .001f) + Vector3.up * 0f, new Vector3(Tile, .8f + top, Tile), Raised(t) ? cliff : EscapeKit.Paint(new Color(.45f, .35f, .25f)), .02f, false).transform.localPosition = p + new Vector3(0, (top - .8f) * .5f - .002f, 0);
                if (Ramp(t))
                {
                    Vector3 up = t == '^' ? Vector3.forward : t == 'v' ? Vector3.back : t == '>' ? Vector3.right : Vector3.left;
                    float len = Mathf.Sqrt(Tile * Tile + High * High), ang = Mathf.Atan2(High, Tile) * Mathf.Rad2Deg;
                    var ramp = EscapeKit.Block(root, "Rampa", p + Vector3.up * (High * .5f - .1f), new Vector3(Tile, .2f, len), grass2, .02f, true);
                    ramp.transform.localRotation = Quaternion.LookRotation(up) * Quaternion.Euler(-ang, 0, 0);
                    ramp.GetComponent<BoxCollider>().sharedMaterial = floorPhys;
                    // Side wedges.
                    EscapeKit.Block(root, "Cuña", p + Vector3.up * (High * .25f - .2f), new Vector3(Tile * .98f, High * .5f, Tile * .98f), cliff, .02f, false);
                }
                // Rails on edges that face nothing (open to water; cliffs act as walls by themselves).
                foreach (var (dr, dc, dir) in new[] { (-1, 0, Vector3.forward), (1, 0, Vector3.back), (0, -1, Vector3.left), (0, 1, Vector3.right) })
                {
                    char n = At(r + dr, c + dc);
                    if (n != ' ') continue;
                    float baseY = Ramp(t) ? 0f : TopOf(t);
                    var size = dir.x != 0 ? new Vector3(.25f, Rail + (Ramp(t) ? High : 0f), Tile + .25f) : new Vector3(Tile + .25f, Rail + (Ramp(t) ? High : 0f), .25f);
                    var w = EscapeKit.Block(root, "Banda", p + dir * (Tile * .5f + .125f) + Vector3.up * (baseY + size.y * .5f), size, rail, .04f, true);
                    w.GetComponent<BoxCollider>().sharedMaterial = wallPhys;
                }
                // Cliff walls: a low tile next to a raised one (not through a ramp) is blocked by the raised base; give it bounce.
                if (Raised(t))
                    foreach (var (dr, dc, dir) in new[] { (-1, 0, Vector3.forward), (1, 0, Vector3.back), (0, -1, Vector3.left), (0, 1, Vector3.right) })
                    {
                        char n = At(r + dr, c + dc);
                        if (!Floor(n) || Raised(n) || Ramp(n)) continue;
                        var size = dir.x != 0 ? new Vector3(.1f, High, Tile) : new Vector3(Tile, High, .1f);
                        var w = new GameObject("Talud"); w.transform.SetParent(root, false); w.transform.localPosition = p + dir * (Tile * .5f - .05f) + Vector3.up * High * .5f;
                        var bc = w.AddComponent<BoxCollider>(); bc.size = size; bc.sharedMaterial = wallPhys;
                    }
                switch (t)
                {
                    case 'S': case 's': h.Tee = Center(r, c) + Vector3.up * (TopOf(t) + .12f); EscapeKit.Block(root, "Salida", p + Vector3.up * (TopOf(t) + .006f), new Vector3(1.2f, .01f, .6f), EscapeKit.Paint(new Color(.9f, .9f, .85f)), .01f, false); break;
                    case 'H': case 'h':
                        h.Cup = Center(r, c) + Vector3.up * TopOf(t); h.CupY = TopOf(t);
                        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Hoyo", p + Vector3.up * (TopOf(t) + .004f), new Vector3(CupRadius * 2f, .004f, CupRadius * 2f), cupMat, false);
                        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Mástil", p + Vector3.up * (TopOf(t) + .9f), new Vector3(.04f, .9f, .04f), EscapeKit.Paint(Color.white), false);
                        EscapeKit.Block(root, "Bandera", p + new Vector3(.25f, TopOf(t) + 1.6f, 0), new Vector3(.5f, .32f, .02f), EscapeKit.Glow(new Color(1f, .3f, .3f), 1.2f), .01f, false);
                        break;
                    case 'B':
                        var bump = EscapeKit.Shape(PrimitiveType.Cylinder, root, "Seta", p + Vector3.up * .2f, new Vector3(.7f, .2f, .7f), bumperMat, true);
                        Destroy(bump.GetComponent<CapsuleCollider>()); var bcol = bump.AddComponent<CapsuleCollider>(); bcol.radius = .5f; bcol.height = 2f; bcol.sharedMaterial = bumperPhys;
                        EscapeKit.Shape(PrimitiveType.Sphere, root, "Bola", p + Vector3.up * .45f, Vector3.one * .3f, EscapeKit.Glow(new Color(1f, .7f, .8f), 1.4f), false);
                        break;
                    case 'M':
                        var mill = new GameObject("Molino").transform; mill.SetParent(root, false); mill.localPosition = p;
                        EscapeKit.Shape(PrimitiveType.Cylinder, mill, "Eje", Vector3.up * .45f, new Vector3(.16f, .45f, .16f), EscapeKit.Paint(new Color(.5f, .3f, .2f)), false);
                        var blades = new GameObject("Aspas").transform; blades.SetParent(mill, false); blades.localPosition = Vector3.up * .18f;
                        var blade = EscapeKit.Block(blades, "Aspa", Vector3.zero, new Vector3(1.75f, .3f, .2f), millMat, .04f, true);
                        var rb = blades.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
                        blade.GetComponent<BoxCollider>().sharedMaterial = wallPhys;
                        h.Windmills.Add(blades);
                        break;
                }
            }
        // Sliders: one block per row of X tiles, sweeping across it.
        for (int r = 0; r < rows; r++)
        {
            int first = -1, last = -1;
            for (int c = 0; c < cols; c++) if (At(r, c) == 'X') { if (first < 0) first = c; last = c; }
            if (first < 0) continue;
            var a = Center(r, first) - h.Origin + Vector3.up * .2f; var b = Center(r, last) - h.Origin + Vector3.up * .2f;
            { var inward = (b - a).normalized * Tile * .55f; a += inward; b -= inward; }
            var block = EscapeKit.Block(root, "Deslizador", a, new Vector3(Tile * 2.2f, .4f, .8f), EscapeKit.Paint(new Color(.45f, .55f, 1f), .4f), .06f, true);
            block.GetComponent<BoxCollider>().sharedMaterial = wallPhys;
            var rb = block.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            h.Sliders.Add((block.transform, a, b));
        }
        // Path for the bots: breadth-first over tiles (ramps only along their slope).
        h.Path.AddRange(FindPath(h, map, rows, cols, Center, TopOf));
        // Area for the cameras.
        h.Area = new Bounds(h.Origin + new Vector3(0, 0, (rows - 1) * Tile * .5f), new Vector3(cols * Tile, 2f, rows * Tile));
        return h;
    }

    static List<Vector3> FindPath(Hole h, string[] map, int rows, int cols, System.Func<int, int, Vector3> center, System.Func<char, float> topOf)
    {
        char At(int r, int c) => r < 0 || r >= rows || c < 0 || c >= map[r].Length ? ' ' : map[r][c];
        (int r, int c) start = (0, 0), goal = (0, 0);
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) { char t = At(r, c); if (t == 'S' || t == 's') start = (r, c); if (t == 'H' || t == 'h') goal = (r, c); }
        var prev = new Dictionary<(int, int), (int, int)>(); var q = new Queue<(int r, int c)>(); q.Enqueue(start); prev[start] = start;
        bool Pass((int r, int c) a, (int r, int c) b)
        {
            char ta = At(a.r, a.c), tb = At(b.r, b.c); if (!Floor(tb)) return false;
            float ha = Ramp(ta) ? -1 : topOf(ta), hb = Ramp(tb) ? -1 : topOf(tb);
            if (ha >= 0 && hb >= 0) return Mathf.Approximately(ha, hb);
            return true;   // ramps connect both levels
        }
        while (q.Count > 0)
        {
            var cur = q.Dequeue(); if (cur == goal) break;
            foreach (var (dr, dc) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                var n = (cur.r + dr, cur.c + dc);
                if (prev.ContainsKey(n) || !Pass(cur, n)) continue;
                prev[n] = cur; q.Enqueue(n);
            }
        }
        var path = new List<Vector3>(); var at = goal;
        if (!prev.ContainsKey(goal)) return path;
        while (true) { char t = At(at.Item1, at.Item2); path.Add(center(at.Item1, at.Item2) + Vector3.up * (Ramp(t) ? High * .5f : topOf(t))); if (at == start) break; at = prev[at]; }
        path.Reverse();
        return path;
    }

    /// <summary>Moving parts follow the hole clock so everybody sees the same windmills and sliders.</summary>
    public void Tick(int hole, float t)
    {
        if (hole < 0 || hole >= Holes.Count) return;
        var h = Holes[hole];
        foreach (var m in h.Windmills) m.GetComponent<Rigidbody>().MoveRotation(Quaternion.Euler(0, t * 70f, 0));
        foreach (var (tr, a, b) in h.Sliders)
        {
            float k = .5f - .5f * Mathf.Cos(t * 1.6f + a.z);
            tr.GetComponent<Rigidbody>().MovePosition(tr.parent.TransformPoint(Vector3.Lerp(a, b, k)));
        }
    }

    public static bool IsWater(Hole h, Vector3 p)
    {
        int rows = h.Map.Length, cols = h.Map[0].Length;
        int c = Mathf.RoundToInt((p.x - h.Origin.x) / Tile + (cols - 1) * .5f), r = rows - 1 - Mathf.RoundToInt((p.z - h.Origin.z) / Tile);
        if (r < 0 || r >= rows || c < 0 || c >= h.Map[r].Length) return true;
        return h.Map[r][c] == '~' || h.Map[r][c] == ' ';
    }
}
