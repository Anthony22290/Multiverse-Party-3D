using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One digger of Pico, Pala y Tijera: owns a column of blocks and stands on top of it. Choose the tool (1 pala,
/// 2 pico, 3 tijera) and click to hit the block under your feet. The right tool breaks it (hard stone takes two hits)
/// and you drop to the next one; the wrong tool bounces off and stuns you for a moment.
/// The computer that owns the slot decides; the others replay its depth (the column is the same for everybody).
/// </summary>
public sealed class MinaMiner : MonoBehaviour
{
    public const float Cooldown = .13f, StunSeconds = .4f, FallSpeed = 16f;

    public MinaDirector Director { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    public int Depth { get; private set; }
    public MinaKit.Tool Tool { get; private set; } = MinaKit.Tool.Pala;
    public int Hits { get; private set; }            // hits on the current (hard) block
    public int Mistakes { get; private set; }
    public int Clicks { get; private set; }
    public float FinishedAt { get; private set; } = -1f;
    public bool Finished => FinishedAt >= 0f;
    public bool Stunned => Time.time < stunUntil;
    public bool Falling => Mathf.Abs(visualY - TargetY) > .02f;
    public bool Celebrating { get; set; }
    public float X { get; private set; }
    public float TargetY => -Depth;
    public MinaKit.Block Next => Depth < Director.Seq.Length ? Director.Seq[Depth] : MinaKit.Block.Oro;

    // Input from the bot.
    public bool InMine; public int InTool = -1;

    GameObject[] blocks; float stunUntil, cooldownUntil, visualY; Transform visual, hand, toolRoot, stars, tagRoot, hint; TextMesh hintText;
    readonly GameObject[] tools = new GameObject[3];
    Animator anim; string animState = ""; float swingUntil;

    public void Init(MinaDirector d, int slot, int member, string name, Color color, bool human, bool puppet, float x, GameObject prefab, int lookSeed)
    {
        Director = d; Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet; X = x;
        transform.position = new Vector3(x, 0, 0); transform.rotation = Quaternion.LookRotation(Vector3.back);
        visualY = 0f;
        BuildColumn();

        visual = new GameObject("Cuerpo").transform; visual.SetParent(transform, false);
        if (prefab != null)
        {
            var go = Instantiate(prefab, visual, false); go.name = "Fiestero";
            var b = PirateArenaVisuals.BoundsOf(go); go.transform.localScale *= 1.25f / Mathf.Max(.1f, b.size.y);
            b = PirateArenaVisuals.BoundsOf(go); go.transform.position += new Vector3(0, transform.position.y - b.min.y, 0);
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
            // Nothing else in the hands: the tool goes there.
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Mano_")) r.gameObject.SetActive(false);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("hand") && (n.EndsWith(".r") || n.EndsWith("_r") || n.EndsWith("r"))) { hand = t; break; }
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        else EscapeKit.Shape(PrimitiveType.Capsule, visual, "Cápsula", Vector3.up * .6f, new Vector3(.5f, .6f, .5f), EscapeKit.Paint(color), false);

        toolRoot = new GameObject("Herramienta").transform;
        if (hand != null)
        {
            toolRoot.SetParent(hand, false); toolRoot.localPosition = Vector3.zero;
            toolRoot.rotation = transform.rotation * Quaternion.Euler(-20f, 0f, 0f);
            toolRoot.position = hand.position; toolRoot.localScale = Vector3.one / Mathf.Max(.01f, hand.lossyScale.x);
            toolRoot.position -= toolRoot.up * .18f;
        }
        else { toolRoot.SetParent(visual, false); toolRoot.localPosition = new Vector3(-.35f, .45f, 0); }
        for (int i = 0; i < 3; i++) { tools[i] = MinaKit.BuildTool((MinaKit.Tool)i, toolRoot); tools[i].SetActive(i == 0); }

        // Dizzy stars.
        stars = new GameObject("Estrellas").transform; stars.SetParent(transform, false); stars.localPosition = Vector3.up * 1.45f;
        var starMat = EscapeKit.Glow(new Color(1f, .9f, .3f), 2f);
        for (int i = 0; i < 4; i++) EscapeKit.Shape(PrimitiveType.Sphere, stars, "Estrella", Quaternion.Euler(0, i * 90, 0) * Vector3.forward * .32f, Vector3.one * .1f, starMat, false);
        stars.gameObject.SetActive(false);

        // Name tag.
        tagRoot = new GameObject("Etiqueta").transform; tagRoot.SetParent(transform, false); tagRoot.localPosition = Vector3.up * 1.75f;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var nt = new GameObject("Nombre").AddComponent<TextMesh>(); nt.transform.SetParent(tagRoot, false);
        nt.font = font; nt.GetComponent<MeshRenderer>().sharedMaterial = font.material; nt.text = human ? name + " (tú)" : name;
        nt.anchor = TextAnchor.MiddleCenter; nt.alignment = TextAlignment.Center; nt.fontSize = 48; nt.characterSize = .035f; nt.color = color; nt.fontStyle = FontStyle.Bold;

        if (human)
        {
            hint = new GameObject("Pista").transform;
            hintText = hint.gameObject.AddComponent<TextMesh>(); hintText.font = font; hint.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            hintText.anchor = TextAnchor.MiddleLeft; hintText.fontSize = 48; hintText.characterSize = .04f; hintText.fontStyle = FontStyle.Bold;
        }
        Play("Idle");
    }

    void BuildColumn()
    {
        var seq = Director.Seq;
        blocks = new GameObject[seq.Length];
        var col = new GameObject("Columna de " + DisplayName).transform;
        for (int k = 0; k < seq.Length; k++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(go.GetComponent<Collider>());
            go.name = seq[k] + " " + k; go.transform.SetParent(col, false);
            go.transform.position = new Vector3(X, -k - .5f, 0); go.transform.localScale = Vector3.one * .98f;
            go.GetComponent<Renderer>().sharedMaterial = MinaKit.BlockMat(seq[k]);
            blocks[k] = go;
        }
        // Treasure at the bottom.
        var chest = EscapeKit.Block(col, "Cofre del tesoro", new Vector3(X, -seq.Length - .35f, 0), new Vector3(.8f, .7f, .6f), EscapeKit.Paint(new Color(.55f, .34f, .18f)), .06f, false);
        EscapeKit.Block(chest.transform, "Tapa", new Vector3(0, .4f, 0), new Vector3(.86f, .16f, .66f), EscapeKit.Glow(new Color(1f, .8f, .25f), 1.4f), .04f, false);
    }

    void OnDestroy() { if (hint != null) Destroy(hint.gameObject); }

    // ------------------------------------------------------------------ owner

    void Update()
    {
        if (Director == null) return;
        if (!Puppet)
        {
            if (IsHuman && GetComponent<MinaBot>() == null) ReadInput();
            if (InTool >= 0) { SetTool((MinaKit.Tool)InTool); InTool = -1; }
            if (InMine) { InMine = false; TryMine(); }
        }
        Present();
    }

    void ReadInput()
    {
        if (!Director.InputFree) return;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        if (k != null)
        {
            if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) InTool = 0;
            if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) InTool = 1;
            if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) InTool = 2;
            if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) InMine = true;
        }
        if (m != null)
        {
            if (m.leftButton.wasPressedThisFrame) InMine = true;
            float wheel = m.scroll.ReadValue().y;
            if (wheel > .01f) InTool = ((int)Tool + 2) % 3; else if (wheel < -.01f) InTool = ((int)Tool + 1) % 3;
        }
        if (g != null)
        {
            if (g.buttonWest.wasPressedThisFrame) InTool = 0;
            if (g.buttonNorth.wasPressedThisFrame) InTool = 1;
            if (g.buttonEast.wasPressedThisFrame) InTool = 2;
            if (g.buttonSouth.wasPressedThisFrame || g.rightTrigger.wasPressedThisFrame) InMine = true;
        }
    }

    public void SetTool(MinaKit.Tool t)
    {
        if (t == Tool) return;
        Tool = t;
        for (int i = 0; i < 3; i++) tools[i].SetActive(i == (int)t);
        if (IsHuman) MinaAudio.UI("select", .5f);
    }

    public void TryMine()
    {
        if (Director.State != MinaDirector.Phase.Running || Finished || Stunned || Time.time < cooldownUntil || Falling) return;
        Clicks++;
        cooldownUntil = Time.time + Cooldown;
        Swing();
        var block = Next;
        if (!MinaKit.Works(block, Tool))
        {
            Mistakes++;
            stunUntil = Time.time + StunSeconds;
            MinaAudio.Play("wrong", BlockPos(Depth), IsHuman ? 1f : .45f);
            MultiverseFX.FloatText(BlockPos(Depth) + new Vector3(0, 1.6f, -.6f), "¡" + MinaKit.ToolNames[(int)MinaKit.Needed(block)] + "!", new Color(1f, .4f, .35f), .8f, .9f);
            if (IsHuman) Director.View?.Shake(.25f);
            Play("Hit");
            return;
        }
        Hits++;
        if (Hits < MinaKit.HitsFor(block))
        {
            blocks[Depth].GetComponent<Renderer>().sharedMaterial = MinaKit.BlockMat(block, true);
            MinaAudio.Play("crack", BlockPos(Depth), IsHuman ? .9f : .4f);
            Debris(Depth, 5);
            return;
        }
        Break(Depth);
        Hits = 0; Depth++;
        if (Depth >= Director.Seq.Length) { FinishedAt = Director.Elapsed; Director.OnFinished(this); }
    }

    Vector3 BlockPos(int k) => new Vector3(X, -k - .5f, 0);

    void Break(int k)
    {
        if (k < 0 || k >= blocks.Length || blocks[k] == null) return;
        var b = Director.Seq[k];
        string sound = b == MinaKit.Block.Tierra ? "dirt" : b == MinaKit.Block.Hojas ? "leaves" : b == MinaKit.Block.Oro ? "gold" : "stone";
        MinaAudio.Play(sound, BlockPos(k), IsHuman ? 1f : .35f);
        Debris(k, b == MinaKit.Block.Oro ? 14 : 9);
        if (b == MinaKit.Block.Oro) MultiverseFX.Burst(BlockPos(k), new Color(1f, .85f, .3f), 14, 5);
        Destroy(blocks[k]); blocks[k] = null;
    }

    void Debris(int k, int count)
    {
        var mat = MinaKit.BlockMat(Director.Seq[k]);
        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.position = BlockPos(k) + Random.insideUnitSphere * .3f; go.transform.localScale = Vector3.one * Random.Range(.12f, .22f);
            go.AddComponent<MinaDebris>().velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(1.5f, 4f), Random.Range(-2.5f, -.5f));
        }
    }

    void Swing() { swingUntil = Time.time + .22f; animState = ""; Play("Attack"); }

    // ------------------------------------------------------------------ network puppets

    public void ApplyNet(int depth, int tool, bool stunned, int hits, bool finished, float finishedAt)
    {
        SetTool((MinaKit.Tool)Mathf.Clamp(tool, 0, 2));
        if (stunned && !Stunned) { stunUntil = Time.time + StunSeconds; Play("Hit"); MinaAudio.Play("wrong", BlockPos(Depth), .35f); }
        depth = Mathf.Clamp(depth, 0, Director.Seq.Length);
        while (Depth < depth) { Swing(); Break(Depth); Depth++; }
        if (hits > 0 && hits != Hits && Depth < blocks.Length && blocks[Depth] != null) { blocks[Depth].GetComponent<Renderer>().sharedMaterial = MinaKit.BlockMat(Next, true); Swing(); }
        Hits = hits;
        if (finished && !Finished) { FinishedAt = finishedAt; Director.OnFinished(this); }
    }

    // ------------------------------------------------------------------ presentation

    void Present()
    {
        visualY = Mathf.MoveTowards(visualY, TargetY, FallSpeed * Time.deltaTime);
        transform.position = new Vector3(X, visualY, 0);
        if (stars != null) { stars.gameObject.SetActive(Stunned); if (Stunned) stars.Rotate(0, 540f * Time.deltaTime, 0); }
        if (tagRoot != null && Director.View != null) tagRoot.rotation = Director.View.transform.rotation;

        if (hint != null)
        {
            bool show = Director.State == MinaDirector.Phase.Running && Director.Elapsed < 25f && !Finished;
            hint.gameObject.SetActive(show);
            if (show)
            {
                var need = MinaKit.Needed(Next);
                hintText.text = Next == MinaKit.Block.Oro ? "◀ ORO: cualquiera" : "◀ " + ((int)need + 1) + " " + MinaKit.ToolNames[(int)need];
                hintText.color = MinaKit.ToolColors[(int)need];
                hint.position = new Vector3(X + .62f, -Depth - .5f, -.55f);
                hint.rotation = Director.View != null ? Director.View.transform.rotation : Quaternion.identity;
            }
        }

        string state = Celebrating ? "Victory" : Stunned ? "Hit" : Falling ? "Jump" : Time.time < swingUntil ? "Attack" : "Idle";
        Play(state);
    }

    void Play(string state)
    {
        if (anim == null || state == animState) return;
        animState = state;
        anim.CrossFadeInFixedTime(state, state == "Attack" ? .03f : .1f);
    }
}

/// <summary>Little cube that flies off a broken block and vanishes.</summary>
public sealed class MinaDebris : MonoBehaviour
{
    public Vector3 velocity; float age; Vector3 spin, size;
    void Start() { spin = Random.insideUnitSphere * 720f; size = transform.localScale; }
    void Update()
    {
        age += Time.deltaTime; velocity.y -= 18f * Time.deltaTime;
        transform.position += velocity * Time.deltaTime; transform.Rotate(spin * Time.deltaTime);
        transform.localScale = size * Mathf.Clamp01(1f - (age - .35f) / .3f);
        if (age > .65f) Destroy(gameObject);
    }
}
