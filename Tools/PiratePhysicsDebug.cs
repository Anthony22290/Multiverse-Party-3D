using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PiratePhysicsDebug
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object obj, string key, object value) => obj.GetType().GetField(key, Flags).SetValue(obj, value);
    static object Get(object obj, string key) => obj.GetType().GetField(key, Flags).GetValue(obj);
    static async Task Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) await Task.Delay(25); }

    public static async Task<string[]> Run()
    {
        var lines = new List<string>();
        PirateArenaDirector d = null;
        float waited = 0f;
        while ((d == null || d.Fighters.Count < 6) && waited < 8f)
        {
            await Task.Delay(200); waited += 0.2f;
            d = Object.FindObjectsByType<PirateArenaDirector>().FirstOrDefault(x => x.Fighters.Count >= 6);
        }
        if (d == null || d.Fighters.Count < 6) throw new Exception("El arena no terminó de construirse (tripulantes=" + (d?.Fighters.Count ?? 0) + ", escena=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path + ")");
        d.SendMessage("RestartRound");
        Set(d, "<IsPlaying>k__BackingField", true);
        foreach (var f in d.Fighters) { f.enabled = false; f.Eliminate(); }
        foreach (var f in d.Fighters) f.enabled = true;
        PirateFighter C = d.Fighters[2], W = d.Fighters[3];
        float deck = d.DeckY, court = d.Art.Map.CenterCourtZ;
        float y = deck + 0.95f;
        C.Respawn(); C.Body.interpolation = RigidbodyInterpolation.None;
        W.Respawn(); W.Body.interpolation = RigidbodyInterpolation.None;
        C.transform.SetPositionAndRotation(new Vector3(-1.4f, y, court - 4.5f), Quaternion.identity);
        C.Body.position = new Vector3(-1.4f, y, court - 4.5f);
        W.transform.SetPositionAndRotation(new Vector3(-0.1f, y, court - 4.5f), Quaternion.identity);
        W.Body.position = new Vector3(-0.1f, y, court - 4.5f);
        Set(C, "botThink", 100000f); Set(W, "botThink", 100000f);
        var traces = new List<string>();
        PirateFighter.TraceHook = (message, who) => traces.Add(who + ": " + message);
        for (int i = 0; i < 5; i++)
        {
            await Wait(0.1f);
            lines.Add(i + "s C=" + C.Body.position.ToString("F2") + " vC=" + C.Body.linearVelocity.magnitude.ToString("F2") +
                " W=" + W.Body.position.ToString("F2") + " vW=" + W.Body.linearVelocity.magnitude.ToString("F2") +
                " kinW=" + W.Body.isKinematic);
        }
        Set(C, "facing", (W.Body.position - C.Body.position).normalized);
        Set(C, "grabPressed", true);
        for (int i = 0; i < 12; i++)
        {
            await Task.Delay(25);
            lines.Add(i + " t=" + Time.time.ToString("F2") + " C=" + C.Body.position.ToString("F2") + " W=" + W.Body.position.ToString("F2") +
                " vW=" + W.Body.linearVelocity.magnitude.ToString("F2") + " joint=" + (Get(C, "grabJoint") != null) +
                " holderW=" + (Get(W, "holder") != null));
        }
        bool grabbed = Get(W, "holder") != null;
        lines.Add("ruta Simulate: joint=" + (Get(C, "grabJoint") != null) + " holderW=" + grabbed +
            " facing=" + ((Vector3)Get(C, "facing")).ToString("F2"));
        if (grabbed)
        {
            Set(d, "<IsPlaying>k__BackingField", false);
            return lines.ToArray();
        }
        // Diagnóstico si falló
        var canReach = typeof(PirateFighter).GetMethod("CanReach", Flags);
        lines.Add("CanReach W=" + canReach.Invoke(C, new object[] { W.Body }) +
            " delta=" + (W.Body.worldCenterOfMass - C.Body.worldCenterOfMass).magnitude.ToString("F2"));
        Vector3 origin = C.Body.position + Vector3.up * 0.25f + (Vector3)Get(C, "facing") * 0.55f;
        var colliders = new Collider[32];
        int count = Physics.OverlapSphereNonAlloc(origin, d.Settings.grabReach * 0.65f, colliders, ~0, QueryTriggerInteraction.Ignore);
        lines.Add("overlap count=" + count + " origin=" + origin.ToString("F2"));
        foreach (var c in colliders.Take(count))
            if (c != null) lines.Add(" " + c.name + (c.attachedRigidbody != null ? " rb=" + c.attachedRigidbody.name : " sin rb"));
        lines.Add("grabPressed residual=" + (bool)Get(C, "grabPressed") + " stun=" + C.IsStunned + " isGrabbing=" + C.IsGrabbing);
        foreach (var trace in traces.Take(20)) lines.Add("TRAZA " + trace);
        PirateFighter.TraceHook = null;
        Set(d, "<IsPlaying>k__BackingField", false);
        return lines.ToArray();
    }
}
