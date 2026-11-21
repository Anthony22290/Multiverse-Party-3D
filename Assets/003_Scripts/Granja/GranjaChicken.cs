using UnityEngine;

/// <summary>
/// One chicken of Gallinas Locas. The host moves it (CharacterController: it bumps into fences, bales and players);
/// other computers only show it where the host says. Normal = 1 point, golden = 5 (faster and more skittish),
/// trap = looks normal (its eyes flash red now and then) and, once inside a pen, ticks for a moment and blows up
/// every chicken in that pen.
/// </summary>
public sealed class GranjaChicken : MonoBehaviour
{
    public enum Kind : byte { Normal, Gold, Trap }
    public enum State : byte { Free, Carried, Penned, Ticking, Dead }

    public int Id; public Kind Type; public State Mode; public int Pen = -1; public int Carrier = -1;   // carrier = slot
    public float Fuse; public Vector3 Vel; public Vector3 WanderDir; public float NextWander, Panic;
    public int Value => Type == Kind.Gold ? 5 : 1;
    public CharacterController Cc { get; private set; }

    Transform body, head, wingL, wingR, legL, legR; Renderer bodyR, eyeL, eyeR; Material bodyMat, eyeMat, eyeRed;
    Vector3 netPos; float netYaw; bool hasNet; float nextBlink, blinkUntil, peckT;
    Color baseColor;

    public static GranjaChicken Create(int id, Kind kind, Vector3 pos, bool host)
    {
        var go = new GameObject((kind == Kind.Gold ? "Gallina dorada " : "Gallina ") + id);
        go.transform.position = pos;
        var c = go.AddComponent<GranjaChicken>(); c.Id = id; c.Type = kind; c.netPos = pos;
        if (host)
        {
            c.Cc = go.AddComponent<CharacterController>(); c.Cc.height = .55f; c.Cc.radius = .24f; c.Cc.center = Vector3.up * .3f; c.Cc.stepOffset = .15f; c.Cc.slopeLimit = 45f; c.Cc.skinWidth = .03f;
        }
        c.Build();
        return c;
    }

    void Build()
    {
        float scale = Type == Kind.Gold ? 1.15f : 1f;
        var root = new GameObject("Cuerpo").transform; root.SetParent(transform, false); root.localScale = Vector3.one * scale; body = root;
        baseColor = Type == Kind.Gold ? new Color(1f, .8f, .2f) : new Color(.97f, .95f, .9f);
        bodyMat = Type == Kind.Gold ? EscapeKit.Glow(baseColor, .6f) : EscapeKit.Paint(baseColor, .25f);
        var comb = EscapeKit.Paint(Type == Kind.Trap ? new Color(.85f, .08f, .08f) : new Color(.95f, .2f, .2f), .3f);
        var beak = EscapeKit.Paint(new Color(1f, .62f, .15f), .3f);
        bodyR = EscapeKit.Shape(PrimitiveType.Sphere, root, "Cuerpo", new Vector3(0, .32f, -.02f), new Vector3(.42f, .38f, .5f), bodyMat, false).GetComponent<Renderer>();
        EscapeKit.Shape(PrimitiveType.Sphere, root, "Cola", new Vector3(0, .45f, -.25f), new Vector3(.22f, .28f, .14f), bodyMat, false, new Vector3(-30f, 0, 0));
        head = EscapeKit.Shape(PrimitiveType.Sphere, root, "Cabeza", new Vector3(0, .62f, .16f), Vector3.one * .24f, bodyMat, false).transform;
        EscapeKit.Shape(PrimitiveType.Cube, head, "Cresta", new Vector3(0, .55f, 0), new Vector3(.18f, .45f, .5f), comb, false);
        EscapeKit.Shape(PrimitiveType.Cube, head, "Pico", new Vector3(0, -.05f, .55f), new Vector3(.3f, .22f, .35f), beak, false, new Vector3(10f, 0, 0));
        EscapeKit.Shape(PrimitiveType.Sphere, head, "Barbilla", new Vector3(0, -.35f, .35f), new Vector3(.18f, .28f, .15f), comb, false);
        eyeMat = EscapeKit.Paint(new Color(.08f, .08f, .1f), .6f); eyeRed = EscapeKit.Glow(new Color(1f, .1f, .05f), 3f);
        eyeL = EscapeKit.Shape(PrimitiveType.Sphere, head, "Ojo", new Vector3(-.36f, .12f, .25f), Vector3.one * .16f, eyeMat, false).GetComponent<Renderer>();
        eyeR = EscapeKit.Shape(PrimitiveType.Sphere, head, "Ojo", new Vector3(.36f, .12f, .25f), Vector3.one * .16f, eyeMat, false).GetComponent<Renderer>();
        wingL = EscapeKit.Shape(PrimitiveType.Sphere, root, "Ala", new Vector3(-.2f, .34f, -.02f), new Vector3(.08f, .22f, .32f), bodyMat, false).transform;
        wingR = EscapeKit.Shape(PrimitiveType.Sphere, root, "Ala", new Vector3(.2f, .34f, -.02f), new Vector3(.08f, .22f, .32f), bodyMat, false).transform;
        var legMat = EscapeKit.Paint(new Color(1f, .62f, .15f), .2f);
        legL = EscapeKit.Shape(PrimitiveType.Cube, root, "Pata", new Vector3(-.08f, .08f, 0), new Vector3(.04f, .18f, .04f), legMat, false).transform;
        legR = EscapeKit.Shape(PrimitiveType.Cube, root, "Pata", new Vector3(.08f, .08f, 0), new Vector3(.04f, .18f, .04f), legMat, false).transform;
        if (Type == Kind.Gold) EscapeKit.PointLight(root, new Vector3(0, .6f, 0), new Color(1f, .85f, .3f), 3.5f, 1.4f);
        nextBlink = Time.time + Random.Range(1f, 4f);
    }

