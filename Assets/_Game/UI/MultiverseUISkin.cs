using UnityEngine;

/// <summary>Editable UI resources shared by every mode. Lives in Assets/_Game/Resources/MultiverseUISkin.asset.</summary>
[CreateAssetMenu(menuName = "Multiverse/UI Skin")]
public sealed class MultiverseUISkin : ScriptableObject
{
    [Tooltip("Rounded display font for titles and buttons (Kenney Future, CC0, ya incluido en el proyecto).")]
    public Font display;
    [Tooltip("Narrow font for body text and hints.")]
    public Font body;
    public AudioClip click, hover;
}
