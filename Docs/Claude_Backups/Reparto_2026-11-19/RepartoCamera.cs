using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Camera for Reparto Loco: fixed angle looking up the street (W = towards the neighbourhood), follows you. Wheel zooms.</summary>
public sealed class RepartoCamera : MonoBehaviour
{
    public RepartoDirector Director;
    public float Yaw => 0f;
    float distance = 15f, trauma; Vector3 focus; bool snap = true;
    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>(); cam.fieldOfView = 52f; cam.nearClipPlane = .2f; cam.farClipPlane = 250f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.62f, .8f, .98f);
        var m = Mouse.current;
        if (m != null && !Director.Paused) { float wheel = m.scroll.ReadValue().y; if (Mathf.Abs(wheel) > .01f) distance = Mathf.Clamp(distance - Mathf.Sign(wheel) * 1.5f, 9f, 26f); }
        var h = Director.Human; Vector3 want; float dist = distance;
        if (Director.State == RepartoDirector.Phase.Finished && Director.WinnerSlot >= 0) { want = Director.Players[Director.WinnerSlot].transform.position; dist = Mathf.Min(distance, 10f); }
        else if (Director.State != RepartoDirector.Phase.Playing) { want = new Vector3(0, 0, 9f); dist = 30f; }
        else want = h != null ? new Vector3(h.transform.position.x * .6f, 0, h.transform.position.z + 1.5f) : new Vector3(0, 0, 14f);
        focus = snap ? want : Vector3.Lerp(focus, want, Time.deltaTime * 4f);
        var rot = Quaternion.Euler(52f, 0, 0);
        Vector3 pos = focus + Vector3.up * .5f - rot * Vector3.forward * dist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .4f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 2f); }
        transform.SetPositionAndRotation(snap ? pos : Vector3.Lerp(transform.position, pos, Time.deltaTime * 7f), rot);
        snap = false;
    }
}
