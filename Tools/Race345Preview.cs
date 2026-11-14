using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
public static class Race345Preview
{
 public static async Task<string> Run(){
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();var p=d.Karts.First(k=>k.IsHuman);p.GiveItem(4);
  await Task.Delay(150);Time.timeScale=0;return "Preview frozen with item explanation";
 }
}
