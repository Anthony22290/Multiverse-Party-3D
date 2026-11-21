using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// The ten rooms of the Torre del Multiverso. Coordinates are local to the room: x ∈ [-8, 8], z ∈ [-9, 9], +Z = exit.

// ============================================================ 4 · tile bridge
public sealed class RoomTiles : EscapeRoom
{
    const int Cols = 4, Rows = 6;
    const float Pitch = 1.9f, Size = 1.72f, Row0 = -5.2f;
    sealed class Tile { public GameObject go; public Collider col; public Material mat; public bool safe; public int state, r, c; public float t; public Vector3 home; }
    readonly Tile[,] tiles = new Tile[Rows, Cols];
    readonly Dictionary<Collider, Tile> byCollider = new();
    readonly List<Tile> path = new();
    readonly Dictionary<EscapeActor, int> progress = new();
    static readonly Color Stone = new Color(.62f, .58f, .72f), Safe = new Color(.4f, 1f, .75f);

    public override string Title => "Puente de Baldosas";
    int crossed;
    public override string Objective => Solved ? "El puente es estable." : $"Cruza el abismo hasta el otro lado. No todas las baldosas aguantan.";
    public override string[] Clues => new[]
    {
        "De vez en cuando el puente brilla un instante. No es decoración.",
        "Las baldosas seguras forman un camino continuo desde el borde dorado: memoriza columna a columna cuando brille y no te salgas."
    };
    public override Color Floor => new Color(.55f, .5f, .7f);
    public override Color Wall => new Color(.72f, .66f, .86f);
    public override Color Accent => new Color(.5f, .35f, .8f);
    public override Color Glow => new Color(.85f, .75f, 1f);
    public override Vector2[] FloorSegments => new[] { new Vector2(-11f, -6.15f), new Vector2(5.2f, 11f) };

    protected override Vector3 WaitSpot(int slot) => new Vector3(6.6f - (slot % 2) * 1.2f, 0f, 7.3f + (slot / 2) * .9f);
    static float X(int c) => (c - 1.5f) * Pitch;
    static float Z(int r) => Row0 + r * Pitch;

    public override void Build()
    {
        var safe = new bool[Rows, Cols];
        var order = new List<(int r, int c)>();
        int col = Random.Range(0, Cols);
        for (int r = 0; r < Rows; r++)
        {
            if (!safe[r, col]) { safe[r, col] = true; order.Add((r, col)); }
            if (r < Rows - 1 && Random.value < .7f)
            {
                int target = Mathf.Clamp(col + (Random.value < .5f ? -1 : 1) * Random.Range(1, 3), 0, Cols - 1);
                while (col != target) { col += target > col ? 1 : -1; safe[r, col] = true; order.Add((r, col)); }
            }
        }
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var t = new Tile { r = r, c = c, safe = safe[r, c], home = new Vector3(X(c), -.16f, Z(r)) };
                t.mat = new Material(EscapeKit.Lit) { name = "Baldosa" };
                t.mat.EnableKeyword("_EMISSION");
                t.mat.SetFloat("_Smoothness", .35f);
                t.mat.SetColor("_BaseColor", Stone);
                t.mat.SetColor("_EmissionColor", Color.black);
                t.go = Block("Baldosa", t.home, new Vector3(Size, .32f, Size), t.mat, .08f);
                t.col = t.go.GetComponent<Collider>();
                byCollider[t.col] = t;
                tiles[r, c] = t;
            }
        foreach (var (r, c) in order) path.Add(tiles[r, c]);

