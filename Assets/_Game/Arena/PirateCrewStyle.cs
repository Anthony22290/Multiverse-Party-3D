using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// "Tripulantes blanditos": an original soft-toy look for the Arena crew, built from smooth procedural
/// shapes attached to the existing physical skeleton (same bones, animations, physics and networking).
/// Chubby bean torso, big round head with expressive eyes, sausage limbs, mitten hands, chunky boots,
/// and a pirate outfit that differs per crew member. Toggle with PirateArenaSettings.softCrew.
/// </summary>
public sealed class PirateCrewStyle : MonoBehaviour
{
    struct Look
    {
        public Color skin, shirt, pants, boots, accent;
        public int hat;      // 0 tricorn, 1 bandana, 2 captain, 3 none (bald + earring), 4 headscarf with knot, 5 tall cook hat
        public bool beard, eyepatch, stripes;
    }

    static readonly Look[] Looks =
    {
        new Look { skin = new Color(1f, .82f, .66f), shirt = new Color(.95f, .32f, .38f), pants = new Color(.2f, .26f, .45f), boots = new Color(.32f, .2f, .13f), accent = new Color(1f, .8f, .25f), hat = 2, beard = false, eyepatch = false, stripes = false },
        new Look { skin = new Color(.78f, .55f, .4f), shirt = new Color(.25f, .7f, .95f), pants = new Color(.9f, .85f, .7f), boots = new Color(.2f, .14f, .1f), accent = new Color(.95f, .25f, .3f), hat = 1, beard = true, eyepatch = false, stripes = true },
        new Look { skin = new Color(.98f, .76f, .6f), shirt = new Color(.45f, .82f, .35f), pants = new Color(.35f, .22f, .14f), boots = new Color(.12f, .12f, .14f), accent = new Color(.15f, .15f, .18f), hat = 0, beard = false, eyepatch = true, stripes = false },
        new Look { skin = new Color(.55f, .37f, .26f), shirt = new Color(1f, .78f, .25f), pants = new Color(.55f, .3f, .7f), boots = new Color(.3f, .18f, .1f), accent = new Color(1f, .9f, .4f), hat = 3, beard = true, eyepatch = false, stripes = false },
        new Look { skin = new Color(1f, .86f, .72f), shirt = new Color(.7f, .45f, .9f), pants = new Color(.2f, .55f, .55f), boots = new Color(.22f, .15f, .12f), accent = new Color(1f, .45f, .65f), hat = 4, beard = false, eyepatch = false, stripes = true },
        new Look { skin = new Color(.86f, .64f, .48f), shirt = new Color(.95f, .95f, .92f), pants = new Color(.85f, .35f, .2f), boots = new Color(.18f, .12f, .1f), accent = new Color(.3f, .3f, .35f), hat = 5, beard = true, eyepatch = false, stripes = false },
    };

    static readonly Dictionary<string, Mesh> meshCache = new();
    readonly Dictionary<Color, Material> mats = new();

    public static int IndexFor(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return 0;
        int n = 0; foreach (char ch in displayName) if (char.IsDigit(ch)) n = n * 10 + (ch - '0');
        return n;
    }

    public static bool Apply(PirateFighter fighter, GameObject visual, int index)
    {
        if (visual == null) return false;
        return fighter != null && Apply(fighter, fighter.transform, visual, index);
    }

    /// <summary>Generic entry point (used by other modes, e.g. the escape room): any host, any facing transform.</summary>
    public static bool Apply(Component host, Transform facing, GameObject visual, int index)
    {
        if (host == null || visual == null) return false;
        var style = host.gameObject.AddComponent<PirateCrewStyle>();
        return style.Build(visual.transform, facing != null ? facing : host.transform, ((index % Looks.Length) + Looks.Length) % Looks.Length);
    }

    public static int LookCount => Looks.Length;

