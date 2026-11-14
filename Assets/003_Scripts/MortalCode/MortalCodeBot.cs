using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class MortalCodeBot : MonoBehaviour
{
    MortalCodeActor actor;
    readonly Dictionary<MortalCodeActor,string> memory=new();
    float thinkAt,submitAt,roamUntil;
    MortalCodeTerminal terminal;
    public void Learn(MortalCodeActor target,string code){if(target!=null&&code!=null)memory[target]=code;}
    void Start(){actor=GetComponent<MortalCodeActor>();thinkAt=Time.time+Random.Range(.2f,1.2f);}
    void Update()
    {
        if(actor==null||!actor.Alive||actor.Agent==null||!actor.Agent.enabled||!actor.Agent.isOnNavMesh)return;
        var d=actor.Director;
        actor.Agent.isStopped=!d.Running;
        if(!d.Running||Time.time<thinkAt)return;
        thinkAt=Time.time+.35f;
        foreach(var rival in d.Actors)
        {
            var code=d.Observe(actor,rival);
            if(code!=null){memory[rival]=code;if(actor.ProtectorAvailable && Vector3.Distance(transform.position,rival.transform.position)<7 && Random.value<.08f)actor.ActivateProtector();}
        }
        var known=memory.Keys.FirstOrDefault(a=>a.Alive && !a.Protected);
        if(known!=null && !actor.Protected)
        {
            terminal=d.NearestTerminal(transform.position);
            if(d.CanUse(actor,terminal))
            {
                actor.Agent.isStopped=true;
                if(submitAt==0)submitAt=Time.time+3.5f;
                if(Time.time>=submitAt && Time.time>=actor.LockedUntil){d.Submit(actor,terminal,memory[known],out _);submitAt=0;}
            }
            else{submitAt=0;actor.Agent.SetDestination(terminal.UsePoint);}
        }
        else if(NearPickup(out var pick)){submitAt=0;actor.Agent.SetDestination(pick);}
        else if(Time.time>roamUntil || (!actor.Agent.pathPending && actor.Agent.remainingDistance<1))
        {
            submitAt=0;roamUntil=Time.time+Random.Range(5,10);
            var p=d.patrolPoints[Random.Range(0,d.patrolPoints.Length)].position;
            actor.Agent.SetDestination(p);
        }
    }
    // With no target known, detour to a pickup within 16 m.
    bool NearPickup(out Vector3 at)
    {
        at=Vector3.zero;float best=16*16;bool found=false;
        foreach(var p in FindObjectsByType<MortalCodePickup>(FindObjectsSortMode.None)){float d=(p.transform.position-transform.position).sqrMagnitude;if(d<best){best=d;at=p.transform.position;found=true;}}
        return found;
    }
}
