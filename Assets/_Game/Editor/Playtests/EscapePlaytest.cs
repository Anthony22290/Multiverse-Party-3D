using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Automatic run of the escape tower in Play mode: bots drive the whole team (including the local player),
/// time runs faster, and the test checks that every room is solved, nobody is left behind and the results appear.
/// Usage (ClaudeBridge): open SalaDeEscape → play → run EscapePlaytest.Run4 / Run6.
/// </summary>
public static class EscapePlaytest
{
    public static Task<string[]> Run4() => Run(4);
    public static Task<string[]> Run6() => Run(6);
    public static Task<string[]> Run5() => Run(5);
    public static string ForceNew() { PlayerPrefs.SetString("Escape_Salas", "RoomClock,RoomBells,RoomMirrors,RoomCipher,RoomSlider,RoomPortal"); PlayerPrefs.Save(); return "ok"; }
    public static string ForceFive() { PlayerPrefs.SetString("Escape_Salas", "RoomLanterns,RoomJugs,RoomPipes,RoomNumbers,RoomFootprints,RoomSlider,RoomPortal"); PlayerPrefs.Save(); return "ok"; }
    public static string ForceAll() { PlayerPrefs.SetString("Escape_Salas", string.Join(",", EscapeLevel.AllRooms.Select(t => t.Name))); PlayerPrefs.Save(); return "ok"; }

