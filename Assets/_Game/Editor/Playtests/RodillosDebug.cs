using System.Linq; using System.Text; using UnityEngine;
public static class RodillosDebug
{
    public static string Dump()
    {
        var d = Object.FindAnyObjectByType<RodillosDirector>(); if (d == null) return "sin director";
        var sb = new StringBuilder($"state={d.State} t={d.Elapsed:0.00} cd={d.Countdown:0.0}\n");
        foreach (var r in d.Runners) { var p = r.transform.position; var tile = d.Arena.TileAt(p); sb.AppendLine($"{r.DisplayName} pos={p} alive={r.Alive} ground={d.Arena.Ground(p, d.Elapsed)} tile={(tile != null ? tile.Index + " r" + tile.R0 + "-" + tile.R1 + " a" + tile.A0 + "-" + tile.A1 : "null")} ang={RodillosArena.AngleOf(p):0}"); }
        return sb.ToString();
    }
}
public static class RodillosDebug2
{
    public static string Fast() { Time.timeScale = 2f; return "x2"; }
}
