using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Measured against the imported Ship_Large mesh. Source length is 15.43296 m.</summary>
public sealed class PirateArenaMap
{
    private readonly PirateArenaBootstrap art;
    private Transform root;
    private float scale;
    private readonly List<PirateArenaRamp> ramps = new List<PirateArenaRamp>();
    public float Width { get; private set; }
    public float Length { get; private set; }
    public float CenterCourtZ => Z(-0.78f);
    public float BowCenterZ => Z(4.2f);
    public float BowHeight => Y(1.55f);
    public float SternHeight => Y(1.95f);
    public PirateArenaMap(PirateArenaBootstrap source) { art = source; }
    private float Z(float sourceX) => (sourceX - 0.35f) * scale;
    private float Y(float sourceY) => art.settings.deckHeight + (sourceY - 1.02f) * scale;

    public void Build()
    {
        scale = art.settings.shipLength / 15.4329567f;
        Width = 3.76f * scale * art.settings.beamMultiplier;
        Length = 12.3f * scale;
        root = new GameObject("ARENA DEL CAOS - cubierta y archipiélago").transform;
        BuildShip();
        Deck("01 - Patio central", Z(-3.28f), Z(2.31f), art.settings.deckHeight, Width);
        Deck("02 - Castillo de proa", Z(2.31f), Z(6.4f), BowHeight, Width);
        Deck("03 - Alcázar de popa", Z(-5.85f), Z(-3.28f), SternHeight, Width);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * Width * 0.32f;
            Ramp("Rampa a proa", new Vector3(x, art.settings.deckHeight, Z(0.7f)),
                new Vector3(x, BowHeight, Z(2.31f)), 2.4f);
            Ramp("Rampa a popa", new Vector3(x, art.settings.deckHeight, Z(-1.8f)),
                new Vector3(x, SternHeight, Z(-3.28f)), 2.4f);
        }
        BuildRails(Z(-3.28f), Z(2.31f), art.settings.deckHeight, true);
        BuildRails(Z(2.31f), Z(6.4f), BowHeight, true);
        BuildRails(Z(-5.85f), Z(-3.28f), SternHeight, false);
        Box("Remate de popa", new Vector3(0f, SternHeight + 0.55f, Z(-5.85f)),
            new Vector3(Width, 1.1f, 0.22f), art.darkWood, true);
        Box("Remate de proa", new Vector3(0f, BowHeight + 0.45f, Z(6.4f)),
            new Vector3(Width, 0.9f, 0.22f), art.darkWood, true);
        BuildDetails();
        BuildOcean();
        BuildIsland(new Vector3(-44f, -0.4f, 24f), 1f);
        BuildIsland(new Vector3(48f, -0.4f, 48f), 1.3f);
        BuildIsland(new Vector3(-55f, -0.4f, -43f), 0.8f);
        // Visual-only pirate village, lighthouse, ships and buoys (no colliders): Assets/_Game/Art.
        PirateArenaArtDressing.Dress(root, Width, Length);
    }

    private void BuildShip()
    {
        if (art.shipLarge == null) return;
        GameObject hull = new GameObject("Casco original - centrado y nivelado");
        hull.transform.SetParent(root, false);
        GameObject heading = new GameObject("Orientación longitudinal");
        heading.transform.SetParent(hull.transform, false);
        GameObject model = Object.Instantiate(art.shipLarge, heading.transform, false);
        Bounds native = PirateArenaVisuals.BoundsOf(model);
        // Rotate a parent around world Y; preserve the model's FBX axis correction.
        if (native.size.x > native.size.z) heading.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
        float fit = art.settings.shipLength / Mathf.Max(native.size.x, native.size.z);
        hull.transform.localScale = new Vector3(fit * art.settings.beamMultiplier, fit, fit);
        Bounds bounds = PirateArenaVisuals.BoundsOf(model);
        // The mast and bowsprit skew the overall bounds. Align using the measured main-deck plane.
        float mainDeck = bounds.min.y + bounds.size.y * (2.63091f / 11.47903f);
        // Separate the original uneven deck mesh from the smooth gameplay planks to prevent z-fighting.
        hull.transform.position += new Vector3(-bounds.center.x, art.settings.deckHeight - mainDeck - 0.18f,
            -bounds.center.z + 1.4505831f * scale);
        PirateArenaVisuals.ApplyMaterial(model, art.atlas);
        // Gameplay uses smooth, deliberate deck colliders, avoiding collisions with decorative rigging.
        foreach (Collider collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
        // Source stair geometry differs from the smooth gameplay ramps. Use it for camera occlusion only.
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            var collision = filter.gameObject.AddComponent<MeshCollider>();
            collision.sharedMesh = filter.sharedMesh;
            filter.gameObject.AddComponent<PirateArenaSurface>();
            filter.gameObject.AddComponent<PirateCameraObstacle>();
        }
    }

    private GameObject Box(string name, Vector3 center, Vector3 size, Material material, bool solid)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(root, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid)
        {
            box.GetComponent<Collider>().enabled = false;
            Object.Destroy(box.GetComponent<Collider>());
        }
        else box.AddComponent<PirateArenaSurface>();
        return box;
    }

    private void Deck(string name, float from, float to, float height, float width)
    {
        // Raised decks are solid down to the main deck: no crawl-through void behind the stairs.
        float thickness = Mathf.Max(0.32f, height - art.settings.deckHeight + 0.32f);
        Box(name + " - suelo continuo", new Vector3(0f, height - thickness * 0.5f, (from + to) * 0.5f),
            new Vector3(width, thickness, to - from), art.darkWood, true).GetComponent<PirateArenaSurface>().walkable = true;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uv = new List<Vector2>();
        int rows = Mathf.CeilToInt(width / 0.56f);
        float plankWidth = width / rows;
        for (int row = 0; row < rows; row++)
        {
            float x = -width * 0.5f + row * plankWidth;
            float first = from - (row % 3) * 0.72f;
            for (float z = first; z < to; z += 2.2f)
            {
                float a = Mathf.Max(z, from) + 0.014f, b = Mathf.Min(z + 2.2f, to) - 0.014f;
                if (b <= a) continue;
                int i = vertices.Count;
                vertices.Add(new Vector3(x + 0.012f, height + 0.014f, a));
                vertices.Add(new Vector3(x + 0.012f, height + 0.014f, b));
                vertices.Add(new Vector3(x + plankWidth - 0.012f, height + 0.014f, b));
                vertices.Add(new Vector3(x + plankWidth - 0.012f, height + 0.014f, a));
                triangles.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
            }
        }
        Mesh mesh = new Mesh { name = name + " - tablones unidos" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        MeshObject(name + " - tablones", mesh, art.wood).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        for (int side = -1; side <= 1; side += 2)
            Box("Borde de cubierta", new Vector3(side * (width * 0.5f - 0.14f), height + 0.04f, (from + to) * 0.5f),
                new Vector3(0.24f, 0.08f, to - from), art.brass, false);
    }

    private void Ramp(string label, Vector3 low, Vector3 high, float width)
    {
        Vector3 left = Vector3.left * width * 0.5f;
        Vector3[] v = {
            low + left, low - left, high + left, high - left,
            low + left - Vector3.up * 0.24f, low - left - Vector3.up * 0.24f,
            high + left - Vector3.up * 0.24f, high - left - Vector3.up * 0.24f
        };
        int[] t = {0,2,1,1,2,3,4,5,6,5,7,6,0,4,2,2,4,6,1,3,5,3,7,5,0,1,4,1,5,4,2,6,3,3,6,7};
        if (high.z < low.z) for (int i = 0; i < t.Length; i += 3) { int temp = t[i+1]; t[i+1] = t[i+2]; t[i+2] = temp; }
        Mesh mesh = new Mesh { name = label, vertices = v, triangles = t };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        MeshObject(label + " - superficie", mesh, art.wood);
        // A continuous box overlaps both landings. Capsules cannot hit triangle seams or slip through the join.
        Vector3 direction = (high - low).normalized;
        Vector3 normal = new Vector3(0f, Mathf.Abs(direction.z), -direction.y * Mathf.Sign(direction.z));
        GameObject ramp = new GameObject(label + " - colisión continua", typeof(BoxCollider), typeof(PirateArenaSurface), typeof(PirateArenaRamp));
        ramp.transform.SetParent(root, false);
        ramp.transform.SetPositionAndRotation((low + high) * 0.5f - normal * 0.15f,
            Quaternion.LookRotation(direction, normal));
        ramp.GetComponent<BoxCollider>().size = new Vector3(width, 0.3f, Vector3.Distance(low, high) + 0.3f);
        ramp.GetComponent<PirateArenaSurface>().walkable = true;
        ramp.GetComponent<PirateArenaRamp>().Initialize(low, high, width);
        ramps.Add(ramp.GetComponent<PirateArenaRamp>());
        // Fill the entire underside down into the main deck. The upper box keeps its smooth traversal surface.
        float bottom = art.settings.deckHeight - .2f;
        Vector3[] fill = {
            low + left - Vector3.up * .02f, low - left - Vector3.up * .02f,
            high + left - Vector3.up * .02f, high - left - Vector3.up * .02f,
            new Vector3(low.x-width*.5f,bottom,low.z), new Vector3(low.x+width*.5f,bottom,low.z),
            new Vector3(high.x-width*.5f,bottom,high.z), new Vector3(high.x+width*.5f,bottom,high.z)
        };
        var fillMesh = new Mesh { name = label + " - relleno sólido", vertices = fill, triangles = t };
        fillMesh.RecalculateNormals(); fillMesh.RecalculateBounds();
        var fillObject = MeshObject(label + " - lateral cerrado", fillMesh, art.darkWood);
        var fillCollider = fillObject.AddComponent<MeshCollider>();
        fillCollider.sharedMesh = fillMesh; fillCollider.convex = true;
        fillObject.AddComponent<PirateArenaSurface>();
        for (int step = 1; step < 10; step++)
        {
            Vector3 p = Vector3.Lerp(low, high, step / 10f) + Vector3.up * 0.02f;
            GameObject strip = Box("Listón de rampa", p, new Vector3(width, 0.025f, 0.055f), art.darkWood, false);
            strip.transform.rotation = Quaternion.FromToRotation(Vector3.forward, (high - low).normalized);
        }
    }

    private void BuildRails(float from, float to, float height, bool gaps)
    {
        // Low, broken rails keep the deck readable and leave several ways to fall overboard.
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (Width * 0.5f - 0.12f);
            int count = Mathf.CeilToInt((to - from) / 2f);
            float spacing = (to - from) / count;
            for (int i = 0; i <= count; i++)
            {
                float z = from + i * spacing;
                bool inGap = gaps && (Mathf.Abs(z - CenterCourtZ) < 1.5f || Mathf.Abs(z - Z(4.8f)) < 1.25f);
                if (inGap) continue;
                Box("Poste de borda", new Vector3(x, height + 0.5f, z), new Vector3(0.18f, 1f, 0.18f), art.darkWood, true);
                Box("Casquillo dorado", new Vector3(x, height + 1f, z), new Vector3(0.23f, 0.09f, 0.23f), art.brass, false);
                float next = z + spacing;
                if (i == count || (gaps && (Mathf.Abs(next - CenterCourtZ) < 1.5f || Mathf.Abs(next - Z(4.8f)) < 1.25f))) continue;
                Box("Pasamanos", new Vector3(x, height + 0.88f, z + spacing * 0.5f),
                    new Vector3(0.18f, 0.17f, spacing), art.wood, true);
                Line("Cuerda de borda", new[] { new Vector3(x, height + 0.45f, z),
                    new Vector3(x, height + 0.28f, z + spacing * 0.5f), new Vector3(x, height + 0.45f, next) }, 0.045f, art.rope);
            }
        }
    }

    // Stand inside an actual opening in the rails; never steer a carrier into the sea.
    public Vector3 CarryExit(Vector3 from, out Vector3 outward)
    {
        float side = from.x < 0f ? -1f : 1f;
        outward = Vector3.right * side;
        float z = from.z > Z(2.31f) ? Z(4.8f) : CenterCourtZ;
        return new Vector3(side * (Width * .5f - 1.35f),
            from.z > Z(2.31f) ? BowHeight : art.settings.deckHeight, z);
    }

    public Vector3 NavigationTarget(Vector3 from, Vector3 target)
    {
        int currentDeck = from.z > Z(2.31f) ? 1 : from.z < Z(-3.28f) ? -1 : 0;
        int destinationDeck = target.z > Z(2.31f) ? 1 : target.z < Z(-3.28f) ? -1 : 0;
        if (currentDeck == destinationDeck) return target;
        int rampDeck = currentDeck != 0 ? currentDeck : destinationDeck;
        PirateArenaRamp nearest = null;
        float best = float.MaxValue;
        foreach (var ramp in ramps)
        {
            if (Mathf.Sign(ramp.High.z - ramp.Low.z) != rampDeck) continue;
            float distance = Mathf.Abs(from.x - ramp.Low.x);
            if (distance < best) { best = distance; nearest = ramp; }
        }
        if (nearest == null) return target;
        Vector3 direction = Vector3.ProjectOnPlane(nearest.High - nearest.Low, Vector3.up).normalized;
        Vector3 entry = currentDeck == 0 ? nearest.Low - direction * 0.8f : nearest.High + direction * 0.8f;
        Vector3 exit = currentDeck == 0 ? nearest.High + direction * 0.8f : nearest.Low - direction * 0.8f;
        Vector3 along = currentDeck == 0 ? direction : -direction;
        // Once aligned with the staircase, keep heading to its far landing instead of turning back to the entry.
        return best < nearest.Width * 0.3f && Vector3.Dot(from - entry, along) > -0.25f ? exit : entry;
    }

    private void BuildDetails()
    {
        float deck = art.settings.deckHeight;
        // Flush hatch cannot snag the capsule or cover the cannon warning with raised geometry.
        Vector3 hatch = new Vector3(0f, deck + 0.026f, CenterCourtZ);
        Box("Escotilla - marco", hatch, new Vector3(2.6f, 0.025f, 3.2f), art.darkWood, false);
        for (int i = -4; i <= 4; i++)
            Box("Rejilla de escotilla", hatch + new Vector3(i * 0.27f, 0.02f, 0f),
                new Vector3(0.08f, 0.025f, 2.9f), art.iron, false);
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * (Width * 0.5f - 1.1f);
            Model("Cofre de popa", art.chestModel, new Vector3(x, SternHeight + 0.03f, Z(-4.9f)), 0.85f, side * 20f);
            Model("Ancla de proa", art.anchorModel, new Vector3(x, BowHeight + 0.03f, Z(5.7f)), 1.2f, side * 90f);
            Lantern(new Vector3(x, SternHeight, Z(-5.6f)));
            Lantern(new Vector3(x, BowHeight, Z(6.1f)));
            for (int i = 0; i < 2; i++) RopeCoil(new Vector3(x, deck + 0.04f, CenterCourtZ - 5f + i * 9f));
        }
        Vector3 wheel = new Vector3(0f, SternHeight + 1.25f, Z(-5.1f));
        Box("Timón - pedestal", wheel - Vector3.up * 0.7f, new Vector3(0.4f, 1.1f, 0.45f), art.darkWood, true);
        var ring = new Vector3[33];
        for (int i = 0; i < ring.Length; i++)
        {
            float a = i * Mathf.PI * 2f / 32f;
            ring[i] = wheel + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.65f;
        }
        Line("Timón - aro", ring, 0.1f, art.brass);
        var wheelBlocker = Box("Timón - colisión", wheel, new Vector3(1.4f, 1.4f, .16f), art.wood, true);
        wheelBlocker.GetComponent<Renderer>().enabled = false;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            Line("Timón - radio", new[] { wheel, wheel + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 0.85f }, 0.07f, art.wood);
        }
        // Physical mast bases match the two main obstructions, keeping the center routes readable.
        foreach (float source in new[] { -1.9f, 3.4f })
        {
            float y = source < 2.31f ? deck : BowHeight;
            Box("Base de mástil", new Vector3(0f, y + 0.35f, Z(source)), new Vector3(0.85f, 0.7f, 0.85f), art.darkWood, true);
            var mast = Box("Mástil - colisión vertical", new Vector3(0f, y + 4f, Z(source)),
                new Vector3(.65f, 8f, .65f), art.darkWood, true);
            mast.GetComponent<Renderer>().enabled = false;
        }
    }

    private void RopeCoil(Vector3 center)
    {
        Vector3[] points = new Vector3[65];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / 64f, a = t * Mathf.PI * 6f, r = Mathf.Lerp(0.12f, 0.5f, t);
            points[i] = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
        Line("Cabo enrollado", points, 0.055f, art.rope);
    }

    private void Lantern(Vector3 p)
    {
        Box("Farol - poste", p + Vector3.up * 0.85f, new Vector3(0.12f, 1.7f, 0.12f), art.darkWood, true);
        Box("Farol - cristal", p + Vector3.up * 1.8f, new Vector3(0.25f, 0.34f, 0.25f), art.cloth, true);
        Box("Farol - techo", p + Vector3.up * 2f, new Vector3(0.34f, 0.08f, 0.34f), art.brass, false);
    }

    private void Model(string label, GameObject prefab, Vector3 p, float height, float yaw)
    {
        GameObject holder = new GameObject(label);
        holder.transform.SetParent(root, false);
        holder.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        var visual = PirateArenaVisuals.AddModel(prefab, holder.transform, height, art.atlas, false);
        if (visual != null && (label == "Cofre de popa" || label == "Ancla de proa"))
            foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                filter.gameObject.AddComponent<PirateArenaSurface>();
            }
    }

    private void BuildOcean()
    {
        const int n = 64;
        var v = new Vector3[(n + 1) * (n + 1)];
        var t = new int[n * n * 6];
        for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++) v[z * (n+1) + x] = new Vector3((x/(float)n - 0.5f)*400f, 0f, (z/(float)n - 0.5f)*400f);
        int k = 0;
        for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
        {
            int i = z * (n+1) + x;
            t[k++] = i; t[k++] = i+n+1; t[k++] = i+1;
            t[k++] = i+1; t[k++] = i+n+1; t[k++] = i+n+2;
        }
        Mesh mesh = new Mesh { name = "Mar abierto", vertices = v, triangles = t };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        MeshObject("Mar turquesa - olas animadas", mesh, art.water).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        for (int side = -1; side <= 1; side += 2)
        {
            var foam = new Vector3[30];
            for (int i = 0; i < foam.Length; i++)
            {
                float z = Mathf.Lerp(-Length * 0.45f, Length * 0.45f, i / 29f);
                foam[i] = new Vector3(side * (Width * 0.57f + Mathf.Sin(i * 1.8f) * 0.13f), 0.08f, z);
            }
            Line("Espuma junto al casco", foam, 0.11f, art.foam);
        }
    }

    private void BuildIsland(Vector3 p, float size)
    {
        GameObject sand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sand.name = "Islote - arena";
        sand.transform.SetParent(root, false);
        sand.transform.position = p;
        sand.transform.localScale = new Vector3(17f, 3f, 13f) * size;
        sand.GetComponent<Renderer>().sharedMaterial = art.rope;
        sand.GetComponent<Collider>().enabled = false;
        for (int i = 0; i < 3; i++)
        {
            Vector3 offset = new Vector3((i-1)*4f, 0.7f, (i%2)*3f) * size;
            Model("Rocas del archipiélago", art.rockModel, p+offset, (4f+i)*size, i*75f);
        }
        Model("Palmera", art.palmModel, p + new Vector3(4f, 1f, -3f) * size, 9f * size, 25f);
        Model("Palmera pequeña", art.palmModel, p + new Vector3(-3f, 1f, 3f) * size, 6f * size, -40f);
    }

    private GameObject MeshObject(string label, Mesh mesh, Material material)
    {
        GameObject obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(root, false);
        obj.GetComponent<MeshFilter>().sharedMesh = mesh;
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
        return obj;
    }

    private void Line(string label, Vector3[] points, float width, Material material)
    {
        GameObject obj = new GameObject(label, typeof(LineRenderer));
        obj.transform.SetParent(root, false);
        LineRenderer line = obj.GetComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.widthMultiplier = width;
        line.numCapVertices = 3;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
    }
}

public sealed class PirateArenaSurface : MonoBehaviour
{
    public bool walkable;
}

/// <summary>Detailed hull for camera obstruction. Gameplay uses the continuous deck, ramp and mast volumes.</summary>
public sealed class PirateCameraObstacle : MonoBehaviour
{
    public static void IgnoreFor(Collider actor)
    {
        foreach (var obstacle in Object.FindObjectsByType<PirateCameraObstacle>(FindObjectsSortMode.None))
            foreach (var collider in obstacle.GetComponents<Collider>())
                if (collider.enabled) Physics.IgnoreCollision(actor, collider, true);
    }
}

/// <summary>Endpoints used by traversal checks and local bot routing.</summary>
public sealed class PirateArenaRamp : MonoBehaviour
{
    public Vector3 Low { get; private set; }
    public Vector3 High { get; private set; }
    public float Width { get; private set; }
    public void Initialize(Vector3 low, Vector3 high, float width) { Low = low; High = high; Width = width; }
}
