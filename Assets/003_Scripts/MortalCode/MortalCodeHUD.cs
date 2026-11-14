using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public sealed class MortalCodeHUD : MonoBehaviour
{
    public MortalCodeDirector Director;
    public bool TerminalOpen => terminalPanel!=null && terminalPanel.activeSelf;
    public bool MenuOpen => menu!=null && menu.activeSelf;
    Font font;
    Transform root;
    UnityEngine.UI.Text status,notice,hint,entry,terminalTitle,response,results,protection;
    GameObject terminalPanel,menu,resultPanel;
    MortalCodeTerminal terminal;
    string digits="";
    UnityEngine.UI.Text notebook,feed; MortalCodeActor flashTarget; float flashUntil;
    public void Flash(MortalCodeActor target){flashTarget=target;flashUntil=Time.time+2.5f;}
    Keyboard subscribed;
    readonly Dictionary<MortalCodeActor,UnityEngine.UI.Text> labels=new();
    readonly Color ink=new(.055f,.075f,.13f,.96f), cyan=new(.35f,1, .86f), paper=new(.94f,.97f,1);
    RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=anchor;r.anchoredPosition=pos;r.sizeDelta=size;return r;
    }
    UnityEngine.UI.Text Text(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int point,Color color)
    {
        var t=Rect(name,parent,anchor,pos,size).gameObject.AddComponent<UnityEngine.UI.Text>();t.font=font;t.fontSize=point;t.color=color;
        t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;t.text=name;return t;
    }
    GameObject Panel(string name,Vector2 anchor,Vector2 pos,Vector2 size)
    {var g=Rect(name,root,anchor,pos,size).gameObject;g.AddComponent<UnityEngine.UI.Image>().color=ink;return g;}
    void Button(string text,Transform parent,Vector2 pos,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var r=Rect(text,parent,new(.5f,.5f),pos,size);r.gameObject.AddComponent<UnityEngine.UI.Image>().color=new(.18f,.33f,.4f);
        r.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(action);Text(text,r,new(.5f,.5f),Vector2.zero,size,23,paper);
    }
    void Start()
    {
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go=new GameObject("Código Mortal HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));root=go.transform;
        go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new(1280,720);scaler.matchWidthOrHeight=.5f;
        if(EventSystem.current==null)new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var top=Panel("Estado",new(.5f,1),new(0,-43),new(850,68));
        status=Text("",top.transform,new(.5f,.5f),Vector2.zero,new(830,60),24,paper);
        protection=Text("",root,new(1,1),new(-120,-100),new(230,58),18,cyan);
        notice=Text("",root,new(.5f,1),new(0,-117),new(850,62),22,cyan);
        var book=Panel("Libreta",new(0,.5f),new(135,30),new(250,270));
        Text("LIBRETA DE CÓDIGOS",book.transform,new(.5f,1),new(0,-22),new(230,30),18,cyan);
        notebook=Text("",book.transform,new(.5f,.5f),new(0,-15),new(225,215),19,paper);notebook.alignment=TextAnchor.UpperLeft;
        feed=Text("",root,new(1,1),new(-150,-190),new(280,120),16,paper);feed.alignment=TextAnchor.UpperRight;
        var bottom=Panel("Controles",new(.5f,0),new(0,37),new(1180,62));
        hint=Text("",bottom.transform,new(.5f,.5f),Vector2.zero,new(1160,58),17,paper);
        foreach(var a in Director.Actors)if(!a.IsHuman)
        {var t=Text("",root,new(.5f,.5f),Vector2.zero,new(180,65),25,cyan);t.gameObject.SetActive(false);labels[a]=t;}
        terminalPanel=Panel("Terminal",new(.5f,.5f),Vector2.zero,new(490,550));
        terminalTitle=Text("TERMINAL",terminalPanel.transform,new(.5f,.5f),new(0,225),new(460,50),25,cyan);
        entry=Text("_ _ _ _",terminalPanel.transform,new(.5f,.5f),new(0,162),new(450,70),44,paper);
        response=Text("Escribe los 4 números que viste",terminalPanel.transform,new(.5f,.5f),new(0,105),new(460,45),17,paper);
        for(int i=0;i<10;i++){int n=i;int index=i==0?10:i-1;Button(i.ToString(),terminalPanel.transform,new((index%3-1)*105,45-index/3*61),new(94,53),()=>Append((char)('0'+n)));}
        Button("BORRAR",terminalPanel.transform,new(-145,-139),new(140,53),Backspace);
        Button("ENVIAR",terminalPanel.transform,new(145,-139),new(140,53),Submit);
        Button("SALIR · E / ESC",terminalPanel.transform,new(0,-220),new(300,48),CloseTerminal);
        terminalPanel.SetActive(false);
        menu=Panel("Menú",new(.5f,.5f),Vector2.zero,new(470,330));
        Text("MENÚ · LA RONDA CONTINÚA",menu.transform,new(.5f,.5f),new(0,110),new(450,70),23,cyan);
        Button("CONTINUAR",menu.transform,new(0,35),new(330,50),()=>menu.SetActive(false));
        Button("REINICIAR",menu.transform,new(0,-35),new(330,50),Director.Restart);
        Button("MUNDO DE INICIO",menu.transform,new(0,-105),new(330,50),Director.ReturnToHub);menu.SetActive(false);
        resultPanel=Panel("Resultados",new(.5f,.5f),Vector2.zero,new(620,470));
        results=Text("",resultPanel.transform,new(.5f,.5f),new(0,75),new(590,290),26,cyan);
        Button("REVANCHA · ENTER",resultPanel.transform,new(0,-125),new(340,52),Director.Restart);
        Button("MUNDO DE INICIO",resultPanel.transform,new(0,-190),new(340,52),Director.ReturnToHub);resultPanel.SetActive(false);
        MultiverseUI.Skin(go,MultiverseUI.Mode.Mortal);
        MultiverseParty.StripMenus();
    }
    void Update()
    {
        if(root==null)return;
        if(subscribed!=Keyboard.current){if(subscribed!=null)subscribed.onTextInput-=OnText;subscribed=Keyboard.current;if(subscribed!=null)subscribed.onTextInput+=OnText;}
        var k=Keyboard.current;var p=Director.Human;
        status.text="CÓDIGO MORTAL  /  PLAZA VÉRTIGO     "+Mathf.FloorToInt(Director.Remaining/60).ToString("00")+":"+((int)Director.Remaining%60).ToString("00")+"     VIVOS "+Director.AliveCount+"/"+Director.Actors.Count+(Director.SuddenDeath?"     ¡MUERTE SÚBITA!":"");
        status.color=Director.SuddenDeath?Color.Lerp(paper,new Color(1,.35f,.35f),Mathf.PingPong(Time.time*2,1)):paper;
        // Notebook of codes the player has read (and the scanner revealed).
        var lines=new System.Text.StringBuilder();
        foreach(var (who,code) in Director.KnownCodes(p))
        {
            string row=who.DisplayName.PadRight(7)+"  "+(who.Alive?code:"— — — —");
            if(!who.Alive)row="<color=#7f8a99>"+row+"  ✝</color>";
            else if(who==flashTarget && Time.time<flashUntil)row="<color=#FFE066>"+row+" ★</color>";
            lines.AppendLine(row);
        }
        notebook.text=lines.Length>0?lines.ToString():"<color=#9fb3c8>Mira a un rival de cerca para leer su código. Los escáneres lo revelan aunque haya paredes.</color>";
        feed.text=string.Join("\n",Director.Feed);
        if(Director.Countdown>0)notice.text="PREPÁRATE  "+Mathf.CeilToInt(Director.Countdown)+"\nTu código es secreto. Lee los de tus rivales.";else notice.text=Director.Notice;
        protection.text=p.Protected?"PROTEGIDO · "+Mathf.CeilToInt(p.ProtectionRemaining)+" s":p.ProtectorAvailable?"Q · PROTECTOR (1)":"PROTECTOR AGOTADO";
        var nearby=Director.NearestTerminal(p.transform.position);
        bool use=Director.CanUse(p,nearby);
        hint.text=!p.Alive?"ELIMINADO · Observando la partida · TAB cambiar jugador · ESC menú":
            use?"E · USAR "+nearby.label+"    |    Introduce un código rival que hayas visto":
            "WASD mover · ESPACIO saltar · RATÓN DERECHO cámara · Q protector · R mantener para volver · M sonido · ESC menú";
        if(k!=null)
        {
            if(k.escapeKey.wasPressedThisFrame){if(TerminalOpen)CloseTerminal();else if(!Director.Finished)menu.SetActive(!MenuOpen);}
            if(k.eKey.wasPressedThisFrame && !MenuOpen){if(TerminalOpen)CloseTerminal();else if(use)OpenTerminal(nearby);}
            if(TerminalOpen){if(k.backspaceKey.wasPressedThisFrame)Backspace();if(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame)Submit();}
            else if(Director.Finished && k.enterKey.wasPressedThisFrame)Director.Restart();
        }
        if(TerminalOpen)
        {
            if(!Director.CanUse(p,terminal))CloseTerminal();
            else if(Time.time<p.LockedUntil)response.text="BLOQUEO · "+Mathf.CeilToInt(p.LockedUntil-Time.time)+" s";
        }
        resultPanel.SetActive(Director.Finished);
        if(Director.Finished){menu.SetActive(false);results.text=Director.Result+"\n<size=19>"+Director.Ranking+"</size>";}
    }
    void LateUpdate()
    {
        if(root==null)return;
        var camera=Camera.main;var rect=(RectTransform)root;
        foreach(var pair in labels)
        {
            var a=pair.Key;var t=pair.Value;
            var point=camera.WorldToScreenPoint(a.Eye+Vector3.up*.65f);
            bool onScreen=point.z>0 && point.x>0 && point.x<Screen.width && point.y>0 && point.y<Screen.height;
            var code=onScreen && !TerminalOpen && !MenuOpen?Director.Observe(Director.Human,a):null;
            // Observe validates sight from both character and camera, excluding the observer's collider.
            t.gameObject.SetActive(code!=null);
            if(code!=null){t.text=a.DisplayName+"\n"+code;RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,point,null,out var local);t.rectTransform.anchoredPosition=local;}
        }
    }
    public void OpenTerminal(MortalCodeTerminal t)
    {if(!Director.CanUse(Director.Human,t))return;terminal=t;digits="";entry.text="_ _ _ _";response.text="Escribe los 4 números que viste";terminalTitle.text=t.label;terminalPanel.SetActive(true);}
    public void ShowResponse(string text){if(response!=null)response.text=text;}
    public void CloseTerminal(){if(terminalPanel!=null)terminalPanel.SetActive(false);terminal=null;}
    void OnText(char c){if(TerminalOpen && c>='0' && c<='9')Append(c);}
    void Append(char c){if(digits.Length>=4)return;digits+=c;MortalCodeAudio.UI("key",.6f);entry.text=digits.PadRight(4,'_');}
    void Backspace(){if(digits.Length>0)digits=digits[..^1];entry.text=digits.PadRight(4,'_');}
    void Submit(){if(digits.Length!=4){response.text="Faltan números";return;}Director.Submit(Director.Human,terminal,digits,out var text);response.text=text;digits="";entry.text="_ _ _ _";}
    void OnDestroy(){if(subscribed!=null)subscribed.onTextInput-=OnText;}
}
