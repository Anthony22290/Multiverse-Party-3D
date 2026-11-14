using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared party-game look for every runtime-built uGUI canvas: chunky rounded panels with a thick
/// outline and a drop "lip", springy buttons, a rounded display font and pop-in panels.
/// The HUD scripts still build their own layout and callbacks; Skin() only restyles what exists.
/// </summary>
public static class MultiverseUI
{
    public enum Mode { Hub, Arena, Race, Mortal, Escape, Zombie }

    public struct Theme
    {
        public Color panel, panelEdge, button, buttonText, accent, text, ink;
    }

    public static Theme For(Mode mode)
    {
        switch (mode)
        {
            case Mode.Arena: return new Theme {
                panel = new Color(.16f, .06f, .26f, .9f), panelEdge = new Color(1f, .3f, .62f),
                button = new Color(1f, .82f, .2f), buttonText = new Color(.2f, .05f, .22f),
                accent = new Color(1f, .3f, .62f), text = Color.white, ink = new Color(.08f, .02f, .12f) };
            case Mode.Race: return new Theme {
                panel = new Color(.1f, .14f, .38f, .9f), panelEdge = new Color(1f, .82f, .16f),
                button = new Color(1f, .27f, .45f), buttonText = Color.white,
                accent = new Color(1f, .82f, .16f), text = Color.white, ink = new Color(.03f, .04f, .14f) };
            case Mode.Mortal: return new Theme {
                panel = new Color(.06f, .1f, .2f, .92f), panelEdge = new Color(.3f, 1f, .85f),
                button = new Color(.3f, 1f, .85f), buttonText = new Color(.03f, .08f, .14f),
                accent = new Color(1f, .35f, .62f), text = new Color(.94f, .98f, 1f), ink = new Color(.01f, .03f, .07f) };
            case Mode.Escape: return new Theme {
                panel = new Color(.07f, .1f, .2f, .92f), panelEdge = new Color(.55f, 1f, .8f),
                button = new Color(1f, .76f, .3f), buttonText = new Color(.12f, .07f, .03f),
                accent = new Color(.78f, .55f, 1f), text = new Color(.96f, .97f, 1f), ink = new Color(.03f, .03f, .09f) };
            case Mode.Zombie: return new Theme {
                panel = new Color(.06f, .12f, .1f, .92f), panelEdge = new Color(.55f, 1f, .4f),
                button = new Color(1f, .8f, .25f), buttonText = new Color(.1f, .08f, .02f),
                accent = new Color(1f, .4f, .35f), text = new Color(.95f, 1f, .94f), ink = new Color(.02f, .05f, .03f) };
            default: return new Theme {
                panel = new Color(.12f, .2f, .36f, .9f), panelEdge = new Color(1f, .78f, .25f),
                button = new Color(1f, .74f, .22f), buttonText = new Color(.16f, .1f, .04f),
                accent = new Color(.35f, .9f, 1f), text = new Color(1f, .98f, .92f), ink = new Color(.04f, .06f, .12f) };
        }
    }

    static MultiverseUISkin skin;
    static bool skinLoaded;
    static Sprite rounded, pill;
    static Texture2D roundedTexture;

    public static MultiverseUISkin Skin()
    {
        if (!skinLoaded) { skin = Resources.Load<MultiverseUISkin>("MultiverseUISkin"); skinLoaded = true; }
        return skin;
    }

    public static Font DisplayFont => Skin() != null && Skin().display != null ? Skin().display : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    public static Font BodyFont => Skin() != null && Skin().body != null ? Skin().body : DisplayFont;

