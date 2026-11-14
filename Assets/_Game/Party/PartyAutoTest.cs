#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Development builds only (2026-11-12): an automatic client for two-instance network tests.
///   -party-join CODE     join that party room (retries until the host has created it)
///   -party-probe FILE    every 2 s write what this computer sees (scene, phase, minigame state) and take screenshots
///   -party-autohuman     a bot plays this computer's character in Invasión Zombi and Sala de Escape
/// Without these arguments it does nothing.
/// </summary>
public sealed class PartyAutoTest : MonoBehaviour
{
    string code, probe, shots;
    bool autoHuman, joining, joined;
    float nextProbe, nextJoin, sceneAt;
    string lastScene = "";
    int shot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var args = Environment.GetCommandLineArgs();
        string Arg(string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        string join = Arg("-party-join"), file = Arg("-party-probe");
        if (join == null && file == null) return;
        var go = new GameObject("Prueba automática de red");
        DontDestroyOnLoad(go);
        var t = go.AddComponent<PartyAutoTest>();
        t.code = join; t.probe = file; t.autoHuman = args.Contains("-party-autohuman");
        if (file != null) { t.shots = Path.Combine(Path.GetDirectoryName(file), "capturas_cliente"); Directory.CreateDirectory(t.shots); File.WriteAllText(file, "Cliente de prueba " + DateTime.Now + "\n"); }
        SceneManager.sceneLoaded += (s, m) => t.OnScene(s);
        Application.logMessageReceived += (msg, stack, type) => { if (type == LogType.Error || type == LogType.Exception) t.Log("ERROR " + msg + "\n" + stack); };
        Application.runInBackground = true;
    }

    void Log(string line)
    {
        if (probe == null) return;
        try { File.AppendAllText(probe, $"[{Time.realtimeSinceStartup:0.0}] {line}\n"); } catch { }
    }

    void OnScene(Scene s)
    {
        sceneAt = Time.realtimeSinceStartup; shot = 0;
        Log("ESCENA " + s.name);
        if (!autoHuman) return;
        var z = FindAnyObjectByType<SurvivalDirector>(); if (z != null) z.AutoHuman = true;
        var e = FindAnyObjectByType<EscapeDirector>(); if (e != null) e.AutoHuman = true;
    }

    async void Update()
    {
        if (code != null && !joined && !joining && Time.realtimeSinceStartup > Mathf.Max(nextJoin, 8f) && SceneManager.GetActiveScene().name == MultiverseParty.MenuScene)
        {
            joining = true; nextJoin = Time.realtimeSinceStartup + 4f;
            MultiverseParty.SavedName = "Cliente";
            bool ok = await MultiverseParty.StartOnline(false, code, 4);
            joined = ok; joining = false;
            Log(ok ? "UNIDO a " + code : "no se pudo unir: " + MultiverseParty.LastStatus);
        }
        if (probe == null || Time.realtimeSinceStartup < nextProbe) return;
        nextProbe = Time.realtimeSinceStartup + 2f;
        Log(Describe());
        float age = Time.realtimeSinceStartup - sceneAt;
        string scene = SceneManager.GetActiveScene().name;
        if (scene != MultiverseParty.MenuScene && (shot == 0 && age > 12f || shot == 1 && age > 24f))
        {
            shot++;
            ScreenCapture.CaptureScreenshot(Path.Combine(shots, $"{scene}_{shot}.png"));
        }
    }

    static string Describe()
    {
        var p = MultiverseParty.Instance;
        var sb = new StringBuilder();
        sb.Append(SceneManager.GetActiveScene().name);
        if (p != null) sb.Append($" · fase {p.Phase} ronda {p.Round} miembros {p.Members.Count} (humanos {p.Members.Count(m => !m.Bot)}) yo={p.LocalIndex} red={PartyLink.Active} semilla={p.Seed}");
        var mortal = FindAnyObjectByType<MortalCodeDirector>();
        if (mortal != null)
            sb.Append($" | MORTAL cuenta {mortal.Countdown:0.0} resto {mortal.Remaining:0} vivos {mortal.AliveCount} títeres {mortal.Actors.Count(a => a.Puppet)} con datos {mortal.Actors.Count(a => a.Puppet && Time.time - a.NetPose.LastReceived < 1f)} códigos {mortal.Actors.Count(a => !string.IsNullOrEmpty(mortal.CodeOf(a)))} fin={mortal.Finished}");
        var race = FindAnyObjectByType<CrazyKartDirector>();
        if (race != null)
            sb.Append($" | CARRERA empezó {race.Net?.Started} t {race.RoundTime:0.0} títeres {race.Karts.Count(k => k.Puppet)} vel títeres {string.Join("/", race.Karts.Where(k => k.Puppet).Select(k => k.Speed.ToString("0")))} vueltas {string.Join("/", race.Karts.Select(k => k.CurrentLap + ":" + k.CurrentCheckpoint))} fin={race.Finished}");
        var arena = FindAnyObjectByType<PirateArenaDirector>();
        if (arena != null)
            sb.Append($" | ARENA jugando {arena.IsPlaying} fin {arena.Finished} cuenta {arena.Countdown:0.0} pos {string.Join(" ", arena.Fighters.Select(f => f.transform.position.x.ToString("0.0") + "," + f.transform.position.z.ToString("0.0")))}");
        var zombie = FindAnyObjectByType<SurvivalDirector>();
        if (zombie != null)
            sb.Append($" | ZOMBI fase {zombie.State} oleada {zombie.Wave} zombis {SurvivalZombie.All.Count} quedan {zombie.ZombiesLeft} piezas {zombie.PartsInstalled} vida {string.Join("/", zombie.Team.Select(t => t.Health.ToString("0")))} bajas {zombie.TotalKills} aviso '{zombie.Notice}'");
        var escape = FindAnyObjectByType<EscapeDirector>();
        if (escape != null)
            sb.Append($" | ESCAPE fase {escape.State} sala {escape.ActiveRoomIndex + 1}/{escape.Rooms?.Length} resueltas {escape.RoomsSolved} t {escape.Elapsed:0} salas [{(escape.Rooms != null ? string.Join(",", escape.Rooms.Select(r => r.GetType().Name.Replace("Room", ""))) : "")}] títeres {escape.Actors.Count(a => a.Puppet)} aviso '{escape.Notice}'");
        return sb.ToString();
    }
}
#endif
