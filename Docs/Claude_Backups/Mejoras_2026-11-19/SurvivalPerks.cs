using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Roguelike layer (2026-11-10): after every wave each survivor picks 1 of 3 random upgrades (they stack).
// The human chooses with 1/2/3 or a click; bots (and the human in automated tests) pick at random.
public static class SurvivalPerks
{
    public enum Kind { Fuerza, Armadura, Velocidad, Alcance, Vampiro, Regeneracion, Onda, Suerte, Explosivo, Espinas }
    public const int Count = 10;
    public static readonly string[] Names = { "FUERZA", "ARMADURA", "VELOCIDAD", "ALCANCE", "VAMPIRO", "REGENERACIÓN", "ONDA DE CHOQUE", "SUERTE", "CADÁVERES EXPLOSIVOS", "ESPINAS" };
    public static readonly string[] Info =
    {
        "+20 % de daño en cada golpe", "-15 % de daño recibido", "+10 % de velocidad al correr", "+20 % de alcance del arma",
        "Recuperas 4 de vida por zombi abatido", "Regeneras antes y el doble de rápido", "El tercer golpe del combo lanza una onda (25 de daño)",
        "+60 % de objetos soltados", "20 % de probabilidad de que un zombi explote al morir", "Quien te golpea recibe 12 de daño"
    };
    public static readonly Color[] Colors =
    {
        new Color(1, .45f, .3f), new Color(.6f, .75f, 1), new Color(.4f, 1, .9f), new Color(1, .85f, .35f), new Color(.85f, .2f, .35f),
        new Color(.4f, 1, .5f), new Color(.7f, .5f, 1), new Color(1, .95f, .4f), new Color(1, .6f, .15f), new Color(.6f, 1, .3f)
    };
    public static int[] Roll(SurvivalPlayer p)
    {
        // Three different perks, capped at 4 stacks each.
        var pool = Enumerable.Range(0, Count).Where(i => p.Perks[i] < 4).OrderBy(_ => Random.value).Take(3).ToArray();
        return pool;
    }
    public static void Give(SurvivalPlayer p, int perk)
    {
        p.Perks[perk]++;
        MultiverseFX.Ring(p.transform.position, 2.4f, Colors[perk]);
        MultiverseFX.FloatText(p.transform.position + Vector3.up * 2.4f, Names[perk] + " " + Roman(p.Perks[perk]), Colors[perk], .7f, 1.6f);
    }
    public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => n.ToString() };
    public static string Summary(SurvivalPlayer p)
    {
        var parts = Enumerable.Range(0, Count).Where(i => p.Perks[i] > 0).Select(i => Names[i] + " " + Roman(p.Perks[i]));
        return string.Join("  ·  ", parts);
    }
}

public sealed class SurvivalPerkUI : MonoBehaviour
{
    SurvivalDirector director; GameObject panel; Text title, owned; readonly Text[] cardTitle = new Text[3], cardInfo = new Text[3];
    readonly Image[] cardImage = new Image[3], cardStripe = new Image[3]; int[] offer; bool open; Font font;
    public bool Open => open;
    public void Init(SurvivalDirector d)
    {
        director = d; font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("Mejoras roguelike", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
        var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = .5f;
        panel = Box("Elige una mejora", go.transform, new Vector2(0, -40), new Vector2(1000, 290), new Color(.08f, .16f, .08f, .95f));
        title = Label(panel.transform, new Vector2(0, 112), new Vector2(960, 40), 28, new Color(.7f, 1, .5f)); title.text = "ELIGE UNA MEJORA · 1 / 2 / 3";
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            var card = Box("Carta " + i, panel.transform, new Vector2((i - 1) * 320, -20), new Vector2(300, 190), new Color(.15f, .28f, .14f, 1));
            cardImage[i] = card.GetComponent<Image>();
            card.AddComponent<Button>().onClick.AddListener(() => Choose(idx));
            cardStripe[i] = Box("Franja", card.transform, new Vector2(0, 84), new Vector2(250, 10), Color.white).GetComponent<Image>();
            cardTitle[i] = Label(card.transform, new Vector2(0, 50), new Vector2(280, 60), 24, Color.white);
            cardInfo[i] = Label(card.transform, new Vector2(0, -25), new Vector2(270, 90), 17, new Color(.9f, .95f, .85f));
            var key = Label(card.transform, new Vector2(0, -78), new Vector2(100, 26), 16, new Color(.7f, 1, .5f)); key.text = "[" + (i + 1) + "]";
        }
        owned = Label(go.transform, new Vector2(0, 0), new Vector2(900, 30), 16, new Color(.75f, 1, .6f));
        var r = owned.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, 0); r.anchoredPosition = new Vector2(0, 70);
        MultiverseUI.Skin(go, MultiverseUI.Mode.Zombie);
        panel.SetActive(false);
    }
    GameObject Box(string name, Transform parent, Vector2 pos, Vector2 size, Color c)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image)); var r = g.GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; g.GetComponent<Image>().color = c; return g;
    }
    Text Label(Transform parent, Vector2 pos, Vector2 size, int pt, Color c)
    {
        var g = new GameObject("Texto", typeof(RectTransform), typeof(Text)); var r = g.GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size;
        var t = g.GetComponent<Text>(); t.font = font; t.fontSize = pt; t.color = c; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false; return t;
    }
    public void Offer(SurvivalPlayer human)
    {
        offer = SurvivalPerks.Roll(human);
        if (offer.Length == 0) return;
        for (int i = 0; i < 3; i++)
        {
            bool has = i < offer.Length; cardImage[i].gameObject.SetActive(has); if (!has) continue;
            int k = offer[i]; int lvl = human.Perks[k] + 1;
            cardTitle[i].text = SurvivalPerks.Names[k] + " " + SurvivalPerks.Roman(lvl); cardTitle[i].color = new Color(.1f, .16f, .07f);
            cardInfo[i].text = SurvivalPerks.Info[k]; cardInfo[i].color = new Color(.2f, .25f, .15f);
            cardStripe[i].color = SurvivalPerks.Colors[k] * .85f;
        }
        open = true; panel.SetActive(true); EscapeAudio.PlayUI("unlock", .6f);
    }
    public void Choose(int i)
    {
        if (!open || offer == null || i >= offer.Length) return;
        open = false; panel.SetActive(false);
        if (director.IsClient) director.Net.RequestPerk(offer[i]);   // online: the host applies it too
        SurvivalPerks.Give(director.Human, offer[i]); EscapeAudio.PlayUI("success", .7f);
        director.NotifyLocal("MEJORA: " + SurvivalPerks.Names[offer[i]] + " " + SurvivalPerks.Roman(director.Human.Perks[offer[i]]));
    }
    public void ForcePick() { if (open && offer != null && offer.Length > 0) Choose(Random.Range(0, offer.Length)); }
    void Update()
    {
        if (director == null) return;
        var h = director.Human;
        owned.text = h != null && director.Fighting ? (SurvivalPerks.Summary(h) is var s && s.Length > 0 ? "MEJORAS: " + s : "") + (director.Modifier.Length > 0 && director.State == SurvivalDirector.Phase.Wave ? "    |    " + director.Modifier : "") : "";
        if (!open) return;
        if (director.State != SurvivalDirector.Phase.Break) { ForcePick(); return; }
        var k = Keyboard.current; if (k == null) return;
        if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) Choose(0);
        if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) Choose(1);
        if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) Choose(2);
    }
}
