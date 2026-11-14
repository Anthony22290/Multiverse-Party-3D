using UnityEditor;
using UnityEngine;

/// <summary>Creates the shared UI skin from resources already in the project (Kenney UI Pack 2.0, CC0).</summary>
public static class MultiverseUISetup
{
    const string SkinPath = "Assets/_Game/Resources/MultiverseUISkin.asset";
    const string Kenney = "Assets/002_Modelos/interfaces/Interfaces/";

    [MenuItem("Multiverse/Arte/0 · Crear skin de interfaz", priority = 0)]
    public static void Menu() => Debug.Log(EnsureSkin());

    public static string EnsureSkin()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources")) AssetDatabase.CreateFolder("Assets/_Game", "Resources");
        var skin = AssetDatabase.LoadAssetAtPath<MultiverseUISkin>(SkinPath);
        if (skin == null) { skin = ScriptableObject.CreateInstance<MultiverseUISkin>(); AssetDatabase.CreateAsset(skin, SkinPath); }
        if (skin.display == null) skin.display = AssetDatabase.LoadAssetAtPath<Font>(Kenney + "Font/Kenney Future.ttf");
        if (skin.body == null) skin.body = AssetDatabase.LoadAssetAtPath<Font>(Kenney + "Font/Kenney Future Narrow.ttf");
        if (skin.click == null) skin.click = AssetDatabase.LoadAssetAtPath<AudioClip>(Kenney + "Sounds/click-a.ogg");
        if (skin.hover == null) skin.hover = AssetDatabase.LoadAssetAtPath<AudioClip>(Kenney + "Sounds/tap-a.ogg");
        EditorUtility.SetDirty(skin); AssetDatabase.SaveAssets();
        return "UI skin: display=" + (skin.display != null) + " body=" + (skin.body != null) + " click=" + (skin.click != null);
    }
}
