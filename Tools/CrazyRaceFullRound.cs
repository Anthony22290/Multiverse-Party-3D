using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Object=UnityEngine.Object;
public static class CrazyRaceFullRound
{
 public static async Task<string> Run()
 {
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();if(!Application.isPlaying||d==null)throw new Exception("Play race");
  foreach(var k in d.Karts)k.IsHuman=false;
  float oldScale=Time.timeScale;Time.timeScale=2;float maxY=0,maxSpeed=0;int initialFrame=Time.frameCount;
  var deadline=DateTime.UtcNow.AddSeconds(85);
  try
  {
   while(!d.Finished&&DateTime.UtcNow<deadline)
   {
    foreach(var k in d.Karts){maxY=Mathf.Max(maxY,k.transform.position.y);maxSpeed=Mathf.Max(maxSpeed,k.Speed);}
    await Task.Delay(80);
   }
   string s=(d.Finished&&d.Finishers.Count==4?"PASS":"FAIL")+" Full three-lap race, finishers="+d.Finishers.Count+", time="+d.RoundTime.ToString("F1")+", frames="+(Time.frameCount-initialFrame)+", maxY="+maxY.ToString("F2")+", maxSpeed="+maxSpeed.ToString("F2")+"\n"+
    string.Join("\n",d.Karts.OrderBy(k=>d.Position(k)).Select(k=>d.Position(k)+". "+k.DisplayName+" lap="+k.CurrentLap+" cp="+k.CurrentCheckpoint+" finish="+k.FinishTime.ToString("F1")));
   File.WriteAllText("Docs/CrazyRace_FullRound_2026-11-03.txt",s);return s;
  }
  finally{Time.timeScale=oldScale;}
 }
}
