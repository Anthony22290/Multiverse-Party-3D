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
    void Sectors()
    {
        void P(string key, Vector3 c, float x, float z, float yaw, float h, bool solid = true) => SurvivalKit.Model(key, root, c + new Vector3(x, 0, z), yaw, h, solid);
        void Sign(string text, Vector3 c, Color col)
        {
            EscapeKit.Text(root, text, c + new Vector3(0, 4.2f, -7.5f), new Vector3(0, 0, 0), .07f, col);
            EscapeKit.PointLight(root, c + Vector3.up * 6, col, 22, 2.2f);
        }
        float o = 28f;
        // NE · junkyard: wrecked cars and tyres
        Vector3 ne = new Vector3(o, 0, o); Sign("EL DESGUACE", ne, new Color(1, .7f, .35f));
        P("Vehicle_Sports", ne, -6, 4, 120, 1.35f); P("Vehicle_Pickup", ne, 5, 6, 200, 1.7f); P("Vehicle_Sports_Armored", ne, 6.5f, -4, 30, 1.4f);
        P("Vehicle_Truck", ne, -7, -6, 80, 2.7f); P("Vehicle_Pickup_Armored", ne, 0, 9, 95, 1.8f);
        P("Wheels_Stack", ne, -3, 8, 0, 1); P("Wheels_Stack", ne, 9, 1, 0, 1); P("Wheel", ne, 3, -7, 70, .6f, false); P("Blood_1", ne, 2, 3, 0, .05f, false);
        // NW · warehouse: container corridors and pallets
        Vector3 nw = new Vector3(-o, 0, o); Sign("EL ALMACÉN", nw, new Color(.5f, .8f, 1));
        P("Container_Red", nw, -6, 5, 0, 2.6f); P("Container_Green", nw, 6, 5, 0, 2.6f); P("Container_Red", nw, -7, -5, 90, 2.6f); P("Container_Green", nw, 8, -6, 90, 2.6f);
        P("Pallet", nw, -2, 8, 10, .22f, false); P("Pallet_Broken", nw, 3, -8, -20, .22f, false); P("Chest_Special", nw, -9, 0, 90, .9f); P("CinderBlock", nw, 4, 9, 30, .3f, false);
        // SE · petrol station: barrels and a tanker
        Vector3 se = new Vector3(o, 0, -o); Sign("LA GASOLINERA", se, new Color(1, .45f, .35f));
        P("Vehicle_Truck_Armored", se, -6, 5, -30, 2.8f); P("Pipes", se, 7, 6, 0, .8f); P("PlasticBarrier", se, -3, -8, 90, 1);
        for (int i = 0; i < 7; i++) P("Barrel", se, 6 + (i % 3) * 1.1f, -5 - (i / 3) * 1.1f, i * 40, 1.05f);
        P("TrafficLight_1", se, -9, -3, 90, 4.5f);
        // SW · fire station yard
        Vector3 sw = new Vector3(-o, 0, -o); Sign("PARQUE DE BOMBEROS", sw, new Color(.6f, 1, .5f));
        P("Vehicle_Pickup", sw, -6, -5, 45, 1.7f); P("FireHydrant", sw, 4, 6, 0, .8f); P("Couch", sw, 7, -3, 120, .9f);
        P("TrashBag_1", sw, -8, 6, 0, .7f); P("TrashBag_2", sw, -7, 7, 60, .7f); P("TrafficCone_1", sw, 2, -8, 0, .7f); P("TrafficCone_2", sw, 3, -7.4f, 30, .7f); P("StreetLights", sw, 9, 8, 225, 5.5f);
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