        Material gold = M("Dorado", new Color(1f, .78f, .3f));
        Block("Borde de salida", new Vector3(0, .02f, -6.2f), new Vector3(Width, .06f, .12f), gold, .02f, false);
        Block("Borde de llegada", new Vector3(0, .02f, 5.25f), new Vector3(Width, .06f, .12f), gold, .02f, false);
        EscapeKit.Text(transform, "MEMORIZA EL CAMINO", new Vector3(0, .03f, -7.7f), new Vector3(90, 0, 0), .04f, new Color(.95f, .9f, 1f));
        EscapeKit.PointLight(transform, new Vector3(0, -4f, 0), new Color(.6f, .35f, 1f), 16f, 2.5f);
        var crystal = EscapeKit.Glow(new Color(.6f, 1f, .85f), 2f);
        for (int s = -1; s <= 1; s += 2)
            EscapeKit.Block(transform, "Cristal de llegada", new Vector3(s * 6.6f, .7f, 7.6f), new Vector3(.5f, 1.4f, .5f), crystal, .15f, true, new Vector3(0, 45, 0));
    }

    public override void Tick()
    {
        bool mine = D.ActiveRoom == this && D.Running;
        bool reveal = mine && (ActiveTime < 3f || ActiveTime % 20f < 1.6f);
        float pulse = 2f + Mathf.Sin(Time.time * 8f) * .5f;
        foreach (var t in tiles)
        {
            switch (t.state)
            {
                case 1:
                    t.t += Time.deltaTime;
                    t.go.transform.localPosition = t.home + new Vector3(Random.Range(-.04f, .04f), 0, Random.Range(-.04f, .04f));
                    if (t.t > .35f) { t.state = 2; t.t = 0f; t.col.enabled = false; EscapeAudio.Play("crack", t.go.transform.position, .8f); }
                    break;
                case 2:
                    t.t += Time.deltaTime;
                    t.go.transform.localPosition = t.home + Vector3.down * (t.t * t.t * 9f);
                    if (t.t > 1.1f) { t.state = 3; t.t = 0f; t.go.GetComponent<Renderer>().enabled = false; }
                    break;
                case 3:
                    t.t += Time.deltaTime;
                    if (t.t > 2.2f) Restore(t);
                    break;
            }
            bool glow = Solved || (reveal && t.safe);
            Color baseColor = glow ? Color.Lerp(Stone, Safe, .6f) : Stone;
            t.mat.SetColor("_BaseColor", baseColor);
            t.mat.SetColor("_EmissionColor", glow ? Safe * (Solved ? .6f : pulse) : Color.black);
        }
        if (!mine || Solved) return;

        crossed = Runners.Count(x => L(x.transform.position).z > 5.6f && x.transform.position.y > -1f);
        if (crossed >= TeamSize) { Solve(lastTouch != null ? lastTouch : Team[0]); return; }
        foreach (var a in Team)
        {
            if (!a.Grounded) continue;
            if (Physics.Raycast(a.transform.position + Vector3.up * .3f, Vector3.down, out var hit, .7f, ~0, QueryTriggerInteraction.Ignore)
                && byCollider.TryGetValue(hit.collider, out var t) && !t.safe && t.state == 0)
            {
                t.state = 1; t.t = 0f;
                EscapeAudio.Play("crack", t.go.transform.position, .5f);
            }
            if (L(a.transform.position).z > 5.6f) Touch(a);
        }
    }

    void Restore(Tile t)
    {
        t.state = 0; t.t = 0f;
        t.go.transform.localPosition = t.home;
        t.go.GetComponent<Renderer>().enabled = true;
        t.col.enabled = true;
    }

    protected override void OnSolved()
    {
        foreach (var t in tiles) { t.safe = true; if (t.state != 0) Restore(t); }
        Confetti(W(new Vector3(0, 1.5f, 6.5f)), Safe, 60);
    }

    public override EscapeOrder Order(EscapeActor a)
    {
        Vector3 lp = L(a.transform.position);
        if (lp.z > 5.3f) return Wait(a);
        if (!progress.TryGetValue(a, out int i) || lp.z < -6.25f) i = -1;
        if (i == -1)
        {
            float startAt = 1.5f + a.Role * 1.4f;
            var approach = W(new Vector3(X(path[0].c), 0, -6.75f));
            if (!Solved && (ActiveTime < startAt || !Assist(90f))) return EscapeOrder.Go(W(new Vector3((a.Role - 2.5f) * 1.1f, 0, -8.2f)), .5f);
            Vector3 d = a.transform.position - approach; d.y = 0f;
            if (d.magnitude > .3f) { progress[a] = -1; return EscapeOrder.Exact(approach); }
            i = 0;
        }
        if (i < path.Count)
        {
            Vector3 c = W(new Vector3(X(path[i].c), 0, Z(path[i].r)));
            Vector3 d = a.transform.position - c; d.y = 0f;
            if (d.magnitude < .3f) i++;
        }
        progress[a] = i;
        if (i >= path.Count) return EscapeOrder.Exact(W(new Vector3(X(path[path.Count - 1].c), 0, 6.4f)));
        return EscapeOrder.Exact(W(new Vector3(X(path[i].c), 0, Z(path[i].r))));
    }
}

