using System.Linq;
using UnityEngine;

/// <summary>Screen UI for Rodillos del Calabozo (immediate mode, scaled to a 1280×720 reference).</summary>
public sealed class RodillosHUD : MonoBehaviour
{
    public RodillosDirector Director;
    GUIStyle big, mid, small, tiny, left;
    float scale = 1f;
    static readonly Color Panel = new Color(.08f, .05f, .06f, .78f), Edge = new Color(1f, .55f, .2f, .9f);

    void Styles()
    {
        if (big != null) return;
        var display = MultiverseUI.DisplayFont; var body = MultiverseUI.BodyFont;
        GUIStyle S(Font f, int size, TextAnchor a) { var s = new GUIStyle(GUI.skin.label) { font = f, fontSize = size, alignment = a, fontStyle = FontStyle.Bold, wordWrap = true, richText = true }; s.normal.textColor = Color.white; return s; }
        big = S(display, 96, TextAnchor.MiddleCenter); mid = S(display, 30, TextAnchor.MiddleCenter); small = S(body, 19, TextAnchor.MiddleCenter);
        tiny = S(body, 15, TextAnchor.MiddleCenter); left = S(body, 17, TextAnchor.MiddleLeft); left.wordWrap = false; left.clipping = TextClipping.Clip;   // 2026-11-19: long lines are cut instead of wrapping over the next one
    }

