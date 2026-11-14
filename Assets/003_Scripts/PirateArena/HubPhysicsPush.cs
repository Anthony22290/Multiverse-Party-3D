using UnityEngine;

/// <summary>Lets the hub character push light practice objects.</summary>
public sealed class HubPhysicsPush : MonoBehaviour
{
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.rigidbody;
        if (body == null || body.isKinematic || hit.moveDirection.y < -.3f) return;
        Vector3 direction = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up);
        if (direction.sqrMagnitude > .01f)
            body.AddForceAtPosition(direction.normalized * 2.5f, hit.point, ForceMode.Impulse);
    }
}
