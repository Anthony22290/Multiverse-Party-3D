using System; using System.IO; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class GPPlayShots
{
    static async Task Until(Func<bool> c, float s) { var d = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < d) await Task.Delay(10); }
    static void Shot(string n) => ScreenCapture.CaptureScreenshot(Path.GetFullPath("Docs/Claude_Capturas/Carreras/" + n + ".png"));
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<CrazyKartDirector>(); var p = d.Karts.First(k => k.IsHuman); var sb = new StringBuilder();
        await Until(() => d.Countdown < 2.5f, 6); Shot("60_Salida"); await Task.Delay(500);
        p.IsHuman = false;
        var spots = new (string name, Vector2 at)[] { ("61_Pueblo", new Vector2(253, 33)), ("62_Puente", new Vector2(340, -59)), ("63_Montana", new Vector2(236, -114)), ("64_Tunel", new Vector2(150, -236)),
            ("65_Acantilado", new Vector2(52, -252)), ("66_Viaducto", new Vector2(-69, -231)), ("67_Salto_rio", new Vector2(-223, -50)), ("68_Penascos", new Vector2(-229, 30)) };
        foreach (var s in spots)
        {
            Time.timeScale = 3;
            await Until(() => Vector2.Distance(new Vector2(p.transform.position.x, p.transform.position.z), s.at) < 9, 90);
            Time.timeScale = 1; await Task.Delay(60); Shot(s.name); sb.Append(s.name + " pos=" + p.transform.position.ToString("F0") + " v=" + p.Speed.ToString("F0") + "\n"); await Task.Delay(350);
        }
        Time.timeScale = 1; return sb.ToString();
    }
}