// ============================================================ 5 · library code
public sealed class RoomLibrary : EscapeRoom
{
    readonly int[] digits = new int[4];
    readonly List<int> order = new() { 0, 1, 2, 3 };
    readonly Vector3[] fronts = new Vector3[4];
    readonly Dictionary<EscapeActor, int> readStep = new();
    float dwell, lockedUntil;
    EscapeInteractable keypad;
    readonly List<(Renderer r, Material off)> litBooks = new();
    Material screen;

    public override string Title => "Biblioteca del Código";
    public override string Objective => "Un código de cuatro cifras abre la puerta. La biblioteca lo sabe, pero no lo dice.";
    public override string[] Clues => new[]
    {
        "Cuatro estanterías, cuatro cifras. Fíjate en qué libros destacan y en qué no.",
        "Cuenta los libros que brillan en cada estantería; el panel sobre el teclado dicta en qué orden de colores se escriben."
    };
    public override Color Floor => new Color(.5f, .34f, .26f);
    public override Color Wall => new Color(.78f, .66f, .52f);
    public override Color Accent => new Color(.4f, .22f, .14f);
    public override Color Glow => new Color(1f, .85f, .6f);
    public string Code => string.Concat(order.Select(h => digits[h].ToString()));

    public override void Build()
    {
        Shuffle(order);
        var spots = new List<Vector3> { new(-9.45f, 0, -4.5f), new(-9.45f, 0, 3.5f), new(9.45f, 0, -4.5f), new(9.45f, 0, 3.5f) };
        Shuffle(spots);
        Material wood = M("Madera oscura", new Color(.36f, .22f, .13f)), gold = M("Dorado", new Color(1f, .78f, .3f));
        for (int h = 0; h < 4; h++)
        {
            digits[h] = Random.Range(1, 10);
            Vector3 p = spots[h];
            float side = Mathf.Sign(p.x);
            fronts[h] = W(new Vector3(side * 7.5f, 0, p.z));
            var root = new GameObject("Estantería " + HueNames[h]).transform;
            root.SetParent(transform, false);
            root.localPosition = p;
            root.localRotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
            EscapeKit.Block(root, "Mueble", new Vector3(0, 1.15f, -.05f), new Vector3(2.7f, 2.3f, .5f), wood, .06f);
            EscapeKit.Block(root, "Remate de color", new Vector3(0, 2.36f, 0), new Vector3(2.86f, .16f, .62f), P(Hues[h]), .05f, false);
            var lit = new HashSet<int>();
            while (lit.Count < digits[h]) lit.Add(Random.Range(0, 12));
            Material on = EscapeKit.Glow(Hues[h], 2.4f), off = P(Color.Lerp(Hues[h], new Color(.35f, .3f, .28f), .65f));
            for (int row = 0; row < 2; row++)
            {
                float y = .42f + row * 1f;
                EscapeKit.Block(root, "Balda", new Vector3(0, y - .04f, .22f), new Vector3(2.5f, .06f, .3f), gold, .01f, false);
                for (int b = 0; b < 6; b++)
                {
                    float hgt = .5f + ((b * 7 + row * 3 + h) % 4) * .06f;
                    var book = EscapeKit.Block(root, "Libro", new Vector3(-1.05f + b * .42f, y + hgt * .5f, .24f), new Vector3(.3f, hgt, .32f), lit.Contains(row * 6 + b) ? on : off, .03f, false);
                    if (lit.Contains(row * 6 + b)) litBooks.Add((book.GetComponent<Renderer>(), off));
                }
            }
        }

        // Keypad pedestal and the colour-order panel on the exit wall above it.
        var pad = new Vector3(-3.2f, 0, Length * .5f - 1.7f);
        Block("Pedestal del teclado", pad + new Vector3(0, .55f, 0), new Vector3(.9f, 1.1f, .6f), M("Hierro", new Color(.3f, .32f, .36f)), .08f);
        screen = EscapeKit.Glow(new Color(.4f, 1f, .8f), .8f);
        Block("Pantalla", pad + new Vector3(0, 1.2f, -.12f), new Vector3(.78f, .46f, .08f), screen, .03f, false, new Vector3(-30, 0, 0));
        var keypadPoint = new GameObject("Teclado");
        keypadPoint.transform.SetParent(transform, false);
        keypadPoint.transform.localPosition = pad + new Vector3(0, 1f, -.4f);
        keypad = EscapeInteractable.Add(keypadPoint, "Escribir el código", 0f, UseKeypad, a => Active && Time.time >= lockedUntil, 1.9f); keypad.NetLocal = true;

        float wz = Length * .5f - .3f;
        Block("Panel del orden", new Vector3(-3.2f, 2.1f, wz), new Vector3(4.2f, 1f, .12f), M("Azul noche", new Color(.1f, .12f, .25f)), .05f, false);
        for (int j = 0; j < 4; j++)
        {
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Color del orden", new Vector3(-4.6f + j * .93f, 2.15f, wz - .09f), new Vector3(.55f, .03f, .55f), EscapeKit.Glow(Hues[order[j]], 1.8f), false, new Vector3(90, 0, 0));
            EscapeKit.Text(transform, (j + 1).ToString(), new Vector3(-4.6f + j * .93f, 1.72f, wz - .09f), Vector3.zero, .02f, new Color(1f, .92f, .7f));
        }
        Prop("Maceta", new Vector3(8.6f, 0, -9.6f), 0);
        Prop("Banco", new Vector3(0, 0, -9.6f), 0);
    }

