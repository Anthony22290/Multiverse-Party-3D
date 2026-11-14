using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Measures how far the visible physical puppet sags away from the animated reference pose
/// (per bone: angle error and position drop), idle / walking / running, and captures close-ups.
/// </summary>
public static class ArenaPuppetDiag
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly string[] Bones = { "Torso", "Head", "UpperArm.L", "LowerArm.L", "UpperArm.R", "LowerArm.R", "UpperLeg.L", "LowerLeg.L", "UpperLeg.R", "LowerLeg.R" };

    static (Transform puppet, Transform reference) Pair(PirateFighter f, string bone)
    {
        Transform p = null, r = null;
        foreach (var t in f.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != bone) continue;
            if (t.GetComponent<Rigidbody>() != null) p = t; else if (r == null) r = t;
        }
        return (p, r);
    }

    public static async Task<string> Run() => await Measure(Label);
    public static string Label = "despues";

    public static async Task<string> Measure(string label)
    {
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        if (PirateSession.Instance != null) typeof(PirateSession).GetMethod("SetMenu", F).Invoke(PirateSession.Instance, new object[] { false });
        d.SendMessage("RestartRound");
        await Task.Delay(3300);
        foreach (var c in Object.FindObjectsByType<CannonHazard>()) c.enabled = false;
        foreach (var s in Object.FindObjectsByType<SharkHazard>()) s.enabled = false;
        var subject = d.Fighters[1];
        // Freeze the other bots far away; drive the subject directly.
        int i = 0;
        foreach (var f in d.Fighters) if (f != subject) { f.enabled = false; f.Body.isKinematic = true; f.transform.position = f.Body.position = new Vector3(-4f + 2f * i++, d.Art.Map.SternHeight + .9f, -15.5f); }
        var sb = new StringBuilder($"== {label} ==\n");
        Vector3 start = new Vector3(-3f, d.DeckY + .95f, d.Art.Map.CenterCourtZ + 2f);
        subject.Respawn(); subject.transform.position = subject.Body.position = start;
        subject.GetType().GetField("botThink", F).SetValue(subject, 1e6f);
        var cam = new GameObject("Diag cam").AddComponent<Camera>(); cam.fieldOfView = 40; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.38f, .72f, .83f);
        Directory.CreateDirectory("Docs/Claude_Capturas");
        foreach (var (mode, speed) in new[] { ("quieto", 0f), ("andando", .4f), ("corriendo", 1f) })
        {
            subject.Respawn(); subject.transform.position = subject.Body.position = start;
            subject.GetType().GetField("botThink", F).SetValue(subject, 1e6f);
            await Task.Delay(500);
            var sums = new Dictionary<string, (float ang, float drop, int n)>();
            float t0 = Time.time;
            while (Time.time - t0 < 1.6f)
            {
                subject.GetType().GetField("move", F).SetValue(subject, Vector3.right * speed);
                foreach (var b in Bones)
                {
                    var (p, r) = Pair(subject, b); if (p == null || r == null) continue;
                    float ang = Quaternion.Angle(p.rotation, r.rotation);
                    float drop = r.position.y - p.position.y;
                    sums.TryGetValue(b, out var s); sums[b] = (s.ang + ang, s.drop + drop, s.n + 1);
                }
                await Task.Delay(40);
            }
            var pos = subject.transform.position;
            cam.transform.position = pos + new Vector3(0f, 1.0f, -3.2f) + (speed > 0 ? Vector3.right * 1.2f : Vector3.zero);
            cam.transform.LookAt(pos + Vector3.up * .3f);
            ClaudeBridge.CaptureCamera(cam, Path.GetFullPath($"Docs/Claude_Capturas/Personaje_{label}_{mode}.png"), 900, 900);
            sb.Append($"{mode}: ");
            float total = 0; int k = 0;
            foreach (var kv in sums) { float a = kv.Value.ang / kv.Value.n, dr = kv.Value.drop / kv.Value.n * 100f; total += a; k++; sb.Append($"{kv.Key} {a:0}°/{dr:0}cm  "); }
            sb.Append($"\n   media {total / Mathf.Max(1, k):0.0}° | inclinación torso cápsula {Vector3.Angle(subject.transform.up, Vector3.up):0}°\n");
            subject.GetType().GetField("move", F).SetValue(subject, Vector3.zero);
        }
        Object.Destroy(cam.gameObject);
        foreach (var f in d.Fighters) { f.enabled = true; f.Body.isKinematic = false; }
        foreach (var c in Object.FindObjectsByType<CannonHazard>()) c.enabled = true;
        foreach (var s in Object.FindObjectsByType<SharkHazard>()) s.enabled = true;
        d.SendMessage("RestartRound");
        File.AppendAllText("Docs/Claude_Pruebas/Personajes_Postura.txt", sb.ToString());
        return sb.ToString();
    }
}
