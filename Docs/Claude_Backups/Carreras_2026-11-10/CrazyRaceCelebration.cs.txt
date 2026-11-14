using UnityEngine;
public sealed class CrazyRaceCelebration : MonoBehaviour
{
    CrazyKartDirector director;
    bool celebrated;
    void Start(){director=GetComponent<CrazyKartDirector>();}
    void Update()
    {
        if(celebrated||director==null||!director.Finished)return;
        celebrated=true;
        Vector3 p=director.GetWaypointPosition(0)+Vector3.up*1.5f;
        Color[] colors={Color.cyan,new Color(1,.2f,.45f),Color.yellow,new Color(.4f,1,.35f)};
        for(int i=0;i<4;i++)CrazyRaceBurst.Spawn(p+new Vector3((i-1.5f)*3,0,(i%2)*3),colors[i],true);
    }
}
