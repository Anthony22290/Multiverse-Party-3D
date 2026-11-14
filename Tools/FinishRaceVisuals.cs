using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class FinishRaceVisuals
{
 public static string Run()
 {
  if(Application.isPlaying)throw new System.Exception("Edit mode required");
  var scene=EditorSceneManager.OpenScene("Assets/001_Scenes/CarrerasLocas.unity");
  foreach(var name in new[]{"Isla suelo continuo","Jardin central"})
  {
   var old=GameObject.Find(name);
   if(GameObject.Find(name+" - costa redondeada")!=null)continue;
   var disc=GameObject.CreatePrimitive(PrimitiveType.Cylinder);disc.name=name+" - costa redondeada";
   disc.transform.SetParent(old.transform.parent,false);disc.transform.position=old.transform.position;
   var size=old.transform.localScale;disc.transform.localScale=new Vector3(size.x,size.y*.5f,size.z);
   disc.GetComponent<Renderer>().sharedMaterial=old.GetComponent<Renderer>().sharedMaterial;
   Object.DestroyImmediate(disc.GetComponent<Collider>());old.GetComponent<Renderer>().enabled=false;
  }
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.Refresh();return "Coast rounded; structural collider preserved";
 }
}