    /// <summary>White rounded rectangle with soft anti-aliased corners, 9-sliced.</summary>
    public static Sprite Rounded
    {
        get
        {
            if (rounded == null) { roundedTexture = MakeRounded(64, 20); rounded = Sprite.Create(roundedTexture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24)); rounded.name = "Multiverse Rounded"; }
            return rounded;
        }
    }

    public static Texture2D RoundedTexture { get { _ = Rounded; return roundedTexture; } }

    public static Sprite Pill
    {
        get
        {
            if (pill == null) { var t = MakeRounded(64, 31); pill = Sprite.Create(t, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31)); pill.name = "Multiverse Pill"; }
            return pill;
        }
    }

    static Texture2D MakeRounded(int size, float radius)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Multiverse Rounded Tex" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + .5f, radius, size - radius), cy = Mathf.Clamp(y + .5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + .5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        t.SetPixels32(px); t.Apply(false, true);
        return t;
    }

    /// <summary>Restyles a runtime canvas hierarchy. Safe to call more than once.</summary>
    public static void Skin(GameObject root, Mode mode, bool popPanels = true)
    {
        if (root == null) return;
        var theme = For(mode);
        var buttons = new HashSet<GameObject>();
        foreach (var b in root.GetComponentsInChildren<Button>(true)) { StyleButton(b, theme); buttons.Add(b.gameObject); }
        foreach (var field in root.GetComponentsInChildren<InputField>(true))
        {
            var img = field.GetComponent<Image>();
            if (img != null) { img.sprite = Pill; img.type = Image.Type.Sliced; img.color = new Color(1, 1, 1, .95f); AddOutline(img.gameObject, theme.panelEdge, 3); }
            if (field.textComponent != null) field.textComponent.color = theme.ink;
        }
        foreach (var img in root.GetComponentsInChildren<Image>(true))
        {
            if (buttons.Contains(img.gameObject) || img.sprite != null || img.GetComponent<InputField>() != null) continue;
            var r = img.rectTransform.rect;
            bool fullscreen = img.rectTransform.anchorMin == Vector2.zero && img.rectTransform.anchorMax == Vector2.one;
            if (fullscreen) { img.color = new Color(theme.ink.r, theme.ink.g, theme.ink.b, Mathf.Max(.55f, img.color.a * .85f)); continue; }
            if (r.width < 40 || r.height < 30) continue; // minimap lines, markers, bars stay crisp
            StylePanel(img, theme, popPanels && r.width >= 300 && r.height >= 200);
        }
        foreach (var text in root.GetComponentsInChildren<Text>(true)) StyleText(text, theme, buttons.Contains(text.transform.parent != null ? text.transform.parent.gameObject : null));
    }

    public static void StylePanel(Image img, Theme theme, bool pop)
    {
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.4f;
        // Keep the original hue when a HUD encodes meaning in the panel color; otherwise use the theme.
        var c = img.color;
        img.color = new Color(Mathf.Lerp(c.r, theme.panel.r, .8f), Mathf.Lerp(c.g, theme.panel.g, .8f), Mathf.Lerp(c.b, theme.panel.b, .8f), Mathf.Max(.95f, c.a)); // near-opaque so the outline copies don't tint through
        AddOutline(img.gameObject, theme.panelEdge, 3);
        AddShadow(img.gameObject, new Color(0, 0, 0, .35f), new Vector2(0, -7));
        if (pop && img.GetComponent<MultiverseUIPop>() == null) img.gameObject.AddComponent<MultiverseUIPop>();
    }

    public static void StyleButton(Button b, Theme theme)
    {
        var img = b.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = Pill; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            img.color = theme.button;
            AddOutline(img.gameObject, theme.ink, 3);
            AddShadow(img.gameObject, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .85f), new Vector2(0, -6));
        }
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.82f, .82f, .82f);
        colors.disabledColor = new Color(.6f, .6f, .6f, .5f);
        colors.colorMultiplier = 1.2f; colors.fadeDuration = .08f;
        b.colors = colors;
        if (b.GetComponent<MultiverseUIBounce>() == null) b.gameObject.AddComponent<MultiverseUIBounce>();
        foreach (var t in b.GetComponentsInChildren<Text>(true))
        {
            t.color = theme.buttonText;
            foreach (var s in t.GetComponents<Shadow>()) Object.Destroy(s);
        }
    }

    public static void StyleText(Text t, Theme theme, bool inButton)
    {
        int size = t.fontSize;
        t.font = size >= 20 || inButton ? DisplayFont : BodyFont;
        // Display fonts are wider than Arial; shrink to fit instead of clipping.
        t.resizeTextForBestFit = true; t.resizeTextMaxSize = size; t.resizeTextMinSize = Mathf.Max(9, Mathf.RoundToInt(size * .55f));
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
        t.lineSpacing = 1.05f;
        if (t.GetComponentInParent<InputField>() != null) return;
        if (!inButton && size >= 24 && t.GetComponent<Outline>() == null) AddOutline(t.gameObject, theme.ink, Mathf.Clamp(size / 14f, 1.5f, 4f));
        else if (!inButton && t.GetComponent<Shadow>() == null) AddShadow(t.gameObject, new Color(theme.ink.r, theme.ink.g, theme.ink.b, .9f), new Vector2(1.5f, -2f));
        if (size >= 56 && t.GetComponent<MultiverseUIWobble>() == null) t.gameObject.AddComponent<MultiverseUIWobble>();
    }

    static void AddOutline(GameObject go, Color c, float distance)
    {
        foreach (var existing in go.GetComponents<Outline>()) { existing.effectColor = c; existing.effectDistance = new Vector2(distance, -distance); return; }
        var o = go.AddComponent<Outline>(); o.effectColor = c; o.effectDistance = new Vector2(distance, -distance); o.useGraphicAlpha = true;
    }

    static void AddShadow(GameObject go, Color c, Vector2 offset)
    {
        foreach (var s in go.GetComponents<Shadow>()) if (s.GetType() == typeof(Shadow)) { s.effectColor = c; s.effectDistance = offset; return; }
        var sh = go.AddComponent<Shadow>(); sh.effectColor = c; sh.effectDistance = offset;
    }

    // ---------- IMGUI helpers (Arena del Caos HUD uses OnGUI) ----------
    static GUIStyle imguiPanel;
    public static void DrawPanel(Rect rect, Color fill, Color edge)
    {
        if (imguiPanel == null) imguiPanel = new GUIStyle { normal = { background = RoundedTexture }, border = new RectOffset(22, 22, 22, 22) };
        var old = GUI.color;
        GUI.color = new Color(0, 0, 0, .35f); GUI.Box(new Rect(rect.x, rect.y + 6, rect.width, rect.height), GUIContent.none, imguiPanel);
        GUI.color = edge; GUI.Box(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), GUIContent.none, imguiPanel);
        GUI.color = fill; GUI.Box(rect, GUIContent.none, imguiPanel);
        GUI.color = old;
    }

    public static void DrawMeter(Rect rect, float fill, Color back, Color front)
    {
        var tex = Pill.texture; var old = GUI.color;
        GUI.color = back; GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), tex, ScaleMode.StretchToFill);
        float w = rect.width * Mathf.Clamp01(fill);
        if (w > 1) { GUI.color = front; GUI.DrawTexture(new Rect(rect.x, rect.y, Mathf.Max(w, rect.height), rect.height), tex, ScaleMode.StretchToFill); }
        GUI.color = old;
    }

    public static void PlayClick(bool hover)
    {
        var s = Skin(); if (s == null) return;
        var clip = hover ? s.hover : s.click; if (clip == null) return;
        if (source == null)
        {
            var go = new GameObject("Multiverse UI Audio") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go); source = go.AddComponent<AudioSource>(); source.spatialBlend = 0; source.playOnAwake = false;
        }
        source.PlayOneShot(clip, hover ? .25f : .6f);
    }
    static AudioSource source;
}

