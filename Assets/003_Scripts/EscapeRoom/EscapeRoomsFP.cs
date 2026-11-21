using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// First-person puzzle rooms for the Torre del Multiverso. Room space: x ∈ [-10, 10], z ∈ [-11, 11], +Z = exit door.
// Inner wall faces: exit wall z = 10.75, entrance wall z = -10.75, side walls x = ±9.75.
// Text orientation helpers: exit wall Vector3.zero, right wall (0, 90, 0), left wall (0, -90, 0), entrance wall (0, 180, 0).

public static class EscapeFP
{
    public const float ExitZ = 10.72f, BackZ = -10.72f, SideX = 9.72f;
    public static readonly Vector3 OnExit = Vector3.zero, OnRight = new(0, 90, 0), OnLeft = new(0, -90, 0), OnBack = new(0, 180, 0);
    public static readonly Color Ink = new(.16f, .1f, .06f), Parchment = new(.95f, .89f, .72f), Brass = new(1f, .78f, .3f);
}

// ============================================================ clock seen in a mirror
public sealed class RoomClock : EscapeRoom
{
    int targetH, targetM, curH = 12, curM;
    Transform hourHand, minuteHand, hourCrank, minuteCrank, leverPivot;
    EscapeInteractable hourUse, minuteUse, leverUse;
    float lockedUntil, hourShown, minuteShown, crankSpinH, crankSpinM, leverT;
    Material clockGlow;

    public override string Title => "Vestíbulo del Reloj";
    public override string Objective => Time.time < lockedUntil ? "El reloj se resiste… espera a que vuelva a las 12." : "El gran reloj espera una hora concreta. Ajústalo con las manivelas y tira de la palanca.";
    public override string[] Clues => new[]
    {
        "El espejo recuerda la hora en que la torre se durmió… pero un espejo cambia la izquierda por la derecha.",
        "Imagina el reloj del espejo visto desde detrás del cristal: las 3 del reflejo son las 9 de verdad; las 12 y las 6 no cambian."
    };
    public override Color Floor => new Color(.72f, .66f, .58f);
    public override Color Wall => new Color(.9f, .84f, .74f);
    public override Color Accent => new Color(.55f, .38f, .2f);
    public override Color Glow => new Color(1f, .9f, .72f);

    static float HourAngle(int h, int m) => (h % 12) * 30f + m * .5f;
    static float MinuteAngle(int m) => m * 6f;

    public override void Build()
    {
        int[] hours = { 1, 2, 4, 5, 7, 8, 10, 11 };
        int[] minutes = { 5, 10, 20, 25, 35, 40, 50, 55 };
        targetH = hours[Random.Range(0, hours.Length)];
        targetM = minutes[Random.Range(0, minutes.Length)];

        Material wood = M("Madera oscura", new Color(.36f, .22f, .13f)), gold = M("Dorado", EscapeFP.Brass), cream = M("Crema", new Color(.96f, .92f, .82f));
        Material iron = M("Hierro", new Color(.3f, .32f, .36f));

        // ---- the big clock on the exit wall (left of the door)
        var c = new Vector3(-5.6f, 2.35f, EscapeFP.ExitZ - .05f);
        Block("Caja del gran reloj", c + new Vector3(0, 0, .05f), new Vector3(3.3f, 3.3f, .2f), wood, .1f, false);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Esfera del reloj", c + new Vector3(0, 0, -.08f), new Vector3(2.9f, .04f, 2.9f), cream, false, new Vector3(90, 0, 0));
        clockGlow = EscapeKit.Glow(new Color(1f, .85f, .5f), .2f);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Aro del reloj", c + new Vector3(0, 0, -.07f), new Vector3(3.1f, .03f, 3.1f), clockGlow, false, new Vector3(90, 0, 0));
        for (int n = 1; n <= 12; n++)
        {
            float a = n * 30f * Mathf.Deg2Rad;
            EscapeKit.Text(transform, n.ToString(), c + new Vector3(Mathf.Sin(a) * 1.1f, Mathf.Cos(a) * 1.1f, -.12f), EscapeFP.OnExit, .03f, EscapeFP.Ink);
        }
        hourHand = Hand(c + new Vector3(0, 0, -.14f), .72f, .1f, EscapeFP.Ink, false);
        minuteHand = Hand(c + new Vector3(0, 0, -.17f), 1.05f, .06f, EscapeFP.Ink, false);
        EscapeKit.Shape(PrimitiveType.Sphere, transform, "Eje", c + new Vector3(0, 0, -.2f), Vector3.one * .16f, gold, false);

        // ---- console with two cranks and the lever
        var console = new Vector3(-5.6f, 0, EscapeFP.ExitZ - 1.6f);
        Block("Consola del reloj", console + new Vector3(0, .5f, 0), new Vector3(3.2f, 1f, .9f), wood, .08f);
        Block("Tapa de la consola", console + new Vector3(0, 1.03f, 0), new Vector3(3.3f, .08f, 1f), gold, .02f, false);
        hourCrank = Crank(console + new Vector3(-.85f, 1.25f, -.15f), "HORAS", iron, gold, out hourUse, a => Turn(a, true));
        minuteCrank = Crank(console + new Vector3(.85f, 1.25f, -.15f), "MINUTOS", iron, gold, out minuteUse, a => Turn(a, false));
        leverPivot = new GameObject("Palanca del reloj").transform; leverPivot.SetParent(transform, false);
        leverPivot.localPosition = new Vector3(-2.9f, .9f, EscapeFP.ExitZ - 1.4f);
        Block("Base de la palanca", leverPivot.localPosition + new Vector3(0, -.45f, 0), new Vector3(.5f, .9f, .5f), iron, .06f);
        EscapeKit.Shape(PrimitiveType.Cylinder, leverPivot, "Mango", new Vector3(0, .35f, 0), new Vector3(.08f, .35f, .08f), iron, false);
        EscapeKit.Shape(PrimitiveType.Sphere, leverPivot, "Pomo", new Vector3(0, .72f, 0), Vector3.one * .22f, P(new Color(.85f, .2f, .2f)), false);
        leverUse = EscapeInteractable.Add(leverPivot.gameObject, "Fijar la hora", 0f, Check, a => Active && Time.time >= lockedUntil, 2f);
        leverUse.aimRadius = .45f; leverUse.markOffset = new Vector3(0, 1.05f, 0);

        // ---- the mirror on the right wall shows the reflected clock (no numbers: only marks and a gold top)
        var mc = new Vector3(EscapeFP.SideX - .05f, 2.1f, 1.5f);
        Block("Marco del espejo", mc + new Vector3(.05f, 0, 0), new Vector3(.18f, 2.9f, 2.2f), gold, .08f, false);
        var glass = new Material(EscapeKit.Lit) { name = "Espejo" };
        glass.SetColor("_BaseColor", new Color(.72f, .82f, .9f)); glass.SetFloat("_Smoothness", .97f); glass.SetFloat("_Metallic", .9f);
        Block("Cristal del espejo", mc + new Vector3(-.05f, 0, 0), new Vector3(.04f, 2.6f, 1.9f), glass, .01f, false);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Reloj reflejado", mc + new Vector3(-.1f, .2f, 0), new Vector3(1.4f, .02f, 1.4f), cream, false, new Vector3(0, 0, 90));
        for (int n = 0; n < 12; n++)
        {
            float a = n * 30f * Mathf.Deg2Rad;
            bool top = n == 0;
            Block("Marca", mc + new Vector3(-.13f, .2f + Mathf.Cos(a) * .58f, -Mathf.Sin(a) * .58f), top ? new Vector3(.02f, .16f, .16f) : new Vector3(.02f, .1f, .05f),
                top ? gold : P(EscapeFP.Ink), .005f, false, new Vector3(-n * 30f, 0, 0));
        }
        // Mirror image: the hands turn the other way round.
        float hShown = HourAngle(targetH, targetM), mShown = MinuteAngle(targetM);
        var mirrorHour = Hand(mc + new Vector3(-.15f, .2f, 0), .36f, .06f, EscapeFP.Ink, true); mirrorHour.localRotation = Quaternion.Euler(hShown, 0, 0);
        var mirrorMinute = Hand(mc + new Vector3(-.17f, .2f, 0), .52f, .035f, EscapeFP.Ink, true); mirrorMinute.localRotation = Quaternion.Euler(mShown, 0, 0);
        Secret(mirrorHour); Secret(mirrorMinute);   // co-op: whoever pulls the lever cannot see the mirror's hands
        EscapeKit.Text(transform, "EL ESPEJO RECUERDA LA HORA\nEN QUE LA TORRE SE DURMIÓ", mc + new Vector3(-.04f, -1.72f, 0), EscapeFP.OnRight, .018f, EscapeFP.Ink);

        // ---- red herring: the night-watch clock on the left wall, stopped at a random time
        var wc = new Vector3(-EscapeFP.SideX + .05f, 2.2f, -3.5f);
        Block("Reloj del vigilante", wc, new Vector3(.16f, 1.5f, 1.5f), wood, .06f, false);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Esfera del vigilante", wc + new Vector3(.1f, 0, 0), new Vector3(1.2f, .02f, 1.2f), cream, false, new Vector3(0, 0, 90));
        int fakeH = Random.Range(1, 13), fakeM = Random.Range(0, 12) * 5;
        Hand(wc + new Vector3(.12f, 0, 0), .32f, .06f, EscapeFP.Ink, true).localRotation = Quaternion.Euler(HourAngle(fakeH, fakeM), 0, 0);
        Hand(wc + new Vector3(.14f, 0, 0), .46f, .035f, EscapeFP.Ink, true).localRotation = Quaternion.Euler(MinuteAngle(fakeM), 0, 0);
        EscapeKit.Text(transform, "EL VIGILANTE SIEMPRE\nLLEGA TARDE", wc + new Vector3(.12f, -1f, 0), EscapeFP.OnLeft, .018f, EscapeFP.Ink);

        Prop("Banco", new Vector3(-8.6f, 0, 4f), 90); Prop("Maceta", new Vector3(8.6f, 0, -9.4f), 0); Prop("Maceta", new Vector3(-8.6f, 0, -9.4f), 0);
        Prop("Cajas", new Vector3(8.4f, 0, 8.8f), 20);
    }

