using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
public static class RaceInputProbe
{
public static async Task<string[]> Run(){
var p=UnityEngine.Object.FindObjectsByType<CrazyKartController>().First(k=>k.IsHuman);
var bg=InputSystem.settings.backgroundBehavior;var route=InputSystem.settings.editorInputBehaviorInPlayMode;var old=Keyboard.current;
InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
var key=InputSystem.AddDevice<Keyboard>();var l=new List<string>();
try{p.Recover();await Task.Delay(650);InputSystem.QueueStateEvent(key,new KeyboardState(Key.W));
for(int i=0;i<20;i++){await Task.Delay(150);l.Add("t="+Time.time+" speed="+p.Speed+" pos="+p.transform.position+" w="+Keyboard.current.wKey.isPressed+" keyboard="+Keyboard.current.deviceId+" expected="+key.deviceId+" stun="+p.Stunned+" ground="+Physics.Raycast(p.transform.position+Vector3.up*.15f,Vector3.down,.9f,~0,QueryTriggerInteraction.Ignore));}
return l.ToArray();}finally{InputSystem.RemoveDevice(key);old?.MakeCurrent();InputSystem.settings.backgroundBehavior=bg;InputSystem.settings.editorInputBehaviorInPlayMode=route;}
}
}
