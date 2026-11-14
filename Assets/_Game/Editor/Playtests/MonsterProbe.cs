using System.Linq; using System.Text; using UnityEditor; using UnityEngine;
public static class MonsterProbe
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { "Assets/002_Modelos/mounstrous/Big-20261027T131625Z-1-001/Big/FBX/Orc.fbx", "Assets/002_Modelos/mounstrous/Big-20261027T131625Z-1-001/Big/FBX/Demon.fbx",
            "Assets/002_Modelos/mounstrous/Blob-20261027T131626Z-1-001/Blob/FBX/GreenBlob.fbx", "Assets/002_Modelos/mounstrous/Flying-20261027T131626Z-1-001/Flying/FBX/Ghost.fbx",
            "Assets/002_Modelos/mounstrous/Big-20261027T131625Z-1-001/Big/FBX/Orc_Skull.fbx", "Assets/002_Modelos/mounstrous/Big-20261027T131625Z-1-001/Big/FBX/Yeti.fbx" })
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).Select(c => c.name + "(" + c.length.ToString("F1") + ")");
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var r = go ? go.GetComponentInChildren<Renderer>() : null;
            sb.Append(System.IO.Path.GetFileName(path)).Append(" anim=").Append(go && go.GetComponentInChildren<Animator>() ? "yes" : "no").Append(" mat=").Append(r ? string.Join(",", r.sharedMaterials.Select(m => m ? m.name + "/" + (m.mainTexture ? m.mainTexture.name : "-") : "null")) : "-")
              .Append(" size=").Append(r ? r.bounds.size.ToString("F2") : "-").Append("\n  ").Append(string.Join(" ", clips)).Append('\n');
        }
        var sk = Resources.Load<GameObject>("PirateCharacters/Characters_Skeleton");
        var an = sk ? sk.GetComponentInChildren<Animator>() : null;
        sb.Append("Skeleton animator=").Append(an ? (an.runtimeAnimatorController ? an.runtimeAnimatorController.name + " clips=" + string.Join(" ", an.runtimeAnimatorController.animationClips.Select(c => c.name)) : "no controller") : "none").Append('\n');
        var art = Resources.Load<SurvivalArtSet>("SurvivalArtSet");
        sb.Append("Zombie controller states: ");
        var ctrl = art.zombies[0].GetComponentInChildren<Animator>()?.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl != null) sb.Append(ctrl.name + ": " + string.Join(" ", ctrl.layers[0].stateMachine.states.Select(s => s.state.name + "=" + (s.state.motion ? s.state.motion.name : "-"))));
        return sb.ToString();
    }
}
