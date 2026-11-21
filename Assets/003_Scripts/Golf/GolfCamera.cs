using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Camera for Minigolf Loco: hole overview in the intro and at the end, behind your ball while you aim, following it while it rolls.</summary>
public sealed class GolfCamera : MonoBehaviour
{
    public GolfDirector Director;
    float dist = 4.2f, pitch = 28f; Vector3 vel; bool snap = true; int lastHole = -1;

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>(); cam.fieldOfView = 55f; cam.nearClipPlane = .05f; cam.farClipPlane = 250f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.55f, .8f, 1f);
        var h = Director.CurrentHole; var me = Director.Human;
        if (h == null) { transform.SetPositionAndRotation(new Vector3(0, 12, -12), Quaternion.Euler(40, 0, 0)); return; }
        if (Director.HoleIndex != lastHole) { lastHole = Director.HoleIndex; snap = true; }
        var m = Mouse.current;
        if (m != null && Director.InputFree)
        {
            float wheel = m.scroll.ReadValue().y; if (Mathf.Abs(wheel) > .01f) dist = Mathf.Clamp(dist - Mathf.Sign(wheel) * .6f, 2f, 9f);
            if (m.rightButton.isPressed) pitch = Mathf.Clamp(pitch - m.delta.ReadValue().y * .1f, 8f, 75f);
        }
        Vector3 wantPos; Quaternion wantRot;
        bool overview = Director.State == GolfDirector.Phase.Intro || Director.State == GolfDirector.Phase.HoleEnd || Director.State == GolfDirector.Phase.Finished || Director.State == GolfDirector.Phase.Waiting || me == null || me.Done;
        if (overview)
        {
            var c = h.Area.center; float size = Mathf.Max(h.Area.size.x, h.Area.size.z);
            if (Director.State == GolfDirector.Phase.Finished && Director.WinnerSlot >= 0) c = Director.Players[Director.WinnerSlot].Ball.position;
            float a = Director.PhaseTime * 6f;
            wantRot = Quaternion.Euler(52f, a * .3f, 0);
            wantPos = c - wantRot * Vector3.forward * (size * .95f + 6f);
        }
        else
        {
            var ball = me.Ball.position;
            float yaw = me.State == GolfPlayer.Phase.Rolling ? Quaternion.LookRotation(Flat(me.Body.linearVelocity).sqrMagnitude > .3f ? Flat(me.Body.linearVelocity) : transform.forward).eulerAngles.y : me.Aim;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            wantPos = ball - rot * Vector3.forward * dist + Vector3.up * .3f;
            wantRot = Quaternion.LookRotation(ball + rot * Vector3.forward * 1.5f - wantPos);
        }
        if (snap) { transform.SetPositionAndRotation(wantPos, wantRot); snap = false; return; }
        float k = overview ? 2.5f : 7f;
        transform.position = Vector3.Lerp(transform.position, wantPos, Time.deltaTime * k);
        transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, Time.deltaTime * k);
    }
    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
}
