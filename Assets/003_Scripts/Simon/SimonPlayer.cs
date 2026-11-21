using UnityEngine;

/// <summary>A contestant of Memoria Musical: the party character on a podium, with hearts (lives) over the head.</summary>
public sealed class SimonPlayer : MonoBehaviour
{
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    public bool IsBot { get; set; }
    public int Score; public int Lives = SimonDirector.StartLives;
    public bool Out => Lives <= 0;
    public Transform Hand { get; private set; }

    Animator anim; string animState = ""; float actionUntil; string action = "";
    Transform character; TextMesh nameTag, hearts; Renderer podiumTop; Material podiumMat;

    public void Init(int slot, int member, string name, Color color, bool human, bool puppet, GameObject prefab, int lookSeed, Vector3 at, Vector3 lookAt)
    {
        Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet;
        transform.SetPositionAndRotation(at, Quaternion.LookRotation(new Vector3(lookAt.x - at.x, 0, lookAt.z - at.z)));
        podiumMat = new Material(EscapeKit.Paint(Color.Lerp(color, Color.white, .15f), .4f));
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Podio", new Vector3(0, .35f, 0), new Vector3(1.5f, .35f, 1.5f), EscapeKit.Paint(new Color(.95f, .95f, .98f), .5f), false);
        podiumTop = EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Aro", new Vector3(0, .72f, 0), new Vector3(1.55f, .03f, 1.55f), podiumMat, false).GetComponent<Renderer>();
        var stand = new GameObject("Personaje").transform; stand.SetParent(transform, false); stand.localPosition = Vector3.up * .74f; character = stand;
        if (prefab != null)
        {
            var go = Instantiate(prefab, stand, false); go.name = "Fiestero";
            var b = PirateArenaVisuals.BoundsOf(go); go.transform.localScale *= 1.6f / Mathf.Max(.1f, b.size.y);
            b = PirateArenaVisuals.BoundsOf(go); go.transform.position += new Vector3(0, stand.position.y - b.min.y, 0);
            anim = go.GetComponentInChildren<Animator>(); if (anim != null) anim.applyRootMotion = false;
            var look = go.GetComponent<CharacterLook>();
            if (look != null)
            {
                var saved = Random.state; Random.InitState(lookSeed); look.Init(); look.Randomize();
                int ropa = System.Array.IndexOf(CharacterLook.ColorSlots, "Ropa");
                if (ropa >= 0)
                {
                    var opts = CharacterLook.ColorOptions[ropa]; int best = 0; float bd = 9f;
                    for (int i = 0; i < opts.Length; i++) { float dd = Mathf.Abs(opts[i].r - color.r) + Mathf.Abs(opts[i].g - color.g) + Mathf.Abs(opts[i].b - color.b); if (dd < bd) { bd = dd; best = i; } }
                    look.colorSel[ropa] = best; look.Apply();
                }
                Random.state = saved;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) { string n = t.name.ToLowerInvariant(); if (n.Contains("hand") && n.EndsWith("r")) { Hand = t; break; } }
        }
        else EscapeKit.Shape(PrimitiveType.Capsule, stand, "Cápsula", Vector3.up * .8f, new Vector3(.6f, .8f, .6f), EscapeKit.Paint(color), false);
        if (Hand == null) { Hand = new GameObject("Mano").transform; Hand.SetParent(stand, false); Hand.localPosition = new Vector3(.3f, 1.3f, .35f); }
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        TextMesh Tag(string n, float y, float size)
        {
            var t = new GameObject(n).AddComponent<TextMesh>(); t.transform.SetParent(transform, false); t.transform.localPosition = Vector3.up * y;
            t.font = font; t.GetComponent<MeshRenderer>().sharedMaterial = font.material; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center;
            t.fontSize = 48; t.characterSize = size; t.fontStyle = FontStyle.Bold; return t;
        }
        nameTag = Tag("Nombre", 2.95f, .035f); nameTag.text = human ? name + " (tú)" : name; nameTag.color = color;
        hearts = Tag("Vidas", 2.62f, .04f); hearts.color = new Color(1f, .35f, .4f);
        Play("Idle");
    }

    public void Act(string state, float seconds) { action = state; actionUntil = Time.time + seconds; }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam != null) { nameTag.transform.rotation = cam.transform.rotation; hearts.transform.rotation = cam.transform.rotation; }
        hearts.text = Out ? "FUERA" : new string('♥', Mathf.Max(0, Lives));
        if (Out) { podiumMat.color = Color.Lerp(podiumMat.color, new Color(.35f, .35f, .38f), Time.deltaTime * 3f); }
        string s = Time.time < actionUntil ? action : Out ? "Death" : "Idle";
        Play(s);
    }

    void Play(string s) { if (anim == null || s == animState) return; animState = s; anim.CrossFadeInFixedTime(s, s == "Jump" ? .05f : .12f); }
}
