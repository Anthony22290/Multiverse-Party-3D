using System; using System.IO; using System.Linq; using System.Reflection; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class ZombieRogueShots
{
    static SurvivalDirector d;
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(15); }
    static void Cam(string n) { Directory.CreateDirectory("Docs/Claude_Capturas/Minijuegos"); ClaudeBridge.CaptureCamera(d.View.GetComponent<Camera>(), Path.GetFullPath("Docs/Claude_Capturas/Minijuegos/" + n + ".png"), 1280, 720); }
    static void Screen(string n) => ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/Claude_Capturas/Minijuegos/" + n + ".png"));
    public static async Task<string> Run()
    {
        await Until(() => (d = Object.FindAnyObjectByType<SurvivalDirector>()) != null && d.HUD != null && d.Level != null, 10);
        var sb = new StringBuilder(); d.StartGame(2);
        var lobby = typeof(SurvivalHUD).GetField("lobby", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(d.HUD) as GameObject; if (lobby != null) lobby.SetActive(false);
        await Until(() => d.State == SurvivalDirector.Phase.Wave, 15);
        var h = d.Human; var fwd = Vector3.forward;
        // Line up one of each new enemy in front of the player.
        var kinds = new[] { SurvivalZombie.ZombieKind.Skeleton, SurvivalZombie.ZombieKind.Bomber, SurvivalZombie.ZombieKind.Blob, SurvivalZombie.ZombieKind.Ghost, SurvivalZombie.ZombieKind.Brute };
        for (int i = 0; i < kinds.Length; i++) d.Spawn(new SurvivalZombie.Spec { kind = kinds[i], wave = 3, part = -1, guard = true }, -1, h.transform.position + fwd * 9 + Vector3.right * (i - 2) * 3.2f);
        await Task.Delay(900); Cam("R1_Monstruos");
        sb.Append("alive=" + string.Join(",", SurvivalZombie.All.Select(z => z.Kind.ToString()).Distinct()) + "\n");
        // Wake the bomber to see a throw.
        var bomber = SurvivalZombie.All.First(z => z.Kind == SurvivalZombie.ZombieKind.Bomber); bomber.TakeHit(1, Vector3.forward, null);
        await Until(() => Object.FindAnyObjectByType<SurvivalBomb>() != null, 8); await Task.Delay(700); Cam("R2_Bomba_lanzada");
        // Guarded cache with its pointer.
        var cache = d.Level.Caches[0]; cache.Arm(d, 5, 1); await Task.Delay(600); Screen("R3_Pieza_custodiada"); await Task.Delay(400);
        sb.Append("guards=" + cache.Guards.Count + " sector=" + cache.SectorName + "\n");
        foreach (var z in SurvivalZombie.All.ToArray()) z.TakeHit(9999, Vector3.forward, null);
        await Task.Delay(500);
        // Perk choice: offer to the human directly.
        typeof(SurvivalDirector).GetField("<State>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(d, SurvivalDirector.Phase.Break); typeof(SurvivalDirector).GetProperty("BreakLeft").SetValue(d, 30f); d.PerkUI.Offer(h); await Task.Delay(500); Screen("R4_Mejoras"); await Task.Delay(400);
        d.PerkUI.Choose(0); sb.Append("perks=" + SurvivalPerks.Summary(h) + "\n");
        return sb.ToString();
    }
}
