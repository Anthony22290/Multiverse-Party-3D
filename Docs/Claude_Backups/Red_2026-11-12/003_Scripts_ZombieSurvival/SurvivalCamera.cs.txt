using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>High third-person camera (sees over the barricades), orbit with the right mouse or Z/C, zoom with the wheel.
/// Lobby: slow orbit over the plaza. Victory: follows the truck out of the city.</summary>
public sealed class SurvivalCamera : MonoBehaviour
{
    public SurvivalDirector Director;
    public float Yaw { get; private set; }
    float pitch = 52f, distance = 12f, trauma;
    bool snap = true, wasLobby;
    readonly RaycastHit[] hits = new RaycastHit[24];

    public void Snap() => snap = true;
    public void Shake(float amount) => trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>();
        cam.nearClipPlane = .1f; cam.farClipPlane = 220f; cam.fieldOfView = 55f;
        var target = Director.Human;
        if (Director.State == SurvivalDirector.Phase.Lobby || target == null)
        {
            float yaw = Time.unscaledTime * 6f;
            var r = Quaternion.Euler(36f, yaw, 0f);
            transform.SetPositionAndRotation(Vector3.zero + r * Vector3.back * 38f + Vector3.up * 2f, r);
            Yaw = yaw; wasLobby = true;
            return;
        }
        if (wasLobby) { wasLobby = false; Yaw = 0f; pitch = 52f; snap = true; }

        Vector3 focus;
        if (Director.State == SurvivalDirector.Phase.Won && Director.Level.Truck != null)
        {
            focus = Director.Level.Truck.transform.position + Vector3.up * 1.5f;
            Yaw = Mathf.LerpAngle(Yaw, 200f, Time.deltaTime * .8f);
            pitch = Mathf.Lerp(pitch, 30f, Time.deltaTime);
            distance = Mathf.Lerp(distance, 15f, Time.deltaTime);
        }
        else focus = target.transform.position + Vector3.up * 1.2f;

        var mouse = Mouse.current;
        if (mouse != null && Director.InputFree)
        {
            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue() * MultiverseSettings.Sensitivity;
                Yaw += delta.x * .16f;
                pitch = Mathf.Clamp(pitch - delta.y * .12f, 25f, 80f);
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > .01f) distance = Mathf.Clamp(distance - Mathf.Sign(wheel) * 1f, 7f, 20f);
        }
        var k = Keyboard.current;
        if (k != null && Director.InputFree)
        {
            if (k.zKey.isPressed) Yaw -= 90f * Time.deltaTime;
            if (k.cKey.isPressed) Yaw += 90f * Time.deltaTime;
        }

        var rot = Quaternion.Euler(pitch, Yaw, 0f);
        Vector3 back = rot * Vector3.back;
        float allowed = distance;
        int n = Physics.SphereCastNonAlloc(focus, .25f, back, hits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c == null || c.transform.IsChildOf(target.transform)) continue;
            if (c.GetComponent<SurvivalShell>() != null || c.GetComponent<CharacterController>() != null) continue;
            if (hits[i].distance <= 0f) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(3f, hits[i].distance - .2f));
        }
        Vector3 desired = focus + back * allowed;
        transform.position = snap ? desired : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-10f * Time.deltaTime));
        transform.rotation = rot;
        if (trauma > 0f)
        {
            float s = trauma * trauma * .45f, t = Time.time * 25f;
            transform.position += transform.right * (Mathf.PerlinNoise(t, 0f) - .5f) * s + transform.up * (Mathf.PerlinNoise(0f, t) - .5f) * s;
            trauma = Mathf.Max(0f, trauma - Time.deltaTime * 1.3f);
        }
        snap = false;
    }
}
