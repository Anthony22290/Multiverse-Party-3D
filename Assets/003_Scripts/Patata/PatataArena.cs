using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arena of Patata Caliente: a round fairground floor with coloured rings, a candy-striped fence that closes in a
/// little after every explosion, three crates to run around and three trampolines. Fair stalls, lamps and bunting
/// outside. Everything is built from primitives at play time (same on every computer).
/// </summary>
public sealed class PatataArena : MonoBehaviour
{
    public const float MaxRadius = 13.5f, MinRadius = 7.5f, FenceHeight = .95f;
    const int Segments = 40;

    public float Radius { get; private set; } = MaxRadius;
    float targetRadius = MaxRadius;
    public readonly List<Vector3> Crates = new();
    public readonly List<Vector3> Trampolines = new();
    readonly List<Transform> segments = new();
    readonly List<Transform> trampTops = new();
    Transform root;

    public static float RadiusFor(int round, int rounds) => rounds <= 1 ? MaxRadius : Mathf.Lerp(MaxRadius, MinRadius, Mathf.Clamp01(round / (float)(rounds - 1)));

    public void Build()
    {
        root = new GameObject("Feria").transform;
        // Floor: one flat collider, rings for looks (they also show where the fence will close).
        var floor = new GameObject("Suelo", typeof(BoxCollider)); floor.transform.SetParent(root, false);
        floor.transform.position = new Vector3(0, -.5f, 0); floor.GetComponent<BoxCollider>().size = new Vector3(90, 1, 90);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Césped", new Vector3(0, -.06f, 0), new Vector3(90, .05f, 90), EscapeKit.Paint(new Color(.45f, .72f, .36f), .1f), false);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Borde", new Vector3(0, -.03f, 0), new Vector3(MaxRadius * 2f + 1.4f, .05f, MaxRadius * 2f + 1.4f), EscapeKit.Paint(new Color(.85f, .72f, .5f), .1f), false);
        Color[] rings = { new Color(1f, .55f, .45f), new Color(1f, .85f, .45f), new Color(.55f, .8f, 1f), new Color(.7f, .95f, .6f), new Color(1f, .7f, .85f), new Color(.98f, .95f, .88f) };
        float[] radii = { MaxRadius, 12f, 10.5f, 9f, MinRadius, 4f };
        for (int i = 0; i < radii.Length; i++)
            EscapeKit.Shape(PrimitiveType.Cylinder, root, "Anillo " + i, new Vector3(0, i * .004f, 0), new Vector3(radii[i] * 2f, .02f, radii[i] * 2f), EscapeKit.Paint(rings[i], .2f), false);
        // Centre star.
        var star = EscapeKit.Paint(new Color(1f, .45f, .3f), .3f);
        for (int i = 0; i < 4; i++) EscapeKit.Block(root, "Estrella", new Vector3(0, .03f, 0), new Vector3(.5f, .02f, 3f), star, .01f, false, new Vector3(0, i * 45f, 0));

        // Fence segments (moved when the arena shrinks).
        var red = EscapeKit.Paint(new Color(.95f, .3f, .3f), .35f); var white = EscapeKit.Paint(new Color(.98f, .96f, .92f), .35f);
        var cap = EscapeKit.Paint(new Color(1f, .82f, .3f), .4f);
        for (int i = 0; i < Segments; i++)
        {
            var seg = new GameObject("Valla " + i, typeof(BoxCollider)).transform; seg.SetParent(root, false);
            seg.GetComponent<BoxCollider>().center = new Vector3(0, 1.5f, 0); seg.GetComponent<BoxCollider>().size = new Vector3(1f, 3f, .4f);
            EscapeKit.Shape(PrimitiveType.Cube, seg, "Tabla", new Vector3(0, FenceHeight * .5f, 0), new Vector3(1f, FenceHeight, .22f), i % 2 == 0 ? red : white, false);
            EscapeKit.Shape(PrimitiveType.Sphere, seg, "Bola", new Vector3(0, FenceHeight + .12f, 0), Vector3.one * .3f, cap, false);
            segments.Add(seg);
        }
        Place(MaxRadius);

