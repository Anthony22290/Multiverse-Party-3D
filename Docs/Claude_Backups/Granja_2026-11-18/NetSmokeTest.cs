#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Online smoke test (2026-11-12): hosts a real party room on Photon from the editor (no clients) and plays
/// every minigame through the online code paths for a few seconds: PartyLink active, start handshake,
/// host simulation and snapshot sending. Then the host resolves the round and the party returns to the plaza.
/// Usage (ClaudeBridge): open MenuPrincipal → play → wait 4 → run NetSmokeTest.Run
/// Two-computer checks still need a second instance (build) as client.
/// </summary>
public static class NetSmokeTest
{
    public static Task<string[]> Run() => RunGames(new[] { PartyGames.Mortal, PartyGames.Race, PartyGames.Arena, PartyGames.Zombie, PartyGames.Escape, PartyGames.Rollers, PartyGames.Dig, PartyGames.Ship, PartyGames.Flap, PartyGames.Golf, PartyGames.Potato });
    /// <summary>Only Rodillos del Calabozo (2026-11-17).</summary>
    public static Task<string[]> Rodillos() => RunGames(new[] { PartyGames.Rollers });
    /// <summary>Only Pico, Pala y Tijera (2026-11-17).</summary>
    public static Task<string[]> Mina() => RunGames(new[] { PartyGames.Dig });
    /// <summary>Only Arma tu Nave (2026-11-17).</summary>
    public static Task<string[]> Nave() => RunGames(new[] { PartyGames.Ship });
    /// <summary>Only Propulsores (2026-11-17).</summary>
    public static Task<string[]> Propulsores() => RunGames(new[] { PartyGames.Flap });
    public static Task<string[]> Golf() => RunGames(new[] { PartyGames.Golf });
    public static Task<string[]> Patata() => RunGames(new[] { PartyGames.Potato });

