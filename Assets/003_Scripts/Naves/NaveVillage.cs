using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Arma tu Nave (2026-11-17) · builds the village: round plaza with the fountain and the launch pads, market stalls,
/// eight houses you can walk into (furniture inside, roof hides while you are in), hedges, trees, carts, a well…
/// and the list of hiding spots where the ship pieces appear. Bakes a NavMesh at runtime for the bots.
/// Every prefab comes from Resources/NaveArt (Multiverse → Zonas → Preparar prefabs) and faces +Z.
/// </summary>
public sealed class NaveVillage : MonoBehaviour
{
    public const float PlazaRadius = 13f, PadRing = 7.6f, HouseRing = 25f, Bound = 39f;
    public readonly List<Vector3> Spots = new();
    public readonly List<House> Houses = new();
    Transform root;

    public sealed class House { public Vector3 Center; public float Yaw; public Renderer[] Roof; public Vector2 Half; }

    public static float PadAngle(int index) => 30f + index * 60f;
    public static Vector3 PadPos(int index) => Quaternion.Euler(0, PadAngle(index), 0) * Vector3.forward * PadRing;

    public void Build(int seed)
    {
        root = new GameObject("Pueblo").transform;
        var rng = new System.Random(seed * 3 + 1);

        // Ground, plaza, paths.
        var grassMat = EscapeKit.Paint(new Color(.42f, .7f, .32f), .05f);
        EscapeKit.Block(root, "Césped", new Vector3(0, -.5f, 0), new Vector3(Bound * 2f + 30f, 1f, Bound * 2f + 30f), grassMat, .02f, true);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Plaza", new Vector3(0, .01f, 0), new Vector3(PlazaRadius * 2f, .02f, PlazaRadius * 2f), EscapeKit.Paint(new Color(.78f, .74f, .66f), .1f), false);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Borde plaza", new Vector3(0, .005f, 0), new Vector3(PlazaRadius * 2f + 1f, .02f, PlazaRadius * 2f + 1f), EscapeKit.Paint(new Color(.6f, .55f, .5f)), false);
        var path = EscapeKit.Paint(new Color(.72f, .64f, .5f), .05f);
        for (int k = 0; k < 8; k++)
        {
            float a = 22.5f + k * 45f;
            EscapeKit.Block(root, "Camino", Quaternion.Euler(0, a, 0) * Vector3.forward * (PlazaRadius + 4.5f) + Vector3.up * .012f, new Vector3(2.6f, .02f, 9f), path, .01f, false, new Vector3(0, a, 0));
        }
        Prop("Fuente", Vector3.zero, 0, true);

        // Market stalls between the paths (spot behind the counter).
        for (int k = 0; k < 4; k++)
        {
            float a = k * 90f; var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            var at = dir * 15.5f; Prop("Puesto", at, a + 180f, true);
            Spots.Add(at + dir * 1.25f);
        }

        // Houses.
        Color[] walls = { new Color(.96f, .88f, .72f), new Color(.95f, .72f, .72f), new Color(.68f, .82f, .95f), new Color(.98f, .9f, .55f), new Color(.75f, .9f, .7f) };
        Color[] roofs = { new Color(.75f, .25f, .2f), new Color(.45f, .3f, .22f), new Color(.3f, .4f, .65f) };
        for (int k = 0; k < 8; k++)
        {
            float a = 22.5f + k * 45f;
            var c = Quaternion.Euler(0, a, 0) * Vector3.forward * HouseRing;
            BuildHouse(c, a + 180f, walls[k % walls.Length], roofs[k % roofs.Length], k);
        }

        // Outdoor clutter with a hiding spot behind each thing (seen from the plaza).
        string[] outdoor = { "Seto", "Arbol", "Carreta", "Barriles", "Seto", "Pozo", "Arbol", "Seto", "Barriles", "Carreta", "Arbol", "Seto" };
        for (int k = 0; k < 16; k++)
        {
            float a = k * 22.5f + 11.25f + (float)(rng.NextDouble() - .5) * 8f;
            if (k % 2 == 1) a = k * 22.5f + 22.5f;   // between two houses
            float r = k % 2 == 1 ? 25.5f : 18.5f + (float)rng.NextDouble() * 1.5f;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.forward; var at = dir * r;
            string key = outdoor[k % outdoor.Length];
            Prop(key, at, a + (key == "Seto" || key == "Carreta" ? 0f : (float)rng.NextDouble() * 360f), true);
            Spots.Add(at + dir * (key == "Arbol" ? 1.1f : 1.5f));
        }
        for (int k = 0; k < 8; k++)
        {   // Far ring: crates and hedges near the wall.
            float a = 22.5f + k * 45f + 22.5f; var dir = Quaternion.Euler(0, a, 0) * Vector3.forward; var at = dir * 33.5f;
            Prop(k % 2 == 0 ? "Seto" : "Barriles", at, a, true);
            Spots.Add(at + dir * 1.4f);
            if (k % 2 == 0) Prop("Arbol", Quaternion.Euler(0, a + 9f, 0) * Vector3.forward * 36.5f, (float)rng.NextDouble() * 360f, true);
        }
        for (int k = 0; k < 6; k++) Prop("Valla", Quaternion.Euler(0, 45f + k * 60f, 0) * Vector3.forward * 30f, 45f + k * 60f + 90f, true);
        foreach (float a in new[] { 60f, 150f, 240f, 330f }) Lamp(Quaternion.Euler(0, a, 0) * Vector3.forward * 11.5f);

