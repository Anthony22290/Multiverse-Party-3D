using UnityEngine;

/// <summary>Returns a practice object if it falls into the sea.</summary>
public sealed class HubPracticeReset : MonoBehaviour
{
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Rigidbody body;
    private void Awake() { body = GetComponent<Rigidbody>(); startPosition = body.position; startRotation = body.rotation; }
    private void FixedUpdate()
    {
        if (body.position.y > -5f) return;
        body.position = startPosition; body.rotation = startRotation;
        body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
    }
}
