using UnityEngine;
using UnityEngine.InputSystem;

// Race music and announcer-style cues (original clips from Tools/generate_race_audio.py).
// M toggles every race sound; the choice persists between sessions.
public sealed class CrazyRaceAudio : MonoBehaviour
{
    public static bool Muted { get; private set; }
    CrazyKartDirector director;
    AudioSource music, cues;
    AudioClip beep, go, lap, final, win, finish, record, slip, hop, tick, itemLand, thunder;
    AudioClip[] tiers = new AudioClip[4];
    bool wasDrifting, wasSpinning; int lastTier; float lastTick, lastThunder = -100;
    int lastCount = 99, lastLap = 1;
    bool started, ended;
    float lastRecordCue = -100, lastSlipCue = -100;
    static AudioClip Load(string n) => Resources.Load<AudioClip>("RaceAudio/" + n);
    void Start()
    {
        director = GetComponent<CrazyKartDirector>();
        Muted = PlayerPrefs.GetInt("CarrerasLocas_Silencio", 0) == 1;
        music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.loop = true; music.spatialBlend = 0; music.volume = .2f;
        cues = gameObject.AddComponent<AudioSource>(); cues.playOnAwake = false; cues.spatialBlend = 0; cues.volume = .32f;
        beep = Load("beep"); go = Load("go"); lap = Load("lap"); final = Load("final"); win = Load("win"); finish = Load("finish"); record = Load("record"); slip = Load("slip"); hop = Load("hop"); tick = Load("tick"); itemLand = Load("itemland"); thunder = Load("thunder");
        for (int i = 1; i <= 3; i++) tiers[i] = Load("tier" + i);
        music.clip = Load("music");
        if (music.clip != null) music.Play();
    }
    void Cue(AudioClip c, float volume = 1) { if (c != null && cues != null) cues.PlayOneShot(c, volume); }
    CrazyKartController Player()
    {
        if (director == null) return null;
        foreach (var k in director.Karts) if (k.IsHuman) return k;
        return null;
    }
    void Update()
    {
        if (director == null) return;
        var k = Keyboard.current;
        if (k != null && k.mKey.wasPressedThisFrame)
        { Muted = !Muted; PlayerPrefs.SetInt("CarrerasLocas_Silencio", Muted ? 1 : 0); }
        music.mute = cues.mute = Muted;
        if (director.Countdown > 0 && director.Countdown <= 3)
        {
            int c = Mathf.CeilToInt(director.Countdown);
            if (c != lastCount) { lastCount = c; Cue(beep); }
        }
        if (!started && director.Playing) { started = true; Cue(go); }
        var player = Player();
        if (player != null)
        {
            if (player.CurrentLap != lastLap && !player.HasFinished)
            {
                lastLap = player.CurrentLap;
                Cue(player.CurrentLap == director.laps ? final : lap);
                if (player.CurrentLap == director.laps) music.pitch = 1.06f;
            }
            if (player.LapRecordTime > lastRecordCue && Time.time - player.LapRecordTime < .5f) { lastRecordCue = player.LapRecordTime; Cue(record, .8f); }
            if (player.LastSlipstreamBoost > lastSlipCue && Time.time - player.LastSlipstreamBoost < .3f) { lastSlipCue = player.LastSlipstreamBoost; Cue(slip, .7f); }
        }
        if (player != null)
        {
            if (player.Drifting && !wasDrifting) Cue(hop, .6f);
            int tier = player.DriftTier;
            if (player.Drifting && tier > lastTier && tier > 0) Cue(tiers[tier], .7f);
            lastTier = player.Drifting ? tier : 0; wasDrifting = player.Drifting;
            if (player.RouletteSpinning && Time.time - lastTick > .07f) { lastTick = Time.time; Cue(tick, .45f); }
            if (wasSpinning && !player.RouletteSpinning) Cue(itemLand, .8f);
            wasSpinning = player.RouletteSpinning;
        }
        foreach (var kart in director.Karts)
            if (kart.LastStormTime > lastThunder && Time.time - kart.LastStormTime < .3f) { lastThunder = kart.LastStormTime; Cue(thunder, 1); }
        if (!ended && director.Finished)
        {
            ended = true;
            Cue(player != null && director.Position(player) == 1 ? win : finish);
            music.volume = .08f; music.pitch = 1;
        }
    }
}
