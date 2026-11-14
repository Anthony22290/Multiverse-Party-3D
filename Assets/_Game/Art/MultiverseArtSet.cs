using UnityEngine;

/// <summary>Named prefab references a runtime-built map can load from Resources (Arena del Caos).</summary>
[CreateAssetMenu(menuName = "Multiverse/Art Set")]
public sealed class MultiverseArtSet : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string key;
        public GameObject prefab;
        [Tooltip("Imported pirate-pack model: apply the shared atlas and fit to `height`.")]
        public bool atlas;
        public float height;
    }

    public Entry[] entries;
    public Material atlas;

    public bool TryGet(string key, out Entry entry)
    {
        if (entries != null)
            foreach (var e in entries)
                if (e.key == key && e.prefab != null) { entry = e; return true; }
        entry = default; return false;
    }
}
