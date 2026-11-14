using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
public static class ExpandSolidRace
{
 const float Factor=1.7f;
 const string Folder="Assets/003_Scripts/CrazyRacers/Generated/";
 static Vector3[] old,newPath;
 static float[] widths;
 static Transform added;
 static Vector3 Scale(Vector3 p)=>new Vector3(p.x*Factor,p.y,p.z*Factor);
 static float Lift(int i){
  if(i>=18&&i<=38)return 4*Mathf.Min(Mathf.SmoothStep(0,1,(i-18)/6f),Mathf.SmoothStep(0,1,(38-i)/6f));
  if(i>=72&&i<=92)return 3*Mathf.Pow(Mathf.Sin((i-72)*Mathf.PI/20),2);
  return 0;
 }
 static Vector3 Side(int i){var f=newPath[(i+1)%96]-newPath[(i+95)%96];f.y=0;return Vector3.Cross(Vector3.up,f.normalized);}
 static Mesh Volume(string name,Vector3[] centers,float[] half,bool loop,float thickness)
 {
  int n=centers.Length,segments=loop?n:n-1,topCount=n*2;
  var vertices=new Vector3[topCount*2];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
  for(int i=0;i<n;i++){
   Vector3 f=centers[loop?(i+1)%n:Mathf.Min(i+1,n-1)]-centers[loop?(i+n-1)%n:Mathf.Max(0,i-1)];f.y=0;
   var side=Vector3.Cross(Vector3.up,f.normalized);
   vertices[i*2]=centers[i]-side*half[i]+Vector3.up*.035f;vertices[i*2+1]=centers[i]+side*half[i]+Vector3.up*.035f;
   vertices[topCount+i*2]=vertices[i*2]-Vector3.up*thickness;vertices[topCount+i*2+1]=vertices[i*2+1]-Vector3.up*thickness;
   uv[i*2]=new Vector2(0,i);uv[i*2+1]=new Vector2(1,i);
  }
  for(int i=0;i<segments;i++){
   int a=i*2,b=((i+1)%n)*2,A=a+topCount,B=b+topCount;
   triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});
   triangles.AddRange(new[]{A,A+1,B,A+1,B+1,B});
   triangles.AddRange(new[]{a,A,B,a,B,b});
   triangles.AddRange(new[]{a+1,b+1,B+1,a+1,B+1,A+1});
  }
  if(!loop){
   int e=topCount-2;
   triangles.AddRange(new[]{0,1,topCount+1,0,topCount+1,topCount});
   triangles.AddRange(new[]{e,e+topCount,e+topCount+1,e,e+topCount+1,e+1});
  }
  var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.uv=uv;mesh.RecalculateBounds();mesh.RecalculateNormals();
  AssetDatabase.CreateAsset(mesh,Folder+name+".asset");return mesh;
 }
 static void SetSurface(GameObject go,Vector3[] centers,float[] half,bool loop,string asset)
 {
  go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;go.transform.localScale=Vector3.one;
  var mesh=Volume(asset,centers,half,loop,.7f);go.GetComponent<MeshFilter>().sharedMesh=mesh;
  var collider=go.GetComponent<MeshCollider>();if(collider==null)collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;
  var surface=go.GetComponent<CrazyKartRoadSurface>();if(surface==null)surface=go.AddComponent<CrazyKartRoadSurface>();
  surface.centers=centers;surface.halfWidths=half;surface.loop=loop;
 }
 static GameObject Box(string name,Vector3 p,Vector3 size,Material m,bool solid=true){
  var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(added,false);o.transform.position=p;o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=m;
  if(!solid)Object.DestroyImmediate(o.GetComponent<Collider>());return o;
 }
 static void Sign(string text,Vector3 p,Vector3 f){
  var o=new GameObject(text,typeof(TextMesh));o.transform.SetParent(added,false);o.transform.SetPositionAndRotation(p,Quaternion.LookRotation(f));
  var t=o.GetComponent<TextMesh>();t.text=text;t.fontSize=90;t.characterSize=.1f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=Color.yellow;
 }
 static void Model(string name,Vector3 p,float height,float yaw,Material material){
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/002_Modelos/piratas/FBX/"+name+".fbx");
  if(asset==null)throw new Exception("Missing "+name);
  var go=new GameObject(name+" XL");go.transform.SetParent(added,false);go.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
  PirateArenaVisuals.AddModel(asset,go.transform,height,material,false);
  foreach(var mf in go.GetComponentsInChildren<MeshFilter>())if(mf.sharedMesh!=null&&mf.GetComponent<Collider>()==null)mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;
 }
 public static string Run(){
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(Application.isPlaying||scene.path!="Assets/001_Scenes/CarrerasLocas.unity")throw new Exception("Race edit mode");
  if(GameObject.Find("Costa XL - recuperación y zonas")!=null)return "Already expanded";
  var road=GameObject.Find("Pista continua");var source=road.GetComponent<MeshFilter>().sharedMesh.vertices;
  old=new Vector3[96];newPath=new Vector3[96];widths=new float[96];
  for(int i=0;i<96;i++){
   old[i]=road.transform.TransformPoint((source[i*2]+source[i*2+1])*.5f)-Vector3.up*.035f;
   newPath[i]=Scale(old[i])+Vector3.up*Lift(i);
   float taper=i>=20&&i<=36?Mathf.Min(Mathf.SmoothStep(0,1,(i-20)/4f),Mathf.SmoothStep(0,1,(36-i)/4f)):0;
   widths[i]=Mathf.Lerp(6,3.5f,taper);
  }
  var deck=GameObject.Find("Tablero elevado con rampas");
  var dv=deck.GetComponent<MeshFilter>().sharedMesh.vertices;
  var bridge=new Vector3[dv.Length/2];for(int i=0;i<bridge.Length;i++)bridge[i]=Scale(deck.transform.TransformPoint((dv[2*i]+dv[2*i+1])*.5f)-Vector3.up*.035f);
  var track=GameObject.Find("Circuito Costa del Caos").transform;
  // Move world objects, retaining car and model scale. Never scale the vehicle physics.
  foreach(var sceneRoot in scene.GetRootGameObjects()){
   if(!sceneRoot.activeSelf)continue;
   foreach(Transform child in sceneRoot.transform){
    var original=child.position;
    child.position=Scale(original);
    bool attach=child.name=="Marca central"||child.name.StartsWith("Puerta de vuelta ")||child.name=="Caja sorpresa"||child.name=="Turbo de pista"||child.name==">>>";
    if(attach){int index=Enumerable.Range(0,96).OrderBy(i=>Vector2.Distance(new Vector2(original.x,original.z),new Vector2(old[i].x,old[i].z))).First();child.position+=Vector3.up*Lift(index);}
    if(child.name.StartsWith("Isla suelo")||child.name.StartsWith("Jardin central")||child.name=="Mar tropical"){
     var scale=child.localScale;child.localScale=new Vector3(scale.x*Factor,scale.y,scale.z*Factor);
    }
    if(child.name=="Columna meta"||child.name=="Arco de salida"||child.name=="COSTA DEL CAOS"||child.name=="Cuadros de meta")
       child.position=newPath[0]+(original-old[0]);
   }
  }
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();var so=new SerializedObject(d);var wp=so.FindProperty("waypoints");
  for(int i=0;i<32;i++){wp.GetArrayElementAtIndex(i).vector3Value=newPath[i*3];track.Find("Puerta de vuelta "+i).position=newPath[i*3]+Vector3.up;}
  so.ApplyModifiedPropertiesWithoutUndo();
  SetSurface(road,newPath,widths,true,"CostaXL_CarreteraSolida");
  SetSurface(deck,bridge,Enumerable.Repeat(2f,bridge.Length).ToArray(),false,"CostaXL_PuenteSolido");
  foreach(string borderName in new[]{"Borde izquierdo","Borde derecho"}){
   var go=GameObject.Find(borderName);bool left=borderName=="Borde izquierdo";
   var centers=new Vector3[96];for(int i=0;i<96;i++)centers[i]=newPath[i]+Side(i)*(left?-1:1)*(widths[i]+.3f)+Vector3.up*.01f;
   var mesh=Volume("CostaXL_"+borderName,centers,Enumerable.Repeat(.3f,96).ToArray(),true,.035f);
   go.GetComponent<MeshFilter>().sharedMesh=mesh;
  }
  for(int i=0;i<96;i++)foreach(int sign in new[]{-1,1}){
   var b=track.Find("Barrera "+i+" "+sign);if(b==null)continue;
   Vector3 a=newPath[i]+Side(i)*sign*(widths[i]+1.3f),next=newPath[(i+1)%96]+Side((i+1)%96)*sign*(widths[(i+1)%96]+1.3f);
   b.position=(a+next)*.5f+Vector3.up*.6f;b.rotation=Quaternion.LookRotation(next-a);b.localScale=new Vector3(.55f,1.2f,Vector3.Distance(a,next)+.35f);
   if((i>=24&&i<=31)||(i>=78&&i<=85&&sign==-1))b.gameObject.SetActive(false);
  }
  var shortcut=Object.FindAnyObjectByType<CrazyKartShortcut>();shortcut.start=Scale(shortcut.start);shortcut.end=Scale(shortcut.end);
  // Scaling the bends changes where the shortcut cuts the inner barriers.
  for(int i=53;i<=67;i++){
   var b=track.Find("Barrera "+i+" 1");if(b==null)continue;Vector3 p=b.position;p.y=shortcut.start.y;
   Vector3 delta=shortcut.end-shortcut.start;float t=Mathf.Clamp01(Vector3.Dot(p-shortcut.start,delta)/delta.sqrMagnitude);
   if(Vector3.Distance(p,shortcut.start+delta*t)<4.4f)b.gameObject.SetActive(false);
  }
  added=new GameObject("Costa XL - recuperación y zonas").transform;
  var rockMat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Atlas isla.mat");
  var stone=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Asfalto.mat");
  var water=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Mar.mat");
  var yellow=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Amarillo.mat");
  foreach(int i in new[]{24,28,32,78,82,86}){
   var p=newPath[i];Box("Pilar del viaducto",new Vector3(p.x,(p.y-.7f)*.5f,p.z),new Vector3(1.4f,Mathf.Max(.1f,p.y-.7f),1.4f),stone);
  }
  for(int i=0;i<6;i++){
   var p=newPath[25+i]+Side(25+i)*(13+i%2*3);p.y=0;Model("Environment_Rock_"+(i%5+1),p,4+i%3,37*i,rockMat);
  }
  var village=newPath[8]+Side(8)*24;village.y=0;
  for(int i=0;i<3;i++)Model("Environment_House"+(i+1),village+new Vector3(i*9,0,i%2*8),6,150,rockMat);
  var lagoon=newPath[82]-Side(82)*12;lagoon.y=.02f;Box("Laguna del naufragio",lagoon,new Vector3(28,.025f,26),water,false);
  var ship=lagoon-Side(82)*8;ship.y=.05f;Model("Ship_Small",ship,6,80,rockMat);
  Sign("PASARELA DE LA CANTERA\nSIN BARANDILLAS",newPath[22]+Vector3.up*3,newPath[23]-newPath[22]);
  Sign("CURVA DE LA LAGUNA\nCUIDADO CON EL BORDE",newPath[76]+Vector3.up*3,newPath[77]-newPath[76]);
  Sign("CAÍDA = VOLVER AL ÚLTIMO CONTROL",newPath[53]+Vector3.up*3,newPath[54]-newPath[53]);
  // Edge markers communicate the deliberate openings without adding invisible walls.
  foreach(int i in new[]{24,26,28,30,78,80,82,84})foreach(int s in new[]{-1,1}){
   var p=newPath[i]+Side(i)*s*(widths[i]-.15f)+Vector3.up*.08f;
   Box("Borde de precaución",p,new Vector3(.3f,.08f,1.5f),yellow,false).transform.rotation=Quaternion.LookRotation(newPath[(i+1)%96]-newPath[i]);
  }
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  float length=0,previous=0;for(int i=0;i<96;i++){length+=Vector3.Distance(newPath[i],newPath[(i+1)%96]);previous+=Vector3.Distance(old[i],old[(i+1)%96]);}
  return "Saved closed road and bridge volumes. Lap "+previous.ToString("F0")+" -> "+length.ToString("F0")+" m. Quarry walkway, lagoon ledge, village and automatic recovery.";
 }
}
