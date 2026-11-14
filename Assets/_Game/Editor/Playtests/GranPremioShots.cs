using System.IO; using UnityEngine; using UnityEditor;
public static class GranPremioShots
{
    static string Render(string file, Vector3 pos, Vector3 look, float fov, bool ortho = false, float size = 0)
    {
        var go = new GameObject("Cámara captura"); var cam = go.AddComponent<Camera>();
        cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = fov; cam.farClipPlane = 2000;
        if (ortho) { cam.orthographic = true; cam.orthographicSize = size; }
        var rt = new RenderTexture(1600, 1000, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(1600, 1000, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); tex.Apply();
        RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        Directory.CreateDirectory("Docs/Claude_Capturas/Carreras"); File.WriteAllBytes("Docs/Claude_Capturas/Carreras/" + file + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        return file;
    }
    public static string Run()
    {
        Render("50_Puente_entrada", new Vector3(345, 8, 25), new Vector3(332, 1, -10), 55);
        Render("51_Pueblo", new Vector3(200, 14, 95), new Vector3(240, 0, 45), 60);
        Render("52_Montana", new Vector3(300, 45, -150), new Vector3(220, 12, -170), 60);
        Render("53_Tunel_acantilado", new Vector3(70, 30, -150), new Vector3(60, 14, -210), 60);
        Render("54_Selva_rio", new Vector3(-150, 25, -20), new Vector3(-215, 2, -45), 60);
        Render("55_Cueva", new Vector3(230, 30, 130), new Vector3(255, 2, 85), 55);
        return "ok";
    }
}
