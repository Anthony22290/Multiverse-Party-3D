using System.Linq; using System.Text; using UnityEngine;
public static class RaceProbe2
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            sb.Append("ROOT ").Append(root.name).Append(" active=").Append(root.activeSelf).Append(" children=").Append(root.transform.childCount).Append('\n');
            var groups = root.transform.Cast<Transform>().GroupBy(t => System.Text.RegularExpressions.Regex.Replace(t.name, @"[\d\s]+$", ""));
            foreach (var g in groups.Take(60)) { var t = g.First(); sb.Append("   ").Append(g.Key).Append(" x").Append(g.Count()).Append(" @").Append(t.position.ToString("F0")).Append(t.GetComponent<Collider>() ? " col" : "").Append('\n'); }
        }
        sb.Append("MATS ").Append(string.Join(", ", System.IO.Directory.GetFiles("Assets/003_Scripts/CrazyRacers/Generated", "*.mat").Select(System.IO.Path.GetFileNameWithoutExtension)));
        return sb.ToString();
    }
}
