using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Close-up captures of real bots during a normal round (walking, fighting, running).</summary>
public static class ArenaCharacterShots
{
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        if (PirateSession.Instance != null) typeof(PirateSession).GetMethod("SetMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(PirateSession.Instance, new object[] { false });
        d.SendMessage("RestartRound");
        var cam = new GameObject("Shots cam").AddComponent<Camera>(); cam.fieldOfView = 45; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.38f, .72f, .83f);
        Directory.CreateDirectory("Docs/Claude_Capturas");
        string list = "";
        for (int i = 0; i < 6; i++)
        {
            await Task.Delay(i == 0 ? 3600 : 900);
            PirateFighter subject = null; float best = -1;
            foreach (var f in d.Fighters) if (f.IsAlive && !f.IsHuman) { float sp = f.VisualVelocity.magnitude; if (sp > best) { best = sp; subject = f; } }
            if (subject == null) break;
            var p = subject.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(subject.VisualVelocity.sqrMagnitude > .1f ? subject.VisualVelocity : subject.transform.forward, Vector3.up).normalized);
            cam.transform.position = p + side * 3.4f + Vector3.up * 1.1f; cam.transform.LookAt(p + Vector3.up * .2f);
            string file = Path.GetFullPath($"Docs/Claude_Capturas/Personajes_ronda_{i}.png");
            ClaudeBridge.CaptureCamera(cam, file, 1000, 800);
            list += $"{i}: {subject.DisplayName} v={best:0.0}\n";
        }
        Object.Destroy(cam.gameObject);
        return list;
    }
}
