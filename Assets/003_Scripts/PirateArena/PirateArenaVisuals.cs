using UnityEngine;

public static class PirateArenaVisuals
{
    public static Bounds BoundsOf(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    public static void ApplyMaterial(GameObject root, Material material)
    {
        if (material == null) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }
    }

    public static GameObject AddModel(GameObject prefab, Transform parent, float height, Material material, bool centered)
    {
        if (prefab == null) return null;
        // Keep the FBX import rotation and scale. Resetting them tips Z-up source assets onto their side.
        GameObject visual = Object.Instantiate(prefab, parent, false);
        Bounds bounds = BoundsOf(visual);
        visual.transform.localScale *= height / Mathf.Max(0.001f, bounds.size.y);
        bounds = BoundsOf(visual);
        Vector3 anchor = centered ? bounds.center : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        visual.transform.position += parent.position - anchor;
        ApplyMaterial(visual, material);
        return visual;
    }
}
