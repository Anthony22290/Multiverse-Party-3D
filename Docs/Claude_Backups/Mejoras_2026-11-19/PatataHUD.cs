using UnityEngine;

/// <summary>Screen UI for Patata Caliente: round and players left, who holds the potato, how hot it is (never the exact fuse), dash, notices and results.</summary>
public sealed class PatataHUD : MonoBehaviour
{
    public PatataDirector Director;
    GUIStyle big, mid, small, tiny, left;
    static readonly Color Panel = new Color(.14f, .07f, .05f, .82f), Edge = new Color(1f, .7f, .35f, .9f), Hot = new Color(1f, .45f, .2f);

    void Styles()
    {
        if (big != null) return;
        GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
        big = S(MultiverseUI.DisplayFont, 96, TextAnchor.MiddleCenter); mid = S(MultiverseUI.DisplayFont, 34, TextAnchor.MiddleCenter);
        small = S(MultiverseUI.BodyFont, 19, TextAnchor.MiddleCenter); tiny = S(MultiverseUI.BodyFont, 15, TextAnchor.MiddleCenter); left = S(MultiverseUI.BodyFont, 17, TextAnchor.MiddleLeft);
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

        // Header.
        MultiverseUI.DrawPanel(new Rect(W * .5f - 200, 10, 400, 62), Panel, Edge);
        Shadowed(new Rect(W * .5f - 200, 12, 400, 30), "PATATA CALIENTE", small, new Color(1f, .8f, .45f));
        string sub = d.State == PatataDirector.Phase.Finished ? "¡fin!" : "ronda " + Mathf.Min(d.Round + 1, d.Rounds) + "/" + d.Rounds + "   ·   quedan " + d.AliveCount;
        Shadowed(new Rect(W * .5f - 200, 38, 400, 30), sub, tiny, Color.white);

        // Heat of the potato (grows with time; the real fuse stays secret).
        if (d.State == PatataDirector.Phase.Playing && d.HolderSlot >= 0)
        {
            var holder = d.Players[d.HolderSlot]; float heat = d.Heat;
            float pulse = .5f + .5f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 22f, heat));
            var box = new Rect(W * .5f - 200, 80, 400, 52);
            MultiverseUI.DrawPanel(box, Panel, Color.Lerp(Edge, Hot, heat * pulse));
            Shadowed(new Rect(box.x, box.y + 2, box.width, 24), "la patata la tiene: <b>" + holder.DisplayName + (holder.IsHuman ? " (¡TÚ!)" : "") + "</b>", tiny, holder.Color);
            MultiverseUI.DrawMeter(new Rect(box.x + 20, box.y + 30, box.width - 40, 12), heat, new Color(0, 0, 0, .5f), Color.Lerp(new Color(1f, .85f, .4f), Hot, heat));
        }

        // You hold it!
        if (me != null && me.Alive && d.HolderSlot == me.Slot && d.State == PatataDirector.Phase.Playing)
        {
            float pulse = .5f + .5f * Mathf.Sin(Time.time * 10f);
            Shadowed(new Rect(0, H * .66f, W, 60), "¡TIENES LA PATATA! ¡PÁSALA!", mid, Color.Lerp(new Color(1f, .85f, .4f), Hot, pulse));
        }

        // Dash.
        if (me != null && me.Alive && d.CanMove)
        {
            var r = new Rect(W * .5f - 110, H - 56, 220, 38);
            MultiverseUI.DrawPanel(r, Panel, Edge);
            MultiverseUI.DrawMeter(new Rect(r.x + 12, r.y + 25, r.width - 24, 7), me.DashReady, new Color(0, 0, 0, .5f), me.DashReady >= 1f ? new Color(.5f, 1f, .5f) : new Color(.8f, .8f, .8f));
            Shadowed(new Rect(r.x, r.y + 2, r.width, 22), me.DashReady >= 1f ? "SHIFT · ACELERÓN listo" : "acelerón…", tiny, Color.white);
        }

        // Players.
        var list = d.Ranking();
        MultiverseUI.DrawPanel(new Rect(W - 252, 84, 234, 18 + list.Count * 30), Panel, Edge);
        float y = 92;
        foreach (var p in list)
        {
            string state = !p.Alive ? "<color=#ff8866>fuera</color>" : d.HolderSlot == p.Slot ? "<color=#ffcc55>PATATA</color>" : "<color=#9be59b>vivo</color>";
            Shadowed(new Rect(W - 240, y, 150, 26), (p.IsHuman ? "▶ " : "") + p.DisplayName, left, p.Alive ? p.Color : new Color(p.Color.r, p.Color.g, p.Color.b, .45f));
            Shadowed(new Rect(W - 96, y, 86, 26), state, left, Color.white);
            y += 30;
        }
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 90 + i * 26, 460, 24), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));

        if (d.State == PatataDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == PatataDirector.Phase.Countdown) { int n = Mathf.CeilToInt(d.Countdown); Shadowed(new Rect(0, H * .26f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(1f, .8f, .45f)); Help(W, H); }
        if (!string.IsNullOrEmpty(d.BigText) && d.State != PatataDirector.Phase.Finished) Shadowed(new Rect(0, H * .3f - 50, W, 100), d.BigText, mid, d.BigColor);
        if (me != null && !me.Alive && d.State != PatataDirector.Phase.Finished && string.IsNullOrEmpty(d.BigText)) Shadowed(new Rect(0, H - 70, W, 40), "Estás fuera · mira quién aguanta más", small, new Color(1, 1, 1, .85f));
        if (!string.IsNullOrEmpty(d.Notice) && d.State != PatataDirector.Phase.Finished && (me == null || me.Alive))
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 320, H - 120, 640, 46), new Color(.3f, .12f, .06f, .9f), Edge);
            Shadowed(new Rect(W * .5f - 320, H - 120, 640, 46), d.Notice, small, new Color(1f, .95f, .85f));
        }

        if (d.State == PatataDirector.Phase.Finished)
        {
            var rank = d.Ranking(); float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 310, H * .5f - h * .5f, 620, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 12, box.width, 56), d.Result, mid, new Color(1f, .8f, .45f));
            for (int i = 0; i < rank.Count; i++)
            {
                var p = rank[i]; int outIdx = d.Out.IndexOf(p.Slot);
                Shadowed(new Rect(box.x + 60, box.y + 78 + i * 34, 330, 30), (i + 1) + ".  " + p.DisplayName + (p.IsHuman ? "  (tú)" : ""), left, p.Color);
                Shadowed(new Rect(box.x + 400, box.y + 78 + i * 34, 180, 30), i == 0 ? "¡superviviente!" : outIdx >= 0 ? "explotó en la ronda " + (outIdx + 1) : "", left, Color.white);
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
        var box = new Rect(W * .5f - 390, H * .5f - 10, 780, 100);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "Quien tiene la patata la pasa tocando a otro. Cuando explota, ¡fuera! El último en pie gana.", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "La mecha es secreta (10–20 s) · al recibirla te quedas quieto un instante · no se puede devolver al momento", small, new Color(1f, .85f, .6f));
        Shadowed(new Rect(box.x, box.y + 66, box.width, 30), "WASD moverte · Espacio saltar · Shift o clic izq. acelerón · camas elásticas para escapar", tiny, new Color(1, 1, 1, .85f));
    }
}
