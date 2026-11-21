using UnityEngine;

/// <summary>Screen UI for ¡Atrápalo!: throw counter, points and lives, your reaction, notices, results.</summary>
public sealed class AtrapaHUD : MonoBehaviour
{
    public AtrapaDirector Director;
    GUIStyle big, mid, small, tiny, left;
    static readonly Color Panel = new Color(.12f, .06f, .2f, .86f), Edge = new Color(1f, .78f, .3f, .9f);

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
        var d = Director; if (d == null) return;
        Styles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        float W = Screen.width / scale, H = Screen.height / scale; var me = d.Human;

        MultiverseUI.DrawPanel(new Rect(W * .5f - 200, 10, 400, 64), Panel, Edge);
        Shadowed(new Rect(W * .5f - 200, 12, 400, 28), "¡ATRÁPALO! · primero a " + AtrapaDirector.Goal + " puntos", small, new Color(1f, .82f, .4f));
        Shadowed(new Rect(W * .5f - 200, 40, 400, 30), d.State == AtrapaDirector.Phase.Finished ? "¡fin!" : "lanzamiento " + Mathf.Max(1, d.ThrowIndex + 1) + " de " + AtrapaDirector.MaxThrows, small, Color.white);

        var list = d.Ranking();
        MultiverseUI.DrawPanel(new Rect(W - 282, 84, 264, 22 + list.Count * 32), Panel, Edge);
        float y = 94;
        foreach (var p in list)
        {
            Shadowed(new Rect(W - 270, y, 150, 28), (p.IsHuman ? "▶ " : "") + p.DisplayName, left, p.Out ? new Color(p.Color.r, p.Color.g, p.Color.b, .45f) : p.Color);
            Shadowed(new Rect(W - 120, y, 50, 28), "<color=#FF6677>" + (p.Out ? "fuera" : new string('♥', p.Lives)) + "</color>", left, Color.white);
            Shadowed(new Rect(W - 56, y, 40, 28), p.Score.ToString(), left, Color.white);
            y += 32;
        }
        for (int i = 0; i < d.Feed.Count; i++) Shadowed(new Rect(18, 92 + i * 26, 560, 24), d.Feed[i], left, new Color(1f, 1f, 1f, 1f - i * .18f));

        // Legend + your state.
        if (d.State == AtrapaDirector.Phase.Playing && me != null)
        {
            var r = new Rect(W * .5f - 270, H - 90, 540, 74);
            MultiverseUI.DrawPanel(r, Panel, Edge);
            Shadowed(new Rect(r.x, r.y + 6, r.width, 26), "<color=#9BE59B>fruta +1</color>   ·   <color=#FFD54A>piña dorada +2</color>   ·   <color=#FF8877>bota o bomba: ¡NO la cojas! (−1 ♥)</color>", small, Color.white);
            string mine = me.Out ? "Estás fuera · sigue mirando" : d.MyFalseStart ? "¡Muy pronto! Espera al siguiente" : d.MyReaction >= 0f ? "Pulsado en " + Mathf.RoundToInt(d.MyReaction * 1000f) + " ms" : "ESPACIO o CLIC cuando salga algo bueno";
            Shadowed(new Rect(r.x, r.y + 38, r.width, 26), mine, tiny, new Color(1, 1, 1, .9f));
            if (d.ThrowState == AtrapaDirector.Throw.Ready && d.Elapsed < d.LaunchAt) Shadowed(new Rect(0, H * .16f, W, 50), "preparados…", mid, new Color(1f, 1f, 1f, .55f + .3f * Mathf.Sin(Time.time * 6f)));
        }
        if (!string.IsNullOrEmpty(d.MyNote) && d.State == AtrapaDirector.Phase.Playing) Shadowed(new Rect(0, H * .62f, W, 40), d.MyNote, small, new Color(1f, .95f, .7f));

        if (d.State == AtrapaDirector.Phase.Waiting) { MultiverseUI.DrawPanel(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), Panel, Edge); Shadowed(new Rect(W * .5f - 260, H * .4f - 40, 520, 80), d.WaitingText, mid, Color.white); }
        if (d.State == AtrapaDirector.Phase.Countdown) { int n = Mathf.CeilToInt(d.Countdown); Shadowed(new Rect(0, H * .2f - 60, W, 120), n > 3 ? "¡PREPARADOS!" : n.ToString(), big, new Color(1f, .82f, .4f)); Help(W, H); }
        if (!string.IsNullOrEmpty(d.BigText) && d.State != AtrapaDirector.Phase.Finished) Shadowed(new Rect(0, H * .3f - 50, W, 100), d.BigText, mid, d.BigColor);

        if (d.State == AtrapaDirector.Phase.Finished)
        {
            var rank = d.Ranking(); float h = 150 + rank.Count * 34;
            var box = new Rect(W * .5f - 310, H * .5f - h * .5f, 620, h);
            MultiverseUI.DrawPanel(box, Panel, Edge);
            Shadowed(new Rect(box.x, box.y + 12, box.width, 56), d.Result, mid, new Color(1f, .82f, .4f));
            for (int i = 0; i < rank.Count; i++)
            {
                var p = rank[i];
                Shadowed(new Rect(box.x + 60, box.y + 78 + i * 34, 300, 30), (i + 1) + ".  " + p.DisplayName + (p.IsHuman ? "  (tú)" : ""), left, p.Color);
                Shadowed(new Rect(box.x + 380, box.y + 78 + i * 34, 220, 30), p.Score + " pts · " + (p.Out ? "sin vidas" : p.Lives + " ♥"), left, Color.white);
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
        var box = new Rect(W * .5f - 410, H * .5f - 10, 820, 104);
        MultiverseUI.DrawPanel(box, Panel, Edge);
        Shadowed(new Rect(box.x, box.y + 8, box.width, 30), "El cañón dispara cosas: el primero en pulsar ESPACIO (o clic) se las queda.", small, Color.white);
        Shadowed(new Rect(box.x, box.y + 38, box.width, 30), "Fruta +1 · Piña dorada +2 · Bota o bomba = −1 vida · Pulsar antes de tiempo = fuera de ese tiro", small, new Color(1f, .85f, .6f));
        Shadowed(new Rect(box.x, box.y + 68, box.width, 30), "3 vidas · gana el primero a 10 puntos (o el mejor tras 30 lanzamientos)", tiny, new Color(1, 1, 1, .85f));
    }
}
