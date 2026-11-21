using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The street of Reparto Loco: the shop (south, z &lt; 0) and the neighbourhood (north, z &gt; 28) with three roads
/// in between (3 + 4 + 2 lanes) and two grass medians to catch your breath. Traffic is a pure function of the round
/// seed and the clock, so every computer sees exactly the same trucks without sending them over the network.
/// </summary>
public sealed class RepartoRoad : MonoBehaviour
{
    public const float SouthEdge = -.4f, NorthEdge = 28.2f, HalfWidth = 12.5f, LaneHalf = 1.25f, Loop = 64f, Ramp = .45f, RoundSeconds = 90f;
    /// <summary>The parcel stall in the middle of the shop sidewalk: parcels are only picked up here (2026-11-19).</summary>
    public static readonly Vector3 Stall = new Vector3(0f, 0f, -2.4f);
    public const float StallRadius = 2.3f;
    public static bool AtStall(Vector3 p) { var d = p - Stall; d.y = 0; return d.magnitude < StallRadius; }
    public static readonly float[] LaneZ = { 1.5f, 4f, 6.5f, 11.5f, 14f, 16.5f, 19f, 24f, 26.5f };
    public static readonly (float a, float b)[] Roads = { (.25f, 7.75f), (10.25f, 20.25f), (22.75f, 27.75f) };

    public sealed class Lane { public float Z, Dir, Speed; public readonly List<Vehicle> Cars = new(); }
    public sealed class Vehicle { public float Offset, Length; public Transform T; public int Kind; }
    public readonly List<Lane> Lanes = new();
    Transform root;

