using UnityEngine;

// Small shared FX kit for the minigames (built on the RaceFX shaders in Resources/RaceFX).
// Presentation only: nothing here has a collider or touches game state.
public static class MultiverseFX
{
    public static void Burst(Vector3 at, Color color, int count = 18, float speed = 7) => CrazyRaceFX.Shards(at, count, false, color, speed);
    public static void Rainbow(Vector3 at, int count = 30, float speed = 8) => CrazyRaceFX.Shards(at, count, true, Color.white, speed);
    public static void Lightning(Vector3 ground) => CrazyRaceFX.Lightning(ground);
    public static Material Glow(Color color, float alpha = .35f, float rim = 2f, bool pulse = false)
    {
        var m = CrazyRaceFX.Fresnel(color, false, alpha, rim); if (pulse) m.SetFloat("_Pulse", 1); return m;
    }

    /// <summary>Expanding flat ring on the ground (shockwave / telegraph flash).</summary>
    public static void Ring(Vector3 at, float radius, Color color, float seconds = .45f)
    {
        var go = new GameObject("FX onda"); go.transform.position = at + Vector3.up * .06f;
        var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = CrazyRaceFX.Glow; line.loop = true; line.useWorldSpace = false;
        line.positionCount = 40; line.widthMultiplier = .25f; line.startColor = line.endColor = color;
        go.AddComponent<MultiverseRing>().Init(radius, seconds, color);
    }

    /// <summary>World-space text that rises and fades, always facing the camera.</summary>
    public static void FloatText(Vector3 at, string text, Color color, float size = 1f, float seconds = 1.3f)
    {
        var go = new GameObject("FX texto"); go.transform.position = at;
        var tm = go.AddComponent<TextMesh>(); tm.text = text; tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontSize = 64; tm.characterSize = .06f * size; tm.color = color; tm.fontStyle = FontStyle.Bold;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        go.AddComponent<MultiverseFloat>().Init(seconds, color);
    }
}

public sealed class MultiverseRing : MonoBehaviour
{
    float radius, life, start; Color color; LineRenderer line;
    public void Init(float r, float seconds, Color c) { radius = r; life = seconds; color = c; start = Time.time; line = GetComponent<LineRenderer>(); Update(); }
    void Update()
    {
        float k = (Time.time - start) / life; if (k >= 1) { Destroy(gameObject); return; }
        float r = Mathf.Lerp(.2f, radius, 1 - (1 - k) * (1 - k));
        for (int i = 0; i < 40; i++) { float a = i * Mathf.PI * 2 / 40; line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
        var c = color; c.a = 1 - k; line.startColor = line.endColor = c; line.widthMultiplier = Mathf.Lerp(.35f, .08f, k);
    }
}

public sealed class MultiverseFloat : MonoBehaviour
{
    float life, start; Color color; TextMesh tm; Vector3 origin;
    public void Init(float seconds, Color c) { life = seconds; color = c; start = Time.time; tm = GetComponent<TextMesh>(); origin = transform.position; }
    void LateUpdate()
    {
        float k = (Time.time - start) / life; if (k >= 1) { Destroy(gameObject); return; }
        transform.position = origin + Vector3.up * (1.2f * k);
        var cam = Camera.main; if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        float pop = k < .15f ? Mathf.Lerp(1.6f, 1, k / .15f) : 1;
        transform.localScale = Vector3.one * pop;
        var c = color; c.a = k < .7f ? 1 : 1 - (k - .7f) / .3f; tm.color = c;
    }
}
