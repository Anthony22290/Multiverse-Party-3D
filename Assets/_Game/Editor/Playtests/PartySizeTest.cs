#if UNITY_EDITOR
using System.Linq; using System.Text; using UnityEngine;
/// <summary>2026-11-17 · El tamaño elegido (4, 5 o 6) es el número de jugadores de todos los minijuegos. run PartySizeTest.Run (en Play).</summary>
public static class PartySizeTest
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (int size in new[] { 4, 5, 6 })
        {
            var p = MultiverseParty.StartOffline(size, false);
            int roster = p.Roster().Count, members = p.Members.Count;
            p.SetTargetSize(size);
            sb.AppendLine($"{(roster == size && members == size ? "PASS" : "FAIL")} sala de {size}: miembros {members} · jugadores por minijuego {roster}");
            Object.Destroy(p.gameObject);
        }
        return sb.ToString();
    }
}
#endif
