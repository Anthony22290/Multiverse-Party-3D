using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Player settings shared by every scene: mouse sensitivity, master and music volume (saved in PlayerPrefs).</summary>
public static class MultiverseSettings
{
    const string SensKey = "Ajuste_Sensibilidad", VolKey = "Ajuste_Volumen", MusicKey = "Ajuste_Musica";
    public static event Action Changed;

    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat(SensKey, 1f);
        set { PlayerPrefs.SetFloat(SensKey, Mathf.Clamp(value, .2f, 3f)); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat(VolKey, 1f);
        set { PlayerPrefs.SetFloat(VolKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); AudioListener.volume = MasterVolume; Changed?.Invoke(); }
    }

    public static float MusicVolume
    {
        get => PlayerPrefs.GetFloat(MusicKey, .8f);
        set { PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Changed?.Invoke(); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Apply() => AudioListener.volume = MasterVolume;
}

/// <summary>Looping background music for the menu and the plaza; follows the music volume setting.</summary>
public sealed class PartyMusic : MonoBehaviour
{
    AudioSource source;
    float baseVolume = .45f;

    public static PartyMusic Play(GameObject host, string clip, float volume = .45f)
    {
        var c = Resources.Load<AudioClip>("PartyAudio/" + clip);
        if (c == null) return null;
        var m = host.AddComponent<PartyMusic>();
        m.baseVolume = volume;
        m.source = host.AddComponent<AudioSource>();
        m.source.clip = c; m.source.loop = true; m.source.spatialBlend = 0f; m.source.playOnAwake = false;
        m.Refresh(); m.source.Play();
        MultiverseSettings.Changed += m.Refresh;
        return m;
    }

    void Refresh() { if (source != null) source.volume = baseVolume * MultiverseSettings.MusicVolume; }
    void OnDestroy() => MultiverseSettings.Changed -= Refresh;
}

/// <summary>Settings card (sensitivity, volume, music). Toggle(root) opens or closes it on a canvas.</summary>
public sealed class MultiverseSettingsPanel : MonoBehaviour
{
    public static bool IsOpen => current != null;
    static MultiverseSettingsPanel current;
    Text sensValue, volValue, musicValue;

    public static void Toggle(Transform canvas)
    {
        if (current != null) { Destroy(current.gameObject); current = null; return; }
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var dim = new GameObject("Ajustes", typeof(RectTransform), typeof(Image));
        var r = dim.GetComponent<RectTransform>(); r.SetParent(canvas, false);
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        dim.GetComponent<Image>().color = new Color(0, 0, 0, .55f);
        current = dim.AddComponent<MultiverseSettingsPanel>();
        current.Build(dim.transform);
        MultiverseUI.Skin(dim, MultiverseUI.Mode.Hub);
    }

    void Build(Transform root)
    {
        var card = Rect("Tarjeta", root, Vector2.zero, new Vector2(500, 380));
        card.gameObject.AddComponent<Image>().color = new Color(.1f, .16f, .3f, .97f);
        Label("AJUSTES", card, new Vector2(0, 150), new Vector2(460, 50), 34, new Color(1f, .8f, .3f));
        sensValue = Row(card, 80, "SENSIBILIDAD DEL RATÓN", .2f, 3f, MultiverseSettings.Sensitivity, v => { MultiverseSettings.Sensitivity = v; Refresh(); });
        volValue = Row(card, 0, "VOLUMEN GENERAL", 0f, 1f, MultiverseSettings.MasterVolume, v => { MultiverseSettings.MasterVolume = v; Refresh(); });
        musicValue = Row(card, -80, "MÚSICA DE LA PLAZA Y EL MENÚ", 0f, 1f, MultiverseSettings.MusicVolume, v => { MultiverseSettings.MusicVolume = v; Refresh(); });
        var close = Rect("CERRAR", card, new Vector2(0, -150), new Vector2(200, 46));
        close.gameObject.AddComponent<Image>().color = new Color(1f, .74f, .22f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(() => { Destroy(gameObject); current = null; });
        Label("CERRAR", close, Vector2.zero, new Vector2(190, 40), 20, new Color(.16f, .1f, .04f));
        Refresh();
    }

    void Refresh()
    {
        sensValue.text = "x" + MultiverseSettings.Sensitivity.ToString("0.0");
        volValue.text = Mathf.RoundToInt(MultiverseSettings.MasterVolume * 100) + "%";
        musicValue.text = Mathf.RoundToInt(MultiverseSettings.MusicVolume * 100) + "%";
    }

    void OnDestroy() { if (current == this) current = null; }

    Text Row(Transform card, float y, string label, float min, float max, float value, Action<float> changed)
    {
        Label(label, card, new Vector2(-30, y + 20), new Vector2(380, 26), 16, new Color(1f, .97f, .9f)).alignment = TextAnchor.MiddleLeft;
        var valueText = Label("", card, new Vector2(185, y + 20), new Vector2(90, 26), 16, new Color(1f, .8f, .3f));
        var sr = Rect("Barra " + label, card, new Vector2(0, y - 10), new Vector2(420, 18));
        var bg = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        Stretch(bg.GetComponent<RectTransform>(), sr); bg.GetComponent<Image>().color = new Color(1, 1, 1, .18f); bg.GetComponent<Image>().sprite = MultiverseUI.Pill; bg.GetComponent<Image>().type = Image.Type.Sliced;
        var fillArea = new GameObject("Relleno", typeof(RectTransform)); Stretch(fillArea.GetComponent<RectTransform>(), sr);
        var fill = new GameObject("Color", typeof(RectTransform), typeof(Image)); Stretch(fill.GetComponent<RectTransform>(), fillArea.transform);
        fill.GetComponent<Image>().color = new Color(.35f, .9f, 1f); fill.GetComponent<Image>().sprite = MultiverseUI.Pill; fill.GetComponent<Image>().type = Image.Type.Sliced;
        var handleArea = new GameObject("Asa", typeof(RectTransform)); Stretch(handleArea.GetComponent<RectTransform>(), sr);
        var handle = new GameObject("Botón", typeof(RectTransform), typeof(Image));
        var hr = handle.GetComponent<RectTransform>(); hr.SetParent(handleArea.transform, false); hr.sizeDelta = new Vector2(24, 30);
        handle.GetComponent<Image>().color = new Color(1f, .8f, .3f); handle.GetComponent<Image>().sprite = MultiverseUI.Pill;
        var slider = sr.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.GetComponent<RectTransform>(); slider.handleRect = hr; slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = min; slider.maxValue = max; slider.value = value;
        slider.onValueChanged.AddListener(v => changed(v));
        return valueText;
    }

    static void Stretch(RectTransform r, Transform parent)
    {
        r.SetParent(parent, false); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }

    static Text Label(string value, Transform parent, Vector2 pos, Vector2 size, int pt, Color color)
    {
        var t = Rect("Texto", parent, pos, size).gameObject.AddComponent<Text>();
        t.font = MultiverseUI.DisplayFont; t.text = value; t.fontSize = pt; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        return t;
    }
}
