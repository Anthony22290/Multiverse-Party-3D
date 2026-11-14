using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;
public static class HubPlazaPlaytest
{
 public static string[] Run()
 {
  var h=Object.FindAnyObjectByType<MultiverseHub>();var cc=GameObject.Find("Explorador").GetComponent<CharacterController>();
  var results=new List<string>();Action<bool,string> check=(ok,s)=>results.Add((ok?"PASS ":"FAIL ")+s);
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  Action<Vector3> place=p=>{cc.enabled=false;cc.transform.position=p;cc.enabled=true;typeof(MultiverseHub).GetField("verticalSpeed",flags).SetValue(h,0f);Physics.SyncTransforms();};
  h.enabled=false;
  try
  {
   int covered=0,total=0;
   for(int x=-34;x<=34;x+=4)for(int z=-26;z<=42;z+=4)
   {
    total++;if(Physics.Raycast(new Vector3(x,6,z),Vector3.down,out var hit,8f,~0,QueryTriggerInteraction.Ignore) && hit.point.y>=.79f)covered++;
   }
   check(covered==total,"Floor coverage "+covered+"/"+total);
   foreach(var p in new[]{new Vector3(-30,4,-20),new Vector3(30,4,-20),new Vector3(-30,4,35),new Vector3(30,4,35),new Vector3(-10,4,5),new Vector3(10,4,5),new Vector3(0,4,-5)})
   {
    place(p);for(int i=0;i<150;i++)h.MoveExplorer(Vector3.zero,1f/60f,false);
    check(cc.isGrounded && cc.transform.position.y>.75f,"Ground contact at "+p+" feet="+cc.transform.position.y.ToString("F2"));
   }
   var visual=cc.GetComponentsInChildren<Renderer>();var bounds=visual[0].bounds;foreach(var r in visual)bounds.Encapsulate(r.bounds);
   check(Mathf.Abs(bounds.min.y-cc.transform.position.y)<.18f,"Avatar feet align with controller, offset="+(bounds.min.y-cc.transform.position.y).ToString("F3"));
   place(new Vector3(35,1,8));for(int i=0;i<120;i++)h.MoveExplorer(Vector3.right,1f/60f,false);
   check(cc.transform.position.x<37f,"Perimeter collision blocks traversal: x="+cc.transform.position.x.ToString("F2"));
   var car=GameObject.Find("Exhibición - Vehicle_Sports");var cb=PirateArenaVisuals.BoundsOf(car);
   place(new Vector3(cb.center.x,1,cb.min.z-2));for(int i=0;i<150;i++)h.MoveExplorer(Vector3.forward,1f/60f,false);
   check(cc.transform.position.z<cb.max.z || Mathf.Abs(cc.transform.position.x-cb.center.x)>.5f,"Imported sports car blocks or deflects explorer (no straight traversal), x="+cc.transform.position.x.ToString("F2"));
   check(Application.CanStreamedLevelBeLoaded("ArenaDelCaos") && Application.CanStreamedLevelBeLoaded("CarrerasLocas"),"Both playable scenes available");
   var select=typeof(MultiverseHub).GetMethod("Select",flags);select.Invoke(h,new object[]{3});
   var label=(UnityEngine.UI.Text)typeof(MultiverseHub).GetField("title",flags).GetValue(h);
   var button=(UnityEngine.UI.Button)typeof(MultiverseHub).GetField("playButton",flags).GetValue(h);
   check(label.text=="CARRERAS LOCAS" && button.gameObject.activeSelf,"Racing portal available after local race implementation");
   select.Invoke(h,new object[]{1});check(!button.gameObject.activeSelf,"Future minigame remains unavailable");
   select.Invoke(h,new object[]{0});
  }
  finally {place(new Vector3(0,1.05f,-5));h.enabled=true;File.WriteAllLines("Docs/Hub_Plaza_Test_2026-11-03.txt",results);}
  return results.ToArray();
 }
}