    Transform Hand(Vector3 center, float length, float width, Color color, bool sideWall)
    {
        var pivot = new GameObject("Manecilla").transform; pivot.SetParent(transform, false); pivot.localPosition = center;
        var size = sideWall ? new Vector3(.02f, length, width) : new Vector3(width, length, .02f);
        EscapeKit.Block(pivot, "Aguja", new Vector3(0, length * .42f, 0), size, P(color), .005f, false);
        return pivot;
    }

    Transform Crank(Vector3 at, string label, Material iron, Material gold, out EscapeInteractable use, System.Action<EscapeActor> action)
    {
        var pivot = new GameObject("Manivela " + label).transform; pivot.SetParent(transform, false); pivot.localPosition = at;
        EscapeKit.Shape(PrimitiveType.Cylinder, pivot, "Rueda", Vector3.zero, new Vector3(.55f, .05f, .55f), gold, false, new Vector3(90, 0, 0));
        EscapeKit.Block(pivot, "Radio", new Vector3(0, 0, -.05f), new Vector3(.5f, .06f, .04f), iron, .01f, false);
        EscapeKit.Shape(PrimitiveType.Cylinder, pivot, "Asa", new Vector3(.22f, 0, -.12f), new Vector3(.07f, .1f, .07f), iron, false, new Vector3(90, 0, 0));
        EscapeKit.Text(transform, label, at + new Vector3(0, -.48f, -.32f), new Vector3(20, 0, 0), .014f, EscapeFP.Parchment);
        use = EscapeInteractable.Add(pivot.gameObject, "Girar la manivela de las " + label.ToLowerInvariant(), 0f, action, a => Active && Time.time >= lockedUntil, 2f);
        use.aimRadius = .4f;
        return pivot;
    }

    void Turn(EscapeActor a, bool hours)
    {
        Touch(a);
        if (hours) { curH = curH % 12 + 1; crankSpinH += 90f; }
        else { curM = (curM + 5) % 60; crankSpinM += 90f; }
        EscapeAudio.Play("tick", W(new Vector3(-5.6f, 2.3f, 10.5f)), .7f);
    }

    void Check(EscapeActor a)
    {
        Touch(a);
        leverT = 1f;
        EscapeAudio.Play("lever", leverPivot.position);
        if (curH == targetH && curM == targetM) { EscapeAudio.Play("chime", W(new Vector3(-5.6f, 2.3f, 10.5f))); Solve(a); return; }
        lockedUntil = Time.time + 4f;
        curH = 12; curM = 0;
        EscapeAudio.Play("error", leverPivot.position);
        D.Notify("El reloj se resiste y vuelve a las 12…");
    }

    public override void Tick()
    {
        float h = HourAngle(curH, curM), m = MinuteAngle(curM);
        if (curH == 12 && curM == 0 && hourShown > 300f) hourShown -= 360f;
        hourShown = Mathf.MoveTowardsAngle(hourShown, h, Time.deltaTime * 260f);
        minuteShown = Mathf.MoveTowardsAngle(minuteShown, m, Time.deltaTime * 520f);
        hourHand.localRotation = Quaternion.Euler(0, 0, -hourShown);
        minuteHand.localRotation = Quaternion.Euler(0, 0, -minuteShown);
        hourCrank.localRotation = Quaternion.Euler(0, 0, -crankSpinH); minuteCrank.localRotation = Quaternion.Euler(0, 0, -crankSpinM);
        leverT = Mathf.MoveTowards(leverT, 0f, Time.deltaTime * 1.5f);
        leverPivot.localRotation = Quaternion.Euler(-40f + leverT * 80f, 0, 0);
        EscapeKit.SetGlow(clockGlow, Solved ? new Color(.4f, 1f, .5f) : Time.time < lockedUntil ? new Color(1f, .3f, .3f) : new Color(1f, .85f, .5f), Solved ? 2.2f : .4f);
    }

    protected override void OnSolved() => Confetti(W(new Vector3(-5.6f, 2.4f, 10f)), Accent, 60);

    protected override EscapeInteractable BlindStation => leverUse;
    public override string[] ReadAloud
    {
        get
        {
            int lo = 11 - targetH % 12, mark = (60 - targetM) / 5;
            return new[] { $"En el espejo, la aguja corta está entre el {(lo == 0 ? 12 : lo)} y el {lo + 1}…", $"…y la larga apunta a la marca del {mark} (contando como en un reloj, el 12 arriba)." };
        }
    }

    public override EscapeOrder Order(EscapeActor a)
    {
        if (a.Role != 1 || !Assist(120f) || Time.time < lockedUntil) return Wait(a);
        if (curH != targetH) return EscapeOrder.Use(hourUse);
        if (curM != targetM) return EscapeOrder.Use(minuteUse);
        return EscapeOrder.Use(leverUse);
    }
}

// ============================================================ bell melody
public sealed class RoomBells : EscapeRoom
{
    sealed class Bell { public int pitch; public Transform swing; public EscapeInteractable use; public float t; public Vector3 local; }
    readonly List<Bell> bells = new();
    readonly List<int> melody = new();
    readonly List<Material> candles = new();
    int progress;
    float lastRing = -10f;
    const int Notes = 6, Shown = 4;

    public override string Title => "Campanario";
    public override string Objective => "Las campanas guardan una melodía. Tocadla entera, sin un solo error.";
    public override string[] Clues => new[]
    {
        "La melodía no termina en la pared: alguien arrancó el final y lo dejó en un atril.",
        "Las campanas grandes suenan grave y las pequeñas agudo: abajo y arriba del pentagrama."
    };
    public override Color Floor => new Color(.55f, .58f, .7f);
    public override Color Wall => new Color(.78f, .8f, .9f);
    public override Color Accent => new Color(.85f, .65f, .25f);
    public override Color Glow => new Color(.9f, .92f, 1f);

