using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using Object=UnityEngine.Object;
public static class PolishRaceDecor
{
 public static string Run()
 {
  if(Application.isPlaying)throw new System.Exception("Stop Play");
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(scene.path!="Assets/001_Scenes/CarrerasLocas.unity")throw new System.Exception("Open race");
  if(GameObject.Find("Señalización Costa del Caos")!=null)return "Already dressed";
  var root=new GameObject("Señalización Costa del Caos").transform;
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();
  var coral=AssetDatabase.LoadAssetAtPath<Material>("Assets/003_Scripts/CrazyRacers/Generated/Coral.mat");
  var dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/003_Scripts/CrazyRacers/Generated/Asfalto.mat");
  for(int i=4;i<32;i+=8)
  {
   var p=d.GetWaypointPosition(i);var f=(d.GetWaypointPosition(i+1)-p).normalized;var side=Vector3.Cross(Vector3.up,f);
   var rot=Quaternion.LookRotation(f);var center=p-side*9+Vector3.up*2.2f;
   var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.name="Panel de curva "+i;board.transform.SetParent(root,false);
   board.transform.SetPositionAndRotation(center,rot);board.transform.localScale=new Vector3(4,1.5f,.2f);board.GetComponent<Renderer>().sharedMaterial=dark;
   var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.name="Soporte señal";post.transform.SetParent(root,false);post.transform.position=center-Vector3.up*1.3f;
   post.transform.localScale=new Vector3(.2f,2.6f,.2f);post.GetComponent<Renderer>().sharedMaterial=coral;
   var t=new GameObject("Flechas de curva",typeof(TextMesh));t.transform.SetParent(root,false);t.transform.SetPositionAndRotation(center-f*.12f,rot);
   var text=t.GetComponent<TextMesh>();text.text=">>>";text.fontSize=80;text.characterSize=.18f;text.color=new Color(1,.85f,.1f);text.anchor=TextAnchor.MiddleCenter;
  }
  var source=AssetDatabase.FindAssets("Barrel t:Model",new[]{"Assets/002_Modelos/zombies"}).Select(AssetDatabase.GUIDToAssetPath).First(p=>p.EndsWith("/Barrel.fbx"));
  var atlas=AssetDatabase.LoadAssetAtPath<Material>("Assets/003_Scripts/CrazyRacers/Generated/Atlas coches.mat");
  for(int i=0;i<4;i++)
  {
   var o=new GameObject("Barril de boxes");o.transform.SetParent(root,false);o.transform.position=new Vector3(13+i*1.3f,0,38);
   PirateArenaVisuals.AddModel(AssetDatabase.LoadAssetAtPath<GameObject>(source),o.transform,1.1f,atlas,false);
   foreach(var m in o.GetComponentsInChildren<MeshFilter>())if(m.GetComponent<Collider>()==null)m.gameObject.AddComponent<MeshCollider>().sharedMesh=m.sharedMesh;
  }
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);return "Added readable corner boards and pit props outside racing surface";
 }
}
