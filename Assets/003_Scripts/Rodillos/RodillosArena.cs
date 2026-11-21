using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rodillos del Calabozo (2026-11-17) · deterministic timeline shared by every computer: the angle of both spiked
/// rollers, when they reverse, and when each floor tile shakes and falls. Everything is a pure function of the
/// party seed and the time since "¡YA!", so online peers only need to agree on the clock.
/// Angles are Unity yaw degrees: an arm with angle a points along Quaternion.Euler(0, a, 0) * Vector3.right.
/// </summary>
public sealed class RodillosSpin
{
    public const int Low = 0, High = 1;
    readonly List<float> flips = new List<float>();
    float lowAngle, time;
    const float Step = 1f / 120f;

    public RodillosSpin(int seed)
    {
        var rng = new System.Random(seed);
        float t = RodillosDirector.ReverseFrom + (float)rng.NextDouble() * 5f;
        while (t < RodillosDirector.HardEnd + 30f) { flips.Add(t); t += 7f + (float)rng.NextDouble() * 9f; }
        Reset();
    }

    void Reset() { time = 0f; lowAngle = 0f; }

    /// <summary>Degrees per second (always positive) at time t. Both rollers share the hub, so they share the speed.</summary>
    public static float Speed(float t)
    {
        float w = Mathf.Min(130f, 52f + .5f * t);
        if (t > RodillosDirector.SuddenDeath) w += Mathf.Min(70f, (t - RodillosDirector.SuddenDeath) * 1.4f);
        return w;
    }
    /// <summary>
    /// Angle from the low roller to the high one: opposite sides of the hub (so the two never arrive together),
    /// closing in during sudden death so jump-then-duck has to be quick.
    /// </summary>
    public static float Offset(float t) => t <= RodillosDirector.SuddenDeath ? 180f : Mathf.Lerp(180f, 105f, Mathf.Clamp01((t - RodillosDirector.SuddenDeath) / 30f));
    public float Dir(float t)
    {
        int n = 0;
        for (int i = 0; i < flips.Count && flips[i] <= t; i++) n++;
        return n % 2 == 0 ? 1f : -1f;
    }
    /// <summary>A reversal is coming within <paramref name="window"/> seconds.</summary>
    public bool Warning(float t, float window = 1.3f)
    {
        foreach (var f in flips) if (f > t && f - t < window) return true;
        return false;
    }
    public bool FlippedBetween(float a, float b) { foreach (var f in flips) if (f > a && f <= b) return true; return false; }

    public void Advance(float to)
    {
        if (to < time - .001f) Reset();
        while (time < to)
        {
            float dt = Mathf.Min(Step, to - time);
            lowAngle = Mathf.Repeat(lowAngle + Dir(time) * Speed(time) * dt, 360f);
            time += dt;
        }
    }
    public float Angle(int arm) => arm == Low ? lowAngle : Mathf.Repeat(lowAngle + Offset(time), 360f);
    /// <summary>Signed yaw speed (deg/s) right now.</summary>
    public float YawSpeed(int arm) => Dir(time) * Speed(time);
    public Vector3 ArmDir(int arm) => Quaternion.Euler(0, Angle(arm), 0) * Vector3.right;
}

/// <summary>Builds the dungeon, the round platform of tiles over the lava, the hub and the two spiked rollers.</summary>
public sealed class RodillosArena : MonoBehaviour
{
    public sealed class Tile
    {
        public int Index, Ring; public float A0, A1, R0, R1;   // A in degrees, math convention: atan2(z, x)
        public float ShakeAt = 9999f, FallAt = 9999f;
        public Transform T; public Material Mat; public Color Base; public Vector3 Home;
        public bool Dropped; public float DropVel, Spin;
        public bool Fallen(float t) => t >= FallAt;
        public bool Shaking(float t) => t >= ShakeAt && t < FallAt;
        public bool Contains(float r, float a) => r >= R0 && r < R1 && Mathf.Repeat(a - A0, 360f) < Mathf.Repeat(A1 - A0, 360f);
        public Vector3 Center => Quaternion.Euler(0, -(A0 + Mathf.Repeat(A1 - A0, 360f) * .5f), 0) * Vector3.right * ((R0 + R1) * .5f);
    }

