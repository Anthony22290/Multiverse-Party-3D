using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Pintura Loca: clock, share of the floor of every player (bar), notices, results.</summary>
public sealed class PinturaHUD : MonoBehaviour
{
    public PinturaDirector Director;
    static readonly Color Title = new Color(1f, .6f, .85f);

    void OnGUI()
    {
        var d = Director; if (d == null || d.Counts == null) return;
        PartyHud.Begin(new Color(.12f, .08f, .2f, .86f), new Color(1f, .6f, .85f, .9f));
        int total = PinturaDirector.N * PinturaDirector.N;
        PartyHud.Header("PINTURA LOCA", d.State == PinturaDirector.Phase.Finished ? "¡fin!" : PartyHud.Clock(d.TimeLeft), Title, d.State == PinturaDirector.Phase.Playing && d.TimeLeft < 10f);
        PartyHud.Players(d.Ranking().Select(p => (p.DisplayName, p.Color, Mathf.RoundToInt(d.Counts[p.Slot] * 100f / total) + "%", false, p.IsHuman)).ToList());
        // Share bar along the top.
        var bar = new Rect(PartyHud.W * .5f - 300, 80, 600, 14); float x = bar.x;
        GUI.color = new Color(1, 1, 1, .25f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
        foreach (var p in d.Ranking()) { float w = bar.width * d.Counts[p.Slot] / total; GUI.color = p.Color; GUI.DrawTexture(new Rect(x, bar.y, w, bar.height), Texture2D.whiteTexture); x += w; }
        GUI.color = Color.white;
        PartyHud.Feed(d.Feed);
        if (d.State == PinturaDirector.Phase.Waiting) PartyHud.Waiting(d.WaitingText);
        if (d.State == PinturaDirector.Phase.Countdown) PartyHud.Countdown(d.Countdown, Title,
            "Pinta de tu color todas las baldosas que pises. Gana quien tenga más suelo a los 90 s.",
            "Los cubos de pintura pintan un círculo entero · un empujón deja al otro mareado",
            "WASD moverte · Espacio saltar · Shift o clic empujón");
        if (d.State != PinturaDirector.Phase.Finished) PartyHud.BigText(d.BigText, d.BigColor);
        var me = d.Human;
        if (me != null && d.State == PinturaDirector.Phase.Playing)
        {
            var r = new Rect(PartyHud.W * .5f - 200, PartyHud.H - 70, 400, 52);
            MultiverseUI.DrawPanel(r, PartyHud.Panel, PartyHud.Edge);
            PartyHud.Label(new Rect(r.x, r.y + 4, r.width, 24), "tu suelo: " + Mathf.RoundToInt(d.Counts[me.Slot] * 100f / total) + "%", PartyHud.Small, me.Color);
            MultiverseUI.DrawMeter(new Rect(r.x + 30, r.y + 34, r.width - 60, 7), me.DashReady, new Color(0, 0, 0, .5f), me.DashReady >= 1f ? new Color(.5f, 1f, .5f) : new Color(.8f, .8f, .8f));
        }
        if (d.State == PinturaDirector.Phase.Finished)
            PartyHud.Results(d.Result, Title, d.Ranking().Select(p => (p.DisplayName, p.Color, Mathf.RoundToInt(d.Counts[p.Slot] * 100f / total) + "% del suelo", p.IsHuman)).ToList());
        if (d.Paused) PartyHud.Pause(() => d.Paused = false, d.Quit);
    }
}
