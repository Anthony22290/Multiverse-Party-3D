using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class CrazyRacePortalTest
{
 public static async Task<string> Run()
 {
  var h=Object.FindAnyObjectByType<MultiverseHub>();if(h==null)throw new Exception("Open hub in Play");
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  typeof(MultiverseHub).GetMethod("Select",flags).Invoke(h,new object[]{3});
  var b=(UnityEngine.UI.Button)typeof(MultiverseHub).GetField("playButton",flags).GetValue(h);
  bool available=b.gameObject.activeSelf;
  b.onClick.Invoke();
  var deadline=DateTime.UtcNow.AddSeconds(15);
  while(Object.FindAnyObjectByType<CrazyKartDirector>()==null&&DateTime.UtcNow<deadline)await Task.Delay(100);
  await Task.Delay(600);
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();
  bool loaded=d!=null&&d.Karts.Count==4;
  d.ReturnToHub();
  deadline=DateTime.UtcNow.AddSeconds(15);
  while(Object.FindAnyObjectByType<MultiverseHub>()==null&&DateTime.UtcNow<deadline)await Task.Delay(100);
  string result=(available&&loaded&&Object.FindAnyObjectByType<MultiverseHub>()!=null?"PASS ":"FAIL ")+"Hub portal button -> CarrerasLocas with 4 racers -> MundoDeInicio.";
  File.WriteAllText("Docs/CrazyRace_Portal_2026-11-03.txt",result);return result;
 }
}
