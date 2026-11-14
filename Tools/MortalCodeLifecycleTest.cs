using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class MortalCodeLifecycleTest
{
 static async Task Frames(int n){int first=Time.frameCount;var end=DateTime.UtcNow.AddSeconds(5);while(Time.frameCount<first+n && DateTime.UtcNow<end)await Task.Delay(15);}
 public static async Task<string[]> Run()
 {
  var report=new List<string>();void Check(bool ok,string s)=>report.Add((ok?"PASS ":"FAIL ")+s);
  var d=Object.FindAnyObjectByType<MortalCodeDirector>();d.Restart();await Frames(12);d=Object.FindAnyObjectByType<MortalCodeDirector>();
  Check(d!=null && !d.Finished && d.AliveCount==6 && d.Human.ProtectorAvailable,"Restart resets round and all participants");
  foreach(var b in Object.FindObjectsByType<MortalCodeBot>())b.enabled=false;
  var p=d.Human;p.Controller.enabled=false;p.transform.position=new(0,-12,-43);p.Controller.enabled=true;await Frames(8);
  Check(p.transform.position.y>=0 && p.Alive,"Out-of-map fall automatically recovers without elimination");
  typeof(MortalCodeDirector).GetProperty("Countdown").SetValue(d,0f);p.ActivateProtector();
  float old=Time.timeScale;Time.timeScale=10;
  var end=DateTime.UtcNow.AddSeconds(5);while(p.Protected && DateTime.UtcNow<end)await Task.Delay(25);Time.timeScale=old;
  Check(!p.Protected && !p.ProtectorAvailable,"Protector expires and does not recharge");
  p.Eliminate();await Frames(10);
  Check(!p.Alive && !d.Finished && !d.HUD.TerminalOpen,"Eliminated player spectates while rivals remain");
  typeof(MortalCodeDirector).GetProperty("Remaining").SetValue(d,.01f);await Frames(8);
  Check(d.Finished && d.Result.StartsWith("EMPATE"),"Timer expiry produces draw with multiple survivors");
  d.ReturnToHub();await Frames(16);
  var hub=Object.FindAnyObjectByType<MultiverseHub>();Check(hub!=null && SceneManager.GetActiveScene().name=="MundoDeInicio","Return to hub loads original hub");
  typeof(MultiverseHub).GetMethod("Select",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hub,new object[]{4});
  Check(GameObject.Find("Portal Código Mortal")!=null,"Fifth physical portal exists in hub");
  typeof(MultiverseHub).GetMethod("PlaySelected",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hub,null);await Frames(16);
  d=Object.FindAnyObjectByType<MortalCodeDirector>();Check(d!=null && d.AliveCount==6,"Hub selection enters CodigoMortal with new round");
  File.WriteAllLines("Docs/CodigoMortal_LifecycleTests_2026-11-04.txt",report);return report.ToArray();
 }
}
