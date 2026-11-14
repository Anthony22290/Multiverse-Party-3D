#if UNITY_EDITOR
using System.IO; using System.Threading.Tasks; using UnityEngine;
/// <summary>En Play en SalaDeEscape o SalaDeEscape_Juguete: mismas vistas en las dos para comparar estilos.</summary>
public static class EscapeStyleShots
{
    public static async Task<string> Run()
    {
        var d = Object.FindAnyObjectByType<EscapeDirector>();
        if (d == null || d.Rooms == null || !Application.isPlaying) return "FAIL sin director en Play";
        var toy = Object.FindAnyObjectByType<EscapeToyStyle>();
        string tag = toy != null ? "Juguete" : "Original";
        string dir = Path.GetFullPath("Docs/Claude_Capturas/Escape/Comparar"); Directory.CreateDirectory(dir);
        if (d.State == EscapeDirector.Phase.Lobby) { d.AutoHuman = true; d.StartGame(4); }
        await Task.Delay(2500);
        var cam = new GameObject("Cámara comparar").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.07f, .05f, .16f); cam.fieldOfView = 60f; cam.nearClipPlane = .05f;
        cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
        int n = 0;
        for (int i = 0; i < Mathf.Min(4, d.Rooms.Length); i++)
        {
            var room = d.Rooms[i];
            cam.transform.position = room.W(new Vector3(0, 9f, -12.5f)); cam.transform.LookAt(room.W(new Vector3(0, 0, 1f)));
            ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, $"{tag}_sala{i + 1}_arriba.png"), 960, 540); n++;
            cam.transform.position = room.W(new Vector3(-3f, 1.6f, -7.5f)); cam.transform.LookAt(room.W(new Vector3(1f, 1.4f, 6f)));
            ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, $"{tag}_sala{i + 1}_ojos.png"), 960, 540); n++;
        }
        if (d.Actors.Count > 0)
        {
            var a = d.Actors[Mathf.Min(1, d.Actors.Count - 1)].transform;
            cam.transform.position = a.position + a.forward * 3.2f + Vector3.up * 1.6f; cam.transform.LookAt(a.position + Vector3.up * .9f);
            ClaudeBridge.CaptureCamera(cam, Path.Combine(dir, $"{tag}_jugadores.png"), 960, 540); n++;
        }
        Object.Destroy(cam.gameObject);
        string extra = toy != null ? $" · bloques redondeados {toy.MeshesRounded} · materiales {toy.MaterialsSwapped} · personajes {toy.ActorsSwapped}" : "";
        return $"PASS {tag}: {n} capturas{extra}";
    }
}
#endif