    static void Shadowed(Rect r, string text, GUIStyle s, Color c)
    {
        var old = s.normal.textColor;
        s.normal.textColor = new Color(0, 0, 0, .75f * c.a); GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, s);
        s.normal.textColor = c; GUI.Label(r, text, s); s.normal.textColor = old;
    }

    void OnGUI()
    {
        if (Director == null) return;
        Styles();
        scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        float W = Screen.width / scale, H = Screen.height / scale;
        var d = Director; var me = d.Human;

        // Title and clock.
        MultiverseUI.DrawPanel(new Rect(W * .5f - 200, 10, 400, 62), Panel, Edge);
        Shadowed(new Rect(W * .5f - 200, 12, 400, 30), "RODILLOS DEL CALABOZO", small, new Color(1f, .75f, .35f));
        string clock = d.State == RodillosDirector.Phase.Running || d.State == RodillosDirector.Phase.Finished ? Mathf.FloorToInt(d.Elapsed / 60f) + ":" + Mathf.FloorToInt(d.Elapsed % 60f).ToString("00") : "--:--";
        Shadowed(new Rect(W * .5f - 200, 38, 400, 30), clock + "   ·   vivos " + d.AliveCount + "/" + d.Runners.Count + (d.Elapsed >= RodillosDirector.SuddenDeath ? "   ·   <color=#ff6655>MUERTE SÚBITA</color>" : ""), tiny, Color.white);

        // Your lives.
        if (me != null)
        {
            MultiverseUI.DrawPanel(new Rect(18, H - 96, 270, 78), Panel, me.Alive ? Edge : new Color(.5f, .5f, .5f, .8f));
            Shadowed(new Rect(32, H - 92, 240, 26), me.Alive ? "TUS VIDAS" : "ELIMINADO · puesto " + d.PlaceOf(me), left, Color.white);
            for (int i = 0; i < RodillosDirector.Lives; i++)
                MultiverseUI.DrawPanel(new Rect(34 + i * 52, H - 60, 40, 32), i < me.Lives ? new Color(1f, .27f, .32f) : new Color(.25f, .25f, .27f), new Color(1, 1, 1, .5f));
        }

        // Scoreboard.
        float y = 86; var list = d.Runners.OrderByDescending(r => r.Alive).ThenByDescending(r => r.Alive ? r.Lives : r.EliminatedAt).ToList();
        MultiverseUI.DrawPanel(new Rect(W - 250, y - 6, 232, 16 + list.Count * 30), Panel, Edge);
        foreach (var r in list)
        {
            string hearts = r.Alive ? new string('●', r.Lives) + "<color=#555555>" + new string('●', RodillosDirector.Lives - r.Lives) + "</color>" : "<color=#888888>fuera</color>";
            var c = r.Alive ? r.Color : new Color(r.Color.r * .5f, r.Color.g * .5f, r.Color.b * .5f);
            Shadowed(new Rect(W - 238, y, 134, 26), (r.IsHuman ? "▶ " : "") + r.DisplayName, left, c);
            Shadowed(new Rect(W - 100, y, 80, 26), hearts, left, new Color(1f, .35f, .4f));
            y += 30;
        }

        // Countdown / waiting.
        if (d.State == RodillosDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .5f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .5f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == RodillosDirector.Phase.Countdown)
        {
            int n = Mathf.CeilToInt(d.Countdown);
            Shadowed(new Rect(0, H * .3f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(1f, .8f, .3f));
            Help(W, H, 1f);
        }
        if (d.State == RodillosDirector.Phase.Running && d.Elapsed < 1f) Shadowed(new Rect(0, H * .3f - 60, W, 120), "¡YA!", big, new Color(.5f, 1f, .4f));
        if (d.State == RodillosDirector.Phase.Running && d.Elapsed < 9f) Help(W, H, Mathf.Clamp01(9f - d.Elapsed));

        // Teaching prompts while the player is still learning.
        if (d.State == RodillosDirector.Phase.Running && me != null && me.Alive && d.Elapsed < 45f)
        {
            RodillosBot.Approach(d, RodillosSpin.Low, me.transform.position, me.Velocity, out float tl, out _, out _);
            float th = 99f; if (d.Elapsed > RodillosDirector.HighArrives - .5f) RodillosBot.Approach(d, RodillosSpin.High, me.transform.position, me.Velocity, out th, out _, out _);
            if (tl < .8f && tl > -.1f && tl <= th) Shadowed(new Rect(0, H * .62f, W, 60), "▲ ¡SALTA! ▲", mid, new Color(1f, .65f, .2f));
            else if (th < .8f && th > -.1f) Shadowed(new Rect(0, H * .62f, W, 60), "▼ ¡AGÁCHATE! ▼", mid, new Color(.45f, .85f, 1f));
        }

        // Notices.
        if (!string.IsNullOrEmpty(d.Notice) && d.State != RodillosDirector.Phase.Finished)
        {
            MultiverseUI.DrawPanel(new Rect(W * .5f - 330, 84, 660, 50), new Color(.3f, .06f, .04f, .85f), Edge);
            Shadowed(new Rect(W * .5f - 330, 84, 660, 50), d.Notice, small, new Color(1f, .9f, .6f));
        }
        // Feed.
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 90 + i * 26, 420, 24), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));

        if (me != null && !me.Alive && d.State == RodillosDirector.Phase.Running)
        {
            var w = d.View != null ? d.View.Watching : null;
            Shadowed(new Rect(300, H - 76, W - 600, 30), w != null ? "Eliminado · mirando a <b>" + w.DisplayName + "</b>" : "Eliminado · vista general", small, new Color(1, 1, 1, .9f));
            Shadowed(new Rect(300, H - 46, W - 600, 26), PartySpectate.Hint, tiny, new Color(1, 1, 1, .7f));
        }

        // Results.
        if (d.State == RodillosDirector.Phase.Finished)
        {
            var rank = d.Ranking();
            float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 300, H * .5f - h * .5f, 600, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 14, box.width, 50), d.Result, mid, new Color(1f, .8f, .3f));
            for (int i = 0; i < rank.Count; i++)
            {
                var r = rank[i];
                Shadowed(new Rect(box.x + 60, box.y + 78 + i * 34, 360, 30), (i + 1) + ".  " + r.DisplayName + (r.IsHuman ? "  (tú)" : ""), left, r.Color);
                Shadowed(new Rect(box.x + 380, box.y + 78 + i * 34, 180, 30), r.Alive ? "¡en pie!" : "aguantó " + Mathf.FloorToInt(r.EliminatedAt) + " s", left, Color.white);
            }
            if (!MultiverseParty.InMatch) Shadowed(new Rect(box.x, box.yMax - 44, box.width, 30), "R: jugar otra vez", tiny, new Color(1, 1, 1, .8f));
        }

        if (d.Paused)
        {
            GUI.matrix = Matrix4x4.identity;
            MultiverseUI.DrawPanel(new Rect(0, 0, Screen.width, Screen.height), new Color(0, 0, 0, .55f), new Color(0, 0, 0, 0));
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            var box = new Rect(W * .5f - 220, H * .5f - 130, 440, 260);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 16, box.width, 50), "PAUSA", mid, Color.white);
            var b1 = new Rect(box.x + 70, box.y + 90, 300, 52); var b2 = new Rect(box.x + 70, box.y + 160, 300, 52);
            MultiverseUI.DrawPanel(b1, new Color(.35f, .55f, .2f, .95f), Edge); MultiverseUI.DrawPanel(b2, new Color(.55f, .18f, .14f, .95f), Edge);
            if (GUI.Button(b1, "Continuar", small)) d.Paused = false;
            if (GUI.Button(b2, MultiverseParty.InMatch ? "Salir de la fiesta" : "Salir", small)) d.Quit();
        }
    }

    void Help(float W, float H, float alpha)
    {
        var box = new Rect(W * .5f - 380, H - 132, 760, 104);
        MultiverseUI.DrawPanel(box, new Color(Panel.r, Panel.g, Panel.b, Panel.a * alpha), new Color(Edge.r, Edge.g, Edge.b, alpha));
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "<color=#ffa040>RODILLO DE FUEGO</color> (bajo): <b>SALTA</b> · Espacio / clic izq.", small, new Color(1, 1, 1, alpha));
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "<color=#70d8ff>RODILLO DE HIELO</color> (alto): <b>AGÁCHATE</b> · Shift / Ctrl / clic der.", small, new Color(1, 1, 1, alpha));
        Shadowed(new Rect(box.x, box.y + 68, box.width, 30), "En el aire, agacharte te hace caer rápido · 3 vidas · si caes a la lava quedas fuera", tiny, new Color(1, 1, 1, .85f * alpha));
    }
}
