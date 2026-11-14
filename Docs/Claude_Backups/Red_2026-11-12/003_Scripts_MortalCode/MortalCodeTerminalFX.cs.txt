using UnityEngine;

// Holographic screen floating over each terminal: slowly rotating frame, pulsing glow,
// and a green / red flash when a code is accepted or rejected (presentation only).
public sealed class MortalCodeTerminalFX : MonoBehaviour
{
    Transform holo, ring; Material mat; Light glow; float flashUntil; Color flash;
    static readonly Color Idle = new Color(.35f, 1, .86f);
    void Start()
    {
        var t = GetComponent<MortalCodeTerminal>();
        holo = new GameObject("Holograma terminal").transform; holo.SetParent(transform, false); holo.localPosition = new Vector3(0, 3.1f, 0);
        mat = MultiverseFX.Glow(Idle, .3f, 2.2f, true);
        var screen = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(screen.GetComponent<Collider>()); screen.name = "Pantalla";
        screen.transform.SetParent(holo, false); screen.transform.localScale = new Vector3(1.6f, .9f, .05f); screen.GetComponent<Renderer>().sharedMaterial = mat;
        ring = new GameObject("Anillo").transform; ring.SetParent(holo, false);
        for (int i = 0; i < 4; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(b.GetComponent<Collider>()); b.transform.SetParent(ring, false);
            float a = i * 90 * Mathf.Deg2Rad; b.transform.localPosition = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.25f; b.transform.localScale = Vector3.one * .16f;
            b.GetComponent<Renderer>().sharedMaterial = mat;
        }
        if (t != null && !string.IsNullOrEmpty(t.label))
        {
            var label = new GameObject("Etiqueta"); label.transform.SetParent(holo, false); label.transform.localPosition = new Vector3(0, .8f, 0);
            var tm = label.AddComponent<TextMesh>(); tm.text = t.label; tm.anchor = TextAnchor.MiddleCenter; tm.fontSize = 48; tm.characterSize = .05f; tm.color = Idle;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tm.font = font; label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
        glow = holo.gameObject.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 7; glow.intensity = 1.4f; glow.color = Idle;
    }
    public void Pulse(bool ok) { flash = ok ? new Color(.3f, 1, .4f) : new Color(1, .25f, .25f); flashUntil = Time.time + .8f; MultiverseFX.Burst(holo.position, flash, ok ? 26 : 10, 5); }
    void Update()
    {
        if (holo == null) return;
        holo.localPosition = new Vector3(0, 3.1f + Mathf.Sin(Time.time * 1.7f) * .08f, 0);
        var cam = Camera.main; if (cam != null) { var d = holo.position - cam.transform.position; d.y = 0; if (d.sqrMagnitude > .01f) holo.rotation = Quaternion.LookRotation(d); }
        ring.Rotate(0, 60 * Time.deltaTime, 0, Space.World);
        Color c = Time.time < flashUntil ? Color.Lerp(Idle, flash, Mathf.PingPong(Time.time * 8, 1)) : Idle;
        mat.SetColor("_Color", c); glow.color = c;
    }
    void OnDestroy() { if (mat != null) Destroy(mat); }
}
