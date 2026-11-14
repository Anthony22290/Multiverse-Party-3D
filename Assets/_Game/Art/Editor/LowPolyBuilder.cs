using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Accumulates flat-shaded low-poly geometry with one submesh per material.
/// Primitives combine into readable silhouettes (bevelled boxes, prisms, roofs, rings, arches).
/// All primitives are convex and auto-oriented outward, so winding mistakes cannot invert faces.
/// </summary>
public sealed class LowPolyBuilder
{
    readonly List<Vector3> verts = new();
    readonly List<Vector3> normals = new();
    readonly List<Vector2> uvs = new();
    readonly List<Material> materials = new();
    readonly List<List<int>> tris = new();
    Matrix4x4 m = Matrix4x4.identity;
    readonly Stack<Matrix4x4> stack = new();

    public IDisposable At(Vector3 position, Vector3 euler = default, Vector3? scale = null)
    {
        stack.Push(m);
        m = m * Matrix4x4.TRS(position, Quaternion.Euler(euler), scale ?? Vector3.one);
        return new Pop(this);
    }
    sealed class Pop : IDisposable { readonly LowPolyBuilder b; public Pop(LowPolyBuilder b) { this.b = b; } public void Dispose() { b.m = b.stack.Pop(); } }

    int Slot(Material mat)
    {
        int i = materials.IndexOf(mat);
        if (i >= 0) return i;
        materials.Add(mat); tris.Add(new List<int>()); return materials.Count - 1;
    }

