using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Creates the particle texture and materials used by PirateArenaFx (Assets/_Game/Resources/ArenaFx).</summary>
public static class ArenaFxSetup
{
    const string Dir = "Assets/_Game/Resources/ArenaFx";

    [MenuItem("Multiverse/Arena/Crear materiales de efectos")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources")) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_Game/Resources", "ArenaFx");
        string texPath = Dir + "/Punto suave.png";
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32)) / 32f;
            float a = Mathf.Clamp01(1f - d); a = a * a * (3f - 2f * a);
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.alphaIsTransparency = true; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        foreach (var (name, add) in new[] { ("Particula suave", false), ("Particula brillante", true) })
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            PirateArenaFx.ConfigureParticleMaterial(m, add, t);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        return "Materiales de efectos listos en " + Dir;
    }
}
