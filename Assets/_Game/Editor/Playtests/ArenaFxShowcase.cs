using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Triggers each new effect in front of the camera during Play and captures the Game view.</summary>
public static class ArenaFxShowcase
{
    static async Task Shot(string name, int delayMs)
    {
        await Task.Delay(delayMs);
        Directory.CreateDirectory("Docs/Claude_Capturas");
        ScreenCapture.CaptureScreenshot("Docs/Claude_Capturas/ArenaFx_" + name + ".png");
        await Task.Delay(400);
    }

    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        if (PirateSession.Instance != null) typeof(PirateSession).GetMethod("SetMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(PirateSession.Instance, new object[] { false });
        await Task.Delay(3600); // countdown runs; "¡AL ABORDAJE!" appears
        await Shot("Inicio", 0);
        var cam = d.ArenaCamera.transform;
        Vector3 ahead = cam.position + Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized * 7f; ahead.y = d.DeckY;
        PirateArenaAudio.Play(PirateArenaAudio.Sound.CannonBlast, ahead);
        await Shot("Explosion", 180);
        Vector3 side = new Vector3(d.Art.Map.Width * .5f + 2.5f, .1f, cam.position.z + 6f);
        PirateArenaAudio.Play(PirateArenaAudio.Sound.Splash, side);
        await Shot("Chapuzon", 220);
        var bot = d.Fighters.Where(f => !f.IsHuman && f.IsAlive).OrderBy(f => Vector3.Distance(f.transform.position, ahead)).First();
        PirateArenaAudio.Play(PirateArenaAudio.Sound.Punch, bot.transform.position + Vector3.up * .6f);
        await Shot("Golpe", 90);
        for (int i = 0; i < 3; i++) { bot.TakeCombatHit(Vector3.zero); await Task.Delay(120); }
        await Shot("KO", 1400);
        PirateArenaFx.Confetti(ahead + Vector3.up);
        await Shot("Confeti", 700);
        return "capturas: Inicio, Explosion, Chapuzon, Golpe, KO, Confeti (bot " + bot.DisplayName + ")";
    }
}
