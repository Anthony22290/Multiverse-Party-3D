using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PirateHybridPlaytest
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new List<string>();
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    static void Check(bool ok, string detail) { results.Add((ok ? "PASS " : "FAIL ") + detail); }
    static async Task Wait(float seconds)
    {
        float end = Time.time + seconds;
        var deadline = DateTime.UtcNow.AddSeconds(seconds * 5 + 10);
        while (Time.time < end && DateTime.UtcNow < deadline) await Task.Delay(20);
        if (Time.time < end) throw new Exception("Player loop stopped advancing");
    }
    static void Place(PirateFighter f, Vector3 p)
    {
        f.Respawn(); f.SetControl(false, "Test"); f.NetworkDriven = false;
        f.transform.SetPositionAndRotation(p, Quaternion.identity);
        f.Body.position = p; f.Body.rotation = Quaternion.identity;
        f.Body.linearVelocity = f.Body.angularVelocity = Vector3.zero;
        Set(f, "botThink", 100000f); Set(f, "botThrowAt", 100000f);
        f.GetComponent<PirateActivePuppet>()?.ResetPose(); Physics.SyncTransforms();
    }
    public static async Task<string[]> Run()
    {
        results.Clear();
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        if (d == null || !Application.isPlaying) throw new Exception("Arena must be in Play");
        var a = d.Fighters[0]; var b = d.Fighters[1];
        bool human = a.IsHuman; string label = a.DisplayName;
        try
        {
            d.RestartRound(); d.Tick(4f); d.enabled = false;
            foreach (var h in d.Cannons) h.enabled = false;
            foreach (var h in d.Sharks) h.enabled = false;
            foreach (var prop in d.Props) prop.gameObject.SetActive(false);
            foreach (var f in d.Fighters) { f.NetworkDriven = true; f.Eliminate(); }
            var p = new Vector3(0f, d.DeckY + 1f, d.Art.Map.CenterCourtZ);
            Place(a, p); Place(b, p + Vector3.right * 4f);
            await Wait(.5f);
            Check(a.GetComponent<PirateActivePuppet>().Ready, "Active physical puppet initialized");
            float startY = a.Body.position.y, peakY = startY, maxVy = 0f;
            Set(a, "jumpBufferedUntil", Time.time + .13f);
            float end = Time.time + 2f;
            while (Time.time < end) { peakY = Mathf.Max(peakY, a.Body.position.y); maxVy = Mathf.Max(maxVy, a.Body.linearVelocity.y); await Wait(.02f); }
            Check(peakY - startY > .35f && peakY - startY < 2f && a.Body.position.y < startY + .4f,
                "Jump rises and lands: height=" + (peakY-startY).ToString("F2") + ", maxVy=" + maxVy.ToString("F2"));
            Place(a, p + Vector3.up * 6f);
            float fallStart = a.Body.position.y, rebound = 0f;
            end = Time.time + 3f;
            while (Time.time < end) { rebound = Mathf.Max(rebound, a.Body.linearVelocity.y); await Wait(.02f); }
            Check(a.Body.position.y < fallStart - 4f && rebound < 3f && a.IsAlive,
                "6m fall lands without relaunch: upward=" + rebound.ToString("F2") + ", y=" + a.Body.position.y.ToString("F2"));
            Place(a, p); await Wait(.4f);
            a.TakeCombatHit(Vector3.zero); Check(!a.IsKnockedOut, "One combat hit does not KO");
            await Wait(.3f); a.TakeCombatHit(Vector3.zero);
            await Wait(.3f); a.TakeCombatHit(Vector3.zero);
            Check(a.IsKnockedOut && a.IsStunned && a.IsAlive, "Three close hits cause temporary KO, not elimination");
            a.TakeCombatHit(Vector3.zero);
            await Wait(d.Settings.knockoutSeconds + .15f);
            Check(!a.IsKnockedOut && !a.IsStunned && a.IsAlive, "KO recovers without indefinite hit extension");
            a.TakeCombatHit(Vector3.zero); Check(!a.IsStunned, "Recovery protection prevents immediate stun lock");
            Place(a, p); Place(b, p + Vector3.forward * 1.1f); await Wait(.15f);
            Set(a, "facing", Vector3.forward); Set(a, "lookDirection", Vector3.forward);
            Set(a, "leftReach", true); Set(a, "rightReach", true); Set(a, "grabHeld", true); Set(a, "grabPressed", true);
            float gripDeadline = Time.time + .8f;
            while (!(a.IsGrabbing && b.IsHeld) && Time.time < gripDeadline) await Wait(.02f);
            Check(a.IsGrabbing && b.IsHeld, "Physical hand grip captures opponent: left=" + a.LeftGripped + ", right=" + a.RightGripped);
            float initialStamina = a.GripStamina;
            await Wait(.3f);
            Check(a.GripStamina < initialStamina, "Holding opponent consumes stamina");
            // Bot carry navigation now deliberately turns toward an exit. Fix the
            // launch direction here; its autonomous strategy has a separate test.
            Set(a, "facing", Vector3.forward); Set(a, "move", Vector3.zero);
            a.ReleaseGrab(true); Set(a, "leftReach", false); Set(a, "rightReach", false); Set(a, "grabHeld", false);
            Set(a, "blockGrabUntilRelease", true);
            await Wait(.08f);
            Check(!a.IsGrabbing && !b.IsHeld && b.Body.linearVelocity.z > 1f, "Throw releases both hands and propels opponent");
            Place(a, p); Place(b, p + Vector3.forward * 1.1f); await Wait(.15f);
            Set(a, "leftReach", true); Set(a, "grabHeld", true); Set(a, "grabPressed", true);
            await Wait(.15f); Set(a, "gripStamina", .001f); await Wait(.1f);
            Check(!a.IsGrabbing && !b.IsHeld, "Exhausted grip releases without throwing");
            Set(a, "leftReach", false); Set(a, "grabHeld", false); await Wait(.5f);
            Check(a.GripStamina > .1f, "Rest restores grip stamina");
            d.RestartRound(); d.Tick(4f);
            foreach (var f in d.Fighters.Skip(1)) d.Eliminate(f, "test");
            d.Tick(.02f);
            Check(d.Winner == a && !d.IsPlaying, "Last survivor wins");
            d.RestartRound(); d.Tick(4f);
            foreach (var f in d.Fighters) d.Eliminate(f, "test");
            d.Tick(.02f);
            Check(d.Winner == null && !d.IsPlaying, "Simultaneous elimination has no false winner");
            d.RestartRound();
            Check(d.Fighters.All(f => f.IsAlive && !f.IsKnockedOut && f.GripStamina == 1f && !f.IsHeld), "Restart clears KO and grips for all fighters");
        }
        catch (Exception e) { results.Add("ERROR " + e); }
        finally
        {
            a.SetControl(human, label);
            foreach (var f in d.Fighters) f.NetworkDriven = false;
            foreach (var h in d.Cannons) h.enabled = true;
            foreach (var h in d.Sharks) h.enabled = true;
            foreach (var prop in d.Props) prop.gameObject.SetActive(true);
            d.RestartRound(); d.enabled = true;
            File.WriteAllLines("Docs/Arena_Hybrid_Test_2026-11-03.txt", results);
        }
        return results.ToArray();
    }
}
