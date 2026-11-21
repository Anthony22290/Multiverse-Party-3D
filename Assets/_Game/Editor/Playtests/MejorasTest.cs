#if UNITY_EDITOR
using System; using System.Linq; using System.Threading.Tasks; using UnityEngine; using UnityEngine.SceneManagement; using Object = UnityEngine.Object;

/// <summary>
/// Pruebas de las mejoras del 2026-11-19 (en Play dentro de MenuPrincipal):
///   run MejorasTest.Settings → abre Ajustes (para capturar el botón de dificultad de los bots).
///   run MejorasTest.Intro    → fiesta sin conexión de 4 con solo Patata Caliente; vuelve cuando la tarjeta «cómo se juega» está abierta.
///   run MejorasTest.After    → comprueba que el juego arrancó al cerrarse la tarjeta.
///   run MejorasTest.Spectate → elimina a tu personaje y comprueba que la cámara sigue a otro.
/// </summary>
public static class MejorasTest
{
    static async Task Until(Func<bool> c, float s) { var t = DateTime.UtcNow.AddSeconds(s); while (!c() && DateTime.UtcNow < t) await Task.Delay(30); }

    public static string Settings()
    {
        var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).OrderByDescending(c => c.sortingOrder).FirstOrDefault();
        if (canvas == null) return "FAIL sin canvas";
        MultiverseSettingsPanel.Toggle(canvas.transform);
        return "OK ajustes abiertos · bots " + MultiverseSettings.BotLevelName;
    }

    public static async Task<string> Intro()
    {
        if (MultiverseSettingsPanel.IsOpen) Settings();
        MultiverseParty.ChosenPlaylist.Clear(); MultiverseParty.ChosenPlaylist.Add(PartyGames.Potato);
        MultiverseParty.StartOffline(4);
        await Until(() => SceneManager.GetActiveScene().name == "PatataCaliente" && PartyGameIntro.Holding, 40);
        return PartyGameIntro.Holding ? $"OK tarjeta abierta · timeScale={Time.timeScale}" : "FAIL no se abrió la tarjeta (escena " + SceneManager.GetActiveScene().name + ")";
    }

    public static async Task<string> After()
    {
        await Until(() => !PartyGameIntro.Holding, 10);
        var d = Object.FindAnyObjectByType<PatataDirector>();
        await Until(() => d != null && d.State == PatataDirector.Phase.Playing, 15);
        bool ok = !PartyGameIntro.Holding && Time.timeScale == 1f && d != null && d.State == PatataDirector.Phase.Playing;
        return (ok ? "PASS" : "FAIL") + $" tarjeta cerrada, timeScale={Time.timeScale}, fase={d?.State}";
    }

    public static async Task<string> Spectate()
    {
        var d = Object.FindAnyObjectByType<PatataDirector>(); if (d == null) return "FAIL sin PatataDirector";
        d.Human.Explode();
        await Task.Delay(2500);
        var w = d.View.Watching;
        return (w != null && w.Alive && w != d.Human ? "PASS" : "FAIL") + " la cámara sigue a " + (w != null ? w.DisplayName : "nadie");
    }
}
#endif
