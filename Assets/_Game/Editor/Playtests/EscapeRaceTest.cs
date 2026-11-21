using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Sala de Escape — CARRERA (2026-11-17). Usage (ClaudeBridge): open SalaDeEscape → play → run EscapeRaceTest.X
/// (restart Play between runs).
///  · Race: the AI drives your explorer through the whole tower against ghost rivals; checks solo solving, ranking and race end.
///  · Helper: you stand still; checks the helper arrives at 1:30, solves (or opens) the room, and leaves.
/// </summary>
public static class EscapeRaceTest
{
    public static string ForceHelper() { PlayerPrefs.SetString("Escape_Salas", "RoomCipher,RoomTiles,RoomPortal"); PlayerPrefs.Save(); return "ok"; }

    public static string Status()
    {
        var d = Object.FindAnyObjectByType<EscapeDirector>();
        if (d == null) return "sin director";
        return $"state={d.State} inMatch={MultiverseParty.InMatch} racers={d.Racers.Count} actors={d.Actors.Count} room={d.ActiveRoomIndex} elapsed={d.Elapsed:0} race={d.RaceClock:0} over={d.RaceOver} " +
               string.Join(" | ", d.Ranking.Select(r => r.Name + ":" + r.Rooms + (r.Finished ? "✔" + r.Time.ToString("0") : "")));
    }

    static async Task<EscapeDirector> Director()
    {
        EscapeDirector d = null;
        for (int i = 0; i < 60 && d == null; i++) { d = Object.FindAnyObjectByType<EscapeDirector>(); if (d == null || d.Rooms == null) { d = null; await Task.Delay(200); } }
        return d;
    }

