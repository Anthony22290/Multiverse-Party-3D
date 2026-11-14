using System; using System.Linq; using System.Reflection; using System.Text; using System.Threading.Tasks;
using UnityEngine; using UnityEngine.InputSystem; using UnityEngine.InputSystem.LowLevel; using Object = UnityEngine.Object;
public static class RaceDriftProbe
{
    static async Task Wait(float s){float e=Time.time+s;var d=DateTime.UtcNow.AddSeconds(s*4+4);while(Time.time<e&&DateTime.UtcNow<d)await Task.Delay(15);}
    public static async Task<string> Run()
    {
        var d=Object.FindAnyObjectByType<CrazyKartDirector>();var p=d.Karts.First(k=>k.IsHuman);var body=p.GetComponent<Rigidbody>();
        foreach(var b in d.Karts.Where(k=>!k.IsHuman))b.gameObject.SetActive(false);
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var kb=InputSystem.AddDevice<Keyboard>();var sb=new StringBuilder();
        typeof(CrazyKartDirector).GetField("countdown",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(d,0f);
        try{
            p.CurrentCheckpoint=0;p.Recover();await Wait(.6f);
            InputSystem.QueueStateEvent(kb,new KeyboardState(Key.W));await Wait(1.2f);
            InputSystem.QueueStateEvent(kb,new KeyboardState(Key.W,Key.D,Key.LeftShift));
            for(int i=0;i<12;i++){await Wait(.08f);var v=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up);
                if(p.Speed<10)foreach(var c in Physics.OverlapBox(body.position,new Vector3(1.6f,.8f,2.2f),body.rotation,~0,QueryTriggerInteraction.Collide))if(c.attachedRigidbody!=body)sb.Append("  near: "+c.name+" parent="+(c.transform.parent?c.transform.parent.name:"-")+" trigger="+c.isTrigger+"\n");
                sb.Append($"pos={body.position} t={i*.08f:F2} drift={p.Drifting} nitro={p.NitroAmount:F0} speed={p.Speed:F1} fwd={Vector3.Dot(v,p.transform.forward):F1} steer={p.Steering:F2} stun={p.Stunned} grounded={p.Grounded} boost={p.Boosting}\n");}
        } finally{InputSystem.QueueStateEvent(kb,new KeyboardState());InputSystem.RemoveDevice(kb);foreach(var b in d.Karts)b.gameObject.SetActive(true);}
        return sb.ToString();
    }
}
