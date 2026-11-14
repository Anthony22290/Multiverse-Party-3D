using UnityEngine;
using UnityEngine.InputSystem;

// Music and cues for Código Mortal (original clips from Tools/generate_minigame_audio.py). M mutes.
public sealed class MortalCodeAudio : MonoBehaviour
{
    static MortalCodeAudio instance;
    public static bool Muted { get; private set; }
    AudioSource music, ui; AudioSource[] voices; int next;
    readonly System.Collections.Generic.Dictionary<string, AudioClip> clips = new();
    void Awake()
    {
        instance = this; Muted = PlayerPrefs.GetInt("CodigoMortal_Silencio", 0) == 1;
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.playOnAwake = false; music.spatialBlend = 0; music.volume = .18f;
        ui = gameObject.AddComponent<AudioSource>(); ui.playOnAwake = false; ui.spatialBlend = 0; ui.volume = .5f;
        voices = new AudioSource[10];
        for (int i = 0; i < voices.Length; i++)
        {
            var v = new GameObject("Voz " + i).AddComponent<AudioSource>(); v.transform.SetParent(transform, false);
            v.playOnAwake = false; v.spatialBlend = .85f; v.rolloffMode = AudioRolloffMode.Linear; v.minDistance = 5; v.maxDistance = 55; v.dopplerLevel = 0; voices[i] = v;
        }
        music.clip = Clip("music"); if (music.clip != null) music.Play();
    }
    void OnDestroy() { if (instance == this) instance = null; }
    AudioClip Clip(string n) { if (!clips.TryGetValue(n, out var c)) clips[n] = c = Resources.Load<AudioClip>("MortalAudio/" + n); return c; }
    public static void UI(string name, float volume = 1) { if (instance != null && instance.Clip(name) != null) instance.ui.PlayOneShot(instance.Clip(name), volume); }
    public static void At(string name, Vector3 p, float volume = 1)
    {
        if (instance == null || instance.Clip(name) == null) return;
        var v = instance.voices[instance.next++ % instance.voices.Length]; v.transform.position = p; v.clip = instance.Clip(name); v.volume = volume; v.pitch = Random.Range(.95f, 1.05f); v.Play();
    }
    public static void Tension(bool on) { if (instance != null) instance.music.pitch = on ? 1.08f : 1; }
    public static void FadeMusic() { if (instance != null) instance.music.volume = .05f; }
    void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.mKey.wasPressedThisFrame) { Muted = !Muted; PlayerPrefs.SetInt("CodigoMortal_Silencio", Muted ? 1 : 0); }
        music.mute = ui.mute = Muted; foreach (var v in voices) v.mute = Muted;
    }
}