    protected override Vector3 WaitSpot(int slot) => new Vector3(-1.2f + (slot % 3) * 1.2f, 0f, -5.8f + (slot / 3) * 1.2f);

    // Co-op: whoever owns the keypad sees every book dark; the others count them aloud.
    protected override EscapeInteractable BlindStation => keypad;
    protected override void OnBlind() { foreach (var (r, off) in litBooks) if (r != null) r.sharedMaterial = off; }
    public override string[] ReadAloud => Enumerable.Range(0, 4).Select(h => $"En la estantería {HueNames[h]} brillan {digits[h]} libros.").ToArray();

    public override bool KeypadSubmit(string code, EscapeActor a) => Submit(code, a);
    void UseKeypad(EscapeActor a)
    {
        if (a.DrivenByBot)
        {
            D.NetSubmit(this, readStep.Values.Any(s => s >= 4) ? Code : Random.Range(1000, 10000).ToString(), a);   // co-op: the reader may not be the typist
            return;
        }
        D.HUD.OpenKeypad("CÓDIGO DE LA BIBLIOTECA", code => D.NetSubmit(this, code, a));
    }

    bool Submit(string code, EscapeActor a)
    {
        EscapeAudio.Play("beep", keypad.Point);
        if (code == Code)
        {
            EscapeKit.SetGlow(screen, new Color(.4f, 1f, .5f), 3f);
            Solve(a);
            return true;
        }
        lockedUntil = Time.time + 3f;
        EscapeKit.SetGlow(screen, new Color(1f, .3f, .3f), 2.5f);
        EscapeAudio.Play("error", keypad.Point);
        D.Notify("Código incorrecto · teclado bloqueado 3 s");
        return false;
    }

    public override void Tick()
    {
        if (!Solved && Time.time > lockedUntil) EscapeKit.SetGlow(screen, new Color(.4f, 1f, .8f), .8f + Mathf.Sin(Time.time * 3f) * .2f);
    }