    /// <summary>Adds a planar convex polygon (local space). `inside` is any point inside the solid, used to orient it.</summary>
    public void Face(Material mat, Vector3 inside, params Vector3[] poly)
    {
        if (poly.Length < 3) return;
        var w = new Vector3[poly.Length];
        for (int i = 0; i < poly.Length; i++) w[i] = m.MultiplyPoint3x4(poly[i]);
        Vector3 wInside = m.MultiplyPoint3x4(inside);
        Vector3 n = Vector3.zero;
        for (int i = 1; i < w.Length - 1; i++) n += Vector3.Cross(w[i] - w[0], w[i + 1] - w[0]);
        if (n.sqrMagnitude < 1e-12f) return;
        Vector3 centroid = Vector3.zero; foreach (var p in w) centroid += p; centroid /= w.Length;
        if (Vector3.Dot(n, centroid - wInside) < 0) { Array.Reverse(w); n = -n; }
        n.Normalize();
        int slot = Slot(mat), start = verts.Count;
        // Planar UV projection on the dominant axis: tiles cleanly for wood/stone textures if assigned later.
        Vector3 a = Mathf.Abs(n.y) > .7f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(n, a).normalized, v = Vector3.Cross(n, u);
        foreach (var p in w) { verts.Add(p); normals.Add(n); uvs.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v))); }
        // Unity uses clockwise front faces: reverse the counter-clockwise (right-hand) order.
        for (int i = 1; i < w.Length - 1; i++) tris[slot].AddRange(new[] { start, start + i + 1, start + i });
    }

    public void Box(Material mat, Vector3 center, Vector3 size, Vector3 euler = default)
    {
        using (At(center, euler))
        {
            Vector3 h = size * .5f;
            Vector3 P(int x, int y, int z) => new Vector3(x * h.x, y * h.y, z * h.z);
            Face(mat, Vector3.zero, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
            Face(mat, Vector3.zero, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
            Face(mat, Vector3.zero, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
            Face(mat, Vector3.zero, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
            Face(mat, Vector3.zero, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
            Face(mat, Vector3.zero, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
        }
    }

    /// <summary>Box with chamfered edges and corners: the cheapest way to read as "soft cartoon".</summary>
    public void Bevel(Material mat, Vector3 center, Vector3 size, float bevel, Vector3 euler = default)
    {
        Vector3 h = size * .5f;
        float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .95f);
        if (b <= .001f) { Box(mat, center, size, euler); return; }
        using (At(center, euler))
        {
            // Point at corner (sx,sy,sz) lying on the face whose normal axis is `axis`.
            Vector3 P(int sx, int sy, int sz, int axis) => new Vector3(
                sx * (axis == 0 ? h.x : h.x - b), sy * (axis == 1 ? h.y : h.y - b), sz * (axis == 2 ? h.z : h.z - b));
            int[] s = { -1, 1 };
            foreach (int a in s)
            {
                Face(mat, Vector3.zero, P(a, -1, -1, 0), P(a, 1, -1, 0), P(a, 1, 1, 0), P(a, -1, 1, 0));
                Face(mat, Vector3.zero, P(-1, a, -1, 1), P(1, a, -1, 1), P(1, a, 1, 1), P(-1, a, 1, 1));
                Face(mat, Vector3.zero, P(-1, -1, a, 2), P(1, -1, a, 2), P(1, 1, a, 2), P(-1, 1, a, 2));
            }
            foreach (int x in s) foreach (int y in s)
                Face(mat, Vector3.zero, P(x, y, -1, 0), P(x, y, -1, 1), P(x, y, 1, 1), P(x, y, 1, 0));
            foreach (int x in s) foreach (int z in s)
                Face(mat, Vector3.zero, P(x, -1, z, 0), P(x, -1, z, 2), P(x, 1, z, 2), P(x, 1, z, 0));
            foreach (int y in s) foreach (int z in s)
                Face(mat, Vector3.zero, P(-1, y, z, 1), P(-1, y, z, 2), P(1, y, z, 2), P(1, y, z, 1));
            foreach (int x in s) foreach (int y in s) foreach (int z in s)
                Face(mat, Vector3.zero, P(x, y, z, 0), P(x, y, z, 1), P(x, y, z, 2));
        }
    }

    /// <summary>Vertical frustum/prism: bottom radius r0, top radius r1. sides=4 gives a square post rotated 45°.</summary>
    public void Frustum(Material mat, Vector3 baseCenter, float r0, float r1, float height, int sides, Vector3 euler = default, float twist = 0f)
    {
        using (At(baseCenter, euler))
        {
            var bottom = new Vector3[sides]; var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides + twist) * Mathf.PI * 2f;
                bottom[i] = new Vector3(Mathf.Cos(a) * r0, 0, Mathf.Sin(a) * r0);
                top[i] = new Vector3(Mathf.Cos(a) * r1, height, Mathf.Sin(a) * r1);
            }
            Vector3 inside = new Vector3(0, height * .5f, 0);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (r1 > .0005f) Face(mat, inside, bottom[i], bottom[j], top[j], top[i]);
                else Face(mat, inside, bottom[i], bottom[j], top[0]);
            }
            Face(mat, inside, bottom);
            if (r1 > .0005f) Face(mat, inside, top);
        }
    }

    public void Cylinder(Material mat, Vector3 baseCenter, float radius, float height, int sides, Vector3 euler = default) =>
        Frustum(mat, baseCenter, radius, radius, height, sides, euler);

    public void Cone(Material mat, Vector3 baseCenter, float radius, float height, int sides, Vector3 euler = default) =>
        Frustum(mat, baseCenter, radius, 0f, height, sides, euler);

    /// <summary>Gable roof (triangular prism) with overhang, ridge along local Z.</summary>
    public void Gable(Material mat, Vector3 baseCenter, float width, float depth, float height, Vector3 euler = default, float thickness = .12f)
    {
        using (At(baseCenter, euler))
        {
            float w = width * .5f, d = depth * .5f;
            // Two sloped slabs give the roof a visible thickness at the eaves.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 eave = new Vector3(side * w, 0, 0), ridge = new Vector3(0, height, 0);
                Vector3 down = Vector3.down * thickness;
                var a = new[] { eave + Vector3.back * d, ridge + Vector3.back * d, ridge + Vector3.forward * d, eave + Vector3.forward * d };
                Vector3 inside = (eave + ridge) * .5f + down * .5f + Vector3.right * -side * .001f;
                var bot = new Vector3[4]; for (int i = 0; i < 4; i++) bot[i] = a[i] + down;
                Face(mat, inside, a[0], a[1], a[2], a[3]);
                Face(mat, inside, bot[0], bot[1], bot[2], bot[3]);
                Face(mat, inside, a[0], a[3], bot[3], bot[0]);
                Face(mat, inside, a[0], a[1], bot[1], bot[0]);
                Face(mat, inside, a[3], a[2], bot[2], bot[3]);
            }
        }
    }

    /// <summary>Solid triangular gable wall filling, ridge along local Z.</summary>
    public void GableFill(Material mat, Vector3 baseCenter, float width, float depth, float height, Vector3 euler = default)
    {
        using (At(baseCenter, euler))
        {
            float w = width * .5f, d = depth * .5f;
            Vector3 inside = new Vector3(0, height * .3f, 0);
            Vector3 l0 = new(-w, 0, -d), r0 = new(w, 0, -d), t0 = new(0, height, -d), l1 = new(-w, 0, d), r1 = new(w, 0, d), t1 = new(0, height, d);
            Face(mat, inside, l0, r0, t0); Face(mat, inside, l1, r1, t1);
            Face(mat, inside, l0, t0, t1, l1); Face(mat, inside, r0, t0, t1, r1); Face(mat, inside, l0, r0, r1, l1);
        }
    }

    /// <summary>Hip/pyramid roof: rectangle base narrowing to a ridge (ridge = 0 for a pyramid).</summary>
    public void Hip(Material mat, Vector3 baseCenter, float width, float depth, float height, float ridge, Vector3 euler = default)
    {
        using (At(baseCenter, euler))
        {
            float w = width * .5f, d = depth * .5f, r = Mathf.Min(ridge, depth) * .5f;
            Vector3 inside = new Vector3(0, height * .3f, 0);
            Vector3 a = new(-w, 0, -d), b = new(w, 0, -d), c = new(w, 0, d), e = new(-w, 0, d);
            Vector3 t0 = new(0, height, -r), t1 = new(0, height, r);
            Face(mat, inside, a, b, c, e);
            Face(mat, inside, a, b, t0);
            Face(mat, inside, c, e, t1);
            if (r > .001f) { Face(mat, inside, b, c, t1, t0); Face(mat, inside, e, a, t0, t1); }
            else { Face(mat, inside, b, c, t0); Face(mat, inside, e, a, t0); }
        }
    }

    /// <summary>Low-poly ring (tyre, lifebuoy, wheel rim). Axis = local Y.</summary>
    public void Torus(Material mat, Vector3 center, float radius, float tube, int segments, int sides, Vector3 euler = default, Material alt = null)
    {
        Material Pick(int i) => alt != null && (i * 8 / segments) % 2 == 1 ? alt : mat;
        using (At(center, euler))
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 c0 = new(Mathf.Cos(a0) * radius, 0, Mathf.Sin(a0) * radius), c1 = new(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                for (int j = 0; j < sides; j++)
                {
                    float b0 = j * Mathf.PI * 2f / sides, b1 = (j + 1) * Mathf.PI * 2f / sides;
                    Vector3 P(Vector3 c, float a, float b) => c + (new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b)) * tube;
                    Vector3 inside = (c0 + c1) * .5f;
                    Face(Pick(i), inside, P(c0, a0, b0), P(c1, a1, b0), P(c1, a1, b1), P(c0, a0, b1));
                }
            }
        }
    }

    /// <summary>Faceted ball (bushes, lamp globes, clouds, canopies). detail 0 = icosahedron, 1 = 80 faces.</summary>
    public void Ball(Material mat, Vector3 center, Vector3 radii, int detail = 1, Vector3 euler = default)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var p = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
        for (int i = 0; i < p.Count; i++) p[i] = p[i].normalized;
        var f = new List<int[]> { new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11}, new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
            new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9}, new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1} };
        for (int d = 0; d < detail; d++)
        {
            var next = new List<int[]>();
            foreach (var tri in f)
            {
                int Mid(int a, int b) { p.Add(((p[a] + p[b]) * .5f).normalized); return p.Count - 1; }
                int ab = Mid(tri[0], tri[1]), bc = Mid(tri[1], tri[2]), ca = Mid(tri[2], tri[0]);
                next.Add(new[] { tri[0], ab, ca }); next.Add(new[] { tri[1], bc, ab }); next.Add(new[] { tri[2], ca, bc }); next.Add(new[] { ab, bc, ca });
            }
            f = next;
        }
        using (At(center, euler, radii))
            foreach (var tri in f) Face(mat, Vector3.zero, p[tri[0]], p[tri[1]], p[tri[2]]);
    }

    /// <summary>Semicircular arch of bevelled voussoirs in the local XY plane.</summary>
    public void Arch(Material mat, Vector3 center, float radius, float thickness, float depth, int pieces)
    {
        for (int i = 0; i < pieces; i++)
        {
            float a0 = Mathf.PI * i / pieces, a1 = Mathf.PI * (i + 1) / pieces;
            float ri = radius - thickness * .5f, ro = radius + thickness * .5f;
            Vector3 P(float a, float r, float z) => center + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
            float d = depth * .5f;
            var q = new[] { P(a0, ri, -d), P(a0, ro, -d), P(a1, ro, -d), P(a1, ri, -d), P(a0, ri, d), P(a0, ro, d), P(a1, ro, d), P(a1, ri, d) };
            Vector3 inside = Vector3.zero; foreach (var v in q) inside += v; inside /= 8;
            Face(mat, inside, q[0], q[1], q[2], q[3]); Face(mat, inside, q[4], q[5], q[6], q[7]);
            Face(mat, inside, q[0], q[1], q[5], q[4]); Face(mat, inside, q[3], q[2], q[6], q[7]);
            Face(mat, inside, q[1], q[2], q[6], q[5]); Face(mat, inside, q[0], q[3], q[7], q[4]);
        }
    }

    /// <summary>Flat quad, both sides (flags, signs, awning skirts).</summary>
    public void Plate(Material mat, Vector3 center, Vector2 size, Vector3 euler = default, float thickness = .03f) =>
        Box(mat, center, new Vector3(size.x, size.y, thickness), euler);

    /// <summary>Striped awning: alternating colored sloped strips plus a scalloped front skirt.</summary>
    public void Awning(Material a, Material b, Vector3 backTop, float width, float depth, float drop, int stripes)
    {
        float sw = width / stripes;
        float slope = Mathf.Atan2(drop, depth) * Mathf.Rad2Deg;
        float len = Mathf.Sqrt(depth * depth + drop * drop);
        for (int i = 0; i < stripes; i++)
        {
            var mat = i % 2 == 0 ? a : b;
            float x = -width * .5f + sw * (i + .5f);
            Box(mat, backTop + new Vector3(x, -drop * .5f, -depth * .5f), new Vector3(sw + .002f, .06f, len), new Vector3(-slope, 0, 0));
            // Scalloped skirt: each stripe hangs a tab with a pointed tip.
            Vector3 front = backTop + new Vector3(x, -drop, -depth);
            Box(mat, front + new Vector3(0, -.14f, 0), new Vector3(sw, .28f, .04f));
            using (At(front + new Vector3(0, -.28f, 0), new Vector3(180, 45, 0)))
                Frustum(mat, Vector3.zero, sw * .7f, 0f, .2f, 4, default, .125f);
        }
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
        mesh.subMeshCount = tris.Count;
        for (int i = 0; i < tris.Count; i++) mesh.SetTriangles(tris[i], i);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
        return mesh;
    }

    public Material[] Materials => materials.ToArray();
    public int TriangleCount { get { int n = 0; foreach (var t in tris) n += t.Count / 3; return n; } }
}
