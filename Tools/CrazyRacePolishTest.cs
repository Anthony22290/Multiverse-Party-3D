using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object=UnityEngine.Object;
public static class CrazyRacePolishTest
{
 static List<string> output=new List<string>();
 static void Check(bool ok,string text)=>output.Add((ok?"PASS ":"FAIL ")+text);
 static async Task Wait(float sec){float end=Time.time+sec;var limit=DateTime.UtcNow.AddSeconds(sec*5+5);while(Time.time<end&&DateTime.UtcNow<limit)await Task.Delay(20);if(Time.time<end)throw new Exception("No frames");}
 public static async Task<string[]> Run()
 {
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();var p=d.Karts.First(k=>k.IsHuman);
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;var type=typeof(CrazyKartController);
  foreach(var bot in d.Karts.Where(k=>k!=p))bot.gameObject.SetActive(false);
  try{
   await Wait(.1f);Check(p.GetComponent<CrazyKartFeedback>()!=null,"Presentation component active");
   Check(p.GetComponents<AudioSource>().Length==2&&p.GetComponents<AudioSource>().Any(a=>a.isPlaying&&a.clip!=null),"Original engine loop playing");
   type.GetField("<HeldItem>k__BackingField",flags).SetValue(p,0);p.GiveItem(4);p.UseItem();await Wait(.1f);
   Check(p.GetComponentInChildren<LineRenderer>().enabled,"Shield visible on kart");
   p.Hit();p.Hit();await Wait(.1f);
   Check(p.Stunned&&p.GetComponentInChildren<LineRenderer>().enabled,"Impact visible on kart");
   type.GetField("driftCharge",flags).SetValue(p,1f);type.GetField("wasDrifting",flags).SetValue(p,true);await Wait(.08f);
   Check(!p.Boosting,"Interrupting drift while stunned cannot award turbo");
   await Wait(1.08f);bool recovered=!p.Stunned;p.Hit();
   Check(recovered&&!p.Stunned,"Recovery protection prevents immediate stun chain");
   await Wait(.7f);p.Hit();Check(p.Stunned,"Impact applies again after protection ends");
   await Wait(1.8f);p.ApplyBoost(2);
   p.GetComponent<Rigidbody>().linearVelocity=p.transform.forward*12;await Wait(.08f);
   Check(p.GetComponentsInChildren<TrailRenderer>().All(t=>t.emitting),"Boost activates visible wheel trails");
   var cam=Object.FindAnyObjectByType<CrazyKartCamera>();
   var focus=p.transform.position+Vector3.up*1.1f;
   var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name="Temporary camera wall";
   block.transform.position=p.transform.position-p.transform.forward*4+Vector3.up*3;
   block.transform.rotation=p.transform.rotation;block.transform.localScale=new Vector3(10,8,.4f);
   p.enabled=false;p.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;
   Physics.SyncTransforms();await Wait(.7f);
   Check(!Physics.Linecast(focus,cam.transform.position,out var hit,~0,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<CrazyKartController>()==p,"Follow camera stays on near side of obstruction");
   Object.Destroy(block);p.enabled=true;
   p.CurrentLap=3;await Wait(.1f);
   Check(GameObject.Find("Avisos").GetComponent<UnityEngine.UI.Text>().text.Contains("ÚLTIMA"),"Last-lap notice appears");
   Check(GameObject.Find("Progreso derrape")!=null,"Drift charge bar exists");
  }finally{
   p.enabled=true;foreach(var bot in d.Karts.Where(k=>k!=p))bot.gameObject.SetActive(true);
   var block=GameObject.Find("Temporary camera wall");if(block!=null)Object.Destroy(block);
   File.WriteAllLines("Docs/CrazyRace_PolishTest_2026-11-03.txt",output);
  }
  return output.ToArray();
 }
}
