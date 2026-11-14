using UnityEngine;
public sealed class CrazyKartCamera : MonoBehaviour
{
    public CrazyKartController target;
    [SerializeField] float distance = 8, height = 4.5f, followSmoothTime = .18f;
    Vector3 velocity, heading;
    Camera view;
    float observedRecovery=-100, observedHit=-100, shake;
    void Awake() { view = GetComponent<Camera>(); }
    void LateUpdate()
    {
        if (target == null) return;
        bool recovering=target.LastRecoveryTime>observedRecovery;
        observedRecovery=target.LastRecoveryTime;
        if (heading.sqrMagnitude < .1f || recovering) heading = target.transform.forward;
        heading = Vector3.Slerp(heading,target.transform.forward,1-Mathf.Exp(-6*Time.deltaTime));
        Vector3 focus=target.transform.position+Vector3.up*1.1f;
        Vector3 desired=target.transform.position-heading*distance+Vector3.up*height;
        bool snap=recovering||Vector3.Distance(transform.position,desired)>24;
        Vector3 offset=desired-focus;
        float obstruction=offset.magnitude;
        foreach(var hit in Physics.SphereCastAll(focus,.28f,offset.normalized,offset.magnitude,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.collider.GetComponentInParent<CrazyKartController>()!=null)continue;
            obstruction=Mathf.Min(obstruction,Mathf.Max(.8f,hit.distance-.18f));
        }
        desired=focus+offset.normalized*obstruction;
        if(snap){transform.position=desired;velocity=Vector3.zero;transform.rotation=Quaternion.LookRotation(focus-transform.position);}
        else transform.position=Vector3.SmoothDamp(transform.position,desired,ref velocity,followSmoothTime);
        // Resolve obstruction immediately; easing only outward avoids passing through walls.
        if(Vector3.Distance(transform.position,focus)>obstruction && obstruction<offset.magnitude)
            transform.position=focus+(transform.position-focus).normalized*obstruction;
        Vector3 look=focus+heading*Mathf.Lerp(.3f,2.2f,Mathf.Clamp01(target.Speed/25));
        transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(look-transform.position),1-Mathf.Exp(-10*Time.deltaTime));
        if(target.LastHitTime>observedHit){observedHit=target.LastHitTime;shake=.5f;}
        if(target.LastStruckTime>observedHit){observedHit=target.LastStruckTime;shake=.7f;}
        if(shake>0){transform.position+=Random.insideUnitSphere*shake*.35f;shake=Mathf.MoveTowards(shake,0,Time.deltaTime*1.4f);}
        view.fieldOfView=Mathf.Lerp(view.fieldOfView,target.Boosting?74:Mathf.Lerp(62,68,Mathf.Clamp01(target.Speed/25)),1-Mathf.Exp(-3*Time.deltaTime));
    }
}