    public override void Build()
    {
        Material gold = M("Dorado", EscapeFP.Brass), wood = M("Madera oscura", new Color(.36f, .22f, .13f)), iron = M("Hierro", new Color(.3f, .32f, .36f));
        var spots = new List<Vector3> { new(-6.5f, 0, -3.5f), new(-3.2f, 0, 2.2f), new(0, 0, -2.2f), new(3.2f, 0, 2.2f), new(6.5f, 0, -3.5f) };
        var pitches = new List<int> { 0, 1, 2, 3, 4 };
        Shuffle(pitches);
        for (int k = 0; k < 5; k++)
        {
            var b = new Bell { pitch = pitches[k], local = spots[k] };
            float s = 1.35f - b.pitch * .19f;
            // Frame: two posts and a crossbar.
            for (int side = -1; side <= 1; side += 2)
                Block("Poste del campanario", b.local + new Vector3(side * (s * .5f + .45f), 1.75f, 0), new Vector3(.22f, 3.5f, .22f), wood, .04f);
            Block("Travesaño", b.local + new Vector3(0, 3.45f, 0), new Vector3(s + 1.3f, .2f, .26f), wood, .04f, false);
            var swing = new GameObject("Campana").transform; swing.SetParent(transform, false);
            swing.localPosition = b.local + new Vector3(0, 3.3f, 0);
            float hgt = s * .9f;
            EscapeKit.Shape(PrimitiveType.Cylinder, swing, "Cuerpo de la campana", new Vector3(0, -hgt * .5f, 0), new Vector3(s * .75f, hgt * .5f, s * .75f), gold, false);
            EscapeKit.Shape(PrimitiveType.Sphere, swing, "Corona", new Vector3(0, -.05f, 0), new Vector3(s * .75f, s * .4f, s * .75f), gold, false);
            EscapeKit.Shape(PrimitiveType.Cylinder, swing, "Borde", new Vector3(0, -hgt, 0), new Vector3(s, .06f, s), gold, false);
            EscapeKit.Shape(PrimitiveType.Sphere, swing, "Badajo", new Vector3(0, -hgt - .12f, 0), Vector3.one * .16f, iron, false);
            b.swing = swing;
            var point = new GameObject("Tocar campana"); point.transform.SetParent(transform, false);
            point.transform.localPosition = b.local + new Vector3(0, 3.3f - hgt - .1f, 0);
            var bb = b;
            b.use = EscapeInteractable.Add(point, "Tocar la campana", 0f, a => Ring(bb, a), a => Active, 2.3f);
            b.use.maxBelow = 3.4f; b.use.aimRadius = .25f + s * .4f; b.use.markOffset = new Vector3(0, -.45f, 0);
            bells.Add(b);
        }

        // Melody without immediate repeats.
        int last = -1;
        for (int i = 0; i < Notes; i++) { int n; do n = Random.Range(0, 5); while (n == last); melody.Add(n); last = n; }

        // Staff on the exit wall (right of the door) with the first notes and a torn edge.
        var st = new Vector3(5.4f, 2.2f, EscapeFP.ExitZ - .04f);
        Block("Partitura", st + new Vector3(0, 0, .02f), new Vector3(4.4f, 1.9f, .06f), M("Crema", EscapeFP.Parchment), .02f, false);
        Staff(st, 4f, Shown, 0, 1f);
        for (int j = 0; j < 4; j++) Block("Borde arrancado", st + new Vector3(2.15f, -.75f + j * .5f, -.04f), new Vector3(.2f, .2f, .02f), P(Wall), .01f, false, new Vector3(0, 0, 45));
        EscapeKit.Text(transform, "…", st + new Vector3(1.85f, -.6f, -.06f), EscapeFP.OnExit, .05f, EscapeFP.Ink);

        // The torn page on a lectern in the back-left corner (players must turn around).
        var lect = new Vector3(-8.2f, 0, -8.8f);
        Block("Atril", lect + new Vector3(0, .55f, 0), new Vector3(.5f, 1.1f, .5f), wood, .05f);
        var page = new GameObject("Hoja arrancada").transform; page.SetParent(transform, false);
        page.localPosition = lect + new Vector3(0, 1.18f, 0); page.localRotation = Quaternion.Euler(-60f, 135f, 0);
        EscapeKit.Block(page, "Papel", Vector3.zero, new Vector3(1.1f, .8f, .02f), M("Crema", EscapeFP.Parchment), .005f, false);
        StaffOn(page, 1f, Notes - Shown, Shown, .4f);

        // Six candles on the exit wall shelf count correct notes.
        for (int i = 0; i < Notes; i++)
        {
            var m = EscapeKit.Glow(new Color(1f, .7f, .3f), 0f);
            Vector3 p = new Vector3(2.6f + i * .55f, .9f, EscapeFP.ExitZ - .3f);
            Block("Vela", p, new Vector3(.12f, .3f, .12f), M("Crema", new Color(.96f, .92f, .82f)), .02f, false);
            EscapeKit.Shape(PrimitiveType.Sphere, transform, "Llama", p + new Vector3(0, .22f, 0), new Vector3(.08f, .13f, .08f), m, false);
            candles.Add(m);
        }
        Block("Repisa de las velas", new Vector3(4f, .7f, EscapeFP.ExitZ - .3f), new Vector3(3.8f, .1f, .4f), wood, .02f, false);
        Prop("Banco", new Vector3(-8.6f, 0, 3f), 90); Prop("Banco", new Vector3(8.6f, 0, 3f), -90);
    }

    void Staff(Vector3 center, float width, int count, int start, float scale)
    {
        for (int l = 0; l < 5; l++)
            Block("Línea", center + new Vector3(0, (-.5f + l * .25f) * scale, -.03f), new Vector3(width * scale, .025f, .01f), P(EscapeFP.Ink), .002f, false);
        for (int i = 0; i < count; i++)
        {
            float x = (-width * .5f + .6f + i * .9f) * scale, y = (-.5f + melody[start + i] * .25f) * scale;
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Nota", center + new Vector3(x, y, -.04f), new Vector3(.24f, .01f, .18f) * scale, P(EscapeFP.Ink), false, new Vector3(90, 0, 20));
            Block("Plica", center + new Vector3(x + .1f * scale, y + .3f * scale, -.04f), new Vector3(.025f, .6f, .01f) * scale, P(EscapeFP.Ink), .002f, false);
        }
    }

    void StaffOn(Transform page, float width, int count, int start, float scale)
    {
        for (int l = 0; l < 5; l++)
            EscapeKit.Block(page, "Línea", new Vector3(0, (-.5f + l * .25f) * .55f, -.015f), new Vector3(width * .9f, .012f, .005f), P(EscapeFP.Ink), .002f, false);
        for (int i = 0; i < count; i++)
        {
            float x = -.2f + i * .4f, y = (-.5f + melody[start + i] * .25f) * .55f;
            EscapeKit.Shape(PrimitiveType.Cylinder, page, "Nota", new Vector3(x, y, -.02f), new Vector3(.12f, .005f, .09f), P(EscapeFP.Ink), false, new Vector3(90, 0, 20));
            EscapeKit.Block(page, "Plica", new Vector3(x + .05f, y + .16f, -.02f), new Vector3(.015f, .32f, .005f), P(EscapeFP.Ink), .002f, false);
        }
    }

    void Ring(Bell b, EscapeActor a)
    {
        Touch(a);
        b.t = 1f; lastRing = Time.time;
        EscapeAudio.Play("bell_" + b.pitch, b.swing.position, 1f, 0f);
        if (melody[progress] == b.pitch)
        {
            progress++;
            if (progress >= Notes) Solve(a);
        }
        else if (progress > 0 || melody[0] != b.pitch)
        {
            if (progress > 0) { EscapeAudio.Play("error", b.swing.position, .6f); D.Notify("La melodía se rompe… vuelve a empezar"); }
            progress = melody[0] == b.pitch ? 1 : 0;
        }
    }

