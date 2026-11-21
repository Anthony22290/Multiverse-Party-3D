using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Third-person camera for Arma tu Nave: orbit with the right mouse button or Z/C, zoom with the wheel, pulls in at walls.</summary>
public sealed class NaveCamera : MonoBehaviour
{
    public NaveDirector Director;
    public float Yaw { get; private set; }
    float pitch = 38f, distance = 8.5f, trauma, curDist = 8.5f;
    readonly RaycastHit[] hits = new RaycastHit[16];
    bool snap = true;

    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>(); cam.fieldOfView = 55f; cam.nearClipPlane = .1f; cam.farClipPlane = 220f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.62f, .8f, .98f);
        var h = Director.Human;
        Transform target = h != null ? h.transform : null;
        if (Director.State == NaveDirector.Phase.Finished && Director.WinnerSlot >= 0) target = Director.Ships[Director.WinnerSlot].transform;
        if (target == null) return;
        if (snap) Yaw = Quaternion.LookRotation(target.position.sqrMagnitude > .1f ? -target.position : Vector3.forward).eulerAngles.y;   // start looking at the plaza

        var m = Mouse.current; var k = Keyboard.current; var g = Gamepad.current;
        if (Director.InputFree)
        {
            if (m != null)
            {
                if (m.rightButton.isPressed) { var dlt = m.delta.ReadValue() * MultiverseSettings.Sensitivity; Yaw += dlt.x * .16f; pitch = Mathf.Clamp(pitch - dlt.y * .12f, 12f, 70f); }
                float wheel = m.scroll.ReadValue().y; if (Mathf.Abs(wheel) > .01f) distance = Mathf.Clamp(distance - Mathf.Sign(wheel), 4.5f, 14f);
            }
            if (k != null) { if (k.zKey.isPressed) Yaw -= 100f * Time.deltaTime; if (k.cKey.isPressed) Yaw += 100f * Time.deltaTime; }
            if (g != null) { var s = g.rightStick.ReadValue(); Yaw += s.x * 120f * Time.deltaTime; pitch = Mathf.Clamp(pitch - s.y * 70f * Time.deltaTime, 12f, 70f); }
        }
        Vector3 focus = target.position + Vector3.up * 1.5f;
        var rot = Quaternion.Euler(pitch, Yaw, 0);
        Vector3 back = rot * Vector3.back;
        float want = distance;
        int n = Physics.SphereCastNonAlloc(focus, .3f, back, hits, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider; if (c == null || c.GetComponent<CharacterController>() != null || c.transform.IsChildOf(target)) continue;
            want = Mathf.Min(want, Mathf.Max(1.2f, hits[i].distance - .1f));
        }
        curDist = snap ? want : want < curDist ? want : Mathf.Lerp(curDist, want, Time.deltaTime * 4f);
        Vector3 pos = focus + back * curDist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .3f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 2f); }
        transform.SetPositionAndRotation(pos, rot);
        snap = false;
    }
}