/// <summary>Springy hover/press scale for buttons, in unscaled time so it works while paused.</summary>
public sealed class MultiverseUIBounce : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    float target = 1f, scale = 1f, velocity;
    bool hovered;
    Selectable selectable;
    void Awake() { selectable = GetComponent<Selectable>(); }
    void OnEnable() { scale = target = 1f; velocity = 0; transform.localScale = Vector3.one; }
    bool Interactable => selectable == null || selectable.IsInteractable();
    public void OnPointerEnter(PointerEventData e) { if (!Interactable) return; hovered = true; target = 1.07f; MultiverseUI.PlayClick(true); }
    public void OnPointerExit(PointerEventData e) { hovered = false; target = 1f; }
    public void OnPointerDown(PointerEventData e) { if (!Interactable) return; target = .92f; MultiverseUI.PlayClick(false); }
    public void OnPointerUp(PointerEventData e) { target = hovered ? 1.07f : 1f; }
    public void OnSelect(BaseEventData e) { if (Interactable) target = 1.05f; }
    public void OnDeselect(BaseEventData e) { target = 1f; }
    void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        velocity += (target - scale) * 320f * dt; velocity *= Mathf.Exp(-18f * dt);
        scale += velocity * dt;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}

/// <summary>Overshooting pop when a panel becomes visible.</summary>
public sealed class MultiverseUIPop : MonoBehaviour
{
    float t = 1f;
    void OnEnable() { t = 0f; transform.localScale = Vector3.one * .85f; }
    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime * 4.5f);
        float s = 1f + Mathf.Sin(t * Mathf.PI) * .06f * (1f - t);
        transform.localScale = Vector3.one * Mathf.Lerp(.85f, 1f, 1f - Mathf.Pow(1f - t, 3f)) * s;
    }
}

/// <summary>Gentle playful motion for big headline text.</summary>
public sealed class MultiverseUIWobble : MonoBehaviour
{
    Quaternion baseRotation; float seed;
    void Awake() { baseRotation = transform.localRotation; seed = Random.value * 10f; }
    void Update()
    {
        float time = Time.unscaledTime + seed;
        transform.localRotation = baseRotation * Quaternion.Euler(0, 0, Mathf.Sin(time * 1.6f) * 1.6f);
        float s = 1f + Mathf.Sin(time * 2.4f) * .025f;
        transform.localScale = new Vector3(s, s, 1);
    }
}