    public override void Tick()
    {
        foreach (var b in bells)
        {
            b.t = Mathf.MoveTowards(b.t, 0f, Time.deltaTime * .7f);
            b.swing.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 9f) * 22f * b.t, 0, 0);
        }
        for (int i = 0; i < candles.Count; i++)
            EscapeKit.SetGlow(candles[i], new Color(1f, .7f, .3f), Solved || i < progress ? 2.6f + Mathf.Sin(Time.time * 13f + i) * .3f : 0f);
    }

    protected override void OnSolved() => Confetti(W(new Vector3(0, 2.5f, 0)), Accent, 70);

    public override EscapeOrder Order(EscapeActor a)
    {
        if (a.Role != 1 || !Assist(120f) || Time.time - lastRing < .5f) return Wait(a);
        return EscapeOrder.Use(bells.First(b => b.pitch == melody[progress]).use);
    }
}

// ============================================================ light beam and mirrors
public sealed class RoomMirrors : EscapeRoom
{
    sealed class Mirror { public Transform pivot; public Collider panel; public int state, solution = -1; public float yaw; public EscapeInteractable use; public Vector3 local; }
    readonly List<Mirror> mirrors = new();
    readonly Dictionary<Collider, Mirror> byPanel = new();
    LineRenderer beam;
    Collider receiver;
    Material receiverMat, beamMat;
    float lit;
    readonly RaycastHit[] hits = new RaycastHit[16];
    static readonly Vector3 Emitter = new(-9.5f, 1.3f, -2f);
    static readonly Color BeamColor = new(1f, .35f, .45f);

    public override string Title => "Galería de Espejos";
    public override string Objective => lit > 0f ? "¡La luz llega al cristal! Que no se mueva…" : "Un rayo de luz busca el cristal de la salida. Girad los espejos para guiarlo.";
    public override string[] Clues => new[]
    {
        "La luz rebota como una pelota contra una pared: sale con el mismo ángulo con el que llega.",
        "Uno de los espejos no hace falta. Sigue el rayo desde la lámpara, espejo a espejo, y no gires los que ya funcionan."
    };
    public override Color Floor => new Color(.36f, .34f, .44f);
    public override Color Wall => new Color(.55f, .52f, .66f);
    public override Color Accent => new Color(.9f, .35f, .5f);
    public override Color Glow => new Color(.75f, .72f, .95f);

    public override void Build()
    {
        Material stone = M("Crema", new Color(.86f, .84f, .9f)), gold = M("Dorado", EscapeFP.Brass), iron = M("Hierro", new Color(.3f, .32f, .36f));
        // Emitter on the left wall.
        Block("Lámpara del rayo", Emitter + new Vector3(-.1f, 0, 0), new Vector3(.5f, .6f, .6f), iron, .06f);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Lente", Emitter + new Vector3(.2f, 0, 0), new Vector3(.36f, .06f, .36f), EscapeKit.Glow(BeamColor, 2.5f), false, new Vector3(0, 0, 90));

        // Path mirrors (with the yaw that solves them) and one decoy.
        (Vector3 at, int sol)[] spots =
        {
            (new(-5f, 0, -2f), 1), (new(-5f, 0, -7f), 1), (new(6f, 0, -7f), 3), (new(6f, 0, 3f), 1), (new(-1f, 0, 3f), 1), (new(2.5f, 0, -2.5f), -1)
        };
        var glass = new Material(EscapeKit.Lit) { name = "Espejo" };
        glass.SetColor("_BaseColor", new Color(.78f, .88f, .95f)); glass.SetFloat("_Smoothness", .97f); glass.SetFloat("_Metallic", .85f);
        foreach (var (at, sol) in spots)
        {
            var m = new Mirror { local = at, solution = sol };
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Pedestal del espejo", at + new Vector3(0, .4f, 0), new Vector3(.7f, .4f, .7f), stone, true);
            var pivot = new GameObject("Espejo giratorio").transform; pivot.SetParent(transform, false);
            pivot.localPosition = at + new Vector3(0, 1.3f, 0);
            var panel = EscapeKit.Block(pivot, "Luna del espejo", Vector3.zero, new Vector3(1.25f, 1f, .07f), glass, .02f, true);
            EscapeKit.Block(pivot, "Marco", new Vector3(0, -.56f, 0), new Vector3(1.35f, .1f, .12f), gold, .02f, false);
            EscapeKit.Block(pivot, "Marco", new Vector3(0, .56f, 0), new Vector3(1.35f, .1f, .12f), gold, .02f, false);
            m.pivot = pivot; m.panel = panel.GetComponent<Collider>();
            do m.state = Random.Range(0, 4); while (m.state == m.solution);
            m.yaw = m.state * 45f;
            var mm = m;
            m.use = EscapeInteractable.Add(pivot.gameObject, "Girar el espejo", 0f, a => Rotate(mm, a), a => Active, 2.1f);
            m.use.aimRadius = .7f; m.use.markOffset = new Vector3(0, .8f, 0);
            byPanel[m.panel] = m;
            mirrors.Add(m);
        }
        // Receiver crystal beside the exit door.
        var rc = new Vector3(-1f, 0, 9.6f);
        Block("Peana del cristal", rc + new Vector3(0, .5f, 0), new Vector3(.8f, 1f, .8f), stone, .08f);
        receiverMat = EscapeKit.Glow(new Color(.5f, 1f, .8f), .3f);
        var crystal = Block("Cristal receptor", rc + new Vector3(0, 1.35f, 0), new Vector3(.55f, .8f, .55f), receiverMat, .2f, true, new Vector3(0, 45, 0));
        receiver = crystal.GetComponent<Collider>();
        // Pillars that block lazy shortcuts.
        Block("Columna", new Vector3(-7.4f, 1.8f, 5.2f), new Vector3(.9f, 3.6f, .9f), stone, .1f);
        Block("Columna", new Vector3(8.2f, 1.8f, -1f), new Vector3(.9f, 3.6f, .9f), stone, .1f);
        Block("Columna", new Vector3(2.4f, 1.8f, 7.2f), new Vector3(.9f, 3.6f, .9f), stone, .1f);

        var go = new GameObject("Rayo de luz"); go.transform.SetParent(transform, false);
        beam = go.AddComponent<LineRenderer>();
        beamMat = EscapeKit.Glow(BeamColor, 3f);
        beam.sharedMaterial = beamMat; beam.widthMultiplier = .06f; beam.numCapVertices = 2; beam.useWorldSpace = true;
        beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Prop("Maceta", new Vector3(8.6f, 0, -9.4f), 0); Prop("Maceta", new Vector3(-8.6f, 0, 9.4f), 0);
    }

    void Rotate(Mirror m, EscapeActor a)
    {
        Touch(a);
        m.state = (m.state + 1) % 4;
        EscapeAudio.Play("mirror", m.pivot.position, .8f);
    }

    public override void Tick()
    {
        foreach (var m in mirrors)
        {
            m.yaw = Mathf.MoveTowardsAngle(m.yaw, m.state * 45f, Time.deltaTime * 240f);
            m.pivot.localRotation = Quaternion.Euler(0, m.yaw, 0);
        }
        Physics.SyncTransforms();
        var points = new List<Vector3> { W(Emitter + new Vector3(.25f, 0, 0)) };
        Vector3 origin = points[0], dir = transform.rotation * Vector3.right;
        bool hitReceiver = false;
        for (int bounce = 0; bounce < 12; bounce++)
        {
            if (!Cast(origin, dir, out var hit)) { points.Add(origin + dir * 30f); break; }
            points.Add(hit.point);
            if (hit.collider == receiver) { hitReceiver = true; break; }
            if (!byPanel.ContainsKey(hit.collider)) break;
            dir = Vector3.Reflect(dir, hit.normal); dir.y = 0f; dir.Normalize();
            origin = hit.point + dir * .02f;
        }
        beam.positionCount = points.Count;
        beam.SetPositions(points.ToArray());
        beam.widthMultiplier = .05f + Mathf.Sin(Time.time * 20f) * .008f;
        if (hitReceiver && Active) { lit += Time.deltaTime; if (lit > 1f) Solve(lastTouch); }
        else lit = 0f;
        EscapeKit.SetGlow(receiverMat, new Color(.5f, 1f, .8f), Solved ? 3f : hitReceiver ? 2.2f + Mathf.Sin(Time.time * 12f) : .3f);
    }

