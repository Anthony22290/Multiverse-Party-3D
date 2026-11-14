using UnityEngine;
public sealed class CrazyKartShortcut : MonoBehaviour
{
    public Vector3 start, end;
    public float halfWidth = 2;
    public float RoadDistance(Vector3 p, float mainHalfWidth)
    {
        p.y=0;Vector3 a=start,b=end;a.y=b.y=0;
        float t=Mathf.Clamp01(Vector3.Dot(p-a,b-a)/Mathf.Max(.01f,(b-a).sqrMagnitude));
        return Vector3.Distance(p,Vector3.Lerp(a,b,t))+mainHalfWidth-halfWidth;
    }
}
