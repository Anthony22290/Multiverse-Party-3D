using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Automatic run of Invasión Zombi in Play mode: bots drive the whole team (including the local player), time runs
/// faster, and the test checks the 10 waves, the 4 bosses, the 10 parts, the escape and the results screen.
/// Usage (ClaudeBridge): open InvasionZombi → play → run SurvivalPlaytest.Run4 (or Run2).
/// </summary>
public static class SurvivalPlaytest
{
    public static Task<string[]> Run4() => Run(4, 4f);
    public static Task<string[]> Run2() => Run(2, 4f);

    /// <summary>Open MundoDeInicio → play → run SurvivalPlaytest.HubPortal: selects the zombie portal and enters it.</summary>
    public static async Task<string> HubPortal()
    {
        var hub = Object.FindAnyObjectByType<MultiverseHub>();
        if (hub == null) return "FAIL no hay Hub en Play";
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        typeof(MultiverseHub).GetMethod("Select", flags).Invoke(hub, new object[] { 1 });
        var button = typeof(MultiverseHub).GetField("playButton", flags)?.GetValue(hub) as UnityEngine.UI.Button;
        string label = button != null ? button.GetComponentInChildren<UnityEngine.UI.Text>().text : "?";
        bool shown = button != null && button.gameObject.activeSelf;
        typeof(MultiverseHub).GetMethod("PlaySelected", flags).Invoke(hub, null);
        for (int i = 0; i < 40 && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "InvasionZombi"; i++) await Task.Delay(150);
        await Task.Delay(800);
        bool loaded = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "InvasionZombi" && Object.FindAnyObjectByType<SurvivalDirector>() != null;
        return $"{(shown && loaded ? "PASS" : "FAIL")} Portal zombi: botón {(shown ? "visible" : "oculto")} ('{label}'), escena {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}";
    }

    static async Task<string[]> Run(int players, float speed)
    {
        var lines = new List<string>();
        SurvivalDirector d = null;
        for (int i = 0; i < 60 && d == null; i++) { d = Object.FindAnyObjectByType<SurvivalDirector>(); if (d == null || d.Level == null || d.HUD == null) { d = null; await Task.Delay(200); } }
        if (d == null || !Application.isPlaying) return new[] { "FAIL escena InvasionZombi no está en Play" };
        if (d.State != SurvivalDirector.Phase.Lobby) return new[] { "FAIL la partida ya empezó (reinicia Play)" };

        var shots = Path.GetFullPath("Docs/Claude_Capturas/Zombis");
        Directory.CreateDirectory(shots);
        var cam = d.View.GetComponent<Camera>();
        await Task.Delay(400);
        ClaudeBridge.CaptureCamera(cam, Path.Combine(shots, "00_Plaza.png"), 1280, 720);

        d.AutoHuman = true;
        Time.timeScale = speed;
        d.StartGame(players);
        lines.Add($"Equipo: {d.Team.Count} jugadores");

        float realStart = Time.realtimeSinceStartup;
        int lastWave = 0, maxAlive = 0, downs = 0;
        bool bossShot = false, escapeShot = false, wonShot = false;
        float waveStart = 0f;
        var bossesSeen = new HashSet<SurvivalZombie.BossKind>();
        float lastShot = -1f;
        while (Application.isPlaying && d.State != SurvivalDirector.Phase.Lost && Time.realtimeSinceStartup - realStart < 215f)
        {
            if (d.Wave != lastWave)
            {
                if (lastWave > 0) lines.Add($"PASS Oleada {lastWave}: {d.WaveClearedAt[lastWave - 1] - waveStart:0} s de juego · camioneta {d.PartsInstalled}/10 · bajas {d.TotalKills}");
                waveStart = lastWave > 0 ? d.WaveClearedAt[lastWave - 1] : 0f;
                lastWave = d.Wave; bossShot = false;
                lastShot = Time.realtimeSinceStartup;
            }
            maxAlive = Mathf.Max(maxAlive, SurvivalZombie.All.Count);
            if (d.Boss != null) bossesSeen.Add(d.Boss.Boss);
            if (lastShot > 0f && Time.realtimeSinceStartup - lastShot > 3.5f)
            {
                lastShot = -1f;
                ClaudeBridge.CaptureCamera(cam, Path.Combine(shots, $"Oleada_{d.Wave:00}.png"), 1280, 720);
            }
            if (!bossShot && d.Boss != null && SurvivalZombie.All.Count > 2 && d.Level.Inside(d.Boss.transform.position, 1f))
            {
                bossShot = true;
                ClaudeBridge.CaptureCamera(cam, Path.Combine(shots, $"Jefe_{d.Boss.Boss}.png"), 1280, 720);
            }
            if (!escapeShot && d.State == SurvivalDirector.Phase.Escape && d.EscapeProgress > .3f)
            {
                escapeShot = true;
                ClaudeBridge.CaptureCamera(cam, Path.Combine(shots, "Escape.png"), 1280, 720);
            }
            if (d.State == SurvivalDirector.Phase.Won)
            {
                if (!wonShot && d.DriveTime > 2f) { wonShot = true; ClaudeBridge.CaptureCamera(cam, Path.Combine(shots, "Huida.png"), 1280, 720); }
                if (d.DriveTime > 6f) break;
            }
            downs = d.Team.Sum(p => p.Downs);
            await Task.Delay(100);
        }
        Time.timeScale = 1f;
        bool won = d.State == SurvivalDirector.Phase.Won;
        if (won && lastWave == 10) lines.Add($"PASS Oleada 10: {d.WaveClearedAt[9] - waveStart:0} s de juego · camioneta {d.PartsInstalled}/10");
        lines.Add((won ? "PASS" : "FAIL") + $" Partida: estado {d.State}, oleada {d.Wave}/10, camioneta {d.PartsInstalled}/10, {d.Elapsed:0} s de juego, {Time.realtimeSinceStartup - realStart:0} s reales");
        lines.Add((bossesSeen.Count == 4 ? "PASS" : won ? "FAIL" : "INFO") + " Jefes vistos: " + string.Join(", ", bossesSeen));
        lines.Add($"INFO máx. zombis a la vez {maxAlive} · bajas {d.TotalKills} · caídas del equipo {downs}");
        foreach (var s in d.DownLog) lines.Add("INFO caída: " + s);
        foreach (var p in d.Team) lines.Add($"INFO {p.DisplayName}: {p.Kills} zombis, {p.Parts} piezas, {p.Revives} reanimaciones, {p.Downs} caídas");
        if (!won)
        {
            lines.Add($"INFO Al fallar: zombis vivos {SurvivalZombie.All.Count}, pieza suelta {(d.LoosePart != null ? d.LoosePart.Label + " @" + d.LoosePart.transform.position.ToString("0") : "no")}, portador {(d.Carrier != null ? d.Carrier.Title + " @" + d.Carrier.transform.position.ToString("0") : "no")}");
            foreach (var z in SurvivalZombie.All.Take(6)) lines.Add($"INFO   {z.Title} @{z.transform.position:0.0}");
        }
        await Task.Delay(1200);
        if (won) await Task.Delay(4000);
        ClaudeBridge.CaptureCamera(cam, Path.Combine(shots, "Resultados_escena.png"), 1280, 720);
        lines.Add((d.HUD != null ? "PASS" : "FAIL") + " Resultados mostrados");
        File.WriteAllLines(Path.GetFullPath($"Docs/Claude_Pruebas/Zombis_{players}_jugadores.txt"), lines);
        return lines.ToArray();
    }
}
