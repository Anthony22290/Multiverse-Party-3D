using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
public static class XLShortcutTest
{
 public static async Task<string> Run(){
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();var p=d.Karts.First(k=>k.IsHuman);var rb=p.GetComponent<Rigidbody>();var s=d.Shortcut;
  var old=Keyboard.current;var bg=InputSystem.settings.backgroundBehavior;var routing=InputSystem.settings.editorInputBehaviorInPlayMode;
  InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;var keyboard=InputSystem.AddDevice<Keyboard>();
  try{
   var deadline=DateTime.UtcNow.AddSeconds(20);while(!d.Playing&&DateTime.UtcNow<deadline)await Task.Delay(30);
   foreach(var k in d.Karts.Where(k=>k!=p))k.gameObject.SetActive(false);
   foreach(var h in Object.FindObjectsByType<CrazyKartHazard>())Object.Destroy(h.gameObject);
   p.CurrentCheckpoint=18;p.CurrentLap=1;var f=(s.end-s.start).normalized;
   rb.position=s.start+f*2+Vector3.up*.55f;rb.rotation=Quaternion.LookRotation(f);rb.linearVelocity=Vector3.zero;
   int recoveries=p.GetComponent<CrazyKartRecovery>().AutomaticRecoveries;
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
   float end=Time.time+7;deadline=DateTime.UtcNow.AddSeconds(20);
   while(p.CurrentCheckpoint<22&&Time.time<end&&DateTime.UtcNow<deadline)await Task.Delay(30);
   bool pass=p.CurrentCheckpoint>=22&&p.CurrentLap==1&&p.GetComponent<CrazyKartRecovery>().AutomaticRecoveries==recoveries;
   string result=(pass?"PASS ":"FAIL ")+"Expanded solid shortcut driven end-to-end with W: checkpoint="+p.CurrentCheckpoint+", lap="+p.CurrentLap+", automatic recoveries="+(p.GetComponent<CrazyKartRecovery>().AutomaticRecoveries-recoveries);
   File.WriteAllText("Docs/CostaXL_ShortcutTest_2026-11-04.txt",result);return result;
  }finally{InputSystem.RemoveDevice(keyboard);old?.MakeCurrent();InputSystem.settings.backgroundBehavior=bg;InputSystem.settings.editorInputBehaviorInPlayMode=routing;foreach(var k in d.Karts.Where(k=>k!=p))k.gameObject.SetActive(true);}
 }
}
