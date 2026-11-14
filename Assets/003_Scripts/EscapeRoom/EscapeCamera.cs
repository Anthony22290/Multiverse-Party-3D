using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// First-person view for the escape tower: mouse look with a locked cursor, a slight head bob, and aiming:
/// the local player uses whatever interactable the crosshair points at. Before the game starts (editor-only lobby)
/// the camera flies slowly along the tower.
/// </summary>
public sealed class EscapeCamera : MonoBehaviour
{
    public EscapeDirector Director;
    public float Yaw { get; private set; }
    public float Pitch => pitch;
    public bool FirstPerson => Director != null && Director.Human != null && Director.State != EscapeDirector.Phase.Lobby;
    public const float EyeHeight = 1.58f;
    public float sensitivity = .11f;
    float pitch, trauma, bob, bobWeight;
    bool snap = true, wasLobby = true, fpApplied;
    readonly RaycastHit[] hits = new RaycastHit[16];

    public void Snap() => snap = true;
    public void Shake(float amount) => trauma = Mathf.Clamp01(Mathf.Max(trauma, amount));

    void LateUpdate()
    {
        if (Director == null) return;
        var cam = GetComponent<Camera>();
        cam.nearClipPlane = .05f; cam.farClipPlane = 260f;
        var target = Director.Human;

        if (!FirstPerson)
        {
            // Slow fly-over along the whole tower behind the start card.
            float span = (Director.Rooms != null ? Director.Rooms.Length - 1 : 5) * EscapeDirector.RoomLength;
            float z = Mathf.PingPong(Time.unscaledTime * 2.2f, span);
            var focus = new Vector3(0f, 0f, z);
            float yaw = 28f * Mathf.Sin(Time.unscaledTime * .12f);
            var r = Quaternion.Euler(42f, yaw, 0f);
            cam.fieldOfView = 55f;
            transform.SetPositionAndRotation(focus + r * Vector3.back * 21f, r);
            wasLobby = true;
            return;
        }
        if (wasLobby)
        {
            wasLobby = false; Yaw = target.transform.eulerAngles.y; pitch = 4f; snap = true;
        }
        if (!fpApplied) { target.SetFirstPerson(true); fpApplied = true; }
        cam.fieldOfView = 72f;

        bool look = Director.InputFree && (Director.Running || Director.State == EscapeDirector.Phase.Countdown) && !target.DrivenByBot;
        Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !look;
        if (look)
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var d = mouse.delta.ReadValue() * MultiverseSettings.Sensitivity;
                Yaw += d.x * sensitivity;
                pitch = Mathf.Clamp(pitch - d.y * sensitivity, -82f, 82f);
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                var s = pad.rightStick.ReadValue();
                Yaw += s.x * 160f * Time.deltaTime;
                pitch = Mathf.Clamp(pitch - s.y * 110f * Time.deltaTime, -82f, 82f);
            }
        }
        else if (target.DrivenByBot)
        {
            Yaw = Mathf.LerpAngle(Yaw, target.transform.eulerAngles.y, 1f - Mathf.Exp(-6f * Time.deltaTime));
            pitch = Mathf.Lerp(pitch, 8f, Time.deltaTime * 2f);
        }

        // Head bob while walking on the ground.
        float speed = target.Speed;
        bobWeight = Mathf.MoveTowards(bobWeight, target.Grounded && speed > 1f ? 1f : 0f, Time.deltaTime * 4f);
        bob += Time.deltaTime * Mathf.Lerp(6f, 11f, Mathf.Clamp01(speed / 6f));
        Vector3 bobOffset = new Vector3(Mathf.Cos(bob * .5f) * .025f, Mathf.Abs(Mathf.Sin(bob)) * .045f, 0f) * bobWeight;

        var rot = Quaternion.Euler(pitch, Yaw, 0f);
        Vector3 eye = target.transform.position + Vector3.up * EyeHeight;
        Vector3 desired = eye + rot * bobOffset;
        transform.position = snap ? desired : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-30f * Time.deltaTime));
        transform.rotation = rot;
        if (trauma > 0f)
        {
            float s = trauma * trauma * .18f, t = Time.time * 25f;
            transform.position += transform.right * (Mathf.PerlinNoise(t, 0f) - .5f) * s + transform.up * (Mathf.PerlinNoise(0f, t) - .5f) * s;
            transform.rotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 3f) - .5f) * trauma * 4f, (Mathf.PerlinNoise(5f, t) - .5f) * trauma * 4f, 0f);
            trauma = Mathf.Max(0f, trauma - Time.deltaTime * 1.2f);
        }
        snap = false;
    }

    void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

    /// <summary>The usable thing under the crosshair (within reach of the player's feet), or null.</summary>
    public EscapeInteractable Aimed(EscapeActor a)
    {
        Vector3 origin = transform.position, dir = transform.forward;
        EscapeInteractable best = null;
        float bestScore = float.MaxValue;
        foreach (var it in EscapeInteractable.All)
        {
            if (it == null || !it.CanUse(a) || !it.InRange(a)) continue;
            Vector3 to = it.Point - origin;
            float along = Vector3.Dot(to, dir);
            if (along < .1f || along > 4.4f) continue;
            float off = (to - dir * along).magnitude;
            float allowed = it.aimRadius + along * .09f;
            if (off > allowed) continue;
            if (Blocked(origin, it)) continue;
            float score = off / allowed + along * .05f;
            if (score < bestScore) { bestScore = score; best = it; }
        }
        return best;
    }

    bool Blocked(Vector3 origin, EscapeInteractable it)
    {
        Vector3 to = it.Point - origin;
        float dist = to.magnitude;
        if (dist < .01f) return false;
        int n = Physics.RaycastNonAlloc(origin, to / dist, hits, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c == null || hits[i].distance > dist - .45f) continue;
            if (c.GetComponentInParent<EscapeActor>() != null) continue;
            if (c.transform.IsChildOf(it.transform) || (it.transform.parent != null && c.transform.IsChildOf(it.transform.parent) && it.transform.parent.GetComponent<EscapeRoom>() == null)) continue;
            return true;
        }
        return false;
    }
}
