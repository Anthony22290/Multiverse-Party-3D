using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class ShortcutProbe
{
 public static async Task<string[]> Run()
 {
 var d=UnityEngine.Object.FindAnyObjectByType<CrazyKartDirector>();var p=d.Karts.First(k=>k.IsHuman);var s=d.Shortcut;var rb=p.GetComponent<Rigidbody>();
 var bg=InputSystem.settings.backgroundBehavior;var routing=InputSystem.settings.editorInputBehaviorInPlayMode;var old=Keyboard.current;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;var keys=InputSystem.AddDevice<Keyboard>();var log=new List<string>();
 try {
 foreach(var k in d.Karts.Where(k=>k!=p))k.gameObject.SetActive(false);
 var f=(s.end-s.start).normalized;p.CurrentCheckpoint=18;p.CurrentLap=1;rb.position=s.start+f*2+Vector3.up*.55f;rb.rotation=Quaternion.LookRotation(f);rb.linearVelocity=Vector3.zero;
 InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));
 for(int i=0;i<24;i++){await Task.Delay(200);string obstacle="";if(Physics.SphereCast(rb.position,.5f,f,out var h,3,~0,QueryTriggerInteraction.Ignore))obstacle=h.collider.name;log.Add("cp="+p.CurrentCheckpoint+" pos="+rb.position+" speed="+p.Speed+" ahead="+obstacle);}
 return log.ToArray();
 }finally{InputSystem.RemoveDevice(keys);old?.MakeCurrent();InputSystem.settings.backgroundBehavior=bg;InputSystem.settings.editorInputBehaviorInPlayMode=routing;}
 }
}
