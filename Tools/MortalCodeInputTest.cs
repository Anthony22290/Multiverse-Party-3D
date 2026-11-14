using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
public static class MortalCodeInputTest
{
 static async Task Frames(int n){int start=Time.frameCount;var limit=DateTime.UtcNow.AddSeconds(8);while(Time.frameCount-start<n && DateTime.UtcNow<limit)await Task.Delay(10);}
 public static async Task<string[]> Run()
 {
  var checks=new List<string>();void Check(bool ok,string s)=>checks.Add((ok?"PASS ":"FAIL ")+s);
  var d=Object.FindAnyObjectByType<MortalCodeDirector>();var p=d.Human;
  foreach(var b in Object.FindObjectsByType<MortalCodeBot>())b.enabled=false;
  foreach(var a in d.Actors)if(a.Agent!=null && a.Agent.isOnNavMesh)a.Agent.isStopped=true;
  typeof(MortalCodeDirector).GetProperty("Countdown").SetValue(d,0f);
  var old=Keyboard.current;var bg=InputSystem.settings.backgroundBehavior;var routing=InputSystem.settings.editorInputBehaviorInPlayMode;
  InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  var keyboard=InputSystem.AddDevice<Keyboard>();
  async Task KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));await Frames(3);InputSystem.QueueStateEvent(keyboard,new KeyboardState());await Frames(3);}
  try
  {
   var from=p.transform.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Frames(45);InputSystem.QueueStateEvent(keyboard,new KeyboardState());
   Check(Vector3.Distance(from,p.transform.position)>1,"W moves real controller");
   var terminal=d.terminals[0];p.Controller.enabled=false;p.transform.position=terminal.UsePoint;p.Controller.enabled=true;d.View.Snap();await Frames(4);
   await KeyPress(Key.E);Check(d.HUD.TerminalOpen,"E opens terminal");
   foreach(char c in "1234")InputSystem.QueueTextEvent(keyboard,c);await Frames(4);
   Check(GameObject.Find("_ _ _ _").GetComponent<UnityEngine.UI.Text>().text=="1234","Keyboard text fills four-digit terminal");
   await KeyPress(Key.Backspace);Check(GameObject.Find("_ _ _ _").GetComponent<UnityEngine.UI.Text>().text=="123_","Backspace removes last digit");
   await KeyPress(Key.Escape);Check(!d.HUD.TerminalOpen,"Escape closes terminal");
   await KeyPress(Key.Q);Check(p.Protected && !p.ProtectorAvailable,"Q activates protector once");
   await KeyPress(Key.Escape);Check(d.HUD.MenuOpen,"Escape opens menu");await KeyPress(Key.Escape);
   File.WriteAllLines("Docs/CodigoMortal_InputTests_2026-11-04.txt",checks);return checks.ToArray();
  }
  finally{InputSystem.RemoveDevice(keyboard);old?.MakeCurrent();InputSystem.settings.backgroundBehavior=bg;InputSystem.settings.editorInputBehaviorInPlayMode=routing;}
 }
}
