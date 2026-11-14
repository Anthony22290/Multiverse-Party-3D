using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
public static class TestMortalCode
{
 static List<string> checks=new();
 static void Check(bool ok,string text){checks.Add((ok?"PASS ":"FAIL ")+text);}
 static void Set(object o,string p,object value)=>o.GetType().GetProperty(p).SetValue(o,value);
 static void Place(MortalCodeActor a,Vector3 p){if(a.Controller!=null)a.Controller.enabled=false;if(a.Agent!=null)a.Agent.enabled=false;a.transform.position=p;if(a.IsHuman){a.Director.View.enabled=false;Set(a.Director.View,"Yaw",0f);a.Director.View.transform.SetPositionAndRotation(p+new Vector3(0,3.2f,-5.2f),Quaternion.Euler(18,0,0));}if(a.Controller!=null)a.Controller.enabled=true;}
 public static async Task<string[]> Run()
 {
  checks.Clear();var d=Object.FindAnyObjectByType<MortalCodeDirector>();if(d==null||!Application.isPlaying)throw new Exception("Play CodigoMortal first");
  foreach(var b in Object.FindObjectsByType<MortalCodeBot>())b.enabled=false;
  Set(d,"Countdown",0f);Set(d,"Remaining",240f);Set(d,"Finished",false);
  foreach(var a in d.Actors){Set(a,"Alive",true);foreach(var c in a.GetComponents<Collider>())c.enabled=true;}
  var p=d.Human;p.enabled=false;var rival=d.Actors[1];var t=d.terminals[0];
  for(int i=1;i<d.Actors.Count;i++)Place(d.Actors[i],new(40+i*2,0,-40));
  Place(p,t.UsePoint+Vector3.up*.06f);Place(rival,new(-4,.06f,-31));
  await Task.Delay(200);
  var codes=(Dictionary<MortalCodeActor,string>)typeof(MortalCodeDirector).GetField("codes",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d);
  Check(codes.Values.Distinct().Count()==6 && codes.Values.All(c=>c.Length==4),"Six unique four-digit codes");
  Check(d.Observe(p,p)==null,"Own code cannot be observed");
  Check(d.CanUse(p,t),"Terminal reachable at interaction point");
  var observed=d.Observe(p,rival);Check(observed==codes[rival],"Nearby unobstructed opponent code readable");
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new(-4,1.5f,-35);wall.transform.localScale=new(6,3,1);Physics.SyncTransforms();
  Check(d.Observe(p,rival)==null,"Wall blocks code reading");Object.Destroy(wall);await Task.Delay(50);
  Place(rival,new(-4,0,-10));Check(d.Observe(p,rival)==null,"Code hidden beyond 18 metres");Place(rival,new(-4,0,-31));
  Check(rival.ActivateProtector() && !rival.ActivateProtector(),"Protector has one charge");
  Check(d.Observe(p,rival)==null && !d.Submit(p,t,codes[rival],out _),"Protector hides code and rejects elimination");
  typeof(MortalCodeActor).GetField("protectionUntil",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(rival,0f);p.LockedUntil=0;
  Check(!d.Submit(p,t,"invalid",out _) && p.LockedUntil-Time.time>4.8f,"Wrong code causes five-second lockout");
  Check(!d.Submit(p,t,codes[rival],out _),"Correct code rejected during lockout");p.LockedUntil=0;
  Check(!d.Submit(p,t,codes[d.Actors[2]],out _),"Unseen code rejected even if guessed correctly");p.LockedUntil=0;
  Check(p.ActivateProtector() && !d.Submit(p,t,codes[rival],out _),"Protected attacker cannot eliminate");
  typeof(MortalCodeActor).GetField("protectionUntil",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p,0f);
  Place(p,new(4,0,-40));Check(!d.Submit(p,t,codes[rival],out _),"Submission rejected away from terminal");Place(p,t.UsePoint+Vector3.up*.05f);await Task.Delay(100);
  Check(d.Submit(p,t,codes[rival],out _) && !rival.Alive && p.Eliminations==1,"Observed code eliminates target once");
  Check(!d.Submit(p,t,codes[rival],out _),"Eliminated target cannot be scored twice");p.LockedUntil=0;
  // Physical CharacterController checks with automatic input disabled, without bypassing collision.
  p.enabled=false;Place(p,new(0,3,12));
  for(int i=0;i<120;i++){p.Move(Vector3.zero,1/60f,i==45);await Task.Delay(1);}
  Check(p.transform.position.y<4.3f,"Character stays below solid bridge when jumping underneath");
  Place(p,new(0,10,12));for(int i=0;i<100;i++){p.Move(Vector3.zero,1/60f,false);await Task.Delay(1);}
  Check(p.transform.position.y>5.9f && p.transform.position.y<6.2f,"Character lands on bridge top");
  Place(p,new(0,0,-49));for(int i=0;i<100;i++){p.Move(Vector3.back,1/60f,false);await Task.Delay(1);}
  Check(p.transform.position.z>-50.3f,"Boundary wall blocks leaving map");
  p.Recover();Check(p.transform.position.y>-.1f,"Recovery returns to valid surface");p.enabled=false;
  int paths=0,total=0;
  foreach(var start in d.spawnPoints)foreach(var terminal in d.terminals)
  {total++;var path=new NavMeshPath();if(NavMesh.CalculatePath(start.position,terminal.UsePoint,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)paths++;}
  Check(paths==total,"Navigation connects all spawns to all terminals: "+paths+"/"+total);
  Place(p,t.UsePoint+Vector3.up*.05f);await Task.Delay(100);
  d.HUD.OpenTerminal(t);Check(d.HUD.TerminalOpen,"Terminal HUD opens");d.HUD.CloseTerminal();Check(!d.HUD.TerminalOpen,"Terminal HUD closes");
  foreach(var a in d.Actors.Where(a=>a!=p && a.Alive).ToArray())
  {Place(a,new(-4,0,-31));await Task.Delay(80);var code=d.Observe(p,a);if(code!=null)d.Submit(p,t,code,out _);Place(a,new(40,0,-45));}
  await Task.Delay(150);Check(d.Finished && d.AliveCount==1 && d.Result.Contains("TÚ"),"Last survivor produces winner and results");
  p.enabled=true;d.View.enabled=true;d.View.Snap();
  File.WriteAllLines("Docs/Claude_Pruebas/CodigoMortal_Tests_2026-11-04.txt",checks);return checks.ToArray();
 }
}
