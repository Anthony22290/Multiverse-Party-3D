using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
public static class RacePolishPreview
{
 public static async Task<string> Run()
 {
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();var p=d.Karts.First(k=>k.IsHuman);
  typeof(CrazyKartDirector).GetField("countdown",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(d,0f);
  p.GiveItem(4);p.UseItem();p.ApplyBoost(4);p.GetComponent<Rigidbody>().linearVelocity=p.transform.forward*17;
  await Task.Delay(350);
  bool trails=p.GetComponentsInChildren<TrailRenderer>().All(t=>t.positionCount>1);
  Time.timeScale=0;
  foreach(var source in p.GetComponents<AudioSource>())source.mute=true;
  return "Preview: shield="+p.Shielded+", trail geometry="+trails+", speed="+p.Speed;
 }
}
