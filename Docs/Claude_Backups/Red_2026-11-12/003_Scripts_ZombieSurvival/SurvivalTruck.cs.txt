using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The broken pickup: starts up on cinder blocks, smoking, with a 10-light progress board. Every installed part
/// shows on it (wheels lower it, the radiator stops the smoke, the headlights turn on). At the end the team boards
/// the bed and it drives out through the north gate.
/// </summary>
public sealed class SurvivalTruck : MonoBehaviour
{
    [Tooltip("Turns the pack model so its front faces +Z (north gate).")] public float modelYaw = 0f;

    public Vector3 ZoneCenter => transform.position;
    SurvivalDirector director;
    SurvivalLevel level;
    Transform body, model;
    readonly List<Material> pips = new();
    readonly List<Transform> seats = new();
    Material zoneMat;
    TextMesh boardLabel, zoneLabel;
    Light[] headlights;
    int progress;
    bool ready, boarded;
    float smokeAt, startZ;
    float bodyLift = .42f;

    public void Build(SurvivalDirector d, SurvivalLevel owner)
    {
        director = d; level = owner;
        startZ = transform.position.z;
        body = new GameObject("Carrocería").transform;
        body.SetParent(transform, false);
        var go = SurvivalKit.Model("Vehicle_Pickup", body, Vector3.zero, 0f, 1.9f, false, "Modelo");
        if (go != null)
        {
            model = go.transform;
            var b = PirateArenaVisuals.BoundsOf(go);
            float yaw = (b.size.x > b.size.z ? 90f : 0f) + modelYaw;
            model.localRotation = Quaternion.Euler(0, yaw, 0);
            b = PirateArenaVisuals.BoundsOf(go);
            model.position += new Vector3(transform.position.x - b.center.x, 0, transform.position.z - b.center.z);
        }
        else
        {
            model = EscapeKit.Block(body, "Camioneta", Vector3.up * .9f, new Vector3(2.2f, 1.4f, 5f), EscapeKit.Paint(new Color(.85f, .4f, .2f)), .2f, false).transform;
        }
        // One solid box for the whole truck (static, so the controller and zombies slide around it).
        var col = gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 1f, 0); col.size = new Vector3(2.3f, 2f, 5.1f);
        gameObject.AddComponent<SurvivalShell>();

        // Up on cinder blocks until the wheels are back.
        body.localPosition = Vector3.up * bodyLift;
        foreach (var p in new[] { new Vector3(-.85f, 0, 1.6f), new Vector3(.85f, 0, 1.6f), new Vector3(-.85f, 0, -1.6f), new Vector3(.85f, 0, -1.6f) })
            if (SurvivalKit.Model("CinderBlock", transform, p, 0f, .42f, false, "Bloque") == null)
                EscapeKit.Block(transform, "Bloque", p + Vector3.up * .21f, new Vector3(.4f, .42f, .3f), EscapeKit.Paint(new Color(.6f, .6f, .58f)), .03f, false);

        // Repair zone ring.
        zoneMat = EscapeKit.Glow(new Color(1f, .8f, .25f), .5f);
        float r = SurvivalLevel.ZoneRadius * 2f;
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Zona de reparación", Vector3.up * .03f, new Vector3(r, .01f, r), EscapeKit.Paint(new Color(.24f, .24f, .2f)), false);
        // Slightly wider and lower than the dark disc, so only its rim shows.
        EscapeKit.Shape(PrimitiveType.Cylinder, transform, "Borde de la zona", Vector3.up * .02f, new Vector3(r + .4f, .008f, r + .4f), zoneMat, false);
        zoneLabel = EscapeKit.Text(transform, "ZONA DE REPARACIÓN", new Vector3(0, .06f, -SurvivalLevel.ZoneRadius + .7f), new Vector3(90, 0, 0), .045f, new Color(1f, .85f, .35f));

        // Progress board beside the truck.
        var board = new GameObject("Tablero").transform;
        board.SetParent(transform, false);
        board.localPosition = new Vector3(3.2f, 0, -1.4f);
        board.localRotation = Quaternion.Euler(0, -25f, 0);
        var wood = EscapeKit.Mat("Madera oscura", new Color(.35f, .22f, .14f));
        EscapeKit.Block(board, "Poste", new Vector3(0, 1.1f, 0), new Vector3(.18f, 2.2f, .18f), wood, .03f, false);
        EscapeKit.Block(board, "Panel", new Vector3(0, 2.2f, 0), new Vector3(2.4f, 1.15f, .12f), EscapeKit.Paint(new Color(.12f, .14f, .16f)), .05f, false);
        boardLabel = EscapeKit.Text(board, "CAMIONETA 0/10", new Vector3(0, 2.58f, -.08f), Vector3.zero, .03f, new Color(1f, .9f, .6f));
        for (int i = 0; i < 10; i++)
        {
            var m = EscapeKit.Glow(new Color(.9f, .2f, .2f), .5f);
            pips.Add(m);
            EscapeKit.Shape(PrimitiveType.Sphere, board, "Luz " + (i + 1), new Vector3(-.92f + (i % 5) * .46f, i < 5 ? 2.25f : 1.9f, -.08f), Vector3.one * .26f, m, false);
        }

