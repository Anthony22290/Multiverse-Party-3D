using System.Linq;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Object=UnityEngine.Object;
public static class XLRecoveryPresentation
{
 public static async Task<string[]> Run(){
  var d=Object.FindAnyObjectByType<CrazyKartDirector>();var p=d.Karts.First(k=>k.IsHuman);var cam=Object.FindAnyObjectByType<CrazyKartCamera>();
  p.CurrentCheckpoint=9;p.Recover();await Task.Delay(200);
  p.ApplyBoost(2);p.GetComponent<Rigidbody>().linearVelocity=p.transform.forward*12;await Task.Delay(120);
  cam.transform.position=p.transform.position+Vector3.down*4;
  p.Recover();await Task.Delay(50);
  var checks=new[]{
   (cam.transform.position.y>p.transform.position.y+1?"PASS ":"FAIL ")+"Camera immediately returns above road on recovery",
   (p.GetComponentsInChildren<TrailRenderer>().All(t=>!t.emitting&&t.positionCount<=1)?"PASS ":"FAIL ")+"Respawn clears trails instead of drawing through deck",
   (GameObject.Find("Avisos").GetComponent<UnityEngine.UI.Text>().text=="DE VUELTA EN PISTA"?"PASS ":"FAIL ")+"HUD explains automatic recovery"
  };
  File.WriteAllLines("Docs/CostaXL_RecoveryPresentation_2026-11-04.txt",checks);return checks;
 }
}
