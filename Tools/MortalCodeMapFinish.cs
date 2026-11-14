using System;
using System.Linq;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class MortalCodeMapFinish
{
 public static string Run()
 {
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(Application.isPlaying||scene.path!="Assets/001_Scenes/CodigoMortal.unity")throw new Exception("Wrong scene");
  var world=GameObject.Find("Plaza Vértigo · mapa").transform;
  var teal=AssetDatabase.LoadAssetAtPath<Material>("Assets/003_Scripts/MortalCode/Generated/Menta.mat");
  foreach(var t in world.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Borde ")).ToArray())Object.DestroyImmediate(t.gameObject);
  for(int side=-1;side<=1;side+=2)for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
  {
   var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name="Baranda galería segmentada";b.transform.SetParent(world,false);
   b.transform.position=new Vector3(side*35+x*13,6.6f,12+z*10);b.transform.localScale=new(.2f,1.2f,9);b.GetComponent<Renderer>().sharedMaterial=teal;
  }
  var nav=world.GetComponent<NavMeshSurface>();var old=nav.navMeshData;nav.BuildNavMesh();
  EditorUtility.CopySerialized(nav.navMeshData,old);Object.DestroyImmediate(nav.navMeshData);nav.navMeshData=old;nav.RemoveData();nav.AddData();EditorUtility.SetDirty(old);
  EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return "Bridge entrances opened; navigation rebaked";
 }
 public static string Hub()
 {
  var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)throw new Exception("Unsaved scene");
  var s=EditorSceneManager.OpenScene("Assets/001_Scenes/MundoDeInicio.unity");
  var hub=Object.FindAnyObjectByType<MultiverseHub>();
  if(hub.transform.Find("Portal Código Mortal")==null)
  {
   var root=new GameObject("Portal Código Mortal").transform;root.SetParent(hub.transform,false);
   void Box(string name,Vector3 p,Vector3 size,Material m){var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name=name;b.transform.SetParent(root,false);b.transform.position=p;b.transform.localScale=size;b.GetComponent<Renderer>().sharedMaterial=m;}
   Box("Isla del código",new(-24,0,-10),new(12,1.8f,12),hub.darkWood);
   var a=new Vector3(-6,0,-5);var b=new Vector3(-24,0,-10);
   Box("Pasarela al código",(a+b)*.5f,new(4,1.8f,Vector3.Distance(a,b)+2),hub.wood);
   root.Find("Pasarela al código").rotation=Quaternion.LookRotation(b-a);
   for(int side=-1;side<=1;side+=2)Box("Pilar",new(-24+side*2.8f,3,-10),new(.7f,4,.7f),hub.brass);
   Box("Dintel",new(-24,5,-10),new(6.3f,.7f,.8f),hub.brass);
   var t=new GameObject("CÓDIGO MORTAL",typeof(TextMesh)).GetComponent<TextMesh>();t.transform.SetParent(root,false);t.transform.SetPositionAndRotation(new(-24,5.8f,-10.5f),Quaternion.identity);
   t.text="CÓDIGO MORTAL";t.fontSize=80;t.characterSize=.065f;t.anchor=TextAnchor.MiddleCenter;t.color=Color.white;
  }
  EditorSceneManager.SaveScene(s);EditorSceneManager.OpenScene("Assets/001_Scenes/CodigoMortal.unity");return "Fifth hub portal saved";
 }
}
