#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Two-instance network test (2026-11-12): the editor hosts, a development build joins as client
/// (PartyAutoTest: -party-join / -party-probe / -party-autohuman) and both play each minigame for a while.
/// Usage (ClaudeBridge, in steps so each one stays under the bridge's async limit):
///   run NetDuoTest.Build (edit mode) → open MenuPrincipal → play → run NetDuoTest.Setup →
///   run NetDuoTest.Mortal / Race / Arena / Zombie / Escape → run NetDuoTest.End
/// The client writes Docs/Claude_Pruebas/Red_cliente.txt and screenshots in Docs/Claude_Pruebas/capturas_cliente.
/// </summary>
public static class NetDuoTest
{
    const string Folder = "Builds/ClaudeRedPrueba", Exe = Folder + "/MultiverseParty3D.exe";
    static string code;
    static Process client;
    static readonly List<string> report = new();

    public static string Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = Exe, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
        });
        return $"{r.summary.result} · {r.summary.totalTime.TotalSeconds:0} s · errores {r.summary.totalErrors} · {scenes.Length} escenas";
    }

    static string extraArgs = "";
    /// <summary>Same as Setup, but the client leaves in the middle of the first minigame and comes back (2026-11-21).</summary>
    public static Task<string[]> SetupRejoin() { extraArgs = " -party-rejoin"; return Setup(); }

    public static async Task<string[]> Setup()
    {
        report.Clear();
        if (!Application.isPlaying) return new[] { "FAIL no está en Play" };
        Directory.CreateDirectory("Docs/Claude_Pruebas");
        code = "CL" + Random.Range(1000, 9999);
        MultiverseParty.SavedName = "Anfitrion";
        if (!await MultiverseParty.StartOnline(true, code, 4)) return new[] { "FAIL sala: " + MultiverseParty.LastStatus };
        var party = MultiverseParty.Instance;
        string probe = Path.GetFullPath("Docs/Claude_Pruebas/Red_cliente.txt");
        foreach (var old in Directory.GetFiles(Path.GetFullPath("Docs/Claude_Pruebas")).Where(f => f.EndsWith(".png"))) File.Delete(old);
        var info = new ProcessStartInfo(Path.GetFullPath(Exe),
            $"-party-join {code} -party-probe \"{probe}\" -party-autohuman -screen-fullscreen 0 -screen-width 960 -screen-height 540" + extraArgs) { UseShellExecute = false };
        extraArgs = "";
        client = Process.Start(info);
        Add($"PASS Sala {code} creada y cliente lanzado (pid {client?.Id})");
        if (!await Until(() => party.Members.Count(m => !m.Bot) >= 2, 60f)) return Done("FAIL el cliente no se unió");
        var guest = party.Members.First(m => !m.Bot && m.Owner != party.Runner.LocalPlayer);
        Add($"PASS {guest.Name} se unió · miembros {party.Members.Count}");
        if (!await Until(() => SceneManager.GetActiveScene().name == MultiverseParty.HubScene, 20f)) return Done("FAIL no cargó la plaza");
        await Task.Delay(1500);
        party.HostStartMatch();
        Add($"PASS Partida iniciada · {party.Members.Count} miembros ({party.Members.Count(m => m.Bot)} bots)");
        return Done(null);
    }

    public static Task<string[]> Mortal() => Play(PartyGames.Mortal, 20);
    public static Task<string[]> Race() => Play(PartyGames.Race, 22);
    public static Task<string[]> Arena() => Play(PartyGames.Arena, 18);
    public static Task<string[]> Zombie() => Play(PartyGames.Zombie, 40);
    public static Task<string[]> Escape() => Play(PartyGames.Escape, 70);
    public static Task<string[]> Hill() => Play(PartyGames.Hill, 25);

    /// <summary>Run after SetupRejoin: the guest drops mid-minigame, comes back, waits, and gets its own seat and points.</summary>
    public static async Task<string[]> Rejoin()
    {
        var party = MultiverseParty.Instance;
        if (party == null) return new[] { "FAIL sin partida (ejecuta SetupRejoin)" };
        report.Clear();
        if (!await Until(() => party.Phase == PartyPhase.Voting && !party.Loading && SceneManager.GetActiveScene().name == MultiverseParty.HubScene, 45f)) return Done("FAIL no volvió la votación");
        int guest = Enumerable.Range(0, party.Members.Count).First(i => !party.Members[i].Bot && i != party.LocalIndex);
        var g = party.Members[guest];
        g.Score = 2;   // points to keep
        string name = g.Name;
        party.Selected = PartyGames.Hill; party.Phase = PartyPhase.Reveal;
        typeof(MultiverseParty).GetField("phaseEnds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(party, Time.unscaledTime);
        if (!await Until(() => SceneManager.GetActiveScene().name == PartyGames.All[PartyGames.Hill].Scene && !party.Loading, 40f)) return Done("FAIL no cargó la colina");
        if (!await Until(() => g.Bot, 40f)) return Done("FAIL el invitado no se desconectó");
        Add($"PASS {name} se desconectó a mitad del minijuego · lo sustituye el bot {g.Name} · puntos del hueco {g.Score}");
        if (!await Until(() => g.Reserved != Fusion.PlayerRef.None, 60f)) return Done("FAIL el invitado no volvió a entrar");
        Add($"PASS {g.PendingName} volvió y espera su sitio · sigue de bot mientras dura el minijuego: {g.Bot} · fase {party.Phase}");
        await Task.Delay(4000);
        Add($"INFO durante la espera: el hueco sigue siendo bot={g.Bot} ({g.Name})");
        party.HostResolve(-1);
        if (!await Until(() => !g.Bot, 15f)) return Done("FAIL no recuperó su sitio al acabar el minijuego");
        Add($"{(g.Name == name && g.Score == 2 ? "PASS" : "FAIL")} recuperó su sitio: hueco {guest} · nombre {g.Name} · puntos {g.Score} (esperados 2)");
        return Done(null);
    }
    public static Task<string[]> Potato() => Play(PartyGames.Potato, 25);

    static async Task<string[]> Play(int game, int seconds)
    {
        var party = MultiverseParty.Instance;
        if (party == null) return new[] { "FAIL sin partida (ejecuta Setup)" };
        report.Clear();
        if (!await Until(() => party.Phase == PartyPhase.Voting && !party.Loading && SceneManager.GetActiveScene().name == MultiverseParty.HubScene, 45f)) return Done("FAIL no volvió la votación");
        party.Selected = game; party.Phase = PartyPhase.Reveal;
        typeof(MultiverseParty).GetField("phaseEnds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(party, Time.unscaledTime);
        string scene = PartyGames.All[game].Scene;
        if (!await Until(() => SceneManager.GetActiveScene().name == scene && !party.Loading, 40f)) return Done("FAIL no cargó " + scene);
        float start = Time.realtimeSinceStartup;
        int guest = Enumerable.Range(0, party.Members.Count).First(i => !party.Members[i].Bot && i != party.LocalIndex);
        int slot = PartyLink.SlotOfMember(guest);
        Vector3? first = null;
        await Task.Delay(6000);
        first = GuestPosition(game, slot);
        await Task.Delay(Mathf.Max(1000, seconds * 1000 - 6000));
        var cam = Camera.main;
        if (cam != null) ClaudeBridge.CaptureCamera(cam, Path.GetFullPath($"Docs/Claude_Pruebas/anfitrion_{scene}.png"), 1280, 720);
        var last = GuestPosition(game, slot);
        float moved = first.HasValue && last.HasValue ? Vector3.Distance(first.Value, last.Value) : -1f;
        Add($"INFO {PartyGames.All[game].Title}: listos={PartyLink.AllReady} · ping invitado {party.Members[guest].Ping} ms · invitado en el hueco {slot} · se movió {moved:0.0} m · {Describe(game, slot)}");
        party.HostResolve(guest);
        if (!await Until(() => party.Phase == PartyPhase.Results, 5f)) return Done("FAIL no se resolvió la ronda");
        Add($"PASS Ronda resuelta: {party.ResultText}");
        return Done(null);
    }

    static Vector3? GuestPosition(int game, int slot)
    {
        switch (game)
        {
            case PartyGames.Mortal: { var d = Object.FindAnyObjectByType<MortalCodeDirector>(); return d != null && slot < d.Actors.Count ? d.Actors[slot].transform.position : null; }
            case PartyGames.Race: { var d = Object.FindAnyObjectByType<CrazyKartDirector>(); var k = d?.KartOfSlot(slot); return k != null ? k.transform.position : null; }
            case PartyGames.Arena: { var d = Object.FindAnyObjectByType<PirateArenaDirector>(); return d != null && slot < d.Fighters.Count ? d.Fighters[slot].transform.position : null; }
            case PartyGames.Zombie: { var d = Object.FindAnyObjectByType<SurvivalDirector>(); return d != null && slot < d.Team.Count ? d.Team[slot].transform.position : null; }
            case PartyGames.Escape: { var d = Object.FindAnyObjectByType<EscapeDirector>(); return d != null && slot < d.Actors.Count ? d.Actors[slot].transform.position : null; }
            case PartyGames.Hill: { var d = Object.FindAnyObjectByType<ColinaDirector>(); return d != null && slot < d.Players.Count ? d.Players[slot].transform.position : null; }
            case PartyGames.Potato: { var d = Object.FindAnyObjectByType<PatataDirector>(); return d != null && slot < d.Players.Count ? d.Players[slot].transform.position : null; }
        }
        return null;
    }

    static string Describe(int game, int slot)
    {
        switch (game)
        {
            case PartyGames.Mortal:
            {
                var d = Object.FindAnyObjectByType<MortalCodeDirector>(); var a = d.Actors[slot];
                return $"corriendo {d.Running} · invitado títere={a.Puppet} último dato hace {Time.time - a.NetPose.LastReceived:0.00} s · vivos {d.AliveCount}";
            }
            case PartyGames.Race:
            {
                var d = Object.FindAnyObjectByType<CrazyKartDirector>(); var k = d.KartOfSlot(slot);
                return $"empezó {d.Net?.Started} t {d.RoundTime:0.0} · kart invitado títere={k.Puppet} último dato hace {Time.time - k.NetPose.LastReceived:0.00} s · bots velocidad {string.Join("/", d.Karts.Where(x => !x.Puppet && !x.IsHuman).Select(x => x.Speed.ToString("0")))}";
            }
            case PartyGames.Arena:
            {
                var d = Object.FindAnyObjectByType<PirateArenaDirector>();
                return $"jugando {d.IsPlaying} fin {d.Finished} · pirata invitado vivo={d.Fighters[slot].IsAlive} remoto={PartyLink.IsRemoteHuman(slot)}";
            }
            case PartyGames.Zombie:
            {
                var d = Object.FindAnyObjectByType<SurvivalDirector>(); var p = d.Team[slot];
                return $"fase {d.State} oleada {d.Wave} · invitado en red={p.NetDriven} último dato hace {Time.time - p.NetPose.LastReceived:0.00} s vida {p.Health:0} bajas {p.Kills} golpes {p.SwingSeq} · zombis {SurvivalZombie.All.Count} piezas {d.PartsInstalled}";
            }
            case PartyGames.Hill:
            {
                var d = Object.FindAnyObjectByType<ColinaDirector>(); var pm = d.Players[slot];
                return $"fase {d.State} · invitado títere={pm.Puppet} último dato hace {Time.time - pm.Net.LastReceived:0.00} s";
            }
            case PartyGames.Escape:
            {
                var d = Object.FindAnyObjectByType<EscapeDirector>(); var a = d.Actors[slot];
                return $"fase {d.State} sala {d.ActiveRoomIndex + 1}/{d.Rooms.Length} resueltas {d.RoomsSolved} · invitado títere={a.Puppet} último dato hace {Time.time - a.NetPose.LastReceived:0.00} s acciones {a.Actions} resolvió {a.Solves} · salas [{string.Join(",", d.Rooms.Select(r => r.GetType().Name.Replace("Room", "")))}]";
            }
        }
        return "";
    }

    /// <summary>Closes only the client build (End() also leaves the party, which has crashed the editor twice).</summary>
    public static string[] KillClient()
    {
        int n = 0;
        foreach (var p in Process.GetProcessesByName("MultiverseParty3D"))
            try { if (p.MainModule != null && p.MainModule.FileName.Replace('\\', '/').Contains("ClaudeRedPrueba")) { p.Kill(); n++; } } catch { }
        return new[] { "clientes cerrados: " + n };
    }

    public static string[] End()
    {
        MultiverseParty.Instance?.LeaveToMenu("Prueba terminada");
        try { if (client != null && !client.HasExited) client.Kill(); } catch { }
        foreach (var p in Process.GetProcessesByName("MultiverseParty3D"))
            try { if (p.MainModule != null && p.MainModule.FileName.Replace('\\', '/').Contains("ClaudeRedPrueba")) p.Kill(); } catch { }
        return new[] { "cliente cerrado" };
    }

    static void Add(string line) { report.Add(line); Debug.Log("NetDuoTest: " + line); }
    static string[] Done(string fail)
    {
        if (fail != null) report.Add(fail);
        File.AppendAllLines("Docs/Claude_Pruebas/Red_dos_instancias.txt", report);
        return report.ToArray();
    }

    static async Task<bool> Until(System.Func<bool> condition, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Application.isPlaying && Time.realtimeSinceStartup < end)
        {
            if (condition()) return true;
            await Task.Delay(100);
        }
        return false;
    }
}
#endif
