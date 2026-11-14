using UnityEngine;
public sealed class CrazyKartItemBox : MonoBehaviour
{
    float availableAt, respawnedAt = -10, phase;
    Renderer[] renderers;
    Collider trigger;
    Vector3 basePosition, baseScale;
    void Awake() { renderers = GetComponentsInChildren<Renderer>(); trigger = GetComponent<Collider>(); trigger.isTrigger = true; basePosition = transform.position; baseScale = transform.localScale; phase = basePosition.x * .37f; }
    public void Collect(CrazyKartController kart)
    {
        var director = FindAnyObjectByType<CrazyKartDirector>();
        if (Time.time < availableAt || !kart.GiveItem(director != null ? director.RollItem(kart) : Random.Range(1, 5), true)) return;
        director?.Net?.SendBox(this);
        Hide();
    }
    /// <summary>Taken: vanish for 5 s (online: also when another computer's kart took it).</summary>
    public void Hide()
    {
        if (!trigger.enabled) return;
        availableAt = Time.time + 5;
        CrazyRaceBurst.Spawn(transform.position,new Color(1,.85f,.15f),false);
        CrazyRaceFX.Shards(transform.position, 28, true, Color.white, 9);
        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = false;
        trigger.enabled = false;
    }
    void Update()
    {
        // Bob and tumble; pop back in with a small overshoot when it respawns.
        float t = Time.time + phase;
        transform.position = basePosition + Vector3.up * Mathf.Sin(t * 2.2f) * .18f;
        transform.rotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 14, t * 70, Mathf.Cos(t * 1.1f) * 14);
        float k = Mathf.Clamp01((Time.time - respawnedAt) / .35f);
        transform.localScale = baseScale * (k >= 1 ? 1 : Mathf.Sin(k * Mathf.PI * .5f) * (1 + .25f * Mathf.Sin(k * Mathf.PI)));
        if (!trigger.enabled && Time.time >= availableAt)
        { trigger.enabled = true; respawnedAt = Time.time; foreach (var r in renderers) r.enabled = true; }
    }
}
