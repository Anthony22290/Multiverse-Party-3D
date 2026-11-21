using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Camera for Patata Caliente: high, fixed angle (W is always "up the screen"), drifts towards you so the whole fair stays in view. Wheel zooms.</summary>
public sealed class PatataCamera : MonoBehaviour
{
    public PatataDirector Director;
    public float Yaw => 0f;
    /// <summary>Who the camera follows while you are out (null = overview).</summary>
    public PatataPlayer Watching { get; private set; }
    float distance = 17f, trauma; Vector3 focus; bool snap = true;

    public void Shake(float a) => trauma = Mathf.Clamp01(Mathf.Max(trauma, a));

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>(); cam.fieldOfView = 50f; cam.nearClipPlane = .2f; cam.farClipPlane = 220f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.62f, .8f, .98f);
        var m = Mouse.current;
        if (m != null && !Director.Paused) { float wheel = m.scroll.ReadValue().y; if (Mathf.Abs(wheel) > .01f) distance = Mathf.Clamp(distance - Mathf.Sign(wheel) * 1.5f, 10f, 26f); }
        var h = Director.Human;
        Vector3 want = Vector3.zero;
        if (Director.State == PatataDirector.Phase.Finished && Director.WinnerSlot >= 0) want = Director.Players[Director.WinnerSlot].transform.position;
        else if (h != null && h.Alive) want = Vector3.Lerp(Vector3.zero, new Vector3(h.transform.position.x, 0, h.transform.position.z), .5f);
        else if (h != null && Director.State != PatataDirector.Phase.Finished)
        {
            // Out: follow one of the players still in (←/→ to switch, Tab for the overview).
            var alive = Director.Players.FindAll(p => p.Alive);
            Watching = alive.Count > 0 ? alive[PartySpectate.Pick(alive.Count)] : null;
            if (PartySpectate.Overview) Watching = null;
            if (Watching != null) { want = Vector3.Lerp(Vector3.zero, new Vector3(Watching.transform.position.x, 0, Watching.transform.position.z), .75f); }
        }
        float dist = Director.State == PatataDirector.Phase.Finished ? Mathf.Min(distance, 11f) : distance * Mathf.Lerp(.8f, 1f, Director.Arena.Radius / PatataArena.MaxRadius);
        focus = snap ? want : Vector3.Lerp(focus, want, Time.deltaTime * 3f);
        var rot = Quaternion.Euler(52f, 0, 0);
        Vector3 pos = focus + Vector3.up * .5f - rot * Vector3.forward * dist;
        if (trauma > 0f) { pos += Random.insideUnitSphere * trauma * trauma * .45f; trauma = Mathf.Max(0, trauma - Time.deltaTime * 1.8f); }
        if (snap) transform.SetPositionAndRotation(pos, rot);
        else transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, Time.deltaTime * 6f), rot);
        snap = false;
    }
}
