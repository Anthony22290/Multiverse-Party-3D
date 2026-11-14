#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Whole party flow without playing the minigames: menu → hub → vote → minigame scene loads (no lobby / menus)
/// → a winner is reported → results → back to the hub … until someone reaches 5 points.
/// Usage (ClaudeBridge): open MenuPrincipal → play → wait → run PartyPlaytest.FullMatch
/// </summary>
public static class PartyPlaytest
{
    static readonly string[] MenuLabels = { "REINICIAR", "MUNDO DE INICIO", "REVANCHA · ENTER", "OTRA CARRERA", "VOLVER A PLAZA", "VOLVER AL HUB", "JUGAR OTRA VEZ", "REINICIAR LA TORRE", "¡EMPEZAR!", "¡SOBREVIVIR!" };

    public static async Task<string[]> FullMatch()
    {
        var lines = new List<string>();
        var shots = Path.GetFullPath("Docs/Claude_Capturas/Partida");
        Directory.CreateDirectory(shots);
        if (!Application.isPlaying) return new[] { "FAIL no está en Play" };
        var menu = Object.FindAnyObjectByType<MultiverseMainMenu>();
        lines.Add((menu != null ? "PASS" : "FAIL") + " Menú principal en pantalla");
        MultiverseParty.SavedName = "Prueba";
        var party = MultiverseParty.StartOffline(5);
        if (!await Until(() => SceneManager.GetActiveScene().name == MultiverseParty.HubScene && Object.FindAnyObjectByType<MultiverseHub>() != null, 15f))
            return Fail(lines, "No se cargó el Mundo de Inicio");
        lines.Add($"PASS Plaza cargada · {party.Members.Count} jugadores ({party.Members.Count(m => m.Bot)} bots)");
        float start = Time.realtimeSinceStartup;
        var played = new List<string>();
        int round = 0;
        while (party != null && party.Phase != PartyPhase.Champion && Time.realtimeSinceStartup - start < 560f)
        {
            if (!await Until(() => party.Phase == PartyPhase.Voting, 40f)) return Fail(lines, "No empezó la votación");
            var choices = party.Choices.Select(c => PartyGames.All[c].Title).ToArray();
            bool distinct = party.Choices.Distinct().Count() == 3;
            await Task.Delay(1200);
            if (round == 0)
            {
                var cam = Camera.main;
                if (cam != null) ClaudeBridge.CaptureCamera(cam, Path.Combine(shots, "10_Plaza_votacion.png"), 1280, 720);
            }
            party.Vote(Random.Range(0, 3));
            if (!await Until(() => party.Phase == PartyPhase.Reveal || party.Phase == PartyPhase.Playing, 40f)) return Fail(lines, "La votación no terminó");
            string chosen = PartyGames.All[party.Selected].Scene;
            int[] votes = new int[3]; foreach (var m in party.Members) if (m.Vote >= 0) votes[m.Vote]++;
            if (!await Until(() => SceneManager.GetActiveScene().name == chosen, 25f)) return Fail(lines, "No se cargó " + chosen);
            await Task.Delay(2500);
            var openMenus = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                .Where(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<UnityEngine.UI.Text>() is var t && t != null && MenuLabels.Contains(t.text.Trim().ToUpperInvariant())).Select(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text).ToList();
            var cam2 = Camera.main;
            if (cam2 != null && !played.Contains(chosen)) ClaudeBridge.CaptureCamera(cam2, Path.Combine(shots, "20_" + chosen + ".png"), 1280, 720);
            played.Add(chosen);
            int winner = Random.value < .45f ? 0 : Random.Range(0, party.Members.Count);
            var roster = party.Roster();
            string winnerName = roster.Names[winner];
            int before = party.Members[roster.Members[winner]].Score;
            MultiverseParty.ReportMinigame(winner);
            if (!await Until(() => party.Phase == PartyPhase.Results, 10f)) return Fail(lines, "No se mostró el resultado");
            bool scored = party.Members[roster.Members[winner]].Score == before + 1;
            round++;
            lines.Add($"{(distinct && scored && openMenus.Count == 0 ? "PASS" : "FAIL")} Ronda {round}: [{string.Join(" / ", choices)}] votos {string.Join("-", votes)} → {PartyGames.All[party.Selected].Title} · gana {winnerName} ({before}→{party.Members[roster.Members[winner]].Score})" +
                      (openMenus.Count > 0 ? " · MENÚS VISIBLES: " + string.Join(", ", openMenus) : ""));
            if (round == 1) { await Task.Delay(4000); var cam3 = Camera.main; if (cam3 != null) ClaudeBridge.CaptureCamera(cam3, Path.Combine(shots, "11_Resultado_ronda.png"), 1280, 720); }
            if (!await Until(() => party.Phase != PartyPhase.Results, 15f)) return Fail(lines, "No terminó la pantalla de resultados");
            if (!await Until(() => SceneManager.GetActiveScene().name == MultiverseParty.HubScene, 15f)) return Fail(lines, "No volvió a la plaza");
        }
        bool champ = party != null && party.Phase == PartyPhase.Champion;
        int lead = party != null ? party.Leader() : -1;
        lines.Add((champ ? "PASS" : "FAIL") + (champ ? $" Campeón: {party.Members[lead].Name} con {party.Members[lead].Score} puntos tras {round} rondas" : " No hubo campeón"));
        lines.Add("INFO Marcador: " + string.Join(", ", party.Members.Select(m => m.Name + " " + m.Score)));
        lines.Add("INFO Minijuegos jugados: " + string.Join(", ", played.GroupBy(x => x).Select(g => g.Key + "×" + g.Count())));
        await Task.Delay(2500);
        var cam4 = Camera.main;
        if (cam4 != null) ClaudeBridge.CaptureCamera(cam4, Path.Combine(shots, "12_Campeon.png"), 1280, 720);
        Directory.CreateDirectory("Docs/Claude_Pruebas");
        File.WriteAllLines("Docs/Claude_Pruebas/Partida_completa.txt", lines);
        return lines.ToArray();
    }

    static string[] Fail(List<string> lines, string why)
    {
        lines.Add("FAIL " + why + $" (fase {MultiverseParty.Instance?.Phase}, escena {SceneManager.GetActiveScene().name})");
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
        return Application.isPlaying && condition();
    }
}
#endif
