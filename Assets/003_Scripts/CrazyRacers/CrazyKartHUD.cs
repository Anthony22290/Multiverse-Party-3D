using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
public sealed class CrazyKartHUD : MonoBehaviour
{
    CrazyKartDirector director;
    UnityEngine.UI.Text status, item, banner, results;
    GameObject resultPanel;
    RectTransform map;
    RectTransform[] markers;
    Font font;
    UnityEngine.UI.Image driftFill;
    UnityEngine.UI.Text notice;
    int previousLap=1;
    float lapNoticeUntil;
    UnityEngine.UI.Image itemIcon;
    UnityEngine.UI.Text itemHelp;
    Sprite[] itemSprites;
    int previousItem;
    float itemFlashUntil;
    GameObject pausePanel;
    UnityEngine.UI.Text bigPosition; UnityEngine.UI.Image flash;
    int shownPosition; float positionPopUntil, lastRouletteSwap; int rouletteIcon; bool wasSpinning;
    UnityEngine.UI.Image slipFill;
    RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false); r.anchorMin=r.anchorMax=anchor; r.anchoredPosition=pos; r.sizeDelta=size; return r;
    }
    UnityEngine.UI.Text Label(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,int pt)
    {
        var t=Rect(name,parent,anchor,pos,size).gameObject.AddComponent<UnityEngine.UI.Text>();
        t.font=font;t.fontSize=pt;t.fontStyle=FontStyle.Bold;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;return t;
    }
    void Panel(RectTransform r,Color c) { r.gameObject.AddComponent<UnityEngine.UI.Image>().color=c; }
    void Start()
    {
        director=GetComponent<CrazyKartDirector>();
        font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvas=new GameObject("HUD Carreras",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f; scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        if(FindAnyObjectByType<EventSystem>()==null)new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        var top=Rect("Panel carrera",canvas.transform,new Vector2(0,1),new Vector2(185,-88),new Vector2(340,150));Panel(top,new Color(.09f,.04f,.2f,.92f));
        status=Label("Posicion y vueltas",top,new Vector2(.5f,.5f),Vector2.zero,new Vector2(325,140),22);
        var bottom=Rect("Controles",canvas.transform,new Vector2(.5f,0),new Vector2(0,26),new Vector2(1000,40));Panel(bottom,new Color(.09f,.04f,.2f,.9f));
        Label("Ayuda",bottom,new Vector2(.5f,.5f),Vector2.zero,new Vector2(990,38),17).text="WASD / Flechas  Conducir    SHIFT  Derrapar    ESPACIO / F  Objeto    R  Volver a pista    ESC  Pausa";
        var slot=Rect("Objeto",canvas.transform,new Vector2(0,0),new Vector2(185,155),new Vector2(340,185));Panel(slot,new Color(.24f,.07f,.4f,.92f));
        item=Label("Estado objeto",slot,new Vector2(.5f,.5f),new Vector2(38,30),new Vector2(245,75),20);
        itemSprites=new Sprite[CrazyKartController.ItemCount+1];for(int i=0;i<itemSprites.Length;i++)itemSprites[i]=CrazyKartItemArt.Icon(i);
        itemIcon=Rect("Icono objeto",slot,new Vector2(0,.5f),new Vector2(45,30),new Vector2(58,58)).gameObject.AddComponent<UnityEngine.UI.Image>();itemIcon.preserveAspect=true;itemIcon.raycastTarget=false;
        itemHelp=Label("Uso del objeto",slot,new Vector2(.5f,.5f),new Vector2(0,-40),new Vector2(310,60),17);
        var track=Rect("Carga derrape",slot,new Vector2(.5f,0),new Vector2(0,10),new Vector2(280,8));Panel(track,new Color(.05f,.03f,.1f));
        var fill=Rect("Progreso derrape",track,new Vector2(0,.5f),Vector2.zero,new Vector2(0,8));fill.pivot=new Vector2(0,.5f);driftFill=fill.gameObject.AddComponent<UnityEngine.UI.Image>();
        var slipTrack=Rect("Rebufo",canvas.transform,new Vector2(.5f,1),new Vector2(0,-86),new Vector2(220,6));Panel(slipTrack,new Color(.05f,.03f,.1f,.6f));
        var slip=Rect("Carga rebufo",slipTrack,new Vector2(0,.5f),Vector2.zero,new Vector2(0,6));slip.pivot=new Vector2(0,.5f);slipFill=slip.gameObject.AddComponent<UnityEngine.UI.Image>();slipFill.color=new Color(.75f,.95f,1f);
        notice=Label("Avisos",canvas.transform,new Vector2(.5f,1),new Vector2(0,-48),new Vector2(490,60),30);notice.color=new Color(1,.85f,.2f);
        banner=Label("Cuenta atras",canvas.transform,new Vector2(.5f,.5f),new Vector2(0,100),new Vector2(700,150),64);banner.color=new Color(1,.86f,.2f);
        map=Rect("Minimapa",canvas.transform,Vector2.one,new Vector2(-110,-120),new Vector2(180,210));Panel(map,new Color(.09f,.04f,.2f,.88f));
        for(int i=0;i<director.Waypoints.Count;i++) { Vector2 a=Map(director.GetWaypointPosition(i)), b=Map(director.GetWaypointPosition(i+1));var line=Rect("Pista",map,new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(Vector2.Distance(a,b)+1,5));line.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);Panel(line,new Color(.6f,.7f,.8f)); }
        foreach(var sc in director.Shortcuts){var a=Map(sc.start);var b=Map(sc.end);var line=Rect("Atajo",map,new Vector2(.5f,.5f),(a+b)*.5f,new Vector2(Vector2.Distance(a,b),3));line.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);Panel(line,Color.yellow);}
        Label("Sonido",map,new Vector2(.5f,0),new Vector2(0,12),new Vector2(170,22),12).text="M · SONIDO ON / OFF";
        markers=new RectTransform[director.Karts.Count];
        for(int i=0;i<markers.Length;i++){markers[i]=Rect("Corredor",map,new Vector2(.5f,.5f),Vector2.zero,new Vector2(11,11));Panel(markers[i],director.Karts[i].KartColor);}
        var panel=Rect("Resultados",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,430));Panel(panel,new Color(.08f,.03f,.17f,.97f));resultPanel=panel.gameObject;
        results=Label("Clasificacion",panel,new Vector2(.5f,.5f),new Vector2(0,55),new Vector2(590,290),25);
        Button(panel,"OTRA CARRERA",new Vector2(-140,-160),director.Restart);
        Button(panel,"VOLVER A PLAZA",new Vector2(140,-160),director.ReturnToHub);
        resultPanel.SetActive(false);
        bigPosition=Label("Posicion grande",canvas.transform,new Vector2(1,0),new Vector2(-115,105),new Vector2(210,140),96);
        var fl=Rect("Destello",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);fl.anchorMin=Vector2.zero;fl.anchorMax=Vector2.one;
        flash=fl.gameObject.AddComponent<UnityEngine.UI.Image>();flash.raycastTarget=false;flash.color=Color.clear;
        var dim=Rect("Pausa",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);dim.anchorMin=Vector2.zero;dim.anchorMax=Vector2.one;Panel(dim,new Color(0,0,0,.55f));pausePanel=dim.gameObject;
        var card=Rect("Tarjeta de pausa",dim,new Vector2(.5f,.5f),Vector2.zero,new Vector2(400,380));Panel(card,new Color(.08f,.03f,.17f,.97f));
        Label("Titulo pausa",card,new Vector2(.5f,.5f),new Vector2(0,145),new Vector2(340,50),34).text="PAUSA";
        Label("Circuito",card,new Vector2(.5f,.5f),new Vector2(0,108),new Vector2(360,26),16).text=CrazyKartDirector.CircuitTitle;
        Button(card,"CONTINUAR",new Vector2(0,55),()=>director.SetPaused(false));
        Button(card,"REINICIAR",new Vector2(0,-8),director.Restart);
        Button(card,"VOLVER A PLAZA",new Vector2(0,-71),director.ReturnToHub);
        Label("Ayuda pausa",card,new Vector2(.5f,.5f),new Vector2(0,-140),new Vector2(360,30),15).text="ESC / P para continuar · M sonido";
        pausePanel.SetActive(false);
        MultiverseUI.Skin(canvas,MultiverseUI.Mode.Race);
        MultiverseParty.StripMenus();
        flash.color=Color.clear;
    }
    void Button(Transform parent,string text,Vector2 pos,UnityEngine.Events.UnityAction action,float width=255)
    {
        var r=Rect(text,parent,new Vector2(.5f,.5f),pos,new Vector2(width,52));Panel(r,new Color(.85f,.16f,.46f));
        var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.onClick.AddListener(action);
        Label(text,r,new Vector2(.5f,.5f),Vector2.zero,new Vector2(250,50),21).text=text;
    }
    static string Ordinal(int n)=>n+".";
    static int ChargeTier(CrazyKartController k){float c=k.NitroAmount/100*CrazyKartController.Tier3;return !k.Drifting?0:c>=CrazyKartController.Tier3?3:c>=CrazyKartController.Tier2?2:c>=CrazyKartController.Tier1?1:0;}
    void OnDestroy(){if(itemSprites!=null)foreach(var sprite in itemSprites)if(sprite!=null){Destroy(sprite.texture);Destroy(sprite);}}
    Vector2 Map(Vector3 p){float x=1,z=1;foreach(var point in director.Waypoints){x=Mathf.Max(x,Mathf.Abs(point.x));z=Mathf.Max(z,Mathf.Abs(point.z));}return new Vector2(p.x*75/x,p.z*82/z);}
    void Update()
    {
        if(director==null||status==null)return;
        var player=director.Karts.FirstOrDefault(k=>k.IsHuman)??director.Karts.FirstOrDefault();
        if(player==null)return;
        string lapLine=(player.LastLapTime>0?"Últ. "+player.LastLapTime.ToString("F1")+" s":"Vuelta "+player.CurrentLapTime.ToString("F1")+" s")+"   Récord "+(director.RecordLap>0?director.RecordLap.ToString("F1")+" s":"—");
        status.text="CARRERAS LOCAS\n"+director.Position(player)+" / "+director.Karts.Count+"    VUELTA "+Mathf.Min(player.CurrentLap,director.laps)+"/"+director.laps+"\n"+director.RoundTime.ToString("F1")+" s     "+(player.Speed*3.6f).ToString("F0")+" km/h\n<size=17>"+lapLine+"</size>";
        item.text=player.ItemName+"\n"+(player.Stunned?"¡IMPACTO!":player.Shielded?"ESCUDO ACTIVO":player.Boosting?"¡TURBO!":player.RouletteSpinning?"¡RULETA!":ChargeTier(player)==3?"¡SUELTA: TURBO ROSA!":ChargeTier(player)==2?"SUELTA: TURBO NARANJA":ChargeTier(player)==1?"SUELTA: TURBO AZUL":player.NitroAmount>0?"CARGANDO DERRAPE":player.HeldItem>0?"OBJETO PREPARADO":"BUSCA UNA CAJA");
        if(player.RouletteSpinning)item.text="? ? ?\n¡RULETA!";
        int shownItem=player.HeldItem;
        if(player.RouletteSpinning){if(Time.unscaledTime-lastRouletteSwap>.07f){lastRouletteSwap=Time.unscaledTime;rouletteIcon=rouletteIcon%CrazyKartController.ItemCount+1;}shownItem=rouletteIcon;}
        itemIcon.sprite=itemSprites[shownItem];itemIcon.color=CrazyKartItemArt.ColorFor(shownItem);
        if(previousItem!=player.HeldItem||(wasSpinning&&!player.RouletteSpinning)){previousItem=player.HeldItem;itemFlashUntil=Time.time+.5f;}
        wasSpinning=player.RouletteSpinning;
        itemIcon.rectTransform.localScale=Vector3.one*(1+Mathf.Max(0,itemFlashUntil-Time.time)*.3f);
        itemHelp.text=player.RouletteSpinning?"Eligiendo objeto…":CrazyKartItemArt.Help[player.HeldItem]+(player.HeldItem==0?"":"\nESPACIO / F para usar");
        bool startNews=Time.time-player.StartResultTime<1.6f;
        banner.text=director.Countdown>3?"PREPARADOS":director.Countdown>0?Mathf.CeilToInt(director.Countdown).ToString():startNews?(player.StartResult>0?"¡SALIDA PERFECTA!":"¡CALADO!"):director.RoundTime<1?"¡SALIDA!":"";
        for(int i=0;i<markers.Length;i++)markers[i].anchoredPosition=Map(director.Karts[i].transform.position);
        driftFill.rectTransform.sizeDelta=new Vector2(280*player.NitroAmount/100,8);
        driftFill.color=CrazyKartFX.TierColor(Mathf.Max(1,ChargeTier(player)));
        // Countdown numbers pop on each tick.
        float tick=director.Countdown>0&&director.Countdown<=3?director.Countdown-Mathf.Floor(director.Countdown):0;
        banner.rectTransform.localScale=Vector3.one*(1+.55f*Mathf.Pow(tick,4));
        // Big race position, pops when it changes.
        int pos=director.Position(player);
        if(pos!=shownPosition){shownPosition=pos;positionPopUntil=Time.time+.35f;}
        bigPosition.text=pos+"<size=40>/"+director.Karts.Count+"</size>";
        bigPosition.color=pos==1?new Color(1,.84f,.2f):pos==2?new Color(.85f,.9f,1):pos==3?new Color(1,.6f,.35f):Color.white;
        bigPosition.rectTransform.localScale=Vector3.one*(1+Mathf.Max(0,positionPopUntil-Time.time)*1.2f);
        bigPosition.gameObject.SetActive(!director.Finished&&director.Countdown<=0);
        // Screen flash: white for lightning, red when you get hit.
        float storm=0,hit=Mathf.Max(0,.25f-(Time.time-player.LastHitTime))*2.2f;
        foreach(var k in director.Karts)storm=Mathf.Max(storm,Mathf.Max(0,.35f-(Time.time-k.LastStormTime))*2.2f);
        flash.color=storm>0?new Color(1,1,.9f,storm*.8f):new Color(1,.15f,.1f,hit*.45f);
        if(player.CurrentLap!=previousLap){previousLap=player.CurrentLap;lapNoticeUntil=Time.time+2.5f;}
        notice.text=director.Finished?"":player.HasFinished?"¡EN META! Esperando a los demás…":director.Net!=null&&!director.Net.Started?PartyLink.WaitingText:director.Countdown>0?"Acelera cuando salga el 1: ¡turbo de salida!":Time.time-player.LastRecoveryTime<1.3f?"DE VUELTA EN PISTA":player.WrongWay?"¡SENTIDO CONTRARIO!":Time.time-player.LapRecordTime<2.5f?"¡RÉCORD DE VUELTA! "+director.RecordLap.ToString("F1")+" s":Time.time<lapNoticeUntil?(player.CurrentLap==director.laps?"¡ÚLTIMA VUELTA!":"VUELTA "+player.CurrentLap):Time.time-player.LastSlipstreamBoost<1f?"¡REBUFO!":player.SlipstreamCharge01>.2f?"REBUFO…":"";
        slipFill.rectTransform.sizeDelta=new Vector2(220*player.SlipstreamCharge01,6);
        slipFill.transform.parent.gameObject.SetActive(player.SlipstreamCharge01>0&&!director.Finished);
        pausePanel.SetActive(director.Paused);
        resultPanel.SetActive(director.Finished);
        if(director.Finished)
        {
            string rows=string.Join("\n",director.Karts.OrderBy(k=>director.Position(k)).Select(k=>Ordinal(director.Position(k))+"  "+k.DisplayName+(k.HasFinished?"   "+k.FinishTime.ToString("F1")+" s":"   ≈ "+k.EstimatedFinish.ToString("F1")+" s")));
            string best=player.BestLapTime>0?"Mejor vuelta "+player.BestLapTime.ToString("F1")+" s":"";
            string record=director.NewRaceRecord?"<color=#FFD447>¡NUEVO RÉCORD DE CARRERA!</color>":director.NewLapRecord?"<color=#FFD447>¡NUEVO RÉCORD DE VUELTA!</color>":"Récord: carrera "+(director.RecordRace>0?director.RecordRace.ToString("F1")+" s":"—")+" · vuelta "+(director.RecordLap>0?director.RecordLap.ToString("F1")+" s":"—");
            var winner=director.Finishers.Count>0?director.Finishers[0]:null;
            results.text=(winner==null?"":winner.IsHuman?"¡HAS GANADO!":"¡"+winner.DisplayName.ToUpper()+" GANA!")+"\n<size=21>"+rows+"</size>\n<size=18>"+best+"\n"+record+"</size>";
        }
    }
}





