using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Rey de la Colina: clock, points (● = flag owner), the flag, bomb and shove meters, results.</summary>
public sealed class ColinaHUD : MonoBehaviour
{
    public ColinaDirector Director;
    static readonly Color Title = new Color(1f, .85f, .35f);

    void OnGUI()
    {
        var d = Director; if (d == null || d.Points == null) return;
        PartyHud.Begin(new Color(.08f, .12f, .2f, .86f), new Color(1f, .82f, .3f, .9f));
        PartyHud.Header("REY DE LA COLINA", d.State == ColinaDirector.Phase.Finished ? "¡fin!" : PartyHud.Clock(d.TimeLeft), Title, d.State == ColinaDirector.Phase.Playing && d.TimeLeft < 10f);
        PartyHud.Players(d.Ranking().Select(p => (p.DisplayName, p.Color, Mathf.FloorToInt(d.Points[p.Slot]) + (d.FlagOwner == p.Slot ? " <color=#FFD54A>●</color>" : ""), p.Out, p.IsHuman)).ToList());
        PartyHud.Feed(d.Feed);
        if (d.State == ColinaDirector.Phase.Playing)
        {
            var owner = d.FlagOwner >= 0 ? d.Players[d.FlagOwner] : null;
            string line = owner == null ? "la bandera está libre: ¡sube a la cima!" : "bandera de <b>" + owner.DisplayName + "</b>" + (d.AtTop(owner) ? "" : " · ¡no está arriba, róbala!");
            PartyHud.Label(new Rect(PartyHud.W * .5f - 260, 78, 520, 26), line, PartyHud.Tiny, owner != null ? Color.Lerp(owner.Color, Color.white, .3f) : new Color(1, 1, 1, .85f));
        }
        if (d.State == ColinaDirector.Phase.Waiting) PartyHud.Waiting(d.WaitingText);
        if (d.State == ColinaDirector.Phase.Countdown) PartyHud.Countdown(d.Countdown, Title,
            "Sube por las rampas hasta la cima: el primero que llega pinta la bandera de su color y suma 1 punto por segundo.",
            "Cambia de color cuando otro llega arriba y tú no estás · tira bombas y empuja · si caes al vacío vuelves abajo",
            "WASD moverte · Espacio saltar · E o clic izq. bomba · Shift o clic der. empujón");
        if (d.State != ColinaDirector.Phase.Finished) PartyHud.BigText(d.BigText, d.BigColor);
        var me = d.Human;
        if (me != null && d.State == ColinaDirector.Phase.Playing && !me.Out)
        {
            var r = new Rect(PartyHud.W * .5f - 230, PartyHud.H - 78, 460, 62);
            MultiverseUI.DrawPanel(r, PartyHud.Panel, PartyHud.Edge);
            bool mine = d.FlagOwner == me.Slot;
            PartyHud.Label(new Rect(r.x, r.y + 4, r.width, 24), mine ? "¡LA BANDERA ES TUYA! sumando puntos" : d.AtTop(me) ? "¡en la cima! quédate para robarla" : "sube a la cima", PartyHud.Small, mine ? Title : Color.white);
            float bomb = d.BombReady(me), dash = me.DashReady;
            PartyHud.Label(new Rect(r.x + 18, r.y + 34, 80, 22), "bomba", PartyHud.Left, new Color(1, 1, 1, .85f));
            MultiverseUI.DrawMeter(new Rect(r.x + 92, r.y + 43, 120, 7), bomb, new Color(0, 0, 0, .5f), bomb >= 1f ? new Color(1f, .6f, .3f) : new Color(.7f, .7f, .7f));
            PartyHud.Label(new Rect(r.x + 240, r.y + 34, 90, 22), "empujón", PartyHud.Left, new Color(1, 1, 1, .85f));
            MultiverseUI.DrawMeter(new Rect(r.x + 322, r.y + 43, 120, 7), dash, new Color(0, 0, 0, .5f), dash >= 1f ? new Color(.5f, 1f, .5f) : new Color(.7f, .7f, .7f));
        }
        if (d.State == ColinaDirector.Phase.Finished)
            PartyHud.Results(d.Result, Title, d.Ranking().Select(p => (p.DisplayName, p.Color, Mathf.FloorToInt(d.Points[p.Slot]) + " s de bandera", p.IsHuman)).ToList());
        if (d.Paused) PartyHud.Pause(() => d.Paused = false, d.Quit);
    }
}
