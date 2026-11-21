using System.Collections.Generic;
using UnityEngine;

/// <summary>Simon · sounds from Resources/SimonAudio (Tools/generate_patata_audio.py) and the music loop.</summary>
public static class SimonAudio
{
    static readonly Dictionary<string, AudioClip> clips = new();
    static AudioSource ui, music;

    public static AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out var c) || c == null) clips[name] = c = Resources.Load<AudioClip>("SimonAudio/" + name);
        return c;
    }
    static void Ensure()
    {
        if (ui != null) return;
        var go = new GameObject("Audio de Simon");
        ui = go.AddComponent<AudioSource>(); ui.spatialBlend = 0f; ui.playOnAwake = false;
        music = go.AddComponent<AudioSource>(); music.spatialBlend = 0f; music.loop = true; music.playOnAwake = false;
        MultiverseSettings.Changed += Refresh;
    }
    static void Refresh() { if (music != null) music.volume = .4f * MultiverseSettings.MusicVolume; }
    public static void UI(string name, float volume = 1f) { Ensure(); var c = Clip(name); if (c != null) ui.PlayOneShot(c, volume); }
    public static void Play(string name, float volume = 1f) { Ensure(); var c = Clip(name); if (c != null) ui.PlayOneShot(c, volume); }
    public static void Music(bool on)
    {
        Ensure();
        if (on) { if (music.clip == null) music.clip = Clip("music"); Refresh(); if (music.clip != null && !music.isPlaying) music.Play(); }
        else music.Stop();
    }
    public static void FadeMusic(float volume) { if (music != null) music.volume = volume * MultiverseSettings.MusicVolume; }
    public static void Release() { MultiverseSettings.Changed -= Refresh; ui = null; music = null; }
}
