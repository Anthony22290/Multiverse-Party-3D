using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class BuildMortalCode
{
 const string ScenePath="Assets/001_Scenes/CodigoMortal.unity";
 const string Folder="Assets/003_Scripts/MortalCode/Generated";
 static Transform world;
 static Material floor,navy,teal,pink,gold,white,wood,atlas;
 static List<Transform> patrol=new();
 static Material Mat(string name,Color color)
 {
  var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.color=color;m.SetFloat("_Smoothness",.22f);EditorUtility.SetDirty(m);return m;
 }
 static GameObject Box(string name,Vector3 p,Vector3 scale,Material m,bool solid=true,Transform parent=null)
 {
  var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent==null?world:parent,false);
  o.transform.position=p;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=m;o.isStatic=true;
  if(!solid)Object.DestroyImmediate(o.GetComponent<Collider>());return o;
 }
 static Transform Point(string name,Vector3 p){var o=new GameObject(name);o.transform.SetParent(world,false);o.transform.position=p;return o.transform;}
 static void Patrol(Vector3 p)=>patrol.Add(Point("Ruta "+patrol.Count,p+Vector3.up*.1f));
 static void Text(string label,Vector3 p,float size= .12f)
 {
  var t=new GameObject(label,typeof(TextMesh)).GetComponent<TextMesh>();t.transform.SetParent(world,false);t.transform.SetPositionAndRotation(p,Quaternion.identity);
  t.text=label;t.fontSize=80;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.white;
 }
 static void Ramp(string name,Vector3 start,Vector3 end,float width)
 {
  Vector3 f=(end-start).normalized;Vector3 up=Vector3.Cross(f,Vector3.Cross(Vector3.up,f)).normalized;
  var o=Box(name,(start+end)*.5f-up*.3f,new(width,.6f,Vector3.Distance(start,end)+.4f),wood);
  o.transform.rotation=Quaternion.LookRotation(f,up);
  for(int side=-1;side<=1;side+=2){var rail=Box(name+" baranda",(start+end)*.5f+Vector3.Cross(Vector3.up,f).normalized*side*(width/2-.1f)+up*.6f,new(.18f,1.2f,Vector3.Distance(start,end)),gold);rail.transform.rotation=o.transform.rotation;}
  Patrol(start);Patrol(end);
 }
 static void Deck(string name,Vector3 top,Vector2 size)
 {
  Box(name,top-Vector3.up*.5f,new(size.x,1,size.y),floor);
  for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
   Box("Soporte "+name,new(top.x+x*(size.x/2-1),top.y/2-.5f,top.z+z*(size.y/2-1)),new(1.2f,top.y,1.2f),navy);
  // Short segmented rails leave the planned entrances clear.
  if(size.y==36) for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2) Box("Borde "+name,top+new Vector3(x*size.x/2,.6f,z*10),new(.2f,1.2f,9),teal);
 }
 static void Cover(Vector3 p,Material color)
 {
  Box("Kiosco · pared",p+new Vector3(0,1.6f,1.7f),new(6,3.2f,.4f),color);
  Box("Kiosco · lateral",p+new Vector3(-2.8f,1.6f,0),new(.4f,3.2f,3.8f),color);
  Box("Kiosco · cubierta",p+Vector3.up*3.4f,new(6.6f,.35f,4.6f),navy);
  Box("Mostrador",p+new Vector3(.5f,.55f,-.8f),new(3,1.1f,.75f),white);
  Box("Rótulo",p+new Vector3(0,2.8f,-2.1f),new(3.5f,.5f,.12f),gold,false);
  Patrol(p+new Vector3(1,0,0));
 }
 static void Model(string name,Vector3 p,float height,float yaw=0)
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/002_Modelos/piratas/FBX/"+name+".fbx");
  if(prefab==null)return;
  var root=Point(name,p);root.rotation=Quaternion.Euler(0,yaw,0);
  var model=PirateArenaVisuals.AddModel(prefab,root,height,atlas,false);
  foreach(var c in model.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
  // Simple predictable gameplay collision for decorative objects.
  var b=PirateArenaVisuals.BoundsOf(model);var col=root.gameObject.AddComponent<BoxCollider>();
  col.center=root.InverseTransformPoint(b.center);col.size=new Vector3(b.size.x,height,b.size.z);
 }
 static MortalCodeTerminal Terminal(string name,Vector3 p,float yaw)
 {
  var root=Point(name,p);root.rotation=Quaternion.Euler(0,yaw,0);var t=root.gameObject.AddComponent<MortalCodeTerminal>();t.label=name;
  var body=Box("Carcasa",p+Vector3.up*.7f,new(1.3f,1.4f,.7f),navy,true,root);
  var screen=Box("Pantalla",p+Vector3.up*1.5f+root.forward*.2f,new(1.1f,.8f,.12f),teal,false,root);screen.transform.rotation=root.rotation;
  body.transform.rotation=root.rotation;
  var sign=new GameObject("Señal",typeof(TextMesh)).GetComponent<TextMesh>();sign.transform.SetParent(root,false);
  sign.transform.localPosition=new(0,2.55f,0);sign.transform.localRotation=Quaternion.Euler(0,180,0);
  sign.text=name+"\n[E]";sign.fontSize=64;sign.characterSize=.045f;sign.anchor=TextAnchor.MiddleCenter;sign.alignment=TextAlignment.Center;sign.color=Color.white;
  Patrol(t.UsePoint);return t;
 }
 public static string Run()
 {
  if(Application.isPlaying)throw new Exception("Stop Play first");
  var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)throw new Exception("Active scene has unsaved changes");
  if(System.IO.File.Exists(ScenePath))throw new Exception("Scene already exists; edit it rather than rebuilding");
  System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  world=new GameObject("Plaza Vértigo · mapa").transform;patrol.Clear();
  floor=Mat("Piedra crema",new(.73f,.79f,.8f));navy=Mat("Azul noche",new(.085f,.12f,.22f));
  teal=Mat("Menta",new(.18f,.75f,.67f));pink=Mat("Coral",new(.88f,.3f,.46f));gold=Mat("Ámbar",new(1,.68f,.2f));white=Mat("Marfil",new(.94f,.92f,.82f));wood=Mat("Pasarela",new(.4f,.45f,.6f));
  atlas=AssetDatabase.LoadAssetAtPath<Material>("Assets/003_Scripts/PirateArena/Materials/Pirate_Atlas_URP.mat");
  Box("Plaza principal · 112 x 104",new(0,-.6f,0),new(112,1.2f,104),floor);
  Box("Paseo central",new(0,.015f,0),new(12,.025f,100),wood,false);
  for(int side=-1;side<=1;side+=2)
  {
   Box("Muro perimetral este oeste",new(side*55,2,0),new(1,4,104),navy);
   Box("Muro perimetral norte sur",new(0,2,side*51),new(110,4,1),navy);
   Deck(side<0?"Galería oeste · 6 m":"Galería este · 6 m",new(side*35,6,12),new(26,36));
   Ramp("Rampa a galería "+side,new(side*35,0,-34),new(side*35,6,-6),6);
   Ramp("Acceso al observatorio "+side,new(side*25,6,24),new(side*12,12,35),5);
   Cover(new(side*41,6,19),side<0?pink:teal);Cover(new(side*29,6,2),side<0?teal:pink);
   Cover(new(side*18,0,-30),side<0?teal:pink);Cover(new(side*16,0,-9),side<0?pink:teal);
   Cover(new(side*41,0,-43),teal);Cover(new(side*18,0,40),pink);
   Model("Environment_PalmTree_1",new(side*48,0,-20),8,side*25);
   Model("Environment_PalmTree_2",new(side*8,0,31),7);
   Model("Environment_House2",new(side*47,0,40),9,180);
   for(int z=-36;z<=36;z+=18)
   {Box("Jardinera",new(side*8,.55f,z),new(3,1.1f,5),white);Box("Seto",new(side*8,1.5f,z),new(2.7f,1.6f,4.7f),teal);}
   for(int z=-22;z<=28;z+=25)
   {Box("Escondite en L",new(side*47,1.6f,z),new(5,3.2f,.5f),wood);Box("Escondite lateral",new(side*49,1.6f,z-2),new(.5f,3.2f,4),wood);Patrol(new(side*47,0,z-2));}
  }
  Deck("Puente de los códigos",new(0,6,12),new(44,5));
  // Main bridge has continuous side rails and full solid underside.
  for(int side=-1;side<=1;side+=2)Box("Baranda puente",new(0,6.65f,12+side*2.4f),new(44,1.3f,.2f),gold);
  Deck("Observatorio · 12 m",new(0,12,38),new(28,20));
  Box("Respaldo observatorio",new(0,13.6f,47.5f),new(28,3.2f,.5f),navy);
  Cover(new(5,12,39),teal);Cover(new(-5,12,30),pink);
  Box("Fuente · base",new(0,.5f,-15),new(7,1,7),navy);
  Box("Fuente · agua",new(0,1.04f,-15),new(6,.1f,6),teal,false);
  Box("Monumento central",new(0,3,-15),new(1.3f,5,1.3f),gold);
  Text("PLAZA VÉRTIGO",new(0,9.5f,11.6f),.15f);
  Text("CÓDIGO MORTAL",new(0,4,-49.5f),.16f);
  Text("OBSERVATORIO",new(0,15,47.1f),.1f);
  var director=new GameObject("Código Mortal · reglas").AddComponent<MortalCodeDirector>();director.characterAtlas=atlas;
  director.terminals=new[]{Terminal("T1 · MERCADO",new(-4,0,-38),180),Terminal("T2 · PATIO",new(18,0,23),180),Terminal("T3 · OESTE",new(-36,6,26),180),Terminal("T4 · ESTE",new(35,6,22),180),Terminal("T5 · MIRADOR",new(0,12,44),180)};
  Vector3[] starts={new(0,.1f,-43),new(-26,.1f,-18),new(26,.1f,-18),new(-36,6.1f,12),new(36,6.1f,12),new(0,12.1f,34)};
  director.spawnPoints=starts.Select((p,i)=>Point("Salida "+i,p)).ToArray();
  foreach(var p in starts)Patrol(p);
  foreach(var p in new[]{new Vector3(0,0,26),new Vector3(0,0,43),new Vector3(-23,0,12),new Vector3(23,0,12),new Vector3(0,6,12),new Vector3(-35,6,9),new Vector3(35,6,9)})Patrol(p);
  director.patrolPoints=patrol.ToArray();
  RenderSettings.ambientLight=new Color(.68f,.72f,.82f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new(.45f,.62f,.73f);RenderSettings.fogStartDistance=95;RenderSettings.fogEndDistance=220;
  var sun=new GameObject("Sol").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
  var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.transform.position=new(85,85,-95);camera.transform.LookAt(new Vector3(0,3,0));camera.GetComponent<Camera>().farClipPlane=300;camera.GetComponent<Camera>().backgroundColor=RenderSettings.fogColor;camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;
  Physics.SyncTransforms();
  var nav=world.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
  nav.overrideVoxelSize=true;nav.voxelSize=.15f;nav.BuildNavMesh();
  if(nav.navMeshData==null)throw new Exception("NavMesh did not bake");
  AssetDatabase.CreateAsset(nav.navMeshData,Folder+"/PlazaNavigation.asset");
  var settings=EditorBuildSettings.scenes.ToList();if(!settings.Any(s=>s.path==ScenePath))settings.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=settings.ToArray();
  EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
  return "Created CodigoMortal: 112x104 m, levels 0/6/12 m, 5 terminals, 6 players, "+patrol.Count+" navigation points";
 }
}