    public void Build(int seed)
    {
        root = new GameObject("Calle").transform;
        var rng = new System.Random(seed);
        var asphalt = EscapeKit.Paint(new Color(.27f, .28f, .31f), .15f); var white = EscapeKit.Paint(new Color(.95f, .95f, .92f), .2f);
        var walk = EscapeKit.Paint(new Color(.78f, .76f, .72f), .1f);
        var curb = EscapeKit.Paint(new Color(.62f, .6f, .58f), .1f); var grass = EscapeKit.Paint(new Color(.5f, .76f, .38f), .05f);
        var floor = new GameObject("Suelo", typeof(BoxCollider)); floor.transform.SetParent(root, false);
        floor.transform.position = new Vector3(0, -.5f, 14f); floor.GetComponent<BoxCollider>().size = new Vector3(120, 1, 120);
        EscapeKit.Block(root, "Césped", new Vector3(0, -.06f, 14f), new Vector3(120, .1f, 90), grass, .01f, false);
        // Sidewalks (both ends) and medians.
        EscapeKit.Block(root, "Acera tienda", new Vector3(0, .06f, -3f), new Vector3(70, .12f, 6.5f), walk, .02f, false);
        EscapeKit.Block(root, "Acera barrio", new Vector3(0, .06f, 31f), new Vector3(70, .12f, 6.5f), walk, .02f, false);
        foreach (var r in Roads)
        {
            float mid = (r.a + r.b) * .5f, len = r.b - r.a;
            EscapeKit.Block(root, "Asfalto", new Vector3(0, .01f, mid), new Vector3(70, .02f, len), asphalt, .005f, false);
            EscapeKit.Block(root, "Bordillo", new Vector3(0, .07f, r.a - .07f), new Vector3(70, .14f, .14f), curb, .02f, false);
            EscapeKit.Block(root, "Bordillo", new Vector3(0, .07f, r.b + .07f), new Vector3(70, .14f, .14f), curb, .02f, false);
        }
        EscapeKit.Block(root, "Mediana", new Vector3(0, .05f, 9f), new Vector3(70, .1f, 2.3f), grass, .02f, false);
        EscapeKit.Block(root, "Mediana", new Vector3(0, .05f, 21.5f), new Vector3(70, .1f, 2.3f), grass, .02f, false);

        // Lanes: direction, speed and the vehicles on each loop.
        for (int i = 0; i < LaneZ.Length; i++)
        {
            var lane = new Lane { Z = LaneZ[i], Dir = rng.NextDouble() < .5 ? -1f : 1f, Speed = Mathf.Lerp(4f, 8.6f, (float)rng.NextDouble()) };
            if (i > 0 && LaneZ[i] - LaneZ[i - 1] < 3f && rng.NextDouble() < .7) lane.Dir = -Lanes[i - 1].Dir;
            Lanes.Add(lane);
            // Lane markings.
            float lineZ = LaneZ[i] + LaneHalf;
            bool roadEnd = System.Array.Exists(Roads, r => Mathf.Abs(r.b - lineZ) < .3f);
            if (!roadEnd && i + 1 < LaneZ.Length)
            {
                for (float x = -34f; x < 34f; x += 3f) EscapeKit.Block(root, "Raya", new Vector3(x, .025f, lineZ), new Vector3(1.6f, .01f, .12f), white, .005f, false);
            }
            // Vehicles: random lengths and gaps, always one big gap so the lane can be crossed.
            // 2–4 vehicles per lane, cars the most common, buses the rarest. (2026-11-19: faster lanes and slightly
            // tighter gaps than the first version; 3–4 per lane made the 4-lane road almost uncrossable.)
            float x0 = 0f; int n = 0, max = 2 + rng.Next(3);
            while (x0 < Loop - 12f && n < max)
            {
                double kr = rng.NextDouble(); int kind = kr < .42 ? 0 : kr < .68 ? 1 : kr < .88 ? 2 : 3;
                float len = kind == 0 ? 3.2f : kind == 1 ? 4.2f : kind == 2 ? 6.2f : 7f;
                var v = new Vehicle { Offset = x0 + len * .5f, Length = len, Kind = kind };
                v.T = BuildVehicle(kind, rng, lane.Dir);
                lane.Cars.Add(v); n++;
                x0 += len + Mathf.Lerp(8f, 17f, (float)rng.NextDouble());
            }
        }
        BuildShop(); BuildStall(); BuildHomes(rng);
        // Side walls (players only) and lamp posts on the medians.
        foreach (int s in new[] { -1, 1 })
        {
            var w = new GameObject("Límite", typeof(BoxCollider)); w.transform.SetParent(root, false);
            w.transform.position = new Vector3(s * (HalfWidth + .5f), 2f, 14f); w.GetComponent<BoxCollider>().size = new Vector3(1f, 4f, 50f);
            EscapeKit.Block(root, "Valla obras", new Vector3(s * (HalfWidth + .4f), .5f, -3f), new Vector3(.25f, 1f, 6f), EscapeKit.Paint(new Color(1f, .5f, .1f), .3f), .04f, false);
            EscapeKit.Block(root, "Valla obras", new Vector3(s * (HalfWidth + .4f), .5f, 31f), new Vector3(.25f, 1f, 6f), EscapeKit.Paint(new Color(1f, .5f, .1f), .3f), .04f, false);
        }
        foreach (var w in new[] { (new Vector3(0, 2f, -6.6f), new Vector3(60f, 4f, 1f)), (new Vector3(0, 2f, 34.6f), new Vector3(60f, 4f, 1f)) })
        { var g = new GameObject("Límite", typeof(BoxCollider)); g.transform.SetParent(root, false); g.transform.position = w.Item1; g.GetComponent<BoxCollider>().size = w.Item2; }
        var pole = EscapeKit.Paint(new Color(.3f, .32f, .36f), .4f); var lamp = EscapeKit.Glow(new Color(1f, .92f, .7f), 1.6f);
        foreach (float z in new[] { 9f, 21.5f })
            for (float x = -10f; x <= 10f; x += 10f)
            {
                EscapeKit.Block(root, "Farola", new Vector3(x, 1.8f, z), new Vector3(.14f, 3.6f, .14f), pole, .02f, false);
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Luz", new Vector3(x, 3.7f, z), Vector3.one * .4f, lamp, false);
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Arbusto", new Vector3(x + 4f, .45f, z), new Vector3(1.4f, .9f, 1.1f), EscapeKit.Paint(new Color(.32f, .6f, .3f), .1f), false);
            }
    }

