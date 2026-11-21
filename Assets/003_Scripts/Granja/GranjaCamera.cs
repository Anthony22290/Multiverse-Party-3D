using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Camera for Gallinas Locas: high, fixed angle (W is always "up the screen"), follows you across the farm. Wheel zooms.</summary>
public sealed class GranjaCamera : MonoBehaviour
{
    public GranjaDirector Director;
    public float Yaw => 0f;
    float distance = 17f, trauma; Vector3 focus; bool snap = true;

    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>(); cam.fieldOfView = 50f; cam.nearClipPlane = .2f; cam.farClipPlane = 250f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.62f, .8f, .98f);
        var m = Mouse.current;
        if (m != null && !Director.Paused) { float wheel = m.scroll.ReadValue().y; if (Mathf.Abs(wheel) > .01f) distance = Mathf.Clamp(distance - Mathf.Sign(wheel) * 1.5f, 10f, 30f); }
        var h = Director.Human;
        Vector3 want = Vector3.zero; float dist = distance;
        if (Director.State == GranjaDirector.Phase.Finished && Director.WinnerSlot >= 0) { want = Director.Players[Director.WinnerSlot].transform.position; dist = Mathf.Min(distance, 11f); }
        else if (Director.State == GranjaDirector.Phase.Countdown || Director.State == GranjaDirector.Phase.Waiting) { want = h != null ? Vector3.Lerp(Vector3.zero, Flat(h.transform.position), .45f) : Vector3.zero; dist = 30f; }
        else if (h != null) want = Vector3.Lerp(Vector3.zero, Flat(h.transform.position), .72f);
        focus = snap ? want : Vector3.Lerp(focus, want, Time.deltaTime * 3.5f);
        var rot = Quaternion.Euler(54f, 0, 0);
        Vector3 pos = focus + Vector3.up * .5f - rot * Vector3.forward * dist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .45f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 1.8f); }
        transform.SetPositionAndRotation(snap ? pos : Vector3.Lerp(transform.position, pos, Time.deltaTime * 6f), rot);
        snap = false;
    }
    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
