using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Camera for Rodillos del Calabozo: behind and above the player on the line from the hub, so both rollers are always
/// coming at you on screen. Q/E (or right stick) orbit a little; when you are out it circles the platform.
/// </summary>
public sealed class RodillosCamera : MonoBehaviour
{
    public RodillosDirector Director;
    public float Yaw { get; private set; }
    /// <summary>Who the camera follows while you are out (null = overview).</summary>
    public RodillosRunner Watching { get; private set; }
    float trauma, offsetYaw, height = 8.5f, back = 8.8f;
    Vector3 smoothFocus; bool snap = true;

    public void Shake(float amount) => trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>();
        cam.nearClipPlane = .1f; cam.farClipPlane = 160f; cam.fieldOfView = 58f;
        var me = Director.Human;
        bool follow = me != null && (me.Alive || Time.time - lastAlive < 1.6f) && Director.State != RodillosDirector.Phase.Finished;
        if (me != null && me.Alive) lastAlive = Time.time;
        // Out: follow one of the runners still in (←/→ to switch, Tab for the overview).
        var target = me; Watching = null;
        if (!follow && me != null && !me.Alive && Director.State == RodillosDirector.Phase.Running)
        {
            var alive = Director.Runners.FindAll(r => r.Alive);
            if (alive.Count > 0) { int i = PartySpectate.Pick(alive.Count); if (!PartySpectate.Overview) { target = alive[i]; Watching = target; follow = true; } }
        }

        Vector3 pos; Quaternion rot;
        if (!follow)
        {
            // Overview: slow orbit, with the winner in the middle at the end.
            float a = Time.time * 9f;
            Vector3 focus = Director.State == RodillosDirector.Phase.Finished && Director.WinnerSlot >= 0 && Director.WinnerSlot < Director.Runners.Count
                ? Director.Runners[Director.WinnerSlot].transform.position + Vector3.up : Vector3.up * .5f;
            if (snap) smoothFocus = focus;
            smoothFocus = Vector3.Lerp(smoothFocus, focus, Time.deltaTime * 2f);
            float dist = Director.State == RodillosDirector.Phase.Finished ? 9f : 21f;
            rot = Quaternion.Euler(Director.State == RodillosDirector.Phase.Finished ? 24f : 42f, a, 0);
            pos = smoothFocus - rot * Vector3.forward * dist;
            Yaw = a; snap = false;
        }
        else
        {
            var k = Keyboard.current; var g = Gamepad.current;
            if (Director.InputFree)
            {
                if (k != null) { if (k.qKey.isPressed) offsetYaw -= 70f * Time.deltaTime; if (k.eKey.isPressed) offsetYaw += 70f * Time.deltaTime; }
                if (g != null) offsetYaw += g.rightStick.ReadValue().x * 80f * Time.deltaTime;
                var m = Mouse.current; if (m != null) { float wheel = m.scroll.ReadValue().y; if (Mathf.Abs(wheel) > .01f) { back = Mathf.Clamp(back - Mathf.Sign(wheel) * .8f, 6f, 13f); height = back * .97f; } }
            }
            offsetYaw = Mathf.Clamp(offsetYaw, -70f, 70f);
            Vector3 p = target.transform.position; p.y = Mathf.Max(p.y, -1f);
            Vector3 flat = new Vector3(p.x, 0, p.z);
            // Look from outside towards the hub; near the centre keep the last direction.
            float wantYaw = flat.magnitude > 1.5f ? Quaternion.LookRotation(-flat).eulerAngles.y : Yaw - offsetYaw;
            wantYaw += offsetYaw;
            Yaw = snap ? wantYaw : Mathf.LerpAngle(Yaw, wantYaw, Time.deltaTime * 2.2f);
            Vector3 focus = new Vector3(p.x, Mathf.Clamp(p.y, 0f, 1.5f), p.z) + Vector3.up * .9f;
            if (snap) smoothFocus = focus;
            smoothFocus = Vector3.Lerp(smoothFocus, focus, Time.deltaTime * 8f);
            rot = Quaternion.Euler(0, Yaw, 0);
            pos = smoothFocus - rot * Vector3.forward * back + Vector3.up * height;
            rot = Quaternion.LookRotation(smoothFocus + rot * Vector3.forward * 3.2f - pos);
            snap = false;
        }
        if (trauma > 0f)
        {
            float s = trauma * trauma * .45f;
            pos += new Vector3(Mathf.PerlinNoise(Time.time * 25f, 0) - .5f, Mathf.PerlinNoise(0, Time.time * 25f) - .5f, 0) * s;
            trauma = Mathf.Max(0f, trauma - Time.deltaTime * 1.8f);
        }
        transform.SetPositionAndRotation(pos, rot);
    }
    float lastAlive;
}
