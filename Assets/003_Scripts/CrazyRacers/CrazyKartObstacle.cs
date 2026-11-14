using System.Collections.Generic;
using UnityEngine;

// Something solid in the middle of the road: barrels, rocks, or a boulder rolling across.
// Touching it spins the kart out (a shield absorbs it). Bots read All to steer around.
[RequireComponent(typeof(Collider))]
public sealed class CrazyKartObstacle : MonoBehaviour
{
    public static readonly List<CrazyKartObstacle> All = new List<CrazyKartObstacle>();
    public float radius = 1.2f;
    public Vector3 axis;          // world direction of travel for moving obstacles
    public float amplitude;       // metres either side of the start position (0 = static)
    public float speed = 1;       // radians per second of the ping-pong
    public float phase;
    public bool rolls;            // rotate the visual like a rolling boulder
    public Transform visual;
    Vector3 origin, last;
    Rigidbody body;
    public bool Moving => amplitude > 0;
    public Vector3 Position => body != null ? body.position : transform.position;
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    void Start()
    {
        origin = last = transform.position;
        body = GetComponent<Rigidbody>(); if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true; body.interpolation = RigidbodyInterpolation.Interpolate; body.useGravity = false;
    }
    void FixedUpdate()
    {
        if (!Moving) return;
        var p = origin + axis.normalized * Mathf.Sin(Time.time * speed + phase) * amplitude;
        body.MovePosition(p);
        if (rolls && visual != null)
        {
            Vector3 delta = p - last; float d = delta.magnitude;
            if (d > 1e-4f) visual.Rotate(Vector3.Cross(Vector3.up, delta / d), d / Mathf.Max(.3f, radius) * Mathf.Rad2Deg, Space.World);
        }
        last = p;
    }
    void OnCollisionEnter(Collision c)
    {
        var kart = c.collider.GetComponentInParent<CrazyKartController>();
        if (kart == null) return;
        bool wasStunned = kart.Stunned;
        kart.Hit();
        if (!wasStunned && kart.Stunned)
        {
            var kb = kart.GetComponent<Rigidbody>(); Vector3 away = kb.position - Position; away.y = 0;
            kb.linearVelocity = away.normalized * 6 + Vector3.up * 2;
            CrazyRaceFX.Shards(c.GetContact(0).point, 12, false, new Color(1, .75f, .3f), 6);
        }
    }
}