    static void Shot(EscapeDirector d, string name)
    {
        var dir = Path.GetFullPath("Docs/Claude_Capturas/Escape/Carrera"); Directory.CreateDirectory(dir);
        if (d.View != null) ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.Combine(dir, name + ".png"), 1280, 720);
    }

    public static async Task<string[]> Race()
    {
        var lines = new List<string>();
        var d = await Director();
        if (d == null || !Application.isPlaying) return new[] { "FAIL escena SalaDeEscape no está en Play" };
        if (d.State != EscapeDirector.Phase.Lobby) return new[] { "FAIL la partida ya empezó (reinicia Play)" };
        d.AutoHuman = true;
        Time.timeScale = 6f;
        d.StartGame(4);
        lines.Add((d.Actors.Count == 1 ? "PASS" : "FAIL") + $" Solo tu explorador en la torre ({d.Actors.Count} cuerpo/s)");
        lines.Add((d.Racers.Count == 4 && d.Racers.Count(r => r.Ghost) == 3 ? "PASS" : "FAIL") + $" Corredores: {d.Racers.Count} ({d.Racers.Count(r => r.Ghost)} rivales de la IA sin cuerpo)");
        lines.Add("INFO Salas: " + string.Join(", ", d.Rooms.Select(r => r.GetType().Name)));
        float start = Time.realtimeSinceStartup;
        int last = -1, helpers = 0; bool helperSeen = false, midShot = false;
        while (Application.isPlaying && d.State != EscapeDirector.Phase.Won && d.State != EscapeDirector.Phase.Lost && Time.realtimeSinceStartup - start < 420f)
        {
            if (d.HelperBot != null && !helperSeen) { helperSeen = true; helpers++; }
            if (d.ActiveRoomIndex != last)
            {
                if (last >= 0) lines.Add($"PASS Sala {last + 1} {d.Rooms[last].Title}: {d.SolvedAt[last] - (last > 0 ? d.SolvedAt[last - 1] : 0f):0} s{(helperSeen ? " (con ayudante)" : "")}");
                last = d.ActiveRoomIndex; helperSeen = false;
            }
            if (!midShot && d.ActiveRoomIndex == 2) { midShot = true; await Task.Delay(300); Shot(d, "Carrera_HUD"); }
            await Task.Delay(100);
        }
        if (last >= 0 && last < d.Rooms.Length && d.Rooms[last].Solved) lines.Add($"PASS Sala {last + 1} {d.Rooms[last].Title}: {d.SolvedAt[last] - (last > 0 ? d.SolvedAt[last - 1] : 0f):0} s{(helperSeen ? " (con ayudante)" : "")}");
        bool won = d.State == EscapeDirector.Phase.Won;
        lines.Add((won ? "PASS" : "FAIL") + $" Torre completada en solitario: {EscapeDirector.Clock(d.Elapsed)} · salas con ayudante: {helpers}");
        lines.Add((d.Me != null && d.Me.Finished && d.Place(d.Me) >= 1 ? "PASS" : "FAIL") + $" Puesto al llegar: {d.Place(d.Me)}.º");
        float waitStart = Time.realtimeSinceStartup;
        while (Application.isPlaying && !d.RaceOver && Time.realtimeSinceStartup - waitStart < 30f) await Task.Delay(100);
        Time.timeScale = 1f;
        lines.Add((d.RaceOver ? "PASS" : "FAIL") + " Fin de la carrera tras la última llegada humana");
        foreach (var (r, i) in d.Ranking.Select((r, i) => (r, i))) lines.Add($"INFO {i + 1}. {r.Name}{(r.Ghost ? " (IA)" : "")}: {(r.Finished ? EscapeDirector.Clock(r.Time) : "sala " + (r.Rooms + 1))}");
        await Task.Delay(500);
        Shot(d, "Carrera_Resultados");
        Directory.CreateDirectory("Docs/Claude_Pruebas");
        File.WriteAllLines("Docs/Claude_Pruebas/Escape_Carrera.txt", lines);
        return lines.ToArray();
    }

    public static async Task<string[]> Helper()
    {
        var lines = new List<string>();
        var d = await Director();
        if (d == null || !Application.isPlaying) return new[] { "FAIL escena SalaDeEscape no está en Play" };
        if (d.State != EscapeDirector.Phase.Lobby) return new[] { "FAIL la partida ya empezó (reinicia Play)" };
        Time.timeScale = 8f;
        d.StartGame(1);   // you alone, standing still
        lines.Add("INFO Salas: " + string.Join(", ", d.Rooms.Select(r => r.GetType().Name)));
        float start = Time.realtimeSinceStartup;
        for (int room = 0; room < d.Rooms.Length - 1 && Application.isPlaying; room++)
        {
            float appeared = -1f; bool shot = false;
            while (Application.isPlaying && !d.Rooms[room].Solved && Time.realtimeSinceStartup - start < 300f)
            {
                if (d.HelperBot != null && appeared < 0f) appeared = d.RoomTime;
                if (d.HelperBot != null && !shot && d.RoomTime > appeared + 3f) { shot = true; Shot(d, $"Ayudante_sala{room + 1}"); }
                if (d.Human != null) { d.Human.BotMove = Vector3.zero; }
                await Task.Delay(50);
            }
            float solvedIn = d.SolvedAt[room] - (room > 0 ? d.SolvedAt[room - 1] : 0f);
            lines.Add((appeared >= EscapeDirector.HelperAfter - 1f && appeared < EscapeDirector.HelperAfter + 2f ? "PASS" : "FAIL") + $" Sala {room + 1}: el ayudante aparece a los {appeared:0} s");
            lines.Add((d.Rooms[room].Solved && solvedIn <= EscapeDirector.HelperAfter + EscapeDirector.HelperGiveUp + 2f ? "PASS" : "FAIL") + $" Sala {room + 1} {d.Rooms[room].Title} resuelta a los {solvedIn:0} s por {(d.Rooms[room].SolvedBy != null ? d.Rooms[room].SolvedBy.DisplayName : "?")}");
            await Task.Delay(600);
            lines.Add((d.HelperBot == null && d.Actors.Count == 1 ? "PASS" : "FAIL") + $" Sala {room + 1}: el ayudante se va al abrirse la puerta");
        }
        Time.timeScale = 1f;
        File.WriteAllLines("Docs/Claude_Pruebas/Escape_Ayudante.txt", lines);
        return lines.ToArray();
    }
}
