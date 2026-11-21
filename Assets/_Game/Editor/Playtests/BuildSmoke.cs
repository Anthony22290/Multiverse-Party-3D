#if UNITY_EDITOR
using System; using System.Diagnostics; using System.IO; using System.Linq; using System.Threading.Tasks;

/// <summary>Abre la última build de Windows 25 s, la cierra y resume su Player.log (errores y modo de pantalla). run BuildSmoke.Run</summary>
public static class BuildSmoke
{
    public static async Task<string> Run()
    {
        string exe = Path.GetFullPath(Path.Combine("Builds", "MultiverseParty3D_" + DateTime.Now.ToString("yyyy-MM-dd"), "MultiverseParty3D.exe"));
        if (!File.Exists(exe)) return "FAIL no existe " + exe;
        string log = Path.GetFullPath("Docs/Claude_Pruebas/build_player.log"); Directory.CreateDirectory(Path.GetDirectoryName(log)); if (File.Exists(log)) File.Delete(log);
        var p = Process.Start(new ProcessStartInfo(exe, "-logFile \"" + log + "\"") { UseShellExecute = false });
        await Task.Delay(25000);
        bool alive = !p.HasExited; if (alive) { p.Kill(); p.WaitForExit(5000); }
        await Task.Delay(500);
        var lines = File.Exists(log) ? File.ReadAllLines(log) : new string[0];
        var screen = lines.Where(l => l.StartsWith("Pantalla:")).ToArray();
        var errors = lines.Where(l => l.Contains("Exception") || l.StartsWith("Error") || l.Contains("NullReference")).Distinct().Take(8).ToArray();
        return $"{(alive && screen.Length > 0 ? "PASS" : "FAIL")} abierta 25 s={alive} · líneas de log {lines.Length}\n  " + string.Join("\n  ", screen) + "\n  errores: " + errors.Length + (errors.Length > 0 ? "\n  " + string.Join("\n  ", errors) : "");
    }
}
#endif
