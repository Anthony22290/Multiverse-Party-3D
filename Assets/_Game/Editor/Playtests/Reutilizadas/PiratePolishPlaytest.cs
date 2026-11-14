using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public static class PiratePolishPlaytest
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new List<string>();
    static void Set(object o, string key, object value) => o.GetType().GetField(key, Flags).SetValue(o, value);
    static void Check(bool pass, string detail) => results.Add((pass ? "PASS " : "FAIL ") + detail);
    static async Task Wait(float seconds)
    {
        float end = Time.time + seconds;
        var deadline = DateTime.UtcNow.AddSeconds(seconds * 4 + 10);
        while (Time.time < end && DateTime.UtcNow < deadline) await Task.Delay(15);
        if (Time.time < end) throw new Exception("Play loop stopped");
    }
    static void Place(PirateFighter f, Vector3 p, Quaternion rotation)
    {
        f.Respawn(); f.transform.SetPositionAndRotation(p, rotation);
        f.Body.position = p; f.Body.rotation = rotation;
        f.Body.linearVelocity = f.Body.angularVelocity = Vector3.zero;
        f.GetComponent<PirateActivePuppet>()?.ResetPose(); Physics.SyncTransforms();
        Set(f, "botThink", 100000f); Set(f, "botThrowAt", 100000f);
    }
    public static async Task<string[]> Run()
    {
        results.Clear();
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        if (!Application.isPlaying || d == null) throw new Exception("Play Arena first");
        var a = d.Fighters[0]; var b = d.Fighters[1];
        var bg = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldKeyboard = Keyboard.current; var oldMouse = Mouse.current;
        Keyboard keys = null; Mouse mouse = null;
        var orbit = d.ArenaCamera.GetComponent<PirateArenaCamera>();
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keys = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            keys.MakeCurrent(); mouse.MakeCurrent();
            PirateSession.Instance.SetMenu(false);
            d.RestartRound(); d.Tick(4f); d.enabled = false;
            foreach (var h in d.Cannons) h.enabled = false;
            foreach (var h in d.Sharks) h.enabled = false;
            foreach (var prop in d.Props) prop.gameObject.SetActive(false);
            foreach (var f in d.Fighters) { f.NetworkDriven = true; f.Eliminate(); }
            a.NetworkDriven = false; a.SetControl(true, "Tú");
            Set(orbit, "yaw", 0f); Set(orbit, "pitch", 26f);
            Vector3 p = new Vector3(0f, d.DeckY + 1f, d.Art.Map.CenterCourtZ);
            Place(a, p, Quaternion.identity); await Wait(.5f);
            Vector3 start = a.Body.position;
            InputSystem.QueueStateEvent(keys, new KeyboardState(Key.W)); await Wait(.6f);
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            Check(Vector3.ProjectOnPlane(a.Body.position-start, Vector3.up).magnitude > .8f, "W moves through actual input path");
            bool jumps = true; float peak = 0f;
            for (int i=0; i<4; i++)
            {
                Place(a, p, Quaternion.identity); await Wait(.35f);
                float y = a.Body.position.y, highest = y;
                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Space)); await Wait(.08f);
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                float end = Time.time + 1.8f;
                while (Time.time < end) { highest = Mathf.Max(highest, a.Body.position.y); await Wait(.02f); }
                peak = Mathf.Max(peak, highest-y);
                jumps &= highest-y > .3f && highest-y < 2f && a.Body.position.y < y+.5f;
            }
            Check(jumps, "Four Space jumps land without relaunch, max height="+peak.ToString("F2"));
            foreach (float height in new[] { 3f, 8f, 12f })
            {
                Place(a, p + Vector3.up*height, Quaternion.Euler(20f, 0f, 15f));
                float maxUp = 0f, end = Time.time+3f;
                while(Time.time < end) { maxUp=Mathf.Max(maxUp,a.Body.linearVelocity.y); await Wait(.02f); }
                Check(a.Body.position.y < p.y+1f && maxUp < 3f && a.IsAlive,
                    "Tilted fall "+height+"m settles; upward="+maxUp.ToString("F2")+", up="+a.transform.up.y.ToString("F2"));
            }
            Place(a,p,Quaternion.identity); Place(b,p+Vector3.forward*1.05f,Quaternion.identity);
            await Wait(.3f);
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.E)); await Wait(.12f);
            InputSystem.QueueStateEvent(keys,new KeyboardState());
            Check(b.HitStrain > 0f, "E accumulates combat damage via keyboard");
            Place(a,p,Quaternion.identity); Place(b,p+Vector3.forward*1.05f,Quaternion.identity);
            await Wait(.2f);
            InputSystem.QueueStateEvent(mouse,new MouseState { buttons=3 }); await Wait(.6f);
            Check(a.LeftGripped && a.RightGripped && b.IsHeld,"Both mouse buttons grip the rival");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.LeftShift)); await Wait(.35f);
            Check(a.IsLifting,"Shift activates lift while gripping");
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.F)); await Wait(.12f);
            Check(!a.IsGrabbing && !b.IsHeld && b.Body.linearVelocity.z>1f,"F releases and throws toward camera aim");
            InputSystem.QueueStateEvent(keys,new KeyboardState()); InputSystem.QueueStateEvent(mouse,new MouseState());
            await Wait(.2f);
            // Bot strategy: start close to a real rail opening with a passive captive.
            Vector3 exit = d.Art.Map.CarryExit(p+Vector3.right*2f, out Vector3 outward);
            Vector3 carrier = new Vector3(exit.x-.55f,p.y,exit.z);
            Place(a,carrier,Quaternion.LookRotation(outward)); Place(b,carrier+outward*1.05f,Quaternion.identity);
            a.SetControl(false,"Bot de prueba"); Set(a,"facing",outward); Set(a,"lookDirection",outward);
            Set(a,"grabPressed",true); Set(a,"botThrowAt",Time.time+4f);
            float deadline = Time.time+3f; bool captured=false, thrown=false;
            while(Time.time < deadline)
            {
                captured |= b.IsHeld;
                if(captured && !b.IsHeld && Vector3.Dot(b.Body.linearVelocity,outward)>1f) { thrown=true; break; }
                await Wait(.02f);
            }
            Check(captured && thrown,"Bot captures and throws outward through rail opening");
            Check(a.Body.position.y>1f && Mathf.Abs(a.Body.position.x)<d.Art.Map.Width*.5f,"Carrier stays inside deck at release");
        }
        catch(Exception e) { results.Add("ERROR "+e); }
        finally
        {
            if(keys!=null) InputSystem.RemoveDevice(keys);
            if(mouse!=null) InputSystem.RemoveDevice(mouse);
            oldKeyboard?.MakeCurrent(); oldMouse?.MakeCurrent();
            InputSystem.settings.backgroundBehavior=bg;
            InputSystem.settings.editorInputBehaviorInPlayMode=routing;
            a.SetControl(true,"Tú");
            foreach(var f in d.Fighters) f.NetworkDriven=false;
            foreach(var h in d.Cannons) h.enabled=true;
            foreach(var h in d.Sharks) h.enabled=true;
            foreach(var prop in d.Props) prop.gameObject.SetActive(true);
            d.RestartRound(); d.enabled=true;
            File.WriteAllLines("Docs/Claude_Pruebas/Arena_Polish_Test_2026-11-03.txt",results);
        }
        return results.ToArray();
    }
}
