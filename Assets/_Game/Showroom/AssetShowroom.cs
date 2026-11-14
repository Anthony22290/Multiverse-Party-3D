using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Probador de assets: muestra cada modelo en su pedestal y permite revisar sus animaciones antes de llevarlo al juego.
/// La escena la construye Multiverse → Probador de assets → Reconstruir escena (AssetShowroomBuilder).
/// Controles: ←/→ o A/D cambiar modelo · 1-9 animación · Q/W/E golpe, golpe recibido, muerte (procedurales)
/// · clic derecho arrastrar girar · rueda zoom · Espacio giro automático · T todos a la vez · G caminar en círculo · F1 ocultar panel.
/// </summary>
public class AssetShowroom : MonoBehaviour
{
    [System.Serializable]
    public class Item
    {
        public string name, folder;
        public Animator animator;
        public Transform root;
        public string[] states;
        public int triangles, bones;
        public float height;
        public CharacterLook look;
        [System.NonSerialized] public string proc; [System.NonSerialized] public float procStart;
        [System.NonSerialized] public Quaternion baseRot; [System.NonSerialized] public Vector3 basePos;
    }

    public List<Item> items = new();
    public Camera cam;
    int sel, stateIdx;
    float yaw = 180f, pitch = 12f, dist = 5f, animSpeed = 1f;
    bool spin, all, walkCircle, hideUI;
    float circleT;
    GUIStyle box, title, small;

    void Start()
    {
        foreach (var it in items) { it.baseRot = it.root.localRotation; it.basePos = it.root.localPosition; }
        Select(0);
    }

    Item Cur => items.Count > 0 ? items[Mathf.Clamp(sel, 0, items.Count - 1)] : null;

    void Select(int i)
    {
        if (items.Count == 0) return;
        if (walkCircle) ResetPose(Cur);
        sel = (i + items.Count) % items.Count;
        dist = Mathf.Max(2.5f, Cur.height * 2.2f);
        stateIdx = 0; PlayState(Cur, 0);
    }

    void PlayState(Item it, int idx)
    {
        if (it?.animator == null || it.states == null || it.states.Length == 0) return;
        idx = Mathf.Clamp(idx, 0, it.states.Length - 1);
        it.animator.speed = animSpeed;
        it.animator.CrossFadeInFixedTime(it.states[idx], .15f, 0, 0f);
        it.proc = null; ResetPose(it);
        if (it == Cur) stateIdx = idx;
    }

    void Proc(Item it, string kind) { it.proc = kind; it.procStart = Time.time; }

    void ResetPose(Item it) { if (it == null) return; it.root.localRotation = it.baseRot; it.root.localPosition = it.basePos; }

