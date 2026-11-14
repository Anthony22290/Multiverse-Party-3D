using UnityEngine;
using System.Collections.Generic;

// Chunky kart wheels built at runtime on top of the original wheel positions (presentation only).
// Rubber tyre with tread blocks, rim in the kart colour, chrome spokes and hubcap. Wheels roll with
// speed, the front pair steers, and each wheel has a small spring for landings and rough ground.
public sealed class CrazyKartWheels : MonoBehaviour
{
    sealed class Wheel { public Transform pivot, spin; public Vector3 rest; public bool front; public float radius, offset, velocity, phase; }
    readonly List<Wheel> wheels = new List<Wheel>();
    CrazyKartController kart; CrazyKartDirector director; Rigidbody body;
    float roll; bool wasGrounded = true; float airTime;
    static Material rubber, chrome, dark;
    Material rimMat;

    void Start()
    {
        kart = GetComponent<CrazyKartController>(); body = GetComponent<Rigidbody>(); director = FindAnyObjectByType<CrazyKartDirector>();
        var model = transform.Find("Vehicle_Sports"); if (model == null) model = transform;
        if (rubber == null)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            rubber = new Material(lit) { name = "Neumático", color = new Color(.07f, .07f, .08f) }; rubber.SetFloat("_Smoothness", .15f);
            chrome = new Material(lit) { name = "Cromo", color = new Color(.92f, .93f, .96f) }; chrome.SetFloat("_Metallic", .85f); chrome.SetFloat("_Smoothness", .85f);
            dark = new Material(lit) { name = "Buje", color = new Color(.18f, .18f, .22f) }; dark.SetFloat("_Metallic", .5f);
        }
        rimMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Llanta " + kart.DisplayName, color = Color.Lerp(kart.KartColor, Color.white, .15f) };
        rimMat.SetFloat("_Metallic", .55f); rimMat.SetFloat("_Smoothness", .75f);
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
        {
            string n = r.name; if (n != "BackWheels" && n != "FrontWheel_L" && n != "FrontWheel_R") continue;
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds;
            // Wheel geometry in the model's space (independent of the kart's current heading).
            Vector3 center = model.InverseTransformPoint(r.transform.TransformPoint(b.center));
            Vector3 ext = model.InverseTransformVector(r.transform.TransformVector(b.extents)); ext = new Vector3(Mathf.Abs(ext.x), Mathf.Abs(ext.y), Mathf.Abs(ext.z));
            float radius = Mathf.Max(ext.y, ext.z);
            if (n == "BackWheels")
            {
                float w = .46f, x = ext.x - w * .35f;
                Build(model, center + Vector3.right * x, radius * 1.08f, w, false, 1);
                Build(model, center - Vector3.right * x, radius * 1.08f, w, false, -1);
            }
            else Build(model, center, radius * 1.02f, .32f, true, n.EndsWith("L") ? -1 : 1);
            r.enabled = false;
        }
    }
    void Build(Transform model, Vector3 pos, float radius, float width, bool front, int side)
    {
        var pivot = new GameObject(front ? "Rueda delantera" : "Rueda trasera").transform;
        pivot.SetParent(model, false); pivot.localPosition = pos;
        var spin = new GameObject("Giro").transform; spin.SetParent(pivot, false);
        float d = radius * 2;
        Part(PrimitiveType.Cylinder, spin, Vector3.zero, new Vector3(d, width * .5f, d), Quaternion.Euler(0, 0, 90), rubber);
        for (int i = 0; i < 10; i++)   // tread blocks make the roll readable
        {
            float a = i * 36 * Mathf.Deg2Rad;
            var q = Quaternion.Euler(i * 36, 0, 0);
            Part(PrimitiveType.Cube, spin, new Vector3(0, Mathf.Cos(a), Mathf.Sin(a)) * radius * .98f, new Vector3(width * 1.02f, radius * .16f, radius * .34f), q, rubber);
        }
        float face = side * width * .5f;
        Part(PrimitiveType.Cylinder, spin, new Vector3(face * .55f, 0, 0), new Vector3(d * .66f, width * .26f, d * .66f), Quaternion.Euler(0, 0, 90), rimMat);
        for (int i = 0; i < 5; i++)
            Part(PrimitiveType.Cube, spin, new Vector3(face * .82f, 0, 0) + Quaternion.Euler(i * 72, 0, 0) * new Vector3(0, radius * .3f, 0), new Vector3(width * .1f, radius * .55f, radius * .1f), Quaternion.Euler(i * 72, 0, 0), chrome);
        Part(PrimitiveType.Cylinder, spin, new Vector3(face * .9f, 0, 0), new Vector3(d * .24f, width * .12f, d * .24f), Quaternion.Euler(0, 0, 90), chrome);
        Part(PrimitiveType.Cylinder, spin, new Vector3(face * .98f, 0, 0), new Vector3(d * .1f, width * .06f, d * .1f), Quaternion.Euler(0, 0, 90), dark);
        wheels.Add(new Wheel { pivot = pivot, spin = spin, rest = pos, front = front, radius = radius, phase = Random.value * 10 });
    }
    static void Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var go = GameObject.CreatePrimitive(type); DestroyImmediate(go.GetComponent<Collider>());
        go.name = type == PrimitiveType.Cube ? "Pieza" : "Disco";
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = type == PrimitiveType.Cube ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
    }
    void LateUpdate()
    {
        if (body == null || wheels.Count == 0) return;
        float dt = Time.deltaTime;
        float speed = Vector3.Dot(body.linearVelocity, transform.forward);
        if (kart.Revving) speed = 9;            // wheels spin up while revving on the grid
        float r = wheels[0].radius;
        roll = Mathf.Repeat(roll + speed / Mathf.Max(.1f, r) * Mathf.Rad2Deg * dt, 360);
        bool landed = kart.Grounded && !wasGrounded && airTime > .2f;
        if (!kart.Grounded) airTime += dt; else airTime = 0;
        wasGrounded = kart.Grounded;
        bool rough = director != null && kart.Grounded && kart.Speed > 4 && director.DistanceToRoad(transform.position) > director.roadHalfWidth;
        float steer = kart.Steering * (kart.Drifting ? -14 : 26);   // counter-steer while drifting
        foreach (var w in wheels)
        {
            if (landed) w.velocity += 2.2f;
            float bump = rough ? Mathf.Sin(Time.time * 31 + w.phase) * .035f : 0;
            // Stable spring: fixed sub-steps so long frames (time scale, hitches) cannot blow it up.
            for (float left = Mathf.Min(dt, .1f); left > 0; left -= 1 / 120f)
            {
                float h = Mathf.Min(left, 1 / 120f);
                w.velocity += (-(w.offset - bump) * 220 - w.velocity * 14) * h; w.offset += w.velocity * h;
            }
            if (float.IsNaN(w.offset) || float.IsNaN(w.velocity)) { w.offset = 0; w.velocity = 0; }
            w.velocity = Mathf.Clamp(w.velocity, -6, 6);
            if (!kart.Grounded) w.offset = Mathf.MoveTowards(w.offset, -.08f, dt * .6f);   // wheels droop in the air
            w.pivot.localPosition = w.rest + Vector3.up * Mathf.Clamp(w.offset, -.12f, .14f);
            w.pivot.localRotation = Quaternion.Euler(0, w.front ? steer : 0, 0);
            w.spin.localRotation = Quaternion.Euler(roll, 0, 0);
        }
    }
    void OnDestroy() { if (rimMat != null) Destroy(rimMat); }
}
