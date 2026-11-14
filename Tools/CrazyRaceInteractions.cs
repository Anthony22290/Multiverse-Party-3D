using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using Object=UnityEngine.Object;
public static class CrazyRaceInteractions
{
 static List<string> results=new List<string>();
 static void Check(bool ok,string s)=>results.Add((ok?"PASS ":"FAIL ")+s);
 static async Task Wait(float sec){float end=Time.time+sec;var dead=DateTime.UtcNow.AddSeconds(sec*4+5);while(Time.time<end&&DateTime.UtcNow<dead)await Task.Delay(20);if(Time.time<end)throw new Exception("Player loop stalled");}
 public static async Task<string[]> Run()
 {
  results.Clear();var d=Object.FindAnyObjectByType<CrazyKartDirector>();var a=d.Karts.First(k=>k.IsHuman);var b=d.Karts.First(k=>!k.IsHuman);
  Check(d.Karts.Count==4&&d.Karts.Count(k=>k.IsHuman)==1&&!d.Finished&&d.Finishers.Count==0,"Result button reloads a clean race with original roles");
  var ar=a.GetComponent<Rigidbody>();var br=b.GetComponent<Rigidbody>();
  foreach(var k in d.Karts)k.enabled=false;
  try
  {
   var p=d.GetWaypointPosition(8);var f=(d.GetWaypointPosition(9)-p).normalized;
   ar.position=p+Vector3.up*.45f;ar.rotation=Quaternion.LookRotation(f);ar.linearVelocity=Vector3.zero;
   br.position=p+f*8+Vector3.up*.45f;br.linearVelocity=Vector3.zero;
   Physics.SyncTransforms();await Wait(.2f);
   a.GiveItem(2);a.UseItem();await Wait(.3f);
   Check(b.Stunned&&!a.Stunned,"Moving pulse hits rival and ignores owner");
   await Wait(1.8f);
   a.GiveItem(3);a.UseItem();await Wait(.1f);
   br.position=ar.position-f*3;br.linearVelocity=Vector3.zero;Physics.SyncTransforms();await Wait(.2f);
   Check(b.Stunned,"Driving over trap applies impact");
   await Wait(1.8f);
   var pad=Object.FindAnyObjectByType<CrazyKartBoostPad>();ar.position=pad.transform.position+Vector3.up*.35f;Physics.SyncTransforms();await Wait(.2f);
   Check(a.Boosting,"Entering physical boost-pad trigger activates turbo");
   var wall=GameObject.Find("Barrera 0 -1");var col=wall.GetComponent<Collider>();var inward=(p-wall.transform.position).normalized;
   var origin=col.bounds.center+wall.transform.right*3;
   Check(Physics.Raycast(origin,-wall.transform.right,out var hit,5,~0,QueryTriggerInteraction.Ignore)&&hit.collider==col,"Road barrier is a solid physical collider");
  }
  finally{foreach(var k in d.Karts)k.enabled=true;File.WriteAllLines("Docs/CrazyRace_Interactions_2026-11-03.txt",results);}
  return results.ToArray();
 }
}

