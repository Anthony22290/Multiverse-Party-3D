using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Rey de la Colina: clock, points, the zone timer, notices, results.</summary>
public sealed class ColinaHUD : MonoBehaviour
{
    public ColinaDirector Director;
    static readonly Color Title = new Color(1f, .85f, .35f);

    void OnGUI()
    {
        var d = Director; if (d == null || d.Points == null) return;
        PartyHud.Begin(new Color(.08f, .12f, .2f, .86f), new Color(1f, .82f, .3f, .9f));
        PartyHud.Header("REY DE LA COLINA", d.State == ColinaDirector.Phase.Finished ? "¡fin!" : PartyHud.Clock(d.TimeLeft), Title, d.State == ColinaDirector.Phase.Playing && d.TimeLeft < 10f);
        PartyHud.Players(d.Ranking().Select(p => (p.DisplayName, p.Color, Mathf.FloorToInt(d.Points[p.Slot]) + (d.InZone(p) ? " <color=#FFD54A>●</color>" : ""), p.Out, p.IsHuman)).ToList());
        PartyHud.Feed(d.Feed);
        if (d.State == ColinaDirector.Phase.Playing)
        {
            float t = d.ZoneTimeLeft;
            PartyHud.Label(new Rect(PartyHud.W * .5f - 200, 78, 400, 26), "la corona se mueve en " + Mathf.CeilToInt(t) + " s", PartyHud.Tiny, t < 3f ? new Color(1f, .6f, .4f) : new Color(1, 1, 1, .8f));
        }
        if (d.State == ColinaDirector.Phase.Waiting) PartyHud.Waiting(d.WaitingText);
        if (d.State == ColinaDirector.Phase.Countdown) PartyHud.Countdown(d.Countdown, Title,
            "Quédate dentro del círculo de la corona: suma 1 punto por segundo (2 si estás solo).",
            "La corona cambia de sitio cada 15 s · empuja a los demás fuera… o al agua · cuidado con los plátanos",
            "WASD moverte · Espacio saltar · Shift o clic empujón");
        if (d.State != ColinaDirector.Phase.Finished) PartyHud.BigText(d.BigText, d.BigColor);
        var me = d.Human;
        if (me != null && d.State == ColinaDirector.Phase.Playing && !me.Out)
        {
            var r = new Rect(PartyHud.W * .5f - 200, PartyHud.H - 70, 400, 52);
            MultiverseUI.DrawPanel(r, PartyHud.Panel, PartyHud.Edge);
            bool inside = d.InZone(me);
            PartyHud.Label(new Rect(r.x, r.y + 4, r.width, 24), inside ? "¡EN LA CORONA! sumando puntos" : "ve a la corona", PartyHud.Small, inside ? Title : Color.white);
            MultiverseUI.DrawMeter(new Rect(r.x + 30, r.y + 34, r.width - 60, 7), me.DashReady, new Color(0, 0, 0, .5f), me.DashReady >= 1f ? new Color(.5f, 1f, .5f) : new Color(.8f, .8f, .8f));
        }
        if (d.State == ColinaDirector.Phase.Finished)
            PartyHud.Results(d.Result, Title, d.Ranking().Select(p => (p.DisplayName, p.Color, Mathf.FloorToInt(d.Points[p.Slot]) + " puntos", p.IsHuman)).ToList());
        if (d.Paused) PartyHud.Pause(() => d.Paused = false, d.Quit);
    }
}
