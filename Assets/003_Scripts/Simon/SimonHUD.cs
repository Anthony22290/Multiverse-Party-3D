using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Memoria Musical: round, notes, whose turn, lives, the arrow pad legend, results.</summary>
public sealed class SimonHUD : MonoBehaviour
{
    public SimonDirector Director;
    static readonly Color Title = new Color(1f, .6f, .95f);

    void OnGUI()
    {
        var d = Director; if (d == null || d.Status == null) return;
        PartyHud.Begin(new Color(.12f, .06f, .2f, .86f), new Color(1f, .55f, .95f, .9f));
        string sub = d.State switch
        {
            SimonDirector.Phase.Finished => "¡fin!",
            SimonDirector.Phase.Waiting or SimonDirector.Phase.Countdown => "ronda 1",
            _ => "ronda " + d.Round + " · " + d.Length + " notas",
        };
        bool inputTime = (d.State == SimonDirector.Phase.Input || d.State == SimonDirector.Phase.Show) && d.Elapsed >= d.InputStart;
        PartyHud.Header("MEMORIA MUSICAL", sub, Title, inputTime && d.InputEnd - d.Elapsed < 3f);
        PartyHud.Players(d.Ranking().Select(p => (p.DisplayName, p.Color, p.Out ? "fuera" : new string('♥', p.Lives) + (d.Status[p.Slot] == 1 && d.State != SimonDirector.Phase.Show ? " <color=#7CFF8A>bien</color>" : ""), p.Out, p.IsHuman)).ToList());
        PartyHud.Feed(d.Feed);
        if (d.State == SimonDirector.Phase.Waiting) PartyHud.Waiting(d.WaitingText);
        if (d.State == SimonDirector.Phase.Countdown) PartyHud.Countdown(d.Countdown, Title,
            "El tablero toca una secuencia de colores y notas: memorízala y repítela en el mismo orden.",
            "Cada ronda una nota más y un poco más rápido · un fallo o quedarte sin tiempo = pierdes una vida (tienes 2)",
            "↑ rojo · → azul · ↓ verde · ← amarillo   (también W D S A, 1 2 3 4 o la cruceta del mando)");
        if (d.State != SimonDirector.Phase.Finished) PartyHud.BigText(d.BigText, d.BigColor);

        var me = d.Human;
        if (me != null && (d.State == SimonDirector.Phase.Show || d.State == SimonDirector.Phase.Input || d.State == SimonDirector.Phase.Judge))
        {
            var r = new Rect(PartyHud.W * .5f - 300, PartyHud.H - 92, 600, 76);
            MultiverseUI.DrawPanel(r, PartyHud.Panel, PartyHud.Edge);
            string line; Color c = Color.white;
            if (me.Out) { line = "Estás fuera · mira quién aguanta más"; c = new Color(1, 1, 1, .7f); }
            else if (d.State == SimonDirector.Phase.Judge) { line = d.Status[me.Slot] == 1 ? "¡Bien hecho!" : d.AllFailedLast ? "Se repite la ronda" : "Has perdido una vida"; c = d.Status[me.Slot] == 1 ? new Color(.5f, 1f, .55f) : new Color(1f, .6f, .5f); }
            else if (!inputTime) { line = "MIRA Y ESCUCHA…"; c = new Color(.7f, .85f, 1f); }
            else if (d.Status[me.Slot] != 0 || d.MyPressed >= d.Length) { line = "¡Hecho! espera a los demás"; c = new Color(.5f, 1f, .55f); }
            else { line = "¡TU TURNO!  nota " + (d.MyPressed + 1) + " de " + d.Length + "  ·  " + Mathf.CeilToInt(d.InputEnd - d.Elapsed) + " s"; c = new Color(1f, .85f, .4f); }
            PartyHud.Label(new Rect(r.x, r.y + 6, r.width, 30), line, PartyHud.Small, c);
            // Legend with the four colours.
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(r.x + 60 + i * 125, r.y + 42, 110, 26);
                MultiverseUI.DrawPanel(b, new Color(SimonDirector.PadColors[i].r * .6f, SimonDirector.PadColors[i].g * .6f, SimonDirector.PadColors[i].b * .6f, .95f), SimonDirector.PadColors[i]);
                PartyHud.Label(b, SimonDirector.PadKeys[i] + " " + SimonDirector.PadNames[i], PartyHud.Tiny, Color.white);
            }
        }
        if (d.State == SimonDirector.Phase.Finished)
            PartyHud.Results(d.Result, Title, d.Ranking().Select(p => (p.DisplayName, p.Color, (p.Out ? "fuera" : p.Lives + " ♥") + " · " + d.TotalTime[p.Slot].ToString("0.0") + " s", p.IsHuman)).ToList());
        if (d.Paused) PartyHud.Pause(() => d.Paused = false, d.Quit);
    }
}