    bool Cast(Vector3 origin, Vector3 dir, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(origin, dir, hits, 40f, ~0, QueryTriggerInteraction.Ignore);
        float bestD = float.MaxValue; bool found = false;
        for (int i = 0; i < n; i++)
        {
            var c = hits[i].collider;
            if (c == null || c.GetComponentInParent<EscapeActor>() != null || c.GetComponentInParent<EscapeItem>() != null) continue;
            if (hits[i].distance < bestD) { bestD = hits[i].distance; best = hits[i]; found = true; }
        }
        return found;
    }

    protected override void OnSolved() { Confetti(W(new Vector3(-1f, 2f, 9.4f)), BeamColor, 70); EscapeAudio.Play("chime", W(new Vector3(-1f, 1.5f, 9.4f))); }

    public override EscapeOrder Order(EscapeActor a)
    {
        if (a.Role != 1 || !Assist(110f)) return Wait(a);
        foreach (var m in mirrors)
            if (m.solution >= 0 && m.state != m.solution && Mathf.Abs(Mathf.DeltaAngle(m.yaw, m.state * 45f)) < 2f) return EscapeOrder.Use(m.use);
        return Wait(a);
    }
}

// ============================================================ Caesar cipher
public sealed class RoomCipher : EscapeRoom
{
    static readonly string[] Words = { "LLAVE", "PUENTE", "FAROL", "TORRE", "CIELO", "ESPEJO", "BRUJULA", "ESTRELLA", "LINTERNA", "TRUENO", "VOLCAN", "CORONA" };
    string word, secret;
    int shift, wheel;
    Transform inner;
    EscapeInteractable keypad, wheelUse;
    float lockedUntil, wheelShown;
    Material screen;

    public override string Title => "Gabinete del Cifrado";
    public override string Objective => "La puerta solo obedece a la palabra verdadera. El pergamino la esconde.";
    public override string[] Clues => new[]
    {
        "Las velas de la repisa no están ahí solo para dar luz.",
        "Cada letra del pergamino avanzó tantas posiciones en el alfabeto como velas encendidas hay: hazlas retroceder (la rueda de la pared ayuda)."
    };
    public override Color Floor => new Color(.46f, .3f, .3f);
    public override Color Wall => new Color(.76f, .6f, .56f);
    public override Color Accent => new Color(.6f, .22f, .26f);
    public override Color Glow => new Color(1f, .8f, .62f);

    static char Shift(char c, int k) => (char)('A' + ((c - 'A' + k) % 26 + 26) % 26);

    public override void Build()
    {
        word = Words[Random.Range(0, Words.Length)];
        shift = Random.Range(3, 10);
        secret = new string(word.Select(c => Shift(c, shift)).ToArray());
        Material wood = M("Madera oscura", new Color(.36f, .22f, .13f)), gold = M("Dorado", EscapeFP.Brass), parchment = M("Crema", EscapeFP.Parchment);

        // Scroll on the exit wall, left of the door.
        var sc = new Vector3(-5.4f, 2.2f, EscapeFP.ExitZ - .04f);
        Block("Pergamino", sc, new Vector3(4.4f, 1.6f, .05f), parchment, .02f, false);
        for (int s = -1; s <= 1; s += 2) EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Rodillo", sc + new Vector3(0, s * .85f, -.04f), new Vector3(.14f, 2.35f, .14f), wood, false, new Vector3(0, 0, 90));
        Secret(EscapeKit.Text(transform, string.Join(" ", secret.ToCharArray()), sc + new Vector3(0, -.05f, -.05f), EscapeFP.OnExit, .06f, EscapeFP.Ink));
        EscapeKit.Text(transform, "LA PUERTA OBEDECE A LA PALABRA VERDADERA", sc + new Vector3(0, .55f, -.05f), EscapeFP.OnExit, .016f, EscapeFP.Ink);

        // Nine candles on the right wall shelf: only `shift` of them are lit.
        var shelf = new Vector3(EscapeFP.SideX - .35f, 1f, -1f);
        Block("Repisa de las velas", shelf, new Vector3(.5f, .1f, 5.6f), wood, .02f);
        var lit = Enumerable.Range(0, 9).OrderBy(_ => Random.value).Take(shift).ToHashSet();
        for (int i = 0; i < 9; i++)
        {
            Vector3 p = shelf + new Vector3(0, .25f, -2.4f + i * .6f);
            float h = .3f + (i * 5 % 4) * .08f;
            Block("Vela", p + new Vector3(0, h * .5f - .2f, 0), new Vector3(.14f, h, .14f), M("Crema", new Color(.96f, .92f, .82f)), .02f, false);
            if (lit.Contains(i))
            {
                EscapeKit.Shape(PrimitiveType.Sphere, transform, "Llama", p + new Vector3(0, h - .08f, 0), new Vector3(.09f, .15f, .09f), EscapeKit.Glow(new Color(1f, .7f, .3f), 3f), false)
                    .AddComponent<EscapeSpin>().bob = .01f;
                if (i % 3 == 0) EscapeKit.PointLight(transform, p + Vector3.up * .5f, new Color(1f, .7f, .4f), 3f, .8f);
            }
            else EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Mecha", p + new Vector3(0, h - .17f, 0), new Vector3(.02f, .04f, .02f), P(EscapeFP.Ink), false);
        }
        EscapeKit.Text(transform, "CADA LLAMA EMPUJA LAS LETRAS", shelf + new Vector3(-.2f, 1.2f, 0), EscapeFP.OnRight, .018f, new Color(1f, .92f, .75f));

        // Cipher wheel on the left wall: outer alphabet fixed, inner one turns.
        var wc = new Vector3(-EscapeFP.SideX + .06f, 2f, -1.5f);
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Rueda exterior", wc, new Vector3(3.1f, .05f, 3.1f), parchment, false, new Vector3(0, 0, 90));
        for (int i = 0; i < 26; i++)
        {
            float a = i / 26f * Mathf.PI * 2f;
            EscapeKit.Text(transform, ((char)('A' + i)).ToString(), wc + new Vector3(.06f, Mathf.Cos(a) * 1.38f, Mathf.Sin(a) * 1.38f), EscapeFP.OnLeft, .022f, EscapeFP.Ink);
        }
        inner = new GameObject("Rueda interior").transform; inner.SetParent(transform, false); inner.localPosition = wc + new Vector3(.08f, 0, 0);
        EscapeKit.Shape(PrimitiveType.Cylinder, inner, "Disco", Vector3.zero, new Vector3(2.35f, .04f, 2.35f), P(new Color(.55f, .25f, .28f)), false, new Vector3(0, 0, 90));
        for (int i = 0; i < 26; i++)
        {
            float a = i / 26f * Mathf.PI * 2f;
            EscapeKit.Text(inner, ((char)('A' + i)).ToString(), new Vector3(.05f, Mathf.Cos(a) * 1f, Mathf.Sin(a) * 1f), EscapeFP.OnLeft, .02f, new Color(1f, .9f, .75f));
        }
        EscapeKit.Shape(PrimitiveType.Sphere, inner, "Pomo", new Vector3(.08f, 0, 0), Vector3.one * .25f, gold, false);
        Block("Flecha", wc + new Vector3(.05f, 1.62f, 0), new Vector3(.04f, .16f, .16f), gold, .01f, false, new Vector3(45, 0, 0));
        wheelUse = EscapeInteractable.Add(inner.gameObject, "Girar el disco interior", 0f, a => { wheel = (wheel + 1) % 26; Touch(a); EscapeAudio.Play("tick", inner.position, .6f); }, a => Active, 2.2f);
        wheelUse.maxBelow = 3f; wheelUse.aimRadius = 1f; wheelUse.markOffset = new Vector3(.12f, 1.85f, 0);

        // Lectern keypad near the exit.
        var pad = new Vector3(3.2f, 0, EscapeFP.ExitZ - 1.8f);
        Block("Atril del teclado", pad + new Vector3(0, .55f, 0), new Vector3(.9f, 1.1f, .6f), wood, .08f);
        screen = EscapeKit.Glow(new Color(1f, .75f, .4f), .8f);
        Block("Placa de letras", pad + new Vector3(0, 1.2f, -.1f), new Vector3(.8f, .46f, .08f), screen, .03f, false, new Vector3(-30, 0, 0));
        var kp = new GameObject("Teclado de letras"); kp.transform.SetParent(transform, false); kp.transform.localPosition = pad + new Vector3(0, 1.1f, -.3f);
        keypad = EscapeInteractable.Add(kp, "Escribir la palabra", 0f, UseKeypad, a => Active && Time.time >= lockedUntil, 2f); keypad.NetLocal = true;
        Prop("Banco", new Vector3(0, 0, -8.6f), 0); Prop("Tesoro", new Vector3(-8.4f, 0, 8.6f), 30);
    }

