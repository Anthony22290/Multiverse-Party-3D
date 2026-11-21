using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Pico, Pala y Tijera: tool bar (1/2/3), depth race ladder, clock, countdown and results.</summary>
public sealed class MinaHUD : MonoBehaviour
{
    public MinaDirector Director;
    GUIStyle big, mid, small, tiny, left;
    static readonly Color Panel = new Color(.1f, .07f, .05f, .8f), Edge = new Color(1f, .75f, .3f, .9f);

    void Styles()
    {
        if (big != null) return;
        GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
        big = S(MultiverseUI.DisplayFont, 96, TextAnchor.MiddleCenter); mid = S(MultiverseUI.DisplayFont, 30, TextAnchor.MiddleCenter);
        small = S(MultiverseUI.BodyFont, 19, TextAnchor.MiddleCenter); tiny = S(MultiverseUI.BodyFont, 15, TextAnchor.MiddleCenter); left = S(MultiverseUI.BodyFont, 17, TextAnchor.MiddleLeft); left.wordWrap = false; left.clipping = TextClipping.Clip;   // 2026-11-19: long lines are cut instead of wrapping over the next one
    }
    static void Shadowed(Rect r, string text, GUIStyle s, Color c)
    {
        var old = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, .75f * c.a); GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
        s.normal.textColor = c; GUI.Label(r, text, s); s.normal.textColor = old;
    }

    void OnGUI()
    {
        var d = Director; if (d == null) return;
        Styles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        float W = Screen.width / scale, H = Screen.height / scale; var me = d.Human;

        // Title and clock.
        MultiverseUI.DrawPanel(new Rect(W * .5f - 190, 10, 380, 62), Panel, Edge);
        Shadowed(new Rect(W * .5f - 190, 12, 380, 30), "PICO, PALA Y TIJERA", small, new Color(1f, .82f, .4f));
        string clock = d.State == MinaDirector.Phase.Running || d.State == MinaDirector.Phase.Finished ? Mathf.CeilToInt(d.Remaining) + " s" : "--";
        Shadowed(new Rect(W * .5f - 190, 38, 380, 30), "quedan " + clock + "   ·   tesoro a " + MinaDirector.Depth + " bloques", tiny, d.Remaining <= 10f ? new Color(1f, .5f, .4f) : Color.white);

        // Tool bar.
        if (me != null)
        {
            float bx = W * .5f - 3 * 120 * .5f, by = H - 108;
            for (int i = 0; i < 3; i++)
            {
                bool sel = (int)me.Tool == i;
                var r = new Rect(bx + i * 120 + 6, by + (sel ? -10 : 0), 108, 92);
                MultiverseUI.DrawPanel(r, sel ? new Color(MinaKit.ToolColors[i].r * .55f, MinaKit.ToolColors[i].g * .55f, MinaKit.ToolColors[i].b * .55f, .95f) : Panel, sel ? Color.white : new Color(1, 1, 1, .35f));
                Shadowed(new Rect(r.x, r.y + 6, r.width, 40), (i + 1).ToString(), mid, sel ? Color.white : new Color(1, 1, 1, .6f));
                Shadowed(new Rect(r.x, r.y + 48, r.width, 30), MinaKit.ToolNames[i], small, MinaKit.ToolColors[i]);
            }
            string what = i18n(me);
            Shadowed(new Rect(0, by - 46, W, 30), what, small, me.Stunned ? new Color(1f, .45f, .4f) : new Color(1, 1, 1, .9f));
        }

        // Depth ladder (race).
        var list = d.Miners.OrderByDescending(m => m.Depth).ToList();
        MultiverseUI.DrawPanel(new Rect(W - 262, 84, 244, 22 + list.Count * 34), Panel, Edge);
        float y = 94;
        foreach (var m in list)
        {
            var bar = new Rect(W - 250, y + 20, 220, 8);
            MultiverseUI.DrawMeter(bar, m.Depth / (float)MinaDirector.Depth, new Color(.2f, .15f, .1f, .9f), m.Color);
            Shadowed(new Rect(W - 250, y - 2, 155, 22), (m.IsHuman ? "▶ " : "") + m.DisplayName, left, m.Color);
            Shadowed(new Rect(W - 90, y - 2, 70, 22), m.Finished ? "¡TESORO!" : m.Depth + " m", left, Color.white);
            y += 34;
        }

        if (d.State == MinaDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == MinaDirector.Phase.Countdown)
        {
            int n = Mathf.CeilToInt(d.Countdown);
            Shadowed(new Rect(0, H * .28f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(1f, .82f, .3f));
            Help(W, H);
        }
        if (d.State == MinaDirector.Phase.Running && d.Elapsed < 1f) Shadowed(new Rect(0, H * .28f - 60, W, 120), "¡A CAVAR!", big, new Color(.5f, 1f, .4f));
        if (d.State == MinaDirector.Phase.Running && d.Elapsed < 7f) Help(W, H);
        if (!string.IsNullOrEmpty(d.Notice) && d.State != MinaDirector.Phase.Finished)
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 300, 84, 600, 46), new Color(.3f, .16f, .04f, .88f), Edge);
            Shadowed(new Rect(W * .5f - 300, 84, 600, 46), d.Notice, small, new Color(1f, .92f, .6f));
        }

        if (d.State == MinaDirector.Phase.Finished)
        {
            var rank = d.Ranking(); float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 310, H * .5f - h * .5f, 620, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 12, box.width, 56), d.Result, mid, new Color(1f, .82f, .3f));
            for (int i = 0; i < rank.Count; i++)
            {
                var m = rank[i];
                Shadowed(new Rect(box.x + 50, box.y + 78 + i * 34, 330, 30), (i + 1) + ".  " + m.DisplayName + (m.IsHuman ? "  (tú)" : ""), left, m.Color);
                Shadowed(new Rect(box.x + 370, box.y + 78 + i * 34, 220, 30), (m.Finished ? "tesoro en " + m.FinishedAt.ToString("0.0") + " s" : m.Depth + " m") + " · fallos " + m.Mistakes, left, Color.white);
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

    static string i18n(MinaMiner me)
    {
        if (me.Finished) return "¡Llegaste al tesoro!";
        if (me.Stunned) return "¡Herramienta equivocada! Mareado un momento…";
        return me.Next == MinaKit.Block.PiedraDura ? "Piedra dura: dos golpes de pico" : me.Next == MinaKit.Block.Oro ? "¡Oro! Sirve cualquier herramienta" : "";
    }

    void Help(float W, float H)
    {
        var box = new Rect(W * .5f - 360, H * .5f - 10, 720, 100);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "<color=#d99a4d>1 PALA</color> → tierra   ·   <color=#b3c7e6>2 PICO</color> → piedra   ·   <color=#73f266>3 TIJERA</color> → hojas", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "Clic izquierdo (o Espacio) para cavar el bloque bajo tus pies", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 66, box.width, 30), "Herramienta equivocada = mareo cortito · ¡el primero en llegar al tesoro gana!", tiny, new Color(1, 1, 1, .85f));
    }
}
