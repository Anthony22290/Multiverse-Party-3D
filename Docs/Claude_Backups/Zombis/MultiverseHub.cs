using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>A small walkable island for choosing a minigame.</summary>
public sealed class MultiverseHub : MonoBehaviour
{
    public GameObject pirate, ship, shark, barrel, chest, palm, rock, zombie, monster;
    public Material wood, darkWood, brass, rope, cloth, foam, iron, water, atlas, zombieAtlas, monsterAtlas;
    private readonly Vector3[] gates = { new Vector3(-15, 1, 14), new Vector3(0, 1, 21), new Vector3(15, 1, 14), new Vector3(24, 1, -10), new Vector3(-24, 1, -10), new Vector3(15.5f, 1, 4) };
    private CharacterController walker;
    private Transform orbit;
    private CinemachineThirdPersonFollow cameraFollow;
    private Transform avatar;
    private Animator animator;
    private int selected;
    private float yaw = 0, pitch = 12, cameraDistance = 14;
    private float verticalSpeed;
    private readonly RaycastHit[] cameraObstacles=new RaycastHit[32];
    private float safeCameraDistance=14f;
    private string currentAnimation;
    private UnityEngine.UI.Text title, description, hint;
    private UnityEngine.UI.Button playButton;
    private Color ink = new Color(.07f, .1f, .14f), cream = new Color(.97f, .93f, .83f), gold = new Color(1f, .72f, .29f);
    private Font font;

    private void Start()
    {
        Application.targetFrameRate = 60;
        if (transform.Find("Plaza de las partidas")==null) BuildWorld();
        if (transform.Find("Zona de práctica")==null) BuildPracticeArea();
        if (transform.Find("Portal de escape")==null) BuildEscapePortal();
        BuildWalker(); BuildCamera(); BuildUI(); Select(0);
    }