    public override bool KeypadSubmit(string code, EscapeActor a) => Submit(code, a);
    void UseKeypad(EscapeActor a)
    {
        Touch(a);
        if (a.DrivenByBot) { D.NetSubmit(this, Assist(120f) ? word : new string(word.Reverse().ToArray()), a); return; }
        D.HUD.OpenKeypad("PALABRA VERDADERA (" + word.Length + " LETRAS)", w => D.NetSubmit(this, w, a), word.Length, true);
    }

    bool Submit(string guess, EscapeActor a)
    {
        EscapeAudio.Play("beep", keypad.Point);
        if (guess == word) { EscapeKit.SetGlow(screen, new Color(.4f, 1f, .5f), 3f); Solve(a); return true; }
        lockedUntil = Time.time + 4f;
        EscapeKit.SetGlow(screen, new Color(1f, .3f, .3f), 2.5f);
        EscapeAudio.Play("error", keypad.Point);
        D.Notify("La puerta no reconoce esa palabra · atril bloqueado 4 s");
        return false;
    }

    public override void Tick()
    {
        wheelShown = Mathf.MoveTowardsAngle(wheelShown, wheel * (360f / 26f), Time.deltaTime * 200f);
        inner.localRotation = Quaternion.Euler(wheelShown, 0, 0);
        if (!Solved && Time.time > lockedUntil) EscapeKit.SetGlow(screen, new Color(1f, .75f, .4f), .8f + Mathf.Sin(Time.time * 3f) * .2f);
    }

    protected override void OnSolved() => Confetti(W(new Vector3(-5.4f, 2.2f, 10f)), Accent, 70);

    protected override EscapeInteractable BlindStation => keypad;
    public override string[] ReadAloud => new[] { "En el pergamino pone: " + string.Join(" ", secret.ToCharArray()) };

    public override EscapeOrder Order(EscapeActor a)
    {
        if (a.Role != 1 || !Assist(120f) || Time.time < lockedUntil) return Wait(a);
        return EscapeOrder.Use(keypad);
    }
}

