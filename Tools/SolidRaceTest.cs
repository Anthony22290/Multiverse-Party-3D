using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;
public static class SolidRaceTest
{
 static List<string> checks=new List<string>();
 static void Check(bool ok,string text)=>checks.Add((ok?"PASS ":"FAIL ")+text);
 static async Task Wait(float sec){float end=Time.time+sec;var deadline=DateTime.UtcNow.AddSeconds(sec*5+6);while(Time.time<end&&DateTime.UtcNow<deadline)await Task.Delay(20);if(Time.time<end)throw new Exception("No advancing frames");}
 static bool Closed(Mesh mesh){
  var edges=new Dictionary<(int,int),int>();var t=mesh.triangles;
  for(int i=0;i<t.Length;i+=3)for(int j=0;j<3;j++){int a=t[i+j],b=t[i+(j+1)%3];var e=(Mathf.Min(a,b),Mathf.Max(a,b));edges[e]=edges.TryGetValue(e,out int c)?c+1:1;}
  return edges.Values.All(c=>c==2);
 }
 public static async Task<string[]> Run()
 {
  checks.Clear();var d=Object.FindAnyObjectByType<CrazyKartDirector>();if(!Application.isPlaying||d==null)throw new Exception("Play race");
  var p=d.Karts.First(k=>k.IsHuman);var rb=p.GetComponent<Rigidbody>();var recovery=p.GetComponent<CrazyKartRecovery>();
  var road=GameObject.Find("Pista continua").GetComponent<CrazyKartRoadSurface>();var bridge=GameObject.Find("Tablero elevado con rampas").GetComponent<CrazyKartRoadSurface>();
  try{
   if(d.Countdown>0)await Wait(d.Countdown+.1f);
   foreach(var other in d.Karts.Where(k=>k!=p))other.gameObject.SetActive(false);
   foreach(var hazard in Object.FindObjectsByType<CrazyKartHazard>())Object.Destroy(hazard.gameObject);
   p.enabled=false;recovery.enabled=false;
   foreach(var surface in new[]{road,bridge}){
    Check(Closed(surface.GetComponent<MeshCollider>().sharedMesh),surface.name+" collision mesh is watertight");
    int index=surface==road?28:6;Vector3 center=surface.centers[index];
    var f=surface.centers[index+1]-surface.centers[index-1];f.y=0;f.Normalize();var side=Vector3.Cross(Vector3.up,f);
    rb.useGravity=false;rb.position=center+Vector3.down*1.4f;rb.rotation=Quaternion.LookRotation(f);rb.linearVelocity=Vector3.up*8;await Wait(.4f);
    Check(rb.position.y<center.y-.85f,surface.name+" blocks upward travel from underneath (recovery OFF), y="+rb.position.y.ToString("F2"));
    float width=surface.halfWidths[index];rb.position=center+side*(width+3)+Vector3.down*.3f;rb.linearVelocity=-side*20;await Wait(.35f);
    Check(Vector3.Dot(rb.position-center,side)>width+.55f,surface.name+" side wall blocks lateral entry below deck");
    rb.useGravity=true;rb.position=center+Vector3.up*3;rb.linearVelocity=Vector3.zero;await Wait(1.2f);
    Check(rb.position.y>center.y+.25f&&rb.position.y<center.y+.65f,surface.name+" catches a falling car on top");
   }
   p.enabled=true;recovery.enabled=true;
   foreach(int index in new[]{28,82}){
    p.CurrentCheckpoint=index/3;p.CurrentLap=1;p.Recover();await Wait(1.4f);
    var f=road.centers[index+1]-road.centers[index-1];f.y=0;var side=Vector3.Cross(Vector3.up,f.normalized);
    float before=p.LastRecoveryTime;
    rb.position=road.centers[index]+side*(road.halfWidths[index]+4)+Vector3.up;rb.linearVelocity=Vector3.zero;
    await Wait(2.2f);
    Check(p.LastRecoveryTime>before&&p.CurrentCheckpoint==index/3&&p.CurrentLap==1,"Automatic recovery from zone "+index+" preserves lap and checkpoint");
   }
   p.CurrentCheckpoint=20;p.Recover();await Wait(1.4f);float stamp=p.LastRecoveryTime;
   var direction=(bridge.centers[7]-bridge.centers[5]).normalized;
   rb.position=bridge.centers[6]+Vector3.Cross(Vector3.up,direction)*5+Vector3.up;rb.linearVelocity=Vector3.zero;
   await Wait(2.2f);
   Check(p.LastRecoveryTime>stamp&&p.CurrentCheckpoint==20,"Bridge fall automatically recovers without attempting to climb through road");
   p.Recover();await Wait(1.4f);stamp=p.LastRecoveryTime;rb.position=new Vector3(300,0,300);rb.linearVelocity=Vector3.zero;await Wait(2);
   Check(p.LastRecoveryTime>stamp&&rb.position.x<150,"Outside-map fall returns safely");
   p.enabled=false;recovery.enabled=false;
   var b=d.Karts.First(k=>k!=p);b.gameObject.SetActive(true);b.enabled=false;
   var br=b.GetComponent<Rigidbody>();br.position=d.GetWaypointPosition(20)+Vector3.up*.45f;br.linearVelocity=Vector3.zero;await Wait(.1f);
   p.Recover();await Wait(.15f);
   Check(Vector3.Distance(rb.position,br.position)>1.6f,"Recovery selects free space when checkpoint is occupied");
   b.enabled=true;
   Check(road.centers.Max(v=>v.x)-road.centers.Min(v=>v.x)>140,"Expanded circuit spans more than 140 m");
  }
  finally{
   rb.useGravity=true;p.enabled=true;recovery.enabled=true;foreach(var other in d.Karts.Where(k=>k!=p))other.gameObject.SetActive(true);
   File.WriteAllLines("Docs/CostaXL_CollisionTests_2026-11-04.txt",checks);
  }
  return checks.ToArray();
 }
}