    public readonly List<Tile> Tiles = new();
    public Transform[] Pivots = new Transform[2];
    readonly Transform[] spinners = new Transform[2];
    readonly Material[] bandMats = new Material[2];
    readonly Color[] bandColors = { new Color(1f, .55f, .15f), new Color(.35f, .85f, 1f) };
    readonly AudioSource[] hums = new AudioSource[2];
    RodillosDirector director;
    Material lavaMat; Transform root;
    readonly List<Light> lavaLights = new();
    float nextBubble;

    public void Build(RodillosDirector d, int seed)
    {
        director = d;
        root = new GameObject("Calabozo").transform;
        var rng = new System.Random(seed * 13 + 5);

        // Lava lake under everything.
        lavaMat = new Material(EscapeKit.Glow(new Color(1f, .3f, .05f), .9f));
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Lago de lava", new Vector3(0, -1.7f, 0), new Vector3(40f, .05f, 40f), lavaMat, false);
        var crust = EscapeKit.Paint(new Color(.32f, .09f, .04f), .05f);
        for (int i = 0; i < 26; i++)
        {
            float a = (float)rng.NextDouble() * 360f, r = 12.5f + (float)rng.NextDouble() * 5.5f, s = .8f + (float)rng.NextDouble() * 1.6f;
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Costra", Quaternion.Euler(0, a, 0) * Vector3.right * r + Vector3.down * 1.66f, new Vector3(s * 1.6f, .1f, s), crust, false, new Vector3(0, a + 90f, 0));
        }
        for (int i = 0; i < 6; i++) lavaLights.Add(EscapeKit.PointLight(root, Quaternion.Euler(0, i * 60 + 30, 0) * Vector3.right * 13.5f + Vector3.down * .6f, new Color(1f, .45f, .15f), 14f, 2.4f));

        // Floor tiles: an inner ring of 8 and an outer ring of 12, each a thick stone wedge.
        var stoneA = new Color(.56f, .52f, .48f); var stoneB = new Color(.46f, .43f, .41f);
        AddRing(0, RodillosDirector.HubRadius - .15f, RodillosDirector.InnerSplit, 8, 0f, stoneA, stoneB);
        AddRing(1, RodillosDirector.InnerSplit, RodillosDirector.Radius, 12, 15f, stoneB, stoneA);
        // Crumble schedule: most of the outer ring, then part of the inner ring.
        var outer = Tiles.FindAll(t => t.Ring == 1); var inner = Tiles.FindAll(t => t.Ring == 0);
        Shuffle(outer, rng); Shuffle(inner, rng);
        for (int k = 0; k < 7; k++) { outer[k].ShakeAt = RodillosDirector.CrumbleFrom + k * 6f; outer[k].FallAt = outer[k].ShakeAt + 2.4f; }
        for (int k = 0; k < 4; k++) { inner[k].ShakeAt = RodillosDirector.CrumbleFrom + 46f + k * 7f; inner[k].FallAt = inner[k].ShakeAt + 2.4f; }
        for (int k = 7; k < 10; k++) { outer[k].ShakeAt = RodillosDirector.SuddenDeath + 8f + (k - 7) * 9f; outer[k].FallAt = outer[k].ShakeAt + 2.4f; }

        // Rock column under the platform.
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Pilar de roca", new Vector3(0, -2.2f, 0), new Vector3(9f, 1.4f, 9f), EscapeKit.Paint(new Color(.24f, .2f, .19f)), false);

        // Hub and the two arms.
        Prop("Eje_Central", root, Vector3.zero, 0, 1f);
        for (int arm = 0; arm < 2; arm++)
        {
            var pivot = new GameObject(arm == 0 ? "Brazo bajo (saltar)" : "Brazo alto (agacharse)").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0, arm == 0 ? RodillosDirector.LowHeight : -4f, 0);
            Pivots[arm] = pivot;
            var spin = new GameObject("Giro").transform; spin.SetParent(pivot, false);
            spin.localPosition = new Vector3(RodillosDirector.ArmStart, 0, 0);
            spinners[arm] = spin;
            var model = Prop(arm == 0 ? "Rodillo_Bajo" : "Rodillo_Alto", spin, Vector3.zero, 180f, 1f);
            if (model != null)
            {
                float thick = arm == 0 ? .76f : 1f;
                model.transform.localScale = new Vector3((RodillosDirector.ArmEnd - RodillosDirector.ArmStart) / 9.9f, thick, thick);
                // A unique band material so each roller can flash on its own.
                bandMats[arm] = new Material(EscapeKit.Glow(bandColors[arm], 2f));
                foreach (var r in model.GetComponentsInChildren<Renderer>()) if (r.name.Contains("_Brillo")) r.sharedMaterial = bandMats[arm];
            }
            // Connecting beam from the hub.
            EscapeKit.Block(pivot, "Viga", new Vector3(.65f, 0, 0), new Vector3(1.3f, .3f, .3f), EscapeKit.Paint(new Color(.3f, .32f, .36f), .4f), .05f, false);
            var sound = new GameObject("Sonido"); sound.transform.SetParent(pivot, false); sound.transform.localPosition = new Vector3(6f, 0, 0);
            var hum = sound.AddComponent<AudioSource>(); hum.clip = RodillosAudio.Clip("rumble"); hum.loop = true; hum.spatialBlend = .8f; hum.minDistance = 4f; hum.maxDistance = 26f; hum.volume = 0f;
            hums[arm] = hum;
            if (hum.clip != null) hum.Play();
        }

