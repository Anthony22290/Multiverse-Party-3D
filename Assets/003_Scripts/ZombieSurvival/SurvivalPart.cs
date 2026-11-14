using UnityEngine;

/// <summary>A truck part dropped by a marked zombie or a boss: a light beam while on the ground, carried above the head.</summary>
public sealed class SurvivalPart : MonoBehaviour
{
    public int Index { get; private set; }
    public string Label { get; private set; }
    public SurvivalPlayer Carrier { get; private set; }
    public float DroppedAt { get; private set; }
    public float PickableAt { get; private set; }
    public bool OnGround => Carrier == null;

    SurvivalDirector director;
    Transform model, beam;
    TextMesh label;
    Light glow;
    Vector3 groundPos;

    static readonly Color Gold = new Color(1f, .82f, .25f);

    public void Initialize(SurvivalDirector d, int index)
    {
        director = d; Index = index; Label = SurvivalDirector.PartNames[index];
        model = new GameObject("Modelo").transform;
        model.SetParent(transform, false);
        BuildModel(index);
        beam = EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Haz de luz", Vector3.up * 4f, new Vector3(.22f, 4f, .22f), EscapeKit.Glow(Gold, 1.4f), false).transform;
        label = EscapeKit.Text(transform, Label.ToUpperInvariant(), Vector3.up * 1.5f, Vector3.zero, .03f, Gold);
        glow = EscapeKit.PointLight(transform, Vector3.up * .8f, Gold, 6f, 2.5f);
    }

    void BuildModel(int i)
    {
        Material dark = EscapeKit.Paint(new Color(.18f, .19f, .22f)), metal = EscapeKit.Paint(new Color(.7f, .72f, .76f), .5f);
        Material red = EscapeKit.Paint(new Color(.92f, .25f, .2f)), yellow = EscapeKit.Glow(new Color(1f, .9f, .45f), 1.4f), gold = EscapeKit.Paint(Gold, .6f);
        switch (i)
        {
            case 0:
            case 1:
                if (SurvivalKit.Model("Wheel", model, Vector3.zero, 0f, .8f, false, "Rueda", true) == null)
                    EscapeKit.Shape(PrimitiveType.Cylinder, model, "Rueda", Vector3.up * .38f, new Vector3(.75f, .14f, .75f), dark, false, new Vector3(0, 0, 90));
                break;
            case 2: // battery
                EscapeKit.Block(model, "Batería", Vector3.up * .25f, new Vector3(.62f, .45f, .4f), dark, .05f, false);
                EscapeKit.Block(model, "Tapa", Vector3.up * .5f, new Vector3(.64f, .06f, .42f), EscapeKit.Paint(new Color(.2f, .55f, .95f)), .02f, false);
                EscapeKit.Shape(PrimitiveType.Cylinder, model, "Borne +", new Vector3(-.18f, .58f, 0), new Vector3(.1f, .05f, .1f), red, false);
                EscapeKit.Shape(PrimitiveType.Cylinder, model, "Borne -", new Vector3(.18f, .58f, 0), new Vector3(.1f, .05f, .1f), dark, false);
                break;
            case 3: // spark plugs
                for (int k = -1; k <= 1; k++)
                {
                    EscapeKit.Shape(PrimitiveType.Cylinder, model, "Bujía", new Vector3(k * .2f, .25f, 0), new Vector3(.09f, .22f, .09f), EscapeKit.Paint(Color.white, .6f), false);
                    EscapeKit.Shape(PrimitiveType.Cylinder, model, "Rosca", new Vector3(k * .2f, .55f, 0), new Vector3(.07f, .09f, .07f), metal, false);
                }
                break;
            case 4: // radiator
                EscapeKit.Block(model, "Radiador", Vector3.up * .4f, new Vector3(.8f, .7f, .14f), metal, .03f, false);
                for (int k = 0; k < 5; k++) EscapeKit.Block(model, "Rejilla", new Vector3(0, .15f + k * .12f, -.08f), new Vector3(.72f, .03f, .03f), dark, .01f, false);
                break;
            case 5: // belt
                for (int k = 0; k < 10; k++)
                    EscapeKit.Block(model, "Tramo", Quaternion.Euler(0, 0, k * 36f) * Vector3.up * .3f + Vector3.up * .4f, new Vector3(.2f, .07f, .12f), dark, .02f, false, new Vector3(0, 0, k * 36f));
                break;
            case 6: // headlights
                EscapeKit.Block(model, "Soporte", Vector3.up * .3f, new Vector3(.9f, .12f, .14f), metal, .03f, false);
                EscapeKit.Shape(PrimitiveType.Sphere, model, "Faro", new Vector3(-.32f, .42f, 0), Vector3.one * .3f, yellow, false);
                EscapeKit.Shape(PrimitiveType.Sphere, model, "Faro", new Vector3(.32f, .42f, 0), Vector3.one * .3f, yellow, false);
                break;
            case 7: // steering wheel
                for (int k = 0; k < 12; k++)
                    EscapeKit.Block(model, "Aro", Quaternion.Euler(0, 0, k * 30f) * Vector3.up * .32f + Vector3.up * .45f, new Vector3(.18f, .07f, .07f), dark, .02f, false, new Vector3(0, 0, k * 30f + 90f));
                EscapeKit.Block(model, "Radio", Vector3.up * .45f, new Vector3(.6f, .06f, .05f), metal, .01f, false);
                EscapeKit.Shape(PrimitiveType.Cylinder, model, "Centro", Vector3.up * .45f, new Vector3(.18f, .05f, .18f), red, false, new Vector3(90, 0, 0));
                break;
            case 8: // jerry can
                EscapeKit.Block(model, "Bidón", Vector3.up * .32f, new Vector3(.45f, .6f, .22f), red, .05f, false);
                EscapeKit.Shape(PrimitiveType.Cylinder, model, "Tapón", new Vector3(.14f, .66f, 0), new Vector3(.09f, .06f, .09f), EscapeKit.Paint(Gold, .6f), false);
                EscapeKit.Block(model, "Asa", new Vector3(-.06f, .66f, 0), new Vector3(.22f, .06f, .08f), red, .02f, false);
                break;
            default: // ignition key
                EscapeKit.Shape(PrimitiveType.Cylinder, model, "Cabeza", Vector3.up * .55f, new Vector3(.3f, .04f, .3f), gold, false, new Vector3(90, 0, 0));
                EscapeKit.Block(model, "Hoja", Vector3.up * .25f, new Vector3(.08f, .4f, .04f), gold, .01f, false);
                EscapeKit.Block(model, "Diente", new Vector3(.06f, .12f, 0), new Vector3(.08f, .06f, .04f), gold, .01f, false);
                break;
        }
    }

