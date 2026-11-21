using UnityEngine;

/// <summary>The things the cannon fires in ¡Atrápalo!: fruit (+1), the golden pineapple (+2) and junk (an old boot or a bomb: −1 life).</summary>
public static class AtrapaItem
{
    public enum Kind : byte { Apple, Orange, Banana, Watermelon, GoldPineapple, Boot, Bomb }
    public static bool Bad(Kind k) => k == Kind.Boot || k == Kind.Bomb;
    public static int Points(Kind k) => k == Kind.GoldPineapple ? 2 : Bad(k) ? 0 : 1;
    public static string Name(Kind k) => k switch
    {
        Kind.Apple => "manzana", Kind.Orange => "naranja", Kind.Banana => "plátano", Kind.Watermelon => "sandía",
        Kind.GoldPineapple => "piña dorada", Kind.Boot => "bota vieja", _ => "bomba"
    };

    public static Transform Build(Kind k)
    {
        var root = new GameObject("Objeto · " + Name(k)).transform;
        switch (k)
        {
            case Kind.Apple:
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Manzana", Vector3.zero, Vector3.one * .42f, EscapeKit.Paint(new Color(.88f, .12f, .12f), .7f), false);
                EscapeKit.Shape(PrimitiveType.Cylinder, root, "Rabo", new Vector3(0, .23f, 0), new Vector3(.04f, .07f, .04f), EscapeKit.Paint(new Color(.4f, .25f, .1f)), false);
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Hoja", new Vector3(.08f, .26f, 0), new Vector3(.14f, .04f, .08f), EscapeKit.Paint(new Color(.3f, .7f, .25f)), false);
                break;
            case Kind.Orange:
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Naranja", Vector3.zero, Vector3.one * .44f, EscapeKit.Paint(new Color(1f, .55f, .1f), .35f), false);
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Hoja", new Vector3(0, .22f, 0), new Vector3(.14f, .05f, .1f), EscapeKit.Paint(new Color(.3f, .7f, .25f)), false);
                break;
            case Kind.Banana:
                for (int i = -2; i <= 2; i++)
                    EscapeKit.Shape(PrimitiveType.Sphere, root, "Plátano", new Vector3(i * .1f, -Mathf.Abs(i) * .05f + .05f, 0), new Vector3(.16f, .14f, .14f), EscapeKit.Paint(new Color(1f, .88f, .25f), .35f), false);
                break;
            case Kind.Watermelon:
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Sandía", Vector3.zero, new Vector3(.62f, .5f, .5f), EscapeKit.Paint(new Color(.2f, .55f, .2f), .5f), false);
                for (int i = -1; i <= 1; i++) EscapeKit.Shape(PrimitiveType.Sphere, root, "Raya", new Vector3(i * .17f, 0, 0), new Vector3(.06f, .51f, .51f), EscapeKit.Paint(new Color(.12f, .38f, .12f), .5f), false);
                break;
            case Kind.GoldPineapple:
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Piña", Vector3.zero, new Vector3(.4f, .55f, .4f), EscapeKit.Glow(new Color(1f, .8f, .2f), 1.2f), false);
                for (int i = 0; i < 5; i++) EscapeKit.Block(root, "Penacho", new Vector3(0, .36f, 0), new Vector3(.06f, .3f, .14f), EscapeKit.Glow(new Color(.5f, 1f, .4f), 1f), .01f, false, new Vector3(25f, i * 72f, 0));
                EscapeKit.PointLight(root, Vector3.zero, new Color(1f, .85f, .3f), 4f, 2f);
                break;
            case Kind.Boot:
                var leather = EscapeKit.Paint(new Color(.38f, .24f, .14f), .2f);
                EscapeKit.Block(root, "Caña", new Vector3(-.08f, .14f, 0), new Vector3(.22f, .42f, .24f), leather, .05f, false);
                EscapeKit.Block(root, "Pie", new Vector3(.08f, -.06f, 0), new Vector3(.48f, .18f, .26f), leather, .06f, false);
                EscapeKit.Block(root, "Suela", new Vector3(.08f, -.16f, 0), new Vector3(.5f, .05f, .28f), EscapeKit.Paint(new Color(.15f, .12f, .1f)), .02f, false);
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Mosca", new Vector3(.1f, .4f, .1f), Vector3.one * .05f, EscapeKit.Paint(Color.black), false);
                break;
            default:
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Bomba", Vector3.zero, Vector3.one * .46f, EscapeKit.Paint(new Color(.12f, .12f, .15f), .7f), false);
                EscapeKit.Shape(PrimitiveType.Cylinder, root, "Mecha", new Vector3(.05f, .27f, 0), new Vector3(.05f, .08f, .05f), EscapeKit.Paint(new Color(.85f, .8f, .7f)), false, new Vector3(0, 0, -20f));
                EscapeKit.Shape(PrimitiveType.Sphere, root, "Chispa", new Vector3(.08f, .36f, 0), Vector3.one * .12f, EscapeKit.Glow(new Color(1f, .7f, .2f), 4f), false);
                break;
        }
        return root;
    }
}
