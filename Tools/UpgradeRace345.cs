using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class UpgradeRace345
{
 static Vector3[] oldPath=new Vector3[96],path=new Vector3[96];
 static string folder="Assets/003_Scripts/CrazyRacers/Generated/";
 static Transform root;
 static Vector3 Side(int i){Vector3 f=path[(i+1)%96]-path[(i+95)%96];f.y=0;return Vector3.Cross(Vector3.up,f.normalized);}
 static void MeshRibbon(GameObject go,float inside,float outside,float height,bool solid)
 {
  var vertices=new Vector3[192];var tris=new int[576];var uv=new Vector2[192];
  for(int i=0;i<96;i++){
   Vector3 side=Side(i);vertices[2*i]=path[i]+side*inside+Vector3.up*height;vertices[2*i+1]=path[i]+side*outside+Vector3.up*height;
   uv[2*i]=new Vector2(0,i);uv[2*i+1]=new Vector2(1,i);int a=i*2,b=((i+1)%96)*2;
   tris[i*6]=a;tris[i*6+1]=b;tris[i*6+2]=a+1;tris[i*6+3]=a+1;tris[i*6+4]=b;tris[i*6+5]=b+1;
  }
  var mesh=new Mesh{name=go.name+" V2"};mesh.vertices=vertices;mesh.triangles=tris;mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();
  var existing=AssetDatabase.LoadAssetAtPath<Mesh>(folder+go.name+" V2.asset");if(existing==null)AssetDatabase.CreateAsset(mesh,folder+go.name+" V2.asset");else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}go.GetComponent<MeshFilter>().sharedMesh=mesh;
  if(solid){var collider=go.GetComponent<MeshCollider>();if(collider==null)collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;}
 }
 static GameObject Box(string name,Vector3 p,Vector3 size,Material mat,Quaternion rot)
 {
  var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(root,false);o.transform.SetPositionAndRotation(p,rot);o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=mat;return o;
 }
 static void Sign(string text,Vector3 p,Quaternion rot)
 {
  var o=new GameObject(text,typeof(TextMesh));o.transform.SetParent(root,false);o.transform.SetPositionAndRotation(p,rot);
  var t=o.GetComponent<TextMesh>();t.text=text;t.characterSize=.10f;t.fontSize=80;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=new Color(1,.85f,.1f);
 }
 static void Paint(CrazyKartController kart,int index)
 {
  var renderer=kart.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="Sports");
  var original=renderer.sharedMaterial;var source=original.GetTexture("_BaseMap") as Texture2D;
  if(source==null)throw new Exception("Missing car atlas");
  var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
  var previous=RenderTexture.active;Graphics.Blit(source,rt);RenderTexture.active=rt;
  var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0);texture.Apply();
  RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
  var pixels=texture.GetPixels();Color.RGBToHSV(kart.KartColor,out float hue,out float saturation,out float value);
  for(int i=0;i<pixels.Length;i++){
   Color.RGBToHSV(pixels[i],out float h,out float s,out float v);
   if(h>.095f&&h<.19f&&s>.4f&&v>.25f)pixels[i]=Color.HSVToRGB(hue,Mathf.Max(.65f,saturation),v);
  }
  texture.SetPixels(pixels);texture.Apply();
  string texturePath=folder+"CarPaint"+index+".png";System.IO.File.WriteAllBytes(texturePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);
  AssetDatabase.ImportAsset(texturePath);
  var mat=new Material(original){name="Pintura "+kart.DisplayName};mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
  AssetDatabase.CreateAsset(mat,folder+"CarPaint"+index+".mat");renderer.sharedMaterial=mat;
 }
 public static string Run()
 {
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(Application.isPlaying||scene.path!="Assets/001_Scenes/CarrerasLocas.unity")throw new Exception("Race edit mode required");
  if(GameObject.Find("Ruta V2 - puente y curvas")!=null)return "Already upgraded";
  root=new GameObject("Ruta V2 - puente y curvas").transform;
  for(int i=0;i<96;i++){
   float a=i*Mathf.PI*2/96;oldPath[i]=new Vector3(Mathf.Sin(a)*43+Mathf.Sin(a*2)*7,0,Mathf.Cos(a)*60);path[i]=oldPath[i];
  }
  for(int i=0;i<96;i++){
   if(i>=18&&i<=42){float t=(i-18)/24f;path[i].x+=7*Mathf.Sin(t*Mathf.PI*2)*Mathf.Pow(Mathf.Sin(t*Mathf.PI),2);}
   if(i>=48&&i<=72){float t=(i-48)/24f;path[i].y=3*Mathf.Pow(Mathf.Sin(t*Mathf.PI),2);}
   if(i>=54&&i<=66){float t=(i-54)/12f;Vector3 f=oldPath[(i+1)%96]-oldPath[(i+95)%96];Vector3 side=Vector3.Cross(Vector3.up,f.normalized);path[i]-=side*12*Mathf.Pow(Mathf.Sin(t*Mathf.PI),2);}
  }
  var track=GameObject.Find("Circuito Costa del Caos").transform;
  foreach(Transform child in track){
   string n=child.name;
   bool roadObject=n.StartsWith("Barrera ")||n=="Marca central"||n.StartsWith("Puerta de vuelta ")||n=="Caja sorpresa"||n=="Turbo de pista"||n==">>>";
   bool decor=n.StartsWith("Environment_");
   if(!roadObject&&!decor)continue;
   int nearest=Enumerable.Range(0,96).OrderBy(i=>(new Vector2(child.position.x-oldPath[i].x,child.position.z-oldPath[i].z)).sqrMagnitude).First();
   Vector3 delta=path[nearest]-oldPath[nearest];if(decor)delta.y=0;
   var before=oldPath[(nearest+1)%96]-oldPath[(nearest+95)%96];var after=path[(nearest+1)%96]-path[(nearest+95)%96];before.y=after.y=0;
   child.position+=delta;child.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(before,after,Vector3.up),Vector3.up)*child.rotation;
  }
  // Match every barrier to the new span, closing gaps through the curves and hills.
  for(int i=0;i<96;i++)foreach(int side in new[]{-1,1}){
   var barrier=GameObject.Find("Barrera "+i+" "+side);if(barrier==null)continue;
   Vector3 a=path[i]+Side(i)*side*7.3f,b=path[(i+1)%96]+Side((i+1)%96)*side*7.3f;
   barrier.transform.position=(a+b)*.5f+Vector3.up*.6f;barrier.transform.rotation=Quaternion.LookRotation(b-a);
   barrier.transform.localScale=new Vector3(.55f,1.2f,Vector3.Distance(a,b)+.35f);
   if(side==1&&((i>=53&&i<=55)||(i>=64&&i<=66)))barrier.SetActive(false);
  }
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();var so=new SerializedObject(d);var points=so.FindProperty("waypoints");points.arraySize=32;
  for(int i=0;i<32;i++){
   points.GetArrayElementAtIndex(i).vector3Value=path[i*3];
   var cp=GameObject.Find("Puerta de vuelta "+i);cp.transform.position=path[i*3]+Vector3.up;
  }so.ApplyModifiedPropertiesWithoutUndo();
  MeshRibbon(GameObject.Find("Pista continua"),-6,6,.035f,true);
  MeshRibbon(GameObject.Find("Borde izquierdo"),-6.7f,-6,.042f,false);
  MeshRibbon(GameObject.Find("Borde derecho"),6,6.7f,.042f,false);
  var yellow=AssetDatabase.LoadAssetAtPath<Material>(folder+"Amarillo.mat");
  var coral=AssetDatabase.LoadAssetAtPath<Material>(folder+"Coral.mat");
  var a0=path[54];var b0=path[66];var f0=(b0-a0).normalized;
  var bridge=Box("Atajo - puente estrecho", (a0+b0)*.5f+Vector3.down*.12f,new Vector3(4,.3f,Vector3.Distance(a0,b0)+4),yellow,Quaternion.LookRotation(f0));
  var shortcut=bridge.AddComponent<CrazyKartShortcut>();shortcut.start=a0;shortcut.end=b0;shortcut.halfWidth=2;
  for(int i=1;i<=3;i++){
   var p=Vector3.Lerp(a0,b0,i/4f);
   var cp=new GameObject("Atajo control "+(18+i),typeof(BoxCollider),typeof(CrazyKartCheckpointIndex));cp.transform.SetParent(root,false);cp.transform.SetPositionAndRotation(p+Vector3.up,Quaternion.LookRotation(f0));
   cp.GetComponent<BoxCollider>().size=new Vector3(4.6f,2.5f,1.4f);cp.GetComponent<BoxCollider>().isTrigger=true;cp.GetComponent<CrazyKartCheckpointIndex>().index=18+i;
  }
  // Narrow deck, no rails: missing the line drops onto slow sand below.
  for(int i=1;i<4;i++){var p=Vector3.Lerp(a0,b0,i/4f);Box("Soporte puente",new Vector3(p.x,.65f,p.z),new Vector3(.65f,1.3f,.65f),coral,Quaternion.identity);}
  Sign("ATAJO\nESTRECHO - SIN BARANDILLA",a0+Vector3.up*3,Quaternion.LookRotation(f0));
  Sign("MIRADOR",path[60]+Vector3.up*3,Quaternion.LookRotation(path[61]-path[60]));
  var karts=Object.FindObjectsByType<CrazyKartController>().OrderBy(k=>k.DisplayName).ToArray();
  for(int i=0;i<karts.Length;i++)Paint(karts[i],i);
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  float main=0;for(int i=54;i<66;i++)main+=Vector3.Distance(path[i],path[i+1]);
  return "Saved V2: height 3 m, curves, shortcut "+Vector3.Distance(a0,b0).ToString("F1")+" m vs main "+main.ToString("F1")+" m; 4 car liveries";
 }
}