    void Update()
    {
        if (model == null) return;
        if (Carrier == null)
        {
            model.localPosition = Vector3.up * (.25f + Mathf.Sin(Time.time * 3f) * .12f);
            model.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
            if (beam != null) beam.localScale = new Vector3(.22f + Mathf.Sin(Time.time * 5f) * .04f, 4f, .22f + Mathf.Sin(Time.time * 5f) * .04f);
        }
        else
        {
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, Time.deltaTime * 6f);
        }
        var cam = director != null && director.View != null ? director.View.transform : null;
        if (label != null && cam != null) label.transform.rotation = cam.rotation;
    }

    public void PickUp(SurvivalPlayer p)
    {
        Carrier = p;
        transform.SetParent(p.transform, false);
        transform.localPosition = Vector3.up * 2.05f;
        transform.localRotation = Quaternion.identity;
        beam.gameObject.SetActive(false);
        label.gameObject.SetActive(false);
        glow.range = 3f;
    }

    public void DropAt(Vector3 at)
    {
        Carrier = null;
        transform.SetParent(null, true);
        // The plaza is flat: keep parts on the ground and nudge them out of props toward the truck so they stay reachable.
        at.y = 0f;
        Vector3 toward = director.Level.TruckPos - at; toward.y = 0f;
        for (int i = 0; i < 14 && Blocked(at) && toward.sqrMagnitude > 1f; i++) at += toward.normalized * .8f;
        groundPos = at;
        transform.position = at;
        transform.rotation = Quaternion.identity;
        beam.gameObject.SetActive(true);
        label.gameObject.SetActive(true);
        glow.range = 6f;
        DroppedAt = Time.time;
        PickableAt = Time.time + .5f;
    }

    static bool Blocked(Vector3 at)
    {
        foreach (var c in Physics.OverlapSphere(at + Vector3.up * .6f, .5f, ~0, QueryTriggerInteraction.Ignore))
            if (c.GetComponent<CharacterController>() == null && c.GetComponentInParent<SurvivalPart>() == null) return true;
        return false;
    }

    public Vector3 GroundPosition => Carrier != null ? Carrier.transform.position : groundPos;
}