    /// <summary>Eye-level captures in front of every room's puzzle (works in the start-card state).</summary>
    public static async Task<string> FPShots()
    {
        var d = Object.FindAnyObjectByType<EscapeDirector>();
        if (d == null || d.Rooms == null) return "FAIL sin director";
        var dir = Path.GetFullPath("Docs/Claude_Capturas/Escape/PrimeraPersona");
        Directory.CreateDirectory(dir);
        var cam = new GameObject("Cámara de capturas").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.07f, .05f, .16f); cam.fieldOfView = 72f; cam.nearClipPlane = .05f;
        cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        var poses = new Dictionary<string, (Vector3 pos, Vector3 look)[]>
        {
            ["RoomLanterns"] = new[] { (new Vector3(-4.6f, 1.6f, 6.6f), new Vector3(-5.4f, 2.1f, 10.7f)) },
            ["RoomJugs"] = new[] { (new Vector3(0, 1.75f, -3.2f), new Vector3(0, 1.1f, 2.5f)) },
            ["RoomPipes"] = new[] { (new Vector3(-4.6f, 1.6f, 6.8f), new Vector3(-5f, 2.15f, 10.7f)) },
            ["RoomNumbers"] = new[] { (new Vector3(0, 1.6f, -6.5f), new Vector3(0, 3.6f, -2.3f)), (new Vector3(-5.5f, 1.4f, 3.2f), new Vector3(-9.7f, .8f, 3.5f)) },
            ["RoomFootprints"] = new[] { (new Vector3(-3f, 1.7f, -9.6f), new Vector3(0, 0, -2f)) },
            ["RoomClock"] = new[] { (new Vector3(-4f, 1.6f, 6.2f), new Vector3(-5.6f, 2.3f, 10.7f)), (new Vector3(5.5f, 1.6f, 1.5f), new Vector3(9.7f, 2.1f, 1.5f)) },
            ["RoomBells"] = new[] { (new Vector3(0, 1.6f, -7f), new Vector3(0, 2.4f, 2f)) },
            ["RoomMirrors"] = new[] { (new Vector3(-8.5f, 2.6f, -9.5f), new Vector3(1f, .6f, 1f)) },
            ["RoomCipher"] = new[] { (new Vector3(-3.6f, 1.6f, 6.4f), new Vector3(-5.4f, 2.2f, 10.7f)), (new Vector3(-5f, 1.6f, -1.5f), new Vector3(-9.7f, 2f, -1.5f)) },
            ["RoomSlider"] = new[] { (new Vector3(4.4f, 1.6f, 7.2f), new Vector3(5.4f, 2f, 10.7f)) },
            ["RoomSymbols"] = new[] { (new Vector3(-2f, 1.6f, 3f), new Vector3(-5.2f, 2.3f, 10.7f)), (new Vector3(0, 1.6f, -5f), new Vector3(0, 2.3f, -10.7f)) },
        };
        await Task.Delay(500);
        int n = 0;
        foreach (var room in d.Rooms)
        {
            string name = room.GetType().Name;
            var list = poses.TryGetValue(name, out var l) ? l : new[] { (new Vector3(0, 1.6f, -8f), new Vector3(0, 1f, 4f)) };
            for (int k = 0; k < list.Length; k++)
            {
                cam.transform.position = room.W(list[k].Item1);
                cam.transform.LookAt(room.W(list[k].Item2));
                ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, $"{room.Index + 1:00}_{name}{(k > 0 ? "_" + k : "")}.png"), 960, 540);
                n++;
            }
        }
        Object.Destroy(cam.gameObject);
        return "ok " + n;
    }

    public static string ForceClassic() { PlayerPrefs.SetString("Escape_Salas", "RoomSymbols,RoomLibrary,RoomTiles,RoomBraziers,RoomPortal"); PlayerPrefs.Save(); return "ok"; }

    static async Task<string[]> Run(int players)
    {
        var lines = new List<string>();
        EscapeDirector d = null;
        for (int i = 0; i < 60 && d == null; i++) { d = Object.FindAnyObjectByType<EscapeDirector>(); if (d == null || d.Rooms == null) { d = null; await Task.Delay(200); } }
        if (d == null || !Application.isPlaying) return new[] { "FAIL escena SalaDeEscape no está en Play" };
        if (d.State != EscapeDirector.Phase.Lobby) return new[] { "FAIL la partida ya empezó (reinicia Play)" };

        d.AutoHuman = true;
        Time.timeScale = 4f;
        d.StartGame(players);
        lines.Add($"Equipo: {d.Actors.Count} jugadores, {d.Rooms.Length} salas: " + string.Join(", ", d.Rooms.Select(r => r.GetType().Name)));
        var shots = Path.GetFullPath("Docs/Claude_Capturas/Escape");
        Directory.CreateDirectory(shots);

        float realStart = Time.realtimeSinceStartup;
        int lastRoom = -1;
        var falls = new Dictionary<int, int>();
        float lastShot = -10f;
        while (Application.isPlaying && d.State != EscapeDirector.Phase.Won && d.State != EscapeDirector.Phase.Lost && Time.realtimeSinceStartup - realStart < 420f)
        {
            if (d.ActiveRoomIndex != lastRoom)
            {
                if (lastRoom >= 0) lines.Add($"PASS Sala {lastRoom + 1} {d.Rooms[lastRoom].Title}: {d.SolvedAt[lastRoom] - (lastRoom > 0 ? d.SolvedAt[lastRoom - 1] : 0f):0.0} s de juego");
                lastRoom = d.ActiveRoomIndex;
                lastShot = Time.realtimeSinceStartup;
            }
            // One gameplay capture per room, a couple of seconds after it starts.
            if (lastShot > 0f && Time.realtimeSinceStartup - lastShot > 1.2f && d.Human != null && d.View != null)
            {
                lastShot = -10f;
                ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.Combine(shots, $"Sala_{lastRoom + 1:00}_juego.png"), 1280, 720);
            }
            await Task.Delay(100);
        }
        Time.timeScale = 1f;
        if (lastRoom >= 0 && lastRoom < d.Rooms.Length && d.Rooms[lastRoom].Solved)
            lines.Add($"PASS Sala {lastRoom + 1} {d.Rooms[lastRoom].Title}: {d.SolvedAt[lastRoom] - (lastRoom > 0 ? d.SolvedAt[lastRoom - 1] : 0f):0.0} s de juego");

        bool won = d.State == EscapeDirector.Phase.Won;
        lines.Add((won ? "PASS" : "FAIL") + $" Torre completada: estado {d.State}, sala activa {Mathf.Min(d.ActiveRoomIndex + 1, d.Rooms.Length)}, tiempo de juego {d.Elapsed:0} s de {d.totalSeconds:0}");
        if (!won && d.ActiveRoom != null)
        {
            lines.Add("   Objetivo pendiente: " + d.ActiveRoom.Objective);
            foreach (var a in d.Actors) lines.Add($"   {a.DisplayName}: pos {a.transform.position}  sala {d.RoomIndexAt(a.transform.position) + 1}  lleva {(a.Carried != null ? a.Carried.kind : "-")}");
        }
        int behind = d.Actors.Count(a => d.RoomIndexAt(a.transform.position) < d.Rooms.Length - 1);
        lines.Add((won && behind == 0 ? "PASS" : won ? "FAIL" : "INFO") + $" Todo el equipo llegó a la sala final ({d.Actors.Count - behind}/{d.Actors.Count})");
        var hud = Object.FindAnyObjectByType<EscapeHUD>();
        lines.Add((won && hud != null ? "PASS" : "INFO") + " Pantalla de resultados mostrada");
        var best = d.MostHelpful;
        if (best != null) lines.Add($"INFO Más útil: {best.DisplayName} ({best.Actions} acciones)");
        var mvp = d.MostValuable; if (mvp != null) lines.Add($"INFO Punto de partida para: {mvp.DisplayName} ({mvp.Solves} salas)");
        await Task.Delay(400);
        if (d.View != null) ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.Combine(shots, $"Resultado_{players}.png"), 1280, 720);
        File.WriteAllLines($"Docs/Claude_Pruebas/Escape_{players}_jugadores.txt", lines);
        return lines.ToArray();
    }

    /// <summary>Hub: the escape portal exists, the walker can stand at it, it selects "SALA DE ESCAPE"; capture.</summary>
    public static async Task<string[]> HubPortal()
    {
        var lines = new List<string>();
        var hub = Object.FindAnyObjectByType<MultiverseHub>();
        var portal = hub != null ? hub.transform.Find("Portal de escape") : null;
        lines.Add((portal != null ? "PASS" : "FAIL") + " Portal de escape construido en el Hub");
        if (portal == null) return lines.ToArray();
        var walker = GameObject.Find("Explorador")?.GetComponent<CharacterController>();
        if (walker != null)
        {
            walker.enabled = false; walker.transform.position = portal.position + new Vector3(-3.2f, 1.2f, 0); walker.enabled = true;
            await Task.Delay(1500);
            var title = Object.FindObjectsByType<UnityEngine.UI.Text>().FirstOrDefault(t => t.text == "SALA DE ESCAPE");
            lines.Add((title != null ? "PASS" : "FAIL") + " Al acercarse se selecciona SALA DE ESCAPE");
            lines.Add((walker.transform.position.y > portal.position.y - .2f ? "PASS" : "FAIL") + $" El explorador se sostiene en la plataforma (y={walker.transform.position.y:0.00})");
            lines.Add((UnityEngine.Application.CanStreamedLevelBeLoaded("SalaDeEscape") ? "PASS" : "FAIL") + " Escena SalaDeEscape en Build Settings");
        }
        var cam = new GameObject("Cámara de captura").AddComponent<Camera>();
        cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        cam.transform.position = portal.position + new Vector3(-11f, 5f, -4f);
        cam.transform.LookAt(portal.position + Vector3.up * 2.4f);
        cam.fieldOfView = 50f;
        Directory.CreateDirectory("Docs/Claude_Capturas/Escape");
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath("Docs/Claude_Capturas/Escape/Hub_Portal_Escape.png"), 1280, 720);
        Object.Destroy(cam.gameObject);
        return lines.ToArray();
    }

    /// <summary>Overview captures of every room from above (no gameplay needed; works in the lobby state).</summary>
    public static async Task<string> Shots()
    {
        var d = Object.FindAnyObjectByType<EscapeDirector>();
        if (d == null || d.Rooms == null) return "FAIL sin director";
        var dir = Path.GetFullPath("Docs/Claude_Capturas/Escape");
        Directory.CreateDirectory(dir);
        var cam = new GameObject("Cámara de capturas").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.07f, .05f, .16f); cam.fieldOfView = 50f;
        var data = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        await Task.Delay(300);
        for (int i = 0; i < d.Rooms.Length; i++)
        {
            var c = d.Rooms[i].transform.position;
            cam.transform.SetPositionAndRotation(c + new Vector3(0, 15.5f, -12.5f), Quaternion.Euler(52, 0, 0));
            ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, $"Sala_{i + 1:00}.png"), 1280, 720);
        }
        cam.transform.SetPositionAndRotation(new Vector3(34, 38, -22), Quaternion.Euler(38, -38, 0));
        cam.fieldOfView = 60f;
        ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, "Torre_completa.png"), 1600, 900);
        Object.Destroy(cam.gameObject);
        return "ok " + d.Rooms.Length;
    }
}
