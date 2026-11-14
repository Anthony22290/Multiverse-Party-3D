using System; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class GPStuckProbe
{
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<CrazyKartDirector>(); var sb = new StringBuilder();
        foreach (var k in d.Karts) k.IsHuman = false;
        sb.Append("wp0=").Append(d.GetWaypointPosition(0)).Append(" wp1=").Append(d.GetWaypointPosition(1)).Append(" count=").Append(d.Waypoints.Count).Append('\n');
        Time.timeScale = 3; for (int s = 0; s < 40; s++)
        {
            await Task.Delay(1000);
            foreach (var k in d.Karts)
            {
                if (k.Speed > 6 && !k.Stunned) continue;
                var b = k.GetComponent<Rigidbody>();
                sb.Append($"t={d.RoundTime:F1} {k.DisplayName} pos={b.position:F1} v={k.Speed:F1} cp={k.CurrentCheckpoint} lap={k.CurrentLap} stun={k.Stunned} grounded={k.Grounded}");
                if (k.Speed < 3) foreach (var c in Physics.OverlapBox(b.position, new Vector3(1.2f, .8f, 1.8f), b.rotation, ~0, QueryTriggerInteraction.Ignore)) if (c.attachedRigidbody != b) sb.Append(" near:" + c.name);
                var hit = Physics.Raycast(b.position + Vector3.up * .15f, Vector3.down, out var h, 3, ~0, QueryTriggerInteraction.Ignore);
                sb.Append(hit ? " ground=" + h.collider.name + " y=" + h.point.y.ToString("F2") : " noground").Append('\n');
            }
        }
        Time.timeScale = 1; return sb.ToString();
    }
}