// ============================================================ 3×3 sliding puzzle
public sealed class RoomSlider : EscapeRoom
{
    readonly int[] board = new int[9];      // board[cell] = tile number (0 = empty)
    int[] goal;
    readonly Transform[] tiles = new Transform[9];
    readonly EscapeInteractable[] uses = new EscapeInteractable[9];
    readonly Vector3[] shown = new Vector3[9];
    List<int> plan;
    int planFor = -1, nextCode = -1;
    static readonly Vector3 Center = new(5.4f, 2f, EscapeFP.ExitZ - .1f);
    const float Pitch = .82f;
    static readonly int[][] Goals =
    {
        new[] { 1, 2, 3, 4, 5, 6, 7, 8, 0 },
        new[] { 1, 2, 3, 6, 5, 4, 7, 8, 0 },
        new[] { 1, 2, 3, 8, 0, 4, 7, 6, 5 },
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 },
    };
    static readonly Color[] TileColors = { Color.clear, new(1f, .4f, .4f), new(1f, .65f, .3f), new(1f, .88f, .3f), new(.5f, .9f, .4f), new(.35f, .85f, .85f), new(.4f, .6f, 1f), new(.65f, .45f, 1f), new(1f, .5f, .8f) };

    public override string Title => "Taller del Rompecabezas";
    public override string Objective => $"Las piezas de la pared deben quedar como en el plano ({Correct()}/8 en su sitio).";
    public override string[] Clues => new[]
    {
        "El plano está sobre el banco de trabajo, al otro lado del taller. No todos los planos ordenan del 1 al 8.",
        "Coloca primero la fila de arriba y no la vuelvas a tocar; después la columna izquierda. El resto se resuelve girando las piezas en círculo."
    };
    public override Color Floor => new Color(.62f, .5f, .36f);
    public override Color Wall => new Color(.86f, .76f, .6f);
    public override Color Accent => new Color(.4f, .55f, .8f);
    public override Color Glow => new Color(1f, .9f, .7f);

    Vector3 CellPos(int cell) => Center + new Vector3((cell % 3 - 1) * Pitch, (1 - cell / 3) * Pitch, -.06f);
    int Correct() => Enumerable.Range(0, 9).Count(i => goal != null && board[i] != 0 && board[i] == goal[i]);

    public override void Build()
    {
        goal = Goals[Random.Range(0, Goals.Length)];
        System.Array.Copy(goal, board, 9);
        // Scramble with legal moves from the goal (always solvable), never undoing the previous move.
        int last = -1;
        for (int n = 0; n < 60; n++)
        {
            int empty = System.Array.IndexOf(board, 0);
            var options = Neighbors(empty).Where(c => c != last).ToList();
            int pick = options[Random.Range(0, options.Count)];
            board[empty] = board[pick]; board[pick] = 0; last = empty;
        }
        if (Correct() >= 6) { int e = System.Array.IndexOf(board, 0); int p = Neighbors(e)[0]; board[e] = board[p]; board[p] = 0; }

        Material wood = M("Madera oscura", new Color(.36f, .22f, .13f)), gold = M("Dorado", EscapeFP.Brass);
        Block("Marco del rompecabezas", Center + new Vector3(0, 0, .04f), new Vector3(Pitch * 3 + .4f, Pitch * 3 + .4f, .12f), wood, .06f, false);
        Block("Fondo", Center + new Vector3(0, 0, -.02f), new Vector3(Pitch * 3 + .05f, Pitch * 3 + .05f, .02f), P(new Color(.18f, .14f, .12f)), .01f, false);
        for (int n = 1; n <= 8; n++)
        {
            var t = new GameObject("Pieza " + n).transform; t.SetParent(transform, false);
            EscapeKit.Block(t, "Cara", Vector3.zero, new Vector3(Pitch - .05f, Pitch - .05f, .12f), P(TileColors[n]), .05f, true);
            EscapeKit.Text(t, n.ToString(), new Vector3(0, 0, -.08f), EscapeFP.OnExit, .045f, EscapeFP.Ink);
            tiles[n] = t;
            int num = n;
            uses[n] = EscapeInteractable.Add(t.gameObject, "Deslizar la pieza " + n, 0f, a => Slide(num, a), a => Active && CanSlide(num), 2.5f);
            uses[n].maxBelow = 3.6f; uses[n].aimRadius = .32f; uses[n].markOffset = new Vector3(.27f, .27f, -.12f);
        }
        for (int c = 0; c < 9; c++) if (board[c] != 0) { tiles[board[c]].localPosition = CellPos(c); shown[board[c]] = CellPos(c); }

        // Blueprint on the workbench by the entrance (left side): a small 3×3 grid with the goal.
        var bench = new Vector3(-7.6f, 0, -7.4f);
        Block("Banco de trabajo", bench + new Vector3(0, .45f, 0), new Vector3(2.4f, .9f, 1.2f), wood, .06f);
        var plano = new GameObject("Plano").transform; plano.SetParent(transform, false);
        plano.localPosition = bench + new Vector3(0, .93f, 0); plano.localRotation = Quaternion.Euler(90, 0, 0);
        EscapeKit.Block(plano, "Papel azul", Vector3.zero, new Vector3(1.1f, .9f, .01f), P(new Color(.25f, .4f, .75f)), .004f, false);
        for (int c = 0; c < 9; c++)
        {
            var p = new Vector3((c % 3 - 1) * .26f, (1 - c / 3) * .24f, -.01f);
            EscapeKit.Block(plano, "Casilla", p, new Vector3(.22f, .2f, .005f), P(goal[c] == 0 ? new Color(.2f, .3f, .6f) : new Color(.85f, .9f, 1f)), .003f, false);
            if (goal[c] != 0) EscapeKit.Text(plano, goal[c].ToString(), p + new Vector3(0, 0, -.006f), Vector3.zero, .014f, new Color(.15f, .2f, .45f));
        }
        EscapeKit.Text(plano, "PLANO DEL MAESTRO", new Vector3(0, -.4f, -.012f), Vector3.zero, .009f, new Color(1f, .95f, .82f));
        Prop("Cajas", new Vector3(8.4f, 0, -8.6f), 20); Prop("Cajas", new Vector3(-8.4f, 0, 8.4f), -20);
    }

    static List<int> Neighbors(int cell)
    {
        var r = new List<int>();
        int x = cell % 3, y = cell / 3;
        if (x > 0) r.Add(cell - 1); if (x < 2) r.Add(cell + 1); if (y > 0) r.Add(cell - 3); if (y < 2) r.Add(cell + 3);
        return r;
    }

    bool CanSlide(int tile)
    {
        int at = System.Array.IndexOf(board, tile), empty = System.Array.IndexOf(board, 0);
        return Neighbors(empty).Contains(at);
    }

    void Slide(int tile, EscapeActor a)
    {
        if (!CanSlide(tile)) return;
        Touch(a);
        int at = System.Array.IndexOf(board, tile), empty = System.Array.IndexOf(board, 0);
        board[empty] = tile; board[at] = 0;
        EscapeAudio.Play("slide", W(CellPos(empty)), .7f);
        if (board.SequenceEqual(goal)) Solve(a);
    }

    public override void Tick()
    {
        for (int c = 0; c < 9; c++)
        {
            int n = board[c]; if (n == 0) continue;
            shown[n] = Vector3.MoveTowards(shown[n], CellPos(c), Time.deltaTime * 4f);
            tiles[n].localPosition = shown[n];
        }
    }

    protected override void OnSolved() => Confetti(W(Center + new Vector3(0, 0, -.6f)), Accent, 70);

    int Encode(int[] b) { int v = 0; for (int i = 0; i < 9; i++) v = v * 9 + b[i]; return v; }

    /// <summary>Breadth-first search over board states (at most 181 440): returns the tiles to move, in order.</summary>
    List<int> Plan(int[] start)
    {
        int goalCode = Encode(goal), startCode = Encode(start);
        var parent = new Dictionary<int, (int prev, int tile)> { [startCode] = (-1, 0) };
        var queue = new Queue<int[]>(); queue.Enqueue((int[])start.Clone());
        while (queue.Count > 0)
        {
            var b = queue.Dequeue();
            int code = Encode(b);
            if (code == goalCode) break;
            int e = System.Array.IndexOf(b, 0);
            foreach (int c in Neighbors(e))
            {
                var nb = (int[])b.Clone(); nb[e] = nb[c]; nb[c] = 0;
                int nc = Encode(nb);
                if (parent.ContainsKey(nc)) continue;
                parent[nc] = (code, b[c]);
                queue.Enqueue(nb);
            }
        }
        var moves = new List<int>();
        if (!parent.ContainsKey(goalCode)) return moves;
        for (int at = goalCode; parent[at].prev != -1; at = parent[at].prev) moves.Add(parent[at].tile);
        moves.Reverse();
        return moves;
    }

    public override EscapeOrder Order(EscapeActor a)
    {
        if (a.Role != 1 || !Assist(150f)) return Wait(a);
        int code = Encode(board);
        if (plan != null && plan.Count > 0 && code == nextCode) { plan.RemoveAt(0); planFor = code; }
        if (plan == null || planFor != code) { plan = Plan(board); planFor = code; }
        if (plan.Count == 0) return Wait(a);
        int tile = plan[0];
        if (!CanSlide(tile)) { plan = null; return Wait(a); }
        var after = (int[])board.Clone();
        int from = System.Array.IndexOf(after, tile), hole = System.Array.IndexOf(after, 0);
        after[hole] = tile; after[from] = 0; nextCode = Encode(after);
        return EscapeOrder.Use(uses[tile]);
    }
}

// ============================================================ symbols → colours → levers
public sealed class RoomSymbols : EscapeRoom
{
    static readonly Vector3[] Spots = { new(-9.5f, 0, -4.5f), new(-9.5f, 0, 3.5f), new(9.5f, 0, -5.5f), new(9.5f, 0, 0f), new(9.5f, 0, 5.5f) };
    static readonly Color[] Colors5 = { new(1f, .3f, .32f), new(.3f, .55f, 1f), new(.35f, .9f, .4f), new(1f, .85f, .2f), new(.75f, .4f, 1f) };
    static readonly string[] ColorNames = { "roja", "azul", "verde", "amarilla", "morada" };
    readonly Transform[] handles = new Transform[5];
    readonly float[] sides = new float[5], angles = new float[5];
    readonly EscapeInteractable[] levers = new EscapeInteractable[5];
    readonly bool[] down = new bool[5];
    readonly int[] colorOfSymbol = new int[5];   // symbol s is translated to colour colorOfSymbol[s]
    readonly List<int> sequence = new();          // symbols shown on the board, in order
    readonly List<Material> boardMats = new();
    EscapePlate lectern;
    int progress;

    public override string Title => "Sala de los Símbolos";
    public override string Objective => $"Las palancas esperan un orden ({progress}/4). El tablero solo despierta si alguien lo lee desde el atril.";
    public override string[] Clues => new[]
    {
        "El tablero habla en formas, no en colores. Date la vuelta: el tapiz de la entrada traduce cada forma.",
        "Una palanca sobra: su forma nunca aparece en el tablero. Memoriza el orden en el atril antes de ir a las palancas."
    };
    public override Color Floor => new Color(.6f, .64f, .72f);
    public override Color Wall => new Color(.8f, .82f, .88f);
    public override Color Accent => new Color(.45f, .5f, .62f);
    public override Color Glow => new Color(.85f, .9f, 1f);

