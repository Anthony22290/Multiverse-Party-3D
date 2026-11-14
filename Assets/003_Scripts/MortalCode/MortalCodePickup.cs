using UnityEngine;

// Holographic pickups that appear around the plaza. Escáner: reveals the code of the nearest rival
// (even through walls). Turbo: 6 s of extra running speed. Anyone alive can grab them.
public sealed class MortalCodePickup : MonoBehaviour
{
    public enum Kind { Scanner, Speed }
    public Kind kind;
    /// <summary>Online: id given by the host; Remote copies are only shown (the host decides who grabs them).</summary>
    public int NetId; public bool Remote;
    MortalCodeDirector director; Transform visual; Material mat; float born, phase;
    public static MortalCodePickup Create(MortalCodeDirector d, Kind kind, Vector3 at)
    {
        var go = new GameObject(kind == Kind.Scanner ? "Escáner" : "Turbo"); go.transform.position = at;
        var p = go.AddComponent<MortalCodePickup>(); p.kind = kind; p.director = d; p.born = Time.time; p.phase = Random.value * 6;
        Color c = kind == Kind.Scanner ? new Color(.3f, 1, .9f) : new Color(1, .75f, .2f);
        p.visual = new GameObject("Holograma").transform; p.visual.SetParent(go.transform, false); p.visual.localPosition = Vector3.up * 1.1f;
        p.mat = MultiverseFX.Glow(c, .45f, 2.4f, true);
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(orb.GetComponent<Collider>()); orb.transform.SetParent(p.visual, false); orb.transform.localScale = Vector3.one * .75f; orb.GetComponent<Renderer>().sharedMaterial = p.mat;
        var core = GameObject.CreatePrimitive(kind == Kind.Scanner ? PrimitiveType.Cube : PrimitiveType.Capsule); Destroy(core.GetComponent<Collider>());
        core.transform.SetParent(p.visual, false); core.transform.localScale = kind == Kind.Scanner ? Vector3.one * .32f : new Vector3(.22f, .3f, .22f); core.GetComponent<Renderer>().sharedMaterial = p.mat;
        var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = c; light.range = 5; light.intensity = 1.6f;
        MultiverseFX.Ring(at, 1.8f, c);
        return p;
    }
    void Update()
    {
        if (director == null) { Destroy(gameObject); return; }
        float t = Time.time + phase;
        visual.localPosition = Vector3.up * (1.1f + Mathf.Sin(t * 2.4f) * .15f); visual.Rotate(0, 120 * Time.deltaTime, 0);
        if (Remote) return;
        if (Time.time - born > 35) { Destroy(gameObject); return; }
        if (!director.Running) return;
        foreach (var a in director.Actors)
        {
            if (!a.Alive) continue;
            var d = a.transform.position - transform.position; d.y = 0;
            if (d.sqrMagnitude < 1.4f * 1.4f && Mathf.Abs(a.transform.position.y - transform.position.y) < 1.6f) { director.OnPickup(a, this); Destroy(gameObject); return; }
        }
    }
    void OnDestroy() { if (mat != null) Destroy(mat); }
}
