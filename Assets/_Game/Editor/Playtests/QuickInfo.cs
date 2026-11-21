using System.Linq; using UnityEngine; using UnityEngine.SceneManagement;
/// <summary>run QuickInfo.Scene → escena activa y directores presentes (para depurar pruebas).</summary>
public static class QuickInfo
{
    public static string Scene() => SceneManager.GetActiveScene().path + " · play=" + Application.isPlaying + " · pausa editor=" + UnityEditor.EditorApplication.isPaused + " · timeScale=" + Time.timeScale + " · " + string.Join(",", Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Select(m => m.GetType().Name).Where(n => n.EndsWith("Director")).Distinct());
}
