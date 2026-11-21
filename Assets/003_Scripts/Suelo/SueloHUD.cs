using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Suelo que se Cae.</summary>
public sealed class SueloHUD : MonoBehaviour
{
    public SueloDirector Director;
    static readonly Color Title = new Color(1f, .6f, .4f);

    void OnGUI()
    {
        var d = Director; if (d == null) return;
        PartyHud.Begin(new Color(.18f, .06f, .05f, .86f), new Color(1f, .55f, .3f, .9f));
        var me = d.Human;
        PartyHud.Header("SUELO QUE SE CAE", d.State == SueloDirector.Phase.Finished ? "¡fin!" : "quedan " + d.AliveCount + "/" + d.Players.Count + "   ·   " + PartyHud.Clock(d.TimeLeft), Title, d.State == SueloDirector.Phase.Playing && d.TimeLeft < 10f);
        PartyHud.Players(d.Ranking().Select(p => (p.DisplayName, p.Color, p.Out ? "<color=#FF8866>lava</color>" : "piso " + (d.FloorOf(p.transform.position.y) + 1), p.Out, p.IsHuman)).ToList());
        PartyHud.Feed(d.Feed);
        if (d.State == SueloDirector.Phase.Waiting) PartyHud.Waiting(d.WaitingText);
        if (d.State == SueloDirector.Phase.Countdown) PartyHud.Countdown(d.Countdown, Title,
            "Cada baldosa que pisas se rompe y cae enseguida: ¡no te quedes quieto!",
            "Si caes por un agujero bajas al piso de abajo. Del último piso… a la lava. Gana el último en pie.",
            "WASD moverte · Espacio saltar (salta los agujeros pequeños)");
        if (d.State != SueloDirector.Phase.Finished) PartyHud.BigText(d.BigText, d.BigColor);
        if (me != null && me.Out && d.State == SueloDirector.Phase.Playing && string.IsNullOrEmpty(d.BigText)) PartyHud.Spectating(d.Watching != null ? d.Watching.DisplayName : null, "Estás fuera");
        if (me != null && !me.Out && d.State == SueloDirector.Phase.Playing)
            PartyHud.Label(new Rect(0, PartyHud.H - 50, PartyHud.W, 30), "piso " + (d.FloorOf(me.transform.position.y) + 1) + " de 3", PartyHud.Small, new Color(1, 1, 1, .85f));
        if (d.State == SueloDirector.Phase.Finished)
        {
            var rank = d.Ranking();
            PartyHud.Results(d.Result, Title, rank.Select((p, i) => (p.DisplayName, p.Color, i == 0 ? "¡último en pie!" : p.Out ? "a la lava" : "aguantó", p.IsHuman)).ToList());
        }
        if (d.Paused) PartyHud.Pause(() => d.Paused = false, d.Quit);
    }
}
