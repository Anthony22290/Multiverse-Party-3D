using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// One golfer of Minigolf Loco: a ball (real physics on the owner's computer) and the party character standing next to
/// it with a putter. Aim with the mouse or A/D, hold click/Space to load power (it swings back and forth), release
/// to putt. Water: back to where you hit from, +1 stroke. Fifteen strokes without holing: the ball vanishes.
/// </summary>
public sealed class GolfPlayer : MonoBehaviour
{
    public const float BallRadius = .11f, MaxSpeed = 13f, Damping = .55f, MaxStrokes = 15;
    public enum Phase { Waiting, Ready, Charging, Rolling, Holed, Out }

    public GolfDirector Director { get; private set; }
    public int Slot { get; private set; }
    public int Member { get; private set; }
    public string DisplayName { get; private set; }
    public Color Color { get; private set; }
    public bool IsHuman { get; private set; }
    public bool Puppet { get; private set; }
    public Phase State { get; private set; } = Phase.Waiting;
    public int Strokes { get; private set; }
    public float Aim { get; set; }                 // yaw in degrees
    public float Power { get; private set; }       // 0..1 while charging
    public bool Done => State == Phase.Holed || State == Phase.Out;
    public Transform Ball => ball;
    public Rigidbody Body => rb;
    public bool Celebrating { get; set; }

    // Bot input.
    public float BotAim = float.NaN, BotPower = -1f;
    public readonly NetPose Net = new NetPose();

    Transform ball, character, putter; Rigidbody rb; Animator anim; string animState = "";
    Vector3 lastRest; float stillFor, chargeT, sinkT, swingUntil; bool sinking;
    LineRenderer aimLine; Transform ring;

    public void Init(GolfDirector d, int slot, int member, string name, Color color, bool human, bool puppet, GameObject prefab, int lookSeed)
    {
        Director = d; Slot = slot; Member = member; DisplayName = name; Color = color; IsHuman = human; Puppet = puppet;
        // Ball.
        var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "Bola de " + name; ball = b.transform;
        ball.localScale = Vector3.one * BallRadius * 2f;
        b.GetComponent<Renderer>().sharedMaterial = EscapeKit.Paint(Color.Lerp(color, Color.white, .25f), .6f);
        var col = b.GetComponent<SphereCollider>();
        col.sharedMaterial = new PhysicsMaterial("Bola") { bounciness = .45f, dynamicFriction = .35f, staticFriction = .4f, bounceCombine = PhysicsMaterialCombine.Average };
        rb = b.AddComponent<Rigidbody>(); rb.mass = .5f; rb.linearDamping = Damping; rb.angularDamping = 2f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.interpolation = RigidbodyInterpolation.Interpolate;
        if (puppet) { rb.isKinematic = true; col.enabled = false; }
        ring = EscapeKit.Shape(PrimitiveType.Cylinder, null, "Marca de " + name, Vector3.zero, new Vector3(.5f, .005f, .5f), MultiverseFX.Glow(color, .45f, 2f), false).transform;

        // Character with a putter.
        character = new GameObject("Golfista " + name).transform;
        if (prefab != null)
        {
            var go = Instantiate(prefab, character, false); go.name = "Fiestero";
            var bb = PirateArenaVisuals.BoundsOf(go); go.transform.localScale *= 1.2f / Mathf.Max(.1f, bb.size.y);
            bb = PirateArenaVisuals.BoundsOf(go); go.transform.position += new Vector3(0, -bb.min.y, 0);
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
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Mano_")) r.gameObject.SetActive(false);
            Transform hand = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) { string n = t.name.ToLowerInvariant(); if (n.Contains("hand") && n.EndsWith("r")) { hand = t; break; } }
            putter = new GameObject("Palo").transform;
            if (hand != null) { putter.SetParent(hand, false); putter.position = hand.position; putter.rotation = character.rotation * Quaternion.Euler(170f, 0, 0); putter.localScale = Vector3.one / Mathf.Max(.01f, hand.lossyScale.x); }
            else { putter.SetParent(character, false); putter.localPosition = new Vector3(-.3f, .5f, .2f); }
            var metal = EscapeKit.Paint(new Color(.8f, .82f, .88f), .6f);
            EscapeKit.Block(putter, "Vara", new Vector3(0, .38f, 0), new Vector3(.035f, .76f, .035f), metal, .01f, false);
            EscapeKit.Block(putter, "Cabeza", new Vector3(.06f, .78f, 0), new Vector3(.18f, .06f, .07f), EscapeKit.Paint(color, .4f), .02f, false);
            EscapeKit.Block(putter, "Puño", new Vector3(0, .02f, 0), new Vector3(.05f, .14f, .05f), EscapeKit.Paint(new Color(.15f, .15f, .17f)), .01f, false);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        }
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var nt = new GameObject("Nombre").AddComponent<TextMesh>(); nt.transform.SetParent(character, false); nt.transform.localPosition = Vector3.up * 1.55f;
        nt.font = font; nt.GetComponent<MeshRenderer>().sharedMaterial = font.material; nt.text = human ? name + " (tú)" : name;
        nt.anchor = TextAnchor.MiddleCenter; nt.alignment = TextAlignment.Center; nt.fontSize = 48; nt.characterSize = .03f; nt.color = color; nt.fontStyle = FontStyle.Bold;
        nameTag = nt.transform;

