using System.Collections.Generic;
using UnityEngine;

/// <summary>Rodillos del Calabozo · sounds from Resources/RodillosAudio (Tools/generate_rodillos_audio.py) and the music loop.</summary>
public static class RodillosAudio
{
    static readonly Dictionary<string, AudioClip> clips = new();
    static AudioSource ui, music;

    public static AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var c) || c == null) clips[name] = c = Resources.Load<AudioClip>("RodillosAudio/" + name);
        return c;
    }

    static void Ensure()
    {
        if (ui != null) return;
        var go = new GameObject("Audio de los rodillos");
        ui = go.AddComponent<AudioSource>(); ui.spatialBlend = 0f; ui.playOnAwake = false;
        music = go.AddComponent<AudioSource>(); music.spatialBlend = 0f; music.loop = true; music.playOnAwake = false;
        MultiverseSettings.Changed += Refresh;
    }
    static void Refresh() { if (music != null) music.volume = .42f * MultiverseSettings.MusicVolume; }

    public static void UI(string name, float volume = 1f) { Ensure(); var c = Clip(name); if (c != null) ui.PlayOneShot(c, volume); }

    public static void Play(string name, Vector3 at, float volume = 1f)
    {
        var c = Clip(name); if (c == null) return;
        var go = new GameObject("Sonido " + name); go.transform.position = at;
        var s = go.AddComponent<AudioSource>(); s.clip = c; s.spatialBlend = .75f; s.minDistance = 5f; s.maxDistance = 40f; s.volume = volume; s.pitch = Random.Range(.94f, 1.06f);
        s.Play(); Object.Destroy(go, c.length / s.pitch + .1f);
    }

    public static void Music(bool on)
    {
        Ensure();
        if (on) { if (music.clip == null) music.clip = Clip("music"); Refresh(); if (music.clip != null && !music.isPlaying) music.Play(); }
        else music.Stop();
    }
    public static void FadeMusic(float volume) { if (music != null) music.volume = volume * MultiverseSettings.MusicVolume; }
    public static void Release() { MultiverseSettings.Changed -= Refresh; ui = null; music = null; }
}
