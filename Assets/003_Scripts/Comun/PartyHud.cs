using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared OnGUI pieces for the 2026-11-19 minigames (1280×720 virtual screen, never narrower or shorter): styles,
/// shadowed labels, header with clock, player list, feed, countdown with help, results and pause menu.
/// </summary>
public static class PartyHud
{
    public static GUIStyle Big, Mid, Small, Tiny, Left;
    public static float W, H;
    public static Color Panel = new Color(.1f, .08f, .16f, .86f), Edge = new Color(1f, .78f, .35f, .9f);

    public static void Begin(Color panel, Color edge)
    {
        Panel = panel; Edge = edge;
        if (Big == null)
        {
            GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
            Big = S(MultiverseUI.DisplayFont, 96, TextAnchor.MiddleCenter); Mid = S(MultiverseUI.DisplayFont, 34, TextAnchor.MiddleCenter);
            Small = S(MultiverseUI.BodyFont, 19, TextAnchor.MiddleCenter); Tiny = S(MultiverseUI.BodyFont, 15, TextAnchor.MiddleCenter);
            Left = S(MultiverseUI.BodyFont, 17, TextAnchor.MiddleLeft); Left.wordWrap = false; Left.clipping = TextClipping.Clip;
        }
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        W = Screen.width / scale; H = Screen.height / scale;
    }

    public static void Label(Rect r, string text, GUIStyle s, Color c)
    {
        var old = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, .75f * c.a); GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
        s.normal.textColor = c; GUI.Label(r, text, s); s.normal.textColor = old;
    }

    public static void Header(string title, string sub, Color titleColor, bool urgent = false)
    {
        MultiverseUI.DrawPanel(new Rect(W * .5f - 200, 10, 400, 64), Panel, Edge);
        Label(new Rect(W * .5f - 200, 12, 400, 28), title, Small, titleColor);
        Color c = urgent ? Color.Lerp(Color.white, new Color(1f, .4f, .3f), .5f + .5f * Mathf.Sin(Time.time * 10f)) : Color.white;
        Label(new Rect(W * .5f - 200, 38, 400, 32), sub, Small, c);
    }

    public static string Clock(float seconds) { int s = Mathf.CeilToInt(Mathf.Max(0f, seconds)); return (s / 60) + ":" + (s % 60).ToString("00"); }

    /// <summary>Right-hand list: (name, colour, value text, dimmed, is you).</summary>
    public static void Players(List<(string name, Color color, string value, bool dim, bool you)> rows)
    {
        MultiverseUI.DrawPanel(new Rect(W - 272, 84, 254, 22 + rows.Count * 32), Panel, Edge);
        float y = 94;
        foreach (var r in rows)
        {
            var c = r.dim ? new Color(r.color.r, r.color.g, r.color.b, .45f) : r.color;
            Label(new Rect(W - 260, y, 150, 28), (r.you ? "▶ " : "") + r.name, Left, c);
            Label(new Rect(W - 104, y, 84, 28), r.value, Left, Color.white);
            y += 32;
        }
    }

    public static void Feed(List<string> feed) { for (int i = 0; i < feed.Count; i++) Label(new Rect(18, 92 + i * 26, 540, 24), feed[i], Left, new Color(1f, 1f, 1f, 1f - i * .18f)); }

    public static void Countdown(float countdown, Color color, string line1, string line2, string line3)
    {
        int n = Mathf.CeilToInt(countdown);
        Label(new Rect(0, H * .22f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), Big, color);
        var box = new Rect(W * .5f - 410, H * .5f - 10, 820, 104);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Label(new Rect(box.x, box.y + 8, box.width, 30), line1, Small, Color.white);
        Label(new Rect(box.x, box.y + 38, box.width, 30), line2, Small, new Color(1f, .85f, .6f));
        Label(new Rect(box.x, box.y + 68, box.width, 30), line3, Tiny, new Color(1, 1, 1, .85f));
    }

    public static void Waiting(string text) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Label(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), text, Mid, Color.white); }
    public static void BigText(string text, Color c) { if (!string.IsNullOrEmpty(text)) Label(new Rect(0, H * .3f - 50, W, 100), text, Mid, c); }
    public static void Spectating(string watching, string outText)
    {
        Label(new Rect(300, H - 76, W - 600, 30), watching != null ? outText + " · mirando a <b>" + watching + "</b>" : outText + " · vista general", Small, new Color(1, 1, 1, .9f));
        Label(new Rect(300, H - 46, W - 600, 26), PartySpectate.Hint, Tiny, new Color(1, 1, 1, .7f));
    }

    /// <summary>Final card: rows of (name, colour, detail, you).</summary>
    public static void Results(string result, Color color, List<(string name, Color color, string detail, bool you)> rows)
    {
        float h = 150 + rows.Count * 34;
        var box = new Rect(W * .5f - 310, H * .5f - h * .5f, 620, h);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Label(new Rect(box.x, box.y + 12, box.width, 56), result, Mid, color);
        for (int i = 0; i < rows.Count; i++)
        {
            Label(new Rect(box.x + 60, box.y + 78 + i * 34, 300, 30), (i + 1) + ".  " + rows[i].name + (rows[i].you ? "  (tú)" : ""), Left, rows[i].color);
            Label(new Rect(box.x + 370, box.y + 78 + i * 34, 230, 30), rows[i].detail, Left, Color.white);
        }
        if (!MultiverseParty.InMatch) Label(new Rect(box.x, box.yMax - 44, box.width, 30), "R: jugar otra vez", Tiny, new Color(1, 1, 1, .8f));
    }

    public static void Pause(Action resume, Action quit)
    {
        var box = new Rect(W * .5f - 220, H * .5f - 130, 440, 260);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Label(new Rect(box.x, box.y + 16, box.width, 50), "PAUSA", Mid, Color.white);
        var b1 = new Rect(box.x + 70, box.y + 90, 300, 52); var b2 = new Rect(box.x + 70, box.y + 160, 300, 52);
        MultiverseUI.DrawPanel(b1, new Color(.35f, .55f, .2f, .95f), Edge); MultiverseUI.DrawPanel(b2, new Color(.55f, .18f, .14f, .95f), Edge);
        if (GUI.Button(b1, "Continuar", Small)) resume();
        if (GUI.Button(b2, MultiverseParty.InMatch ? "Salir de la fiesta" : "Salir", Small)) quit();
    }
}
