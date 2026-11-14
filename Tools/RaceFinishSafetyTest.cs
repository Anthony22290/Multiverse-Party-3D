using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
public static class RaceFinishSafetyTest
{
 public static async Task<string> Run()
 {
  var d=UnityEngine.Object.FindAnyObjectByType<CrazyKartDirector>();var bot=d.Karts.First(k=>!k.IsHuman);var rb=bot.GetComponent<Rigidbody>();
  if(d.Countdown>0)await Task.Delay(4000);
  bot.CurrentLap=3;bot.CurrentCheckpoint=31;
  rb.position=d.GetWaypointPosition(0)+Vector3.up*.5f;
  var forward=d.GetWaypointPosition(1)-d.GetWaypointPosition(0);forward.y=0;rb.rotation=Quaternion.LookRotation(forward);
  rb.linearVelocity=forward.normalized*12;
  bool crossed=bot.PassCheckpoint(0);
  var initial=rb.position;await Task.Delay(700);
  string result=(crossed&&bot.HasFinished&&!rb.detectCollisions&&!rb.useGravity&&Vector3.Distance(initial,rb.position)>1?"PASS ":"FAIL ")+"Finished rival coasts clear and cannot block remaining racers";
  File.WriteAllText("Docs/Race345_FinishSafety_2026-11-04.txt",result);return result;
 }
}