        // Headlights (off until the "Faros" part) and seats in the bed.
        headlights = new Light[2];
        for (int i = 0; i < 2; i++)
        {
            var l = new GameObject("Faro").AddComponent<Light>();
            l.transform.SetParent(body, false);
            l.transform.localPosition = new Vector3(i == 0 ? -.7f : .7f, .9f, 2.6f);
            l.transform.localRotation = Quaternion.Euler(12f, 0, 0);
            l.type = LightType.Spot; l.range = 22f; l.spotAngle = 55f; l.intensity = 6f; l.color = new Color(1f, .95f, .75f);
            l.enabled = false;
            headlights[i] = l;
        }
        for (int i = 0; i < 4; i++)
        {
            var s = new GameObject("Asiento " + (i + 1)).transform;
            s.SetParent(body, false);
            s.localPosition = new Vector3(i % 2 == 0 ? -.5f : .5f, 1.0f, i < 2 ? -1.0f : -1.9f);
            s.localRotation = Quaternion.Euler(0, i % 2 == 0 ? -90f : 90f, 0);
            seats.Add(s);
        }
        SetProgress(0);
    }

    public void SetProgress(int installed)
    {
        progress = installed;
        for (int i = 0; i < pips.Count; i++)
            EscapeKit.SetGlow(pips[i], i < installed ? new Color(.35f, 1f, .45f) : new Color(.9f, .2f, .2f), i < installed ? 2f : .5f);
        if (boardLabel != null) boardLabel.text = $"CAMIONETA {installed}/10";
        bodyLift = installed >= 2 ? 0f : installed == 1 ? .2f : .42f;
        foreach (var l in headlights) if (l != null) l.enabled = installed >= 7;
        if (installed > 0) SurvivalKit.Puff(transform.position + Vector3.up * 1.3f, new Color(1f, .9f, .5f), 8, .5f);
    }

    public void SetReady()
    {
        ready = true;
        EscapeKit.SetGlow(zoneMat, new Color(.35f, 1f, .45f), 1.6f);
        if (zoneLabel != null) { zoneLabel.text = "¡TODOS AQUÍ!"; zoneLabel.color = new Color(.5f, 1f, .55f); }
        foreach (var l in headlights) if (l != null) l.enabled = true;
    }

    public void Board(List<SurvivalPlayer> team)
    {
        boarded = true;
        // The repair zone, board and cinder blocks stay behind on the street.
        foreach (Transform child in new List<Transform>(GetComponentsInChildren<Transform>(true)))
            if (child != null && child.parent == transform && child != body && child.GetComponent<Light>() == null) child.SetParent(transform.parent, true);
        for (int i = 0; i < team.Count && i < seats.Count; i++) team[i].Board(seats[i]);
        foreach (var b in level.GateBlockers) if (b != null && b.transform.position.z > SurvivalLevel.Half) b.enabled = false;
        GetComponent<BoxCollider>().enabled = false;
    }

    public void Drive(float t)
    {
        if (!boarded) return;
        float z = startZ + .5f * 7f * Mathf.Max(0f, t - .6f) * Mathf.Max(0f, t - .6f);
        var p = transform.position; p.z = Mathf.Min(z, 140f);
        transform.position = p;
        if (Time.time > smokeAt && t > .6f) { smokeAt = Time.time + .08f; SurvivalKit.Puff(transform.position - transform.forward * 2.7f + Vector3.up * .5f, new Color(.5f, .5f, .5f), 3, .35f); }
    }

    void Update()
    {
        if (body == null) return;
        Vector3 target = Vector3.up * bodyLift;
        body.localPosition = Vector3.MoveTowards(body.localPosition, target, Time.deltaTime * .8f);
        if (ready && model != null) model.localPosition = new Vector3(0, Mathf.Sin(Time.time * 55f) * .02f, 0) + Vector3.Scale(model.localPosition, new Vector3(1, 0, 1));
        if (zoneMat != null && !ready) EscapeKit.SetGlow(zoneMat, new Color(1f, .8f, .25f), .5f + Mathf.PingPong(Time.time, .6f));
        // Steam from the engine until the radiator is in.
        if (progress < 5 && !ready && Time.time > smokeAt && director != null && director.Fighting)
        {
            smokeAt = Time.time + .45f;
            SurvivalKit.Puff(transform.position + transform.forward * 1.9f + Vector3.up * (1.4f + bodyLift), new Color(.75f, .75f, .78f), 3, .45f);
        }
    }
}
