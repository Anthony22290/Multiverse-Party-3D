using UnityEngine;

/// <summary>Screen UI for Reparto Loco: clock, deliveries of everybody, where your parcel goes, dash, notices and results.</summary>
public sealed class RepartoHUD : MonoBehaviour
{
    public RepartoDirector Director;
    GUIStyle big, mid, small, tiny, left;
    static readonly Color Panel = new Color(.08f, .1f, .16f, .84f), Edge = new Color(1f, .78f, .3f, .9f);

    void Styles()
    {
        if (big != null) return;
        GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
        big = S(MultiverseUI.DisplayFont, 96, TextAnchor.MiddleCenter); mid = S(MultiverseUI.DisplayFont, 34, TextAnchor.MiddleCenter);
        small = S(MultiverseUI.BodyFont, 19, TextAnchor.MiddleCenter); tiny = S(MultiverseUI.BodyFont, 15, TextAnchor.MiddleCenter); left = S(MultiverseUI.BodyFont, 17, TextAnchor.MiddleLeft);
        left.wordWrap = false;
    }
    static void Shadowed(Rect r, string text, GUIStyle s, Color c)
    {
        var old = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, .75f * c.a); GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
        s.normal.textColor = c; GUI.Label(r, text, s); s.normal.textColor = old;
    }

    void OnGUI()
    {
        var d = Director; if (d == null || d.Scores == null) return;
        Styles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        float W = Screen.width / scale, H = Screen.height / scale; var me = d.Human;

        MultiverseUI.DrawPanel(new Rect(W * .5f - 190, 10, 380, 64), Panel, Edge);
        Shadowed(new Rect(W * .5f - 190, 12, 380, 28), "REPARTO LOCO", small, new Color(1f, .82f, .4f));
        int secs = Mathf.CeilToInt(d.TimeLeft);
        Color clock = d.State == RepartoDirector.Phase.Playing && secs <= 10 ? Color.Lerp(Color.white, new Color(1f, .4f, .3f), .5f + .5f * Mathf.Sin(Time.time * 10f)) : Color.white;
        Shadowed(new Rect(W * .5f - 190, 38, 380, 32), d.State == RepartoDirector.Phase.Finished ? "¡fin!" : (secs / 60) + ":" + (secs % 60).ToString("00"), mid, clock);

        var list = d.Ranking();
        MultiverseUI.DrawPanel(new Rect(W - 262, 84, 244, 22 + list.Count * 32), Panel, Edge);
        float y = 94;
        foreach (var p in list)
        {
            string carry = p.Carry > 0 ? " ↑" : p.Carry < 0 ? " ↓" : "";
            Shadowed(new Rect(W - 250, y, 170, 28), (p.IsHuman ? "▶ " : "") + p.DisplayName + "<color=#C8A070>" + carry + "</color>", left, p.Color);
            Shadowed(new Rect(W - 82, y, 60, 28), d.Scores[p.Slot].ToString(), left, Color.white);
            y += 32;
        }
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 92 + i * 26, 520, 24), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));

        if (me != null && d.CanMove)
        {
            string goal = me.Carry > 0 ? "Lleva el pedido al <color=#88C8FF>BARRIO</color> ↑ (arriba)" : me.Carry < 0 ? "Lleva el pedido a la <color=#FF9C88>TIENDA</color> ↓ (abajo)" : "<color=#FF9C88>Sin pedido:</color> ve a la tienda o al barrio a por otro";
            var r = new Rect(W * .5f - 250, H - 96, 500, 80);
            MultiverseUI.DrawPanel(r, Panel, Edge);
            Shadowed(new Rect(r.x, r.y + 6, r.width, 26), goal, small, Color.white);
            MultiverseUI.DrawMeter(new Rect(r.x + 40, r.y + 52, r.width - 80, 8), me.DashReady, new Color(0, 0, 0, .5f), me.DashReady >= 1f ? new Color(.5f, 1f, .5f) : new Color(.8f, .8f, .8f));
            Shadowed(new Rect(r.x, r.y + 32, r.width, 20), me.DashReady >= 1f ? "Shift o clic: acelerón" : "acelerón…", tiny, new Color(1, 1, 1, .85f));
        }

        if (d.State == RepartoDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == RepartoDirector.Phase.Countdown) { int n = Mathf.CeilToInt(d.Countdown); Shadowed(new Rect(0, H * .24f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(1f, .82f, .4f)); Help(W, H); }
        if (!string.IsNullOrEmpty(d.BigText) && d.State != RepartoDirector.Phase.Finished) Shadowed(new Rect(0, H * .3f - 50, W, 100), d.BigText, mid, d.BigColor);
        if (!string.IsNullOrEmpty(d.Notice) && d.State == RepartoDirector.Phase.Playing)
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 320, H - 150, 640, 44), new Color(.25f, .1f, .08f, .9f), Edge);
            Shadowed(new Rect(W * .5f - 320, H - 150, 640, 44), d.Notice, small, new Color(1f, .95f, .85f));
        }

        if (d.State == RepartoDirector.Phase.Finished)
        {
            var rank = d.Ranking(); float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 310, H * .5f - h * .5f, 620, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 12, box.width, 56), d.Result, mid, new Color(1f, .82f, .4f));
            for (int i = 0; i < rank.Count; i++)
            {
                var p = rank[i];
                Shadowed(new Rect(box.x + 60, box.y + 78 + i * 34, 300, 30), (i + 1) + ".  " + p.DisplayName + (p.IsHuman ? "  (tú)" : ""), left, p.Color);
                Shadowed(new Rect(box.x + 370, box.y + 78 + i * 34, 220, 30), d.Scores[p.Slot] + " entregas" + (d.Breaks[p.Slot] > 0 ? " · " + d.Breaks[p.Slot] + " rotos" : ""), left, Color.white);
            }
            if (!MultiverseParty.InMatch) Shadowed(new Rect(box.x, box.yMax - 44, box.width, 30), "R: jugar otra vez", tiny, new Color(1, 1, 1, .8f));
        }

        if (d.Paused)
        {
            var box = new Rect(W * .5f - 220, H * .5f - 130, 440, 260);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 16, box.width, 50), "PAUSA", mid, Color.white);
            var b1 = new Rect(box.x + 70, box.y + 90, 300, 52); var b2 = new Rect(box.x + 70, box.y + 160, 300, 52);
            MultiverseUI.DrawPanel(b1, new Color(.35f, .55f, .2f, .95f), Edge); MultiverseUI.DrawPanel(b2, new Color(.55f, .18f, .14f, .95f), Edge);
            if (GUI.Button(b1, "Continuar", small)) d.Paused = false;
            if (GUI.Button(b2, MultiverseParty.InMatch ? "Salir de la fiesta" : "Salir", small)) d.Quit();
        }
    }

    void Help(float W, float H)
    {
        var box = new Rect(W * .5f - 400, H * .5f - 10, 800, 104);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "Cruza la calle con el pedido: TIENDA → BARRIO = 1 punto, y allí te dan otro para volver.", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "Los coches no matan, pero te empujan y el pedido se rompe: tendrás que ir a por otro. 90 segundos.", small, new Color(1f, .85f, .6f));
        Shadowed(new Rect(box.x, box.y + 68, box.width, 30), "WASD moverte · Shift o clic acelerón · Espacio saltar · descansa en las medianas de césped", tiny, new Color(1, 1, 1, .85f));
    }
}
