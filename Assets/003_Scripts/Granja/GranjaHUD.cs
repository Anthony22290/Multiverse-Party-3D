using UnityEngine;

/// <summary>Screen UI for Gallinas Locas: clock, points of every pen, what you carry, dash, notices and results.</summary>
public sealed class GranjaHUD : MonoBehaviour
{
    public GranjaDirector Director;
    GUIStyle big, mid, small, tiny, left;
    static readonly Color Panel = new Color(.16f, .1f, .05f, .84f), Edge = new Color(1f, .82f, .4f, .9f), Gold = new Color(1f, .82f, .3f);

    void Styles()
    {
        if (big != null) return;
        GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
        big = S(MultiverseUI.DisplayFont, 96, TextAnchor.MiddleCenter); mid = S(MultiverseUI.DisplayFont, 34, TextAnchor.MiddleCenter);
        small = S(MultiverseUI.BodyFont, 19, TextAnchor.MiddleCenter); tiny = S(MultiverseUI.BodyFont, 15, TextAnchor.MiddleCenter); left = S(MultiverseUI.BodyFont, 17, TextAnchor.MiddleLeft);
        left.wordWrap = false; left.clipping = TextClipping.Clip;
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

        // Header with the clock.
        MultiverseUI.DrawPanel(new Rect(W * .5f - 190, 10, 380, 64), Panel, Edge);
        Shadowed(new Rect(W * .5f - 190, 12, 380, 28), "GALLINAS LOCAS", small, new Color(1f, .85f, .5f));
        int secs = Mathf.CeilToInt(d.TimeLeft);
        Color clock = d.State == GranjaDirector.Phase.Playing && secs <= 10 ? Color.Lerp(Color.white, new Color(1f, .4f, .3f), .5f + .5f * Mathf.Sin(Time.time * 10f)) : Color.white;
        Shadowed(new Rect(W * .5f - 190, 38, 380, 32), d.State == GranjaDirector.Phase.Finished ? "¡fin!" : (secs / 60) + ":" + (secs % 60).ToString("00"), mid, clock);

        // Pens.
        var list = d.Ranking();
        MultiverseUI.DrawPanel(new Rect(W - 262, 84, 244, 22 + list.Count * 32), Panel, Edge);
        float y = 94;
        foreach (var p in list)
        {
            int s = p.Slot;
            string extra = (d.Golds[s] > 0 ? " <color=#FFD54A>★" + d.Golds[s] + "</color>" : "") + (d.Booms[s] > 0 ? " <color=#FF8866>boom" + (d.Booms[s] > 1 ? "x" + d.Booms[s] : "") + "</color>" : "");
            Shadowed(new Rect(W - 250, y, 160, 28), (p.IsHuman ? "▶ " : "") + p.DisplayName + extra, left, p.Color);
            Shadowed(new Rect(W - 82, y, 60, 28), d.Scores[s].ToString(), left, Color.white);
            y += 32;
        }
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 92 + i * 26, 520, 24), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));

        // You: what you carry + dash.
        if (me != null && d.CanMove)
        {
            var held = d.CarriedBy(me.Slot);
            string carry = held == null ? "E o clic: coger gallina" :
                held.Pen >= 0 ? "<color=#FF7766>¡LLEVAS UNA TRAMPA! Tírala lejos (E)</color>" :
                held.Type == GranjaChicken.Kind.Gold ? "<color=#FFD54A>¡Llevas la DORADA!</color> Suéltala en tu corral (E)" : "Llevas una gallina · suéltala en tu corral (E)";
            var r = new Rect(W * .5f - 230, H - 96, 460, 80);
            MultiverseUI.DrawPanel(r, Panel, Edge);
            Shadowed(new Rect(r.x, r.y + 6, r.width, 26), carry, small, Color.white);
            MultiverseUI.DrawMeter(new Rect(r.x + 40, r.y + 52, r.width - 80, 8), me.DashReady, new Color(0, 0, 0, .5f), me.DashReady >= 1f ? new Color(.5f, 1f, .5f) : new Color(.8f, .8f, .8f));
            Shadowed(new Rect(r.x, r.y + 32, r.width, 20), me.DashReady >= 1f ? "Shift / clic der.: empujón (hace soltar la gallina)" : "empujón…", tiny, new Color(1, 1, 1, .85f));
        }

        if (d.State == GranjaDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == GranjaDirector.Phase.Countdown) { int n = Mathf.CeilToInt(d.Countdown); Shadowed(new Rect(0, H * .24f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(1f, .85f, .45f)); Help(W, H); }
        if (!string.IsNullOrEmpty(d.BigText) && d.State != GranjaDirector.Phase.Finished) Shadowed(new Rect(0, H * .3f - 50, W, 100), d.BigText, mid, d.BigColor);
        if (!string.IsNullOrEmpty(d.Notice) && d.State == GranjaDirector.Phase.Playing)
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 320, H - 150, 640, 44), new Color(.3f, .14f, .06f, .9f), Edge);
            Shadowed(new Rect(W * .5f - 320, H - 150, 640, 44), d.Notice, small, new Color(1f, .95f, .85f));
        }

        if (d.State == GranjaDirector.Phase.Finished)
        {
            var rank = d.Ranking(); float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 310, H * .5f - h * .5f, 620, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 12, box.width, 56), d.Result, mid, new Color(1f, .85f, .45f));
            for (int i = 0; i < rank.Count; i++)
            {
                var p = rank[i]; int s = p.Slot;
                Shadowed(new Rect(box.x + 60, box.y + 78 + i * 34, 300, 30), (i + 1) + ".  " + p.DisplayName + (p.IsHuman ? "  (tú)" : ""), left, p.Color);
                Shadowed(new Rect(box.x + 370, box.y + 78 + i * 34, 220, 30), d.Scores[s] + " pts" + (d.Golds[s] > 0 ? "  ★" + d.Golds[s] : "") + (d.Booms[s] > 0 ? "  explotó " + d.Booms[s] : ""), left, Color.white);
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
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "Mete gallinas en TU corral: llévalas en brazos (E / clic) o espántalas hacia dentro. 90 segundos.", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "Dorada = 5 puntos · Cuidado: algunas son TRAMPA (ojos rojos) y hacen explotar tu corral. ¡Sácala a tiempo!", small, new Color(1f, .85f, .6f));
        Shadowed(new Rect(box.x, box.y + 68, box.width, 30), "WASD moverte · Espacio saltar · E o clic izq. coger/soltar · Shift o clic der. empujón", tiny, new Color(1, 1, 1, .85f));
    }
}
