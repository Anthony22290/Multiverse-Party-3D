using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public sealed class PirateArenaCamera : MonoBehaviour
{
    private PirateArenaDirector director;
    private Vector3 velocity;
    private float distance = 8f, yaw = 20f, pitch = 26f, collisionDistance = 8f;
    private Vector3 smoothedFocus;
    private readonly RaycastHit[] cameraHits = new RaycastHit[32];
    private Transform orbitTarget;
    private CinemachineThirdPersonFollow follow;
    public float Yaw => yaw;
    // Screen shake ("trauma"): events add trauma, attenuated by distance to the followed fighter.
    private static float trauma;
    private static Vector3 shakeFocus;
    public static void Shake(float amount, Vector3 at)
    {
        float falloff = Mathf.Clamp01(1.2f - Vector3.Distance(at, shakeFocus) / 26f);
        trauma = Mathf.Min(1f, trauma + amount * falloff);
    }
    public float Pitch => pitch;
    private bool positioned;
    public void SetDirector(PirateArenaDirector arena)
    {
        director = arena;
        if (GetComponent<CinemachineBrain>() == null) gameObject.AddComponent<CinemachineBrain>();
        var target = new GameObject("Cinemachine Pirate Orbit");
        orbitTarget = target.transform;
        var cameraRig = new GameObject("Cinemachine Pirate Camera");
        var virtualCamera = cameraRig.AddComponent<CinemachineCamera>();
        virtualCamera.Follow = orbitTarget;
        virtualCamera.LookAt = orbitTarget;
        follow = cameraRig.AddComponent<CinemachineThirdPersonFollow>();
        follow.Damping = new Vector3(0.12f, 0.12f, 0.12f);
        follow.ShoulderOffset = Vector3.zero;
        follow.VerticalArmLength = 0f;
        follow.CameraSide = 0.5f;
        follow.CameraDistance = distance;
    }

    private void LateUpdate()
    {
        if (director == null) return;
        PirateFighter player = null;
        foreach (PirateFighter fighter in director.Fighters)
            if (fighter != null && fighter.IsHuman && fighter.IsAlive) { player = fighter; break; }
        if (player == null) player = director.NearestLivingFighter(transform.position);
        Vector3 focus = player != null ? player.transform.position + Vector3.up * 0.45f : new Vector3(0f, director.DeckY + 1f, 0f);
        focus.y = Mathf.Max(0.8f, focus.y);
        bool menu = PirateSession.Instance != null && PirateSession.Instance.MenuOpen;
        if (!menu && Application.isFocused) Cursor.lockState = CursorLockMode.Locked;
        else Cursor.lockState = CursorLockMode.None;
        Cursor.visible = menu || !Application.isFocused;
        Keyboard keys = Keyboard.current;
        if (keys != null && !menu)
        {
            if (keys.qKey.isPressed) yaw -= Time.deltaTime * 60f;
        }
        if (Mouse.current != null && !menu)
        {
            Vector2 look = Mouse.current.delta.ReadValue() * MultiverseSettings.Sensitivity;
            yaw += look.x * 0.13f;
            pitch = Mathf.Clamp(pitch - look.y * 0.1f, -55f, 65f);
            distance = Mathf.Clamp(distance - Mouse.current.scroll.ReadValue().y * 0.008f, 3.5f, 14f);
        }
        smoothedFocus = positioned ? Vector3.SmoothDamp(smoothedFocus, focus, ref velocity, 0.12f) : focus;
        Vector3 direction = Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
        float allowed = distance;
        int count = Physics.SphereCastNonAlloc(smoothedFocus, 0.25f, direction, cameraHits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = cameraHits[i];
            if (hit.rigidbody != null || hit.collider.GetComponent<PirateArenaSurface>() == null) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0.7f, hit.distance - 0.12f));
        }
        collisionDistance = allowed < collisionDistance ? allowed : Mathf.MoveTowards(collisionDistance, allowed, Time.deltaTime * 8f);
        shakeFocus = smoothedFocus;
        trauma = Mathf.MoveTowards(trauma, 0f, Time.unscaledDeltaTime * 1.7f);
        float s = trauma * trauma, t = Time.unscaledTime * 22f;
        Quaternion shake = Quaternion.Euler((Mathf.PerlinNoise(t, 1.3f) - .5f) * 5f * s, (Mathf.PerlinNoise(2.7f, t) - .5f) * 5f * s, (Mathf.PerlinNoise(t, 4.1f) - .5f) * 3.5f * s);
        orbitTarget.SetPositionAndRotation(smoothedFocus, Quaternion.Euler(pitch, yaw, 0f) * shake);
        follow.CameraDistance = collisionDistance;
        positioned = true;
    }

    private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
}