    static async Task<string[]> RunGames(int[] order)
    {
        var lines = new List<string>();
        if (!Application.isPlaying) return new[] { "FAIL no está en Play" };
        MultiverseParty.SavedName = "Anfitrion";
        bool ok = await MultiverseParty.StartOnline(true, "", 4);
        var party = MultiverseParty.Instance;
        if (!ok || party == null) return new[] { "FAIL no se pudo crear la sala: " + MultiverseParty.LastStatus };
        lines.Add("PASS Sala creada " + party.RoomCode);
        if (!await Until(() => SceneManager.GetActiveScene().name == MultiverseParty.HubScene, 20f)) return Fail(lines, "no cargó la plaza");
        party.HostStartMatch();
        lines.Add($"PASS Partida iniciada · {party.Members.Count} miembros ({party.Members.Count(m => m.Bot)} bots)");
        foreach (int g in order)
        {
            if (!await Until(() => party.Phase == PartyPhase.Voting && !party.Loading && SceneManager.GetActiveScene().name == MultiverseParty.HubScene, 40f)) return Fail(lines, "no volvió la votación");
            int round = party.Round;
            party.Selected = g; party.Phase = PartyPhase.Reveal;
            typeof(MultiverseParty).GetField("phaseEnds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(party, Time.unscaledTime);
            string scene = PartyGames.All[g].Scene;
            if (!await Until(() => SceneManager.GetActiveScene().name == scene && !party.Loading, 30f)) return Fail(lines, "no cargó " + scene);
            bool active = PartyLink.Active;
            await Task.Delay(14000);
            string state = Describe(g);
            lines.Add($"{(active && state.StartsWith("OK") ? "PASS" : "FAIL")} {PartyGames.All[g].Title}: PartyLink {(active ? "activo" : "INACTIVO")} · semilla {party.Seed} · {state}");
            party.HostResolve(0);
            if (!await Until(() => party.Phase == PartyPhase.Results, 5f)) return Fail(lines, "no se resolvió la ronda");
            lines.Add($"PASS Ronda {party.Round} resuelta: {party.ResultText}");
        }
        Directory.CreateDirectory("Docs/Claude_Pruebas");
        File.WriteAllLines("Docs/Claude_Pruebas/Red_humo.txt", lines);
        party.LeaveToMenu("Prueba terminada");
        return lines.ToArray();
    }

    static string Describe(int g)
    {
        switch (g)
        {
            case PartyGames.Mortal:
            {
                var d = Object.FindAnyObjectByType<MortalCodeDirector>();
                if (d == null) return "sin director";
                return (d.Net != null && d.Running ? "OK" : "MAL") + $" corriendo={d.Running} cuenta={d.Countdown:0.0} vivos={d.AliveCount}/{d.Actors.Count} títeres={d.Actors.Count(a => a.Puppet)}";
            }
            case PartyGames.Race:
            {
                var d = Object.FindAnyObjectByType<CrazyKartDirector>();
                if (d == null) return "sin director";
                float moved = d.Karts.Max(k => k.Speed);
                return (d.Net != null && d.Net.Started && d.Playing && moved > 1 ? "OK" : "MAL") + $" empezó={d.Net?.Started} carrera={d.RoundTime:0.0}s karts={d.Karts.Count} títeres={d.Karts.Count(k => k.Puppet)} velmax={moved:0.0}";
            }
            case PartyGames.Arena:
            {
                var a = Object.FindAnyObjectByType<PirateArenaDirector>();
                var net = Object.FindAnyObjectByType<PirateArenaPartyNet>();
                if (a == null) return "sin director";
                return (net != null && (a.IsPlaying || a.Finished) ? "OK" : "MAL") + $" red={(net != null)} jugando={a.IsPlaying} terminada={a.Finished} piratas={a.Fighters.Count}";
            }
            case PartyGames.Zombie:
            {
                var d = Object.FindAnyObjectByType<SurvivalDirector>();
                if (d == null) return "sin director";
                return (d.Net != null && d.Fighting && d.Wave >= 1 ? "OK" : "MAL") + $" fase={d.State} oleada={d.Wave} zombis={SurvivalZombie.All.Count} ids={SurvivalZombie.All.Count(z => z.NetId > 0)} equipo={d.Team.Count}";
            }
            case PartyGames.Escape:
            {
                var d = Object.FindAnyObjectByType<EscapeDirector>();
                if (d == null) return "sin director";
                int ids = EscapeInteractable.All.Count(i => i.NetId > 0);
                return (d.Net != null && d.Running && ids > 0 ? "OK" : "MAL") + $" fase={d.State} sala={d.ActiveRoomIndex + 1}/{d.Rooms.Length} interactuables={ids} actores={d.Actors.Count}";
            }
            case PartyGames.Potato:
            {
                var d = Object.FindAnyObjectByType<PatataDirector>();
                if (d == null) return "sin director";
                float moved = d.Players.Max(p => (p.transform.position - PatataDirector.SpawnPoint(p.Slot, d.Players.Count)).magnitude);
                return (d.Net != null && d.State != PatataDirector.Phase.Waiting && d.State != PatataDirector.Phase.Countdown && d.HolderSlot >= -1 && moved > 1f ? "OK" : "MAL") + $" fase={d.State} ronda={d.Round + 1}/{d.Rounds} patata={d.HolderSlot} vivos={d.AliveCount} se movió={moved:0.0} m";
            }
            case PartyGames.Golf:
            {
                var d = Object.FindAnyObjectByType<GolfDirector>();
                if (d == null) return "sin director";
                int strokes = d.Players.Sum(p => p.Strokes);
                return (d.Net != null && d.State != GolfDirector.Phase.Waiting && d.HoleIndex >= 0 && (strokes > 0 || d.HoleIndex > 0) ? "OK" : "MAL") + $" fase={d.State} hoyo={d.HoleIndex + 1} jugadores={d.Players.Count} golpes={strokes}";
            }
            case PartyGames.Flap:
            {
                var d = Object.FindAnyObjectByType<FlapDirector>();
                if (d == null) return "sin director";
                return (d.Net != null && (d.State == FlapDirector.Phase.Running || d.State == FlapDirector.Phase.Finished) && d.Players.Max(p => p.Score) > 0 ? "OK" : "MAL") + $" fase={d.State} reloj={d.Elapsed:0.0}s jugadores={d.Players.Count} máx.puntos={d.Players.Max(p => p.Score)}";
            }
            case PartyGames.Ship:
            {
                var d = Object.FindAnyObjectByType<NaveDirector>();
                if (d == null) return "sin director";
                float moved = d.Players.Max(p => (p.transform.position - d.SpawnPoint(p.Slot)).magnitude);
                return (d.Net != null && (d.State == NaveDirector.Phase.Running || d.State == NaveDirector.Phase.Finished) && moved > 1f ? "OK" : "MAL") + $" fase={d.State} reloj={d.Elapsed:0.0}s jugadores={d.Players.Count} piezas={d.Pieces.Count} se movió={moved:0.0} m";
            }
            case PartyGames.Dig:
            {
                var d = Object.FindAnyObjectByType<MinaDirector>();
                if (d == null) return "sin director";
                return (d.Net != null && (d.State == MinaDirector.Phase.Running || d.State == MinaDirector.Phase.Finished) && d.Miners.Max(m => m.Depth) > 0 ? "OK" : "MAL") + $" fase={d.State} reloj={d.Elapsed:0.0}s mineros={d.Miners.Count} máx.prof={d.Miners.Max(m => m.Depth)} semilla={d.Seed}";
            }
            case PartyGames.Rollers:
            {
                var d = Object.FindAnyObjectByType<RodillosDirector>();
                if (d == null) return "sin director";
                return (d.Net != null && (d.State == RodillosDirector.Phase.Running || d.State == RodillosDirector.Phase.Finished) && d.Elapsed > 1f ? "OK" : "MAL") + $" fase={d.State} reloj={d.Elapsed:0.0}s vivos={d.AliveCount}/{d.Runners.Count} títeres={d.Runners.Count(r => r.Puppet)} semilla={d.Seed}";
            }
        }
        return "?";
    }

    static string[] Fail(List<string> lines, string why)
    {
        lines.Add("FAIL " + why + $" (fase {MultiverseParty.Instance?.Phase}, escena {SceneManager.GetActiveScene().name})");
        MultiverseParty.Instance?.LeaveToMenu("Prueba fallida");
        return lines.ToArray();
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
