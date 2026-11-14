using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class FixShortcutJoins
{
 public static string Run()
 {
  if(Application.isPlaying)throw new System.Exception("Edit mode");
  var shortcut=Object.FindAnyObjectByType<CrazyKartShortcut>();var a=shortcut.start;var b=shortcut.end;
  Vector3 f=(b-a).normalized,side=Vector3.Cross(Vector3.up,f);
  int removed=0;
  for(int i=53;i<=67;i++){
   var barrier=GameObject.Find("Barrera "+i+" 1");if(barrier==null)continue;
   Vector3 p=barrier.transform.position;p.y=a.y;
   float t=Mathf.Clamp01(Vector3.Dot(p-a,b-a)/(b-a).sqrMagnitude);
   if(Vector3.Distance(p,Vector3.Lerp(a,b,t))<4.4f){barrier.SetActive(false);removed++;}
  }
  shortcut.GetComponent<BoxCollider>().enabled=false;shortcut.GetComponent<Renderer>().enabled=false;
  var verts=new Vector3[26];var uv=new Vector2[26];var tris=new int[72];
  for(int i=0;i<=12;i++){
   float t=i/12f;var p=Vector3.Lerp(a,b,t);
   p.y+=1.2f*Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Min(t,1-t)/.25f));
   verts[2*i]=p-side*2+Vector3.up*.035f;verts[2*i+1]=p+side*2+Vector3.up*.035f;
   uv[2*i]=new Vector2(0,i);uv[2*i+1]=new Vector2(1,i);
   if(i<12){int q=i*6,j=i*2;tris[q]=j;tris[q+1]=j+2;tris[q+2]=j+1;tris[q+3]=j+1;tris[q+4]=j+2;tris[q+5]=j+3;}
  }
  var mesh=new Mesh{name="Puente con rampas"};mesh.vertices=verts;mesh.triangles=tris;mesh.uv=uv;mesh.RecalculateBounds();mesh.RecalculateNormals();
  // Winding faces upward for the chosen deck side order.
  if(mesh.normals[0].y<0){for(int i=0;i<tris.Length;i+=3){int j=tris[i+1];tris[i+1]=tris[i+2];tris[i+2]=j;}mesh.triangles=tris;mesh.RecalculateNormals();}
  AssetDatabase.CreateAsset(mesh,"Assets/003_Scripts/CrazyRacers/Generated/PuenteRampas.asset");
  var go=new GameObject("Tablero elevado con rampas",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
  go.transform.SetParent(shortcut.transform.parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=shortcut.GetComponent<Renderer>().sharedMaterial;
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
  return "Removed "+removed+" joining barriers; continuous shortcut ramps saved";
 }
}
