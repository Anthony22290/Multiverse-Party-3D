using System.Linq;
using UnityEngine;

// Pickups dropped by zombies (2026-11-10). Botiquín: heals you and teammates nearby. Furia: 9 s of
// double damage and extra speed. Bomba: blast that wipes out nearby zombies (bosses take a chunk).
public sealed class SurvivalPowerUp : MonoBehaviour
{
    public enum Kind { Medkit, Rage, Bomb }
    public Kind kind;
    SurvivalDirector director; Transform visual; Material glow; Light lamp; float born, phase;
    static readonly Color[] Colors = { new Color(.4f, 1, .5f), new Color(1, .25f, .2f), new Color(1, .7f, .15f) };

    // Online (2026-11-12): the host numbers its pickups; clients show Remote copies that the host removes.
    public int NetId; public bool Remote;
    public static void Drop(SurvivalDirector d, Kind kind, Vector3 at)
    {
        if (FindObjectsByType<SurvivalPowerUp>(FindObjectsSortMode.None).Length >= 5) return;
        Spawn(d, kind, at, false);
    }
    public static SurvivalPowerUp Spawn(SurvivalDirector d, Kind kind, Vector3 at, bool remote)
    {
        var go = new GameObject(kind == Kind.Medkit ? "Botiquín" : kind == Kind.Rage ? "Furia" : "Bomba");
        go.transform.position = remote ? at : new Vector3(at.x, at.y + .05f, at.z);
        var p = go.AddComponent<SurvivalPowerUp>(); p.kind = kind; p.director = d; p.born = Time.time; p.phase = Random.value * 6; p.Remote = remote;
        p.Build(); EscapeAudio.Play("zdrop", at, .7f); MultiverseFX.Ring(at, 1.6f, Colors[(int)kind]);
        return p;
    }
    /// <summary>Client: somebody took it on the host.</summary>
    public void NetTaken()
    {
        var c = Colors[(int)kind];
        MultiverseFX.Burst(transform.position + Vector3.up, c, 24, 6);
        if (kind == Kind.Bomb) { EscapeAudio.Play("zbomb", transform.position, 1); MultiverseFX.Ring(transform.position, 8, c, .7f); director?.View?.Shake(.4f); }
        else EscapeAudio.Play(kind == Kind.Medkit ? "zmedkit" : "zrage", transform.position, .9f);
        Destroy(gameObject);
    }
    GameObject Part(PrimitiveType t, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(t); Destroy(g.GetComponent<Collider>()); g.transform.SetParent(visual, false);
        g.transform.localPosition = pos; g.transform.localScale = scale; g.GetComponent<Renderer>().sharedMaterial = m; return g;
    }
    void Build()
    {
        visual = new GameObject("Visual").transform; visual.SetParent(transform, false); visual.localPosition = Vector3.up * .9f;
        var c = Colors[(int)kind]; glow = MultiverseFX.Glow(c, .25f, 2.2f, true);
        Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.05f, glow);
        if (kind == Kind.Medkit)
        {
            Part(PrimitiveType.Cube, Vector3.zero, new Vector3(.62f, .42f, .4f), EscapeKit.Paint(Color.white, .3f));
            var red = EscapeKit.Paint(new Color(.9f, .1f, .12f), .3f);
            Part(PrimitiveType.Cube, new Vector3(0, 0, -.21f), new Vector3(.36f, .1f, .02f), red); Part(PrimitiveType.Cube, new Vector3(0, 0, -.21f), new Vector3(.1f, .3f, .02f), red);
        }
        else if (kind == Kind.Rage)
        {
            var m = EscapeKit.Paint(new Color(.85f, .1f, .08f), .6f);
            Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * .42f, m);
            for (int i = 0; i < 6; i++) { var s = Part(PrimitiveType.Cube, Quaternion.Euler(i * 60, i * 37, 0) * Vector3.up * .28f, Vector3.one * .14f, m); s.transform.localRotation = Quaternion.Euler(45, i * 60, 45); }
        }
        else
        {
            Part(PrimitiveType.Sphere, Vector3.zero, Vector3.one * .55f, EscapeKit.Paint(new Color(.1f, .1f, .12f), .5f));
            Part(PrimitiveType.Cylinder, new Vector3(0, .33f, 0), new Vector3(.12f, .08f, .12f), EscapeKit.Paint(new Color(.5f, .4f, .3f), .1f));
            Part(PrimitiveType.Sphere, new Vector3(0, .45f, 0), Vector3.one * .12f, MultiverseFX.Glow(new Color(1, .6f, .1f), .9f, 1, true));
        }
        lamp = gameObject.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = c; lamp.range = 5; lamp.intensity = 1.8f;
    }
    void Update()
    {
        if (director == null) { Destroy(gameObject); return; }
        float age = Time.time - born;
        visual.localPosition = Vector3.up * (.9f + Mathf.Sin((Time.time + phase) * 2.6f) * .12f); visual.Rotate(0, 110 * Time.deltaTime, 0);
        visual.gameObject.SetActive(age < 14 || Mathf.Repeat(age * 6, 1) > .35f);   // blinks before vanishing
        if (Remote) return;
        if (age > 18) { Destroy(gameObject); return; }
        if (!director.Fighting) return;
        var taker = director.Team.FirstOrDefault(p => p != null && !p.Downed && (p.transform.position - transform.position).sqrMagnitude < 1.6f * 1.6f);
        if (taker != null) { Apply(taker); Destroy(gameObject); }
    }
    void Apply(SurvivalPlayer p)
    {
        var c = Colors[(int)kind]; Vector3 at = transform.position;
        MultiverseFX.Burst(at + Vector3.up, c, 24, 6);
        if (kind == Kind.Medkit)
        {
            p.Heal(35); foreach (var o in director.Team) if (o != p && o != null && !o.Downed && Vector3.Distance(o.transform.position, at) < 8) o.Heal(20);
            MultiverseFX.Ring(at, 8, c, .6f); EscapeAudio.Play("zmedkit", at, .9f);
            MultiverseFX.FloatText(at + Vector3.up * 2, "+35", c, .9f);
            director.Tell(p, "BOTIQUÍN · +35 de vida (y +20 a quien esté cerca)");
        }
        else if (kind == Kind.Rage)
        {
            p.Rage(9); EscapeAudio.Play("zrage", at, .9f); MultiverseFX.FloatText(at + Vector3.up * 2, "¡FURIA!", c, 1);
            director.Tell(p, "¡FURIA! Doble daño y más velocidad durante 9 s");
        }
        else
        {
            EscapeAudio.Play("zbomb", at, 1); director.View?.Shake(.55f);
            MultiverseFX.Ring(at, 8, c, .7f); MultiverseFX.Burst(at + Vector3.up, new Color(1, .5f, .1f), 50, 12); SurvivalKit.Puff(at + Vector3.up, new Color(.3f, .28f, .26f), 14, 1.6f);
            foreach (var z in SurvivalZombie.All.ToArray())
            {
                if (z == null || z.Dead) continue;
                Vector3 d = z.transform.position - at; d.y = 0;
                if (d.magnitude < 8) z.TakeHit(z.IsBoss ? 45 : 150, d.magnitude > .1f ? d.normalized * 1.6f : Vector3.forward, p);
            }
            director.Tell(p, "¡BOMBA! Los zombis cercanos han volado por los aires");
        }
    }
    void OnDestroy() { if (glow != null) Destroy(glow); }
}
