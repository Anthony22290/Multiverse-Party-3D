using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

// Exercises actual Rigidbody traversal with the same keyboard input as the player.
public static class PirateMapPlaytest
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static List<string> results = new List<string>();
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    static void Check(bool condition, string message)
    {
        results.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new Exception(message);
    }
    static async Task Traverse(PirateFighter p, Keyboard keyboard, PirateArenaRamp ramp, bool up)
    {
        Vector3 direction = Vector3.ProjectOnPlane(ramp.High - ramp.Low, Vector3.up).normalized;
        Vector3 start = (up ? ramp.Low - direction * 0.8f : ramp.High + direction * 0.8f) + Vector3.up * 0.9f;
        Vector3 finish = (up ? ramp.High + direction * 0.8f : ramp.Low - direction * 0.8f) + Vector3.up * 0.9f;
        Vector3 travel = up ? direction : -direction;
        p.Respawn();
        p.Body.interpolation = RigidbodyInterpolation.None;
        p.transform.SetPositionAndRotation(start, Quaternion.LookRotation(travel));
        p.Body.position = start; p.Body.rotation = Quaternion.LookRotation(travel);
        p.Body.linearVelocity = p.Body.angularVelocity = Vector3.zero;
        Set(p, "facing", travel);
        Camera.main.transform.SetPositionAndRotation(start - travel * 5f + Vector3.up * 4f,
            Quaternion.LookRotation(travel - Vector3.up * 0.6f));
        Physics.SyncTransforms();
        await Task.Delay(180);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        float startTime = Time.time;
        bool fellThrough = false;
        while (Time.time - startTime < 5f && Vector3.Dot(finish - p.Body.position, travel) > 0.15f)
        {
            if (p.Body.position.y < ramp.Low.y + 0.5f) fellThrough = true;
            await Task.Delay(25);
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        string label = ramp.name + " x=" + ramp.Low.x.ToString("F2") + (up ? " subida" : " bajada");
        Check(!fellThrough, label + " sin atravesar suelo");
        Check(Vector3.Dot(finish - p.Body.position, travel) < 0.3f, label + " llega al descansillo (" + p.Body.position + ")");
        await Task.Delay(180);
        float settleDeadline = Time.time + 1f;
        while (!p.IsGrounded && Time.time < settleDeadline) await Task.Delay(25);
        Check(p.IsGrounded && p.Body.position.y > Mathf.Min(ramp.Low.y, ramp.High.y) + 0.6f, label + " apoyo estable");
    }
    public static async Task<string[]> Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play first.");
        results.Clear();
        Keyboard prior = Keyboard.current;
        var background = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            var director = Object.FindAnyObjectByType<PirateArenaDirector>();
            PirateSession.Instance?.SetMenu(false);
            director.enabled = false;
            director.SendMessage("RestartRound");
            Set(director, "<IsPlaying>k__BackingField", true);
            foreach (var h in Object.FindObjectsByType<CannonHazard>()) { h.StopAllCoroutines(); h.enabled = false; }
            foreach (var h in Object.FindObjectsByType<SharkHazard>()) { h.StopAllCoroutines(); h.enabled = false; }
            foreach (var prop in Object.FindObjectsByType<GrabbableProp>()) prop.gameObject.SetActive(false);
            foreach (var f in director.Fighters) { f.enabled = false; f.Body.isKinematic = true; f.GetComponent<Collider>().enabled = false; }
            PirateFighter player = director.Fighters[0]; player.enabled = true;
            Set(player, "<IsHuman>k__BackingField", true);
            Camera.main.GetComponent<PirateArenaCamera>().enabled = false;
            var brain = Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            var ramps = Object.FindObjectsByType<PirateArenaRamp>();
            Check(ramps.Length == 4, "Cuatro escaleras con colisión continua");
            foreach (var ramp in ramps)
            {
                int supported = 0;
                foreach (float lateral in new[] { -0.95f, 0f, 0.95f })
                    for (int i = 0; i <= 40; i++)
                    {
                        Vector3 p = Vector3.Lerp(ramp.Low, ramp.High, i / 40f) + Vector3.right * lateral;
                        var hits = Physics.RaycastAll(p + Vector3.up * 10f, Vector3.down, 11f);
                        if (hits.Any(h => h.collider.GetComponent<PirateArenaSurface>() != null && h.normal.y > 0.5f && Mathf.Abs(h.point.y - p.y) < 0.09f)) supported++;
                    }
                Check(supported == 123, ramp.name + " x=" + ramp.Low.x.ToString("F2") + " 123 puntos sin huecos");
                // Side entry below the slope previously reached an empty cavity.
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 middle = Vector3.Lerp(ramp.Low, ramp.High, .8f);
                    middle.y = director.DeckY + .25f;
                    Vector3 origin = middle + Vector3.right * side * (ramp.Width * .5f + .5f);
                    Check(Physics.RaycastAll(origin, Vector3.left * side, 1f).Any(hit =>
                        hit.collider.name.Contains("lateral cerrado")), ramp.name + " lateral sellado " + side);
                }
                await Traverse(player, keyboard, ramp, true);
                await Traverse(player, keyboard, ramp, false);
            }
            // At foot height, the vertical face of each raised deck must block entry underneath it.
            var raised = Object.FindObjectsByType<PirateArenaSurface>().Where(s => s.name.Contains("suelo continuo") && s.GetComponent<Collider>().bounds.max.y > director.DeckY + 1f);
            foreach (var deck in raised)
            {
                Bounds b = deck.GetComponent<Collider>().bounds;
                float sign = Mathf.Sign(b.center.z);
                Vector3 boundary = new Vector3(0f, director.DeckY + 0.2f, sign > 0 ? b.min.z : b.max.z);
                Check(deck.GetComponent<Collider>().Raycast(new Ray(boundary - Vector3.forward * sign, Vector3.forward * sign), out _, 2f),
                    deck.name + " base cerrada bajo el descansillo");
            }
            File.WriteAllLines("Temp/AnimationChecks/map-results.txt", results);
            return results.ToArray();
        }
        catch (Exception e) { results.Add(e.ToString()); File.WriteAllLines("Temp/AnimationChecks/map-results.txt", results); throw; }
        finally
        {
            InputSystem.RemoveDevice(keyboard); prior?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
        }
    }
}
