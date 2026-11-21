using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Propulsores: score race to 30, clock, countdown, notices and results.</summary>
public sealed class FlapHUD : MonoBehaviour
{
    public FlapDirector Director;
    GUIStyle big, huge, mid, small, tiny, left;
    static readonly Color Panel = new Color(.08f, .12f, .2f, .78f), Edge = new Color(1f, .85f, .35f, .9f);

    void Styles()
    {
        if (big != null) return;
        GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
        big = S(MultiverseUI.DisplayFont, 96, TextAnchor.MiddleCenter); huge = S(MultiverseUI.DisplayFont, 64, TextAnchor.MiddleCenter); mid = S(MultiverseUI.DisplayFont, 30, TextAnchor.MiddleCenter);
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

        MultiverseUI.DrawPanel(new Rect(W * .5f - 190, 10, 380, 62), Panel, Edge);
        Shadowed(new Rect(W * .5f - 190, 12, 380, 30), "PROPULSORES · meta " + FlapDirector.Goal + " puntos", small, new Color(1f, .88f, .4f));
        string clock = d.State == FlapDirector.Phase.Running || d.State == FlapDirector.Phase.Finished ? Mathf.CeilToInt(d.Remaining) + " s" : "--";
        Shadowed(new Rect(W * .5f - 190, 38, 380, 30), "quedan " + clock, tiny, d.Remaining <= 10f ? new Color(1f, .5f, .4f) : Color.white);

        if (me != null)
        {
            MultiverseUI.DrawPanel(new Rect(18, H - 110, 220, 92), Panel, Edge);
            Shadowed(new Rect(18, H - 108, 220, 60), me.Score + "<size=26>/" + FlapDirector.Goal + "</size>", huge, Color.white);
            string lives = me.Dead ? "ELIMINADO" : me.Lives > 1 ? "♥ ♥  · 1 vida extra" : "♥  · ¡última vida!";
            Shadowed(new Rect(18, H - 52, 220, 30), lives, small, me.Dead || me.Lives == 1 ? new Color(1f, .45f, .4f) : new Color(1f, .88f, .4f));
        }

        var list = d.Players.OrderByDescending(p => p.Score).ToList();
        MultiverseUI.DrawPanel(new Rect(W - 262, 84, 244, 22 + list.Count * 34), Panel, Edge);
        float y = 94;
        foreach (var p in list)
        {
            MultiverseUI.DrawMeter(new Rect(W - 250, y + 20, 220, 8), p.Score / (float)FlapDirector.Goal, new Color(.15f, .18f, .25f, .9f), p.Color);
            Shadowed(new Rect(W - 250, y - 2, 170, 22), (p.IsHuman ? "▶ " : "") + p.DisplayName, left, p.Color);
            Shadowed(new Rect(W - 80, y - 2, 70, 22), p.Dead ? p.Score + " ✕" : p.Score + (p.Lives > 1 ? " ♥♥" : " ♥"), left, p.Dead ? new Color(.7f, .7f, .7f) : Color.white);
            y += 34;
        }
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 90 + i * 26, 420, 24), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));

        if (d.State == FlapDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == FlapDirector.Phase.Countdown) { int n = Mathf.CeilToInt(d.Countdown); Shadowed(new Rect(0, H * .3f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(1f, .85f, .3f)); Help(W, H); }
        if (d.State == FlapDirector.Phase.Running && d.Elapsed < 1f) Shadowed(new Rect(0, H * .3f - 60, W, 120), "¡A VOLAR!", big, new Color(.5f, 1f, .4f));
        if (d.State == FlapDirector.Phase.Running && d.Elapsed < 5f) Help(W, H);
        if (!string.IsNullOrEmpty(d.Notice) && d.State != FlapDirector.Phase.Finished)
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 300, 84, 600, 46), new Color(.2f, .15f, .05f, .88f), Edge);
            Shadowed(new Rect(W * .5f - 300, 84, 600, 46), d.Notice, small, new Color(1f, .92f, .6f));
        }

        if (d.State == FlapDirector.Phase.Finished)
        {
            var rank = d.Ranking(); float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 300, H * .5f - h * .5f, 600, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 12, box.width, 56), d.Result, mid, new Color(1f, .85f, .3f));
            for (int i = 0; i < rank.Count; i++)
            {
                var p = rank[i];
                Shadowed(new Rect(box.x + 60, box.y + 78 + i * 34, 330, 30), (i + 1) + ".  " + p.DisplayName + (p.IsHuman ? "  (tú)" : ""), left, p.Color);
                Shadowed(new Rect(box.x + 380, box.y + 78 + i * 34, 200, 30), p.Score + " pts" + (p.Dead ? " · fuera" : " · en el aire"), left, Color.white);
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
        var box = new Rect(W * .5f - 340, H * .55f, 680, 76);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "Espacio / clic / W: ¡propulsor! · pasa entre las torres", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 40, box.width, 30), "Cada torre +1 · tienes 1 vida extra: al segundo choque quedas fuera · el primero en llegar a " + FlapDirector.Goal + " gana", tiny, new Color(1, 1, 1, .85f));
    }
}