    Transform BuildVehicle(int kind, System.Random rng, float dir)
    {
        Color[] paints = { new Color(.9f, .25f, .22f), new Color(.25f, .5f, .95f), new Color(.98f, .8f, .2f), new Color(.35f, .8f, .45f), new Color(.95f, .55f, .2f), new Color(.75f, .4f, .9f), new Color(.92f, .92f, .95f) };
        var paint = EscapeKit.Paint(paints[rng.Next(paints.Length)], .45f);
        var glass = EscapeKit.Paint(new Color(.55f, .75f, .9f), .85f); var tyre = EscapeKit.Paint(new Color(.12f, .12f, .14f), .2f);
        var light = EscapeKit.Glow(new Color(1f, .95f, .75f), 1.5f);
        var v = new GameObject(kind == 0 ? "Coche" : kind == 1 ? "Furgoneta" : kind == 2 ? "Camión" : "Autobús").transform; v.SetParent(root, false);
        var body = new GameObject("Carrocería").transform; body.SetParent(v, false); body.localRotation = Quaternion.Euler(0, dir > 0 ? 90f : -90f, 0);
        float len = kind == 0 ? 3.2f : kind == 1 ? 4.2f : kind == 2 ? 6.2f : 7f;
        if (kind == 0)
        {
            EscapeKit.Block(body, "Chasis", new Vector3(0, .55f, 0), new Vector3(1.7f, .6f, len), paint, .15f, false);
            EscapeKit.Block(body, "Cabina", new Vector3(0, 1.1f, -.15f), new Vector3(1.5f, .55f, 1.7f), glass, .12f, false);
        }
        else if (kind == 1)
        {
            EscapeKit.Block(body, "Caja", new Vector3(0, 1.05f, -.3f), new Vector3(1.9f, 1.6f, len - .6f), paint, .12f, false);
            EscapeKit.Block(body, "Morro", new Vector3(0, .7f, len * .5f - .4f), new Vector3(1.85f, .9f, .8f), paint, .12f, false);
            EscapeKit.Block(body, "Parabrisas", new Vector3(0, 1.4f, len * .5f - .62f), new Vector3(1.6f, .55f, .1f), glass, .03f, false);
        }
        else if (kind == 2)
        {
            EscapeKit.Block(body, "Remolque", new Vector3(0, 1.45f, -.9f), new Vector3(2.2f, 2.3f, len - 1.9f), EscapeKit.Paint(new Color(.93f, .93f, .9f), .3f), .1f, false);
            EscapeKit.Block(body, "Franja", new Vector3(0, 1.45f, -.9f), new Vector3(2.24f, .4f, len - 2.1f), paint, .03f, false);
            EscapeKit.Block(body, "Cabina", new Vector3(0, 1.15f, len * .5f - .75f), new Vector3(2.1f, 1.7f, 1.4f), paint, .15f, false);
            EscapeKit.Block(body, "Parabrisas", new Vector3(0, 1.55f, len * .5f - .04f), new Vector3(1.8f, .6f, .08f), glass, .03f, false);
        }
        else
        {
            EscapeKit.Block(body, "Bus", new Vector3(0, 1.4f, 0), new Vector3(2.3f, 2.3f, len), EscapeKit.Paint(new Color(1f, .78f, .15f), .4f), .2f, false);
            for (int w = -2; w <= 2; w++) EscapeKit.Block(body, "Ventana", new Vector3(0, 1.85f, w * 1.25f), new Vector3(2.34f, .7f, .9f), glass, .03f, false);
        }
        foreach (float z in new[] { -len * .32f, len * .32f }) foreach (float x in new[] { -.9f, .9f })
            EscapeKit.Shape(PrimitiveType.Cylinder, body, "Rueda", new Vector3(x * (kind >= 2 ? 1.12f : 1f), .35f, z), new Vector3(.7f, .12f, .7f), tyre, false, new Vector3(0, 0, 90f));
        foreach (float x in new[] { -.6f, .6f }) EscapeKit.Shape(PrimitiveType.Sphere, body, "Faro", new Vector3(x, .7f, len * .5f), Vector3.one * .22f, light, false);
        return v;
    }

