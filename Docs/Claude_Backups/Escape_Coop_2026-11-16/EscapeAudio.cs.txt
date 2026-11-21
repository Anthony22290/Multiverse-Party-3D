using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small sound pool for the escape tower. Clips come from Resources/EscapeAudio (generated, original),
/// falling back to the shared Arena clips (Resources/PirateAudio/v2) for steps, jumps and countdown,
/// and to Resources/SurvivalAudio (Invasión Zombi).
/// </summary>
public sealed class EscapeAudio : MonoBehaviour
{
    static EscapeAudio instance;
    readonly Dictionary<string, AudioClip> clips = new();
    AudioSource[] voices;
    AudioSource ui, music;
    int next;
    public static bool MusicOn { get; private set; } = true;

    public static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("Escape Audio");
        instance = go.AddComponent<EscapeAudio>();
        instance.Init();
    }

    void Init()
    {
        voices = new AudioSource[18];
        for (int i = 0; i < voices.Length; i++)
        {
            var v = new GameObject("Voz " + i).AddComponent<AudioSource>();
            v.transform.SetParent(transform, false);
            v.playOnAwake = false; v.spatialBlend = .8f; v.rolloffMode = AudioRolloffMode.Linear;
            v.minDistance = 4f; v.maxDistance = 38f; v.dopplerLevel = 0f;
            voices[i] = v;
        }
        ui = gameObject.AddComponent<AudioSource>(); ui.playOnAwake = false; ui.spatialBlend = 0f;
        music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.loop = true; music.spatialBlend = 0f; music.volume = 0f;
        MusicOn = PlayerPrefs.GetInt("EscapeMusic", 1) == 1;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    AudioClip Clip(string name)
    {
        if (clips.TryGetValue(name, out var c)) return c;
        c = Resources.Load<AudioClip>("EscapeAudio/" + name);
        if (c == null) c = Resources.Load<AudioClip>("PirateAudio/v2/" + name);
        if (c == null) c = Resources.Load<AudioClip>("SurvivalAudio/" + name);
        clips[name] = c;
        return c;
    }

    public static void Play(string name, Vector3 at, float volume = 1f, float pitchVariation = .06f)
    {
        if (instance == null) return;
        var c = instance.Clip(name);
        if (c == null) return;
        var v = instance.voices[instance.next++ % instance.voices.Length];
        v.transform.position = at; v.clip = c; v.volume = volume;
        v.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        v.Play();
    }

    public static void PlayUI(string name, float volume = 1f)
    {
        if (instance == null) return;
        var c = instance.Clip(name);
        if (c != null) instance.ui.PlayOneShot(c, volume);
    }

    public static void Music(string name)
    {
        if (instance == null) return;
        var c = instance.Clip(name);
        if (c == null) return;
        instance.music.clip = c;
        instance.music.Play();
    }

    public static void ToggleMusic()
    {
        MusicOn = !MusicOn;
        PlayerPrefs.SetInt("EscapeMusic", MusicOn ? 1 : 0);
    }

    float duckUntil;
    public static void Duck(float seconds) { if (instance != null) instance.duckUntil = Time.unscaledTime + seconds; }

    void Update()
    {
        if (music == null) return;
        float target = !MusicOn ? 0f : Time.unscaledTime < duckUntil ? .05f : .22f;
        music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * .4f);
    }
}