    public override EscapeOrder Order(EscapeActor a)
    {
        if (a.Role != 1 || !Assist(120f)) return Wait(a);
        if (!readStep.TryGetValue(a, out int s)) s = 0;
        if (s < 4)
        {
            Vector3 front = fronts[order[s]];
            Vector3 d = a.transform.position - front; d.y = 0f;
            if (d.magnitude < .65f)
            {
                dwell += Time.deltaTime;
                if (dwell > 1.1f) { dwell = 0f; readStep[a] = s + 1; }
            }
            else dwell = 0f;
            return EscapeOrder.Exact(front);
        }
        return EscapeOrder.Use(keypad);
    }
}

// ============================================================ 8 · braziers
public sealed class RoomBraziers : EscapeRoom
{
    sealed class Brazier { public Vector3 local; public float litUntil = -1f; public Transform flame; public Light light; public Material mat; public EscapeInteractable use; }
    const float Burn = 18f;
    readonly List<Brazier> braziers = new();
    readonly Dictionary<EscapeActor, Brazier> claims = new();
    float hold;

    public override string Title => "Cripta de las Antorchas";
    public override string Objective => $"El sello de la cripta solo se rompe con fuego ({braziers.Count(Lit)}/5).";
    public override string[] Clues => new[]
    {
        "Las antorchas no arden para siempre. El sello quiere verlas todas encendidas a la vez.",
        $"Cada antorcha dura {Burn:0} s: planea un recorrido corto y enciéndelas seguidas, sin pararte."
    };
    public override Color Floor => new Color(.4f, .38f, .42f);
    public override Color Wall => new Color(.56f, .52f, .56f);
    public override Color Accent => new Color(.8f, .4f, .15f);
    public override Color Glow => new Color(1f, .62f, .38f);

    bool Lit(Brazier b) => Time.time < b.litUntil;
    float Left(Brazier b) => Mathf.Max(0f, b.litUntil - Time.time);

    public override void Build()
    {
        Material stone = M("Hormigón", new Color(.6f, .58f, .56f)), iron = M("Hierro", new Color(.3f, .32f, .36f)), gold = M("Dorado", new Color(1f, .78f, .3f));
        for (int k = 0; k < 5; k++)
        {
            float ang = (90f + k * 72f) * Mathf.Deg2Rad;
            var b = new Brazier { local = new Vector3(Mathf.Cos(ang) * 6.8f, 0, Mathf.Sin(ang) * 6.8f + .2f) };
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Columna del brasero", b.local + new Vector3(0, .55f, 0), new Vector3(.7f, .55f, .7f), stone, true);
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Cuenco", b.local + new Vector3(0, 1.18f, 0), new Vector3(1.15f, .1f, 1.15f), iron, false);
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Aro dorado", b.local + new Vector3(0, 1.27f, 0), new Vector3(1.22f, .025f, 1.22f), gold, false);
            b.mat = EscapeKit.Glow(new Color(1f, .55f, .15f), 3f);
            b.flame = EscapeKit.Shape(PrimitiveType.Sphere, transform, "Llama", b.local + new Vector3(0, 1.55f, 0), Vector3.zero, b.mat, false).transform;
            b.light = EscapeKit.PointLight(transform, b.local + new Vector3(0, 2f, 0), new Color(1f, .6f, .25f), 7f, 0f);
            var point = new GameObject("Brasero");
            point.transform.SetParent(transform, false);
            point.transform.localPosition = b.local + Vector3.up * 1.2f;
            var bb = b;
            b.use = EscapeInteractable.Add(point, "Encender la antorcha", .6f, a => Ignite(bb, a), a => Active && Left(bb) < 6f, 2.1f);
            b.use.aimRadius = .7f; b.use.markOffset = new Vector3(0, 1f, 0);
            braziers.Add(b);
        }
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Sello de la cripta", new Vector3(0, .02f, .2f), new Vector3(3.2f, .02f, 3.2f), EscapeKit.Glow(Accent, .4f), false);
        Prop("Antorcha tiki", new Vector3(-8.9f, 0, -9.6f), 0);
        Prop("Antorcha tiki", new Vector3(8.9f, 0, -9.6f), 0);
    }

