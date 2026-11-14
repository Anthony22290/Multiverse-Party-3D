using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
// Visual check of the kart FX pass: drives the race with AI and captures key moments.
public static class RaceFXShowcase
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static async Task Until(Func<bool> c, float s = 10) { var d = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < d) await Task.Delay(15); }
    static async Task Wait(float s) { float e = Time.time + s; await Until(() => Time.time >= e, s * 4 + 4); }
    static string Shot(string n) { var p = Path.GetFullPath("Docs/Claude_Capturas/Carreras/" + n + ".png"); ScreenCapture.CaptureScreenshot(p); return n; }
    public static async Task<string> Run()
    {
        var log = new System.Text.StringBuilder();
        var d = Object.FindAnyObjectByType<CrazyKartDirector>();
        if (d.Countdown < 2.2f) { UnityEngine.SceneManagement.SceneManager.LoadScene("CarrerasLocas"); await Task.Delay(300); await Until(() => (d = Object.FindAnyObjectByType<CrazyKartDirector>()) != null && d.Karts.Count > 0 && d.Countdown > 3); }
        var player = d.Karts.First(k => k.IsHuman);
        log.Append("shaders fresnel=").Append(Shader.Find("RaceFX/Fresnel") != null).Append(" particle=").Append(Shader.Find("RaceFX/Particle") != null).Append(" pads=").Append(Shader.Find("RaceFX/BoostChevrons") != null).Append('\n');
        await Until(() => d.Countdown <= 1.6f); log.Append(Shot("10_Semaforo")).Append('\n');
        player.IsHuman = false;            // let the AI drive the camera kart
        await Until(() => d.Playing); await Wait(.6f); log.Append(Shot("11_Salida")).Append('\n');
        await Wait(3f);
        var f = typeof(CrazyKartController);
        var kb = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        try
        {
            foreach (var k in d.Karts) if (k != player) k.gameObject.SetActive(false);
            player.CurrentCheckpoint = 0; player.Recover(); player.IsHuman = true; await Wait(.6f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            await Wait(1.3f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W, UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.LeftShift));
            await Wait(.12f); log.Append(Shot("12_Salto_derrape")).Append(" drifting=").Append(player.Drifting).Append('\n');
            await Until(() => player.DriftTier >= 1, 3); await Wait(.1f); log.Append(Shot("13_Chispas_azul")).Append(" tier=").Append(player.DriftTier).Append('\n');
            await Until(() => player.DriftTier >= 2, 3); await Wait(.1f); log.Append(Shot("14_Chispas_naranja")).Append(" tier=").Append(player.DriftTier).Append('\n');
            await Until(() => player.DriftTier >= 3, 3); await Wait(.1f); log.Append(Shot("14b_Chispas_rosa")).Append(" tier=").Append(player.DriftTier).Append('\n');
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            await Wait(.2f); log.Append(Shot("15_Turbo_rosa")).Append(" boost=").Append(player.Boosting).Append(" tier=").Append(player.LastBoostTier).Append('\n');
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            foreach (var k in d.Karts) k.gameObject.SetActive(true);
            player.IsHuman = false; player.Recover(); await Wait(1.5f);
            // Roulette and shield
            f.GetField("<HeldItem>k__BackingField", Any).SetValue(player, 0);
            player.GiveItem(4, true); await Wait(.4f); log.Append(Shot("16_Ruleta")).Append(" spinning=").Append(player.RouletteSpinning).Append('\n');
            player.IsHuman = true; await Wait(1f); player.UseItem(); player.IsHuman = false; await Wait(.3f); log.Append(Shot("17_Escudo")).Append(" shield=").Append(player.Shielded).Append('\n');
            // Mine dropped and a hit with stars
            player.GiveItem(3); player.UseItem(); await Wait(.4f);
            f.GetField("shieldUntil", Any).SetValue(player, 0f);
            player.Hit(); await Wait(.35f); log.Append(Shot("18_Trompo_estrellas")).Append('\n');
            // Storm from last place
            await Wait(1.5f);
            var last = d.Karts.OrderBy(k => d.Position(k)).Last();
            f.GetField("<HeldItem>k__BackingField", Any).SetValue(last, 0); last.GiveItem(6); last.UseItem();
            await Wait(.08f); log.Append(Shot("19_Tormenta")).Append(" shrunk=").Append(d.Karts.Count(k => k.Shrunk)).Append('\n');
            await Wait(1.2f); log.Append(Shot("20_Encogidos")).Append('\n');
            // Item box close-up
            var cd = typeof(CrazyKartDirector).GetField("countdown", Any);
            var body = player.GetComponent<Rigidbody>();
            var box = Object.FindObjectsByType<CrazyKartItemBox>(FindObjectsSortMode.None).First(bx => bx.GetComponent<Collider>().enabled);
            int near = Enumerable.Range(0, d.Waypoints.Count).OrderBy(i => Vector3.Distance(d.GetWaypointPosition(i), box.transform.position)).First();
            Vector3 dir = d.GetWaypointPosition(near + 1) - d.GetWaypointPosition(near - 1); dir.y = 0; dir.Normalize();
            cd.SetValue(d, 0.01f); await Wait(.1f); cd.SetValue(d, 100f);
            body.position = box.transform.position - dir * 7 + Vector3.down * .3f; body.rotation = Quaternion.LookRotation(dir);
            await Wait(1.2f); log.Append(Shot("21_Caja_arcoiris")).Append('\n');
            var pad = Object.FindObjectsByType<CrazyKartBoostPad>(FindObjectsSortMode.None).First();
            body.position = pad.transform.position - pad.transform.forward * 8 + Vector3.up * .6f; body.rotation = Quaternion.LookRotation(pad.transform.forward);
            await Wait(1f); log.Append(Shot("22_Placa_turbo")).Append(" padFwd=").Append(pad.transform.forward).Append(" scale=").Append(pad.transform.localScale).Append('\n');
            cd.SetValue(d, 0f);
        }
        finally { UnityEngine.InputSystem.InputSystem.RemoveDevice(kb); }
        return log.ToString();
    }
}
