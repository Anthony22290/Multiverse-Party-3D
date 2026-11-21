using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pico, Pala y Tijera (2026-11-17) · block types, the shared block sequence, procedural pixel-art textures and the
/// three toy tools. Everything is generated at runtime (no imported art), with Point filtering for a crisp blocky look.
/// </summary>
public static class MinaKit
{
    public enum Block : byte { Tierra = 0, Piedra = 1, Hojas = 2, PiedraDura = 3, Oro = 4 }
    public enum Tool : byte { Pala = 0, Pico = 1, Tijera = 2 }

    public static readonly string[] ToolNames = { "PALA", "PICO", "TIJERA" };
    public static readonly Color[] ToolColors = { new Color(.85f, .6f, .3f), new Color(.7f, .78f, .9f), new Color(.45f, .95f, .4f) };

    /// <summary>Tool that breaks the block. Gold breaks with anything.</summary>
    public static bool Works(Block b, Tool t) => b == Block.Oro || (b == Block.Tierra && t == Tool.Pala) || ((b == Block.Piedra || b == Block.PiedraDura) && t == Tool.Pico) || (b == Block.Hojas && t == Tool.Tijera);
    public static Tool Needed(Block b) => b == Block.Tierra ? Tool.Pala : b == Block.Hojas ? Tool.Tijera : Tool.Pico;
    public static int HitsFor(Block b) => b == Block.PiedraDura ? 2 : 1;

    /// <summary>The same column for everybody (fair race): soil near the top, more rock deeper, leaves in pockets.</summary>
    public static Block[] Sequence(int seed, int depth)
    {
        var rng = new System.Random(seed);
        var seq = new Block[depth];
        Block prev = Block.Tierra; int run = 0;
        for (int i = 0; i < depth; i++)
        {
            float k = i / (float)depth;
            Block b;
            if (i == 0) b = Block.Tierra;
            else if (run < 1 + rng.Next(2) && rng.NextDouble() < .45) b = prev;   // short runs, frequent switches
            else
            {
                double r = rng.NextDouble();
                float dirt = Mathf.Lerp(.55f, .15f, k), stone = Mathf.Lerp(.2f, .45f, k), hard = k > .3f ? Mathf.Lerp(0f, .18f, (k - .3f) / .7f) : 0f;
                b = r < dirt ? Block.Tierra : r < dirt + stone ? Block.Piedra : r < dirt + stone + hard ? Block.PiedraDura : Block.Hojas;
                if (i > 6 && rng.NextDouble() < .035) b = Block.Oro;
            }
            run = b == prev ? run + 1 : 0; prev = b;
            seq[i] = b;
        }
        return seq;
    }

    // ------------------------------------------------------------------ textures and materials

    static readonly Dictionary<string, Material> mats = new();

