using System.Collections.Generic;
using UnityEngine;

// Tower atmosphere (2026-11-10): dust motes drifting around the camera and a gentle flicker on warm
// point lights. Lights changed by puzzles keep working: the flicker follows their new intensity.
public sealed class EscapeAtmosphere : MonoBehaviour
{
    sealed class Flame { public Light light; public float baseIntensity, lastSet, seed; }
    readonly List<Flame> flames = new();
    ParticleSystem dust; float rescanAt;
    void Start()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            dust = CrazyRaceFX.System(cam.transform, "Polvo en suspensión", new Vector3(0, 0, 6), CrazyRaceFX.Glow, 7, .15f, .06f, 300);
            var main = dust.main; main.startColor = new Color(1, .9f, .7f, .35f); main.startSpeed = new ParticleSystem.MinMaxCurve(.05f, .25f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = -.005f;
            var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(14, 7, 12);
            var em = dust.emission; em.rateOverTime = 28;
            var noise = dust.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .3f;
        }
    }
    void Rescan()
    {
        rescanAt = Time.time + 3;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.type != LightType.Point || l.color.r < l.color.b + .15f || flames.Exists(f => f.light == l)) continue;
            flames.Add(new Flame { light = l, baseIntensity = l.intensity, lastSet = l.intensity, seed = Random.value * 50 });
        }
    }
    void Update()
    {
        if (Time.time > rescanAt) Rescan();
        for (int i = flames.Count - 1; i >= 0; i--)
        {
            var f = flames[i]; if (f.light == null) { flames.RemoveAt(i); continue; }
            if (!Mathf.Approximately(f.light.intensity, f.lastSet)) f.baseIntensity = f.light.intensity;   // a puzzle changed it
            float n = Mathf.PerlinNoise(f.seed, Time.time * 3.2f) * .5f + Mathf.PerlinNoise(f.seed + 7, Time.time * 9) * .2f;
            f.light.intensity = f.lastSet = f.baseIntensity * (.82f + n * .36f);
        }
    }
}
