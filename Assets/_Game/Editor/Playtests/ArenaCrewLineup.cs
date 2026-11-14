using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Lines up the six crew members on the deck, facing the camera, and captures a portrait of each and a group shot.</summary>
public static class ArenaCrewLineup
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    public static async Task<string> Run()
    {
        try { return await Inner(); } catch (System.Exception e) { return "EXC " + e; }
    }
    static async Task<string> Inner()
    {
        PirateArenaDirector d = null;
        if (PirateSession.Instance != null) typeof(PirateSession).GetMethod("SetMenu", F).Invoke(PirateSession.Instance, new object[] { false });
        for (int t = 0; t < 80; t++) { d = Object.FindAnyObjectByType<PirateArenaDirector>(); if (d != null && d.Art != null && d.Art.Map != null && d.Fighters.Count > 0) break; await Task.Delay(250); }
        foreach (var c in Object.FindObjectsByType<CannonHazard>()) if (c) c.enabled = false;
        foreach (var sh in Object.FindObjectsByType<SharkHazard>()) if (sh) sh.enabled = false;
        float z = d.Art.Map.CenterCourtZ;
        for (int i = 0; i < d.Fighters.Count; i++)
        {
            var f = d.Fighters[i];
            f.Respawn();
            var bt = typeof(PirateFighter).GetField("botThink", F); if (bt != null) bt.SetValue(f, 1e6f);
            var p = new Vector3(-3.75f + i * 1.5f, d.DeckY + .95f, z);
            f.transform.SetPositionAndRotation(p, Quaternion.Euler(0, 180, 0)); f.Body.position = p; f.Body.rotation = Quaternion.Euler(0, 180, 0);
            f.Body.linearVelocity = Vector3.zero; f.Body.angularVelocity = Vector3.zero; f.Body.isKinematic = true;
        }
        await Task.Delay(1500);
        var cam = new GameObject("Lineup cam").AddComponent<Camera>(); cam.fieldOfView = 38; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.42f, .74f, .86f);
        Directory.CreateDirectory("Docs/Claude_Capturas");
        var center = new Vector3(0, d.DeckY + .9f, z);
        cam.transform.position = center + new Vector3(0, .9f, -6.5f); cam.transform.LookAt(center);
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath("Docs/Claude_Capturas/Tripulacion_grupo.png"), 1600, 800);
        for (int i = 0; i < d.Fighters.Count; i++)
        {
            var p = d.Fighters[i].transform.position;
            cam.transform.position = p + new Vector3(.6f, .55f, -2.1f); cam.transform.LookAt(p + Vector3.up * .25f);
            ClaudeBridge.CaptureCamera(cam, Path.GetFullPath($"Docs/Claude_Capturas/Tripulante_{i}.png"), 700, 800);
        }
        foreach (var f in d.Fighters) f.Body.isKinematic = false;
        Object.Destroy(cam.gameObject);
        return "ok " + d.Fighters.Count;
    }
}
