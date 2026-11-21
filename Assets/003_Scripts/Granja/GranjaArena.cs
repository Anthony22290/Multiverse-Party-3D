using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The farm of Gallinas Locas: a round yard with a wooden fence, a red barn in the middle (chickens come out of
/// its door), hay bales, a trough, and one pen per player spread evenly round the yard (4 players → the four
/// corners, 6 → a hexagon). Each pen opens towards the middle. Built at play time, identical on every computer.
/// </summary>
public sealed class GranjaArena : MonoBehaviour
{
    public const float FieldRadius = 21f, PenRadius = 15.6f, PenHalf = 2.7f, Opening = 2.6f, FenceHeight = .9f;
    public int Pens { get; private set; }
    public readonly List<Vector3> PenCenter = new();
    public readonly List<Quaternion> PenRot = new();   // +Z of a pen points to the middle (its opening)
    public readonly List<Vector3> Obstacles = new();    // hay bales, trough, barn corners (for bots)
    public Vector3 BarnDoor { get; private set; }
    readonly List<TextMesh> signs = new(); readonly List<Renderer> flags = new();
    Transform root;

    public void Build(int pens)
    {
        Pens = pens; root = new GameObject("Granja").transform;
        var floor = new GameObject("Suelo", typeof(BoxCollider)); floor.transform.SetParent(root, false);
        floor.transform.position = new Vector3(0, -.5f, 0); floor.GetComponent<BoxCollider>().size = new Vector3(140, 1, 140);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Prado", new Vector3(0, -.06f, 0), new Vector3(140, .05f, 140), EscapeKit.Paint(new Color(.47f, .74f, .35f), .08f), false);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Patio", new Vector3(0, -.02f, 0), new Vector3(FieldRadius * 2f + .6f, .04f, FieldRadius * 2f + .6f), EscapeKit.Paint(new Color(.78f, .66f, .45f), .05f), false);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Hierba del patio", new Vector3(0, 0, 0), new Vector3(FieldRadius * 1.5f, .04f, FieldRadius * 1.5f), EscapeKit.Paint(new Color(.58f, .78f, .4f), .05f), false);

        // Outer fence: posts and two rails, tall invisible collider.
        var wood = EscapeKit.Paint(new Color(.62f, .43f, .26f), .12f); var dark = EscapeKit.Paint(new Color(.45f, .3f, .18f), .1f);
        int segs = 56;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * 360f / segs, a1 = (i + 1) * 360f / segs;
            Vector3 p0 = Quaternion.Euler(0, a0, 0) * Vector3.forward * FieldRadius, p1 = Quaternion.Euler(0, a1, 0) * Vector3.forward * FieldRadius;
            var mid = (p0 + p1) * .5f; float len = (p1 - p0).magnitude + .05f; var rot = Quaternion.LookRotation(p1 - p0);
            var seg = new GameObject("Valla", typeof(BoxCollider)).transform; seg.SetParent(root, false); seg.SetPositionAndRotation(mid, rot);
            var bc = seg.GetComponent<BoxCollider>(); bc.center = Vector3.up * 1.5f; bc.size = new Vector3(.4f, 3f, len);
            EscapeKit.Block(seg, "Poste", new Vector3(0, .55f, -len * .5f), new Vector3(.18f, 1.1f, .18f), dark, .03f, false);
            EscapeKit.Block(seg, "Tabla alta", new Vector3(0, .85f, 0), new Vector3(.08f, .14f, len), wood, .02f, false);
            EscapeKit.Block(seg, "Tabla baja", new Vector3(0, .45f, 0), new Vector3(.08f, .14f, len), wood, .02f, false);
        }

        // Barn in the middle.
        var red = EscapeKit.Paint(new Color(.78f, .2f, .17f), .2f); var white = EscapeKit.Paint(new Color(.96f, .94f, .88f), .2f); var roof = EscapeKit.Paint(new Color(.35f, .3f, .32f), .25f);
        EscapeKit.Block(root, "Granero", new Vector3(0, 1.6f, -.6f), new Vector3(5f, 3.2f, 4f), red, .06f, true);
        for (int s = -1; s <= 1; s += 2)
            EscapeKit.Block(root, "Tejado", new Vector3(s * 1.45f, 3.95f, -.6f), new Vector3(3.3f, .22f, 4.4f), roof, .04f, false, new Vector3(0, 0, -s * 32f));
        EscapeKit.Block(root, "Frontón", new Vector3(0, 3.6f, 1.42f), new Vector3(2.6f, 1f, .08f), red, .02f, false);
        EscapeKit.Block(root, "Puerta", new Vector3(0, 1.05f, 1.42f), new Vector3(2f, 2.1f, .1f), EscapeKit.Paint(new Color(.45f, .14f, .12f)), .03f, false);
        EscapeKit.Block(root, "Aspa", new Vector3(0, 1.05f, 1.48f), new Vector3(2.5f, .14f, .05f), white, .02f, false, new Vector3(0, 0, 40f));
        EscapeKit.Block(root, "Aspa", new Vector3(0, 1.05f, 1.48f), new Vector3(2.5f, .14f, .05f), white, .02f, false, new Vector3(0, 0, -40f));
        BarnDoor = new Vector3(0, 0, 2.6f);
        Obstacles.Add(new Vector3(0, 0, -.6f));

        // Hay bales and a trough round the barn.
        var hay = EscapeKit.Paint(new Color(.93f, .8f, .42f), .1f); var band = EscapeKit.Paint(new Color(.7f, .55f, .25f), .1f);
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60f + 30f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 8.6f;
            EscapeKit.Block(root, "Paca", p + Vector3.up * .45f, new Vector3(1.6f, .9f, 1f), hay, .1f, true, new Vector3(0, i * 60f + 30f, 0));
            EscapeKit.Block(root, "Cuerda", p + Vector3.up * .45f, new Vector3(.08f, .92f, 1.02f), band, .01f, false, new Vector3(0, i * 60f + 30f, 0));
            Obstacles.Add(p);
        }
        var trough = new Vector3(-5.2f, 0, -4.5f);
        EscapeKit.Block(root, "Abrevadero", trough + Vector3.up * .3f, new Vector3(2.2f, .6f, .8f), EscapeKit.Paint(new Color(.5f, .5f, .55f), .4f), .05f, true);
        EscapeKit.Block(root, "Agua", trough + Vector3.up * .58f, new Vector3(2f, .04f, .6f), EscapeKit.Paint(new Color(.35f, .65f, .9f), .8f), .01f, false);
        Obstacles.Add(trough);

        // Pens.
        for (int i = 0; i < pens; i++)
        {
            float ang = i * 360f / pens + (pens == 4 ? 45f : pens == 6 ? 30f : 0f);
            var dir = Quaternion.Euler(0, ang, 0) * Vector3.forward;
            var c = dir * PenRadius; var rot = Quaternion.LookRotation(-dir);
            PenCenter.Add(c); PenRot.Add(rot);
            BuildPen(i, c, rot, wood, dark);
        }

        // Outside: trees, a silo, a windmill-ish tower.
        var leaf = EscapeKit.Paint(new Color(.32f, .62f, .3f), .1f); var trunk = EscapeKit.Paint(new Color(.45f, .3f, .18f), .1f);
        var rng = new System.Random(77);
        for (int i = 0; i < 34; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 25f + (float)rng.NextDouble() * 18f, h = 1.6f + (float)rng.NextDouble() * 1.6f;
            var p = new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r);
            EscapeKit.Block(root, "Tronco", p + Vector3.up * h * .5f, new Vector3(.4f, h, .4f), trunk, .04f, false);
            EscapeKit.Shape(PrimitiveType.Sphere, root, "Copa", p + Vector3.up * (h + .9f), Vector3.one * (2.2f + (float)rng.NextDouble()), leaf, false);
        }
        var silo = new Vector3(-24f, 0, 8f);
        EscapeKit.Shape(PrimitiveType.Cylinder, root, "Silo", silo + Vector3.up * 4f, new Vector3(3.4f, 4f, 3.4f), EscapeKit.Paint(new Color(.82f, .84f, .88f), .5f), false);
        EscapeKit.Shape(PrimitiveType.Sphere, root, "Cúpula", silo + Vector3.up * 8f, new Vector3(3.4f, 2f, 3.4f), red, false);
    }

    void BuildPen(int i, Vector3 c, Quaternion rot, Material wood, Material dark)
    {
        var pen = new GameObject("Corral " + (i + 1)).transform; pen.SetParent(root, false); pen.SetPositionAndRotation(c, rot);
        float h = PenHalf, o = Opening * .5f;
        EscapeKit.Block(pen, "Paja", new Vector3(0, .01f, 0), new Vector3(h * 2f, .03f, h * 2f), EscapeKit.Paint(new Color(.95f, .85f, .5f), .05f), .01f, false);
        void Wall(Vector3 center, Vector3 size)
        {
            var w = EscapeKit.Block(pen, "Cerca", center + Vector3.up * (FenceHeight * .5f), size + Vector3.up * FenceHeight, wood, .03f, false);
            var col = new GameObject("Choque", typeof(BoxCollider)); col.transform.SetParent(pen, false);
            col.transform.localPosition = center + Vector3.up * .9f; col.GetComponent<BoxCollider>().size = new Vector3(size.x + .1f, 1.8f, size.z + .1f);
        }
        Wall(new Vector3(-h, 0, 0), new Vector3(.14f, 0, h * 2f));
        Wall(new Vector3(h, 0, 0), new Vector3(.14f, 0, h * 2f));
        Wall(new Vector3(0, 0, -h), new Vector3(h * 2f, 0, .14f));
        float side = h - o;
        Wall(new Vector3(-(o + side * .5f), 0, h), new Vector3(side, 0, .14f));
        Wall(new Vector3(o + side * .5f, 0, h), new Vector3(side, 0, .14f));
        foreach (var x in new[] { -h, h }) foreach (var z in new[] { -h, h }) EscapeKit.Block(pen, "Poste", new Vector3(x, .6f, z), new Vector3(.2f, 1.2f, .2f), dark, .03f, false);
        foreach (var x in new[] { -o, o }) EscapeKit.Block(pen, "Poste puerta", new Vector3(x, .75f, h), new Vector3(.22f, 1.5f, .22f), dark, .03f, false);
        // Little coop at the back and a flag/sign with the owner's name.
        EscapeKit.Block(pen, "Gallinero", new Vector3(h - .8f, .55f, -h + .7f), new Vector3(1.2f, 1.1f, 1f), EscapeKit.Paint(new Color(.85f, .75f, .55f), .1f), .05f, false);
        var flag = EscapeKit.Block(pen, "Bandera", new Vector3(-o - .1f, 1.75f, h + .35f), new Vector3(.9f, .55f, .04f), EscapeKit.Paint(Color.white, .3f), .01f, false);
        flags.Add(flag.GetComponent<Renderer>());
        EscapeKit.Block(pen, "Mástil", new Vector3(-o - .55f, 1.2f, h + .35f), new Vector3(.06f, 2.4f, .06f), dark, .01f, false);
        var sign = EscapeKit.Text(pen, "", new Vector3(0, 2.1f, h + .1f), new Vector3(0, 180f, 0), .045f, Color.white);
        signs.Add(sign);
    }

    void LateUpdate()
    {
        var cam = Camera.main; if (cam == null) return;
        foreach (var t in signs) if (t != null) t.transform.rotation = cam.transform.rotation;
    }

    public void SetOwner(int pen, string name, Color color)
    {
        if (pen < 0 || pen >= signs.Count) return;
        signs[pen].text = name; signs[pen].color = color;
        flags[pen].sharedMaterial = EscapeKit.Paint(color, .3f);
    }

    public Vector3 ToPen(int pen, Vector3 world) => Quaternion.Inverse(PenRot[pen]) * (world - PenCenter[pen]);
    public Vector3 FromPen(int pen, Vector3 local) => PenCenter[pen] + PenRot[pen] * local;
    public bool Inside(int pen, Vector3 world, float margin = .2f)
    {
        var l = ToPen(pen, world); float h = PenHalf - margin;
        return Mathf.Abs(l.x) < h && l.z > -h && l.z < h;
    }
    public int PenAt(Vector3 world) { for (int i = 0; i < Pens; i++) if (Inside(i, world)) return i; return -1; }
    /// <summary>Just outside the opening, where a player stands to drop a chicken in.</summary>
    public Vector3 PenMouth(int pen, float outside = 1.6f) => FromPen(pen, new Vector3(0, 0, PenHalf + outside));
    public Vector3 Spawn(int pen) => FromPen(pen, new Vector3(0, 0, PenHalf + 3.2f)) + Vector3.up * .1f;
    public Vector3 ClampInside(int pen, Vector3 world)
    {
        var l = ToPen(pen, world); float h = PenHalf - .35f;
        l.x = Mathf.Clamp(l.x, -h, h); l.z = Mathf.Clamp(l.z, -h, h); return FromPen(pen, l);
    }
}
