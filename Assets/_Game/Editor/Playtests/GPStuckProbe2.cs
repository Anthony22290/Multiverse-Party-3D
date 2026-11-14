using System; using System.Linq; using System.Text; using System.Threading.Tasks; using UnityEngine; using Object = UnityEngine.Object;
public static class GPStuckProbe2
{
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<CrazyKartDirector>(); var sb = new StringBuilder();
        for (int i = 12; i <= 18; i++) sb.Append("wp" + i + "=" + d.GetWaypointPosition(i).ToString("F1") + "\n");
        foreach (var g in Object.FindObjectsByType<CrazyKartCheckpointIndex>(FindObjectsSortMode.None).Where(c => c.index >= 14 && c.index <= 17).OrderBy(c => c.index))
            sb.Append("gate " + g.index + " " + g.name + " pos=" + g.transform.position.ToString("F1") + " fwd=" + g.transform.forward.ToString("F2") + " size=" + g.GetComponent<BoxCollider>().size + "\n");
        var k = d.Karts.First(); foreach (var o in d.Karts) o.IsHuman = false;
        foreach (var o in d.Karts) if (o != k) o.gameObject.SetActive(false);
        typeof(CrazyKartDirector).GetField("countdown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(d, 0f);
        k.CurrentCheckpoint = 14; k.Recover();
        var b = k.GetComponent<Rigidbody>(); var rec = k.GetComponent<CrazyKartRecovery>();
        for (int s = 0; s < 40; s++)
        {
            await Task.Delay(150);
            var to = d.GetWaypointPosition(k.NextCheckpoint) - b.position; to.y = 0;
            sb.Append($"t={s * .15f:F2} pos={b.position:F1} v={k.Speed:F1} cp={k.CurrentCheckpoint} ang={Vector3.SignedAngle(k.transform.forward, to, Vector3.up):F0} stun={k.Stunned} gr={k.Grounded} autoRec={rec.AutomaticRecoveries}");
            foreach (var c in Physics.OverlapBox(b.position, new Vector3(1.4f, .9f, 2.2f), b.rotation, ~0, QueryTriggerInteraction.Ignore)) if (c.attachedRigidbody != b && c.name != "Carretera sólida") sb.Append(" near:" + c.name);
            sb.Append('\n');
        }
        foreach (var o in d.Karts) o.gameObject.SetActive(true);
        return sb.ToString();
    }
}