    public static Material BlockMat(Block b, bool cracked = false)
    {
        string key = b + (cracked ? "_rota" : "");
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(EscapeKit.Paint(Color.white, .05f)) { name = "Bloque " + key };
        var tex = Make(b, cracked);
        m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (b == Block.Oro && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(.35f, .26f, .05f)); }
        mats[key] = m; return m;
    }

    public static Material Tinted(string key, Block look, Color tint)
    {
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(BlockMat(look)) { name = key, color = tint };
        mats[key] = m; return m;
    }

    static Texture2D Make(Block b, bool cracked)
    {
        const int N = 16;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Píxeles " + b };
        var rng = new System.Random(17 + (int)b * 101);
        float R() => (float)rng.NextDouble();
        Color[] px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color c;
                switch (b)
                {
                    case Block.Tierra:
                        c = Color.Lerp(new Color(.48f, .3f, .17f), new Color(.6f, .4f, .23f), R());
                        if (R() < .07f) c = new Color(.38f, .24f, .14f);
                        if (R() < .03f) c = new Color(.7f, .66f, .6f);   // pebble
                        break;
                    case Block.Piedra:
                        c = Color.Lerp(new Color(.5f, .5f, .52f), new Color(.64f, .64f, .66f), R());
                        if ((x + y * 3) % 11 == 0 && R() < .7f) c = new Color(.36f, .36f, .39f);
                        break;
                    case Block.PiedraDura:
                        c = Color.Lerp(new Color(.22f, .23f, .3f), new Color(.32f, .33f, .42f), R());
                        if (R() < .05f) c = new Color(.6f, .75f, 1f);   // sparkle
                        break;
                    case Block.Hojas:
                        c = Color.Lerp(new Color(.18f, .5f, .16f), new Color(.32f, .72f, .26f), R());
                        if (((x / 3) + (y / 3)) % 2 == 0 && R() < .35f) c = new Color(.12f, .36f, .12f);
                        if (R() < .03f) c = new Color(.95f, .45f, .55f);   // little flower
                        break;
                    default:
                        c = Color.Lerp(new Color(.85f, .62f, .12f), new Color(1f, .86f, .3f), R());
                        if (R() < .06f) c = Color.white;
                        break;
                }
                // Darker border so blocks read as blocks.
                if (x == 0 || y == 0 || x == N - 1 || y == N - 1) c *= .72f;
                c.a = 1f; px[y * N + x] = c;
            }
        if (cracked)
            foreach (var (x, y) in new[] { (3, 12), (4, 11), (5, 11), (6, 10), (7, 9), (8, 9), (9, 8), (9, 7), (10, 6), (11, 5), (12, 5), (6, 12), (7, 13), (10, 9), (11, 10), (4, 6), (5, 7), (6, 7) })
                px[y * N + x] = new Color(.05f, .05f, .07f);
        t.SetPixels(px); t.Apply(false, false);
        return t;
    }

    // ------------------------------------------------------------------ tools (toy models built from blocks)

    public static GameObject BuildTool(Tool tool, Transform parent)
    {
        var root = new GameObject(ToolNames[(int)tool]).transform; root.SetParent(parent, false);
        var wood = EscapeKit.Paint(new Color(.62f, .42f, .24f), .1f);
        var metal = EscapeKit.Paint(new Color(.78f, .8f, .86f), .55f);
        var col = EscapeKit.Paint(ToolColors[(int)tool], .3f);
        switch (tool)
        {
            case Tool.Pala:
                EscapeKit.Block(root, "Mango", new Vector3(0, .32f, 0), new Vector3(.06f, .64f, .06f), wood, .02f, false);
                EscapeKit.Block(root, "Asa", new Vector3(0, .66f, 0), new Vector3(.18f, .06f, .06f), col, .02f, false);
                EscapeKit.Block(root, "Hoja", new Vector3(0, -.06f, 0), new Vector3(.22f, .26f, .04f), metal, .03f, false);
                EscapeKit.Block(root, "Punta", new Vector3(0, -.21f, 0), new Vector3(.14f, .06f, .04f), metal, .02f, false);
                break;
            case Tool.Pico:
                EscapeKit.Block(root, "Mango", new Vector3(0, .3f, 0), new Vector3(.06f, .62f, .06f), wood, .02f, false);
                EscapeKit.Block(root, "Cabeza", new Vector3(0, .6f, 0), new Vector3(.5f, .08f, .08f), metal, .02f, false);
                EscapeKit.Block(root, "Punta izq.", new Vector3(-.27f, .56f, 0), new Vector3(.08f, .1f, .07f), metal, .02f, false, new Vector3(0, 0, 30));
                EscapeKit.Block(root, "Punta der.", new Vector3(.27f, .56f, 0), new Vector3(.08f, .1f, .07f), metal, .02f, false, new Vector3(0, 0, -30));
                EscapeKit.Block(root, "Anillo", new Vector3(0, .6f, 0), new Vector3(.1f, .1f, .1f), col, .02f, false);
                break;
            default:
                EscapeKit.Block(root, "Hoja 1", new Vector3(-.03f, .36f, 0), new Vector3(.05f, .36f, .03f), metal, .015f, false, new Vector3(0, 0, -8));
                EscapeKit.Block(root, "Hoja 2", new Vector3(.03f, .36f, .01f), new Vector3(.05f, .36f, .03f), metal, .015f, false, new Vector3(0, 0, 8));
                EscapeKit.Block(root, "Eje", new Vector3(0, .18f, .02f), new Vector3(.06f, .06f, .06f), metal, .02f, false);
                EscapeKit.Block(root, "Anilla 1", new Vector3(-.08f, .04f, 0), new Vector3(.1f, .14f, .04f), col, .03f, false);
                EscapeKit.Block(root, "Anilla 2", new Vector3(.08f, .04f, 0), new Vector3(.1f, .14f, .04f), col, .03f, false);
                break;
        }
        return root.gameObject;
    }
}
