using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Purely visual pirate dressing for Arena del Caos, added when the map builds at Play.
/// Everything stays outside the deck and has no colliders, so physics, bots, cannons, the shark,
/// the camera and online sync are unaffected. Same pirate palette and models as the ship.
/// </summary>
public static class PirateArenaArtDressing
{
    static MultiverseArtSet set;
    static Transform parent;

    public static void Dress(Transform root, float shipWidth, float shipLength)
    {
        set = Resources.Load<MultiverseArtSet>("ArenaArtSet");
        if (set == null) { Debug.LogWarning("ArenaArtSet no encontrado: Arena sin decorado extra"); return; }
        parent = new GameObject("Decorado pirata · archipiélago (visual)").transform;
        parent.SetParent(root, false);

        // Island 1 (west): fishing village — hut, pier pointing at the ship, torches, flag.
        Island(new Vector3(-44f, -.4f, 24f), 1f, (center, s) =>
        {
            Put("hut", center, new Vector2(-5f, -2.4f), 60f, s);
            Put("torch", center, new Vector2(-2.2f, -3.6f), 0, s); Put("torch", center, new Vector2(-6.6f, -.4f), 0, s);
            Put("flag", center, new Vector2(2.6f, -3.2f), 20, s);
            Pier(center, s, 8.5f);
            Put("skulls", center, new Vector2(.4f, -4.4f), 40, s);
        });
        // Island 2 (north-east, biggest): lighthouse, two houses, treasure, pier.
        Island(new Vector3(48f, -.4f, 48f), 1.3f, (center, s) =>
        {
            Put("lighthouse", center, new Vector2(4.8f, 2.8f), 0, s);
            Put("house1", center, new Vector2(-5.6f, -2.6f), 200, s);
            Put("house3", center, new Vector2(-2f, -3.9f), 170, s);
            Put("treasure", center, new Vector2(1.6f, -3.4f), 220, s);
            Put("torch", center, new Vector2(-.2f, -4.6f), 0, s); Put("flag", center, new Vector2(3.2f, -4f), -20, s);
            Pier(center, s, 10f);
        });
        // Island 3 (south-west): lookout tower and a cliff.
        Island(new Vector3(-55f, -.4f, -43f), .8f, (center, s) =>
        {
            Put("tower", center, new Vector2(1.6f, -2.8f), 15, s);
            Put("cliff", center, new Vector2(-1.5f, 3.6f), 60, s);
            Put("torch", center, new Vector2(-2f, -4f), 0, s); Put("torch", center, new Vector2(4.4f, -3.4f), 0, s);
        });

        // Two small ships on the horizon and a ring of buoys well outside the deck and the shark's path.
        Water("shipSmall", new Vector3(-82f, 0f, -8f), 70f);
        Water("shipSmall", new Vector3(76f, 0f, -36f), -120f);
        Water("rowboat", new Vector3(-37f, .1f, 15f), 25f);
        Water("rowboat", new Vector3(40f, .1f, 35f), -150f);
        float bx = shipWidth * .5f + 10f, bz = shipLength * .5f + 6f;
        foreach (var p in new[] { new Vector2(-bx, -bz * .5f), new Vector2(bx, bz * .4f), new Vector2(-bx * .8f, bz), new Vector2(bx * .9f, -bz), new Vector2(0, bz + 8), new Vector2(0, -bz - 8) })
            Water("buoy", new Vector3(p.x, .05f, p.y), p.x * 7f).AddComponent<PirateArenaBob>();
    }

    delegate void Dresser(Vector3 center, float scale);

    // The islands are flattened spheres (17 x 3 x 13 per unit scale); put props on their curved top.
    static void Island(Vector3 center, float s, Dresser dress) => dress(center, s);

    static float TopY(Vector3 center, float s, Vector2 local)
    {
        float ax = local.x / 8.5f, az = local.y / 6.5f;
        float k = Mathf.Clamp01(1f - ax * ax - az * az);
        return center.y + 1.5f * s * Mathf.Sqrt(k);
    }

    static GameObject Put(string key, Vector3 center, Vector2 local, float yaw, float s)
    {
        local *= s;
        var p = new Vector3(center.x + local.x, TopY(center, s, local) - .05f, center.z + local.y);
        return Spawn(key, p, yaw, s);
    }

    static void Pier(Vector3 center, float s, float reach)
    {
        // Points toward the ship (origin) from the island shore.
        Vector3 dir = (-center).normalized; dir.y = 0;
        var p = center + dir * reach * s; p.y = .35f;
        Spawn("pier", p, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 1f);
        Spawn("dockPole", p + Vector3.Cross(Vector3.up, dir) * 1.8f + dir * 3.4f + Vector3.down * .4f, 0, 1f);
    }

    static GameObject Water(string key, Vector3 p, float yaw) => Spawn(key, p, yaw, 1f);

    static GameObject Spawn(string key, Vector3 p, float yaw, float s)
    {
        if (!set.TryGet(key, out var e)) return new GameObject("(falta " + key + ")");
        var holder = new GameObject(key);
        holder.transform.SetParent(parent, false);
        holder.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
        GameObject visual;
        if (e.atlas) visual = PirateArenaVisuals.AddModel(e.prefab, holder.transform, e.height * s, set.atlas, false);
        else { visual = Object.Instantiate(e.prefab, holder.transform, false); visual.transform.localScale = Vector3.one * s; }
        foreach (var c in holder.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        foreach (var r in holder.GetComponentsInChildren<Renderer>()) if (r.bounds.size.magnitude < 2f) r.shadowCastingMode = ShadowCastingMode.Off;
        return holder;
    }
}

/// <summary>Gentle bobbing for floating props (visual only).</summary>
public sealed class PirateArenaBob : MonoBehaviour
{
    Vector3 basePos; float seed;
    void Start() { basePos = transform.position; seed = basePos.x * .13f + basePos.z * .07f; }
    void Update()
    {
        float t = Time.time + seed;
        transform.position = basePos + Vector3.up * Mathf.Sin(t * 1.3f) * .12f;
        transform.rotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 4f, transform.eulerAngles.y, Mathf.Cos(t * .9f) * 4f);
    }
}
