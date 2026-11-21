using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds "Calle del Fin" at runtime: a 48 × 48 m walled plaza at night with four gates (zombies come in through them,
/// survivors cannot leave), the broken pickup in the middle with its repair zone, cover from the zombie pack
/// (wrecked cars, containers, barrels, pallets) and dark city blocks around it.
/// </summary>
public sealed class SurvivalLevel : MonoBehaviour
{
    public struct Gate { public Vector3 outer, inner; public string name; }

    public const float Half = 40f;   // 2026-11-10: bigger city block with four themed sectors
    public const float GateHalf = 3.6f;
    public const float ZoneRadius = 4.3f;
    public Gate[] Gates { get; private set; }
    public readonly List<Collider> GateBlockers = new();
    public SurvivalTruck Truck { get; private set; }
    public Vector3 TruckPos => Truck != null ? Truck.ZoneCenter : Vector3.zero;
    Transform root;
    public readonly List<SurvivalCache> Caches = new();
    public readonly List<SurvivalLootCrate> Crates = new();
    SurvivalDirector director;

    public void Build(SurvivalDirector d)
    {
        director = d;
        root = new GameObject("Calle del Fin").transform;
        Material asphalt = EscapeKit.Paint(new Color(.2f, .22f, .25f), .1f);
        Material plaza = EscapeKit.Paint(new Color(.27f, .29f, .32f), .08f);
        Material curb = EscapeKit.Paint(new Color(.55f, .56f, .55f));
        Material concrete = EscapeKit.Mat("Hormigón", new Color(.6f, .6f, .58f));
        Material stripe = EscapeKit.Paint(new Color(1f, .82f, .25f));
        Material white = EscapeKit.Paint(new Color(.92f, .92f, .88f));

        // Ground: one solid slab, a lighter plaza and road markings.
        var ground = EscapeKit.Block(root, "Suelo", new Vector3(0, -.5f, 0), new Vector3(170f, 1f, 170f), asphalt, .02f);
        ground.AddComponent<SurvivalShell>();
        EscapeKit.Block(root, "Plaza", new Vector3(0, .01f, 0), new Vector3(Half * 2f + 2f, .02f, Half * 2f + 2f), plaza, .01f, false);
        for (int i = -14; i <= 14; i++)
        {
            if (Mathf.Abs(i) < 3) continue;
            EscapeKit.Block(root, "Línea", new Vector3(0, .025f, i * 3.2f), new Vector3(.22f, .02f, 1.6f), stripe, .005f, false);
            EscapeKit.Block(root, "Línea", new Vector3(i * 3.2f, .025f, 0), new Vector3(1.6f, .02f, .22f), stripe, .005f, false);
        }
        for (int i = -3; i <= 3; i++) EscapeKit.Block(root, "Paso de cebra", new Vector3(i * 1.1f, .025f, -Half + 2.2f), new Vector3(.6f, .02f, 3f), white, .005f, false);

        // Walls with four gates. Zombies ignore the invisible gate blockers; survivors can't leave.
        Gates = new[]
        {
            new Gate { name = "Norte", outer = new Vector3(0, 0, Half + 11f), inner = new Vector3(0, 0, Half - 2.5f) },
            new Gate { name = "Sur", outer = new Vector3(0, 0, -Half - 11f), inner = new Vector3(0, 0, -Half + 2.5f) },
            new Gate { name = "Este", outer = new Vector3(Half + 11f, 0, 0), inner = new Vector3(Half - 2.5f, 0, 0) },
            new Gate { name = "Oeste", outer = new Vector3(-Half - 11f, 0, 0), inner = new Vector3(-Half + 2.5f, 0, 0) },
        };
        float wallLen = Half + 1f - GateHalf, wallMid = GateHalf + wallLen * .5f, edge = Half + 1f;
        for (int s = -1; s <= 1; s += 2)
            for (int side = -1; side <= 1; side += 2)
            {
                Wall(new Vector3(side * wallMid, 1.1f, s * edge), new Vector3(wallLen, 2.2f, .9f), concrete);
                Wall(new Vector3(s * edge, 1.1f, side * wallMid), new Vector3(.9f, 2.2f, wallLen), concrete);
            }
        foreach (var g in Gates)
        {
            Vector3 dir = (g.outer - g.inner).normalized;
            Vector3 at = new Vector3(dir.x * edge, 1.5f, dir.z * edge);
            var blocker = new GameObject("Barrera invisible " + g.name);
            blocker.transform.SetParent(root, false);
            blocker.transform.localPosition = at;
            var box = blocker.AddComponent<BoxCollider>();
            box.size = Mathf.Abs(dir.x) > .5f ? new Vector3(.8f, 3f, GateHalf * 2f) : new Vector3(GateHalf * 2f, 3f, .8f);
            blocker.AddComponent<SurvivalShell>();
            GateBlockers.Add(box);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float yaw = Quaternion.LookRotation(side).eulerAngles.y;
            SurvivalKit.Model("TrafficBarrier_1", root, new Vector3(at.x, 0, at.z) + side * (GateHalf + .2f) - dir * .9f, yaw, 1.1f, true);
            SurvivalKit.Model("TrafficBarrier_2", root, new Vector3(at.x, 0, at.z) - side * (GateHalf + .2f) - dir * .9f, yaw + 180f, 1.1f, true);
            EscapeKit.PointLight(root, g.outer * .8f + Vector3.up * 2f, new Color(.45f, 1f, .4f), 14f, 2.5f);
            // A green glow on the street past each gate: that's where they come from.
            EscapeKit.Block(root, "Niebla zombi", new Vector3(g.outer.x * .82f, .03f, g.outer.z * .82f), Mathf.Abs(dir.x) > .5f ? new Vector3(10f, .02f, GateHalf * 2f) : new Vector3(GateHalf * 2f, .02f, 10f), EscapeKit.Glow(new Color(.3f, .8f, .3f), .35f), .01f, false);
        }

        // Curbs inside the walls (visual only).
        EscapeKit.Block(root, "Bordillo", new Vector3(0, .07f, Half + .2f), new Vector3(Half * 2f, .14f, .6f), curb, .03f, false);
        EscapeKit.Block(root, "Bordillo", new Vector3(0, .07f, -Half - .2f), new Vector3(Half * 2f, .14f, .6f), curb, .03f, false);
        EscapeKit.Block(root, "Bordillo", new Vector3(Half + .2f, .07f, 0), new Vector3(.6f, .14f, Half * 2f), curb, .03f, false);
        EscapeKit.Block(root, "Bordillo", new Vector3(-Half - .2f, .07f, 0), new Vector3(.6f, .14f, Half * 2f), curb, .03f, false);

        // The truck and its repair zone (a little south of the centre so the north gate is the way out).
        var truckRoot = new GameObject("Camioneta averiada");
        truckRoot.transform.SetParent(root, false);
        truckRoot.transform.localPosition = new Vector3(0, 0, -3f);
        Truck = truckRoot.AddComponent<SurvivalTruck>();
        Truck.Build(director, this);

        Dress();
        Sectors();
        City();
    }

    void Wall(Vector3 pos, Vector3 size, Material mat)
    {
        var w = EscapeKit.Block(root, "Muro", pos, size, mat, .08f);
        w.AddComponent<SurvivalShell>();
        // Warning stripes on top.
        EscapeKit.Block(w.transform, "Franja", new Vector3(0, size.y * .5f + .04f, 0), new Vector3(size.x + .02f, .08f, size.z + .02f), EscapeKit.Paint(new Color(.95f, .75f, .2f)), .02f, false);
    }

    void Dress()
    {
        void P(string key, float x, float z, float yaw, float h, bool solid = true) => SurvivalKit.Model(key, root, new Vector3(x, 0, z), yaw, h, solid);
        // Cover and obstacles, away from the repair zone and the gate lanes.
        P("Vehicle_Sports", -14f, 12f, 35f, 1.35f);
        P("Vehicle_Truck", 16f, -14f, -55f, 2.7f);
        P("Container_Red", -19f, -16.5f, 90f, 2.6f);
        P("Container_Green", 18.5f, 17.5f, 0f, 2.6f);
        P("Barrel", -9f, -18.5f, 0f, 1.05f); P("Barrel", -7.8f, -19.3f, 40f, 1.05f); P("Barrel", -8.4f, -17.4f, 80f, 1.05f);
        P("Barrel", 20.5f, 5f, 0f, 1.05f); P("Barrel", 21.3f, 6.2f, 30f, 1.05f);
        P("Pallet", 10f, 16f, 15f, .22f, false); P("Pallet_Broken", 11.6f, 17.2f, -20f, .22f, false);
        P("Wheels_Stack", -21f, 4f, 0f, 1f); P("Wheels_Stack", 7f, -20.5f, 0f, 1f);
        P("TrashBag_1", 12f, -21f, 0f, .7f); P("TrashBag_2", 13.2f, -21.4f, 60f, .7f); P("TrashBag_1", -20.8f, 13f, 20f, .7f);
        P("Couch", -11f, -9f, 25f, .9f);
        P("FireHydrant", 9f, 9f, 0f, .8f);
        P("Pipes", -16f, -3f, 90f, .8f);
        P("TrafficCone_1", 5f, 13f, 0f, .7f); P("TrafficCone_2", 6.2f, 13.8f, 30f, .7f); P("TrafficCone_1", -5.5f, -14f, 0f, .7f);
        P("CinderBlock", -4f, 19f, 10f, .3f, false); P("CinderBlock", -3.2f, 19.4f, -30f, .3f, false);
        P("Chest", 21.5f, -21f, -45f, .8f);
        foreach (var c in new[] { new Vector2(-21.5f, -21.5f), new Vector2(21.5f, -21.5f), new Vector2(-21.5f, 21.5f), new Vector2(21.5f, 21.5f) })
        {
            P("StreetLights", c.x, c.y, Mathf.Atan2(-c.x, -c.y) * Mathf.Rad2Deg, 5.5f);
            EscapeKit.PointLight(root, new Vector3(c.x * .9f, 5f, c.y * .9f), new Color(1f, .82f, .55f), 18f, 2.4f);
        }
        P("TownSign", 6f, -Half - 5f, 180f, 2.6f);
        P("WaterTower", Half + 10f, Half + 10f, 20f, 15f);
    }

    // Four themed corners, each with a guarded cache (some waves hide the part there) and supply crates.
    // 2026-11-17: every sector is a real place built from the toy-style landmarks (Tools/Blender/zonas.py):
    // a petrol station with canopy, pumps, shop and price pylon; a junkyard with a crane and crushed-car stacks;
    // a warehouse with racks and a forklift; a fire station with its tower and fire engine.
    void Sectors()
    {
        void P(string key, Vector3 c, float x, float z, float yaw, float h, bool solid = true) => SurvivalKit.Model(key, root, c + new Vector3(x, 0, z), yaw, h, solid);
        GameObject L(string key, float x, float z, float yaw, bool shell = false) => SurvivalLandmark.Place(key, root, new Vector3(x, 0, z), yaw, shell);
        void Pad(string name, float x, float z, float sx, float sz, Color col, float y = .015f) => EscapeKit.Block(root, name, new Vector3(x, y, z), new Vector3(sx, .02f, sz), EscapeKit.Paint(col, .05f), .01f, false);
        void Lamp(Vector3 at, Color col, float range, float intensity) => EscapeKit.PointLight(root, at, col, range, intensity);
        float o = 28f;
        Vector3 ne = new Vector3(o, 0, o), nw = new Vector3(-o, 0, o), se = new Vector3(o, 0, -o), sw = new Vector3(-o, 0, -o);
        Material paintW = EscapeKit.Paint(new Color(.95f, .95f, .9f)), paintY = EscapeKit.Paint(new Color(1f, .8f, .2f));

        // SE · LA GASOLINERA: canopy over the cache, two pump islands, shop, price pylon, tanker.
        Pad("Gasolinera · losa", 29.5f, -29.5f, 21f, 21f, new Color(.62f, .62f, .6f));
        for (int i = 0; i < 6; i++) EscapeKit.Block(root, "Plaza de aparcamiento", new Vector3(21f, .03f, -21.5f - i * 2.6f), new Vector3(3.2f, .02f, .12f), paintW, .005f, false);
        EscapeKit.Block(root, "Línea de parada", new Vector3(28f, .03f, -21.2f), new Vector3(12f, .02f, .25f), paintY, .005f, false);
        L("Gasolinera_Surtidores", 28f, -28f, 0f);
        var roof = L("Gasolinera_Techo", 28f, -28f, 0f, true); if (roof != null) roof.AddComponent<SurvivalRoofFade>().Setup(director, new Vector2(7.2f, 5.2f));
        L("Gasolinera_Tienda", 30f, -36.6f, 0f, true);
        L("Gasolinera_Totem", 37.6f, -20.5f, -70f);
        L("Gasolinera_Cisterna", 38.1f, -28.6f, 0f);
        L("Gasolinera_Aire", 20.4f, -30f, 90f);
        L("Gasolinera_Aceite", 23.6f, -35.8f, 15f);
        for (int i = 0; i < 4; i++) P("Barrel", se, -6.5f + (i % 2) * 1.05f, -10.3f + (i / 2) * 1.05f, i * 40, 1.05f);
        Lamp(se + new Vector3(0, 4f, 0), new Color(1f, .97f, .9f), 16, 3.2f);
        Lamp(new Vector3(30f, 3f, -32.5f), new Color(1f, .8f, .5f), 9, 2f);
        Lamp(new Vector3(37f, 6.5f, -19f), new Color(1f, .4f, .3f), 8, 2f);

        // NE · EL DESGUACE: dirt yard, crane holding a car over the cache, crushed-car stacks, office with its sign.
        Pad("Desguace · tierra", 29.5f, 29.5f, 21f, 21f, new Color(.42f, .33f, .24f));
        for (int i = 0; i < 7; i++) Pad("Charco de aceite", 22f + (i * 37 % 15), 22f + (i * 53 % 16), 1.6f + (i % 3) * .6f, 1.2f + (i % 2) * .7f, new Color(.18f, .16f, .15f), .022f);
        L("Desguace_Grua", 36.4f, 36.4f, 225f);
        L("Desguace_Pila", 37.7f, 22f, 0f); L("Desguace_Pila", 37.7f, 26.9f, 180f); L("Desguace_Pila", 30f, 37.9f, 90f);
        L("Desguace_Caseta", 22.2f, 37.9f, 180f, true);
        L("Desguace_Valla", 19.2f, 31f, 90f); L("Desguace_Valla", 31f, 19.2f, 0f);
        L("Desguace_Neumaticos", 22.5f, 33.2f, 0f);
        P("Wheel", ne, 3, -7, 70, .6f, false); P("Blood_1", ne, 2, 3, 0, .05f, false);
        Lamp(new Vector3(31f, 6f, 31f), new Color(1f, .7f, .35f), 16, 2.6f);
        Lamp(new Vector3(22f, 4.5f, 35f), new Color(1f, .6f, .3f), 8, 2f);

        // NW · EL ALMACÉN: loading yard with hazard lines, warehouse, racks, forklift, pallets, containers.
        Pad("Almacén · patio", -29.5f, 29.5f, 21f, 21f, new Color(.4f, .42f, .45f));
        for (int i = 0; i < 9; i++) EscapeKit.Block(root, "Franja de carga", new Vector3(-37f + i * 1.25f, .03f, 30.8f), new Vector3(.5f, .02f, 1.6f), paintY, .005f, false, new Vector3(0, 35, 0));
        L("Almacen_Nave", -33f, 36.1f, 180f, true);
        L("Almacen_Estanteria", -39.2f, 24.5f, 90f); L("Almacen_Estanteria", -39.2f, 17.5f, 90f);
        L("Almacen_Carretilla", -23.5f, 31f, 200f);
        L("Almacen_Cajas", -25.5f, 38.8f, 0f); L("Almacen_Cajas", -23.8f, 38.8f, 10f); L("Almacen_Cajas", -31.5f, 23f, -8f);
        P("Container_Red", nw, 6.5f, -4f, 90, 2.6f); P("Container_Green", nw, -6f, -8.5f, 0, 2.6f);
        P("Pallet", nw, 2, 6.5f, 10, .22f, false); P("Pallet_Broken", nw, 3, -8, -20, .22f, false);
        Lamp(new Vector3(-30f, 5f, 30f), new Color(.6f, .82f, 1f), 16, 2.6f);
        Lamp(new Vector3(-34.5f, 4.5f, 30.5f), new Color(1f, .9f, .6f), 8, 2.2f);

        // SW · PARQUE DE BOMBEROS: red apron, station with two garage doors, hose tower, fire engine.
        Pad("Bomberos · patio", -29.5f, -29.5f, 21f, 21f, new Color(.55f, .55f, .53f));
        Pad("Bomberos · rampa roja", -31f, -27.8f, 12f, 8.4f, new Color(.62f, .18f, .16f), .02f);
        for (int i = 0; i < 2; i++) EscapeKit.Block(root, "Prohibido aparcar", new Vector3(-34f + i * 6f, .03f, -27.8f), new Vector3(.25f, .02f, 8f), paintW, .005f, false);
        L("Bomberos_Cuartel", -31f, -36.1f, 0f, true);
        L("Bomberos_Torre", -38.9f, -37.2f, 0f, true);
        L("Bomberos_Camion", -34f, -25.6f, 0f);
        L("Bomberos_Carrete", -23.6f, -33.9f, 0f); L("Bomberos_Carrete", -22.2f, -33.9f, 0f);
        P("FireHydrant", sw, 6, 6, 0, .8f); P("TrafficCone_1", sw, 2, -1.5f, 0, .7f); P("TrafficCone_2", sw, 3, -.6f, 30, .7f); P("TrafficCone_1", sw, 4, .4f, 0, .7f);
        P("StreetLights", sw, 9, 8, 225, 5.5f);
        Lamp(new Vector3(-31f, 5f, -30f), new Color(1f, .55f, .45f), 15, 2.6f);
        Lamp(new Vector3(-31f, 4.2f, -30.8f), new Color(1f, .9f, .7f), 9, 2f);
        string[] names = { "EL DESGUACE", "EL ALMACÉN", "LA GASOLINERA", "PARQUE DE BOMBEROS" };
        Vector3[] centers = { ne, nw, se, sw };
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Cofre · " + names[i]); go.transform.SetParent(root, false); go.transform.localPosition = centers[i];
            var cache = go.AddComponent<SurvivalCache>(); cache.Build(names[i]); Caches.Add(cache);
        }
        // Supply crates scattered around the map (hold still next to one to open it).
        Vector3[] crates = { new(-33, 0, 8), new(33, 0, -9), new(10, 0, 34), new(-12, 0, -34), new(-34, 0, -14), new(34, 0, 14), new(-20, 0, 34), new(20, 0, -34) };
        foreach (var c in crates)
        {
            var go = new GameObject("Caja de suministros"); go.transform.SetParent(root, false); go.transform.localPosition = c; go.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            var crate = go.AddComponent<SurvivalLootCrate>(); crate.Build(director); Crates.Add(crate);
        }
        // Extra lamps so the bigger plaza is readable at night.
        foreach (var c in new[] { new Vector2(0, 30), new Vector2(0, -30), new Vector2(30, 0), new Vector2(-30, 0) })
            EscapeKit.PointLight(root, new Vector3(c.x, 5.5f, c.y), new Color(1, .82f, .55f), 20, 2);
    }
    public void RefillCrates(int count)
    {
        foreach (var c in Crates.Where(c => c.IsOpen).OrderBy(_ => Random.value).Take(count)) c.Close();
    }

    void City()
    {
        // Dark blocks of buildings around the plaza, leaving the four streets open.
        var rng = new System.Random(7);
        Material[] mats = { EscapeKit.Paint(new Color(.16f, .2f, .26f)), EscapeKit.Paint(new Color(.2f, .19f, .24f)), EscapeKit.Paint(new Color(.14f, .22f, .22f)) };
        Material window = EscapeKit.Glow(new Color(1f, .85f, .5f), 1.2f);
        for (int ring = 0; ring < 2; ring++)
            for (float t = -48f; t <= 48f; t += 9f)
                for (int side = 0; side < 4; side++)
                {
                    if (Mathf.Abs(t) < 8.5f) continue;
                    float dist = Half + 9f + ring * 11f + (float)rng.NextDouble() * 2f;
                    Vector3 pos = side switch { 0 => new Vector3(t, 0, dist), 1 => new Vector3(t, 0, -dist), 2 => new Vector3(dist, 0, t), _ => new Vector3(-dist, 0, t) };
                    if (Vector3.Distance(pos, new Vector3(Half + 10f, 0, Half + 10f)) < 8f) continue; // water tower
                    float h = 6f + (float)rng.NextDouble() * 12f + ring * 5f;
                    var b = EscapeKit.Block(root, "Edificio", pos + Vector3.up * h * .5f, new Vector3(7.5f, h, 7.5f), mats[rng.Next(mats.Length)], .15f, false);
                    for (int w = 0; w < 3; w++)
                        if (rng.NextDouble() < .45)
                        {
                            float wy = -h * .5f + 2.5f + w * 3.2f;
                            if (wy > h * .5f - 1f) break;
                            Vector3 face = -pos.normalized;
                            Vector3 local = new Vector3(Mathf.Round(face.x) * 3.78f + ((float)rng.NextDouble() - .5f) * 3f * Mathf.Abs(Mathf.Round(face.z)), wy, Mathf.Round(face.z) * 3.78f + ((float)rng.NextDouble() - .5f) * 3f * Mathf.Abs(Mathf.Round(face.x)));
                            EscapeKit.Block(b.transform, "Ventana", local, Mathf.Abs(face.x) > .5f ? new Vector3(.06f, 1f, .9f) : new Vector3(.9f, 1f, .06f), window, .01f, false);
                        }
                }
    }

    // ------------------------------------------------------------------ queries

    public bool Inside(Vector3 p, float margin) => Mathf.Abs(p.x) < Half - margin && Mathf.Abs(p.z) < Half - margin;

    public Vector3 ClampInside(Vector3 p, float margin)
    {
        p.x = Mathf.Clamp(p.x, -Half + margin, Half - margin);
        p.z = Mathf.Clamp(p.z, -Half + margin, Half - margin);
        return p;
    }

    public bool InTruckZone(Vector3 p)
    {
        Vector3 d = p - TruckPos; d.y = 0f;
        return d.magnitude < ZoneRadius;
    }

    public Vector3 SpawnPoint(int slot)
    {
        float a = (slot * 60f + 30f) * Mathf.Deg2Rad;
        return TruckPos + new Vector3(Mathf.Sin(a) * 3.4f, .1f, Mathf.Cos(a) * 3.4f);
    }
}
