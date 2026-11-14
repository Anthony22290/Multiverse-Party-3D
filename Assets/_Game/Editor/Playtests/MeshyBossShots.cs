#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>En Play en InvasionZombi: aparecen los 4 jefes con modelo Meshy junto al jugador, se fotografían persiguiendo/golpeando y se prueba la muerte.</summary>
public static class MeshyBossShots
{
    public static async Task<string> Run()
    {
        SurvivalDirector d = null;
        for (int i = 0; i < 60 && d == null; i++) { d = Object.FindAnyObjectByType<SurvivalDirector>(); if (d == null || d.Level == null || d.HUD == null) { d = null; await Task.Delay(200); } }
        if (d == null || !Application.isPlaying) return "FAIL InvasionZombi no está en Play";
        var lines = new List<string>();
        string dir = Path.GetFullPath("Docs/Claude_Capturas/Meshy"); Directory.CreateDirectory(dir);
        d.AutoHuman = true; d.StartGame(2);
        await Task.Delay(1500);
        var h = d.Human.transform;
        var kinds = new[] { SurvivalZombie.BossKind.Gordo, SurvivalZombie.BossKind.Brazos, SurvivalZombie.BossKind.Costillas, SurvivalZombie.BossKind.Rey };
        var bosses = new List<SurvivalZombie>();
        for (int i = 0; i < kinds.Length; i++)
        {
            Vector3 at = h.position + Quaternion.Euler(0, -60 + i * 40, 0) * h.forward * 7f;
            var z = d.Spawn(new SurvivalZombie.Spec { kind = SurvivalZombie.ZombieKind.Basic, boss = kinds[i], wave = 3, part = -1 }, -1, at);
            bosses.Add(z);
            var skin = z.GetComponentInChildren<SkinnedMeshRenderer>();
            var mat = skin != null ? skin.sharedMaterial : null;
            lines.Add($"{(skin != null && mat != null && mat.name == kinds[i].ToString() ? "PASS" : "FAIL")} {kinds[i]}: modelo={(skin != null ? skin.sharedMesh.name : "ninguno")} material={(mat ? mat.name : "-")} animator={(z.GetComponentInChildren<Animator>() != null)}");
        }
        var cam = d.View.GetComponent<Camera>();
        await Task.Delay(1200); ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, "jefes_1.png"), 1280, 720);
        await Task.Delay(2500); ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, "jefes_2.png"), 1280, 720);
        foreach (var z in bosses.Take(2)) if (z != null) z.TakeHit(5000f, z.transform.forward * -1f, d.Human);
        await Task.Delay(700); ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, "jefes_muerte.png"), 1280, 720);
        lines.Add($"{(bosses.Take(2).All(z => z == null || z.Dead) ? "PASS" : "FAIL")} muerte procedural de Gordo y Brazos");
        return string.Join("\n", lines);
    }
}
#endif
