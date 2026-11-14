using System; using System.IO; using System.Linq; using System.Reflection; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class MortalShots
{
    static async Task Until(Func<bool> c, float s) { var d = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < d) await Task.Delay(15); }
    static void Shot(string n) { Directory.CreateDirectory("Docs/Claude_Capturas/Minijuegos"); ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/Claude_Capturas/Minijuegos/" + n + ".png")); }
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<MortalCodeDirector>(); var sb = new StringBuilder(); var h = d.Human;
        await Until(() => d.Running, 10);
        // Stand next to a rival, facing it, so the notebook fills in.
        var rival = d.Actors[1]; var cc = h.Controller; cc.enabled = false;
        h.transform.position = rival.transform.position - rival.transform.forward * 5; h.transform.LookAt(rival.transform.position); cc.enabled = true;
        typeof(MortalCodeCamera).GetProperty("Yaw").SetValue(d.View, h.transform.eulerAngles.y);
        await Task.Delay(1500); Shot("M1_Libreta");
        sb.Append("known=" + d.KnownCodes(h).Count() + "\n");
        // A pickup right in front of the player, and the nearest terminal hologram.
        var pk = MortalCodePickup.Create(d, MortalCodePickup.Kind.Scanner, h.transform.position + h.transform.forward * 3);
        await Task.Delay(600); Shot("M2_Escaner");
        cc.enabled = false; h.transform.position = pk.transform.position; cc.enabled = true; await Task.Delay(500);
        sb.Append("after scan known=" + d.KnownCodes(h).Count() + "\n"); Shot("M3_Escaneado");
        var t = d.NearestTerminal(h.transform.position); cc.enabled = false; h.transform.position = t.UsePoint + t.transform.forward * 4; h.transform.LookAt(t.transform.position); cc.enabled = true;
        typeof(MortalCodeCamera).GetProperty("Yaw").SetValue(d.View, h.transform.eulerAngles.y);
        await Task.Delay(900); Shot("M4_Terminal");
        // Let the bots play fast until somebody is eliminated, then capture the feed and FX.
        Time.timeScale = 3; int feed = d.Feed.Count; await Until(() => d.Feed.Count > feed || d.Finished, 80); Time.timeScale = 1;
        var victim = d.Actors.FirstOrDefault(a => !a.Alive && a != h);
        if (victim != null) { cc.enabled = false; h.transform.position = victim.transform.position - Vector3.forward * 6 + Vector3.up; h.transform.LookAt(victim.transform.position); cc.enabled = true; typeof(MortalCodeCamera).GetProperty("Yaw").SetValue(d.View, h.transform.eulerAngles.y); }
        await Task.Delay(150); Shot("M5_Eliminacion");
        sb.Append("feed=" + string.Join(" | ", d.Feed) + " pickups=" + Object.FindObjectsByType<MortalCodePickup>(FindObjectsSortMode.None).Length + "\n");
        Time.timeScale = 5; await Until(() => d.Remaining < 28 || d.Finished, 120); Time.timeScale = 1; await Task.Delay(300); Shot("M6_MuerteSubita");
        Time.timeScale = 5; await Until(() => d.Finished, 120); Time.timeScale = 1; await Task.Delay(500); Shot("M7_Resultados");
        sb.Append("result=" + d.Result + "\n" + d.Ranking);
        return sb.ToString();
    }
}
