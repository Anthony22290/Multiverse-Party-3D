using System.Linq;
using System.Text;
using UnityEngine;
public static class RaceProbe
{
    static void Dump(Transform t, StringBuilder sb, int depth, int max)
    {
        var r = t.GetComponent<Renderer>(); var mf = t.GetComponent<MeshFilter>();
        sb.Append(new string(' ', depth * 2)).Append(t.name).Append(" pos=").Append(t.localPosition).Append(" scale=").Append(t.localScale)
          .Append(r != null ? " mat=" + string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name + "/" + m.shader.name)) : "")
          .Append(mf != null && mf.sharedMesh != null ? " mesh=" + mf.sharedMesh.name + " b=" + mf.sharedMesh.bounds.size : "")
          .Append(" comps=" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name))).Append('\n');
        if (depth < max) foreach (Transform c in t) Dump(c, sb, depth + 1, max);
    }
    public static string Run()
    {
        var sb = new StringBuilder();
        var kart = Object.FindObjectsByType<CrazyKartController>(FindObjectsSortMode.None).First(k => k.gameObject.activeInHierarchy);
        Dump(kart.transform, sb, 0, 3);
        var box = Object.FindAnyObjectByType<CrazyKartItemBox>(); sb.Append("BOXES ").Append(Object.FindObjectsByType<CrazyKartItemBox>(FindObjectsSortMode.None).Length).Append('\n'); Dump(box.transform, sb, 0, 3);
        foreach (var p in Object.FindObjectsByType<CrazyKartBoostPad>(FindObjectsSortMode.None).Take(2)) Dump(p.transform, sb, 0, 2);
        sb.Append("BOOSTPADS ").Append(Object.FindObjectsByType<CrazyKartBoostPad>(FindObjectsSortMode.None).Length).Append('\n');
        var d = Object.FindAnyObjectByType<CrazyKartDirector>(); sb.Append("WP0 ").Append(d.GetWaypointPosition(0)).Append(" WP1 ").Append(d.GetWaypointPosition(1)).Append('\n');
        var cp = Object.FindObjectsByType<CrazyKartCheckpointIndex>(FindObjectsSortMode.None).First(c => c.index == 0); Dump(cp.transform, sb, 0, 1);
        var cam = Object.FindAnyObjectByType<CrazyKartCamera>(); sb.Append("CAM ").Append(cam.name).Append(" target=").Append(cam.target ? cam.target.name : "none").Append('\n');
        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent == null)) sb.Append("ROOT ").Append(go.name).Append('\n');
        sb.Append("URP particles shader: ").Append(Shader.Find("Universal Render Pipeline/Particles/Unlit") != null);
        return sb.ToString();
    }
}