        Dungeon(rng);
        var lavaSound = root.gameObject.AddComponent<AudioSource>(); lavaSound.clip = RodillosAudio.Clip("lava_loop"); lavaSound.loop = true; lavaSound.spatialBlend = 0f; lavaSound.volume = .22f;
        if (lavaSound.clip != null) lavaSound.Play();
    }

    static void Shuffle<T>(List<T> list, System.Random rng) { for (int i = list.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); } }

    void AddRing(int ring, float r0, float r1, int count, float offset, Color a, Color b)
    {
        float span = 360f / count;
        for (int i = 0; i < count; i++)
        {
            var tile = new Tile { Index = Tiles.Count, Ring = ring, A0 = offset + i * span, A1 = offset + (i + 1) * span, R0 = r0, R1 = r1 };
            tile.A0 = Mathf.Repeat(tile.A0, 360f); tile.A1 = Mathf.Repeat(tile.A1, 360f);
            var go = new GameObject((ring == 0 ? "Losa interior " : "Losa exterior ") + i);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = Wedge(r0 + .04f, r1 - .04f, offset + i * span + .25f, offset + (i + 1) * span - .25f, 1.4f);
            tile.Base = i % 2 == 0 ? a : b;
            tile.Mat = new Material(EscapeKit.Paint(tile.Base, .08f));   // own copy: each tile glows red on its own
            go.AddComponent<MeshRenderer>().sharedMaterial = tile.Mat;
            tile.T = go.transform; tile.Home = go.transform.localPosition;
            Tiles.Add(tile);
            // A few studs and cracks so the stone reads as stone.
            var c = tile.Center;
            EscapeKit.Shape(PrimitiveType.Cylinder, go.transform, "Remache", c + Vector3.up * .02f, new Vector3(.35f, .02f, .35f), EscapeKit.Paint(tile.Base * .75f), false);
        }
    }

    /// <summary>A thick annular wedge (math angles in degrees), top at y = 0.</summary>
    static Mesh Wedge(float r0, float r1, float a0, float a1, float depth)
    {
        int seg = Mathf.Max(3, Mathf.CeilToInt((a1 - a0) / 5f));
        var v = new List<Vector3>(); var tri = new List<int>();
        Vector3 P(float r, float a, float y) { float rad = a * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(rad) * r, y, Mathf.Sin(rad) * r); }
        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) { int s = v.Count; v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3); tri.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 }); }
        for (int i = 0; i < seg; i++)
        {
            float x0 = Mathf.Lerp(a0, a1, i / (float)seg), x1 = Mathf.Lerp(a0, a1, (i + 1) / (float)seg);
            Quad(P(r0, x0, 0), P(r0, x1, 0), P(r1, x1, 0), P(r1, x0, 0));                  // top
            Quad(P(r0, x1, -depth), P(r0, x0, -depth), P(r1, x0, -depth), P(r1, x1, -depth)); // bottom
            Quad(P(r1, x0, 0), P(r1, x1, 0), P(r1, x1, -depth), P(r1, x0, -depth));           // outer side
            Quad(P(r0, x1, 0), P(r0, x0, 0), P(r0, x0, -depth), P(r0, x1, -depth));           // inner side
        }
        Quad(P(r0, a0, -depth), P(r0, a0, 0), P(r1, a0, 0), P(r1, a0, -depth));
        Quad(P(r1, a1, -depth), P(r1, a1, 0), P(r0, a1, 0), P(r0, a1, -depth));
        // Fix winding so faces point outwards (top must face +Y).
        var m = new Mesh { name = "Losa" };
        m.SetVertices(v); m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        if (m.normals[0].y < 0) { for (int i = 0; i < tri.Count; i += 3) (tri[i + 1], tri[i + 2]) = (tri[i + 2], tri[i + 1]); m.SetTriangles(tri, 0); m.RecalculateNormals(); }
        m.RecalculateBounds();
        return m;
    }

    void Dungeon(System.Random rng)
    {
        var floor = EscapeKit.Paint(new Color(.33f, .3f, .29f), .05f);
        var brick = EscapeKit.Paint(new Color(.42f, .28f, .22f), .05f);
        var brickDark = EscapeKit.Paint(new Color(.3f, .2f, .17f), .05f);
        // Outer ledge ring and the wall, 24 segments.
        for (int i = 0; i < 24; i++)
        {
            float a = i * 15f; var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            EscapeKit.Block(root, "Repisa", dir * 23f + Vector3.up * .1f, new Vector3(7.8f, 1.6f, 11f), floor, .1f, false, new Vector3(0, a, 0)).transform.position += Vector3.down * .7f;
            EscapeKit.Block(root, "Muro", dir * 28.8f + Vector3.up * 5f, new Vector3(7.8f, 11f, 1.4f), i % 2 == 0 ? brick : brickDark, .1f, false, new Vector3(0, a, 0));
            EscapeKit.Block(root, "Arco", dir * 28.2f + Vector3.up * 9.6f, new Vector3(7.8f, .8f, 1.6f), brickDark, .1f, false, new Vector3(0, a, 0));
        }
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f + 7.5f; var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            Prop("Columna", root, dir * 27f + Vector3.down * .1f, a + 180f, 1.15f);
            if (i % 3 == 0) Prop("Reja", root, Quaternion.Euler(0, a + 15f, 0) * Vector3.forward * 27.9f + Vector3.down * .1f, a + 15f + 180f, 1.3f);
            else
            {
                Prop("Brasero", root, Quaternion.Euler(0, a + 15f, 0) * Vector3.forward * 20.5f + Vector3.down * .1f, 0, 1f);
                EscapeKit.PointLight(root, Quaternion.Euler(0, a + 15f, 0) * Vector3.forward * 20.5f + Vector3.up * 2.2f, new Color(1f, .6f, .25f), 9f, 2.2f);
            }
            if (i % 2 == 1) Prop(rng.Next(2) == 0 ? "Craneos" : "Barril", root, Quaternion.Euler(0, a + 8f, 0) * Vector3.forward * 25.5f + Vector3.down * .1f, (float)rng.NextDouble() * 360f, 1f);
            if (i % 4 == 2) Prop("Columna_Rota", root, Quaternion.Euler(0, a - 6f, 0) * Vector3.forward * 22f + Vector3.down * .1f, (float)rng.NextDouble() * 360f, 1f);
        }
        for (int i = 0; i < 12; i++) Prop("Cadenas", root, Quaternion.Euler(0, i * 30f + 22f, 0) * Vector3.forward * 27.4f + Vector3.up * 5.2f, i * 30f, 1.3f);
    }

    static readonly Dictionary<string, GameObject> prefabs = new();
    public static GameObject Prop(string key, Transform parent, Vector3 pos, float yaw, float scale)
    {
        if (!prefabs.TryGetValue(key, out var p) || p == null) prefabs[key] = p = Resources.Load<GameObject>("RodillosArt/" + key);
        if (p == null) { Debug.LogWarning("Rodillos: falta " + key + " (Multiverse → Zonas → Preparar prefabs)"); return null; }
        var go = Instantiate(p, parent, false); go.name = key;
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        return go;
    }

    // ------------------------------------------------------------------ queries

    public static float AngleOf(Vector3 p) => Mathf.Repeat(Mathf.Atan2(p.z, p.x) * Mathf.Rad2Deg, 360f);

    public Tile TileAt(Vector3 p)
    {
        float r = new Vector2(p.x, p.z).magnitude, a = AngleOf(p);
        foreach (var t in Tiles) if (t.Contains(r, a)) return t;
        return null;
    }

    /// <summary>Solid floor under this point at time t (the hub counts as solid).</summary>
    public bool Ground(Vector3 p, float t)
    {
        float r = new Vector2(p.x, p.z).magnitude;
        if (r > RodillosDirector.Radius) return false;
        if (r < RodillosDirector.HubRadius) return true;
        var tile = TileAt(p);
        return tile != null && !tile.Fallen(t);
    }

    // ------------------------------------------------------------------ per frame

    public void Tick(float t, RodillosSpin spin, bool running)
    {
        for (int arm = 0; arm < 2; arm++)
        {
            var pivot = Pivots[arm];
            pivot.localRotation = Quaternion.Euler(0, spin.Angle(arm), 0);
            // Roll in the direction of travel.
            spinners[arm].Rotate(Vector3.right, spin.YawSpeed(arm) * 2.4f * Time.deltaTime, Space.Self);
            if (arm == RodillosSpin.High)
            {
                float k = Mathf.Clamp01((t - (RodillosDirector.HighArrives - 2.5f)) / 2.5f);
                float y = Mathf.Lerp(-4f, RodillosDirector.HighHeight, 1f - (1f - k) * (1f - k));
                pivot.localPosition = new Vector3(0, y, 0);
            }
            if (bandMats[arm] != null)
            {
                bool warn = running && spin.Warning(t);
                var c = warn && Mathf.Repeat(Time.time * 6f, 1f) < .5f ? new Color(1f, .15f, .1f) : bandColors[arm];
                EscapeKit.SetGlow(bandMats[arm], c, warn ? 3.2f : 2f);
            }
            if (hums[arm] != null) hums[arm].volume = Mathf.Lerp(hums[arm].volume, running && (arm == 0 || t > RodillosDirector.HighArrives - 2f) ? .55f : 0f, Time.deltaTime * 2f);
        }
        foreach (var tile in Tiles)
        {
            if (tile.Dropped)
            {
                if (!tile.T.gameObject.activeSelf) continue;
                tile.DropVel += 16f * Time.deltaTime;
                tile.T.localPosition += Vector3.down * tile.DropVel * Time.deltaTime;
                tile.T.RotateAround(tile.Center + tile.T.localPosition, Vector3.Cross(Vector3.up, tile.Center.normalized), tile.Spin * Time.deltaTime);
                if (tile.T.localPosition.y < -9f) tile.T.gameObject.SetActive(false);
                continue;
            }
            if (tile.Fallen(t))
            {
                tile.Dropped = true; tile.Spin = 25f + Random.value * 30f;
                tile.T.localPosition = tile.Home;
                RodillosAudio.Play("crumble", tile.Center, .9f);
                SurvivalKit.Puff(tile.Center + Vector3.up * .2f, new Color(.5f, .45f, .4f), 9, 1.2f);
                continue;
            }
            if (tile.Shaking(t))
            {
                float k = (t - tile.ShakeAt) / (tile.FallAt - tile.ShakeAt);
                tile.T.localPosition = tile.Home + new Vector3(Mathf.Sin(Time.time * 47f) * .05f, Mathf.Sin(Time.time * 31f) * .03f - k * .12f, Mathf.Cos(Time.time * 53f) * .05f);
                tile.Mat.color = Color.Lerp(tile.Base, new Color(.95f, .3f, .15f), .35f + .35f * Mathf.Repeat(Time.time * 4f, 1f) * k);
                if (!tileWarned.Contains(tile.Index)) { tileWarned.Add(tile.Index); RodillosAudio.Play("shake", tile.Center, .8f); }
            }
            else if (tile.T.localPosition != tile.Home) { tile.T.localPosition = tile.Home; tile.Mat.color = tile.Base; }
        }
        // Lava breathes and bubbles.
        float pulse = .9f + Mathf.Sin(Time.time * 1.3f) * .18f + Mathf.Sin(Time.time * 3.7f) * .07f;
        EscapeKit.SetGlow(lavaMat, new Color(1f, .3f, .05f), pulse);
        for (int i = 0; i < lavaLights.Count; i++) lavaLights[i].intensity = 2.1f + Mathf.Sin(Time.time * (1.7f + i * .3f) + i) * .4f;
        if (Time.time > nextBubble)
        {
            nextBubble = Time.time + Random.Range(.15f, .5f);
            float a = Random.value * 360f, r = Random.Range(11.6f, 18f);
            var at = Quaternion.Euler(0, a, 0) * Vector3.right * r + Vector3.down * 1.6f;
            SurvivalKit.Puff(at, new Color(1f, .5f, .12f), 4, .5f);
        }
    }
    readonly HashSet<int> tileWarned = new();
}
