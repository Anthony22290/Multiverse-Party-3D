using UnityEngine;
public sealed class MortalCodeTerminal : MonoBehaviour
{
    public string label;
    public Vector3 UsePoint => transform.position + transform.forward * 1.8f;
}
