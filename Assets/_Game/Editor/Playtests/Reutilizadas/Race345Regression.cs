using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
public static class Race345Regression
{
    static List<string> results=new List<string>();
    static void Check(bool ok,string s)=>results.Add((ok?"PASS ":"FAIL ")+s);
    static async Task Wait(float seconds)
    {
        float end=Time.time+seconds;var deadline=DateTime.UtcNow.AddSeconds(seconds*4+5);
        while(Time.time<end&&DateTime.UtcNow<deadline)await Task.Delay(20);
        if(Time.time<end)throw new Exception("Frames not advancing");
    }
    public static async Task<string[]> Run()
    {
        results.Clear();var d=Object.FindAnyObjectByType<CrazyKartDirector>();
        if(!Application.isPlaying||d==null)throw new Exception("Race must be playing");
        var player=d.Karts.First(k=>k.IsHuman);var body=player.GetComponent<Rigidbody>();
        var bots=d.Karts.Where(k=>!k.IsHuman).ToArray();
        var oldKeyboard=Keyboard.current; var bg=InputSystem.settings.backgroundBehavior; var routing=InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var keyboard=InputSystem.AddDevice<Keyboard>();
        var field=typeof(CrazyKartDirector).GetField("countdown",BindingFlags.NonPublic|BindingFlags.Instance);
        try
        {
            Check(d.Karts.Count==4&&bots.Length==3,"Saved scene registers one human and three bots");
            Check(d.Waypoints.Count==32,"32 ordered checkpoints persisted");
            foreach(var b in bots)b.gameObject.SetActive(false);
            foreach(var hazard in Object.FindObjectsByType<CrazyKartHazard>())Object.Destroy(hazard.gameObject);
            d.enabled=false;field.SetValue(d,3f);
            player.CurrentLap=1;player.CurrentCheckpoint=0;player.Recover();await Wait(.6f);
            Vector3 start=body.position;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Wait(.6f);
            Check(Vector3.ProjectOnPlane(start-body.position,Vector3.up).magnitude<.1f,"Countdown blocks acceleration");
            field.SetValue(d,0f);await Wait(1.6f);
            Check(player.Speed>10&&player.Speed<23,"W accelerates with bounded speed: "+player.Speed.ToString("F2")+" position="+body.position+" stunned="+player.Stunned+" key="+Keyboard.current.wKey.isPressed+" device="+Keyboard.current.deviceId+"/"+keyboard.deviceId);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.S));await Wait(.7f);
            Check(player.Speed<12,"S brakes: "+player.Speed.ToString("F2"));
            player.Recover();await Wait(.6f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Wait(1.2f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.D,Key.LeftShift));await Wait(.95f);
            float charge=player.NitroAmount;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Wait(.06f);
            Check(charge>35&&player.Boosting,"SHIFT steering charges drift and release gives mini turbo, charge="+charge.ToString("F0"));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            player.Recover();await Wait(.6f);
            typeof(CrazyKartController).GetField("<HeldItem>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(player,0); player.GiveItem(4);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));await Wait(.1f);
            Check(player.Shielded&&player.HeldItem==0,"Space consumes held shield");
            player.Hit();Check(!player.Shielded&&!player.Stunned,"Shield absorbs one impact");
            player.Hit();Check(player.Stunned,"Unshielded impact temporarily slows kart");await Wait(1.2f);
            player.GiveItem(1);player.UseItem();Check(player.Boosting,"Turbo object activates");
            player.GiveItem(2);player.UseItem();await Wait(.05f);
            Check(Object.FindObjectsByType<CrazyKartHazard>(FindObjectsSortMode.None).Length>0,"Pulse spawns");
            player.GiveItem(3);player.UseItem();await Wait(.05f);
            Check(GameObject.Find("Trampa de pista")!=null,"Trap spawns");
            player.CurrentCheckpoint=0;player.CurrentLap=1;
            Check(!player.PassCheckpoint(3)&&player.CurrentLap==1,"Skipped checkpoints cannot add laps");
            body.linearVelocity=-(d.GetWaypointPosition(2)-d.GetWaypointPosition(1)).normalized*10;
            Check(!player.PassCheckpoint(1),"Backward gate cannot advance");
            body.position=new Vector3(0,-8,0);await Wait(.2f);
            Check(body.position.y>0&&player.CurrentCheckpoint==0,"Falling recovers to last valid checkpoint");
            int floor=0;
            foreach(var p in d.Waypoints)if(Physics.Raycast(p+Vector3.up*5,Vector3.down,out var hit,7,~0,QueryTriggerInteraction.Ignore)&&hit.point.y>=-.1f)floor++;
            Check(floor==32,"Continuous road floor "+floor+"/32");
            var box=Object.FindAnyObjectByType<CrazyKartItemBox>();box.Collect(player);
            Check(player.HeldItem>0&&!box.GetComponent<Collider>().enabled,"Item pickup reserves one slot and hides box");
            await Wait(5.1f);Check(box.GetComponent<Collider>().enabled,"Item box respawns");
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); oldKeyboard?.MakeCurrent(); InputSystem.settings.backgroundBehavior=bg; InputSystem.settings.editorInputBehaviorInPlayMode=routing; d.enabled=true;foreach(var b in bots)b.gameObject.SetActive(true);
            File.WriteAllLines("Docs/Claude_Pruebas/Race345_Regression_2026-11-04.txt",results);
        }
        return results.ToArray();
    }
}