    GameObject Symbol(Transform parent, int s, Vector3 at, float size, Material mat, Vector3 faceEuler)
    {
        var root = new GameObject("Símbolo " + s).transform; root.SetParent(parent, false); root.localPosition = at; root.localRotation = Quaternion.Euler(faceEuler);
        switch (s)
        {
            case 0: EscapeKit.Shape(PrimitiveType.Sphere, root, "Círculo", Vector3.zero, new Vector3(size, size, size * .3f), mat, false); break;
            case 1: EscapeKit.Block(root, "Cuadrado", Vector3.zero, new Vector3(size * .85f, size * .85f, size * .25f), mat, .02f, false); break;
            case 2: EscapeKit.Block(root, "Rombo", Vector3.zero, new Vector3(size * .7f, size * .7f, size * .25f), mat, .02f, false, new Vector3(0, 0, 45)); break;
            case 3:
                EscapeKit.Block(root, "Cruz", Vector3.zero, new Vector3(size, size * .28f, size * .25f), mat, .02f, false);
                EscapeKit.Block(root, "Cruz", Vector3.zero, new Vector3(size * .28f, size, size * .25f), mat, .02f, false); break;
            default:
                EscapeKit.Block(root, "Barra", new Vector3(-size * .25f, 0, 0), new Vector3(size * .2f, size, size * .25f), mat, .02f, false);
                EscapeKit.Block(root, "Barra", new Vector3(size * .25f, 0, 0), new Vector3(size * .2f, size, size * .25f), mat, .02f, false); break;
        }
        return root.gameObject;
    }

    public override void Build()
    {
        var colors = new List<int> { 0, 1, 2, 3, 4 }; Shuffle(colors);
        for (int s = 0; s < 5; s++) colorOfSymbol[s] = colors[s];
        var symbols = new List<int> { 0, 1, 2, 3, 4 }; Shuffle(symbols);
        sequence.AddRange(symbols.Take(4));          // the fifth symbol (and its lever) is the decoy
        var spots = Enumerable.Range(0, 5).ToList(); Shuffle(spots);
        Material iron = M("Hierro", new Color(.32f, .34f, .38f));
        for (int k = 0; k < 5; k++)          // lever k has colour k
        {
            Vector3 p = Spots[spots[k]];
            float side = Mathf.Sign(p.x);
            sides[k] = side;
            Block($"Palanca {ColorNames[k]}", new Vector3(p.x + side * .12f, .85f, p.z), new Vector3(.4f, 1.3f, .8f), iron, .08f);
            Block("Placa de color", new Vector3(p.x - side * .1f, 1.3f, p.z), new Vector3(.04f, .34f, .56f), EscapeKit.Glow(Colors5[k], 1.2f), .01f, false);
            var pivot = new GameObject("Eje de la palanca").transform; pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(p.x - side * .12f, .85f, p.z);
            EscapeKit.Shape(PrimitiveType.Cylinder, pivot, "Mango", new Vector3(0, .32f, 0), new Vector3(.09f, .32f, .09f), iron, false);
            EscapeKit.Shape(PrimitiveType.Sphere, pivot, "Pomo", new Vector3(0, .66f, 0), Vector3.one * .24f, P(Colors5[k]), false);
            handles[k] = pivot; angles[k] = 35f;
            int idx = k;
            levers[k] = EscapeInteractable.Add(pivot.gameObject, $"Bajar la palanca {ColorNames[k]}", 0f, a => Pull(idx, a), a => Active && !down[idx], 2f);
            levers[k].aimRadius = .5f; levers[k].markOffset = new Vector3(0, 1f, 0);
        }

        lectern = EscapePlate.Create(transform, "Atril", new Vector3(0, 0, -1.6f), .8f, new Color(1f, .95f, .78f));
        Block("Atril de lectura", new Vector3(0, .55f, -.45f), new Vector3(.8f, 1.1f, .35f), M("Madera oscura", new Color(.35f, .22f, .14f)), .06f);
        Block("Libro abierto", new Vector3(0, 1.14f, -.5f), new Vector3(.7f, .06f, .45f), M("Crema", new Color(.96f, .92f, .82f)), .02f, false, new Vector3(-25, 0, 0));

        // Board on the exit wall: four white shapes in order (lit only while someone stands at the lectern).
        float z = EscapeFP.ExitZ - .05f;
        Block("Tablero", new Vector3(-5.2f, 2.3f, z), new Vector3(5f, 1.7f, .14f), M("Azul noche", new Color(.1f, .12f, .25f)), .06f, false);
        for (int j = 0; j < 4; j++)
        {
            var mat = EscapeKit.Glow(Color.white, .05f);
            boardMats.Add(mat);
            Symbol(transform, sequence[j], new Vector3(-6.95f + j * 1.15f, 2.4f, z - .12f), .6f, mat, Vector3.zero);
            EscapeKit.Text(transform, (j + 1).ToString(), new Vector3(-6.95f + j * 1.15f, 1.72f, z - .1f), EscapeFP.OnExit, .025f, new Color(1f, .92f, .7f));
        }

        // Tapestry on the entrance wall: shape → colour legend for all five shapes.
        float bz = EscapeFP.BackZ + .06f;
        Block("Tapiz", new Vector3(5.6f, 2.1f, bz), new Vector3(7f, 2f, .06f), P(new Color(.42f, .18f, .2f)), .02f, false);
        EscapeKit.Text(transform, "LO QUE LA FORMA ES, EL COLOR LO DICE", new Vector3(5.6f, 2.9f, bz + .06f), EscapeFP.OnBack, .016f, new Color(1f, .9f, .7f));
        var legend = Enumerable.Range(0, 5).OrderBy(_ => Random.value).ToList();
        for (int i = 0; i < 5; i++)
        {
            int s = legend[i];
            float x = 5.6f + 2.6f - i * 1.3f;      // seen from inside the room, mirrored x
            Symbol(transform, s, new Vector3(x, 2.3f, bz + .1f), .45f, P(new Color(.96f, .9f, .8f)), EscapeFP.OnBack);
            EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Color", new Vector3(x, 1.55f, bz + .08f), new Vector3(.42f, .02f, .42f), EscapeKit.Glow(Colors5[colorOfSymbol[s]], .8f), false, new Vector3(90, 0, 0));
        }
        Prop("Cajas", new Vector3(7.4f, 0, -9.2f), 20);
    }

    void Pull(int k, EscapeActor a)
    {
        Touch(a);
        int need = colorOfSymbol[sequence[progress]];
        if (k == need)
        {
            down[k] = true; progress++;
            EscapeAudio.Play("lever", handles[k].position);
            if (progress >= 4) Solve(a);
        }
        else
        {
            for (int i = 0; i < 5; i++) down[i] = false;
            progress = 0;
            EscapeAudio.Play("error", handles[k].position);
            D.Notify("¡Orden incorrecto! Las palancas vuelven a subir");
            a.Stun(.4f);
        }
    }

    public override void Tick()
    {
        lectern.Tick(Team);
        bool show = lectern.Pressed || Solved;
        for (int j = 0; j < boardMats.Count; j++)
            EscapeKit.SetGlow(boardMats[j], show ? Color.white : new Color(.2f, .2f, .25f), show ? (j < progress ? 3f : 1.6f) : .02f);
        for (int k = 0; k < 5; k++)
        {
            angles[k] = Mathf.MoveTowards(angles[k], down[k] || Solved ? 125f : 35f, Time.deltaTime * 420f);
            handles[k].localRotation = Quaternion.Euler(0, 0, sides[k] * angles[k]);
        }
    }

    protected override void OnSolved() => Confetti(W(new Vector3(0, 1.5f, 3f)), Accent, 60);

    public override EscapeOrder Order(EscapeActor a)
    {
        bool humanReading = !D.AutoHuman && lectern.Contains(D.Human);
        int reader = humanReading ? -1 : 1, puller = humanReading ? 1 : 2;
        var need = progress < 4 ? levers[colorOfSymbol[sequence[progress]]] : null;
        if (a.Role == reader)
        {
            // Co-op: the reader may own the lever that comes next: it pulls it and goes back to read.
            if (need != null && need.Mine(a) && Assist(110f)) return EscapeOrder.Use(need);
            return EscapeOrder.Exact(lectern.transform.position);
        }
        if (a.Role == puller)
        {
            if (Assist(110f) && lectern.Pressed && need != null) return EscapeOrder.Use(need);
            return EscapeOrder.Go(W(new Vector3(0, 0, 2f)), .8f);
        }
        return Wait(a);
    }
}
