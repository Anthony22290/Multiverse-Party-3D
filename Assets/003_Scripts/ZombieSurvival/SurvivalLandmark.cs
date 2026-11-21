using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toy-style landmark buildings of the four sectors (2026-11-17). Prefabs live in Resources/SurvivalLandmarks and are
/// made by ZonasSetup (Multiverse → Zonas → Preparar prefabs) from Assets/002_Modelos/Zonas/Zonas_Zombi.fbx.
/// Every prefab faces +Z, has its base at y = 0 and carries its own box colliders.
/// </summary>
public static class SurvivalLandmark
{
    static readonly Dictionary<string, GameObject> cache = new();

    public static GameObject Place(string key, Transform parent, Vector3 pos, float yaw, bool shell = false)
    {
        if (!cache.TryGetValue(key, out var prefab) || prefab == null) cache[key] = prefab = Resources.Load<GameObject>("SurvivalLandmarks/" + key);
        if (prefab == null) { Debug.LogWarning("Invasión Zombi: falta el edificio " + key + " (Multiverse → Zonas → Preparar prefabs)"); return null; }
        var go = Object.Instantiate(prefab, parent, false);
        go.name = key;
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        // Big buildings: the camera may pass through them instead of zooming onto the player's back.
        if (shell) go.AddComponent<SurvivalShell>();
        return go;
    }
}

/// <summary>Hides a roof (keeping its shadow) while the local player stands under it, so the action stays visible.</summary>
public sealed class SurvivalRoofFade : MonoBehaviour
{
    SurvivalDirector director; Vector2 half; Renderer[] renderers; bool hidden;
    public void Setup(SurvivalDirector d, Vector2 halfSize) { director = d; half = halfSize; renderers = GetComponentsInChildren<Renderer>(); }
    void LateUpdate()
    {
        if (director == null || renderers == null) return;
        var h = director.Human;
        bool under = false;
        if (h != null)
        {
            Vector3 local = transform.InverseTransformPoint(h.transform.position);
            under = Mathf.Abs(local.x) < half.x + .8f && Mathf.Abs(local.z) < half.y + .8f;
        }
        if (under == hidden) return;
        hidden = under;
        foreach (var r in renderers) if (r != null) r.shadowCastingMode = under ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
    }
}