    void Ignite(Brazier b, EscapeActor a)
    {
        Touch(a);
        b.litUntil = Time.time + Burn;
        EscapeAudio.Play("fire", b.flame.position);
    }

    public override void Tick()
    {
        foreach (var b in braziers)
        {
            float f = Mathf.Clamp01(Left(b) / Burn);
            bool lit = Lit(b);
            float flicker = 1f + Mathf.Sin(Time.time * 17f + b.local.x) * .07f;
            float s = lit ? (.3f + .45f * f) * flicker : 0f;
            b.flame.localScale = new Vector3(s, s * 1.5f, s);
            b.light.intensity = lit ? .6f + 2.4f * f : 0f;
            EscapeKit.SetGlow(b.mat, Color.Lerp(new Color(1f, .25f, .1f), new Color(1f, .7f, .25f), f), 3f);
        }
        if (!Active) return;
        if (braziers.All(Lit)) { hold += Time.deltaTime; if (hold > .3f) Solve(lastTouch); }
        else hold = 0f;
    }

    protected override void OnSolved()
    {
        foreach (var b in braziers) b.litUntil = float.MaxValue;
        Confetti(W(new Vector3(0, 1.8f, .2f)), Glow, 70);
    }

    public override EscapeOrder Order(EscapeActor a)
    {
        // Co-op: a torch only lights for its owner. When the others burn, the local player is told to light theirs.
        if (braziers.Any(Lit)) foreach (var b in braziers) if (!b.use.Mine(a) && Left(b) < 6f) Want(b.use);
        if (a.Role == 0 && !D.AutoHuman) return Wait(a);
        if (claims.TryGetValue(a, out var mine) && Left(mine) < 6f && mine.use.Mine(a)) return EscapeOrder.Use(mine.use);
        claims.Remove(a);
        Brazier best = null;
        float bestScore = float.MaxValue;
        foreach (var b in braziers)
        {
            float left = Left(b);
            if (left >= 6f || !b.use.Mine(a) || claims.ContainsValue(b)) continue;
            float score = left * 2f + Vector3.Distance(W(b.local), a.transform.position);
            if (score < bestScore) { bestScore = score; best = b; }
        }
        if (best != null) { claims[a] = best; return EscapeOrder.Use(best.use); }
        return Wait(a);
    }
}

// ============================================================ 10 · final portal
public sealed class RoomPortal : EscapeRoom
{
    readonly List<EscapeItem> cells = new();
    readonly Dictionary<EscapeActor, EscapeItem> claims = new();
    readonly Material[] sockets = new Material[3];
    static readonly Vector3 Zone = new Vector3(0, 0, 6.6f);
    const float ZoneRadius = 2.4f;
    EscapeInteractable generator;
    Material portalMat, daisMat;
    EscapeSpin ringSpin;
    Light portalLight;
    int inserted, inZone;
    bool open;
    float allIn;

    public override string Title => "Portal del Multiverso";
    public override string Objective => !open ? $"El portal duerme. El generador pide energía ({inserted}/3)." : $"¡El portal está abierto! Entra en él.";
    public override string[] Clues => new[]
    {
        "Tres luces azules esperan en la sala. Una de ellas no está al alcance de la mano.",
        "Salta (Espacio) por la escalinata de la izquierda para alcanzar la célula alta. Cuando el portal se abra, entra en el círculo."
    };
    public override Color Floor => new Color(.32f, .26f, .5f);
    public override Color Wall => new Color(.48f, .4f, .7f);
    public override Color Accent => new Color(.7f, .45f, 1f);
    public override Color Glow => new Color(.8f, .7f, 1f);

