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

// Run with Pipeline script run while ArenaDelCaos is playing. Changes are Play-mode-only.
public static class PirateAnimationPlaytest
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static void Call(object target, string method) => target.GetType().GetMethod(method, Private | BindingFlags.Public).Invoke(target, null);
    static void Check(bool valid, string name)
    {
        checks.Add((valid ? "PASS " : "FAIL ") + name);
        if (!valid) throw new Exception(name);
    }
    static void Teleport(PirateFighter fighter, Vector3 position)
    {
        fighter.Body.interpolation = RigidbodyInterpolation.None;
        fighter.Body.isKinematic = false;
        fighter.transform.SetPositionAndRotation(position, Quaternion.identity);
        fighter.Body.position = position; fighter.Body.rotation = Quaternion.identity;
        fighter.Body.linearVelocity = fighter.Body.angularVelocity = Vector3.zero;
        Set(fighter, "botThink", float.MaxValue);
        Set(fighter, "move", Vector3.zero);
        Set(fighter, "facing", Vector3.forward);
        Set(fighter, "stunnedUntil", 0f);
    }
    static string Pose(PirateFighter f) => f.GetComponent<PirateFighterAnimation>().CurrentPose;
    static async Task Capture(string name)
    {
        ScreenCapture.CaptureScreenshot("Temp/AnimationChecks/" + name + ".png");
        await Task.Delay(130);
    }
    public static async Task<string[]> Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play first.");
        checks.Clear(); Directory.CreateDirectory("Temp/AnimationChecks");
        int initialFrame = Time.frameCount;
        PirateArenaDirector director = Object.FindAnyObjectByType<PirateArenaDirector>();
        var fighters = director.Fighters.ToArray();
        PirateFighter player = fighters[0], other = fighters[1];
        Keyboard previousKeyboard = Keyboard.current;
        Mouse previousMouse = Mouse.current;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            PirateSession.Instance?.SetMenu(false);
            director.enabled = false;
            foreach (MonoBehaviour hazard in Object.FindObjectsByType<CannonHazard>()) { hazard.StopAllCoroutines(); hazard.enabled = false; }
            foreach (MonoBehaviour hazard in Object.FindObjectsByType<SharkHazard>()) { hazard.StopAllCoroutines(); hazard.enabled = false; }
            Call(director, "RestartRound");
            Set(director, "<IsPlaying>k__BackingField", true);
            Set(director, "_finished", false);
            foreach (var f in fighters)
            {
                Check(f.GetComponent<PirateFighterAnimation>().Ready, f.name + " Animator ready");
                Check(f.GetComponentsInChildren<SkinnedMeshRenderer>().All(r => r.bones.Length > 10), f.name + " skin bones");
                Check(!f.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Weapon_")), f.name + " unarmed");
                f.enabled = false;
                Teleport(f, new Vector3(-4f, 4f, 8f)); f.Body.isKinematic = true;
            }
            Vector3 origin = new Vector3(2.3f, director.DeckY + 0.89f, -5f);
            Set(player, "<IsHuman>k__BackingField", true);
            Teleport(player, origin); player.enabled = true;
            var camera = Camera.main;
            camera.GetComponent<PirateArenaCamera>().enabled = false;
            camera.transform.position = origin + new Vector3(2.5f, 1.4f, 4f);
            camera.transform.LookAt(origin + Vector3.up * 0.1f);
            await Task.Delay(600);
            Check(Time.frameCount > initialFrame + 5, "Player loop advances");
            Check(player.IsGrounded, "Feet on deck");
            Check(Pose(player) == "Idle", "Idle at rest");
            await Capture("01-idle");

            Transform leg = player.GetComponentsInChildren<Transform>().First(t => t.name == "UpperLeg.L");
            Quaternion before = leg.localRotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            await Task.Delay(100);
            Check(Pose(player) == "Walk", "Walking follows speed (actual " + Pose(player) + ", speed " + player.Body.linearVelocity.magnitude + ", keyboard " + keyboard.enabled + ", key " + keyboard.wKey.isPressed + ")");
            Check(Quaternion.Angle(before, leg.localRotation) > 3f, "Walk moves leg bones");
            await Capture("02-walk");
            await Task.Delay(400);
            Check(Pose(player) == "Run", "Running follows speed");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Teleport(player, origin);
            await Task.Delay(250);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            await Task.Delay(100);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Check(player.Body.linearVelocity.y > 1f && Pose(player) == "Jump", "Jump impulse and animation");
            await Capture("03-jump");
            await Task.Delay(550);
            Check(Pose(player) == "Fall", "Airborne fall pose");
            await Task.Delay(850);
            Check(player.IsGrounded, "Lands back on deck");

            Teleport(player, origin); Teleport(other, origin + Vector3.forward * 1.15f);
            other.enabled = false;
            await Task.Delay(100);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            await Task.Delay(100);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            Check(Pose(player) == "Push" && other.Body.linearVelocity.z > 1f, "Two-hand push and target impulse");
            await Capture("04-push");
            await Task.Delay(450);

            Teleport(player, origin); Teleport(other, origin + Vector3.forward * 1.05f);
            Physics.SyncTransforms(); InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            await Task.Delay(220);
            Check(player.IsGrabbing && other.IsHeld, "Grab creates ownership");
            Check(Pose(player) == "Carry" && Pose(other) == "Struggle", "Carry and captive struggle poses");
            await Capture("05-carry");
            float initialHeight = other.Body.position.y - player.Body.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            await Task.Delay(1100);
            await Capture("06-lift");
            Check(Pose(player) == "Lift" && other.Body.position.y - player.Body.position.y > initialHeight + 0.3f,
                "Lift raises captive and hands (pose " + Pose(player) + ", initial " + initialHeight + ", current " +
                (other.Body.position.y - player.Body.position.y) + ", held " + other.IsHeld + ", shift " + keyboard.leftShiftKey.isPressed +
                ", rotation " + player.transform.eulerAngles + ", anchor " + player.GetComponent<ConfigurableJoint>().anchor + ")");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(100);
            Check(!other.IsHeld && Pose(player) == "Throw" && other.Body.linearVelocity.z > 2f, "Throw releases captive with velocity");
            await Capture("07-throw");
            await Task.Delay(500);

            Teleport(other, origin + Vector3.right * 8f); other.Body.isKinematic = true;
            Teleport(player, origin);
            var prop = Object.FindObjectsByType<GrabbableProp>().First();
            prop.ResetProp();
            Rigidbody propBody = prop.GetComponent<Rigidbody>();
            propBody.interpolation = RigidbodyInterpolation.None;
            prop.transform.position = origin + Vector3.forward * 0.95f - Vector3.up * 0.3f;
            propBody.position = prop.transform.position;
            Physics.SyncTransforms();
            await Task.Delay(150);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            await Task.Delay(250);
            Check(prop.Holder == player && Pose(player) == "Carry", "Object grab uses carry pose");
            float propHeight = propBody.position.y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            await Task.Delay(800);
            Check(propBody.position.y > propHeight + 0.3f && Pose(player) == "Lift", "Object lift");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(100);
            Check(prop.Holder == null && propBody.linearVelocity.z > 2f && Pose(player) == "Throw", "Object throw");
            await Task.Delay(450);

            Vector3 ledge = new Vector3(director.DeckWidth * 0.5f + 0.43f, director.DeckY - 0.1f, director.Art.Map.CenterCourtZ);
            Teleport(player, ledge);
            player.transform.rotation = player.Body.rotation = Quaternion.LookRotation(Vector3.left);
            Set(player, "facing", Vector3.left);
            Physics.SyncTransforms();
            await Task.Delay(40);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            await Task.Delay(130);
            Check(player.IsHoldingLedge && Pose(player) == "Hang", "Ledge grip and hanging pose");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            await Task.Delay(100);
            Check(!player.IsGrabbing && player.Body.linearVelocity.y > 1f && Pose(player) == "Jump", "Climb releases ledge and jumps");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(800);

            Teleport(player, origin); player.KnockDown(0.5f);
            await Task.Delay(100);
            Check(Pose(player) == "Ragdoll" && player.GetComponent<PirateFighterRagdoll>().Active &&
                player.GetComponentsInChildren<Rigidbody>().Count(body => !body.isKinematic) > 1,
                "Physical limb ragdoll during stun");
            await Task.Delay(500);
            Check(Pose(player) == "Recover", "Recovery after stun");
            await Capture("08-recover");
            player.Eliminate(); await Task.Delay(150);
            Check(Pose(player) == "Eliminated", "Elimination animation");
            player.Respawn(); Teleport(player, origin); await Task.Delay(300);
            Check(player.IsAlive && !player.IsGrabbing && !player.IsHeld && Pose(player) == "Idle", "Respawn clears animation and grabs");

            foreach (var f in fighters.Skip(1)) f.Eliminate();
            Call(director, "CheckWinner"); await Task.Delay(250);
            Check(director.Winner == player && Pose(player) == "Victory", "Winner celebrates");
            await Capture("09-victory");
            File.WriteAllLines("Temp/AnimationChecks/results.txt", checks);
            return checks.ToArray();
        }
        catch (Exception e)
        {
            checks.Add(e.ToString()); File.WriteAllLines("Temp/AnimationChecks/results.txt", checks);
            throw;
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            previousKeyboard?.MakeCurrent(); previousMouse?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousRouting;
        }
        // End Play after reviewing screenshots. No changes made here are saved to the scene.
    }
}
