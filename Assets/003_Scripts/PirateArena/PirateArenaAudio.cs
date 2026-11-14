using UnityEngine;

/// <summary>
/// Pooled spatial sound system shared by the arena's local and replicated visuals.
/// v2: several variations per sound (Resources/PirateAudio/v2), per-sound mix levels, more voices,
/// and a hook so every sound event also spawns its visual effect (works for online proxies too,
/// because proxies already replay the same sound events).
/// </summary>
public sealed class PirateArenaAudio : MonoBehaviour
{
    // Append-only: existing values keep their meaning.
    public enum Sound { Step, Grab, Push, Throw, Jump, Land, Impact, CannonWarn, CannonShot, CannonBlast, SharkWarn, SharkLeap, Splash,
        Punch, WoodHit, KO, Count, Go, Victory, Eliminated }
    static readonly string[] Legacy = { "step", "grab", "push", "throw", "jump", "land", "impact", "cannon_warn", "cannon_shot", "cannon_blast", "shark_warn", "shark_leap", "splash",
        "impact", "impact", "impact", "jump", "jump", "jump", "splash" };
    // v2 clip names; "name_" means numbered variations name_0, name_1, ...
    static readonly string[] V2 = { "step_", "grab_", "whoosh_", "whoosh_", "jump_", "land_", "impact_", "cannon_warn", "cannon_shot", "cannon_blast", "shark_warn", "shark_leap", "splash_",
        "punch_", "wood_", "ko", "count", "go", "victory", "eliminated" };
    static readonly float[] Level = { .5f, .55f, .6f, .7f, .45f, .7f, .8f, .75f, 1f, 1f, .8f, .9f, .95f,
        .85f, .6f, .8f, .55f, .75f, .8f, .8f };
    static PirateArenaAudio instance;
    AudioClip[][] clips;
    AudioSource[] voices;
    AudioSource ui;
    int nextVoice;
    public static PirateArenaAudio Instance => instance;

    void Awake()
    {
        instance = this;
        int n = System.Enum.GetValues(typeof(Sound)).Length;
        clips = new AudioClip[n][];
        for (int i = 0; i < n; i++) clips[i] = LoadVariants(V2[i], Legacy[i]);
        voices = new AudioSource[24];
        for (int i = 0; i < voices.Length; i++)
        {
            var child = new GameObject("SFX " + i); child.transform.SetParent(transform, false);
            var voice = child.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = .78f; voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = 4f; voice.maxDistance = 48f; voice.dopplerLevel = 0f;
            voices[i] = voice;
        }
        ui = gameObject.AddComponent<AudioSource>(); ui.playOnAwake = false; ui.spatialBlend = 0f;
        // Ambience, music, round presentation and particle effects live next to the mixer.
        // Kept on their own objects: tests and tools treat every child of this object as an SFX voice.
        new GameObject("Pirate Arena FX").AddComponent<PirateArenaFx>();
        new GameObject("Pirate Arena Presentation").AddComponent<PirateArenaPresentation>();
    }

    static AudioClip[] LoadVariants(string v2, string legacy)
    {
        if (v2.EndsWith("_"))
        {
            var list = new System.Collections.Generic.List<AudioClip>();
            for (int i = 0; i < 6; i++) { var c = Resources.Load<AudioClip>("PirateAudio/v2/" + v2 + i); if (c == null) break; list.Add(c); }
            if (list.Count > 0) return list.ToArray();
        }
        else { var c = Resources.Load<AudioClip>("PirateAudio/v2/" + v2); if (c != null) return new[] { c }; }
        var old = Resources.Load<AudioClip>("PirateAudio/" + legacy);
        return old != null ? new[] { old } : new AudioClip[0];
    }

    void OnDestroy() { if (instance == this) instance = null; }

    static AudioClip Pick(Sound sound)
    {
        if (instance == null) return null;
        var pool = instance.clips[(int)sound];
        return pool.Length == 0 ? null : pool[Random.Range(0, pool.Length)];
    }

    public static void Play(Sound sound, Vector3 position, float volume = 1f)
    {
        if (instance == null) return;
        PlayClip(Pick(sound), position, volume * Level[(int)sound]);
        PirateArenaFx.OnSound(sound, position, volume);
    }

    /// <summary>Non-spatial cue (countdown, fanfare, elimination sting).</summary>
    public static void PlayUI(Sound sound, float volume = 1f)
    {
        if (instance == null) return;
        var clip = Pick(sound); if (clip == null) return;
        instance.ui.PlayOneShot(clip, volume * Level[(int)sound]);
    }

    public static void PlayClip(AudioClip clip, Vector3 position, float volume = 1f, float pitchScale = 1f)
    {
        if (instance == null || clip == null) return;
        AudioSource voice = null;
        for (int i = 0; i < instance.voices.Length; i++)
        {
            var candidate = instance.voices[(instance.nextVoice + i) % instance.voices.Length];
            if (!candidate.isPlaying) { voice = candidate; instance.nextVoice = (instance.nextVoice + i + 1) % instance.voices.Length; break; }
        }
        if (voice == null) { voice = instance.voices[instance.nextVoice]; instance.nextVoice = (instance.nextVoice + 1) % instance.voices.Length; voice.Stop(); }
        voice.transform.position = position;
        voice.pitch = Random.Range(.94f, 1.06f) * pitchScale; voice.volume = Mathf.Clamp01(volume);
        voice.clip = clip; voice.Play();
    }

    public static void PlayAction(PirateFighterAnimation.Action action, Vector3 position)
    {
        switch (action)
        {
            case PirateFighterAnimation.Action.Reach: Play(Sound.Grab, position, .8f); break;
            case PirateFighterAnimation.Action.Push: Play(Sound.Push, position, .8f); break;
            case PirateFighterAnimation.Action.Throw: Play(Sound.Throw, position, 1f); break;
            case PirateFighterAnimation.Action.Jump: Play(Sound.Jump, position, .8f); break;
            case PirateFighterAnimation.Action.Land: Play(Sound.Land, position, .9f); break;
        }
    }
}