        if (human)
        {
            aimLine = new GameObject("Mira").AddComponent<LineRenderer>();
            aimLine.sharedMaterial = CrazyRaceFX.Glow; aimLine.positionCount = 2; aimLine.widthMultiplier = .06f; aimLine.startColor = new Color(1, 1, 1, .9f); aimLine.endColor = new Color(1, 1, 1, 0f);
        }
        Hide(true);
    }
    Transform nameTag; Renderer[] bodyRenderers; bool bodyShown = true;

    void OnDestroy() { if (ring != null) Destroy(ring.gameObject); if (aimLine != null) Destroy(aimLine.gameObject); if (character != null) Destroy(character.gameObject); if (ball != null) Destroy(ball.gameObject); }

    // ------------------------------------------------------------------ hole flow (every computer, for its own golfers)

    public void StartHole(GolfCourse.Hole h)
    {
        Strokes = 0; sinking = false; Celebrating = false;
        var tee = h.Tee + TeeOffset();
        lastRest = tee;
        Place(tee);
        Vector3 to = (h.Path.Count > 1 ? h.Path[1] : h.Cup) - h.Tee; to.y = 0; Aim = Quaternion.LookRotation(to.sqrMagnitude > .01f ? to : Vector3.forward).eulerAngles.y;
        State = Phase.Ready; Hide(false); Net.Clear();
    }
    public void StartHolePuppet(GolfCourse.Hole h)
    {
        Strokes = 0; sinking = false; Celebrating = false; State = Phase.Ready;
        Net.Clear(); ball.position = h.Tee + TeeOffset(); Hide(false); PlaceCharacter(true);
    }
    public void Freeze() { if (!Done) State = Phase.Waiting; if (!rb.isKinematic) rb.linearVelocity = Vector3.zero; }
    Vector3 TeeOffset() => new Vector3(((Slot % 3) - 1) * .35f, 0, (Slot / 3) * -.35f);

    void Place(Vector3 at)
    {
        if (!Puppet) { rb.isKinematic = true; ball.position = at; rb.position = at; rb.isKinematic = false; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        else ball.position = at;
        PlaceCharacter(true);
    }

    void Hide(bool hidden)
    {
        ball.gameObject.SetActive(!hidden); ring.gameObject.SetActive(!hidden); character.gameObject.SetActive(!hidden || Celebrating);
        if (aimLine != null) aimLine.enabled = false;
    }

    // ------------------------------------------------------------------ owner

    void Update()
    {
        if (Director == null) return;
        if (Puppet) { PuppetTick(); Present(); return; }
        var h = Director.CurrentHole;
        bool playing = Director.State == GolfDirector.Phase.Playing && h != null;
        if (playing && (State == Phase.Ready || State == Phase.Charging))
        {
            if (IsHuman && GetComponent<GolfBot>() == null) HumanInput();
            else if (BotPower >= 0f) { if (!float.IsNaN(BotAim)) Aim = BotAim; Shoot(BotPower); BotPower = -1f; BotAim = float.NaN; }
        }
        Present();
    }

    void HumanInput()
    {
        if (!Director.InputFree) return;
        var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current;
        float turn = 0f;
        if (k != null) turn += ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0);
        if (g != null) turn += g.leftStick.ReadValue().x;
        bool fine = k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
        Aim += turn * (fine ? 25f : 90f) * Time.deltaTime;
        if (m != null && !m.rightButton.isPressed) Aim += m.delta.ReadValue().x * .12f * MultiverseSettings.Sensitivity * (fine ? .3f : 1f);
        bool hold = (k != null && k.spaceKey.isPressed) || (m != null && m.leftButton.isPressed) || (g != null && (g.buttonSouth.isPressed || g.rightTrigger.isPressed));
        if (State == Phase.Ready && hold) { State = Phase.Charging; chargeT = 0f; }
        if (State == Phase.Charging)
        {
            chargeT += Time.deltaTime;
            Power = Mathf.PingPong(chargeT / .9f, 1f);
            if (!hold) Shoot(Mathf.Max(.04f, Power));
            if (k != null && k.escapeKey.wasPressedThisFrame) { State = Phase.Ready; Power = 0f; }
        }
    }

    public void Shoot(float power)
    {
        if (Done || Director.State != GolfDirector.Phase.Playing) return;
        Strokes++; Power = 0f;
        lastRest = ball.position;
        Vector3 dir = Quaternion.Euler(0, Aim, 0) * Vector3.forward;
        rb.WakeUp(); rb.linearVelocity = dir * MaxSpeed * Mathf.Clamp01(power);
        State = Phase.Rolling; stillFor = 0f;
        swingUntil = Time.time + .5f; animState = ""; Play("Attack");
        GolfAudio.Play("putt", ball.position, IsHuman ? 1f : .4f);
    }

    void FixedUpdate()
    {
        if (Puppet || Director == null || State != Phase.Rolling) return;
        var h = Director.CurrentHole; if (h == null) return;
        Vector3 v = rb.linearVelocity; Vector3 p = rb.position;
        // A little rolling resistance so slow balls settle instead of creeping forever.
        if (v.magnitude < 1.2f && Physics.Raycast(p, Vector3.down, BallRadius + .06f)) rb.linearVelocity = v * .975f;
        // Cup.
        Vector3 toCup = h.Cup - p; toCup.y = 0;
        if (toCup.magnitude < GolfCourse.CupRadius && v.magnitude < 4.6f && Mathf.Abs(p.y - (h.CupY + BallRadius)) < .25f) { Holed(h); return; }
        // Water or off the course.
        if (p.y < h.Origin.y - .45f || (p.y < h.CupY - .2f && GolfCourse.IsWater(h, p)))
        {
            Strokes++;
            GolfAudio.Play("splash", p, IsHuman ? 1f : .4f);
            MultiverseFX.Burst(p + Vector3.up * .2f, new Color(.4f, .7f, 1f), 14, 4);
            if (IsHuman) Director.Notify("¡Al agua! +1 golpe");
            Place(lastRest); Settle(); return;
        }
        if (v.magnitude < .12f) { stillFor += Time.fixedDeltaTime; if (stillFor > .3f) Settle(); }
        else stillFor = 0f;
    }

    void Settle()
    {
        rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        if (Strokes >= MaxStrokes) { GiveUp(); return; }
        State = Phase.Ready; lastRest = ball.position;
        PlaceCharacter(false);
    }

    void Holed(GolfCourse.Hole h)
    {
        State = Phase.Holed; sinking = true; sinkT = 0f;
        rb.isKinematic = true; ball.position = h.Cup + Vector3.up * BallRadius;
        GolfAudio.Play("cup", ball.position, 1f);
        MultiverseFX.Rainbow(h.Cup + Vector3.up * .6f, 26, 5);
        string word = Strokes == 1 ? "¡HOYO EN UNO!" : Strokes - h.Par <= -2 ? "¡ÁGUILA!" : Strokes - h.Par == -1 ? "¡BIRDIE!" : Strokes == h.Par ? "PAR" : Strokes - h.Par == 1 ? "BOGEY" : Strokes + " golpes";
        MultiverseFX.FloatText(h.Cup + Vector3.up * 1.6f, word, Color, 1.2f, 1.6f);
        Director.OnHoled(this, word);
    }

    void GiveUp()
    {
        State = Phase.Out;
        MultiverseFX.Burst(ball.position + Vector3.up * .2f, new Color(.7f, .7f, .7f), 12, 3);
        GolfAudio.Play("poof", ball.position, IsHuman ? 1f : .4f);
        Hide(true);
        Director.OnGaveUp(this);
    }

    // ------------------------------------------------------------------ network puppets

    Phase netState; int netStrokes;
    public void ApplyNet(Vector3 pos, float aim, Phase state, int strokes)
    {
        Net.Add(pos, Quaternion.identity); Aim = aim; netStrokes = strokes;
        if (state != netState)
        {
            if (state == Phase.Holed && Director.CurrentHole != null) { MultiverseFX.Rainbow(Director.CurrentHole.Cup + Vector3.up * .6f, 20, 4); GolfAudio.Play("cup", pos, .5f); Director.OnHoled(this, ""); }
            if (state == Phase.Out) { Hide(true); Director.OnGaveUp(this); }
            if (state == Phase.Rolling && netState != Phase.Rolling) { swingUntil = Time.time + .5f; animState = ""; Play("Attack"); }
            if (state == Phase.Ready && netState == Phase.Rolling) PlaceCharacter(false);
            netState = state;
        }
        State = state; Strokes = strokes;
    }
    public void ResetNet() { netState = Phase.Waiting; }

    void PuppetTick()
    {
        if (Net.Get(out var pos, out _)) ball.position = pos;
        if (State == Phase.Holed) ball.position = Vector3.Lerp(ball.position, Director.CurrentHole != null ? Director.CurrentHole.Cup - Vector3.up * .1f : ball.position, Time.deltaTime * 4f);
    }

    // ------------------------------------------------------------------ presentation

    void PlaceCharacter(bool snap)
    {
        Vector3 dir = Quaternion.Euler(0, Aim, 0) * Vector3.forward, right = Vector3.Cross(Vector3.up, dir);
        Vector3 at = ball.position - Vector3.up * BallRadius - right * .55f - dir * .05f;
        if (!snap && character.gameObject.activeSelf) SurvivalKit.Puff(character.position + Vector3.up * .2f, new Color(.95f, .95f, .9f), 3, .15f);
        character.SetPositionAndRotation(at, Quaternion.LookRotation(right));
    }

    void Present()
    {
        if (sinking) { sinkT += Time.deltaTime; ball.position += Vector3.down * Time.deltaTime * .5f; if (sinkT > .4f) { sinking = false; ball.gameObject.SetActive(false); ring.gameObject.SetActive(false); } }
        if (ball.gameObject.activeSelf)
        {
            ring.position = ball.position - Vector3.up * (BallRadius - .01f);
            ring.localScale = new Vector3(.5f + Mathf.Sin(Time.time * 4f) * .05f, .005f, .5f + Mathf.Sin(Time.time * 4f) * .05f);
        }
        bool aiming = State == Phase.Ready || State == Phase.Charging;
        if (aiming && character.gameObject.activeSelf)
        {
            Vector3 dir = Quaternion.Euler(0, Aim, 0) * Vector3.forward, right = Vector3.Cross(Vector3.up, dir);
            character.rotation = Quaternion.Slerp(character.rotation, Quaternion.LookRotation(right), Time.deltaTime * 10f);
            character.position = Vector3.Lerp(character.position, ball.position - Vector3.up * BallRadius - right * .55f - dir * .05f, Time.deltaTime * 10f);
        }
        if (aimLine != null)
        {
            aimLine.enabled = aiming && Director.State == GolfDirector.Phase.Playing;
            if (aimLine.enabled)
            {
                Vector3 dir = Quaternion.Euler(0, Aim, 0) * Vector3.forward;
                float len = 1.2f + (State == Phase.Charging ? Power * 3.5f : 0f);
                aimLine.SetPosition(0, ball.position + Vector3.up * .02f); aimLine.SetPosition(1, ball.position + Vector3.up * .02f + dir * len);
                var c = State == Phase.Charging ? Color.Lerp(new Color(.4f, 1f, .4f), new Color(1f, .3f, .2f), Power) : Color.white;
                aimLine.startColor = c; aimLine.endColor = new Color(c.r, c.g, c.b, 0f);
            }
        }
        var camT = Camera.main != null ? Camera.main.transform : null;
        if (nameTag != null && camT != null) nameTag.rotation = camT.rotation;
        if (camT != null && character.gameObject.activeSelf)
        {
            // Other golfers right in front of the camera would hide your shot: fade them out (hide) while close.
            if (bodyRenderers == null) bodyRenderers = character.GetComponentsInChildren<Renderer>(true);
            bool mine = Director.Human == this;
            float cd = (character.position + Vector3.up * .6f - camT.position).magnitude;
            bool show = (mine ? cd > 1.4f : cd > 2.6f) || Director.State != GolfDirector.Phase.Playing;
            if (show != bodyShown) { bodyShown = show; foreach (var r in bodyRenderers) if (r != null) r.enabled = show; }
            if (nameTag != null) nameTag.gameObject.SetActive(!mine && cd > 3.2f);
        }
        string s = Celebrating ? "Victory" : Time.time < swingUntil ? "Attack" : State == Phase.Holed ? "Dance" : "Idle";
        Play(s);
    }

    void Play(string s) { if (anim == null || s == animState) return; animState = s; anim.CrossFadeInFixedTime(s, s == "Attack" ? .03f : .15f); }
    public void ShowForResults(bool show) { character.gameObject.SetActive(show); }
}