        // Crates and trampolines (inside the smallest ring).
        var wood = EscapeKit.Paint(new Color(.72f, .5f, .3f), .15f); var band = EscapeKit.Paint(new Color(.45f, .3f, .18f), .1f);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 30f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 3.6f;
            EscapeKit.Block(root, "Caja", p + Vector3.up * .7f, new Vector3(1.4f, 1.4f, 1.4f), wood, .08f, true, new Vector3(0, i * 25f, 0));
            EscapeKit.Block(root, "Fleje", p + Vector3.up * .7f, new Vector3(1.44f, .16f, 1.44f), band, .02f, false, new Vector3(0, i * 25f, 0));
            Crates.Add(p);
        }
        var bounce = EscapeKit.Paint(new Color(.35f, .75f, 1f), .5f); var legs = EscapeKit.Paint(new Color(.25f, .25f, .3f), .3f);
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + 90f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 5.6f;
            EscapeKit.Shape(PrimitiveType.Cylinder, root, "Aro", p + Vector3.up * .12f, new Vector3(1.8f, .12f, 1.8f), legs, false);
            trampTops.Add(EscapeKit.Shape(PrimitiveType.Cylinder, root, "Cama elástica", p + Vector3.up * .22f, new Vector3(1.5f, .03f, 1.5f), bounce, false).transform);
            Trampolines.Add(p);
        }

        // Outside: stalls, lamps, bunting, trees.
        var rng = new System.Random(4242);
        Color[] awnings = { new Color(.95f, .35f, .35f), new Color(.35f, .6f, .95f), new Color(.98f, .8f, .3f), new Color(.5f, .85f, .45f), new Color(.85f, .5f, .9f) };
        for (int i = 0; i < 10; i++)
        {
            float a = (i * 36f + 18f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 19.5f;
            var rot = new Vector3(0, i * 36f + 18f + 180f, 0);
            var stall = new GameObject("Puesto").transform; stall.SetParent(root, false); stall.SetPositionAndRotation(p, Quaternion.Euler(rot));
            EscapeKit.Block(stall, "Mostrador", new Vector3(0, .55f, 0), new Vector3(2.6f, 1.1f, 1.2f), EscapeKit.Paint(new Color(.95f, .9f, .8f)), .05f, true);
            foreach (float x in new[] { -1.2f, 1.2f }) EscapeKit.Block(stall, "Poste", new Vector3(x, 1.5f, -.5f), new Vector3(.12f, 3f, .12f), EscapeKit.Paint(new Color(.5f, .35f, .2f)), .02f, false);
            var aw = EscapeKit.Paint(awnings[i % awnings.Length], .3f);
            for (int s = 0; s < 5; s++) EscapeKit.Block(stall, "Toldo", new Vector3(-1.2f + s * .6f, 2.95f, -.15f), new Vector3(.6f, .1f, 1.5f), s % 2 == 0 ? aw : white, .02f, false, new Vector3(-12f, 0, 0));
            for (int s = 0; s < 3; s++) EscapeKit.Shape(PrimitiveType.Sphere, stall, "Globo", new Vector3(-.8f + s * .8f, 1.35f, 0), Vector3.one * .4f, EscapeKit.Paint(awnings[(i + s + 1) % awnings.Length], .6f), false);
        }
        var lampMat = EscapeKit.Paint(new Color(.2f, .2f, .25f), .4f); var bulb = EscapeKit.Glow(new Color(1f, .9f, .6f), 2f);
        var tops = new List<Vector3>();
        for (int i = 0; i < 8; i++)
        {
            float a = (i * 45f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 16f;
            EscapeKit.Block(root, "Farola", p + Vector3.up * 2.2f, new Vector3(.18f, 4.4f, .18f), lampMat, .03f, true);
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Bombilla", p + Vector3.up * 4.55f, Vector3.one * .5f, bulb, false);
            tops.Add(p + Vector3.up * 4.3f);
        }
        // Bunting between the lamps.
        for (int i = 0; i < tops.Count; i++)
        {
            Vector3 a = tops[i], b = tops[(i + 1) % tops.Count];
            for (int f = 1; f < 10; f++)
            {
                float t = f / 10f; var p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * .8f;
                var flag = EscapeKit.Shape(PrimitiveType.Cube, root, "Banderín", p + Vector3.down * .2f, new Vector3(.35f, .4f, .02f), EscapeKit.Paint(awnings[(i + f) % awnings.Length], .3f), false);
                flag.transform.rotation = Quaternion.LookRotation(Vector3.Cross(b - a, Vector3.up)) * Quaternion.Euler(0, 0, 45f);
            }
        }
        var leaf = EscapeKit.Paint(new Color(.3f, .62f, .3f), .1f); var trunk = EscapeKit.Paint(new Color(.45f, .3f, .18f), .1f);
        for (int i = 0; i < 26; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 24f + (float)rng.NextDouble() * 14f, h = 1.6f + (float)rng.NextDouble() * 1.4f;
            var p = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
            EscapeKit.Block(root, "Tronco", p + Vector3.up * h * .5f, new Vector3(.4f, h, .4f), trunk, .04f, false);
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Copa", p + Vector3.up * (h + .9f), Vector3.one * (2f + (float)rng.NextDouble()), leaf, false);
        }
    }

    void Place(float r)
    {
        Radius = r;
        float len = 2f * Mathf.PI * (r + .15f) / Segments * 1.06f;
        for (int i = 0; i < segments.Count; i++)
        {
            float a = (i + .5f) * 360f / Segments;
            var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
            segments[i].SetPositionAndRotation(dir * (r + .15f), Quaternion.LookRotation(dir));
            segments[i].localScale = new Vector3(len, 1f, 1f);
        }
    }

    public void ShrinkTo(float r) => targetRadius = r;
    public void SnapTo(float r) { targetRadius = r; Place(r); }

    void Update()
    {
        if (Mathf.Abs(Radius - targetRadius) > .001f) Place(Mathf.MoveTowards(Radius, targetRadius, 2.2f * Time.deltaTime));
    }

    public int TrampolineAt(Vector3 p)
    {
        for (int i = 0; i < Trampolines.Count; i++) { var d = p - Trampolines[i]; d.y = 0; if (d.magnitude < .75f && p.y < .6f) return i; }
        return -1;
    }
    public void Wobble(int i) { if (i >= 0 && i < trampTops.Count) SurvivalKit.Puff(trampTops[i].position, new Color(.6f, .85f, 1f), 3, .2f); }
}