    private GameObject Box(string label, Vector3 at, Vector3 size, Material material, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = label;
        go.transform.SetParent(transform, false); go.transform.position = at; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!solid)
        {
            if(Application.isPlaying) Destroy(go.GetComponent<Collider>());
            else DestroyImmediate(go.GetComponent<Collider>());
        }
        return go;
    }

    public void BuildWorld()
    {
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.34f,.67f,.76f);
        RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 125;
        RenderSettings.ambientLight = new Color(.72f,.76f,.75f);
        var sun = new GameObject("Sol de la plaza").AddComponent<Light>(); sun.type = LightType.Directional;
        sun.transform.SetParent(transform,false);
        sun.transform.rotation = Quaternion.Euler(50,-35,0); sun.intensity = 1.25f; sun.shadows = LightShadows.Soft;
        Box("Mar", new Vector3(0,-1.9f,13), new Vector3(160,.2f,150), water, false);
        Box("Plaza de las partidas", new Vector3(0,0,0), new Vector3(18,1.8f,18), wood);
        Box("Franja central", new Vector3(0,.92f,0), new Vector3(3,.06f,17), darkWood, false);
        for (int i=0;i<3;i++)
        {
            Vector3 gate = gates[i]; Vector3 start = Vector3.zero;
            Vector3 midpoint = (start + gate)*.5f;
            var bridge = Box("Pasarela " + i, new Vector3(midpoint.x,.18f,midpoint.z),
                new Vector3(3.2f,1.45f,Vector3.Distance(start,gate)+2f), wood);
            bridge.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(gate.x,gate.z)*Mathf.Rad2Deg,0);
            Box("Isla " + i, new Vector3(gate.x,.05f,gate.z), new Vector3(12,1.7f,10), darkWood);
            Box("Plataforma del portal " + i, gate+Vector3.down*.06f, new Vector3(8,.35f,7), wood);
            Box("Dintel " + i, gate+Vector3.up*4.5f, new Vector3(5.8f,.65f,.75f), i==0 ? brass : iron);
            for (int side=-1;side<=1;side+=2)
                Box("Columna " + i + " " + side, gate+new Vector3(side*2.7f,2.2f,0), new Vector3(.7f,4.7f,.7f), i==0 ? brass : iron);
            Box("Luz del portal " + i, gate+new Vector3(0,2.4f,.1f),new Vector3(4.3f,3.4f,.12f),i==0?foam:cloth,false);
            Box("Frente de isla " + i, gate+new Vector3(0,-.35f,-4.8f),new Vector3(12,.8f,.4f),brass);
            Box("Cartel " + i, gate+new Vector3(0,5.28f,-.2f),new Vector3(5.2f,.9f,.35f),wood);
            var sign=new GameObject("Nombre del portal " + i,typeof(TextMesh));
            sign.transform.SetParent(transform,false);
            sign.transform.SetPositionAndRotation(gate+new Vector3(0,5.28f,-.4f),Quaternion.identity);
            var lettering=sign.GetComponent<TextMesh>();
            lettering.text=i==0?"PIRATAS":i==1?"ZOMBIS":i==2?"CRIATURAS":"CARRERAS";
            lettering.fontSize=96;lettering.characterSize=.085f;lettering.anchor=TextAnchor.MiddleCenter;
            lettering.color=new Color(.98f,.94f,.82f);
        }
        for (int side=-1; side<=1;side+=2)
        {
            Decor(palm,new Vector3(side*6.6f,1.3f,-5.8f),4f,side==1?40:-30);
            Decor(rock,new Vector3(side*8f,1.15f,4),2.4f,side==1?80:-60);
            Decor(barrel,new Vector3(-15+side*3.5f,1.1f,10.5f),1.15f,0);
            Decor(chest,new Vector3(-15+side*3.4f,1.1f,17),1.4f,side*30);
        }
        Decor(ship,new Vector3(-15,-.5f,24),9f,180);
        Decor(shark,new Vector3(-22,-1.45f,13),3f,80);
        Decor(zombie,new Vector3(-2.5f,1.4f,19),2.6f,145,zombieAtlas);
        Decor(zombie,new Vector3(2.5f,1.4f,23),2.6f,210,zombieAtlas);
        Decor(monster,new Vector3(19,1.4f,10.5f),4.7f,180,monsterAtlas);
        for (int i=0;i<4;i++) Decor(rock,new Vector3(15+(i%2==0?-3:3),1.1f,10+i*2.1f),1.4f,30*i);
        Physics.SyncTransforms();
    }

    /// <summary>Portal to the cooperative escape room (Torre del Multiverso), east of the central plaza, facing the plaza.</summary>
    public void BuildEscapePortal()
    {
        if (transform.Find("Portal de escape") != null) return;
        Vector3 gate = gates[5];
        float ground = Physics.Raycast(new Vector3(gate.x, 30f, gate.z), Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : .9f;
        var root = new GameObject("Portal de escape").transform;
        root.SetParent(transform, false);
        root.SetPositionAndRotation(new Vector3(gate.x, ground, gate.z), Quaternion.Euler(0, 90, 0));
        var violet = new Material(iron != null ? iron : wood) { name = "Portal violeta" };
        violet.SetColor("_BaseColor", new Color(.55f, .35f, .95f));
        var glow = new Material(foam != null ? foam : wood) { name = "Velo del portal de escape" };
        glow.SetColor("_BaseColor", new Color(.78f, .6f, 1f));
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(.55f, .35f, 1f) * 1.6f);
        GameObject Piece(string name, Vector3 local, Vector3 size, Material material, bool solid = true)
        {
            var piece = Box(name, Vector3.zero, size, material, solid);
            piece.transform.SetParent(root, false);
            piece.transform.localPosition = local; piece.transform.localRotation = Quaternion.identity;
            return piece;
        }
        Piece("Plataforma del portal de escape", new Vector3(0, .09f, 0), new Vector3(7f, .18f, 6f), darkWood);
        Piece("Dintel de escape", new Vector3(0, 4.7f, 0), new Vector3(5.8f, .65f, .75f), violet);
        for (int side = -1; side <= 1; side += 2)
        {
            Piece("Columna de escape " + side, new Vector3(side * 2.7f, 2.35f, 0), new Vector3(.7f, 4.7f, .7f), violet);
            Piece("Cristal de escape " + side, new Vector3(side * 2.7f, 5.25f, 0), new Vector3(.5f, .5f, .5f), glow, false).transform.localRotation = Quaternion.Euler(45, 45, 0);
        }
        Piece("Luz del portal de escape", new Vector3(0, 2.45f, .1f), new Vector3(4.3f, 3.4f, .12f), glow, false);
        Piece("Cartel de escape", new Vector3(0, 5.45f, -.2f), new Vector3(5.2f, .9f, .35f), wood, false);
        EscapeKit.Text(root, "ESCAPE", new Vector3(0, 5.45f, -.4f), Vector3.zero, .085f, new Color(.98f, .94f, .82f));
        EscapeKit.Text(root, "TORRE DEL MULTIVERSO · 10 SALAS", new Vector3(0, .2f, -2.4f), new Vector3(90, 0, 0), .03f, new Color(.98f, .94f, .82f));
        Physics.SyncTransforms();
    }

    /// <summary>A small optional physics playground beside the minigame plaza.</summary>
    public void BuildPracticeArea()
    {
        if (transform.Find("Zona de práctica") != null) return;
        var area = new GameObject("Zona de práctica").transform;
        area.SetParent(transform, false);
        GameObject Piece(string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        {
            var piece = Box(name, position, size, material, solid);
            piece.transform.SetParent(area, true);
            return piece;
        }
        Piece("Muelle de práctica", new Vector3(0, 0, -14), new Vector3(12, 1.8f, 10), wood);
        Piece("Banda de entrada", new Vector3(0, .92f, -10.2f), new Vector3(2.6f, .05f, 2.2f), brass, false);
        // Low, broad steps give the hub walker an optional route around the movable blocks.
        for (int i = 0; i < 3; i++)
            Piece("Escalón " + (i + 1), new Vector3(-3.8f, 1.05f + i * .25f, -12.7f - i * .65f),
                new Vector3(2.1f, .3f + i * .5f, .8f), darkWood);
        Piece("Mirador de práctica", new Vector3(-3.8f, 1.65f, -16.6f), new Vector3(2.1f, 1.5f, 3.4f), darkWood);
        for (int side = -1; side <= 1; side += 2)
        {
            Piece("Poste del muelle " + side, new Vector3(side * 5.55f, 1.65f, -18.4f), new Vector3(.5f, 1.5f, .5f), brass);
            Piece("Tope lateral " + side, new Vector3(side * 5.8f, 1.2f, -15f), new Vector3(.35f, .6f, 7.6f), darkWood);
        }
        Piece("Cartel de práctica", new Vector3(0, 3.15f, -18.65f), new Vector3(4.8f, 1f, .25f), darkWood, false);
        var text = new GameObject("Letrero: zona de práctica", typeof(TextMesh));
        text.transform.SetParent(area, false);
        text.transform.SetPositionAndRotation(new Vector3(0, 3.15f, -18.48f), Quaternion.Euler(0, 180, 0));
        var letters = text.GetComponent<TextMesh>();
        letters.text = "ZONA DE PRÁCTICA"; letters.fontSize = 72; letters.characterSize = .065f;
        letters.anchor = TextAnchor.MiddleCenter; letters.alignment = TextAlignment.Center;
        letters.color = new Color(.98f, .94f, .82f);
        for (int i = 0; i < 2; i++)
        {
            var block = Piece("Cajón móvil " + (i + 1), new Vector3(1.35f + i * 2f, 1.48f, -15.6f),
                Vector3.one * 1.1f, i == 0 ? darkWood : wood);
            var body = block.AddComponent<Rigidbody>();
            body.mass = 10f; body.linearDamping = .45f; body.angularDamping = .8f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            block.AddComponent<HubPracticeReset>();
        }
    }

    private void Decor(GameObject prefab, Vector3 at, float height, float angle, Material material=null)
    {
        if (prefab==null) return;
        var root=new GameObject("Decorado " + prefab.name); root.transform.SetParent(transform,false);
        root.transform.SetPositionAndRotation(at,Quaternion.Euler(0,angle,0));
        PirateArenaVisuals.AddModel(prefab,root.transform,height,material!=null?material:atlas,false);
        foreach(var collider in root.GetComponentsInChildren<Collider>()) collider.enabled=true;
        foreach(var mesh in root.GetComponentsInChildren<MeshFilter>())
            if(mesh.sharedMesh!=null && mesh.GetComponent<Collider>()==null) mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
        if(root.GetComponentsInChildren<Collider>().Length==0)
        {
            var bounds=PirateArenaVisuals.BoundsOf(root);
            var collider=root.AddComponent<BoxCollider>();
            collider.center=root.transform.InverseTransformPoint(bounds.center);collider.size=bounds.size;
        }
    }

    private void BuildWalker()
    {
        var go=new GameObject("Explorador",typeof(CharacterController)); go.transform.position=new Vector3(0,1.05f,-5);
        walker=go.GetComponent<CharacterController>(); walker.height=1.75f; walker.radius=.38f; walker.center=new Vector3(0,.88f,0);
        walker.stepOffset=.3f; walker.skinWidth=.035f; walker.minMoveDistance=0f; walker.slopeLimit=48f;
        go.AddComponent<HubPhysicsPush>();
        var model=Resources.Load<GameObject>("PirateCharacters/Characters_Anne");
        var visual=PirateArenaVisuals.AddModel(model,go.transform,1.7f,atlas,false);
        avatar=visual!=null?visual.transform:go.transform;
        animator=visual!=null?visual.GetComponentInChildren<Animator>():null;
        if(animator!=null) { animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
    }

    private void BuildCamera()
    {
        Camera camera=Camera.main;
        if(camera==null) { var cameraGo=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)); cameraGo.tag="MainCamera"; camera=cameraGo.GetComponent<Camera>(); }
        camera.fieldOfView=55; camera.nearClipPlane=.15f; camera.farClipPlane=180;
        camera.backgroundColor=new Color(.35f,.71f,.79f); camera.clearFlags=CameraClearFlags.SolidColor;
        camera.gameObject.AddComponent<CinemachineBrain>();
        orbit=new GameObject("Hub Camera Target").transform;
        var rig=new GameObject("Hub Follow Camera"); var vcam=rig.AddComponent<CinemachineCamera>();
        vcam.Follow=orbit; vcam.LookAt=orbit;
        cameraFollow=rig.AddComponent<CinemachineThirdPersonFollow>(); cameraFollow.CameraDistance=cameraDistance;
        cameraFollow.Damping=new Vector3(.12f,.12f,.12f);
        orbit.position=walker.transform.position+Vector3.up*1.2f;
        orbit.rotation=Quaternion.Euler(pitch,yaw,0);
    }

    private void Update()
    {
        Keyboard keys=Keyboard.current; Mouse mouse=Mouse.current;
        if(keys==null) return;
        if(mouse!=null && (EventSystem.current==null || !EventSystem.current.IsPointerOverGameObject()) && mouse.rightButton.isPressed)
        { var delta=mouse.delta.ReadValue(); yaw+=delta.x*.16f; pitch=Mathf.Clamp(pitch-delta.y*.12f,8,65); }
        if(mouse!=null) cameraDistance=Mathf.Clamp(cameraDistance-mouse.scroll.ReadValue().y*.008f,6,16);
        Vector2 axis=new Vector2((keys.dKey.isPressed?1:0)-(keys.aKey.isPressed?1:0),
            (keys.wKey.isPressed?1:0)-(keys.sKey.isPressed?1:0));
        Vector3 move=Quaternion.Euler(0,yaw,0)*new Vector3(axis.x,0,axis.y).normalized;
        MoveExplorer(move, Time.deltaTime, keys.spaceKey.wasPressedThisFrame);
        if(move.sqrMagnitude>.05f) walker.transform.rotation=Quaternion.Slerp(walker.transform.rotation,
            Quaternion.LookRotation(move),Time.deltaTime*12);
        if(animator!=null)
        {
            string clip=move.sqrMagnitude>.05f?"Base Layer.Walk":"Base Layer.Idle";
            if(clip!=currentAnimation) { animator.CrossFadeInFixedTime(clip,.14f); currentAnimation=clip; }
        }
        if(keys.digit1Key.wasPressedThisFrame) Select(0);
        if(keys.digit2Key.wasPressedThisFrame) Select(1);
        if(keys.digit3Key.wasPressedThisFrame) Select(2);
        if(keys.digit4Key.wasPressedThisFrame) Select(3);
        if(keys.digit5Key.wasPressedThisFrame) Select(4);
        if(keys.digit6Key.wasPressedThisFrame) Select(5);
        for(int i=0;i<gates.Length;i++)
            if(Vector3.Distance(walker.transform.position,gates[i])<4.5f && selected!=i) Select(i);
        if(keys.eKey.wasPressedThisFrame && Vector3.Distance(walker.transform.position,gates[selected])<6f) PlaySelected();
        if(keys.escapeKey.wasPressedThisFrame) Select(0);
        if(walker.transform.position.y < .2f) { verticalSpeed=0; walker.enabled=false; walker.transform.position=new Vector3(0,1.05f,-5); walker.enabled=true; }
        hint.text=(selected==0 || selected==3 || selected==4 || selected==5) && Vector3.Distance(walker.transform.position,gates[selected])<6f?"E  ·  ENTRAR AL MINIJUEGO":"WASD MOVER  ·  ESPACIO SALTAR  ·  RATÓN DERECHO MIRAR  ·  1–6 ELEGIR";
    }

    private void LateUpdate()
    {
        if(orbit==null||walker==null)return;
        orbit.SetPositionAndRotation(walker.transform.position+Vector3.up*1.25f,Quaternion.Euler(pitch,yaw,0));
        Vector3 back=Quaternion.Euler(pitch,yaw,0)*Vector3.back;
        float allowed=cameraDistance;
        int count=Physics.SphereCastNonAlloc(orbit.position,.22f,back,cameraObstacles,cameraDistance,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
            if(!cameraObstacles[i].transform.IsChildOf(walker.transform))
                allowed=Mathf.Min(allowed,Mathf.Max(1f,cameraObstacles[i].distance-.15f));
        safeCameraDistance=allowed<safeCameraDistance?allowed:Mathf.MoveTowards(safeCameraDistance,allowed,Time.deltaTime*8f);
        cameraFollow.CameraDistance=safeCameraDistance;
    }

    private void Select(int value)
    {
        selected=value;
        string[] names={"ARENA DEL CAOS","INVASIÓN ZOMBI","CRIATURAS DEL MULTIVERSO","CARRERAS LOCAS","CÓDIGO MORTAL","SALA DE ESCAPE"};
        title.text=names[value];
        description.text=value==0?"Barco pirata · Empuja, agarra y evita el agua\n1–6 jugadores · Bots en plazas libres":
            value==4?"Plaza Vértigo · Lee códigos y busca una terminal\n4 minutos · 1 jugador y 5 bots":
            value==3?"Costa del Caos · Derrapa y usa objetos\n3 vueltas · 1 jugador y 3 rivales":
            value==5?"Torre del Multiverso · 10 salas cooperativas\n4–6 jugadores · Tú y compañeros IA":"PRÓXIMAMENTE · Zona de exhibición";
        playButton.gameObject.SetActive(value==0 || value==3 || value==4 || value==5);
        playButton.GetComponentInChildren<UnityEngine.UI.Text>().text=value==3?"¡CORRER!":value==5?"¡ESCAPAR!":"¡JUGAR!";
    }

    public void MoveExplorer(Vector3 direction, float deltaTime, bool jump)
    {
        if(walker==null || !walker.enabled) return;
        float remaining=Mathf.Min(deltaTime,.2f);
        if(walker.isGrounded && jump) verticalSpeed=7f;
        while(remaining>0f)
        {
            float step=Mathf.Min(remaining,1f/60f); remaining-=step;
            if(walker.isGrounded && verticalSpeed<0f) verticalSpeed=-2f;
            verticalSpeed=Mathf.Max(-25f,verticalSpeed-20f*step);
            var flags=walker.Move((Vector3.ClampMagnitude(direction,1f)*5f+Vector3.up*verticalSpeed)*step);
            if((flags & CollisionFlags.Above)!=0 && verticalSpeed>0f) verticalSpeed=0f;
            if((flags & CollisionFlags.Below)!=0 && verticalSpeed<0f) verticalSpeed=-2f;
        }
    }

    private void PlaySelected()
    {
        string scene=selected==0?"ArenaDelCaos":selected==3?"CarrerasLocas":selected==4?"CodigoMortal":selected==5?"SalaDeEscape":null;
        if(scene!=null && Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
    }

    public void PlayPirate() => SceneManager.LoadScene("ArenaDelCaos");

    private RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private UnityEngine.UI.Text Text(string value,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int point,Color color)
    {
        var t=Rect(value,parent,anchor,pos,size).gameObject.AddComponent<UnityEngine.UI.Text>();
        t.font=font;t.fontSize=point;t.color=color;t.text=value;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
    }
    private UnityEngine.UI.Button Button(string label,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var go=Rect(label,parent,anchor,pos,size).gameObject;
        go.AddComponent<UnityEngine.UI.Image>().color=gold;
        var button=go.AddComponent<UnityEngine.UI.Button>();button.onClick.AddListener(action);
        Text(label,go.transform,new Vector2(.5f,.5f),Vector2.zero,size,23,ink);return button;
    }
    private void BuildUI()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root=new GameObject("Hub UI",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1280,720);
        if(EventSystem.current==null)new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var header=Rect("Marca",root.transform,new Vector2(0,1),new Vector2(250,-50),new Vector2(460,68));
        header.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(ink.r,ink.g,ink.b,.92f);
        Text("MULTIVERSE PARTY 3D",header,new Vector2(.5f,.5f),Vector2.zero,new Vector2(440,60),26,cream);
        var bottom=Rect("Tarjeta del minijuego",root.transform,new Vector2(1,0),new Vector2(-275,120),new Vector2(520,204));
        bottom.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(ink.r,ink.g,ink.b,.95f);
        title=Text("",bottom,new Vector2(.5f,.5f),new Vector2(0,66),new Vector2(490,42),26,gold);
        description=Text("",bottom,new Vector2(.5f,.5f),new Vector2(0,6),new Vector2(480,74),17,cream);
        playButton=Button("¡JUGAR!",bottom,new Vector2(.5f,.5f),new Vector2(0,-66),new Vector2(210,46),PlaySelected);
        for(int i=0;i<6;i++) { int choice=i; Button((i+1)+"  "+(i==0?"PIRATAS":i==1?"ZOMBIS":i==2?"CRIATURAS":i==3?"CARRERAS":i==4?"CÓDIGO MORTAL":"ESCAPE"),root.transform,
            new Vector2(0,0),new Vector2(135,336-i*54),new Vector2(220,46),()=>Select(choice)); }
        hint=Text("",root.transform,new Vector2(0,0),new Vector2(340,22),new Vector2(650,32),14,cream);
        MultiverseUI.Skin(root,MultiverseUI.Mode.Hub);
    }
}


