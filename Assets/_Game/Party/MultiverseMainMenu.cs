using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>First screen of the game: pick a name, then create a room, join one with a code or play offline.</summary>
public sealed class MultiverseMainMenu : MonoBehaviour
{
    public Material atlas;
    public GameObject palm, rock, ship, barrel, chest;
    public Material wood, darkWood, water, brass;

    InputField nameField, codeField;
    Text status, sizeLabel, logoSub, rulesText, mapsLabel, orderText, goalText;
    GameObject mapsPanel;
    Button[] sizeButtons = new Button[3];
    Button createButton, joinButton, offlineButton, roomsButton, publicButton;
    GameObject roomsPanel; RectTransform roomsList; Text roomsStatus; PartyRoomBrowser browser;
    int size = 4;
    bool busy;
    Transform cameraPivot;
    readonly System.Collections.Generic.List<Animator> dancers = new System.Collections.Generic.List<Animator>();
    readonly System.Collections.Generic.List<Transform> stage = new System.Collections.Generic.List<Transform>();
    readonly System.Collections.Generic.List<Renderer> rings = new System.Collections.Generic.List<Renderer>();
    readonly Button[] colorButtons = new Button[6];
    Text characterLabel;
    int character, color;
    float nextWave;

    static readonly Color Ink = new Color(.05f, .07f, .14f), Cream = new Color(1f, .97f, .9f), Gold = new Color(1f, .78f, .25f);

    void Start()
    {
        Time.timeScale = 1f; AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        Application.targetFrameRate = 60; Application.runInBackground = true;
        if (MultiverseParty.Instance != null) Destroy(MultiverseParty.Instance.gameObject);
        size = Mathf.Clamp(PlayerPrefs.GetInt("Multiverse_Tamano", 4), 4, 6);
        BuildBackdrop();
        BuildUI();
        status.text = string.IsNullOrEmpty(MultiverseParty.LastStatus) ? "Partidas de 4 a 6 jugadores · las plazas libres las ocupan bots." : MultiverseParty.LastStatus;
        MultiverseParty.LastStatus = "";
    }

    // ------------------------------------------------------------------ backdrop
    void BuildBackdrop()
    {
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.45f, .72f, .86f);
        RenderSettings.fogStartDistance = 30; RenderSettings.fogEndDistance = 110;
        RenderSettings.ambientLight = new Color(.75f, .78f, .8f);
        var sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(42, -30, 0); sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
        sun.color = new Color(1f, .95f, .86f);

