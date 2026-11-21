using System.Collections.Generic;
using UnityEngine;

/// <summary>Arma tu Nave · the five piece types, the pieces hidden in the village and each player's ship on its pad.</summary>
public static class NaveKit
{
    public const int Types = 5;
    public static readonly string[] Keys = { "Nave_Cabina", "Nave_Motor", "Nave_AlaIzq", "Nave_AlaDer", "Nave_Cola" };
    public static readonly string[] Names = { "CABINA", "MOTOR", "ALA IZQ.", "ALA DER.", "COLA" };
    public static readonly Color[] Colors = { new Color(.4f, .85f, 1f), new Color(1f, .55f, .2f), new Color(.45f, .6f, 1f), new Color(.55f, 1f, .45f), new Color(1f, .4f, .75f) };

    /// <summary>A piece model centred on its own pivot, about <paramref name="size"/> metres across.</summary>
    public static Transform Model(int type, Transform parent, float size)
    {
        var holder = new GameObject(Names[type]).transform; holder.SetParent(parent, false);
        var p = NaveVillage.Load(Keys[type]); if (p == null) return holder;
        var go = Object.Instantiate(p, holder, false);
        foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        var b = PirateArenaVisuals.BoundsOf(go);
        float big = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        go.transform.localScale = Vector3.one * (size / Mathf.Max(.01f, big));
        b = PirateArenaVisuals.BoundsOf(go);
        go.transform.position += holder.position - b.center;
        return holder;
    }
}

/// <summary>One piece. The host decides who holds it; everybody shows it where it is.</summary>
public sealed class NavePiece : MonoBehaviour
{
    public enum State : byte { Free = 0, Carried = 1, Installed = 2 }
    public int Id, Type;
    public State Is { get; private set; } = State.Free;
    public int HolderSlot { get; private set; } = -1;     // carrier, or ship owner once installed
    public Vector3 Home { get; private set; }
    NaveDirector d; Transform model, ring, beam; float phase;

    public static NavePiece Create(NaveDirector director, int id, int type, Vector3 at)
    {
        var go = new GameObject("Pieza " + id + " · " + NaveKit.Names[type]);
        var p = go.AddComponent<NavePiece>(); p.d = director; p.Id = id; p.Type = type; p.phase = id * .7f;
        p.model = NaveKit.Model(type, go.transform, 1.1f);
        p.ring = EscapeKit.Shape(PrimitiveType.Cylinder, go.transform, "Brillo", Vector3.zero, new Vector3(1.2f, .01f, 1.2f), MultiverseFX.Glow(NaveKit.Colors[type], .45f, 2f, true), false).transform;
        var b = EscapeKit.Shape(PrimitiveType.Cylinder, go.transform, "Haz del radar", Vector3.up * 8f, new Vector3(.35f, 8f, .35f), MultiverseFX.Glow(NaveKit.Colors[type], .3f, 1.5f, true), false);
        p.beam = b.transform; b.SetActive(false);
        p.Drop(at);
        return p;
    }

    public void Drop(Vector3 at)
    {
        Is = State.Free; HolderSlot = -1; Home = at;
        transform.SetParent(null, true); transform.position = at; transform.rotation = Quaternion.Euler(0, Id * 47f, 0);
        model.gameObject.SetActive(true); ring.gameObject.SetActive(true);
    }
    public void Carry(int slot) { Is = State.Carried; HolderSlot = slot; ring.gameObject.SetActive(false); beam.gameObject.SetActive(false); }
    public void Install(int slot) { Is = State.Installed; HolderSlot = slot; gameObject.SetActive(false); }

    void Update()
    {
        if (d == null) return;
        if (Is == State.Free)
        {
            model.localPosition = Vector3.up * (.75f + Mathf.Sin(Time.time * 2.2f + phase) * .12f);
            model.Rotate(0, 50f * Time.deltaTime, 0);
            beam.gameObject.SetActive(d.Radar && (d.Human == null || d.NeedsLocal(Type)));
        }
        else if (Is == State.Carried && HolderSlot >= 0 && HolderSlot < d.Players.Count)
        {
            var h = d.Players[HolderSlot].transform;
            transform.position = h.position + Vector3.up * 2.05f; transform.rotation = h.rotation;
            model.localPosition = Vector3.up * Mathf.Sin(Time.time * 6f) * .04f;
        }
    }
}

