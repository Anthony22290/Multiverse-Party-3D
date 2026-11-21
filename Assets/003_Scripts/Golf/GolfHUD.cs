using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Minigolf Loco: hole and par, your strokes (x/15), power bar, the one-minute countdown and the score card.</summary>
public sealed class GolfHUD : MonoBehaviour
{
    public GolfDirector Director;
    GUIStyle big, mid, small, tiny, left, cell;
    static readonly Color Panel = new Color(.06f, .14f, .08f, .82f), Edge = new Color(.95f, .95f, .6f, .9f);

    void Styles()
    {
        if (big != null) return;
        GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
        big = S(MultiverseUI.DisplayFont, 72, TextAnchor.MiddleCenter); mid = S(MultiverseUI.DisplayFont, 30, TextAnchor.MiddleCenter);
        small = S(MultiverseUI.BodyFont, 19, TextAnchor.MiddleCenter); tiny = S(MultiverseUI.BodyFont, 15, TextAnchor.MiddleCenter); left = S(MultiverseUI.BodyFont, 17, TextAnchor.MiddleLeft); left.wordWrap = false; left.clipping = TextClipping.Clip;   // 2026-11-19: long lines are cut instead of wrapping over the next one
        cell = S(MultiverseUI.BodyFont, 15, TextAnchor.MiddleCenter); cell.wordWrap = false;
    }
    static void Shadowed(Rect r, string text, GUIStyle s, Color c)
    {
        var old = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, .75f * c.a); GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, s);
        s.normal.textColor = c; GUI.Label(r, text, s); s.normal.textColor = old;
    }

    void OnGUI()
    {
        var d = Director; if (d == null) return;
        Styles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        float W = Screen.width / scale, H = Screen.height / scale; var me = d.Human; var h = d.CurrentHole;

        if (h != null)
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 210, 10, 420, 62), Panel, Edge);
            Shadowed(new Rect(W * .5f - 210, 12, 420, 30), "HOYO " + (d.HoleIndex + 1) + "/" + GolfDirector.Holes + " · " + h.Name.ToUpperInvariant() + " · PAR " + h.Par, small, new Color(1f, .95f, .6f));
            string sub = d.CountdownLeft >= 0f ? "<color=#ff8866>¡QUEDA " + Mathf.CeilToInt(d.CountdownLeft) + " s!</color>" : "Nadie ha embocado todavía";
            Shadowed(new Rect(W * .5f - 210, 38, 420, 30), sub, tiny, Color.white);
        }
        if (d.CountdownLeft >= 0f && d.State == GolfDirector.Phase.Playing)
            Shadowed(new Rect(W * .5f - 100, 76, 200, 60), Mathf.CeilToInt(d.CountdownLeft).ToString(), big, d.CountdownLeft < 10f ? new Color(1f, .4f, .3f) : new Color(1f, .85f, .4f));

        // Your strokes and power.
        if (me != null && h != null && d.State == GolfDirector.Phase.Playing)
        {
            MultiverseUI.DrawPanel(new Rect(18, H - 118, 280, 100), Panel, Edge);
            string st = me.State == GolfPlayer.Phase.Holed ? "¡EMBOCADA! " + me.Strokes + " golpes" : me.State == GolfPlayer.Phase.Out ? "15 golpes · cuenta 17" : "GOLPES " + me.Strokes + " / 15";
            Shadowed(new Rect(30, H - 114, 260, 30), st, small, me.Strokes >= 12 && !me.Done ? new Color(1f, .5f, .4f) : Color.white);
            MultiverseUI.DrawMeter(new Rect(30, H - 76, 256, 18), me.State == GolfPlayer.Phase.Charging ? me.Power : 0f, new Color(.1f, .15f, .1f, .9f), Color.Lerp(new Color(.4f, 1f, .4f), new Color(1f, .3f, .2f), me.Power));
            Shadowed(new Rect(30, H - 54, 260, 26), me.State == GolfPlayer.Phase.Ready ? "Apunta y mantén clic / Espacio" : me.State == GolfPlayer.Phase.Charging ? "¡Suelta para golpear!" : me.State == GolfPlayer.Phase.Rolling ? "Rodando…" : "", tiny, new Color(1, 1, 1, .85f));
        }

        // Live strokes of everybody on this hole.
        if (d.State == GolfDirector.Phase.Playing || d.State == GolfDirector.Phase.Intro)
        {
            MultiverseUI.DrawPanel(new Rect(W - 262, 84, 244, 16 + d.Players.Count * 28), Panel, Edge);
            float y = 92;
            foreach (var p in d.Players.OrderBy(p => d.Total(d.Players.IndexOf(p))))
            {
                int s = d.Players.IndexOf(p);
                string now = p.State == GolfPlayer.Phase.Holed ? "✓ " + p.Strokes : p.State == GolfPlayer.Phase.Out ? "✕" : p.Strokes.ToString();
                Shadowed(new Rect(W - 250, y, 150, 24), (p.IsHuman ? "▶ " : "") + p.DisplayName, left, p.Color);
                Shadowed(new Rect(W - 110, y, 90, 24), now + "  (" + d.Total(s) + ")", left, Color.white);
                y += 28;
            }
        }
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 84 + i * 24, 440, 22), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));
        if (!string.IsNullOrEmpty(d.Notice)) { MultiverseUI.DrawPanel(new Rect(W * .5f - 300, 140, 600, 44), new Color(.15f, .25f, .1f, .9f), Edge); Shadowed(new Rect(W * .5f - 300, 140, 600, 44), d.Notice, small, Color.white); }
        if (d.State == GolfDirector.Phase.Waiting) Shadowed(new Rect(0, H * .4f, W, 60), d.WaitingText, mid, Color.white);
        if (d.State == GolfDirector.Phase.Intro && h != null)
        {
            Shadowed(new Rect(0, H * .3f - 50, W, 90), "HOYO " + (d.HoleIndex + 1), big, new Color(1f, .95f, .6f));
            Shadowed(new Rect(0, H * .3f + 40, W, 40), h.Name + " · par " + h.Par, mid, Color.white);
            if (d.HoleIndex == 0) Help(W, H);
        }

        bool card = d.State == GolfDirector.Phase.HoleEnd || d.State == GolfDirector.Phase.Finished || (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tabKey.isPressed);
        if (card) Card(W, H);
        if (d.State == GolfDirector.Phase.Finished) Shadowed(new Rect(0, 90, W, 60), d.Result, mid, new Color(1f, .9f, .4f));
        if (d.State == GolfDirector.Phase.Finished && !MultiverseParty.InMatch) Shadowed(new Rect(0, H - 50, W, 30), "R: jugar otra vez", tiny, new Color(1, 1, 1, .8f));

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

    void Card(float W, float H)
    {
        var d = Director; int n = d.Players.Count;
        float cw = 46, nameW = 150, w = nameW + cw * (GolfDirector.Holes + 1) + 30, hgt = 70 + (n + 1) * 30;
        var box = new Rect(W * .5f - w * .5f, H * .5f - hgt * .5f + 30, w, hgt);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 6, box.width, 30), "TARJETA · gana el que menos golpes sume", small, new Color(1f, .95f, .6f));
        float y = box.y + 40, x0 = box.x + 15 + nameW;
        Shadowed(new Rect(box.x + 15, y, nameW, 26), "par", left, new Color(1, 1, 1, .6f));
        for (int h = 0; h < GolfDirector.Holes; h++) Shadowed(new Rect(x0 + h * cw, y, cw, 26), (h + 1) + "<size=11>·" + GolfCourse.Layouts[h].par + "</size>", cell, new Color(1, 1, 1, .7f));
        Shadowed(new Rect(x0 + GolfDirector.Holes * cw, y, cw, 26), "TOT", cell, new Color(1, 1, 1, .7f));
        foreach (var p in d.Ranking())
        {
            y += 30; int s = d.Players.IndexOf(p);
            Shadowed(new Rect(box.x + 15, y, nameW, 26), (p.IsHuman ? "▶ " : "") + p.DisplayName, left, p.Color);
            for (int h = 0; h < GolfDirector.Holes; h++)
            {
                int v = d.Card[s, h]; if (v < 0) continue;
                int diff = v - GolfCourse.Layouts[h].par;
                Shadowed(new Rect(x0 + h * cw, y, cw, 26), v.ToString(), cell, diff < 0 ? new Color(.5f, 1f, .5f) : diff == 0 ? Color.white : diff >= 4 ? new Color(1f, .45f, .4f) : new Color(1f, .8f, .5f));
            }
            Shadowed(new Rect(x0 + GolfDirector.Holes * cw, y, cw, 26), d.Total(s).ToString(), cell, new Color(1f, .95f, .6f));
        }
    }

    void Help(float W, float H)
    {
        var box = new Rect(W * .5f - 380, H * .62f, 760, 100);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "Apunta con el ratón o A/D (Shift = fino) · mantén clic o Espacio y suelta para golpear", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "Cuando alguien emboca, queda 1 minuto · máximo 15 golpes (cuentan 17)", small, new Color(1f, .9f, .6f));
        Shadowed(new Rect(box.x, box.y + 66, box.width, 30), "Agua = +1 golpe · Tab: tarjeta · rueda: zoom · clic der.: altura de cámara", tiny, new Color(1, 1, 1, .85f));
    }
}
