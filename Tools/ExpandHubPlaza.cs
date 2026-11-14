using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class ExpandHubPlaza
{
 static Transform root;
 static Material grass,sand,pink,blue,stone;
 static GameObject Box(string name,Vector3 pos,Vector3 size,Material mat)
 {
  var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(root,false);o.transform.position=pos;o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=mat;return o;
 }
 static Material Mat(string name,Color c,string texture=null)
 {
  string path="Assets/003_Scripts/PirateArena/Materials/"+name+".mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
  m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.15f);
  if(texture!=null)m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texture));
  EditorUtility.SetDirty(m);return m;
 }
 static GameObject Model(string name,string folder,Vector3 p,float height,float yaw,Material mat)
 {
  string path=AssetDatabase.FindAssets(name+" t:Model",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(s=>s.EndsWith("/"+name+".fbx",StringComparison.OrdinalIgnoreCase));
  if(path==null)throw new Exception("Missing model "+name);
  var parent=new GameObject("Exhibición - "+name);parent.transform.SetParent(root,false);parent.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
  PirateArenaVisuals.AddModel(AssetDatabase.LoadAssetAtPath<GameObject>(path),parent.transform,height,mat,false);Solid(parent);return parent;
 }
 static void Solid(GameObject o)
 {
  foreach(var c in o.GetComponentsInChildren<Collider>()) c.enabled=true;
  foreach(var mf in o.GetComponentsInChildren<MeshFilter>())
   if(mf.sharedMesh!=null && mf.GetComponent<Collider>()==null)mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;
  if(o.GetComponentsInChildren<Collider>().Length==0)
  {
   var b=PirateArenaVisuals.BoundsOf(o);var c=o.AddComponent<BoxCollider>();c.center=o.transform.InverseTransformPoint(b.center);c.size=b.size;
  }
 }
 static void Sign(string text,Vector3 p,float size=0.09f)
 {
  var o=new GameObject(text,typeof(TextMesh));o.transform.SetParent(root,false);o.transform.position=p;
  var t=o.GetComponent<TextMesh>();t.text=text;t.fontSize=80;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.white;
 }
 public static string Run()
 {
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(scene.path!="Assets/001_Scenes/MundoDeInicio.unity" || Application.isPlaying)throw new Exception("Open hub in edit mode");
  var h=Object.FindAnyObjectByType<MultiverseHub>();
  if(h.transform.Find("Gran plaza del multiverso")!=null)return "Already expanded";
  root=new GameObject("Gran plaza del multiverso").transform;root.SetParent(h.transform,false);
  grass=Mat("Hub_Plaza_Grass",new Color(.23f,.56f,.34f));sand=Mat("Hub_Plaza_Sand",new Color(.84f,.70f,.46f));
  pink=Mat("Hub_Plaza_Pink",new Color(.9f,.18f,.48f));blue=Mat("Hub_Plaza_Blue",new Color(.18f,.55f,.85f));stone=Mat("Hub_Plaza_Stone",new Color(.43f,.47f,.57f));
  var sushi=Mat("Hub_Plaza_Sushi",Color.white,"Assets/002_Modelos/sushi/Sushi_Atlas.png");
  var space=Mat("Hub_Plaza_Space",Color.white,"Assets/002_Modelos/espacio/Vehicles-20261027T131745Z-1-001/Vehicles/Blends/Atlas.png");
  // Continuous structural collider below every plaza, bridge and garden, 76 x 76 m.
  Box("Suelo continuo - sin huecos",new Vector3(0,-.69f,8),new Vector3(76,3,76),grass);
  Box("Paseo este oeste",new Vector3(0,.85f,-5),new Vector3(69,.08f,6),sand);
  Box("Paseo norte",new Vector3(0,.85f,32),new Vector3(69,.08f,6),sand);
  foreach(int side in new[]{-1,1})
  {
   Box("Paseo lateral",new Vector3(side*28,.85f,13.5f),new Vector3(5,.08f,42),sand);
   Box("Borde lateral",new Vector3(side*37.5f,1.55f,8),new Vector3(.7f,1.5f,76),h.darkWood);
   Box("Borde frontal",new Vector3(0,1.55f,8+side*37.5f),new Vector3(76,1.5f,.7f),h.darkWood);
   for(int z=-24;z<=42;z+=6)Box("Poste perimetral",new Vector3(side*37.5f,2f,z),new Vector3(1,2.4f,1),h.brass);
   for(int z=-20;z<=38;z+=12)
   {
    var palm=new GameObject("Palmera de paseo");palm.transform.SetParent(root,false);palm.transform.position=new Vector3(side*33,.9f,z);
    PirateArenaVisuals.AddModel(h.palm,palm.transform,5.5f,h.atlas,false);Solid(palm);
   }
  }
  // Rest gardens leave broad, unobstructed pedestrian routes.
  foreach(int side in new[]{-1,1})
   for(int i=0;i<3;i++)
   {
    Model("Decoration_SakuraTree","Assets/002_Modelos/sushi",new Vector3(side*(15+i*5),.9f,-23),4.5f,30*i,sushi);
    Model("Environment_Bench","Assets/002_Modelos/sushi",new Vector3(side*(15+i*5),.9f,-19),1.1f,0,sushi);
   }
  Box("Jardín de bienvenida",new Vector3(0,.93f,-24),new Vector3(13,.24f,4),stone);
  Box("Cartel de bienvenida",new Vector3(0,3,-25),new Vector3(12,2,.4f),pink);
  Sign("PLAZA DEL MULTIVERSO\nELIGE TU SIGUIENTE AVENTURA",new Vector3(0,3,-25.23f),.075f);
  // New, playable fourth portal.
  Vector3 gate=new Vector3(24,1,-10);
  Box("Carreras - plataforma",gate+Vector3.down*.06f,new Vector3(11,.25f,9),stone);
  foreach(int side in new[]{-1,1})Box("Carreras - columna",gate+new Vector3(side*3,2.3f,0),new Vector3(.7f,4.6f,.7f),blue);
  Box("Carreras - dintel",gate+Vector3.up*4.5f,new Vector3(6.7f,.7f,1),blue);
  Sign("CARRERAS LOCAS",gate+new Vector3(0,4.5f,-.56f),.075f);
  Model("Vehicle_Sports","Assets/002_Modelos/zombies",new Vector3(22,.96f,-15),1.5f,-35,h.zombieAtlas);
  for(int i=0;i<4;i++)Model("TrafficCone_1","Assets/002_Modelos/zombies",new Vector3(19+i*2,.96f,-17),.7f,0,h.zombieAtlas);
  Model("Spaceship_FinnTheFrog","Assets/002_Modelos/espacio",new Vector3(-23,.96f,33),4f,150,space);
  Box("Exhibición espacial",new Vector3(-23,.85f,33),new Vector3(14,.16f,10),stone);
  Sign("EXPLORA EL MULTIVERSO",new Vector3(-23,5.7f,33),.07f);
  Model("Vehicle_Truck","Assets/002_Modelos/zombies",new Vector3(5,.96f,29),2.4f,100,h.zombieAtlas);
  Model("TrafficBarrier_1","Assets/002_Modelos/zombies",new Vector3(0,.96f,27),1f,0,h.zombieAtlas);
  // Give all existing decorative meshes physical surfaces and correct buried bases.
  foreach(Transform item in h.transform)
  {
   if(!item.name.StartsWith("Decorado "))continue;
   if(!item.name.Contains("Ship") && !item.name.Contains("Shark"))
   {
    var bounds=PirateArenaVisuals.BoundsOf(item.gameObject);item.position+=Vector3.up*(.96f-bounds.min.y);
   }
   Solid(item.gameObject);
  }
  RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=220;
  Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  var scenes=EditorBuildSettings.scenes.ToList();
  const string race="Assets/001_Scenes/CarrerasLocas.unity";
  if(!scenes.Any(s=>s.path==race))scenes.Add(new EditorBuildSettingsScene(race,true));
  EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
  return "Expanded hub saved: "+root.childCount+" additions; continuous floor, decoration collisions and race portal";
 }
}
