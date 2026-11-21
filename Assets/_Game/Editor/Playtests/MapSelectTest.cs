#if UNITY_EDITOR
using System; using System.Linq; using System.Reflection; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;

/// <summary>
/// Prueba de «Seleccionar mapas» (2026-11-18). En Play dentro de MenuPrincipal:
///   run MapSelectTest.Open  → elige Patata Caliente y Minigolf (en ese orden), 2 puntos, y abre el panel (para capturarlo).
///   run MapSelectTest.Run   → juega sin conexión con esa elección: comprueba el orden, que no hay votación y el campeón a 2 puntos.
/// </summary>
public static class MapSelectTest
{
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(50); }
    static void Call(object o, string m) => o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(o, null);

    public static string Open()
    {
        var menu = Object.FindAnyObjectByType<MultiverseMainMenu>(); if (menu == null) return "FAIL no hay menú";
        MultiverseParty.ChosenPlaylist.Clear(); MultiverseParty.ChosenPlaylist.AddRange(new[] { PartyGames.Potato, PartyGames.Golf, PartyGames.Potato });
        MultiverseParty.ChosenGoal = 2;
        Call(menu, "OpenMaps");
        return "OK panel abierto";
    }

    public static async Task<string> Run()
    {
        var sb = new StringBuilder(); bool ok = true;
        void Check(bool c, string what) { ok &= c; sb.AppendLine((c ? "PASS " : "FAIL ") + what); }
        var menu = Object.FindAnyObjectByType<MultiverseMainMenu>(); if (menu == null) return "FAIL no hay menú";
        Call(menu, "CloseMaps");
        MultiverseParty.ChosenPlaylist.Clear(); MultiverseParty.ChosenPlaylist.AddRange(new[] { PartyGames.Potato, PartyGames.Golf });
        MultiverseParty.ChosenGoal = 2;
        Call(menu, "Offline");
        var p = MultiverseParty.Instance;
        Check(p != null && p.Goal == 2 && p.Playlist.SequenceEqual(new[] { PartyGames.Potato, PartyGames.Golf }), $"la partida usa la lista y 2 puntos (meta {p?.Goal}, lista {string.Join(",", p?.Playlist ?? new System.Collections.Generic.List<int>())})");
        Check(p.Phase == PartyPhase.Reveal && p.Selected == PartyGames.Potato, $"sin votación: primero {PartyGames.All[Math.Max(0, p.Selected)].Title} (fase {p.Phase})");
        Check(MultiverseParty.WinScore == 2, "WinScore = 2 en la partida");
        await Until(() => p.Phase == PartyPhase.Playing && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "PatataCaliente", 25);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "PatataCaliente", "se carga Patata Caliente");
        await Task.Delay(3000);
        p.HostResolve(0);
        await Until(() => p.Phase == PartyPhase.Reveal, 25);
        Check(p.Phase == PartyPhase.Reveal && p.Selected == PartyGames.Golf, $"segundo: {PartyGames.All[Math.Max(0, p.Selected)].Title} sin votación");
        Check(p.Members[0].Score == 1, "1 punto tras el primero");
        await Task.Delay(1500);
        ClaudeBridgeShots.Request("Docs/Claude_Capturas/SeleccionMapas/Plaza_revelar.png");
        await Until(() => p.Phase == PartyPhase.Playing && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MinigolfLoco", 25);
        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "MinigolfLoco", "se carga Minigolf Loco");
        await Task.Delay(3000);
        p.HostResolve(0);
        await Until(() => p.Phase == PartyPhase.Champion, 25);
        Check(p.Phase == PartyPhase.Champion && p.Members[0].Score == 2, $"campeón al llegar a 2 puntos (fase {p.Phase}, puntos {p.Members[0].Score})");
        await Task.Delay(2500);
        ClaudeBridgeShots.Request("Docs/Claude_Capturas/SeleccionMapas/Plaza_campeon.png");
        await Task.Delay(1500);
        MultiverseParty.ChosenPlaylist.Clear(); MultiverseParty.ChosenGoal = MultiverseParty.DefaultWinScore;
        return (ok ? "PASS" : "FAIL") + "\n" + sb;
    }
}

static class ClaudeBridgeShots
{
    public static void Request(string rel)
    {
        string path = System.IO.Path.GetFullPath(rel); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        ScreenCapture.CaptureScreenshot(path, 1);
    }
}
#endif
