using UnityEngine;

/// <summary>
/// Particle effects for Arena del Caos, driven by sound events: dust, hit stars, water splashes,
/// cannon fire and smoke, confetti. One world-space particle system per effect type; each effect
/// moves its emitter and emits a burst, so particles already in flight are unaffected.
/// Purely visual: no colliders, no forces.
/// </summary>
public sealed class PirateArenaFx : MonoBehaviour
{
    static PirateArenaFx instance;
    ParticleSystem dust, stars, splash, foam, fire, smoke, debris, confetti, spray;
    Material soft, additive;

    void Awake()
    {
        instance = this;
        soft = Resources.Load<Material>("ArenaFx/Particula suave");
        additive = Resources.Load<Material>("ArenaFx/Particula brillante");
        if (soft == null) soft = Fallback(false);
        if (additive == null) additive = Fallback(true);
        dust = Make("Polvo", soft, 60, .5f, 1.0f, .8f, 2.4f, .35f, .75f, new Color(.86f, .76f, .6f, .75f), new Color(.7f, .6f, .48f, .6f), -.05f, ParticleSystemShapeType.Hemisphere, .25f, true);
        stars = Make("Estrellas de golpe", additive, 80, .3f, .5f, 3.5f, 7f, .1f, .22f, new Color(1f, .95f, .55f), new Color(1f, .7f, .25f), .3f, ParticleSystemShapeType.Sphere, .15f, false);
        splash = Make("Salpicadura", soft, 200, .7f, 1.3f, 4f, 8.5f, .18f, .42f, new Color(.92f, .98f, 1f, .95f), new Color(.6f, .85f, .95f, .85f), 1.25f, ParticleSystemShapeType.Cone, .5f, false, 22f);
        foam = Make("Espuma", soft, 120, .9f, 1.6f, 1.2f, 2.6f, .35f, .7f, new Color(1f, 1f, 1f, .8f), new Color(.85f, .95f, 1f, .7f), 0f, ParticleSystemShapeType.Circle, .6f, true);
        fire = Make("Fogonazo", additive, 120, .25f, .55f, 2f, 6.5f, .7f, 1.5f, new Color(1f, .85f, .4f), new Color(1f, .45f, .12f), -.2f, ParticleSystemShapeType.Sphere, .4f, true);
        smoke = Make("Humo", soft, 160, 1.4f, 2.6f, .8f, 2.8f, 1.1f, 2.4f, new Color(.42f, .4f, .4f, .75f), new Color(.65f, .63f, .6f, .6f), -.12f, ParticleSystemShapeType.Sphere, .6f, true);
        debris = Make("Astillas", soft, 120, .9f, 1.5f, 5f, 10f, .1f, .2f, new Color(.55f, .36f, .2f), new Color(.35f, .22f, .12f), 1.6f, ParticleSystemShapeType.Hemisphere, .4f, false);
        spray = Make("Rocío", soft, 120, .5f, .9f, 3f, 6f, .12f, .3f, new Color(.95f, 1f, 1f, .9f), new Color(.7f, .9f, 1f, .8f), 1f, ParticleSystemShapeType.Hemisphere, .5f, false);
        confetti = Make("Confeti", soft, 500, 2.6f, 3.8f, 6f, 11f, .2f, .34f, Color.white, Color.white, .45f, ParticleSystemShapeType.Cone, 1.4f, false, 35f);
        var cm = confetti.main;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, .3f, .6f), 0f), new GradientColorKey(new Color(1f, .85f, .2f), .33f), new GradientColorKey(new Color(.25f, .85f, .95f), .66f), new GradientColorKey(new Color(.55f, .95f, .35f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        cm.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        var rot = confetti.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        var noiseModule = confetti.noise; noiseModule.enabled = true; noiseModule.strength = .8f; noiseModule.frequency = .6f;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    static Material Fallback(bool add)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        var m = new Material(shader) { name = add ? "Partícula brillante (runtime)" : "Partícula suave (runtime)" };
        ConfigureParticleMaterial(m, add, null);
        return m;
    }

    /// <summary>Transparent URP particle material (alpha-blended or additive). Shared with the editor setup.</summary>
    public static void ConfigureParticleMaterial(Material m, bool additive, Texture texture)
    {
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        if (texture != null) { m.SetTexture("_BaseMap", texture); m.mainTexture = texture; }
        m.SetColor("_BaseColor", Color.white);
    }

    ParticleSystem Make(string label, Material mat, int max, float life0, float life1, float speed0, float speed1, float size0, float size1,
        Color c0, Color c1, float gravity, ParticleSystemShapeType shapeType, float radius, bool grow, float coneAngle = 25f)
    {
        var go = new GameObject("FX · " + label); go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life0, life1);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed0, speed1);
        main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
        main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
        main.gravityModifier = gravity;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0f;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = shapeType; shape.radius = radius;
        if (shapeType == ParticleSystemShapeType.Cone || shapeType == ParticleSystemShapeType.Hemisphere || shapeType == ParticleSystemShapeType.Circle)
            shape.rotation = new Vector3(-90f, 0f, 0f);
        if (shapeType == ParticleSystemShapeType.Cone) shape.angle = coneAngle;
        var col = ps.colorOverLifetime; col.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.85f, .6f), new GradientAlphaKey(0f, 1f) });
        col.color = fade;
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, grow ? AnimationCurve.Linear(0, .6f, 1, 1.6f) : AnimationCurve.EaseInOut(0, 1f, 1, .3f));
        var limit = ps.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = grow ? .12f : .04f; limit.limit = 30f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.sortingFudge = grow ? 10f : 0f;
        return ps;
    }

    static void Burst(ParticleSystem ps, Vector3 at, int count)
    {
        if (ps == null || count <= 0) return;
        ps.transform.position = at;
        ps.Emit(count);
    }

    // ------------------------------------------------------------------ public effects
    public static void Dust(Vector3 at, float strength = 1f) { if (instance) Burst(instance.dust, at + Vector3.up * .05f, Mathf.RoundToInt(8 * strength)); }
    public static void HitStars(Vector3 at) { if (instance) { Burst(instance.stars, at, 14); Burst(instance.dust, at, 4); } }
    public static void Splash(Vector3 at, float strength = 1f)
    {
        if (!instance) return;
        at.y = .05f;
        Burst(instance.splash, at, Mathf.RoundToInt(42 * strength));
        Burst(instance.foam, at, Mathf.RoundToInt(18 * strength));
    }
    public static void Spray(Vector3 at) { if (instance) { at.y = Mathf.Max(at.y, .05f); Burst(instance.spray, at, 26); Burst(instance.foam, new Vector3(at.x, .05f, at.z), 8); } }
    public static void Muzzle(Vector3 at) { if (instance) { Burst(instance.fire, at, 10); Burst(instance.smoke, at, 10); } }
    public static void Explosion(Vector3 at)
    {
        if (!instance) return;
        Burst(instance.fire, at + Vector3.up * .3f, 26); Burst(instance.smoke, at + Vector3.up * .4f, 18);
        Burst(instance.debris, at + Vector3.up * .2f, 16); Burst(instance.dust, at, 14);
    }
    public static void Confetti(Vector3 at) { if (instance) Burst(instance.confetti, at + Vector3.up * .8f, 260); }

    /// <summary>Maps sound events to visuals and camera shake (local and replicated events alike).</summary>
    public static void OnSound(PirateArenaAudio.Sound sound, Vector3 at, float volume)
    {
        switch (sound)
        {
            case PirateArenaAudio.Sound.Land: Dust(at, .8f + volume * .4f); break;
            case PirateArenaAudio.Sound.Impact: Dust(at, 1.2f); PirateArenaCamera.Shake(.18f * volume, at); break;
            case PirateArenaAudio.Sound.Punch: HitStars(at); PirateArenaCamera.Shake(.28f, at); break;
            case PirateArenaAudio.Sound.Splash: Splash(at, Mathf.Clamp(volume, .5f, 1.3f)); PirateArenaCamera.Shake(.12f, at); break;
            case PirateArenaAudio.Sound.CannonShot: Muzzle(at + Vector3.up * .6f); PirateArenaCamera.Shake(.35f, at); break;
            case PirateArenaAudio.Sound.CannonBlast: Explosion(at); PirateArenaCamera.Shake(.85f, at); break;
            case PirateArenaAudio.Sound.SharkLeap: Spray(at); break;
            case PirateArenaAudio.Sound.WoodHit: Dust(at, .5f); break;
        }
    }
}
