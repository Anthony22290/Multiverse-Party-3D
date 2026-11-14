using UnityEngine;

public sealed class SharkHazard : MonoBehaviour
{
    private PirateArenaDirector director;
    private float width, length, nextLeap, started;
    private Vector3 leapStart, leapEnd;
    private PirateFighter target, caught;
    private GameObject splash;
    private int phase;
    public void Initialize(PirateArenaDirector arena, float deckWidth, float deckLength, float deckY)
    {
        director = arena; width = deckWidth; length = deckLength;
        director.Register(this);
    }
    public void StartRound() { nextLeap = Time.time + 8f; }
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
        if (phase == 0)
        {
            float a = Time.time * 0.14f;
            Vector3 p = new Vector3(Mathf.Cos(a) * (width * 0.5f + 6f), -0.6f, Mathf.Sin(a) * (length * 0.5f + 5f));
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(tangent));
            if (Time.time < nextLeap) return;
            target = director.NearestLivingFighter(transform.position, null, true);
            if (target == null) { nextLeap = Time.time + 2f; return; }
            Vector3 pTarget = target.transform.position;
            bool side = width * 0.5f - Mathf.Abs(pTarget.x) < length * 0.5f - Mathf.Abs(pTarget.z);
            leapStart = side
                ? new Vector3(Mathf.Sign(pTarget.x) * (width * 0.5f + 5f), -0.6f, pTarget.z)
                : new Vector3(pTarget.x, -0.6f, Mathf.Sign(pTarget.z) * (length * 0.5f + 5f));
            leapEnd = pTarget;
            splash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            splash.name = "Espuma - aviso del tiburón";
            splash.transform.position = new Vector3(leapStart.x, 0.1f, leapStart.z);
            splash.transform.localScale = new Vector3(3f, 0.02f, 3f);
            splash.GetComponent<Collider>().enabled = false;
            Destroy(splash.GetComponent<Collider>());
            splash.GetComponent<Renderer>().sharedMaterial = director.Art.foam;
            director.ShowEvent("¡Tiburón! Aléjate de la borda");
            PirateArenaAudio.Play(PirateArenaAudio.Sound.SharkWarn, leapStart, .8f);
            phase = 1; started = Time.time;
            transform.position = leapStart;
        }
        else if (phase == 1)
        {
            if (Time.time - started < 1.1f) return;
            if (splash != null) Destroy(splash);
            PirateArenaAudio.Play(PirateArenaAudio.Sound.SharkLeap, leapStart);
            phase = 2; started = Time.time;
        }
        else if (phase == 2)
        {
            float t = Mathf.Clamp01((Time.time - started) / 1.15f);
            Vector3 p = Vector3.Lerp(leapStart, leapEnd, t) + Vector3.up * (3.2f * 4f * t * (1f-t));
            Vector3 heading = p - transform.position;
            if (heading.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(heading);
            transform.position = p;
            if (caught == null && target != null && target.IsAlive && director.IsNearEdge(target, 1.8f) &&
                Vector3.Distance(p, target.transform.position) < 1.5f)
            {
                caught = target;
                director.Eliminate(caught, "atrapado por el tiburón");
                PirateArenaAudio.Play(PirateArenaAudio.Sound.Splash, transform.position);
                if (caught != null && !caught.IsAlive) caught.Body.isKinematic = true;
            }
            CarryCaught();
            if (t >= 1f) { phase = 3; started = Time.time; }
        }
        else
        {
            float t = Mathf.Clamp01((Time.time - started) / 0.95f);
            transform.position = Vector3.Lerp(leapEnd, leapStart - Vector3.up * 2f, t);
            CarryCaught();
            if (t >= 1f)
            {
                ReleaseCaught(); phase = 0;
                nextLeap = Time.time + Random.Range(8f, 13f);
            }
        }
    }
    private void CarryCaught()
    {
        if (caught != null && !caught.IsAlive) caught.Body.position = transform.position + transform.forward * 0.6f;
    }
    private void ReleaseCaught()
    {
        if (caught != null && !caught.IsAlive)
        {
            caught.Body.isKinematic = false;
            caught.Body.linearVelocity = Vector3.down * 3f;
        }
        caught = null;
    }
    public void ResetHazard()
    {
        ReleaseCaught();
        if (splash != null) Destroy(splash);
        phase = 0; target = null; nextLeap = Time.time + 8f;
        transform.position = new Vector3(width * 0.5f + 6f, -0.6f, 0f);
    }

    public PirateHazardState CaptureState() => new PirateHazardState { Position = transform.position, Rotation = transform.rotation,
        Target = leapStart, Phase = phase };

    public void ApplyState(PirateHazardState state)
    {
        if (phase == 0 && state.Phase == 1) PirateArenaAudio.Play(PirateArenaAudio.Sound.SharkWarn, state.Target, .8f);
        if (phase == 1 && state.Phase == 2) PirateArenaAudio.Play(PirateArenaAudio.Sound.SharkLeap, state.Target);
        float blend = 1f - Mathf.Exp(-22f * Time.deltaTime);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, state.Position, blend), Quaternion.Slerp(transform.rotation, state.Rotation, blend));
        if (state.Phase == 1 && splash == null)
        {
            splash = GameObject.CreatePrimitive(PrimitiveType.Cylinder); splash.name = "Aviso de tiburón sincronizado";
            splash.transform.localScale = new Vector3(3f, 0.02f, 3f);
            splash.GetComponent<Collider>().enabled = false; Destroy(splash.GetComponent<Collider>());
            splash.GetComponent<Renderer>().sharedMaterial = director.Art.foam;
        }
        if (splash != null) { splash.transform.position = new Vector3(state.Target.x, 0.1f, state.Target.z); if (state.Phase != 1) Destroy(splash); }
        phase = state.Phase;
    }
}