    void BuildShop()
    {
        var wall = EscapeKit.Paint(new Color(.96f, .86f, .7f), .1f); var awn = EscapeKit.Paint(new Color(.9f, .3f, .25f), .3f); var white = EscapeKit.Paint(Color.white, .3f);
        var box = EscapeKit.Paint(new Color(.78f, .58f, .36f), .1f);
        EscapeKit.Block(root, "Tienda", new Vector3(0, 2f, -7.6f), new Vector3(16f, 4f, 2f), wall, .08f, false);
        for (int i = 0; i < 8; i++) EscapeKit.Block(root, "Toldo", new Vector3(-7f + i * 2f, 3.4f, -6.2f), new Vector3(2f, .12f, 1.6f), i % 2 == 0 ? awn : white, .03f, false, new Vector3(-15f, 0, 0));
        EscapeKit.Text(root, "TIENDA · RECOGE AQUÍ", new Vector3(0, 4.4f, -6.55f), new Vector3(0, 180f, 0), .07f, new Color(.6f, .15f, .1f));
        for (int i = 0; i < 6; i++) EscapeKit.Block(root, "Cajas", new Vector3(-10f + i * 4f, .4f + (i % 2) * .1f, -5.6f), new Vector3(.9f, .8f + (i % 2) * .2f, .9f), box, .05f, false, new Vector3(0, i * 17f, 0));
    }

    void BuildStall()
    {
        var wood = EscapeKit.Paint(new Color(.62f, .42f, .25f), .15f); var awn = EscapeKit.Paint(new Color(.2f, .55f, .95f), .3f); var white = EscapeKit.Paint(Color.white, .3f);
        var box = EscapeKit.Paint(new Color(.78f, .58f, .36f), .1f); var tape = EscapeKit.Paint(new Color(.9f, .85f, .7f), .3f);
        var s = new GameObject("Puesto de paquetes").transform; s.SetParent(root, false); s.position = Stall + new Vector3(0, 0, -1.6f);
        EscapeKit.Block(s, "Mostrador", new Vector3(0, .55f, 0), new Vector3(3.4f, 1.1f, .9f), wood, .06f, true);
        EscapeKit.Block(s, "Tablero", new Vector3(0, 1.12f, .05f), new Vector3(3.6f, .08f, 1.1f), EscapeKit.Paint(new Color(.85f, .7f, .45f), .2f), .03f, false);
        foreach (float x in new[] { -1.6f, 1.6f }) EscapeKit.Block(s, "Poste", new Vector3(x, 1.4f, -.35f), new Vector3(.12f, 2.8f, .12f), wood, .02f, false);
        for (int i = 0; i < 6; i++) EscapeKit.Block(s, "Toldo", new Vector3(-1.5f + i * .6f, 2.75f, .05f), new Vector3(.6f, .1f, 1.5f), i % 2 == 0 ? awn : white, .02f, false, new Vector3(-14f, 0, 0));
        for (int i = 0; i < 5; i++)
        {
            var b = EscapeKit.Block(s, "Paquete", new Vector3(-1.2f + i * .6f, 1.36f + (i % 2) * .05f, .05f), new Vector3(.5f, .4f + (i % 2) * .1f, .42f), box, .04f, false, new Vector3(0, i * 13f, 0));
            EscapeKit.Block(b.transform, "Cinta", Vector3.zero, new Vector3(.51f, .41f, .08f), tape, .01f, false);
        }
        EscapeKit.Text(s, "PAQUETES", new Vector3(0, 3.35f, .3f), new Vector3(0, 180f, 0), .1f, new Color(.1f, .3f, .75f));
        // A ring on the floor marks where to stand.
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Zona de recogida", Stall + Vector3.up * .13f, new Vector3(StallRadius * 2f, .01f, StallRadius * 2f), EscapeKit.Glow(new Color(.3f, .7f, 1f), .25f), false);
    }

