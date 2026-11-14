using UnityEngine;

// Per-kart particles and body animation (presentation only: no forces, colliders or race state).
// Drift hop, tiered drift sparks, boost flames, exhaust/drift smoke, off-road dust, landing squash,
// spin-out stars when hit, shrink after lightning, shield bubble, countdown revving and finish hops.
public sealed class CrazyKartFX : MonoBehaviour
{
    CrazyKartController kart;
    CrazyKartDirector director;
    Transform model;
    Vector3 restPos, restScale; Quaternion restRot;
    ParticleSystem sparksL, sparksR, flame, smoke, dust, stars;
    GameObject bubble; Material bubbleMat;
    float hopStart = -10, spinStart = -10, observedHit = -100, observedBoost = -100, observedStruck = -100;
    float driftYaw, squash, squashVel, stretch, airTime, shrink = 1, phase;
    bool wasDrifting, wasGrounded = true; int lastTier;
    static readonly Color[] TierColors = { new Color(1, 1, 1, .7f), new Color(.25f, .7f, 1), new Color(1, .55f, .1f), new Color(1, .3f, .85f) };
    public static Color TierColor(int tier) => TierColors[Mathf.Clamp(tier, 0, 3)];

    void Start()
    {
        kart = GetComponent<CrazyKartController>(); director = FindAnyObjectByType<CrazyKartDirector>();
        model = transform.Find("Vehicle_Sports");
        if (model != null) { restPos = model.localPosition; restRot = model.localRotation; restScale = model.localScale; }
        phase = Random.value * 10;
        sparksL = Sparks("Chispas derrape L", new Vector3(-.62f, -.3f, -1.05f));
        sparksR = Sparks("Chispas derrape R", new Vector3(.62f, -.3f, -1.05f));
        flame = CrazyRaceFX.System(transform, "Llama turbo", new Vector3(0, -.12f, -1.35f), CrazyRaceFX.Glow, .16f, 7, .55f, 120);
        flame.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var fs = flame.shape; fs.angle = 8;
        smoke = CrazyRaceFX.System(transform, "Humo", new Vector3(0, -.15f, -1.3f), CrazyRaceFX.Smoke, .8f, 1.3f, .35f, 120);
        smoke.transform.localRotation = Quaternion.Euler(-30, 180, 0);
        var sm = smoke.main; sm.startColor = new Color(.85f, .85f, .9f, .4f);
        dust = CrazyRaceFX.System(transform, "Polvo", new Vector3(0, -.35f, -.8f), CrazyRaceFX.Smoke, .7f, 2.2f, .6f, 160);
        dust.transform.localRotation = Quaternion.Euler(-35, 180, 0);
        var ds = dust.shape; ds.shapeType = ParticleSystemShapeType.Box; ds.scale = new Vector3(1.4f, .1f, .4f);
        stars = CrazyRaceFX.System(transform, "Estrellas aturdido", new Vector3(0, 1.1f, 0), CrazyRaceFX.Stars, .9f, 1.2f, .32f, 60);
        stars.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ss = stars.shape; ss.shapeType = ParticleSystemShapeType.Circle; ss.radius = .7f;
        var sMain = stars.main; sMain.startColor = new Color(1, .9f, .3f); sMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        var rot = stars.rotationOverLifetime; rot.enabled = true; rot.z = 4;

        bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere); bubble.name = "Burbuja escudo";
        DestroyImmediate(bubble.GetComponent<Collider>());
        bubble.transform.SetParent(transform, false); bubble.transform.localPosition = new Vector3(0, .2f, -.05f); bubble.transform.localScale = new Vector3(2.5f, 1.8f, 3.7f);
        bubbleMat = CrazyRaceFX.Fresnel(new Color(.3f, .95f, 1, 1), false, .08f, 2.4f);
        var br = bubble.GetComponent<Renderer>(); br.sharedMaterial = bubbleMat; br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        bubble.SetActive(false);
    }
    ParticleSystem Sparks(string name, Vector3 pos)
    {
        var ps = CrazyRaceFX.System(transform, name, pos, CrazyRaceFX.Additive, .28f, 5.5f, .16f, 150, true);
        ps.transform.localRotation = Quaternion.Euler(-28, 180, 0);
        var sh = ps.shape; sh.angle = 35;
        var main = ps.main; main.gravityModifier = 1.6f; main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.5f);
        return ps;
    }
    static void Rate(ParticleSystem ps, float r) { var e = ps.emission; e.rateOverTime = r; }
    static void Tint(ParticleSystem ps, Color c) { var m = ps.main; m.startColor = c; }

    void LateUpdate()
    {
        if (kart == null) return;
        float dt = Time.deltaTime; float now = Time.time;
        bool racing = !kart.HasFinished;
        // --- drift: hop on entry, sparks by tier, burst when a tier is reached
        bool drifting = kart.Drifting;
        if (drifting && !wasDrifting) hopStart = now;
        wasDrifting = drifting;
        int tier = kart.DriftTier;
        float sparkRate = drifting ? new[] { 10f, 45f, 60f, 80f }[tier] : 0;
        Rate(sparksL, sparkRate); Rate(sparksR, sparkRate); Tint(sparksL, TierColor(tier)); Tint(sparksR, TierColor(tier));
        if (drifting && tier > lastTier && tier > 0)
            foreach (var ps in new[] { sparksL, sparksR }) CrazyRaceFX.Emit(ps, 12, TierColor(tier), 0, .22f, .35f);
        lastTier = drifting ? tier : 0;
        // --- boost flame and kick
        if (kart.LastBoostStart > observedBoost) { observedBoost = kart.LastBoostStart; stretch = 1; CrazyRaceFX.Emit(flame, 14, BoostColor(), 0, .7f, .25f); }
        Rate(flame, kart.Boosting && racing ? 70 : 0); Tint(flame, BoostColor());
        // --- smoke and dust
        bool countdown = director != null && !director.Playing && !director.Finished;
        float smokeRate = !racing ? 0 : kart.Revving ? 26 : countdown ? 4 : drifting ? 14 : kart.Speed < 8 ? 6 : 2;
        Rate(smoke, smokeRate);
        bool offroad = director != null && kart.Grounded && kart.Speed > 4 && director.DistanceToRoad(transform.position) > director.roadHalfWidth;
        Rate(dust, offroad ? 32 : 0); Tint(dust, new Color(.88f, .78f, .56f, .65f));
        // --- landing squash
        if (!kart.Grounded) airTime += dt;
        if (kart.Grounded && !wasGrounded && airTime > .22f)
        {
            squashVel -= Mathf.Clamp(airTime * 6, 1.5f, 5);
            CrazyRaceFX.Emit(dust, 14, new Color(.9f, .85f, .7f, .7f), 0, .7f, .5f);
        }
        if (kart.Grounded) airTime = 0;
        wasGrounded = kart.Grounded;
        for (float left = Mathf.Min(dt, .1f); left > 0; left -= 1 / 120f) { float h = Mathf.Min(left, 1 / 120f); squashVel += (-squash * 160 - squashVel * 11) * h; squash += squashVel * h; }
        if (float.IsNaN(squash)) { squash = squashVel = 0; }
        stretch = Mathf.MoveTowards(stretch, 0, dt * 3.5f);
        // --- hit: spin-out with stars
        if (kart.LastHitTime > observedHit) { observedHit = kart.LastHitTime; spinStart = now; CrazyRaceFX.Emit(stars, 8, new Color(1, .9f, .3f), 0, .4f, .9f); }
        Rate(stars, kart.Stunned ? 9 : 0);
        if (kart.LastStruckTime > observedStruck) { observedStruck = kart.LastStruckTime; CrazyRaceFX.Lightning(transform.position); }
        shrink = Mathf.MoveTowards(shrink, kart.Shrunk ? .6f : 1, dt * 2.5f);
        // --- shield bubble (blinks in its last second)
        bubble.SetActive(kart.Shielded);
        if (kart.Shielded)
        {
            float wobble = 1 + Mathf.Sin(now * 7) * .03f;
            bubble.transform.localScale = new Vector3(2.5f, 1.8f, 3.7f) * wobble * shrink;
        }
        if (model == null) return;
        // --- compose body pose
        driftYaw = Mathf.Lerp(driftYaw, drifting ? kart.Steering * 22 : 0, 1 - Mathf.Exp(-10 * dt));
        float roll = -kart.Steering * Mathf.Clamp01(kart.Speed / 12) * 4;
        float hop = now - hopStart < .3f ? Mathf.Sin((now - hopStart) / .3f * Mathf.PI) * .42f : 0;
        float spinT = (now - spinStart) / .95f, spin = spinT < 1 ? (1 - Mathf.Pow(1 - spinT, 3)) * 720 : 0;
        float rumble = countdown ? Mathf.Sin(now * 48 + phase) * (kart.Revving ? .03f : .01f) : racing && kart.Speed < 2 ? Mathf.Sin(now * 40 + phase) * .006f : 0;
        float celebrate = kart.HasFinished ? Mathf.Abs(Mathf.Sin((now - phase) * 5.5f)) * .3f * Mathf.Clamp01(4 - (director != null ? director.RoundTime - kart.FinishTime : 0)) : 0;
        model.localPosition = restPos + Vector3.up * (hop + rumble + celebrate);
        model.localRotation = restRot * Quaternion.Euler(0, driftYaw + spin, roll);
        float sq = Mathf.Clamp(squash, -.6f, .6f);
        model.localScale = Vector3.Scale(restScale, new Vector3(1 - sq * .25f, 1 + sq * .4f, (1 - sq * .25f) * (1 + stretch * .12f))) * shrink;
    }
    Color BoostColor() => kart.LastBoostTier >= 1 ? TierColor(kart.LastBoostTier) : new Color(1, .55f, .12f);
    void OnDestroy() { if (bubbleMat != null) Destroy(bubbleMat); }
}
