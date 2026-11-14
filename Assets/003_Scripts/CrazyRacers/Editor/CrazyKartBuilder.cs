using UnityEditor;
using UnityEditor.SceneManagement;
public static class CrazyKartBuilder
{
    // The former generator replaced the authored scene and generated invalid tags.
    // Open the saved circuit instead, preserving scene edits and stable references.
    [MenuItem("Multiverse/Abrir Carreras Locas")]
    public static void BuildScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/001_Scenes/CarrerasLocas.unity");
    }
}
