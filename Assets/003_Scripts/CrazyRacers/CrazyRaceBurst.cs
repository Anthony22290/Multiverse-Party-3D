using UnityEngine;
public sealed class CrazyRaceBurst : MonoBehaviour
{
    Material material;
    public static void Spawn(Vector3 position,Color color,bool celebration)
    {
        var go=new GameObject(celebration?"Confeti de llegada":"Destello objeto");go.transform.position=position;
        var owner=go.AddComponent<CrazyRaceBurst>();
        var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        owner.material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        owner.material.SetColor("_BaseColor",color);
        var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=.2f;
        main.startLifetime=celebration?2.8f:.55f;main.startSpeed=celebration?8:3;
        main.startSize=celebration?.14f:.12f;main.gravityModifier=celebration?1:.2f;
        main.simulationSpace=ParticleSystemSimulationSpace.World;main.startColor=color;
        var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)(celebration?65:24))});
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=celebration?30:70;shape.radius=.2f;
        go.transform.rotation=Quaternion.Euler(-90,0,0);
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=owner.material;
        ps.Play();Destroy(go,celebration?3.2f:.9f);
    }
    void OnDestroy(){if(material!=null)Destroy(material);}
}
