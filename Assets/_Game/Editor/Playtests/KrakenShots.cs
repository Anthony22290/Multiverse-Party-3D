using System; using System.IO; using System.Linq; using System.Reflection; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class KrakenShots
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static async Task Until(Func<bool> c, float s) { var d = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < d) await Task.Delay(15); }
    static void Shot(string n, PirateKrakenHazard k)
    {
        Directory.CreateDirectory("Docs/Claude_Capturas/Minijuegos");
        var target = (Vector3)typeof(PirateKrakenHazard).GetField("target", Any).GetValue(k); var b = (Vector3)typeof(PirateKrakenHazard).GetField("basePoint", Any).GetValue(k);
        var go = new GameObject("cam"); var cam = go.AddComponent<Camera>(); Vector3 side = (target - b); side.y = 0; side.Normalize();
        go.transform.position = target + Vector3.Cross(Vector3.up, side) * 13 + Vector3.up * 8 - side * 3; go.transform.LookAt((target + b) * .5f + Vector3.up * 3);
        ClaudeBridge.CaptureCamera(cam, Path.GetFullPath("Docs/Claude_Capturas/Minijuegos/" + n + ".png"), 1280, 720); Object.DestroyImmediate(go);
    }
    public static async Task<string> Run()
    {
        var a = Object.FindAnyObjectByType<PirateArenaDirector>(); var sb = new StringBuilder();
        var session = Object.FindAnyObjectByType<PirateSession>();
        if (session != null) { typeof(PirateSession).GetProperty("LocalReady").SetValue(session, true); var close = typeof(PirateSession).GetMethod("CloseMenu", Any) ?? typeof(PirateSession).GetMethod("Resume", Any); close?.Invoke(session, null); }
        await Until(() => a.IsPlaying, 10);
        sb.Append("playing=" + a.IsPlaying + "\n");
        var k = a.GetComponent<PirateKrakenHazard>(); await Task.Delay(300); typeof(PirateKrakenHazard).GetField("wasPlaying", Any).SetValue(k, true); typeof(PirateKrakenHazard).GetField("nextAt", Any).SetValue(k, Time.time + .1f);
        await Task.Delay(1100); Shot("A1_Kraken_aviso", k);
        var before = a.Fighters.Select(f => f.transform.position).ToArray();
        await Task.Delay(780); Shot("A2_Kraken_golpe", k);
        await Task.Delay(400);
        int moved = a.Fighters.Where((f, i) => Vector3.Distance(f.transform.position, before[i]) > 1.5f).Count();
        sb.Append("phase=" + typeof(PirateKrakenHazard).GetField("phase", Any).GetValue(k) + " fightersMoved=" + moved + "\n");
        return sb.ToString();
    }
}
