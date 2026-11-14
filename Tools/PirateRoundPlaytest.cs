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

public static class PirateRoundPlaytest
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new List<string>();
    static void Set(object obj, string key, object value) => obj.GetType().GetField(key, Flags).SetValue(obj, value);
    static object Get(object obj, string key) => obj.GetType().GetField(key, Flags).GetValue(obj);
    static void Check(bool pass, string text) { results.Add((pass ? "PASS " : "FAIL ") + text); if (!pass) throw new Exception(text); }
    static void Place(PirateFighter f, Vector3 position)
    {
        f.Respawn(); f.Body.interpolation = RigidbodyInterpolation.None;
        f.transform.SetPositionAndRotation(position, Quaternion.identity);
        f.Body.position = position; f.Body.rotation = Quaternion.identity;
        Set(f, "botThink", 0f);
    }
    public static async Task<string[]> Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play first.");
        results.Clear();
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        var cannons = Object.FindObjectsByType<CannonHazard>();
        var sharks = Object.FindObjectsByType<SharkHazard>();
        Keyboard previous = Keyboard.current;
        var bg = InputSystem.settings.backgroundBehavior;
        var routing = InputSystem.settings.editorInputBehaviorInPlayMode;
        Keyboard keyboard = null;
        try
        {
            d.enabled = false; d.SendMessage("RestartRound");
            Set(d, "<IsPlaying>k__BackingField", true);
            foreach (var h in cannons) h.enabled = false;
            foreach (var h in sharks) h.enabled = false;
            foreach (var f in d.Fighters) { f.enabled = false; f.Eliminate(); }
            var target = d.Fighters[0]; var bot = d.Fighters[1];
            foreach (float side in new[] { 1f, -1f })
            {
                Place(target, new Vector3(0f, (side > 0 ? d.Art.Map.BowHeight : d.Art.Map.SternHeight) + 0.9f, side > 0 ? 9f : -14f));
                target.Body.isKinematic = true;
                Place(bot, new Vector3(1.2f, d.DeckY + 0.9f, side > 0 ? -0.5f : -4f));
                bot.enabled = true;
                float started = Time.time;
                float deckHeight = side > 0 ? d.Art.Map.BowHeight : d.Art.Map.SternHeight;
                while (Time.time - started < 10f && (bot.Body.position.y < deckHeight + 0.7f || (side > 0 ? bot.Body.position.z < 6f : bot.Body.position.z > -10.8f))) await Task.Delay(40);
                Check(bot.Body.position.y > deckHeight + 0.6f, "Bot usa escalera hacia " + (side > 0 ? "proa" : "popa") + " (" + bot.Body.position + ")");
                bot.enabled = false;
            }

            foreach (var f in d.Fighters) f.enabled = true;
            foreach (var h in cannons) h.enabled = true;
            foreach (var h in sharks) h.enabled = true;
            Camera.main.GetComponent<PirateArenaCamera>().enabled = true;
            d.SendMessage("RestartRound"); d.enabled = true;
            Check(d.Fighters.Count(f => f.IsAlive) == 6, "Ronda normal inicia con seis tripulantes");
            float start = Time.time;
            bool warning = false, projectile = false, shark = false;
            while (!(bool)Get(d, "_finished") && Time.time - start < 70f)
            {
                warning |= cannons.Any(c => (int)Get(c, "phase") == 1);
                projectile |= cannons.Any(c => (int)Get(c, "phase") == 2);
                shark |= sharks.Any(s => (int)Get(s, "phase") != 0);
                await Task.Delay(50);
            }
            Check((bool)Get(d, "_finished") && d.Fighters.Count(f => f.IsAlive) <= 1, "Ronda completa por eliminaciones en " + (Time.time - start).ToString("F1") + " segundos");
            Check(warning && projectile, "Cañones avisan y disparan durante la ronda");
            results.Add("OBS Tiburón activo durante ronda: " + shark);
            await Task.Delay(150);
            Check(cannons.All(c => (int)Get(c, "phase") == 0) && sharks.All(s => (int)Get(s, "phase") == 0), "Peligros quedan limpios al terminar");
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            await Task.Delay(150);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Check(d.Fighters.Count(f => f.IsAlive) == 6 && !d.IsPlaying, "R reinicia los seis y la cuenta atrás");
            Check(d.Fighters.All(f => !f.IsHeld && !f.IsGrabbing && !f.Body.isKinematic), "Reinicio limpia agarres y cuerpos congelados");
            File.WriteAllLines("Temp/AnimationChecks/round-results.txt", results);
            return results.ToArray();
        }
        catch (Exception e) { results.Add(e.ToString()); File.WriteAllLines("Temp/AnimationChecks/round-results.txt", results); throw; }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            previous?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = bg;
            InputSystem.settings.editorInputBehaviorInPlayMode = routing;
        }
    }
}
