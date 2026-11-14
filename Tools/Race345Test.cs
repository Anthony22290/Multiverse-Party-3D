using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
public static class Race345Test
{
 static readonly List<string> checks=new List<string>();
 static void Check(bool ok,string detail)=>checks.Add((ok?"PASS ":"FAIL ")+detail);
 static async Task Wait(float seconds){float end=Time.time+seconds;var max=DateTime.UtcNow.AddSeconds(seconds*6+5);while(Time.time<end&&DateTime.UtcNow<max)await Task.Delay(20);if(Time.time<end)throw new Exception("Frozen play loop");}
 public static async Task<string[]> Run()
 {
  checks.Clear();var d=Object.FindAnyObjectByType<CrazyKartDirector>();if(!Application.isPlaying||d==null)throw new Exception("Race Play required");
  var p=d.Karts.First(k=>k.IsHuman);var rb=p.GetComponent<Rigidbody>();
  var bg=InputSystem.settings.backgroundBehavior;var route=InputSystem.settings.editorInputBehaviorInPlayMode;var old=Keyboard.current;
  InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  var keys=InputSystem.AddDevice<Keyboard>();
  try
  {
   if(d.Countdown>0)await Wait(d.Countdown+.1f);
   foreach(var bot in d.Karts.Where(k=>k!=p))bot.gameObject.SetActive(false);
   Check(d.Waypoints.Max(v=>v.y)>=2.99f,"Road elevation reaches 3 m");
   var shortcut=d.Shortcut;Check(shortcut!=null&&Vector3.Distance(shortcut.start,shortcut.end)<41,"Shortcut is shorter than 50 m detour");
   int floor=0;foreach(var v in d.Waypoints)if(Physics.Raycast(v+Vector3.up*5,Vector3.down,out var hit,10,~0,QueryTriggerInteraction.Ignore)&&Mathf.Abs(hit.point.y-v.y)<.15f)floor++;
   Check(floor==32,"Road collision follows all 32 checkpoints: "+floor);
   p.CurrentLap=1;p.CurrentCheckpoint=18;var f=(shortcut.end-shortcut.start).normalized;
   rb.position=shortcut.start+f*2+Vector3.up*.55f;rb.rotation=Quaternion.LookRotation(f);rb.linearVelocity=Vector3.zero;Physics.SyncTransforms();await Wait(.2f);
   var wheel=p.GetComponentsInChildren<Transform>().First(t=>t.name=="FrontWheel_L");var rest=wheel.localRotation;
   InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));float deadline=Time.time+5;
   while(p.CurrentCheckpoint<22&&Time.time<deadline)await Wait(.04f);
   InputSystem.QueueStateEvent(keys,new KeyboardState());
   Check(p.CurrentCheckpoint>=22&&p.CurrentLap==1,"Driven shortcut crosses 19,20,21,22 without granting lap, actual="+p.CurrentCheckpoint);
   Check(Quaternion.Angle(rest,wheel.localRotation)>5,"Imported wheels rotate while driving");
   // Falling off the narrow bridge lands below its deck, then R recovers to a valid point.
   Vector3 mid=(shortcut.start+shortcut.end)*.5f;Vector3 side=Vector3.Cross(Vector3.up,f);
   rb.position=mid+side*4+Vector3.up*.5f;rb.linearVelocity=Vector3.zero;await Wait(1);
   Check(rb.position.y<mid.y-.5f,"Missing narrow shortcut drops below bridge");
   p.Recover();await Wait(.7f);Check(rb.position.y>.2f,"Recovery returns from shortcut fall");
   p.enabled=false;rb.position=d.GetWaypointPosition(20)+Vector3.up*.5f;rb.rotation=Quaternion.LookRotation(d.GetWaypointPosition(21)-d.GetWaypointPosition(20));rb.linearVelocity=Vector3.zero;await Wait(.1f);
   typeof(CrazyKartController).GetField("<HeldItem>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p,0);
   for(int i=1;i<=4;i++){
    p.GiveItem(i);await Wait(.05f);
    var icon=GameObject.Find("Icono objeto").GetComponent<UnityEngine.UI.Image>();
    var help=GameObject.Find("Uso del objeto").GetComponent<UnityEngine.UI.Text>();
    Check(icon.sprite!=null&&help.text.Contains(CrazyKartItemArt.Help[i]),"Object "+i+" shows unique icon and use instructions");
    p.UseItem();await Wait(.05f);
   }
   var trap=GameObject.Find("Trampa de pista");Check(trap!=null&&trap.transform.position.y>1.5f,"Trap placed on elevated road, not underground");
   var paints=d.Karts.Select(k=>k.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.name=="Sports").sharedMaterial).Distinct().Count();
   Check(paints==4,"Four separate painted car-body materials");
   // Exercise the result presentation, separately from the full-race test.
   d.Finish(p);await Wait(.15f);
   Check(Object.FindObjectsByType<CrazyRaceBurst>().Any(x=>x.name=="Confeti de llegada"),"Finish event creates celebration confetti");
   Check(GameObject.Find("Clasificacion").GetComponent<UnityEngine.UI.Text>().text.Contains("GANA"),"Results identify the winner");
  }
  finally{p.enabled=true;foreach(var bot in d.Karts.Where(k=>k!=p))bot.gameObject.SetActive(true);InputSystem.RemoveDevice(keys);old?.MakeCurrent();InputSystem.settings.backgroundBehavior=bg;InputSystem.settings.editorInputBehaviorInPlayMode=route;File.WriteAllLines("Docs/Race345_Test_2026-11-04.txt",checks);}
  return checks.ToArray();
 }
}