    public override void Build()
    {
        Material metal = M("Hierro", new Color(.3f, .32f, .36f)), gold = M("Dorado", new Color(1f, .78f, .3f)), stone = M("Crema", new Color(.96f, .92f, .82f));
        Color cyan = new Color(.35f, 1f, .95f);

        // Generator.
        var gen = new Vector3(0, 0, -1.4f);
        Block("Generador", gen + new Vector3(0, .6f, 0), new Vector3(2.8f, 1.2f, 1.6f), metal, .12f);
        Block("Tapa del generador", gen + new Vector3(0, 1.26f, 0), new Vector3(2.9f, .12f, 1.7f), gold, .04f, false);
        for (int s = -1; s <= 1; s += 2)
        {
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Bobina", gen + new Vector3(s * 1.05f, 1.75f, 0), new Vector3(.36f, .45f, .36f), gold, false);
            EscapeKit.Shape(PrimitiveType.Sphere, transform, "Esfera", gen + new Vector3(s * 1.05f, 2.35f, 0), Vector3.one * .42f, EscapeKit.Glow(cyan, 1.2f), false).AddComponent<EscapeSpin>().bob = .06f;
        }
        for (int i = 0; i < 3; i++)
        {
            sockets[i] = EscapeKit.Glow(cyan, .1f);
            Block("Ranura", gen + new Vector3(-.8f + i * .8f, .75f, -.83f), new Vector3(.42f, .5f, .08f), sockets[i], .03f, false);
        }
        var genPoint = new GameObject("Generador (usar)");
        genPoint.transform.SetParent(transform, false);
        genPoint.transform.localPosition = gen + new Vector3(0, 1f, -1.1f);
        generator = EscapeInteractable.Add(genPoint, "Insertar la célula", .3f, Insert, a => Active && !open && a.Carried != null && a.Carried.kind == "celula", 2.1f);
        generator.Shared = true;

        // Cells: two on pedestals, one on top of a small stepped platform.
        AddCell(new Vector3(-8.2f, 0, -8.4f), 0f, stone);
        AddCell(new Vector3(8.2f, 0, -8.4f), 0f, stone);
        var top = new Vector3(-7.2f, 0, 5.2f);
        Block("Escalón ancho", top + new Vector3(0, .25f, 0), new Vector3(3.2f, .5f, 3.2f), stone, .08f);
        Block("Escalón alto", top + new Vector3(0, .5f, 0), new Vector3(2f, 1f, 2f), stone, .08f);
        AddCell(top, 1f, gold).Pick.maxBelow = 1.6f;

        // Portal ring, dais and light.
        var ringRoot = new GameObject("Anillo del portal").transform;
        ringRoot.SetParent(transform, false);
        ringRoot.localPosition = new Vector3(0, 2.2f, 9.2f);
        for (int i = 0; i < 20; i++)
        {
            float a = i / 20f * Mathf.PI * 2f;
            EscapeKit.Block(ringRoot, "Segmento", new Vector3(Mathf.Cos(a) * 2.05f, Mathf.Sin(a) * 2.05f, 0), new Vector3(.62f, .36f, .36f), i % 2 == 0 ? gold : P(Accent), .06f, false, new Vector3(0, 0, a * Mathf.Rad2Deg + 90f));
        }
        ringSpin = ringRoot.gameObject.AddComponent<EscapeSpin>();
        ringSpin.axis = Vector3.forward; ringSpin.speed = 8f;
        portalMat = EscapeKit.Glow(Accent, .25f);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Velo del portal", new Vector3(0, 2.2f, 9.2f), new Vector3(3.7f, .02f, 3.7f), portalMat, false, new Vector3(90, 0, 0));
        Block("Base del portal", new Vector3(0, .3f, 9.45f), new Vector3(4.8f, .6f, .8f), stone, .1f);
        daisMat = EscapeKit.Glow(Accent, .3f);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Círculo de salida", Zone + new Vector3(0, .03f, 0), new Vector3(ZoneRadius * 2f, .02f, ZoneRadius * 2f), daisMat, false);
        portalLight = EscapeKit.PointLight(transform, new Vector3(0, 2.4f, 8.3f), Accent, 12f, .4f);
    }

