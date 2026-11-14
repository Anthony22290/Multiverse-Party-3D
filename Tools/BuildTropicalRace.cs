using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class BuildTropicalRace
{
    static Transform root;
    static string folder="Assets/003_Scripts/CrazyRacers/Generated";
    static Vector3[] path;
    static Material road,white,pink,yellow,grass,sand,water,zombie,pirate;
    static Material Mat(string name,Color color,string texture=null)
    {
        string p=folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}
        m.color=color;m.SetFloat("_Smoothness",.15f);
        if(texture!=null)m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
        EditorUtility.SetDirty(m);return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material m,Quaternion rot,bool solid=true)
    {
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(root,false);
        o.transform.SetPositionAndRotation(p,rot);o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=m;
        if(!solid)Object.DestroyImmediate(o.GetComponent<Collider>());return o;
    }
    static GameObject Model(string name,string folderName,Vector3 p,float height,float yaw,Material m,bool solid)
    {
        string asset=AssetDatabase.FindAssets(name+" t:Model",new[]{folderName}).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(s=>s.EndsWith("/"+name+".fbx",StringComparison.OrdinalIgnoreCase));
        if(asset==null)throw new Exception("Model missing "+name);
        var o=new GameObject(name);o.transform.SetParent(root,false);o.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
        PirateArenaVisuals.AddModel(AssetDatabase.LoadAssetAtPath<GameObject>(asset),o.transform,height,m,false);
        foreach(var c in o.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
        if(solid)foreach(var mesh in o.GetComponentsInChildren<MeshFilter>())if(mesh.sharedMesh!=null)mesh.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh.sharedMesh;
        return o;
    }
    static void Ribbon(string name,float inner,float outer,float height,Material mat)
    {
        int n=path.Length;var verts=new Vector3[n*2];var uv=new Vector2[n*2];var tris=new int[n*6];
        for(int i=0;i<n;i++)
        {
            Vector3 f=(path[(i+1)%n]-path[(i+n-1)%n]).normalized,side=Vector3.Cross(Vector3.up,f);
            verts[2*i]=path[i]+side*inner+Vector3.up*height;verts[2*i+1]=path[i]+side*outer+Vector3.up*height;
            uv[2*i]=new Vector2(0,i);uv[2*i+1]=new Vector2(1,i);
            int a=i*2,b=((i+1)%n)*2;
            tris[i*6]=a;tris[i*6+1]=b;tris[i*6+2]=a+1;tris[i*6+3]=a+1;tris[i*6+4]=b;tris[i*6+5]=b+1;
        }
        var mesh=new Mesh{name=name};mesh.vertices=verts;mesh.triangles=tris;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
        string p=folder+"/"+name+".asset";AssetDatabase.CreateAsset(mesh,p);
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=mat;
    }
    static void Sign(string value,Vector3 pos,Quaternion rotation)
    {
        var o=new GameObject(value,typeof(TextMesh));o.transform.SetParent(root,false);o.transform.SetPositionAndRotation(pos,rotation);
        var t=o.GetComponent<TextMesh>();t.text=value;t.fontSize=90;t.characterSize=.08f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.white;
    }
    public static string Run()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!="Assets/001_Scenes/CarrerasLocas.unity")throw new Exception("Open CarrerasLocas edit mode");
        if(GameObject.Find("Circuito Costa del Caos")!=null)return "Already created";
        // Keep the original scene objects intact, but inactive.
        var old=scene.GetRootGameObjects();var archive=new GameObject("Prototipo anterior - conservado");
        foreach(var g in old){g.transform.SetParent(archive.transform,true);g.SetActive(false);}archive.SetActive(false);
        if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/003_Scripts/CrazyRacers","Generated");
        root=new GameObject("Circuito Costa del Caos").transform;
        road=Mat("Asfalto",new Color(.13f,.18f,.25f));white=Mat("Blanco",new Color(.96f,.96f,.91f));
        pink=Mat("Coral",new Color(.96f,.2f,.36f));yellow=Mat("Amarillo",new Color(1,.76f,.14f));
        grass=Mat("Cesped",new Color(.26f,.64f,.35f));sand=Mat("Arena",new Color(.92f,.78f,.48f));water=Mat("Mar",new Color(.07f,.6f,.75f));
        zombie=Mat("Atlas coches",Color.white,"Assets/002_Modelos/zombies/Vehicles-20261027T131326Z-1-001/Vehicles/Blends/Zombie_Atlas.png");
        string pirateTexture=AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/002_Modelos/piratas"}).Select(AssetDatabase.GUIDToAssetPath).First(s=>s.EndsWith(".png")&&s.ToLower().Contains("atlas"));
        pirate=Mat("Atlas isla",Color.white,pirateTexture);
        Box("Mar tropical",new Vector3(0,-1,0),new Vector3(400,1,400),water,Quaternion.identity,false);
        Box("Isla suelo continuo",new Vector3(0,-.75f,0),new Vector3(128,1.5f,158),sand,Quaternion.identity);
        Box("Jardin central",new Vector3(0,.009f,0),new Vector3(47,.015f,70),grass,Quaternion.identity,false);
        path=new Vector3[96];
        for(int i=0;i<path.Length;i++){float a=i*Mathf.PI*2/path.Length;path[i]=new Vector3(Mathf.Sin(a)*43+Mathf.Sin(a*2)*7,0,Mathf.Cos(a)*60);}
        Ribbon("Pista continua", -6,6,.035f,road);
        Ribbon("Borde izquierdo",-6.7f,-6,.042f,white);Ribbon("Borde derecho",6,6.7f,.042f,white);
        var d=new GameObject("Director de carrera",typeof(CrazyKartDirector)).GetComponent<CrazyKartDirector>();d.transform.SetParent(root,false);
        for(int i=0;i<path.Length;i++)
        {
            var p=path[i];var next=path[(i+1)%path.Length];var f=(next-p).normalized;var side=Vector3.Cross(Vector3.up,f);var rot=Quaternion.LookRotation(f);float length=Vector3.Distance(p,next);
            foreach(int s in new[]{-1,1})
                Box("Barrera "+i+" "+s,(p+next)*.5f+side*(s*7.3f)+Vector3.up*.6f,new Vector3(.55f,1.2f,length+.3f),i%4<2?pink:white,rot);
            if(i%2==0)Box("Marca central",p+Vector3.up*.048f,new Vector3(.12f,.01f,1.2f),white,rot,false);
            if(i%3==0)
            {
                int index=i/3;d.AddWaypoint(p);
                var cp=new GameObject("Puerta de vuelta "+index,typeof(BoxCollider),typeof(CrazyKartCheckpointIndex));cp.transform.SetParent(root,false);cp.transform.SetPositionAndRotation(p+Vector3.up,rot);
                cp.GetComponent<BoxCollider>().size=new Vector3(14,4,1.8f);cp.GetComponent<BoxCollider>().isTrigger=true;cp.GetComponent<CrazyKartCheckpointIndex>().index=index;
            }
            if(i%24==12)
                foreach(int lane in new[]{-1,0,1})
                {
                    var item=Box("Caja sorpresa",p+side*lane*3+Vector3.up*1.1f,Vector3.one*1.25f,yellow,Quaternion.Euler(0,45,0));
                    item.GetComponent<Collider>().isTrigger=true;item.AddComponent<CrazyKartItemBox>();
                    var q=new GameObject("?",typeof(TextMesh));q.transform.SetParent(item.transform,false);q.transform.localPosition=new Vector3(0,0,-.51f);
                    var text=q.GetComponent<TextMesh>();text.text="?";text.anchor=TextAnchor.MiddleCenter;text.fontSize=60;text.characterSize=.12f;text.color=new Color(.2f,.1f,.35f);
                }
            if(i==28||i==76)
            {
                var pad=Box("Turbo de pista",p+side*2+Vector3.up*.1f,new Vector3(3,.15f,5),yellow,rot);
                pad.GetComponent<Collider>().isTrigger=true;pad.AddComponent<CrazyKartBoostPad>();
                Sign(">>>",p+Vector3.up*.2f,Quaternion.Euler(90,rot.eulerAngles.y,0));
            }
            if(i%8==0)
            {
                Model("Environment_PalmTree_1","Assets/002_Modelos/piratas",p+side*13,6.5f,i*19,pirate,true);
                Model("Environment_Rock_2","Assets/002_Modelos/piratas",p-side*13,2.3f,i*12,pirate,true);
            }
        }
        var start=path[0];var forward=(path[1]-start).normalized;var right=Vector3.Cross(Vector3.up,forward);var facing=Quaternion.LookRotation(forward);
        for(int lane=0;lane<12;lane++)for(int row=0;row<2;row++)
            Box("Cuadros de meta",start+right*(lane-5.5f)+forward*(row-.5f)+Vector3.up*.06f,new Vector3(1,.02f,1),(lane+row)%2==0?white:road,facing,false);
        foreach(int s in new[]{-1,1})Box("Columna meta",start+right*s*8+Vector3.up*3.5f,new Vector3(.9f,7,.9f),pink,facing);
        Box("Arco de salida",start+Vector3.up*6.8f,new Vector3(17,1.4f,1),pink,facing);
        Sign("COSTA DEL CAOS",start+Vector3.up*6.8f-forward*.56f,facing);
        string[] names={"Tú","Coral","Rayo","Menta"};Color[] colors={Color.cyan,new Color(1,.2f,.4f),Color.yellow,Color.green};
        CrazyKartController human=null;
        for(int i=0;i<4;i++)
        {
            var go=new GameObject("Kart "+names[i],typeof(Rigidbody),typeof(BoxCollider),typeof(CrazyKartController));go.transform.SetParent(root,false);
            go.transform.SetPositionAndRotation(start+forward*(4+(i/2)*4)+right*(i%2==0?-2:2)+Vector3.up*.6f,facing);
            go.GetComponent<BoxCollider>().size=new Vector3(1.6f,.8f,2.8f);
            var kart=go.GetComponent<CrazyKartController>();kart.IsHuman=i==0;kart.DisplayName=names[i];kart.KartColor=colors[i];
            var model=Model("Vehicle_Sports","Assets/002_Modelos/zombies",Vector3.zero,1.05f,0,zombie,false);
            model.transform.SetParent(go.transform,false);model.transform.localPosition=new Vector3(0,-.4f,0);model.transform.localRotation=Quaternion.identity;
            var marker=Box("Color piloto",Vector3.zero,new Vector3(1,.07f,.65f),Mat("Color "+i,colors[i]),Quaternion.identity,false);
            marker.transform.SetParent(go.transform,false);marker.transform.localPosition=new Vector3(0,.67f,-.2f);marker.transform.localRotation=Quaternion.identity;
            if(i==0)human=kart;
        }
        Model("Vehicle_Truck","Assets/002_Modelos/zombies",new Vector3(17,0,44),2.5f,90,zombie,true);
        for(int i=0;i<6;i++)Model("TrafficCone_1","Assets/002_Modelos/zombies",new Vector3(10+i*2,0,40),.8f,0,zombie,true);
        for(int i=0;i<8;i++)Model("Environment_PalmTree_3","Assets/002_Modelos/piratas",new Vector3(-18+(i%4)*12,0,-22+(i/4)*43),7,i*37,pirate,true);
        Model("Environment_Rock_5","Assets/002_Modelos/piratas",new Vector3(0,0,0),9,0,pirate,true);
        var light=new GameObject("Sol tropical",typeof(Light));light.transform.SetParent(root,false);light.transform.rotation=Quaternion.Euler(48,-30,0);light.GetComponent<Light>().type=LightType.Directional;light.GetComponent<Light>().intensity=1.5f;light.GetComponent<Light>().shadows=LightShadows.Soft;
        RenderSettings.ambientLight=new Color(.62f,.73f,.8f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=140;RenderSettings.fogEndDistance=300;RenderSettings.fogColor=new Color(.52f,.83f,.93f);
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(CrazyKartCamera));camera.transform.SetParent(root,false);camera.tag="MainCamera";
        camera.GetComponent<CrazyKartCamera>().target=human;camera.transform.position=human.transform.position-forward*8+Vector3.up*5;camera.transform.LookAt(human.transform.position+Vector3.up);
        camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=RenderSettings.fogColor;camera.GetComponent<Camera>().farClipPlane=400;
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Saved: 96 track sections, 32 ordered gates, 4 cars, 12 item boxes, 2 boost pads. Original prototype preserved inactive.";
    }
}