        // Village wall all around.
        var wallMat = EscapeKit.Paint(new Color(.62f, .58f, .52f), .05f);
        for (int k = 0; k < 36; k++)
        {
            float a = k * 10f; var at = Quaternion.Euler(0, a, 0) * Vector3.forward * Bound;
            EscapeKit.Block(root, "Muralla", at + Vector3.up * 1.1f, new Vector3(7.2f, 2.2f, .8f), wallMat, .1f, true, new Vector3(0, a, 0));
        }

        var surface = root.gameObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
    }

    void BuildHouse(Vector3 center, float yaw, Color wall, Color roof, int index)
    {
        var h = new GameObject("Casa " + (index + 1)).transform; h.SetParent(root, false);
        h.position = center; h.rotation = Quaternion.Euler(0, yaw, 0);
        const float W = 8f, D = 7f, H = 3.1f, T = .3f, Door = 2f;
        var wm = EscapeKit.Paint(wall, .05f); var trim = EscapeKit.Paint(wall * .7f, .05f);
        EscapeKit.Block(h, "Suelo", new Vector3(0, .03f, 0), new Vector3(W, .06f, D), EscapeKit.Paint(new Color(.62f, .45f, .3f), .1f), .02f, false);
        EscapeKit.Block(h, "Pared trasera", new Vector3(0, H / 2, -D / 2), new Vector3(W, H, T), wm, .05f);
        EscapeKit.Block(h, "Pared izq.", new Vector3(-W / 2, H / 2, 0), new Vector3(T, H, D), wm, .05f);
        EscapeKit.Block(h, "Pared der.", new Vector3(W / 2, H / 2, 0), new Vector3(T, H, D), wm, .05f);
        float side = (W - Door) / 2f;
        EscapeKit.Block(h, "Fachada izq.", new Vector3(-W / 2 + side / 2, H / 2, D / 2), new Vector3(side, H, T), wm, .05f);
        EscapeKit.Block(h, "Fachada der.", new Vector3(W / 2 - side / 2, H / 2, D / 2), new Vector3(side, H, T), wm, .05f);
        EscapeKit.Block(h, "Dintel", new Vector3(0, H - .35f, D / 2), new Vector3(Door, .7f, T), trim, .05f);
        EscapeKit.Block(h, "Zócalo", new Vector3(0, .2f, D / 2 + .02f), new Vector3(W, .4f, T), trim, .04f, false);
        foreach (float x in new[] { -2.6f, 2.6f }) EscapeKit.Block(h, "Ventana", new Vector3(x, 1.7f, D / 2 + .17f), new Vector3(1.1f, .9f, .05f), EscapeKit.Glow(new Color(1f, .85f, .5f), .8f), .02f, false);
        // Roof (no collider): hidden while the local player is inside.
        var rm = EscapeKit.Paint(roof, .1f); var roofParts = new List<Renderer>();
        foreach (int s in new[] { -1, 1 })
            roofParts.Add(EscapeKit.Block(h, "Tejado", new Vector3(0, H + 1f, s * D * .26f), new Vector3(W + .8f, .25f, D * .58f), rm, .06f, false, new Vector3(s * 32f, 0, 0)).GetComponent<Renderer>());   // ridge in the middle, eaves outside
        roofParts.Add(EscapeKit.Block(h, "Frontón", new Vector3(0, H + .55f, D / 2 - .05f), new Vector3(W * .7f, 1.1f, .2f), wm, .05f, false).GetComponent<Renderer>());
        roofParts.Add(EscapeKit.Block(h, "Frontón", new Vector3(0, H + .55f, -D / 2 + .05f), new Vector3(W * .7f, 1.1f, .2f), wm, .05f, false).GetComponent<Renderer>());
        roofParts.Add(EscapeKit.Block(h, "Chimenea", new Vector3(2.4f, H + 1.6f, -1.2f), new Vector3(.6f, 1.4f, .6f), trim, .05f, false).GetComponent<Renderer>());
        Houses.Add(new House { Center = center, Yaw = yaw, Roof = roofParts.ToArray(), Half = new Vector2(W / 2, D / 2) });

        // Furniture: each one hides a corner. Layouts vary per house.
        Vector3 L(float x, float z) => h.TransformPoint(new Vector3(x, 0, z));
        float Y(float a) => yaw + a;
        switch (index % 4)
        {
            case 0:
                Prop("Cama", L(-2.6f, -1.9f), Y(0), true); Spots.Add(L(-3.3f, -3.0f));
                Prop("Armario", L(2.6f, -2.3f), Y(0), true); Spots.Add(L(3.45f, -2.95f));
                Prop("Mesa", L(1.2f, 1f), Y(90), true); Prop("Silla", L(.3f, 1f), Y(90), false); Spots.Add(L(3.2f, 2.8f)); Prop("Barriles", L(1.9f, 1.4f), Y(0), true);
                break;
            case 1:
                Prop("Estante", L(0f, -3.05f), Y(0), true); Prop("Mesa", L(0f, -1.6f), Y(0), true); Spots.Add(L(-3.3f, -3.0f));
                Prop("Barriles", L(2.9f, -2.3f), Y(0), true); Spots.Add(L(3.4f, -3.0f));
                Prop("Armario", L(-2.7f, 1.8f), Y(90), true); Spots.Add(L(-3.4f, 2.95f));
                break;
            case 2:
                Prop("Cama", L(2.6f, -1.9f), Y(0), true); Spots.Add(L(3.3f, -3.0f));
                Prop("Estante", L(-2.6f, -2.95f), Y(0), true); Prop("Barriles", L(-2.7f, -1.5f), Y(0), true); Spots.Add(L(-3.4f, -2.3f));
                Prop("Mesa", L(-1.6f, 1.6f), Y(0), true); Spots.Add(L(-3.3f, 2.9f)); Prop("Silla", L(-1.6f, .9f), Y(180), false);
                break;
            default:
                Prop("Armario", L(-1.4f, -2.8f), Y(0), true); Spots.Add(L(-2.5f, -3.0f));
                Prop("Cama", L(2.4f, -1.9f), Y(0), true); Spots.Add(L(3.35f, -3.0f));
                Prop("Barriles", L(2.6f, 2.1f), Y(0), true); Spots.Add(L(3.35f, 2.95f));
                break;
        }
        EscapeKit.PointLight(h, new Vector3(0, H - .4f, 0), new Color(1f, .85f, .6f), 7f, 1.4f);
    }

    void Lamp(Vector3 at)
    {
        EscapeKit.Block(root, "Farola", at + Vector3.up * 1.6f, new Vector3(.16f, 3.2f, .16f), EscapeKit.Paint(new Color(.2f, .22f, .26f)), .03f, true);
        EscapeKit.Shape(PrimitiveType.Sphere, root, "Farol", at + Vector3.up * 3.3f, Vector3.one * .45f, EscapeKit.Glow(new Color(1f, .9f, .6f), 2f), false);
    }

    static readonly Dictionary<string, GameObject> prefabs = new();
    public static GameObject Load(string key)
    {
        if (!prefabs.TryGetValue(key, out var p) || p == null) prefabs[key] = p = Resources.Load<GameObject>("NaveArt/" + key);
        if (p == null) Debug.LogWarning("Arma tu Nave: falta " + key + " (Multiverse → Zonas → Preparar prefabs)");
        return p;
    }

    GameObject Prop(string key, Vector3 pos, float yaw, bool solid)
    {
        var p = Load(key); if (p == null) return null;
        var go = Instantiate(p, root, false); go.name = key;
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        if (!solid) foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        return go;
    }

    /// <summary>Roofs of the houses the local player is in go shadow-only.</summary>
    public void FadeRoofs(Vector3 player)
    {
        foreach (var h in Houses)
        {
            var local = Quaternion.Inverse(Quaternion.Euler(0, h.Yaw, 0)) * (player - h.Center);
            bool inside = Mathf.Abs(local.x) < h.Half.x + .3f && Mathf.Abs(local.z) < h.Half.y + .3f;
            foreach (var r in h.Roof) if (r != null) r.shadowCastingMode = inside ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    /// <summary>A point on the NavMesh close to p (or p).</summary>
    public static Vector3 OnMesh(Vector3 p, float range = 2f) => NavMesh.SamplePosition(p, out var hit, range, NavMesh.AllAreas) ? hit.position : p;
}
