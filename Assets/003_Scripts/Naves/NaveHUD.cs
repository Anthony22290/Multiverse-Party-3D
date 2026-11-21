using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Arma tu Nave: your five pieces, the race of ships, clock, radar, notices and results.</summary>
public sealed class NaveHUD : MonoBehaviour
{
    public NaveDirector Director;
    GUIStyle big, mid, small, tiny, left;
    static readonly Color Panel = new Color(.06f, .08f, .14f, .8f), Edge = new Color(.5f, .8f, 1f, .9f);

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

        MultiverseUI.DrawPanel(new Rect(W * .5f - 190, 10, 380, 62), Panel, Edge);
        Shadowed(new Rect(W * .5f - 190, 12, 380, 30), "ARMA TU NAVE", small, new Color(.6f, .9f, 1f));
        string clock = d.State == NaveDirector.Phase.Running || d.State == NaveDirector.Phase.Finished ? Mathf.FloorToInt(d.Remaining / 60f) + ":" + Mathf.FloorToInt(d.Remaining % 60f).ToString("00") : "--:--";
        string radar = d.Radar ? "   ·   <color=#7fe0ff>RADAR ACTIVO</color>" : d.State == NaveDirector.Phase.Running ? "   ·   radar en " + Mathf.CeilToInt(NaveDirector.RadarAt - d.Elapsed) + " s" : "";
        Shadowed(new Rect(W * .5f - 190, 38, 380, 30), clock + radar, tiny, d.Remaining <= 30f ? new Color(1f, .55f, .45f) : Color.white);

        // Your pieces.
        if (me != null)
        {
            var ship = d.Ships[me.Slot];
            float bx = W * .5f - NaveKit.Types * 118 * .5f, by = H - 100;
            MultiverseUI.DrawPanel(new Rect(bx - 10, by - 34, NaveKit.Types * 118 + 20, 124), Panel, Edge);
            Shadowed(new Rect(bx - 10, by - 32, NaveKit.Types * 118 + 20, 26), "TU NAVE  " + ship.Count + "/5" + (me.Carrying != null ? "   ·   llevas: " + NaveKit.Names[me.Carrying.Type] : ""), small, Color.white);
            for (int t = 0; t < NaveKit.Types; t++)
            {
                bool has = ship.Has[t], carrying = me.Carrying != null && me.Carrying.Type == t;
                var c = NaveKit.Colors[t];
                var r = new Rect(bx + t * 118 + 4, by, 110, 76);
                MultiverseUI.DrawPanel(r, has ? new Color(c.r * .5f, c.g * .5f, c.b * .5f, .95f) : new Color(.12f, .12f, .16f, .9f), carrying ? Color.white : has ? c : new Color(1, 1, 1, .25f));
                Shadowed(new Rect(r.x, r.y + 8, r.width, 30), NaveKit.Names[t], small, has ? Color.white : c);
                Shadowed(new Rect(r.x, r.y + 40, r.width, 28), has ? "✓ montada" : carrying ? "¡a la nave!" : "falta", tiny, has ? new Color(.7f, 1f, .7f) : new Color(1, 1, 1, .7f));
            }
            // Prompt for the closest piece.
            if (d.State == NaveDirector.Phase.Running && me.Carrying == null)
            {
                var near = d.NearestFreePiece(me.transform.position, NavePlayer.PickRange);
                if (near != null) Shadowed(new Rect(0, H * .62f, W, 40), "E · coger " + NaveKit.Names[near.Type] + (d.Needs(me.Slot, near.Type) ? "" : " (ya la tienes)"), mid, NaveKit.Colors[near.Type]);
            }
        }

        // Ships race.
        var list = d.Ranking();
        MultiverseUI.DrawPanel(new Rect(W - 252, 84, 234, 18 + list.Count * 30), Panel, Edge);
        float y = 92;
        foreach (var p in list)
        {
            var s = d.Ships[p.Slot];
            Shadowed(new Rect(W - 240, y, 136, 26), (p.IsHuman ? "▶ " : "") + p.DisplayName, left, p.Color);
            string dots = ""; for (int t = 0; t < NaveKit.Types; t++) dots += s.Has[t] ? "■" : "<color=#444455>■</color>";
            Shadowed(new Rect(W - 100, y, 90, 26), dots, left, new Color(.6f, .9f, 1f));
            y += 30;
        }
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 90 + i * 26, 460, 24), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));

        if (d.State == NaveDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == NaveDirector.Phase.Countdown) { int n = Mathf.CeilToInt(d.Countdown); Shadowed(new Rect(0, H * .28f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(.6f, .9f, 1f)); Help(W, H); }
        if (d.State == NaveDirector.Phase.Running && d.Elapsed < 1f) Shadowed(new Rect(0, H * .28f - 60, W, 120), "¡A BUSCAR!", big, new Color(.5f, 1f, .4f));
        if (d.State == NaveDirector.Phase.Running && d.Elapsed < 8f) Help(W, H);
        if (!string.IsNullOrEmpty(d.Notice) && d.State != NaveDirector.Phase.Finished)
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 320, 84, 640, 46), new Color(.1f, .2f, .35f, .9f), Edge);
            Shadowed(new Rect(W * .5f - 320, 84, 640, 46), d.Notice, small, new Color(.9f, .97f, 1f));
        }

        if (d.State == NaveDirector.Phase.Finished)
        {
            var rank = d.Ranking(); float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 310, H * .5f - h * .5f, 620, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 12, box.width, 56), d.Result, mid, new Color(.6f, .9f, 1f));
            for (int i = 0; i < rank.Count; i++)
            {
                var p = rank[i];
                Shadowed(new Rect(box.x + 60, box.y + 78 + i * 34, 330, 30), (i + 1) + ".  " + p.DisplayName + (p.IsHuman ? "  (tú)" : ""), left, p.Color);
                Shadowed(new Rect(box.x + 400, box.y + 78 + i * 34, 180, 30), d.Ships[p.Slot].Count + "/5 piezas", left, Color.white);
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
        var box = new Rect(W * .5f - 380, H * .5f - 20, 760, 100);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "Encuentra las 5 piezas de tu nave: están escondidas en casas, detrás de setos, carros…", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "E coger · llévala a tu plataforma · Q soltar · F / clic izq. empujar (¡suelta la pieza!)", small, new Color(.7f, .9f, 1f));
        Shadowed(new Rect(box.x, box.y + 66, box.width, 30), "WASD moverte · Espacio saltar · clic der. / Z C girar la cámara", tiny, new Color(1, 1, 1, .85f));
    }
}
