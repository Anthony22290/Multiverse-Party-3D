using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Presentation layer for Arena del Caos. Reads round and fighter state (which online clients also
/// receive) and adds: countdown beeps and start bell, KO stars, throw trails, elimination and victory
/// cues with confetti, sea ambience, creaking ship, gulls, a looping original shanty (M toggles music)
/// and a light post-processing grade. It never changes gameplay state.
/// </summary>
public sealed class PirateArenaPresentation : MonoBehaviour
{
    PirateArenaDirector director;
    AudioSource sea, music;
    AudioClip[] creaks, gulls;
    float nextCreak, nextGull, duckUntil;
    int lastCount = -1;
    bool wasPlaying, wasFinished;
    bool musicOn = true;
    const string MusicPref = "ArenaDelCaos.Music";

    sealed class Watch
    {
        public PirateFighter fighter;
        public bool alive = true;
        public float stunnedSince = -1f;
        public bool starsShown;
        public GameObject stars;
        public TrailRenderer trail;
    }
    readonly List<Watch> watched = new();
    readonly Dictionary<Rigidbody, TrailRenderer> propTrails = new();
    Material starMaterial, trailMaterial;

    void Start()
    {
        director = FindAnyObjectByType<PirateArenaDirector>();
        musicOn = PlayerPrefs.GetInt(MusicPref, 1) == 1;
        sea = Loop("PirateAudio/v2/amb_sea", .32f);
        music = Loop("PirateAudio/v2/music_shanty", musicOn ? .2f : 0f);
        creaks = Load("creak_", 3); gulls = Load("gull_", 3);
        nextCreak = Time.time + 3f; nextGull = Time.time + 6f;
        starMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Estrellas KO" };
        starMaterial.SetColor("_BaseColor", new Color(1f, .86f, .25f));
        starMaterial.EnableKeyword("_EMISSION"); starMaterial.SetColor("_EmissionColor", new Color(1.6f, 1.2f, .3f));
        trailMaterial = Resources.Load<Material>("ArenaFx/Particula brillante");
        if (trailMaterial == null) { trailMaterial = new Material(Shader.Find("Sprites/Default")); }
        SetupGrade();
    }

    AudioSource Loop(string path, float volume)
    {
        var clip = Resources.Load<AudioClip>(path);
        var s = gameObject.AddComponent<AudioSource>();
        s.clip = clip; s.loop = true; s.spatialBlend = 0f; s.volume = volume; s.playOnAwake = false;
        if (clip != null) s.Play();
        return s;
    }

    static AudioClip[] Load(string prefix, int count)
    {
        var list = new List<AudioClip>();
        for (int i = 0; i < count; i++) { var c = Resources.Load<AudioClip>("PirateAudio/v2/" + prefix + i); if (c != null) list.Add(c); }
        return list.ToArray();
    }

    /// <summary>Soft bloom on lanterns and fire, slightly richer colors, gentle vignette.</summary>
    void SetupGrade()
    {
        var cam = director != null ? director.ArenaCamera : Camera.main;
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null) data.renderPostProcessing = true;
        var volumeGo = new GameObject("Arena · post-procesado");
        volumeGo.transform.SetParent(transform, false);
        var volume = volumeGo.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 5;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>(); profile.name = "Arena grade (runtime)";
        var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.55f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.6f);
        var color = profile.Add<ColorAdjustments>(true); color.saturation.Override(12f); color.contrast.Override(8f); color.postExposure.Override(.08f);
        var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.2f); vignette.smoothness.Override(.45f);
        volume.sharedProfile = profile;
    }

    void Update()
    {
        if (director == null) { director = FindAnyObjectByType<PirateArenaDirector>(); if (director == null) return; }
        Keyboard keys = Keyboard.current;
        bool menu = PirateSession.Instance != null && PirateSession.Instance.MenuOpen;
        if (keys != null && !menu && keys.mKey.wasPressedThisFrame)
        {
            musicOn = !musicOn; PlayerPrefs.SetInt(MusicPref, musicOn ? 1 : 0);
            director.ShowEvent(musicOn ? "Música activada (M)" : "Música desactivada (M)");
        }
        if (music != null)
        {
            float target = !musicOn ? 0f : Time.time < duckUntil ? .05f : .2f;
            music.volume = Mathf.MoveTowards(music.volume, target, Time.unscaledDeltaTime * .5f);
        }
        RoundCues();
        Ambience();
        FighterCues();
        PropTrails();
    }

    void RoundCues()
    {
        if (!director.IsPlaying && !director.Finished)
        {
            int c = Mathf.CeilToInt(director.Countdown);
            if (c != lastCount && c >= 1 && c <= 3) PirateArenaAudio.PlayUI(PirateArenaAudio.Sound.Count);
            lastCount = c;
        }
        if (director.IsPlaying && !wasPlaying)
        {
            PirateArenaAudio.PlayUI(PirateArenaAudio.Sound.Go);
            director.ShowEvent("¡AL ABORDAJE!");
            lastCount = -1;
        }
        if (director.Finished && !wasFinished)
        {
            PirateArenaAudio.PlayUI(PirateArenaAudio.Sound.Victory);
            duckUntil = Time.time + 3.5f;
            var w = director.Winner;
            PirateArenaFx.Confetti(w != null ? w.transform.position + Vector3.up : new Vector3(0, director.DeckY + 1.5f, director.Art != null && director.Art.Map != null ? director.Art.Map.CenterCourtZ : 0f));
        }
        if (!director.Finished && wasFinished) lastCount = -1;
        wasPlaying = director.IsPlaying; wasFinished = director.Finished;
    }

    void Ambience()
    {
        if (creaks.Length > 0 && Time.time >= nextCreak)
        {
            nextCreak = Time.time + Random.Range(4.5f, 9f);
            var p = new Vector3(Random.Range(-5f, 5f), director.DeckY - .5f, Random.Range(-18f, 18f));
            PirateArenaAudio.PlayClip(creaks[Random.Range(0, creaks.Length)], p, .45f, Random.Range(.85f, 1.1f));
        }
        if (gulls.Length > 0 && Time.time >= nextGull)
        {
            nextGull = Time.time + Random.Range(9f, 18f);
            float a = Random.value * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(a) * 22f, 14f, Mathf.Sin(a) * 22f);
            PirateArenaAudio.PlayClip(gulls[Random.Range(0, gulls.Length)], p, .5f, Random.Range(.92f, 1.08f));
        }
    }

    void FighterCues()
    {
        var fighters = director.Fighters;
        while (watched.Count < fighters.Count) watched.Add(new Watch { fighter = fighters[watched.Count] });
        foreach (var w in watched)
        {
            var f = w.fighter; if (f == null) continue;
            // Elimination sting for the local player (splash and its visuals come from the director's sound).
            if (w.alive && !f.IsAlive && f.IsHuman) { PirateArenaAudio.PlayUI(PirateArenaAudio.Sound.Eliminated); duckUntil = Time.time + 2f; }
            w.alive = f.IsAlive;
            // KO stars: a long continuous stun (works on proxies, which only receive the stunned flag).
            bool stunned = f.IsAlive && f.IsStunned && !f.IsHeld;
            if (stunned && w.stunnedSince < 0f) w.stunnedSince = Time.time;
            if (!stunned) { w.stunnedSince = -1f; w.starsShown = false; }
            bool showStars = stunned && Time.time - w.stunnedSince > .9f;
            if (showStars && !w.starsShown) { w.starsShown = true; PirateArenaAudio.Play(PirateArenaAudio.Sound.KO, f.transform.position + Vector3.up, .9f); }
            if (showStars && w.stars == null) w.stars = MakeStars();
            if (w.stars != null)
            {
                w.stars.SetActive(showStars);
                if (showStars)
                {
                    w.stars.transform.position = f.transform.position + Vector3.up * 1.15f;
                    w.stars.transform.rotation = Quaternion.Euler(0f, Time.time * 240f, 0f);
                }
            }
            // Throw trail: a fast, airborne, helpless fighter streaks through the air.
            if (w.trail == null) w.trail = MakeTrail(f.gameObject, .45f);
            var v = f.VisualVelocity;
            w.trail.emitting = f.IsAlive && !f.IsGrounded && !f.IsHeld && f.IsStunned && v.magnitude > 6.5f;
        }
    }

    void PropTrails()
    {
        foreach (var prop in director.Props)
        {
            if (prop == null) continue;
            var body = prop.GetComponent<Rigidbody>(); if (body == null) continue;
            if (!propTrails.TryGetValue(body, out var trail)) propTrails[body] = trail = MakeTrail(prop.gameObject, .35f);
            trail.emitting = !body.isKinematic && body.linearVelocity.magnitude > 7f && prop.Holder == null;
        }
    }

    TrailRenderer MakeTrail(GameObject owner, float width)
    {
        var go = new GameObject("Estela de lanzamiento"); go.transform.SetParent(owner.transform, false);
        go.transform.localPosition = Vector3.up * .2f;
        var t = go.AddComponent<TrailRenderer>();
        t.time = .32f; t.minVertexDistance = .15f; t.emitting = false; t.numCapVertices = 2;
        t.widthCurve = AnimationCurve.EaseInOut(0f, width, 1f, 0f);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, .97f, .85f), 0f), new GradientColorKey(new Color(.7f, .9f, 1f), 1f) },
            new[] { new GradientAlphaKey(.75f, 0f), new GradientAlphaKey(0f, 1f) });
        t.colorGradient = g;
        if (trailMaterial != null) t.sharedMaterial = trailMaterial;
        t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    GameObject MakeStars()
    {
        var root = new GameObject("Estrellas KO");
        for (int i = 0; i < 3; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(root.transform, false);
            float a = i * Mathf.PI * 2f / 3f;
            s.transform.localPosition = new Vector3(Mathf.Cos(a) * .42f, Mathf.Sin(i * 2.1f) * .06f, Mathf.Sin(a) * .42f);
            s.transform.localRotation = Quaternion.Euler(45f, i * 40f, 45f);
            s.transform.localScale = Vector3.one * .13f;
            var r = s.GetComponent<Renderer>(); r.sharedMaterial = starMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return root;
    }
}
