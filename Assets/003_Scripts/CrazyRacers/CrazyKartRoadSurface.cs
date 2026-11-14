using UnityEngine;
// Explicit drivable surface; collider uses the closed road volume.
public sealed class CrazyKartRoadSurface : MonoBehaviour
{
    public Vector3[] centers;
    public float[] halfWidths;
    public bool loop;
    public bool Closest(Vector3 point,out Vector3 onRoad,out float edgeDistance)=>Closest(point,out onRoad,out edgeDistance,out _);
    // beyondEnd: the point lies past an open end of the piece (over a jump gap, not under the road).
    public bool Closest(Vector3 point,out Vector3 onRoad,out float edgeDistance,out bool beyondEnd)
    {
        beyondEnd=false;
        onRoad=Vector3.zero;edgeDistance=float.MaxValue;
        if(centers==null||centers.Length<2)return false;
        int count=loop?centers.Length:centers.Length-1;
        float nearest=float.MaxValue;
        for(int i=0;i<count;i++){
            int j=(i+1)%centers.Length;
            Vector3 a=centers[i],b=centers[j],flat=b-a;flat.y=0;
            Vector3 delta=point-a;delta.y=0;
            float raw=Vector3.Dot(delta,flat)/Mathf.Max(.01f,flat.sqrMagnitude),t=Mathf.Clamp01(raw);
            Vector3 q=Vector3.Lerp(a,b,t),distance=point-q;distance.y=0;
            float width=halfWidths!=null&&halfWidths.Length==centers.Length?Mathf.Lerp(halfWidths[i],halfWidths[j],t):6;
            float edge=distance.magnitude-width;
            float metric=Mathf.Max(0,edge)+Mathf.Abs(point.y-q.y)*.015f;
            if(metric<nearest){nearest=metric;onRoad=q;edgeDistance=edge;beyondEnd=!loop&&((i==0&&raw<0)||(i==count-1&&raw>1));}
        }
        return true;
    }
}
