using UnityEngine;

public sealed class CannonHazard : MonoBehaviour
{
    private PirateArenaDirector director;
    private float width, length, nextShot, phaseStarted;
    private Vector3 impact, start;
    private GameObject warning, ball;
    private int phase;
    public void Initialize(PirateArenaDirector arena, float deckY, float deckWidth, float deckLength)
    {
        director = arena; width = deckWidth; length = deckLength;
        director.Register(this);
    }
    public void StartRound() { nextShot = Time.time + Random.Range(4f, 7f); }
    public bool TryGetDanger(Vector3 position, out Vector3 away)
    {
        away = Vector3.ProjectOnPlane(position - impact, Vector3.up);
        if (phase == 0 || away.sqrMagnitude > Mathf.Pow(director.Settings.cannonRadius + 1f, 2f)) return false;
        away = away.sqrMagnitude > 0.1f ? away.normalized : Vector3.right;
        return true;
    }
    public bool NetworkDriven { get; set; }
    private void Update() { if (!NetworkDriven) Tick(); }
    public void Tick()
    {
        if (director == null) return;
        if (!director.IsPlaying)
        {
            if (phase != 0) ResetHazard();
            return;
        }
        if (phase == 0 && Time.time >= nextShot) Aim();
        else if (phase == 1 && Time.time >= phaseStarted + director.Settings.cannonWarningSeconds) Fire();
        else if (phase == 2)
        {
            float t = Mathf.Clamp01((Time.time - phaseStarted) / 0.8f);
            if (ball != null) ball.transform.position = Vector3.Lerp(start, impact, t) + Vector3.up * (5f * 4f * t * (1f-t));
            if (t >= 1f)
            {
                PirateArenaAudio.Play(PirateArenaAudio.Sound.CannonBlast, impact);
                director.Blast(impact, director.Settings.cannonRadius, 9f);
                Destroy(ball); Destroy(warning);
                phase = 0;
                nextShot = Time.time + Random.Range(director.Settings.cannonMinInterval, director.Settings.cannonMaxInterval);
            }
        }
    }
    private void Aim()
    {
        PirateFighter target = director.RandomLivingFighter();
        if (target == null) return;
        Vector2 offset = Random.insideUnitCircle * 0.7f;
        impact = target.transform.position + new Vector3(offset.x, 0f, offset.y);
        impact.x = Mathf.Clamp(impact.x, -width * 0.5f + 0.4f, width * 0.5f - 0.4f);
        impact.z = Mathf.Clamp(impact.z, -length * 0.5f + 0.4f, length * 0.5f - 0.4f);
        impact.y = director.SurfaceHeight(impact) + 0.065f;
        Vector3 look = Vector3.ProjectOnPlane(impact - transform.position, Vector3.up);
        if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
        warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        warning.name = "Zona roja - radio real del impacto";
        warning.transform.position = impact;
        float diameter = director.Settings.cannonRadius * 2f;
        warning.transform.localScale = new Vector3(diameter, 0.012f, diameter);
        warning.GetComponent<Collider>().enabled = false;
        Destroy(warning.GetComponent<Collider>());
        warning.GetComponent<Renderer>().sharedMaterial = director.Art.warning;
        director.ShowEvent("¡Cañón! Sal de la zona roja");
        PirateArenaAudio.Play(PirateArenaAudio.Sound.CannonWarn, transform.position, .75f);
        phase = 1; phaseStarted = Time.time;
    }
    private void Fire()
    {
        ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Bala de cañón";
        ball.transform.localScale = Vector3.one * 0.65f;
        ball.GetComponent<Collider>().enabled = false;
        Destroy(ball.GetComponent<Collider>());
        ball.GetComponent<Renderer>().sharedMaterial = director.Art.iron;
        start = transform.position + transform.forward * 1.2f + Vector3.up * 1.1f;
        ball.transform.position = start;
        PirateArenaAudio.Play(PirateArenaAudio.Sound.CannonShot, start);
        phase = 2; phaseStarted = Time.time;
    }
    public void ResetHazard()
    {
        if (warning != null) Destroy(warning);
        if (ball != null) Destroy(ball);
        phase = 0; nextShot = Time.time + 5f;
    }

    public PirateHazardState CaptureState() => new PirateHazardState { Position = transform.position, Rotation = transform.rotation,
        Target = impact, Projectile = ball != null ? ball.transform.position : start, Phase = phase };

    public void ApplyState(PirateHazardState state)
    {
        if (phase == 0 && state.Phase == 1) PirateArenaAudio.Play(PirateArenaAudio.Sound.CannonWarn, state.Position, .75f);
        if (phase == 1 && state.Phase == 2) PirateArenaAudio.Play(PirateArenaAudio.Sound.CannonShot, state.Position);
        if (phase == 2 && state.Phase == 0) PirateArenaAudio.Play(PirateArenaAudio.Sound.CannonBlast, impact);
        transform.SetPositionAndRotation(state.Position, state.Rotation); impact = state.Target;
        if (state.Phase > 0 && warning == null)
        {
            warning = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            warning.name = "Zona roja sincronizada";
            warning.GetComponent<Collider>().enabled = false;
            Destroy(warning.GetComponent<Collider>());
            warning.transform.localScale = new Vector3(director.Settings.cannonRadius * 2f, 0.012f, director.Settings.cannonRadius * 2f);
            warning.GetComponent<Renderer>().sharedMaterial = director.Art.warning;
        }
        if (warning != null) { warning.transform.position = impact; if (state.Phase == 0) Destroy(warning); }
        if (state.Phase == 2 && ball == null)
        {
            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Bala sincronizada"; ball.transform.localScale = Vector3.one * 0.65f;
            ball.GetComponent<Collider>().enabled = false; Destroy(ball.GetComponent<Collider>());
            ball.GetComponent<Renderer>().sharedMaterial = director.Art.iron;
        }
        if (ball != null) { ball.transform.position = state.Projectile; if (state.Phase != 2) Destroy(ball); }
        phase = state.Phase;
    }
}