        var world = new GameObject("Fondo del menú").transform;
        Prim(PrimitiveType.Cube, world, "Mar", new Vector3(0, -1.2f, 0), new Vector3(260, .2f, 260), water, new Color(.2f, .55f, .75f));
        Prim(PrimitiveType.Cylinder, world, "Isla", new Vector3(0, -.6f, 0), new Vector3(22, 1f, 22), null, new Color(.96f, .85f, .55f));
        Prim(PrimitiveType.Cylinder, world, "Escenario", new Vector3(0, .1f, 0), new Vector3(11, .35f, 11), wood, new Color(.6f, .4f, .25f));
        Prim(PrimitiveType.Cylinder, world, "Escenario centro", new Vector3(0, .32f, 0), new Vector3(6.5f, .12f, 6.5f), brass, new Color(1f, .75f, .3f));
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2 / 10f;
            var pos = new Vector3(Mathf.Sin(a) * 7.6f, 1.6f, Mathf.Cos(a) * 7.6f);
            Prim(PrimitiveType.Cylinder, world, "Poste", pos, new Vector3(.25f, 1.6f, .25f), darkWood, new Color(.35f, .22f, .14f));
            var bulb = Prim(PrimitiveType.Sphere, world, "Farol", pos + Vector3.up * 1.75f, Vector3.one * .55f, null, MultiverseParty.Colors[i % 6]);
            MakeGlow(bulb, MultiverseParty.Colors[i % 6]);
        }
        Decor(palm, world, new Vector3(-9, 0, 6), 6f, 20); Decor(palm, world, new Vector3(9.5f, 0, 5), 5.4f, -40);
        Decor(palm, world, new Vector3(-6, 0, -9), 5f, 80); Decor(rock, world, new Vector3(8, 0, -7), 2f, 10);
        Decor(barrel, world, new Vector3(-4.6f, .3f, 4.8f), 1.1f, 0); Decor(chest, world, new Vector3(4.5f, .3f, 4.6f), 1.2f, 200);
        Decor(ship, world, new Vector3(-28, -1f, 30), 12f, 130);

        int count = PartyGames.Characters.Length;
        for (int i = 0; i < count; i++)
        {
            float a = (i - (count - 1) * .5f) * .5f;
            var root = new GameObject("Personaje " + i).transform; root.SetParent(world, false);
            root.position = new Vector3(Mathf.Sin(a) * 3.6f, .4f, Mathf.Cos(a) * 3.6f - 1.5f);
            root.rotation = Quaternion.LookRotation(new Vector3(0, 0, -12) - root.position);
            var prefab = Resources.Load<GameObject>("PirateCharacters/Characters_" + PartyGames.Characters[i]);
            var visual = prefab != null ? PirateArenaVisuals.AddModel(prefab, root, 1.8f, atlas, false) : null;
            var ring = Prim(PrimitiveType.Cylinder, root, "Aro", root.position + Vector3.up * .05f, new Vector3(1.2f, .03f, 1.2f), null, Color.gray);
            rings.Add(ring.GetComponent<Renderer>()); stage.Add(root);
            if (visual != null)
            {
                var anim = visual.GetComponentInChildren<Animator>();
                if (anim != null) { anim.applyRootMotion = false; anim.CrossFadeInFixedTime("Base Layer.Idle", .1f, 0, Random.value); }
                dancers.Add(anim);
            }
        }

        var cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cg.tag = "MainCamera"; cam = cg.GetComponent<Camera>(); }
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.45f, .74f, .9f); cam.fieldOfView = 45;
        cameraPivot = new GameObject("Pivote cámara").transform;
        cam.transform.SetParent(cameraPivot, false);
        cam.transform.localPosition = new Vector3(-3.2f, 3.6f, -13.5f);
        cam.transform.localRotation = Quaternion.Euler(11, 12, 0);
        if (PartyMusic.Play(gameObject, "menu", .45f) == null && FindAnyObjectByType<MultiverseMusicStub>() == null) gameObject.AddComponent<MultiverseMusicStub>();
    }

    GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, Color tint)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
        go.transform.position = pos; go.transform.localScale = scale;
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (mat != null) r.sharedMaterial = mat;
        else { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = tint; m.SetFloat("_Smoothness", .25f); r.sharedMaterial = m; }
        return go;
    }

    static void MakeGlow(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = c;
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * .55f); r.sharedMaterial = m;
    }

    void Decor(GameObject prefab, Transform parent, Vector3 at, float height, float yaw)
    {
        if (prefab == null) return;
        var root = new GameObject("Decorado " + prefab.name).transform; root.SetParent(parent, false);
        root.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
        PirateArenaVisuals.AddModel(prefab, root, height, atlas, false);
    }

    void Update()
    {
        if (cameraPivot != null) cameraPivot.rotation = Quaternion.Euler(0, Mathf.Sin(Time.time * .12f) * 9f, 0);
        if (Time.time > nextWave && dancers.Count > 0)
        {
            nextWave = Time.time + 2.4f;
            var a = dancers[Random.Range(0, dancers.Count)];
            if (a != null && a != dancers[character]) a.CrossFadeInFixedTime(Random.value < .5f ? "Base Layer.Wave" : "Base Layer.Jump", .15f);
            if (dancers[character] != null) dancers[character].CrossFadeInFixedTime("Base Layer.Wave", .15f);
        }
        for (int i = 0; i < stage.Count; i++)
        {
            bool sel = i == character;
            stage[i].localScale = Vector3.Lerp(stage[i].localScale, Vector3.one * (sel ? 1.25f : .9f), Time.deltaTime * 8f);
        }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && roomsPanel != null) CloseRooms();
        else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && mapsPanel != null) CloseMaps();
        else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && MultiverseSettingsPanel.IsOpen) MultiverseSettingsPanel.Toggle(menuRoot);
        if (status != null && !busy && !string.IsNullOrEmpty(MultiverseParty.LastStatus)) { status.text = MultiverseParty.LastStatus; }
    }

    // ------------------------------------------------------------------ UI
    void BuildUI()
    {
        if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var root = new GameObject("Menú principal", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f; scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        var logo = Rect("Logo", root.transform, new Vector2(0, 1), new Vector2(330, -110), new Vector2(560, 150));
        var logoImg = logo.gameObject.AddComponent<Image>(); logoImg.color = new Color(Ink.r, Ink.g, Ink.b, .9f);
        Label("MULTIVERSE\nPARTY 3D", logo, new Vector2(0, 14), new Vector2(520, 110), 50, Gold);
        logoSub = Label("", logo, new Vector2(0, -54), new Vector2(520, 30), 17, Cream);

        menuRoot = root.transform;
        var pick = Rect("Tu personaje", root.transform, new Vector2(0, .5f), new Vector2(250, 10), new Vector2(400, 190));
        pick.gameObject.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, .86f);
        Label("TU PERSONAJE", pick, new Vector2(0, 70), new Vector2(360, 28), 20, Gold);
        Btn(pick, "◄", new Vector2(-150, 28), new Vector2(54, 44), () => SetCharacter(character - 1));
        Btn(pick, "►", new Vector2(150, 28), new Vector2(54, 44), () => SetCharacter(character + 1));
        characterLabel = Label("", pick, new Vector2(0, 28), new Vector2(220, 40), 26, Cream);
        Label("COLOR", pick, new Vector2(0, -18), new Vector2(360, 24), 16, Gold);
        for (int i = 0; i < 6; i++)
        {
            int c = i;
            colorButtons[i] = Btn(pick, "", new Vector2(-150 + i * 60, -60), new Vector2(46, 46), () => SetColor(c));
        }

        var rules = Rect("Reglas", root.transform, new Vector2(0, 0), new Vector2(330, 120), new Vector2(560, 150));
        rules.gameObject.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, .86f);
        Label("CÓMO SE JUEGA", rules, new Vector2(0, 50), new Vector2(520, 30), 20, Gold);
        rulesText = Label("", rules, new Vector2(0, -14), new Vector2(530, 96), 15, Cream);
        rulesText.alignment = TextAnchor.MiddleLeft;

        var panel = Rect("Panel", root.transform, new Vector2(1, .5f), new Vector2(-290, 0), new Vector2(490, 590));
        panel.gameObject.AddComponent<Image>().color = new Color(.1f, .16f, .32f, .95f);
        Label("TU NOMBRE", panel, new Vector2(0, 262), new Vector2(440, 30), 20, Gold);
        nameField = Field(panel, new Vector2(0, 222), "Escribe tu nombre", MultiverseParty.SavedName, 14);
        nameField.onEndEdit.AddListener(v => MultiverseParty.SavedName = v);

        Label("CREAR SALA · JUGADORES", panel, new Vector2(0, 172), new Vector2(440, 30), 18, Gold);
        for (int i = 0; i < 3; i++)
        {
            int s = 4 + i;
            sizeButtons[i] = Btn(panel, s.ToString(), new Vector2(-110 + i * 110, 132), new Vector2(92, 44), () => SetSize(s));
        }
        sizeLabel = Label("", panel, new Vector2(-92, 96), new Vector2(240, 26), 13, Cream);
        publicButton = Btn(panel, "", new Vector2(132, 96), new Vector2(170, 26), () => { MultiverseParty.PublicRooms = !MultiverseParty.PublicRooms; RefreshPublic(); });
        publicButton.GetComponentInChildren<Text>().fontSize = 14;
        createButton = Btn(panel, "CREAR SALA EN LÍNEA", new Vector2(0, 58), new Vector2(380, 52), () => Create());

        Label("UNIRSE CON CÓDIGO", panel, new Vector2(0, 8), new Vector2(440, 30), 18, Gold);
        codeField = Field(panel, new Vector2(-122, -34), "CÓDIGO", "", 12);
        codeField.characterValidation = InputField.CharacterValidation.Alphanumeric;
        codeField.onValueChanged.AddListener(v => { var up = v.ToUpperInvariant(); if (up != v) codeField.text = up; });
        ((RectTransform)codeField.transform).sizeDelta = new Vector2(176, 48);
        joinButton = Btn(panel, "UNIRSE", new Vector2(30, -34), new Vector2(108, 48), () => Join());
        roomsButton = Btn(panel, "SALAS", new Vector2(150, -34), new Vector2(108, 48), () => OpenRooms());
        roomsButton.GetComponent<Image>().color = new Color(.45f, .85f, 1f);
        // 2026-11-21 · after a lost connection the code is already there: one click on UNIRSE to get back in.
        codeField.text = !string.IsNullOrEmpty(MultiverseParty.RejoinCode) ? MultiverseParty.RejoinCode : MultiverseParty.LastRoom;

        offlineButton = Btn(panel, "JUGAR SIN CONEXIÓN", new Vector2(0, -94), new Vector2(380, 48), () => Offline());
        var maps = Btn(panel, "SELECCIONAR MAPAS", new Vector2(0, -148), new Vector2(380, 42), () => OpenMaps());
        maps.GetComponent<Image>().color = new Color(.45f, .85f, 1f);
        mapsLabel = maps.GetComponentInChildren<Text>(); mapsLabel.fontSize = 19;
        Btn(panel, "AJUSTES", new Vector2(-100, -198), new Vector2(180, 38), () => MultiverseSettingsPanel.Toggle(menuRoot));
        Btn(panel, "SALIR", new Vector2(100, -198), new Vector2(180, 38), Quit);
        status = Label("", panel, new Vector2(0, -250), new Vector2(460, 56), 15, Cream);

        MultiverseUI.Skin(root, MultiverseUI.Mode.Hub);
        RefreshPublic();
        RefreshMode();
        SetSize(size);
        SetCharacter(MultiverseParty.SavedCharacter);
        SetColor(MultiverseParty.SavedColor);
    }

    Transform menuRoot;

    void SetCharacter(int c)
    {
        int n = PartyGames.Characters.Length;
        character = ((c % n) + n) % n;
        MultiverseParty.SavedCharacter = character;
        characterLabel.text = PartyGames.CharacterNames[character];
        RefreshRings();
    }

    void SetColor(int c)
    {
        color = Mathf.Clamp(c, 0, MultiverseParty.Colors.Length - 1);
        MultiverseParty.SavedColor = color;
        for (int i = 0; i < colorButtons.Length; i++)
        {
            colorButtons[i].GetComponent<Image>().color = MultiverseParty.Colors[i];
            colorButtons[i].transform.localScale = Vector3.one * (i == color ? 1.2f : .85f);
            colorButtons[i].GetComponentInChildren<Text>().text = i == color ? "✓" : "";
        }
        RefreshRings();
    }

    void RefreshRings()
    {
        for (int i = 0; i < rings.Count; i++)
        {
            var c = i == character ? MultiverseParty.Colors[color] : new Color(.45f, .45f, .5f);
            MakeGlow(rings[i].gameObject, c);
            if (i != character) rings[i].sharedMaterial.SetColor("_EmissionColor", Color.black);
        }
    }

    void SetSize(int s)
    {
        size = s; PlayerPrefs.SetInt("Multiverse_Tamano", s);
        for (int i = 0; i < 3; i++)
        {
            var img = sizeButtons[i].GetComponent<Image>();
            img.color = 4 + i == s ? Gold : new Color(.55f, .62f, .8f);
            sizeButtons[i].transform.localScale = Vector3.one * (4 + i == s ? 1.08f : 1f);
        }
        sizeLabel.text = "Partida de " + s + " · faltan → bots";
    }

    void SaveName() => MultiverseParty.SavedName = nameField.text;

    async void Create()
    {
        if (busy) return; SaveName(); SetBusy(true, "Creando sala en línea…");
        bool ok = await MultiverseParty.StartOnline(true, "", size);
        if (!ok) SetBusy(false, MultiverseParty.LastStatus + "\nPuedes jugar sin conexión mientras tanto.");
    }

    void RefreshPublic()
    {
        bool pub = MultiverseParty.PublicRooms;
        publicButton.GetComponentInChildren<Text>().text = pub ? "SALA VISIBLE" : "SALA PRIVADA";
        publicButton.GetComponent<Image>().color = pub ? new Color(.5f, .9f, .55f) : new Color(.75f, .75f, .8f);
    }

    // ------------------------------------------------------------------ open rooms (2026-11-21)

    void OpenRooms()
    {
        if (busy || roomsPanel != null) return;
        if (MultiverseSettingsPanel.IsOpen) MultiverseSettingsPanel.Toggle(menuRoot);
        var dim = Rect("Salas abiertas", menuRoot, new Vector2(.5f, .5f), Vector2.zero, new Vector2(4000, 3000));
        dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .6f);
        roomsPanel = dim.gameObject;
        var card = Rect("Tarjeta", dim, new Vector2(.5f, .5f), Vector2.zero, new Vector2(720, 560));
        card.gameObject.AddComponent<Image>().color = new Color(.1f, .16f, .32f, .98f);
        Label("SALAS ABIERTAS", card, new Vector2(0, 240), new Vector2(660, 44), 32, Gold);
        roomsStatus = Label("Conectando…", card, new Vector2(0, 202), new Vector2(660, 26), 15, Cream);
        roomsList = Rect("Lista", card, new Vector2(.5f, .5f), new Vector2(0, -10), new Vector2(660, 380));
        Btn(card, "CERRAR", new Vector2(0, -235), new Vector2(220, 46), CloseRooms);

        browser = PartyRoomBrowser.Open();
        browser.Changed += FillRooms;
        FillRooms();
    }

    void FillRooms()
    {
        if (roomsPanel == null || browser == null) return;
        for (int i = roomsList.childCount - 1; i >= 0; i--) Destroy(roomsList.GetChild(i).gameObject);
        roomsStatus.text = browser.Status;
        int row = 0;
        foreach (var room in browser.Rooms)
        {
            if (row >= 6) break;
            var r = room;
            var line = Rect("Sala " + r.Code, roomsList, new Vector2(.5f, 1), new Vector2(0, -32 - row * 62), new Vector2(650, 56));
            line.gameObject.AddComponent<Image>().color = new Color(1, 1, 1, .07f);
            bool full = r.Players >= r.Size;
            string state = r.Playing ? "<color=#FFB060>jugando</color>" : "<color=#7CFC7C>en la sala de espera</color>";
            var t = Label("<color=#FFD54A>" + r.Code + "</color>  ·  sala de <b>" + r.Host + "</b>\n<size=13>" + r.Players + "/" + r.Size + " personas · " + state + "</size>",
                line, new Vector2(-80, 0), new Vector2(470, 52), 18, Cream);
            t.alignment = TextAnchor.MiddleLeft; t.supportRichText = true;
            var b = Btn(line, full ? "LLENA" : r.Playing ? "ENTRAR" : "UNIRSE", new Vector2(245, 0), new Vector2(140, 42), () => JoinRoom(r.Code));
            b.interactable = !full && r.Open;
            row++;
        }
        if (browser.Rooms.Count > 6) roomsStatus.text += " · se muestran las 6 primeras";
    }

    async void JoinRoom(string code)
    {
        if (busy) return;
        codeField.text = code;
        var b = browser; browser = null;
        CloseRooms();
        if (b != null) await b.Close();
        Join();
    }

    void CloseRooms()
    {
        if (browser != null) { var b = browser; browser = null; _ = b.Close(); }
        if (roomsPanel != null) Destroy(roomsPanel);
        roomsPanel = null; roomsList = null; roomsStatus = null;
    }

    void OnDestroy() { if (browser != null) { var b = browser; browser = null; _ = b.Close(); } }

    async void Join()
    {
        if (busy) return; SaveName();
        string code = codeField.text.Trim().ToUpperInvariant();
        if (code.Length < 4) { status.text = "Escribe el código que te pasó el anfitrión (4 a 12 caracteres)."; return; }
        SetBusy(true, "Entrando a la sala " + code + "…");
        bool ok = await MultiverseParty.StartOnline(false, code, size);
        if (!ok) SetBusy(false, MultiverseParty.LastStatus);
    }

    void Offline()
    {
        if (busy) return; SaveName();
        MultiverseParty.StartOffline(size);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void SetBusy(bool value, string message)
    {
        busy = value; if (status != null) status.text = message;
        if (createButton != null) createButton.interactable = !value;
        if (joinButton != null) joinButton.interactable = !value;
        if (offlineButton != null) offlineButton.interactable = !value;
    }

    // ------------------------------------------------------------------ map selection (2026-11-18)

    string ModeText()
    {
        var list = MultiverseParty.ChosenPlaylist; int goal = MultiverseParty.ChosenGoal;
        string pts = goal + (goal == 1 ? " punto" : " puntos");
        return list.Count > 0 ? "Mapas elegidos: " + list.Count + " en orden (sin votación) · gana quien llegue a " + pts + "."
                              : goal != MultiverseParty.DefaultWinScore ? "Votación normal · gana quien llegue a " + pts + "."
                              : "Modo normal: votación en la plaza · primero a " + pts + ".";
    }

    void RefreshMode()
    {
        int goal = MultiverseParty.ChosenGoal; var list = MultiverseParty.ChosenPlaylist;
        if (logoSub != null) logoSub.text = "Fiesta de minijuegos · primero a " + goal + (goal == 1 ? " punto" : " puntos") + " gana";
        if (rulesText != null) rulesText.text = "1. Crea una sala y comparte el código (o juega sin conexión).\n"
            + (list.Count > 0 ? "2. Se juegan los mapas que elegiste, en tu orden.\n" : "2. En la plaza votad uno de los 3 minijuegos al azar.\n")
            + "3. Ganar un minijuego da 1 punto. Al llegar a " + goal + ", ¡campeón!";
        if (mapsLabel != null) mapsLabel.text = list.Count > 0 ? "MAPAS: " + list.Count + " ELEGIDOS · " + goal + " PTS" : goal != MultiverseParty.DefaultWinScore ? "MAPAS: VOTACIÓN · " + goal + " PTS" : "SELECCIONAR MAPAS";
    }

    void OpenMaps()
    {
        if (busy || mapsPanel != null) return;
        if (MultiverseSettingsPanel.IsOpen) MultiverseSettingsPanel.Toggle(menuRoot);
        var dim = Rect("Seleccionar mapas", menuRoot, new Vector2(.5f, .5f), Vector2.zero, new Vector2(4000, 3000));
        dim.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .6f);
        mapsPanel = dim.gameObject;
        var card = Rect("Tarjeta", dim, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1000, 620));
        card.gameObject.AddComponent<Image>().color = new Color(.1f, .16f, .32f, .98f);
        Label("SELECCIONAR MAPAS", card, new Vector2(0, 275), new Vector2(900, 44), 32, Gold);
        Label("Haz clic en los minijuegos en el orden en que queréis jugarlos (se pueden repetir). Si la lista se acaba y nadie ha ganado, vuelve a empezar.",
            card, new Vector2(0, 238), new Vector2(940, 26), 14, Cream);

        // Games (left).
        Label("MINIJUEGOS", card, new Vector2(-250, 205), new Vector2(480, 28), 18, Gold);
        for (int i = 0; i < PartyGames.Count; i++)
        {
            int g = i; var info = PartyGames.All[g];
            var b = Btn(card, info.Title, new Vector2(-408 + (g % 3) * 158, 168 - (g / 3) * 50), new Vector2(150, 42), () => AddMap(g));   // 3 columns: room for 15 games
            b.GetComponent<Image>().color = Color.Lerp(info.Color, Color.white, .25f);
            var t = b.GetComponentInChildren<Text>(); t.fontSize = 14; t.resizeTextForBestFit = true; t.resizeTextMinSize = 9; t.resizeTextMaxSize = 14;
        }
        // Points (left, bottom).
        Label("PUNTOS PARA GANAR", card, new Vector2(-250, -150), new Vector2(480, 28), 18, Gold);
        Btn(card, "−", new Vector2(-370, -195), new Vector2(60, 46), () => SetGoal(MultiverseParty.ChosenGoal - 1));
        goalText = Label("", card, new Vector2(-250, -195), new Vector2(160, 46), 28, Cream);
        Btn(card, "+", new Vector2(-130, -195), new Vector2(60, 46), () => SetGoal(MultiverseParty.ChosenGoal + 1));

        // Order (right).
        Label("ORDEN DE JUEGO", card, new Vector2(255, 205), new Vector2(440, 28), 18, Gold);
        var box = Rect("Lista", card, new Vector2(.5f, .5f), new Vector2(255, 30), new Vector2(440, 320));
        box.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .3f);
        orderText = Label("", box, Vector2.zero, new Vector2(420, 304), 14, Cream);
        orderText.alignment = TextAnchor.UpperLeft; orderText.font = MultiverseUI.BodyFont;
        orderText.resizeTextForBestFit = true; orderText.resizeTextMinSize = 9; orderText.resizeTextMaxSize = 16;
        Btn(card, "QUITAR ÚLTIMO", new Vector2(145, -150), new Vector2(210, 38), () => { var l = MultiverseParty.ChosenPlaylist; if (l.Count > 0) l.RemoveAt(l.Count - 1); RefreshMaps(); }).GetComponentInChildren<Text>().fontSize = 17;
        Btn(card, "BORRAR LISTA", new Vector2(365, -150), new Vector2(210, 38), () => { MultiverseParty.ChosenPlaylist.Clear(); RefreshMaps(); }).GetComponentInChildren<Text>().fontSize = 17;
        Btn(card, "TODOS EN ORDEN", new Vector2(145, -195), new Vector2(210, 38), () => { var l = MultiverseParty.ChosenPlaylist; l.Clear(); for (int g = 0; g < PartyGames.Count; g++) l.Add(g); RefreshMaps(); }).GetComponentInChildren<Text>().fontSize = 17;
        Btn(card, "MEZCLAR", new Vector2(365, -195), new Vector2(210, 38), () => { var l = MultiverseParty.ChosenPlaylist; for (int i = l.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (l[i], l[j]) = (l[j], l[i]); } RefreshMaps(); }).GetComponentInChildren<Text>().fontSize = 17;

        // Bottom.
        var normal = Btn(card, "MODO NORMAL", new Vector2(-150, -265), new Vector2(260, 50), () => { MultiverseParty.ChosenPlaylist.Clear(); MultiverseParty.ChosenGoal = MultiverseParty.DefaultWinScore; CloseMaps(); });
        normal.GetComponent<Image>().color = new Color(.55f, .62f, .8f);
        Btn(card, "LISTO", new Vector2(150, -265), new Vector2(260, 50), CloseMaps).GetComponent<Image>().color = new Color(.5f, 1f, .55f);
        MultiverseUI.Skin(mapsPanel, MultiverseUI.Mode.Hub);
        RefreshMaps();
    }

    void AddMap(int game)
    {
        var l = MultiverseParty.ChosenPlaylist;
        if (l.Count >= MultiverseParty.MaxPlaylist) { status.text = "Como mucho " + MultiverseParty.MaxPlaylist + " mapas en la lista."; return; }
        l.Add(game); RefreshMaps();
    }

    void SetGoal(int goal) { MultiverseParty.ChosenGoal = Mathf.Clamp(goal, MultiverseParty.MinGoal, MultiverseParty.MaxGoal); RefreshMaps(); }

    void RefreshMaps()
    {
        var l = MultiverseParty.ChosenPlaylist;
        if (orderText != null)
        {
            if (l.Count == 0) orderText.text = "\n  (vacía)\n\n  Sin lista se juega normal: en la plaza\n  se vota uno de 3 minijuegos al azar.";
            else
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < l.Count; i++)
                {
                    sb.Append(i + 1).Append(". ").Append(PartyGames.All[l[i]].Title).Append('\n');
                }
                orderText.text = sb.ToString();
            }
        }
        if (goalText != null) goalText.text = MultiverseParty.ChosenGoal.ToString();
        RefreshMode();
    }

    void CloseMaps()
    {
        if (mapsPanel != null) Destroy(mapsPanel);
        mapsPanel = null; orderText = null; goalText = null;
        RefreshMode();
        if (status != null && !busy) status.text = ModeText();
    }

    // ------------------------------------------------------------------ helpers
    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }

    Text Label(string value, Transform parent, Vector2 pos, Vector2 size, int pt, Color color)
    {
        var t = Rect("Texto", parent, new Vector2(.5f, .5f), pos, size).gameObject.AddComponent<Text>();
        t.font = MultiverseUI.DisplayFont; t.text = value; t.fontSize = pt; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
        return t;
    }

    Button Btn(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var r = Rect(label, parent, new Vector2(.5f, .5f), pos, size);
        r.gameObject.AddComponent<Image>().color = Gold;
        var b = r.gameObject.AddComponent<Button>(); b.onClick.AddListener(action);
        Label(label, r, Vector2.zero, size - new Vector2(16, 6), 22, Ink);
        return b;
    }

    InputField Field(Transform parent, Vector2 pos, string placeholder, string value, int limit)
    {
        var r = Rect("Campo", parent, new Vector2(.5f, .5f), pos, new Vector2(380, 52));
        r.gameObject.AddComponent<Image>().color = Color.white;
        var field = r.gameObject.AddComponent<InputField>();
        var text = Label("", r, Vector2.zero, new Vector2(350, 44), 22, Ink); text.supportRichText = false;
        var ph = Label(placeholder, r, Vector2.zero, new Vector2(350, 44), 20, new Color(.4f, .42f, .5f)); ph.fontStyle = FontStyle.Italic;
        field.textComponent = text; field.placeholder = ph; field.characterLimit = limit; field.text = value;
        return field;
    }
}

/// <summary>Plays the party menu music if any of the existing generated tracks is available.</summary>
public sealed class MultiverseMusicStub : MonoBehaviour
{
    void Start()
    {
        var clip = Resources.Load<AudioClip>("PirateAudio/v2/music_menu") ?? Resources.Load<AudioClip>("PirateAudio/v2/music") ?? Resources.Load<AudioClip>("PirateAudio/music");
        if (clip == null) return;
        var src = gameObject.AddComponent<AudioSource>(); src.clip = clip; src.loop = true; src.volume = .35f; src.spatialBlend = 0; src.Play();
    }
}