    EscapeItem AddCell(Vector3 local, float height, Material pedestalMat)
    {
        Block("Pedestal de la célula", local + new Vector3(0, height + .45f, 0), new Vector3(.7f, .9f, .7f), pedestalMat, .08f);
        var cell = EscapeItem.Create(transform, "celula", "una célula de energía", local + new Vector3(0, height + 1.35f, 0), it =>
        {
            var glow = EscapeKit.Glow(new Color(.35f, 1f, .95f), 2.4f);
            EscapeKit.Shape(PrimitiveType.Capsule, it.transform, "Núcleo", Vector3.zero, new Vector3(.3f, .25f, .3f), glow, false);
            EscapeKit.Shape(PrimitiveType.Cylinder, it.transform, "Tapa", new Vector3(0, .26f, 0), new Vector3(.34f, .04f, .34f), M("Dorado", new Color(1f, .78f, .3f)), false);
            EscapeKit.Shape(PrimitiveType.Cylinder, it.transform, "Tapa", new Vector3(0, -.26f, 0), new Vector3(.34f, .04f, .34f), M("Dorado", new Color(1f, .78f, .3f)), false);
        });
        cell.Pick.range = 1.3f;
        cells.Add(cell);
        return cell;
    }

    void Insert(EscapeActor a)
    {
        Touch(a);
        var it = a.TakeCarried();
        if (it != null) it.Consume();
        inserted++;
        EscapeAudio.Play("cell", generator.Point);
        if (inserted >= 3)
        {
            open = true;
            EscapeAudio.Play("portal", W(new Vector3(0, 2f, 9f)), 1f, 0f);
            D.Notify("¡EL PORTAL ESTÁ ABIERTO! Entrad TODOS en el círculo");
            D.View?.Shake(.45f);
            Confetti(W(new Vector3(0, 2.2f, 8.5f)), Accent, 90);
        }
    }

    public override void Tick()
    {
        for (int i = 0; i < 3; i++) EscapeKit.SetGlow(sockets[i], new Color(.35f, 1f, .95f), i < inserted ? 3f : .12f);
        float pulse = 1f + Mathf.Sin(Time.time * 4f) * .3f;
        EscapeKit.SetGlow(portalMat, Accent, open ? 3.2f * pulse : .25f);
        EscapeKit.SetGlow(daisMat, Accent, open ? 1.8f * pulse : .3f);
        ringSpin.speed = Mathf.MoveTowards(ringSpin.speed, open ? 70f : 8f, Time.deltaTime * 30f);
        portalLight.intensity = open ? 3.5f * pulse : .4f;
        if (!Active || !open) return;
        Vector3 z = W(Zone);
        inZone = Runners.Count(a => { var d = a.transform.position - z; d.y = 0f; return d.magnitude < ZoneRadius; });
        if (inZone == TeamSize) { allIn += Time.deltaTime; if (allIn > 1.5f) Solve(lastTouch); }
        else allIn = 0f;
    }

    public override EscapeOrder Order(EscapeActor a)
    {
        if (open)
        {
            float ang = a.Role / (float)Mathf.Max(1, TeamSize) * Mathf.PI * 2f;
            return EscapeOrder.Exact(W(Zone + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 1.2f));
        }
        if (a.Carried != null && a.Carried.kind == "celula") return EscapeOrder.Use(generator);
        foreach (var c in cells) if (c != null && c.Carrier == null && c.Pick.enabled && !c.Pick.Mine(a)) Want(c.Pick);
        if (claims.TryGetValue(a, out var mine) && mine != null && mine.Carrier == null && mine.Pick.enabled && mine.Pick.Mine(a)) return EscapeOrder.Use(mine.Pick);
        claims.Remove(a);
        EscapeItem best = null;
        float bestD = float.MaxValue;
        foreach (var c in cells)
        {
            if (c == null || c.Carrier != null || !c.Pick.enabled || claims.ContainsValue(c) || !c.Pick.Mine(a)) continue;   // co-op: each cell has an owner
            float d = (c.transform.position - a.transform.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = c; }
        }
        if (best != null) { claims[a] = best; return EscapeOrder.Use(best.Pick); }
        return EscapeOrder.Go(W(new Vector3((a.Role - 2.5f) * 1.1f, 0, -3.8f)), .6f);
    }
}