    /// <summary>Client: where the host says it is.</summary>
    public void SetNet(Vector3 pos, float yaw, State mode, int pen, int carrier)
    {
        if (!hasNet || (pos - transform.position).sqrMagnitude > 25f) transform.position = pos;
        netPos = pos; netYaw = yaw; hasNet = true; Mode = mode; Pen = pen; Carrier = carrier;
    }

    public bool IsClientCopy => Cc == null;

    void Update()
    {
        float t = Time.time;
        if (IsClientCopy && hasNet)
        {
            Vector3 before = transform.position;
            transform.position = Mode == State.Carried ? Vector3.Lerp(transform.position, netPos, Time.deltaTime * 20f) : Vector3.Lerp(transform.position, netPos, Time.deltaTime * 10f);
            Vel = (transform.position - before) / Mathf.Max(Time.deltaTime, .001f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netYaw, 0), Time.deltaTime * 10f);
        }
        // Animation.
        float speed = new Vector2(Vel.x, Vel.z).magnitude;
        float flap = Mode == State.Carried ? 1f : Mathf.Clamp01((speed - 1.5f) / 2f);
        float wing = Mathf.Sin(t * 30f) * 70f * flap;
        wingL.localRotation = Quaternion.Euler(0, 0, -wing); wingR.localRotation = Quaternion.Euler(0, 0, wing);
        float step = speed > .2f ? Mathf.Sin(t * (8f + speed * 4f)) * 35f : 0f;
        legL.localRotation = Quaternion.Euler(step, 0, 0); legR.localRotation = Quaternion.Euler(-step, 0, 0);
        if (speed < .3f && Mode != State.Carried) { peckT += Time.deltaTime; head.localRotation = Quaternion.Euler(Mathf.Max(0, Mathf.Sin(peckT * 3f)) * 55f, 0, 0); }
        else head.localRotation = Quaternion.identity;
        body.localPosition = Vector3.up * (speed > .2f ? Mathf.Abs(Mathf.Sin(t * 14f)) * .05f : 0f);
        // Trap tell: the eyes flash red for an instant every few seconds.
        if (Type == Kind.Trap && t > nextBlink) { blinkUntil = t + .18f; nextBlink = t + Random.Range(2.5f, 4.5f); }
        bool red = Type == Kind.Trap && (t < blinkUntil || Mode == State.Ticking);
        var em = red ? eyeRed : eyeMat; if (eyeL.sharedMaterial != em) { eyeL.sharedMaterial = em; eyeR.sharedMaterial = em; }
        // Ticking: flashes red and swells.
        if (Mode == State.Ticking)
        {
            float pulse = .5f + .5f * Mathf.Sin(t * 26f);
            bodyMat.color = Color.Lerp(baseColor, new Color(1f, .15f, .1f), pulse);
            body.localScale = Vector3.one * (1f + pulse * .15f);
        }
        else if (Type != Kind.Gold) { bodyMat.color = baseColor; body.localScale = Vector3.one; }
    }
}