    Transform Bone(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    bool Build(Transform root, Transform facing, int index)
    {
        var torso = Bone(root, "Torso"); var head = Bone(root, "Head");
        if (torso == null || head == null) return false;
        var look = Looks[index];
        // Local axes of each bone: length along +Y (as the physics colliders assume); face direction from the build pose.
        Vector3 Fwd(Transform b) => b.InverseTransformDirection(facing.forward).normalized;
        Vector3 Up(Transform b) => b.InverseTransformDirection(Vector3.up).normalized;

        Material skin = Mat(look.skin, .35f), shirt = Mat(look.shirt, .25f), pants = Mat(look.pants, .2f), boots = Mat(look.boots, .3f), accent = Mat(look.accent, .5f);
        Material white = Mat(new Color(.98f, .98f, .98f), .7f), ink = Mat(new Color(.08f, .07f, .1f), .6f), blush = Mat(new Color(1f, .55f, .55f), .2f);

        // --- torso: soft bean, wider at the hips; belt and buckle; optional stripes
        {
            Vector3 f = Fwd(torso);
            Part(torso, "Torso blandito", Ellipsoid(.25f, .3f, .22f, 18, 12), shirt, Vector3.up * .2f, f);
            if (look.stripes)
                for (int i = 0; i < 3; i++) Part(torso, "Raya", Torus(.235f - i * .01f, .022f, 20, 6), Mat(Color.Lerp(look.shirt, Color.white, .75f), .25f), Vector3.up * (.12f + i * .1f), f, Vector3.one, Quaternion.identity);
            Part(torso, "Cinturón", Torus(.235f, .035f, 22, 6), Mat(new Color(.22f, .14f, .08f), .3f), Vector3.up * .04f, f);
            Part(torso, "Hebilla", Ellipsoid(.05f, .045f, .02f, 10, 6), accent, Vector3.up * .04f + f * .245f, f);
            Part(torso, "Cadera", Ellipsoid(.235f, .12f, .21f, 18, 8), pants, Vector3.up * -.03f, f);
        }
        // --- head: big round toy head with eyes, brows, nose, blush and outfit
        {
            Vector3 f = Fwd(head), u = Up(head), s = Vector3.Cross(u, f).normalized;
            Vector3 c = u * .17f;
            float r = .23f;
            Part(head, "Cabeza blandita", Ellipsoid(r, r * .95f, r * .96f, 20, 14), skin, c, f);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 eye = c + f * r * .82f + s * side * .085f + u * .03f;
                bool patch = look.eyepatch && side > 0;
                if (!patch)
                {
                    Part(head, "Ojo", Ellipsoid(.055f, .07f, .03f, 12, 8), white, eye, f);
                    Part(head, "Pupila", Ellipsoid(.03f, .04f, .02f, 10, 6), ink, eye + f * .022f + u * -.005f, f);
                    Part(head, "Brillo", Ellipsoid(.011f, .011f, .008f, 6, 4), white, eye + f * .037f + u * .012f + s * .01f, f);
                }
                else
                {
                    Part(head, "Parche", Ellipsoid(.06f, .055f, .02f, 12, 6), ink, eye + f * .01f, f);
                    Part(head, "Cinta del parche", Torus(r * 1.0f, .008f, 24, 4), ink, c + u * .05f, f, Vector3.one, Quaternion.AngleAxis(-18f, s));
                }
                Part(head, "Ceja", Capsule(.012f, .07f, 6), ink, eye + u * .085f + f * .012f, f, Vector3.one, Quaternion.AngleAxis(90f + side * 12f, f));
                Part(head, "Mejilla", Ellipsoid(.035f, .022f, .01f, 8, 4), blush, c + f * r * .78f + s * side * .14f - u * .055f, f);
            }
            Part(head, "Nariz", Ellipsoid(.04f, .035f, .04f, 10, 6), Mat(Color.Lerp(look.skin, new Color(1f, .55f, .5f), .35f), .35f), c + f * r * .98f - u * .02f, f);
            Part(head, "Sonrisa", Ellipsoid(.05f, .017f, .014f, 12, 6), ink, c + f * r * .93f - u * .1f, f);
            if (look.beard) Part(head, "Barba", Ellipsoid(r * .82f, r * .5f, r * .5f, 16, 10), Mat(new Color(.35f, .2f, .12f), .1f), c - u * .12f + f * .1f, f);
            Hat(head, look, c, r, f, u, s, accent);
        }
        // --- limbs: sausage segments, mitten hands, chunky boots
        foreach (string side in new[] { "L", "R" })
        {
            var ua = Bone(root, "UpperArm." + side); var la = Bone(root, "LowerArm." + side);
            var ul = Bone(root, "UpperLeg." + side); var ll = Bone(root, "LowerLeg." + side);
            if (ua != null) Part(ua, "Brazo", Capsule(.075f, .3f, 12), shirt, Vector3.up * .15f, Fwd(ua));
            if (la != null)
            {
                Part(la, "Antebrazo", Capsule(.068f, .26f, 12), skin, Vector3.up * .13f, Fwd(la));
                Part(la, "Puño de la manga", Torus(.075f, .022f, 14, 5), shirt, Vector3.up * .03f, Fwd(la), Vector3.one, Quaternion.identity);
                Part(la, "Manopla", Ellipsoid(.09f, .1f, .08f, 14, 10), skin, Vector3.up * .3f, Fwd(la));
            }
            if (ul != null) Part(ul, "Muslo", Capsule(.1f, .34f, 12), pants, Vector3.up * .17f, Fwd(ul));
            if (ll != null)
            {
                Part(ll, "Pierna", Capsule(.09f, .3f, 12), pants, Vector3.up * .14f, Fwd(ll));
                Vector3 f = Fwd(ll);
                Part(ll, "Bota", Ellipsoid(.11f, .085f, .16f, 14, 8), boots, Vector3.up * .36f + f * .04f, f);
                Part(ll, "Caña de la bota", Capsule(.1f, .12f, 12), boots, Vector3.up * .28f, f);
            }
        }
        // Hide the original skinned look on this physical puppet (reversible: disable this component's parts).
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            if (r.GetComponent<PirateCrewPart>() == null) r.enabled = false;
        return true;
    }

