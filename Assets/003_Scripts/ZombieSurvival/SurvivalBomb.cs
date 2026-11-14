using UnityEngine;

// A lobbed bomb (thrown by Lanzabombas or falling in the "Lluvia de bombas" modifier). A red ring marks
// the landing spot; it lands, fizzes for half a second and explodes, hurting survivors AND zombies.
public sealed class SurvivalBomb : MonoBehaviour
{
    const float Radius = 3f;
    SurvivalDirector director; SurvivalZombie owner;
    Vector3 from, to; float start, flight, landedAt = -1;
    GameObject ring, ball; Material ringMat, fuseMat;
    bool visualOnly;
    /// <param name="visualOnly">Online client copy: it flies and explodes, but the host applies the damage.</param>
    public static void Throw(SurvivalDirector d, Vector3 from, Vector3 target, SurvivalZombie owner, float flight = 1.1f, bool visualOnly = false)
    {
        var go = new GameObject("Bomba lanzada"); var b = go.AddComponent<SurvivalBomb>();
        b.director = d; b.owner = owner; b.from = from; b.to = d.Level.ClampInside(new Vector3(target.x, .05f, target.z), 1f); b.start = Time.time; b.flight = flight; b.visualOnly = visualOnly;
        b.Build();
        if (!visualOnly && d.Net != null && PartyLink.IsHost) d.Net.SendBomb(from, b.to, flight);
    }
    void Build()
    {
        ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(ball.GetComponent<Collider>()); ball.transform.SetParent(transform, false); ball.transform.localScale = Vector3.one * .45f;
        ball.GetComponent<Renderer>().sharedMaterial = EscapeKit.Paint(new Color(.08f, .08f, .1f), .5f);
        var fuse = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(fuse.GetComponent<Collider>()); fuse.transform.SetParent(ball.transform, false); fuse.transform.localPosition = Vector3.up * .55f; fuse.transform.localScale = Vector3.one * .35f;
        fuseMat = MultiverseFX.Glow(new Color(1, .6f, .1f), .9f, 1, true); fuse.GetComponent<Renderer>().sharedMaterial = fuseMat;
        ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(ring.GetComponent<Collider>()); ring.name = "Aviso de bomba";
        ring.transform.position = to + Vector3.up * .04f; ring.transform.localScale = new Vector3(Radius * 2, .015f, Radius * 2);
        ringMat = MultiverseFX.Glow(new Color(1, .15f, .1f), .4f, 1.2f, true); ring.GetComponent<Renderer>().sharedMaterial = ringMat;
        transform.position = from;
    }
    void Update()
    {
        if (director == null || !director.Fighting) { Destroy(gameObject); return; }
        float t = (Time.time - start) / flight;
        if (landedAt < 0)
        {
            if (t >= 1) { landedAt = Time.time; transform.position = to + Vector3.up * .25f; EscapeAudio.Play("land_0", to, .5f); }
            else { var p = Vector3.Lerp(from, to + Vector3.up * .25f, t); p.y += Mathf.Sin(t * Mathf.PI) * (3.5f + Vector3.Distance(from, to) * .15f); transform.position = p; ball.transform.Rotate(400 * Time.deltaTime, 0, 0); }
            return;
        }
        ball.transform.localScale = Vector3.one * (.45f + Mathf.PingPong(Time.time * 4, .12f));
        if (Time.time - landedAt > .5f) Explode();
    }
    void Explode()
    {
        Vector3 at = to;
        EscapeAudio.Play("zbomb", at, .75f); MultiverseFX.Ring(at, Radius + 1, new Color(1, .5f, .15f), .45f);
        MultiverseFX.Burst(at + Vector3.up * .6f, new Color(1, .55f, .15f), 26, 8); SurvivalKit.Puff(at + Vector3.up * .6f, new Color(.25f, .24f, .23f), 9, 1.1f);
        foreach (var p in director.Team)
        {
            if (p == null || p.Downed || visualOnly) continue;
            Vector3 d = p.transform.position - at; d.y = 0;
            if (d.magnitude < Radius) p.Damage(22f, (d.magnitude > .1f ? d.normalized : Vector3.forward) * 7f, owner);
        }
        foreach (var z in SurvivalZombie.All.ToArray())
        {
            if (z == null || z.Dead || z == owner || visualOnly) continue;
            Vector3 d = z.transform.position - at; d.y = 0;
            if (d.magnitude < Radius) z.TakeHit(z.IsBoss ? 15f : 30f, d.magnitude > .1f ? d.normalized : Vector3.forward, null);
        }
        if (director.Human != null && Vector3.Distance(director.Human.transform.position, at) < 10) director.View?.Shake(.3f);
        Destroy(gameObject);
    }
    void OnDestroy() { if (ring != null) Destroy(ring); if (ringMat != null) Destroy(ringMat); if (fuseMat != null) Destroy(fuseMat); }
}
