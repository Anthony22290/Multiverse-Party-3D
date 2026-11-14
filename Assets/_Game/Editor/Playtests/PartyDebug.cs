#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
public static class PartyDebug
{
    public static string Materials()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.name == "Aro" || r.name == "Farol").Take(6))
        {
            var m = r.sharedMaterial;
            sb.AppendLine(r.name + " shader=" + (m.shader != null ? m.shader.name : "null") + " base=" + m.GetColor("_BaseColor") + " emi=" + m.GetColor("_EmissionColor") + " kw=" + string.Join(",", m.shaderKeywords));
        }
        return sb.ToString();
    }
    public static string Shot(string name, Vector3 pos, Vector3 look, float fov = 50)
    {
        var go = new GameObject("Cámara de captura"); var cam = go.AddComponent<Camera>();
        var main = Camera.main; if (main != null) { cam.CopyFrom(main); }
        cam.fieldOfView = fov; go.transform.position = pos; go.transform.LookAt(look);
        var data = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
        string path = System.IO.Path.GetFullPath(Application.dataPath + "/../Docs/Claude_Capturas/Partida/" + name + ".png");
        ClaudeBridge.CaptureCamera(cam, path, 1600, 900); Object.Destroy(go);
        return path;
    }
    public static string HubOverview() { Shot("03_Plaza_aerea", new Vector3(0, 48, -42), new Vector3(0, 0, 4), 55); Shot("04_Portales", new Vector3(0, 9, 2), new Vector3(0, 3, 24), 70); return Shot("05_Torre_podio", new Vector3(8, 7, -10), new Vector3(-6, 1.5f, 0), 60); }
    public static string Karts()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var k in Object.FindObjectsByType<CrazyKartController>(FindObjectsSortMode.None))
        {
            sb.AppendLine(k.name + " human=" + k.IsHuman + " name=" + k.DisplayName + " color=" + k.KartColor + " pos=" + k.transform.position + " fwd=" + k.transform.forward);
            foreach (var r in k.GetComponentsInChildren<Renderer>()) sb.AppendLine("   " + r.name + " : " + string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name)));
        }
        return sb.ToString();
    }
    public static async System.Threading.Tasks.Task<string> ChampionShot()
    {
        var p = MultiverseParty.StartOffline(6);
        for (int i = 0; i < 60 && Object.FindAnyObjectByType<MultiverseHub>() == null; i++) await System.Threading.Tasks.Task.Delay(100);
        await System.Threading.Tasks.Task.Delay(1500);
        p.Members[0].Score = 5; p.Members[2].Score = 4; p.Members[4].Score = 2; p.Members[1].Score = 1;
        p.Phase = PartyPhase.Champion;
        await System.Threading.Tasks.Task.Delay(5000);
        ClaudeBridge.CaptureCamera(Camera.main, System.IO.Path.GetFullPath(Application.dataPath + "/../Docs/Claude_Capturas/Partida/12_Campeon.png"), 1280, 720);
        return "ok";
    }
    public static async System.Threading.Tasks.Task<string> PlazaDemo()
    {
        var p = MultiverseParty.StartOffline(6);
        for (int i = 0; i < 80 && Object.FindAnyObjectByType<MultiverseHub>() == null; i++) await System.Threading.Tasks.Task.Delay(100);
        await System.Threading.Tasks.Task.Delay(2500);
        var hub = Object.FindAnyObjectByType<MultiverseHub>();
        var walker = GameObject.Find("Explorador").GetComponent<CharacterController>();
        // Stand next to a bot and push it.
        int bot = 1; var target = p.Members[bot].Pose;
        walker.enabled = false; walker.transform.position = target + new Vector3(0, .2f, -1.1f); walker.transform.rotation = Quaternion.identity; walker.enabled = true;
        await System.Threading.Tasks.Task.Delay(300);
        p.Emote(0); p.Chat(0);
        p.Push(bot, Vector3.forward);
        await System.Threading.Tasks.Task.Delay(500);
        p.ApplyChat(2, 4); p.ApplyEmote(3, 1);
        var chat = typeof(MultiverseHub).GetField("chatPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(hub) as GameObject;
        chat.SetActive(true);
        await System.Threading.Tasks.Task.Delay(400);
        var m = p.Members[bot];
        return $"empujones dados {p.Local.Pushes} · {m.Name} recibió {m.Pushed} · gestos {p.Local.Emotes} · chats {p.Local.Chats} · pose bot {m.Pose}";
    }

    public static string LoadingDemo(int game)
    {
        var o = Object.FindAnyObjectByType<MultiversePartyOverlay>();
        o.ShowLoading(game); o.SetProgress(.6f);
        return "ok";
    }
    public static string LoadingEscape() => LoadingDemo(4);
    public static string LoadingHide() { Object.FindAnyObjectByType<MultiversePartyOverlay>().HideLoading(); return "ok"; }

    public static async System.Threading.Tasks.Task<string> ChampionStats()
    {
        var p = MultiverseParty.Instance ?? MultiverseParty.StartOffline(6);
        for (int i = 0; i < 80 && Object.FindAnyObjectByType<MultiverseHub>() == null; i++) await System.Threading.Tasks.Task.Delay(100);
        await System.Threading.Tasks.Task.Delay(1000);
        int[][] wins = { new[] { 2, 0, 1, 1, 1 }, new[] { 0, 2, 0, 0, 0 }, new[] { 0, 0, 1, 0, 1 }, new[] { 1, 0, 0, 0, 0 }, new[] { 0, 0, 0, 0, 0 }, new[] { 0, 1, 0, 0, 0 } };
        for (int i = 0; i < p.Members.Count; i++) { p.Members[i].Wins = wins[i]; p.Members[i].Score = wins[i].Sum(); }
        p.Members[3].Pushes = 6; p.Members[4].Pushed = 5; p.Members[2].Emotes = 7; p.Members[5].VoteHits = 4; p.Members[1].Chats = 6;
        p.Phase = PartyPhase.Champion;
        await System.Threading.Tasks.Task.Delay(3000);
        return string.Join(" | ", p.Titles());
    }

    public static string GlowTest()
    {
        var cam = Camera.main;
        var a = GameObject.CreatePrimitive(PrimitiveType.Sphere); a.transform.position = cam.transform.position + cam.transform.forward * 4 - cam.transform.right * 1.2f;
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.transform.position = cam.transform.position + cam.transform.forward * 4 + cam.transform.right * 1.2f;
        var ma = new Material(Shader.Find("Universal Render Pipeline/Lit")); ma.color = Color.red; a.GetComponent<Renderer>().sharedMaterial = ma;
        var mb = new Material(Shader.Find("Universal Render Pipeline/Lit")); mb.color = Color.red; mb.EnableKeyword("_EMISSION"); mb.SetColor("_EmissionColor", Color.red * 1.5f); b.GetComponent<Renderer>().sharedMaterial = mb;
        ClaudeBridge.CaptureCamera(cam, System.IO.Path.GetFullPath(Application.dataPath + "/../Docs/Claude_Capturas/Partida/_glow.png"), 800, 450);
        return "ok";
    }
}
#endif