    void Hat(Transform head, Look look, Vector3 c, float r, Vector3 f, Vector3 u, Vector3 s, Material accent)
    {
        Material black = Mat(new Color(.12f, .1f, .12f), .35f), red = Mat(new Color(.9f, .2f, .25f), .25f), white = Mat(new Color(.97f, .97f, .95f), .3f);
        switch (look.hat)
        {
            case 0: // tricorn: brim + crown + three upturned corners
                Part(head, "Tricornio ala", Ellipsoid(r * 1.25f, .035f, r * 1.15f, 20, 6), black, c + u * r * .78f, f);
                Part(head, "Tricornio copa", Ellipsoid(r * .82f, r * .42f, r * .8f, 18, 10), black, c + u * r * .95f, f);
                for (int i = 0; i < 3; i++)
                {
                    Quaternion q = Quaternion.AngleAxis(i * 120f, u);
                    Part(head, "Tricornio pico", Ellipsoid(r * .38f, r * .22f, .03f, 10, 6), black, c + u * r * .92f + q * (f * r * 1.05f), q * f);
                }
                Part(head, "Calavera de sombrero", Ellipsoid(.035f, .035f, .012f, 8, 6), white, c + u * r * 1.08f + f * r * .82f, f);
                break;
            case 1: // bandana with knot tails
                Part(head, "Pañuelo", Ellipsoid(r * 1.03f, r * .62f, r * 1.02f, 20, 10), red, c + u * r * .38f, f);
                for (int i = 0; i < 6; i++) Part(head, "Lunar", Ellipsoid(.022f, .022f, .008f, 6, 4), white, c + u * r * (.55f + (i % 2) * .15f) + Quaternion.AngleAxis(i * 50f - 125f, u) * f * r * 1.0f, Quaternion.AngleAxis(i * 50f - 125f, u) * f);
                Part(head, "Nudo", Ellipsoid(.05f, .04f, .04f, 8, 6), red, c + u * r * .35f - f * r * 1.0f, f);
                Part(head, "Cola del nudo", Ellipsoid(.03f, .1f, .015f, 8, 6), red, c + u * r * .1f - f * r * 1.05f + s * .04f, f, Vector3.one, Quaternion.AngleAxis(20f, f));
                break;
            case 2: // captain: tall bicorne with gold trim and feather
                Part(head, "Bicornio", Ellipsoid(r * 1.35f, r * .55f, r * .55f, 20, 10), Mat(new Color(.13f, .15f, .3f), .35f), c + u * r * 1.0f, f, Vector3.one, Quaternion.AngleAxis(90f, u));
                Part(head, "Ribete dorado", Torus(r * .9f, .02f, 22, 5), accent, c + u * r * .78f, f);
                Part(head, "Pluma", Ellipsoid(.04f, .2f, .02f, 8, 8), Mat(new Color(1f, .4f, .55f), .2f), c + u * r * 1.45f - f * .05f + s * .1f, f, Vector3.one, Quaternion.AngleAxis(-25f, f));
                Part(head, "Insignia", Ellipsoid(.04f, .04f, .015f, 10, 6), accent, c + u * r * 1.05f + f * r * .55f, f);
                break;
            case 3: // bald, gold earring and stubble ring
                Part(head, "Aro", Torus(.04f, .01f, 14, 5), accent, c - u * .04f + s * r * 1.0f, f, Vector3.one, Quaternion.AngleAxis(90f, f));
                break;
            case 4: // headscarf with big side knot
                Part(head, "Pañuelo", Ellipsoid(r * 1.04f, r * .7f, r * 1.03f, 20, 10), Mat(new Color(.25f, .75f, .9f), .25f), c + u * r * .32f, f);
                Part(head, "Lazo", Ellipsoid(.08f, .06f, .05f, 10, 6), Mat(new Color(.25f, .75f, .9f), .25f), c + u * r * .55f + s * r * .95f, f);
                Part(head, "Lazo 2", Ellipsoid(.06f, .05f, .04f, 10, 6), Mat(new Color(.25f, .75f, .9f), .25f), c + u * r * .3f + s * r * 1.05f - f * .04f, f);
                break;
            case 5: // tall cook hat (puffy)
                Part(head, "Gorro banda", Capsule(r * .92f, .08f, 18), white, c + u * r * .7f, f);
                Part(head, "Gorro", Ellipsoid(r * 1.05f, r * .75f, r * 1.0f, 18, 12), white, c + u * r * 1.35f, f);
                break;
        }
    }