    void BuildHomes(System.Random rng)
    {
        Color[] walls = { new Color(.95f, .8f, .65f), new Color(.7f, .85f, .95f), new Color(.95f, .7f, .75f), new Color(.8f, .9f, .7f), new Color(.95f, .93f, .75f) };
        var roof = EscapeKit.Paint(new Color(.55f, .25f, .2f), .2f); var mail = EscapeKit.Paint(new Color(.2f, .45f, .85f), .4f);
        for (int i = 0; i < 6; i++)
        {
            float x = -12.5f + i * 5f;
            EscapeKit.Block(root, "Casa", new Vector3(x, 1.6f, 36f), new Vector3(4.2f, 3.2f, 3.4f), EscapeKit.Paint(walls[i % walls.Length], .1f), .08f, false);
            EscapeKit.Block(root, "Tejado", new Vector3(x, 3.5f, 36f), new Vector3(4.6f, .6f, 3.8f), roof, .1f, false);
            EscapeKit.Block(root, "Puerta", new Vector3(x, .8f, 34.28f), new Vector3(.9f, 1.6f, .06f), EscapeKit.Paint(new Color(.45f, .3f, .2f)), .02f, false);
            EscapeKit.Block(root, "Buzón", new Vector3(x + 1.5f, .9f, 33.6f), new Vector3(.4f, .45f, .55f), mail, .06f, false);
            EscapeKit.Block(root, "Poste buzón", new Vector3(x + 1.5f, .35f, 33.6f), new Vector3(.08f, .7f, .08f), roof, .01f, false);
        }
        EscapeKit.Text(root, "BARRIO · ENTREGA AQUÍ", new Vector3(0, 5f, 34f), new Vector3(0, 180f, 0), .07f, new Color(.15f, .3f, .6f));
    }

    /// <summary>Distance a lane's traffic has moved at time t (speeds rise 35 % over the round).</summary>
    public static float Travel(Lane l, float t) => l.Speed * (t + Ramp * Mathf.Max(0f, t) * Mathf.Max(0f, t) / (2f * RoundSeconds));
    public static float CarX(Lane l, Vehicle v, float t)
    {
        float x = v.Offset + l.Dir * Travel(l, t) + Loop * .5f;
        x = Mathf.Repeat(x, Loop);
        return x - Loop * .5f;
    }
    public static float LaneSpeed(Lane l, float t) => l.Speed * (1f + Ramp * Mathf.Max(0f, t) / RoundSeconds);

    public void Tick(float t)
    {
        foreach (var l in Lanes) foreach (var v in l.Cars) v.T.position = new Vector3(CarX(l, v, t), 0, l.Z);
    }

    /// <summary>Which lane (and vehicle) is hitting a point at time t, or -1.</summary>
    public int HitAt(Vector3 p, float t, float pad, out Vehicle hit)
    {
        hit = null;
        if (p.y > 1.6f) return -1;
        for (int i = 0; i < Lanes.Count; i++)
        {
            var l = Lanes[i]; if (Mathf.Abs(p.z - l.Z) > LaneHalf - .2f) continue;
            foreach (var v in l.Cars) if (Mathf.Abs(p.x - CarX(l, v, t)) < v.Length * .5f + pad) { hit = v; return i; }
        }
        return -1;
    }

    /// <summary>Seconds until a vehicle covers x in lane i (looking ahead up to <paramref name="horizon"/>), or horizon.</summary>
    public float ClearFor(int i, float x, float t, float horizon, float pad)
    {
        var l = Lanes[i];
        for (float dt = 0f; dt <= horizon; dt += .05f)
            foreach (var v in l.Cars) if (Mathf.Abs(x - CarX(l, v, t + dt)) < v.Length * .5f + pad) return dt;
        return horizon;
    }

    /// <summary>True if no vehicle of lane i covers x at any moment between t0 and t1.</summary>
    public bool ClearBetween(int i, float x, float t0, float t1, float pad)
    {
        var l = Lanes[i];
        for (float t = t0; t <= t1; t += .05f)
            foreach (var v in l.Cars) if (Mathf.Abs(x - CarX(l, v, t)) < v.Length * .5f + pad) return false;
        return true;
    }

    public static int RoadOf(float z) { for (int r = 0; r < Roads.Length; r++) if (z > Roads[r].a - .05f && z < Roads[r].b + .05f) return r; return -1; }

    public static int LaneOf(float z) { for (int i = 0; i < LaneZ.Length; i++) if (Mathf.Abs(z - LaneZ[i]) < LaneHalf) return i; return -1; }
}
