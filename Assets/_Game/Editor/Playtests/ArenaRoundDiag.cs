using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Plays a normal six-bot round (human slot idle, as in PirateRoundPlaytest) and logs the timeline.</summary>
public static class ArenaRoundDiag
{
    public static float Recoil = -1f, RunUp = -1f;
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        var s = d.Settings; float oldRecoil = s.pushRecoil, oldRun = s.pushRunUpBonus;
        if (Recoil >= 0) s.pushRecoil = Recoil; if (RunUp >= 0) s.pushRunUpBonus = RunUp;
        var sb = new StringBuilder();
        try
        {
            if (PirateSession.Instance != null) typeof(PirateSession).GetMethod("SetMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(PirateSession.Instance, new object[] { false });
            d.SendMessage("RestartRound");
            float start = Time.time; int last = 6; var floatSeen = new System.Collections.Generic.Dictionary<PirateFighter, float>(); float minY = 99, maxY = -99;
            while (!d.Finished && Time.time - start < 100f)
            {
                foreach (var f in d.Fighters)
                    if (!f.IsAlive && !floatSeen.ContainsKey(f)) floatSeen[f] = Time.time;
                foreach (var kv in floatSeen)
                {
                    float since = Time.time - kv.Value;
                    if (since > .6f && since < 2.6f) { float y = kv.Key.Body.position.y; minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); }
                }
                int alive = d.Fighters.Count(f => f.IsAlive);
                if (alive != last) { sb.Append($"t={Time.time - start:0.0}s vivos={alive} ({string.Join(",", d.Fighters.Where(f => f.IsAlive).Select(f => f.DisplayName))})\n"); last = alive; }
                await Task.Delay(100);
            }
            sb.Insert(0, $"recoil={s.pushRecoil} runUp={s.pushRunUpBonus} terminada={d.Finished} en {Time.time - start:0.0}s ganador={(d.Winner != null ? d.Winner.DisplayName : "-")}; eliminados flotando 0,6-2,6 s después: y entre {minY:0.00} y {maxY:0.00}\n");
        }
        finally { s.pushRecoil = oldRecoil; s.pushRunUpBonus = oldRun; }
        return sb.ToString();
    }
    public static string NoRecoil() { Recoil = 0; RunUp = 0; return "sin retroceso ni carrera"; }
    public static string Defaults() { Recoil = -1; RunUp = -1; return "valores del asset"; }
}
