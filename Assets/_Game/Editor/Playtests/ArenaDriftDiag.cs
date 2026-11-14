using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>Is a standing fighter stable on the deck in isolation? Measures drift for the human and a bot slot.</summary>
public static class ArenaDriftDiag
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static void Set(object o, string n, object v) { var f = o.GetType().GetField(n, F); if (f != null) f.SetValue(o, v); else o.GetType().GetProperty(n, F).SetValue(o, v); }
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        var sb = new StringBuilder();
        d.enabled = false; d.SendMessage("RestartRound"); Set(d, "<IsPlaying>k__BackingField", true);
        foreach (var c in Object.FindObjectsByType<CannonHazard>()) c.enabled = false;
        foreach (var s in Object.FindObjectsByType<SharkHazard>()) s.enabled = false;
        // Park everyone except the tested fighter far apart on the stern deck, frozen.
        for (int k = 0; k < 2; k++)
        {
            var test = d.Fighters[k == 0 ? 0 : 1];
            int i = 0;
            foreach (var f in d.Fighters)
            {
                if (f == test) continue;
                f.Body.isKinematic = true; f.transform.position = new Vector3(-4f + i * 2f, d.Art.Map.SternHeight + 1f, -15f); f.Body.position = f.transform.position; i++;
            }
            Vector3 p = new Vector3(0f, d.DeckY + .95f, d.Art.Map.CenterCourtZ + 2.5f);
            test.Respawn(); test.transform.SetPositionAndRotation(p, Quaternion.identity); test.Body.position = p;
            test.Body.linearVelocity = test.Body.angularVelocity = Vector3.zero; Set(test, "botThink", 100000f); Set(test, "move", Vector3.zero);
            float end = Time.time + 2.2f; float maxV = 0;
            await Task.Delay(300);
            Vector3 settled = test.Body.position;
            while (Time.time < end) { maxV = Mathf.Max(maxV, test.Body.linearVelocity.magnitude); await Task.Delay(25); }
            Vector3 drift = test.Body.position - settled; drift.y = 0;
            sb.Append($"{test.DisplayName}: deriva tras asentarse={drift.magnitude:0.00} m, vmax={maxV:0.00}, up={test.transform.up.y:0.00}, suelo={test.IsGrounded}\n");
            foreach (var f in d.Fighters) f.Body.isKinematic = false;
        }
        d.enabled = true; foreach (var c in Object.FindObjectsByType<CannonHazard>()) c.enabled = true; foreach (var s in Object.FindObjectsByType<SharkHazard>()) s.enabled = true;
        d.SendMessage("RestartRound");
        return sb.ToString();
    }
}