    void Update()
    {
        var k = Keyboard.current; var m = Mouse.current;
        if (k != null)
        {
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) Select(sel + 1);
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) Select(sel - 1);
            for (int n = 0; n < 9; n++) if (k[Key.Digit1 + n].wasPressedThisFrame) Each(it => PlayState(it, n));
            if (k.qKey.wasPressedThisFrame) Each(it => Proc(it, "Golpe"));
            if (k.wKey.wasPressedThisFrame) Each(it => Proc(it, "Recibe"));
            if (k.eKey.wasPressedThisFrame) Each(it => Proc(it, "Muerte"));
            if (k.spaceKey.wasPressedThisFrame) spin = !spin;
            if (k.tKey.wasPressedThisFrame) all = !all;
            if (k.gKey.wasPressedThisFrame) { walkCircle = !walkCircle; if (!walkCircle) ResetPose(Cur); }
            if (k.f1Key.wasPressedThisFrame) hideUI = !hideUI;
            if (k.rKey.wasPressedThisFrame && Cur?.look != null) Cur.look.Randomize();
        }
        if (m != null)
        {
            if (m.rightButton.isPressed) { var d = m.delta.ReadValue(); yaw += d.x * .25f; pitch = Mathf.Clamp(pitch - d.y * .2f, -10f, 70f); }
            float s = m.scroll.ReadValue().y; if (Mathf.Abs(s) > .01f) dist = Mathf.Clamp(dist * (s > 0 ? .9f : 1.1f), 1f, 30f);
        }
        if (spin) yaw += Time.deltaTime * 25f;

        foreach (var it in items) TickProc(it);
        var c = Cur; if (c == null) return;
        if (walkCircle && c.proc == null)
        {
            circleT += Time.deltaTime * .5f;
            Vector3 off = new Vector3(Mathf.Cos(circleT), 0, Mathf.Sin(circleT)) * 1.6f;
            c.root.localPosition = c.basePos + off;
            c.root.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(circleT), 0, Mathf.Cos(circleT))) * c.baseRot;
        }
        Vector3 focus = c.root.parent.position + Vector3.up * c.height * .55f;
        Vector3 want = focus + Quaternion.Euler(pitch, yaw, 0) * Vector3.back * dist;
        cam.transform.position = Vector3.Lerp(cam.transform.position, want, Time.deltaTime * 8f);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(focus - cam.transform.position), Time.deltaTime * 10f);
    }

    void Each(System.Action<Item> a) { if (all) foreach (var it in items) a(it); else if (Cur != null) a(Cur); }

    // Las mismas inclinaciones que usa SurvivalZombie para los jefes Meshy sin clips de ataque/muerte.
    void TickProc(Item it)
    {
        if (it.proc == null) return;
        float t = Time.time - it.procStart, lean = 0f, push = 0f, drop = 0f; bool done = false;
        switch (it.proc)
        {
            case "Golpe": { float k = Mathf.Clamp01(t / .45f), s = Mathf.Sin(k * Mathf.PI); lean = 20f * s; push = .35f * s; done = k >= 1f; break; }
            case "Recibe": { float k = Mathf.Clamp01(t / .3f); lean = -12f * Mathf.Sin(k * Mathf.PI); done = k >= 1f; break; }
            case "Muerte": { float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .75f)); lean = -84f * k; drop = .1f * k; done = t > 2.2f; break; }
        }
        it.root.localRotation = Quaternion.Euler(lean, 0, 0) * it.baseRot;
        it.root.localPosition = it.basePos + (Vector3.forward * push + Vector3.down * drop) * it.height * .4f;
        if (done) { it.proc = null; ResetPose(it); }
    }

    // Panel de disfraces y colores para personajes modulares (CharacterLook).
    void LookPanel(CharacterLook look)
    {
        GUILayout.BeginArea(new Rect(Screen.width - 344, 14, 330, Screen.height - 28), box);
        GUILayout.Label("DISFRAZ", title);
        for (int s = 0; s < CharacterLook.Slots.Length; s++)
        {
            if (look.Options(s).Count == 0) continue;   // p. ej. "Vehículo" en el personaje de a pie
            GUILayout.BeginHorizontal();
            GUILayout.Label(CharacterLook.SlotNames[s], small, GUILayout.Width(80));
            if (GUILayout.Button("◀", GUILayout.Width(30))) look.Step(s, -1);
            GUILayout.Label(look.Current(s), GUI.skin.box, GUILayout.Width(150));
            if (GUILayout.Button("▶", GUILayout.Width(30))) look.Step(s, 1);
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(8);
        GUILayout.Label("COLORES", small);
        for (int c = 0; c < CharacterLook.ColorSlots.Length; c++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(CharacterLook.ColorSlots[c], small, GUILayout.Width(80));
            if (GUILayout.Button("◀", GUILayout.Width(30))) look.StepColor(c, -1);
            var r = GUILayoutUtility.GetRect(150, 22, GUILayout.Width(150));
            var prev = GUI.color; GUI.color = look.CurrentColor(c); GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Texture2D.whiteTexture); GUI.color = prev;
            if (GUILayout.Button("▶", GUILayout.Width(30))) look.StepColor(c, 1);
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(8);
        if (GUILayout.Button("ALEATORIO (R)", GUILayout.Height(32))) look.Randomize();
        GUILayout.EndArea();
    }

    void OnGUI()
    {
        if (hideUI || items.Count == 0) return;
        box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10) };
        title ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        small ??= new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        var c = Cur;
        GUILayout.BeginArea(new Rect(14, 14, 330, Screen.height - 28), box);
        GUILayout.Label("PROBADOR DE ASSETS", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("◀", GUILayout.Width(36))) Select(sel - 1);
        GUILayout.Label($"{c.name}  ({sel + 1}/{items.Count})", title);
        if (GUILayout.Button("▶", GUILayout.Width(36))) Select(sel + 1);
        GUILayout.EndHorizontal();
        GUILayout.Label($"Carpeta: {c.folder}\nTriángulos: {c.triangles:N0} · Huesos: {c.bones} · Alto real: {c.height:0.00} m", small);
        GUILayout.Space(6);
        GUILayout.Label("Animaciones (1-9)", small);
        if (c.states != null) for (int i = 0; i < c.states.Length; i++)
            if (GUILayout.Toggle(i == stateIdx, $" {i + 1}. {c.states[i]}", GUI.skin.button)) { if (i != stateIdx) Each(it => PlayState(it, i)); }
        GUILayout.Space(4);
        GUILayout.Label("Procedurales (sin clip)", small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Q Golpe")) Each(it => Proc(it, "Golpe"));
        if (GUILayout.Button("W Recibe")) Each(it => Proc(it, "Recibe"));
        if (GUILayout.Button("E Muerte")) Each(it => Proc(it, "Muerte"));
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.Label($"Velocidad de animación: {animSpeed:0.00}x", small);
        float ns = GUILayout.HorizontalSlider(animSpeed, .1f, 2f);
        if (!Mathf.Approximately(ns, animSpeed)) { animSpeed = ns; foreach (var it in items) if (it.animator) it.animator.speed = animSpeed; }
        GUILayout.Space(10);
        spin = GUILayout.Toggle(spin, " Giro automático (Espacio)");
        all = GUILayout.Toggle(all, " Aplicar a todos (T)");
        bool wc = GUILayout.Toggle(walkCircle, " Caminar en círculo (G)"); if (wc != walkCircle) { walkCircle = wc; if (!wc) ResetPose(c); }
        GUILayout.FlexibleSpace();
        GUILayout.Label("←/→ cambiar · clic der. girar · rueda zoom · F1 ocultar", small);
        GUILayout.EndArea();
        if (c.look != null) LookPanel(c.look);
    }
}
