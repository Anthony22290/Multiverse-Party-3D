using UnityEngine;

// Runtime track dressing for Carreras Locas (presentation only, no colliders):
// rainbow item boxes, animated boost-pad chevrons, start lights over the grid and camera speed lines.
public sealed class CrazyRaceDressing : MonoBehaviour
{
    CrazyKartDirector director;
    Transform lights; Renderer[] lamps; Material[] lampMats; Vector3 lightsBase;
    Material lampOff, padMat, boxMat;
    ParticleSystem speedLines; CrazyKartCamera cam;
    float goTime = -1;

    void Start()
    {
        director = GetComponent<CrazyKartDirector>();
        boxMat = CrazyRaceFX.Fresnel(Color.white, true, .5f, 1.6f);
        foreach (var box in FindObjectsByType<CrazyKartItemBox>(FindObjectsSortMode.None))
        {
            var r = box.GetComponent<Renderer>(); if (r != null) { r.sharedMaterial = boxMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            var mark = box.transform.Find("?");
            if (mark != null && box.transform.Find("? atrás") == null)
            {
                var back = Instantiate(mark.gameObject, box.transform); back.name = "? atrás";
                back.transform.localPosition = new Vector3(mark.localPosition.x, mark.localPosition.y, -mark.localPosition.z);
                back.transform.localRotation = mark.localRotation * Quaternion.Euler(0, 180, 0);
            }
        }
        var padShader = Shader.Find("RaceFX/BoostChevrons");
        if (padShader != null)
        {
            padMat = new Material(padShader) { name = "Placa turbo animada" };
            foreach (var pad in FindObjectsByType<CrazyKartBoostPad>(FindObjectsSortMode.None)) { var r = pad.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = padMat; }
        }
        BuildStartLights();
        cam = FindAnyObjectByType<CrazyKartCamera>();
        if (cam != null)
        {
            speedLines = CrazyRaceFX.System(cam.transform, "Líneas de velocidad", new Vector3(0, 0, 16), CrazyRaceFX.Additive, .35f, 46, .12f, 200, true);
            speedLines.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var main = speedLines.main; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.startColor = new Color(1, 1, 1, .55f);
            var shape = speedLines.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 7; shape.radiusThickness = .25f;
            var r = speedLines.GetComponent<ParticleSystemRenderer>(); r.velocityScale = .05f; r.lengthScale = 2;
        }
    }

    void BuildStartLights()
    {
        Vector3 a = director.GetWaypointPosition(0), b = director.GetWaypointPosition(1);
        Vector3 dir = b - a; dir.y = 0; dir.Normalize();
        Vector3 at = a + dir * 17;
        float ground = at.y;
        foreach (var hit in Physics.RaycastAll(at + Vector3.up * 10, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore))
            if (hit.collider.GetComponent<CrazyKartRoadSurface>() != null) { ground = hit.point.y; break; }
        lights = new GameObject("Semáforo de salida").transform;
        lights.SetParent(transform, false);
        lights.position = new Vector3(at.x, ground + 5.2f, at.z);
        lights.rotation = Quaternion.LookRotation(-dir);
        lightsBase = lights.position;
        var frame = Part(PrimitiveType.Cube, lights, Vector3.zero, new Vector3(4.6f, 1.7f, .5f), new Color(.12f, .08f, .22f));
        Part(PrimitiveType.Cube, frame.transform.parent, new Vector3(0, 1.05f, 0), new Vector3(5f, .35f, .7f), new Color(.95f, .2f, .45f));
        lampOff = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.2f, .2f, .25f) };
        lamps = new Renderer[3]; lampMats = new Material[3];
        for (int i = 0; i < 3; i++)
        {
            var lamp = Part(PrimitiveType.Sphere, lights, new Vector3((i - 1) * 1.45f, 0, .32f), Vector3.one * 1.15f, Color.white);
            lamps[i] = lamp.GetComponent<Renderer>();
            lampMats[i] = CrazyRaceFX.Fresnel(Color.red, false, .95f, 1.2f);
        }
    }
    GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type); DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>(); r.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        return go;
    }

    void Update()
    {
        if (director == null) return;
        // Start lights: red lamps fill in on 3-2-1, all green on GO, then the gantry flies away.
        if (lights != null)
        {
            float c = director.Countdown;
            int lit = c > 3 ? 0 : c > 2 ? 1 : c > 1 ? 2 : 3;
            bool go = director.Playing || director.Finished;
            if (go && goTime < 0) goTime = Time.time;
            for (int i = 0; i < 3; i++)
            {
                bool on = go || i < lit;
                var m = lampMats[i]; m.SetColor("_Color", go ? new Color(.2f, 1, .35f) : Color.red);
                lamps[i].sharedMaterial = on ? m : lampOff;
            }
            if (goTime >= 0)
            {
                float t = Time.time - goTime - 1.2f;
                if (t > 0) lights.position = lightsBase + Vector3.up * t * t * 9;
                if (t > 2.5f) lights.gameObject.SetActive(false);
            }
        }
        if (speedLines != null && cam != null && cam.target != null)
        {
            var e = speedLines.emission; e.rateOverTime = cam.target.Boosting && !cam.target.HasFinished ? 90 : cam.target.Speed > 26 ? 15 : 0;
        }
    }
    void OnDestroy()
    {
        foreach (var m in new[] { boxMat, padMat, lampOff }) if (m != null) Destroy(m);
        if (lampMats != null) foreach (var m in lampMats) if (m != null) Destroy(m);
    }
}
