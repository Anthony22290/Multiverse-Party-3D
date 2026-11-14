using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object=UnityEngine.Object;
public static class MortalCodeNaturalRound
{
 public static async Task<string> Run()
 {
  var d=Object.FindAnyObjectByType<MortalCodeDirector>();float old=Time.timeScale;int initialFrame=Time.frameCount;
  try
  {
   Time.timeScale=5;
   var deadline=DateTime.UtcNow.AddSeconds(110);
   while(!d.Finished && DateTime.UtcNow<deadline)await Task.Delay(100);
   string result=(d.Finished && Time.frameCount>initialFrame+100?"PASS ":"FAIL ")+"Natural local round: "+d.Result+", survivors="+d.AliveCount+", remaining="+d.Remaining.ToString("F1")+", bot eliminations="+d.Actors.Where(a=>!a.IsHuman).Sum(a=>a.Eliminations)+", frames="+(Time.frameCount-initialFrame);
   File.WriteAllText("Docs/CodigoMortal_NaturalRound_2026-11-04.txt",result);return result;
  }
  finally{Time.timeScale=old;}
 }
}