    // ----------------------------------------------------------------- parts & materials
    void Part(Transform bone, string label, Mesh mesh, Material mat, Vector3 localPos, Vector3 localForward) =>
        Part(bone, label, mesh, mat, localPos, localForward, Vector3.one, null);

    void Part(Transform bone, string label, Mesh mesh, Material mat, Vector3 localPos, Vector3 localForward, Vector3 scale, Quaternion? extra)
    {
        var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer), typeof(PirateCrewPart));
        go.transform.SetParent(bone, false);
        go.transform.localPosition = localPos;
        // Orient: shape's +Z toward the face direction, +Y along the bone.
        Vector3 up = Vector3.up, fwd = Vector3.ProjectOnPlane(localForward, up);
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        // An explicit identity means "ring around the bone axis" (belts, cuffs); otherwise face the front, then apply the extra twist.
        go.transform.localRotation = extra.HasValue && extra.Value == Quaternion.identity ? Quaternion.identity
            : (extra ?? Quaternion.identity) * Quaternion.LookRotation(fwd.normalized, up);
        // The smile is a half ring in the face plane: ring axis along the face direction, arc bowing downward.
        if (label == "Sonrisa" && extra.HasValue) go.transform.localRotation = extra.Value;
        // Compensate any bone scale so sizes stay in meters.
        Vector3 ls = bone.lossyScale;
        go.transform.localScale = new Vector3(scale.x / Mathf.Max(ls.x, 1e-4f), scale.y / Mathf.Max(ls.y, 1e-4f), scale.z / Mathf.Max(ls.z, 1e-4f));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
        go.GetComponent<PirateCrewPart>().baseColor = mat.GetColor("_BaseColor");
        r.shadowCastingMode = mesh.vertexCount > 120 ? ShadowCastingMode.On : ShadowCastingMode.Off;
    }

    Material Mat(Color c, float smooth)
    {
        if (mats.TryGetValue(c, out var m)) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Tripulante " + ColorUtility.ToHtmlStringRGB(c) };
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
        m.enableInstancing = true;
        mats[c] = m; return m;
    }

    // ----------------------------------------------------------------- smooth procedural shapes (cached)
    static Mesh Ellipsoid(float rx, float ry, float rz, int seg, int rings)
    {
        string key = $"E{rx:0.###}/{ry:0.###}/{rz:0.###}/{seg}/{rings}";
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float phi = Mathf.PI * i / rings;
            for (int j = 0; j <= seg; j++)
            {
                float th = 2 * Mathf.PI * j / seg;
                Vector3 unit = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                v.Add(Vector3.Scale(unit, new Vector3(rx, ry, rz)));
                n.Add(new Vector3(unit.x / rx, unit.y / ry, unit.z / rz).normalized);
            }
        }
        for (int i = 0; i < rings; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = i * (seg + 1) + j, b = a + seg + 1;
                t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        return meshCache[key] = Finish(key, v, n, t);
    }

    static Mesh Capsule(float radius, float length, int seg)
    {
        string key = $"C{radius:0.###}/{length:0.###}/{seg}";
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        int rings = 8; float half = Mathf.Max(0f, length * .5f - radius);
        for (int i = 0; i <= rings * 2 + 1; i++)
        {
            bool top = i <= rings;
            float phi = top ? Mathf.PI * .5f * i / rings : Mathf.PI * .5f + Mathf.PI * .5f * (i - rings - 1) / rings;
            float y = Mathf.Cos(phi) * radius + (top ? half : -half);
            float ringR = Mathf.Sin(phi) * radius;
            for (int j = 0; j <= seg; j++)
            {
                float th = 2 * Mathf.PI * j / seg;
                v.Add(new Vector3(Mathf.Cos(th) * ringR, y, Mathf.Sin(th) * ringR));
                n.Add(new Vector3(Mathf.Cos(th) * Mathf.Sin(phi), Mathf.Cos(phi), Mathf.Sin(th) * Mathf.Sin(phi)).normalized);
            }
        }
        int total = rings * 2 + 1;
        for (int i = 0; i < total; i++)
            for (int j = 0; j < seg; j++)
            {
                int a = i * (seg + 1) + j, b = a + seg + 1;
                t.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        return meshCache[key] = Finish(key, v, n, t);
    }

    static Mesh Torus(float radius, float tube, int seg, int sides, float arc = 1f)
    {
        string key = $"T{radius:0.###}/{tube:0.###}/{seg}/{sides}/{arc:0.##}";
        if (meshCache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = 2 * Mathf.PI * arc * i / seg - (arc < 1f ? Mathf.PI * arc * .5f + Mathf.PI * .5f : 0f);
            Vector3 center = new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius);
            for (int j = 0; j <= sides; j++)
            {
                float b = 2 * Mathf.PI * j / sides;
                Vector3 normal = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                v.Add(center + normal * tube); n.Add(normal);
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * (sides + 1) + j, b = a + sides + 1;
                t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        return meshCache[key] = Finish(key, v, n, t);
    }

    static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<int> t)
    {
        var m = new Mesh { name = "Tripulante " + name };
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }
}

/// <summary>Marks a procedural crew part (so the style can hide only the original renderers).</summary>
public sealed class PirateCrewPart : MonoBehaviour { public Color baseColor = Color.white; }
