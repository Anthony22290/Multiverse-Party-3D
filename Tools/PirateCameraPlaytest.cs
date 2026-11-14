using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

public static class PirateCameraPlaytest
{
    static async Task WaitGame(int milliseconds)
    {
        float target = Time.time + milliseconds / 1000f;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (Time.time < target)
        {
            if (DateTime.UtcNow > deadline) throw new Exception("Player loop did not advance in time.");
            await Task.Delay(25);
        }
    }
    public static async Task<string> Run()
    {
        var results = new List<string>();
        Action<string, bool> check = (name, ok) => results.Add((ok ? "PASS " : "FAIL ") + name);
        var session = PirateSession.Instance;
        var arena = session.Arena;
        var camera = arena.ArenaCamera.GetComponent<PirateArenaCamera>();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        Action<string, object> set = (name, value) => typeof(PirateArenaCamera).GetField(name, flags).SetValue(camera, value);
        var wall = (GameObject)null;
        try
        {
            session.SetMenu(true); arena.NetworkDriven = true;
            foreach (var f in arena.Fighters) { f.Respawn(); f.SetNetworkMode(true, false); f.Body.isKinematic = true; }
            foreach (var cannon in arena.Cannons) cannon.NetworkDriven = true;
            foreach (var shark in arena.Sharks) shark.NetworkDriven = true;
            var player = arena.Fighters[0]; player.transform.position = new Vector3(40, 8, -4);
            set("yaw", 0f); set("pitch", 26f); set("distance", 8f); set("positioned", false);
            int frame = Time.frameCount;
            await WaitGame(1500);
            Vector3 focus = player.transform.position + Vector3.up * .45f;
            check("Frames advance during the camera test", Time.frameCount > frame + 5);
            check("Camera follows the local character at 8 m", Mathf.Abs(Vector3.Distance(camera.transform.position, focus) - 8f) < .15f);
            check("Camera looks at the character", Vector3.Dot(camera.transform.forward, (focus - camera.transform.position).normalized) > .99f);
            Vector3 direction = (camera.transform.position - focus).normalized;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.AddComponent<PirateArenaSurface>();
            wall.transform.position = focus + direction * 4f; wall.transform.localScale = new Vector3(5,5,.4f);
            Physics.SyncTransforms(); await WaitGame(700);
            check("Camera retracts before a wall", Vector3.Distance(camera.transform.position, focus) < 4f);
            UnityEngine.Object.Destroy(wall); wall = null;
            await WaitGame(1800);
            check("Camera returns smoothly after obstruction clears", Vector3.Distance(camera.transform.position, focus) > 7.8f);
            set("yaw", 90f); await WaitGame(700);
            check("Orbit rotates around the character", camera.transform.position.x < focus.x - 6f);
            set("distance", 3.5f); await WaitGame(700);
            check("Close zoom stays at the requested distance", Mathf.Abs(Vector3.Distance(camera.transform.position, focus) - 3.5f) < .15f);
            check("Menu releases the cursor", Cursor.lockState == CursorLockMode.None && Cursor.visible);
            arena.Fighters[1].transform.position = new Vector3(44,8,-4);
            for (int i=2;i<6;i++) arena.Fighters[i].Eliminate();
            player.Eliminate(); set("positioned", false); await WaitGame(700);
            Vector3 spectatorFocus = arena.Fighters[1].transform.position + Vector3.up * .45f;
            check("Eliminated player spectates a survivor", Vector3.Dot(camera.transform.forward, (spectatorFocus-camera.transform.position).normalized) > .99f);
        }
        finally
        {
            if (wall != null) UnityEngine.Object.Destroy(wall);
            await session.Leave(false); session.SetMenu(true);
            set("distance", 8f); set("yaw", 20f); set("pitch", 26f); set("positioned", false);
        }
        string report = string.Join("\n", results);
        File.WriteAllText("Docs/Camara_Resultados_2026-10-29.txt",report);
        return report;
    }
}
