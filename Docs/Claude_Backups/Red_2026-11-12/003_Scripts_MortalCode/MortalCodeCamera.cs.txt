using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
public sealed class MortalCodeCamera : MonoBehaviour
{
    public MortalCodeDirector Director;
    public float Yaw { get; private set; }
    float pitch=18;
    int spectator;
    bool snap=true;
    public void Snap()=>snap=true;
    float shake;
    public void Shake(float amount)=>shake=Mathf.Max(shake,amount);
    void LateUpdate()
    {
        if(Director==null||Director.Human==null)return;
        var target=Director.Human;
        var k=Keyboard.current;
        if(!target.Alive)
        {
            var living=Director.Actors.Where(a=>a.Alive).ToArray();
            if(k!=null && k.tabKey.wasPressedThisFrame){spectator++;snap=true;}
            if(living.Length>0)target=living[spectator%living.Length];
        }
        if(Mouse.current!=null && Mouse.current.rightButton.isPressed && !Director.HUD.TerminalOpen && !Director.HUD.MenuOpen)
        {var delta=Mouse.current.delta.ReadValue()*MultiverseSettings.Sensitivity;Yaw+=delta.x*.16f;pitch=Mathf.Clamp(pitch-delta.y*.13f,-10,65);}
        var origin=target.Eye;
        var rotation=Quaternion.Euler(pitch,Yaw,0);var back=rotation*Vector3.back;
        float distance=5.5f;
        foreach(var hit in Physics.SphereCastAll(origin,.2f,back,distance,~0,QueryTriggerInteraction.Ignore))
            if(!hit.transform.IsChildOf(target.transform))distance=Mathf.Min(distance,Mathf.Max(.35f,hit.distance-.15f));
        // Immediate collision correction prevents interpolation through walls and bridge decks.
        var desired=origin+back*distance;
        transform.position=snap||distance<5.4f?desired:Vector3.Lerp(transform.position,desired,1-Mathf.Exp(-16*Time.deltaTime));
        transform.rotation=rotation; snap=false;
        if(shake>0){transform.position+=Random.insideUnitSphere*shake*.25f;shake=Mathf.MoveTowards(shake,0,Time.deltaTime*1.5f);}
        var cam=GetComponent<Camera>();cam.nearClipPlane=.08f;cam.farClipPlane=250;cam.fieldOfView=65;
    }
}
