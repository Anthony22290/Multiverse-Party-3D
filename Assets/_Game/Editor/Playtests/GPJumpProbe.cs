using System; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class GPJumpProbe
{
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<CrazyKartDirector>(); var sb = new StringBuilder();
        for (int i = 48; i <= 53; i++) sb.Append("wp" + i + "=" + d.GetWaypointPosition(i).ToString("F1") + "\n");
        var k = d.Karts.First(); foreach (var o in d.Karts) { o.IsHuman = false; if (o != k) o.gameObject.SetActive(false); }
        typeof(CrazyKartDirector).GetField("countdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(d, 0f);
        k.CurrentCheckpoint = 48; k.Recover(); var b = k.GetComponent<Rigidbody>();
        float lastRec = k.LastRecoveryTime;
        for (int s = 0; s < 70; s++)
        {
            await Task.Delay(100);
            sb.Append($"t={s * .1f:F1} pos={b.position:F1} vel={b.linearVelocity:F1} cp={k.CurrentCheckpoint} gr={k.Grounded} boost={k.Boosting} stun={k.Stunned}{(k.LastRecoveryTime > lastRec ? " RECOVER" : "")}\n");
            lastRec = k.LastRecoveryTime;
        }
        foreach (var o in d.Karts) o.gameObject.SetActive(true);
        return sb.ToString();
    }
}
