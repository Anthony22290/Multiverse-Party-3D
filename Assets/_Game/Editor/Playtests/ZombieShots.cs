using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class ZombieShots
{
    static async Task Until(Func<bool> c, float s) { var d = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < d) await Task.Delay(15); }
    static SurvivalDirector dir; static void Shot(string n) { Directory.CreateDirectory("Docs/Claude_Capturas/Minijuegos"); ClaudeBridge.CaptureCamera(dir.View.GetComponent<Camera>(), Path.GetFullPath("Docs/Claude_Capturas/Minijuegos/" + n + ".png"), 1280, 720); }
    public static async Task<string> Run()
    {
        SurvivalDirector d = null; await Until(() => (d = Object.FindAnyObjectByType<SurvivalDirector>()) != null && d.HUD != null && d.Level != null, 10);
        dir = d; var sb = new StringBuilder(); d.StartGame(4);
        await Until(() => d.State == SurvivalDirector.Phase.Wave && SurvivalZombie.All.Count > 3, 30);
        var p = d.Team.First(t => t.IsHuman); var fwd = p.transform.forward; fwd.y = 0;
        SurvivalPowerUp.Drop(d, SurvivalPowerUp.Kind.Medkit, p.transform.position + fwd * 3 - Vector3.Cross(Vector3.up, fwd) * 2);
        SurvivalPowerUp.Drop(d, SurvivalPowerUp.Kind.Rage, p.transform.position + fwd * 3.5f);
        SurvivalPowerUp.Drop(d, SurvivalPowerUp.Kind.Bomb, p.transform.position + fwd * 3 + Vector3.Cross(Vector3.up, fwd) * 2);
        await Task.Delay(700); Shot("Z1_Objetos");
        sb.Append("powerups=" + Object.FindObjectsByType<SurvivalPowerUp>(FindObjectsSortMode.None).Length + "\n");
        var rage = Object.FindObjectsByType<SurvivalPowerUp>(FindObjectsSortMode.None).FirstOrDefault(u => u.kind == SurvivalPowerUp.Kind.Rage); if (rage != null) p.Teleport(rage.transform.position); await Task.Delay(400);
        sb.Append("raging=" + p.Raging + "\n"); Shot("Z2_Furia");
        await Until(() => SurvivalZombie.All.Any(z => Vector3.Distance(z.transform.position, p.transform.position) < 12), 20);
        int before = SurvivalZombie.All.Count;
        var bomb = Object.FindObjectsByType<SurvivalPowerUp>(FindObjectsSortMode.None).FirstOrDefault(u => u.kind == SurvivalPowerUp.Kind.Bomb);
        var near = SurvivalZombie.All.OrderBy(z => Vector3.Distance(z.transform.position, p.transform.position)).First();
        if (bomb != null) { bomb.transform.position = near.transform.position; p.Teleport(near.transform.position + Vector3.forward * .5f); }
        await Task.Delay(120); Shot("Z3_Bomba");
        sb.Append("zombies before bomb=" + before + " after=" + SurvivalZombie.All.Count + "\n");
        await Task.Delay(1500); return sb.ToString();
    }
}
