using UnityEngine;
using UnityEngine.Rendering;

// Shared visual resources for Carreras Locas: shaders from Resources/RaceFX, procedural textures,
// particle-system factory and one-shot effects. Presentation only: nothing here has a collider.
public static class CrazyRaceFX
{
    static Material additive, alpha, rainbow, stars;
    static Texture2D dot, spark, star;
    static Shader Find(string name, string fallback)
    {
        var s = Shader.Find(name);
        return s != null ? s : Shader.Find(fallback);
    }
    static Texture2D Make(string name, System.Func<float, float, float> alphaAt)
    {
        const int n = 64; var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            px[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(alphaAt((x + .5f) / n * 2 - 1, (y + .5f) / n * 2 - 1)));
        t.SetPixels(px); t.Apply(false, true); return t;
    }
    public static Texture2D Dot => dot != null ? dot : dot = Make("FX punto", (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return Mathf.Pow(1 - Mathf.Clamp01(r), 1.8f); });
    public static Texture2D Spark => spark != null ? spark : spark = Make("FX chispa", (x, y) => (1 - Mathf.Clamp01(Mathf.Abs(x) * 3.2f)) * (1 - Mathf.Clamp01(Mathf.Abs(y))));
    public static Texture2D Star => star != null ? star : star = Make("FX estrella", (x, y) =>
    {
        float a = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
        float edge = .55f + .35f * Mathf.Cos(a * 5);
        return Mathf.Clamp01((edge - r) * 9);
    });
    static Material Particle(string name, Texture tex, bool add, float boost)
    {
        var m = new Material(Find("RaceFX/Particle", "Universal Render Pipeline/Particles/Unlit")) { name = name, mainTexture = tex };
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(add ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_Boost", boost);
        m.renderQueue = 3100; return m;
    }
    public static Material Additive => additive != null ? additive : additive = Particle("FX aditivo", Spark, true, 1.6f);
    public static Material Smoke => alpha != null ? alpha : alpha = Particle("FX humo", Dot, false, 1);
    public static Material Glow => rainbow != null ? rainbow : rainbow = Particle("FX brillo", Dot, true, 1.3f);
    public static Material Stars => stars != null ? stars : stars = Particle("FX estrellas", Star, true, 1.4f);
    public static Material Fresnel(Color color, bool rainbowHue, float bodyAlpha, float rim)
    {
        var m = new Material(Find("RaceFX/Fresnel", "Universal Render Pipeline/Unlit")) { name = "FX fresnel" };
        m.SetColor("_Color", color); m.SetFloat("_Rainbow", rainbowHue ? 1 : 0); m.SetFloat("_Alpha", bodyAlpha); m.SetFloat("_Rim", rim);
        m.color = color; return m;
    }

    public static ParticleSystem System(Transform parent, string name, Vector3 localPos, Material mat, float lifetime, float speed, float size, int max, bool stretch = false)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.duration = 1; main.loop = true; main.playOnAwake = false;
        main.startLifetime = lifetime; main.startSpeed = speed; main.startSize = size; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.rateOverTime = 0;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 18; shape.radius = .05f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        col.color = g;
        var sizeLife = ps.sizeOverLifetime; sizeLife.enabled = true; sizeLife.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, stretch ? .2f : 1.8f));
        var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .06f; r.lengthScale = 1.5f; }
        ps.Play();
        return ps;
    }
    public static void Emit(ParticleSystem ps, int count, Color color, float speed, float size, float life, Vector3? velocity = null, Vector3? position = null)
    {
        var p = new ParticleSystem.EmitParams { startColor = color, startSize = size, startLifetime = life, applyShapeToPosition = position == null };
        if (velocity != null) p.velocity = velocity.Value;
        if (position != null) p.position = position.Value;
        for (int i = 0; i < count; i++)
        {
            if (velocity == null) p.velocity = Random.onUnitSphere * speed + Vector3.up * speed * .5f;
            ps.Emit(p, 1);
        }
    }

    // A temporary world-space burst (pickups, item boxes, pads, finish). Self-destroys.
    static ParticleSystem burst;
    public static ParticleSystem WorldBurst
    {
        get
        {
            if (burst != null) return burst;
            var root = new GameObject("FX ráfagas");
            burst = System(root.transform, "Destellos", Vector3.zero, Additive, .6f, 6, .4f, 600, true);
            var g = burst.GetComponent<ParticleSystemRenderer>(); g.velocityScale = .08f;
            var grav = burst.main; grav.gravityModifier = 1.2f;
            return burst;
        }
    }
    public static void Shards(Vector3 at, int count, bool rainbowColors, Color tint, float speed = 8)
    {
        var ps = WorldBurst;
        for (int i = 0; i < count; i++)
            Emit(ps, 1, rainbowColors ? Color.HSVToRGB(Random.value, .8f, 1) : tint, 0, Random.Range(.25f, .5f), Random.Range(.4f, .8f), Random.onUnitSphere * speed + Vector3.up * speed * .6f, at);
    }

    // Jagged lightning bolt from the sky that fades out.
    public static void Lightning(Vector3 ground)
    {
        var go = new GameObject("FX rayo");
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = Glow; line.useWorldSpace = true; line.positionCount = 9; line.widthMultiplier = .55f;
        line.startColor = new Color(1, 1, .7f); line.endColor = new Color(.7f, .85f, 1);
        for (int i = 0; i < 9; i++)
        {
            float t = i / 8f;
            line.SetPosition(i, ground + Vector3.up * (18 * (1 - t)) + (i == 0 || i == 8 ? Vector3.zero : new Vector3(Random.Range(-1.2f, 1.2f), 0, Random.Range(-1.2f, 1.2f))));
        }
        go.AddComponent<CrazyRaceFade>().Init(.45f);
        Shards(ground + Vector3.up * .5f, 18, false, new Color(1, .95f, .4f), 7);
    }
}

// Fades a LineRenderer and destroys its object.
public sealed class CrazyRaceFade : MonoBehaviour
{
    float life, start; LineRenderer line; Color a, b;
    public void Init(float seconds) { life = seconds; start = Time.time; line = GetComponent<LineRenderer>(); a = line.startColor; b = line.endColor; }
    void Update()
    {
        float k = 1 - (Time.time - start) / life;
        if (k <= 0) { Destroy(gameObject); return; }
        a.a = b.a = k * (Mathf.Repeat(Time.time * 30, 1) > .3f ? 1 : .4f); line.startColor = a; line.endColor = b;
    }
}