/// <summary>A launch pad with the ghost of the ship; installed pieces become solid. Takes off when complete.</summary>
public sealed class NaveShip : MonoBehaviour
{
    public int Slot;
    public readonly bool[] Has = new bool[NaveKit.Types];
    public int Count { get { int n = 0; foreach (var b in Has) if (b) n++; return n; } }
    public float LastInstallAt = -1f;
    readonly GameObject[] solid = new GameObject[NaveKit.Types], ghost = new GameObject[NaveKit.Types];
    Transform body; TextMesh label; bool launching; float launchT;

    public void Build(int slot, Color color, string owner)
    {
        Slot = slot;
        var pad = NaveVillage.Load("Nave_Plataforma"); if (pad != null) Instantiate(pad, transform, false);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Anillo", Vector3.up * .17f, new Vector3(5.2f, .01f, 5.2f), MultiverseFX.Glow(color, .35f, 2f), false);
        body = new GameObject("Nave").transform; body.SetParent(transform, false);
        var chasis = NaveVillage.Load("Nave_Chasis"); if (chasis != null) foreach (var c in Instantiate(chasis, body, false).GetComponentsInChildren<Collider>()) Destroy(c);
        var ghostMat = MultiverseFX.Glow(new Color(.6f, .85f, 1f), .22f, 2.2f, true);
        for (int t = 0; t < NaveKit.Types; t++)
        {
            var p = NaveVillage.Load(NaveKit.Keys[t]); if (p == null) continue;
            solid[t] = Instantiate(p, body, false); solid[t].SetActive(false);
            ghost[t] = Instantiate(p, body, false);
            foreach (var r in ghost[t].GetComponentsInChildren<Renderer>()) { var m = new Material[r.sharedMaterials.Length]; for (int i = 0; i < m.Length; i++) m[i] = ghostMat; r.sharedMaterials = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            foreach (var c in solid[t].GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var c in ghost[t].GetComponentsInChildren<Collider>()) Destroy(c);
        }
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label = new GameObject("Marcador").AddComponent<TextMesh>(); label.transform.SetParent(transform, false); label.transform.localPosition = Vector3.up * 3.7f;
        label.font = font; label.GetComponent<MeshRenderer>().sharedMaterial = font.material; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
        label.fontSize = 64; label.characterSize = .05f; label.color = color; label.fontStyle = FontStyle.Bold;
        Refresh(owner);
        // A solid block so nobody walks through the ship.
        var box = gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0, 1.1f, 0); box.size = new Vector3(2.2f, 2.2f, 4.2f);
    }

    public void Refresh(string owner) { if (label != null) label.text = owner + "\n" + Count + "/5"; }

    public void Install(int type, string owner, bool fx)
    {
        if (Has[type]) return;
        Has[type] = true;
        if (solid[type] != null) solid[type].SetActive(true);
        if (ghost[type] != null) ghost[type].SetActive(false);
        Refresh(owner);
        if (fx) { MultiverseFX.Burst(transform.position + Vector3.up * 1.5f, NaveKit.Colors[type], 22, 6); MultiverseFX.Ring(transform.position, 3f, NaveKit.Colors[type]); NaveAudio.Play("install", transform.position, 1f); }
    }

    public void Launch() { launching = true; launchT = 0f; NaveAudio.Play("launch", transform.position, 1f); }

    void Update()
    {
        if (label != null && Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
        if (!launching || body == null) return;
        launchT += Time.deltaTime;
        float up = launchT < .8f ? Mathf.Sin(launchT * 40f) * .03f : (launchT - .8f) * (launchT - .8f) * 6f;
        body.localPosition = new Vector3(0, up, 0);
        body.localRotation = Quaternion.Euler(launchT > .8f ? -Mathf.Min(70f, (launchT - .8f) * 40f) : 0f, 0, 0);
        if (Random.value < .6f) SurvivalKit.Puff(body.position + body.forward * -2.5f + Vector3.up * .8f, new Color(1f, .6f, .2f), 3, .6f);
    }
}
