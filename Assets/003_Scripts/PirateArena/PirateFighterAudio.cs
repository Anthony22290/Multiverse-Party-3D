using UnityEngine;

/// <summary>Pasos de los luchadores. Clips CC0 de Kenney: ver Resources/PirateAudio/CREDITS.txt.</summary>
public static class PirateFighterAudio
{
    enum Floor { Wood, Stone }
    static AudioClip[] wood, stone;
    static readonly RaycastHit[] hits = new RaycastHit[6];
    static bool leftFoot;

    public static void PlayFootstep(Vector3 position, float speed01)
    {
        if (wood == null) { wood = LoadPool("step_wood_", 5); stone = LoadPool("step_stone_", 3); }
        AudioClip[] pool = PickFloor(position) == Floor.Stone ? stone : wood;
        AudioClip clip = pool == null || pool.Length == 0 ? null : pool[Random.Range(0, pool.Length)];
        if (clip == null) return;
        leftFoot = !leftFoot;
        PirateArenaAudio.PlayClip(clip, position, Mathf.Lerp(.28f, .52f, speed01) * (leftFoot ? 1f : .88f), Random.Range(.94f, 1.06f));
    }

    static AudioClip[] LoadPool(string prefix, int count)
    {
        AudioClip[] clips = new AudioClip[count];
        for (int i = 0; i < count; i++) clips[i] = Resources.Load<AudioClip>("PirateAudio/" + prefix + i.ToString("000"));
        return clips;
    }

    static Floor PickFloor(Vector3 position)
    {
        int count = Physics.RaycastNonAlloc(position + Vector3.up * 0.45f, Vector3.down, hits, 1.3f, ~0, QueryTriggerInteraction.Ignore);
        if (count == 0) return Floor.Wood;
        Collider surface = hits[0].collider;
        if (surface.attachedRigidbody != null) return Floor.Wood;
        if (surface.GetComponent<PirateArenaSurface>() != null) return Floor.Wood;
        return Floor.Stone;
    }
}
