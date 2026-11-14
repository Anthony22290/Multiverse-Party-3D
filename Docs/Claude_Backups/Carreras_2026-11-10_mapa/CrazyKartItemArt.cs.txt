using UnityEngine;
public static class CrazyKartItemArt
{
    public static readonly string[] Help={
        "Recoge una caja amarilla",
        "Acelera durante 2,5 segundos",
        "Persigue al rival que tengas delante",
        "Deja una trampa detrás del coche",
        "Bloquea un impacto durante 6 segundos",
        "Tres acelerones: úsalos cuando quieras",
        "Un rayo encoge a todos los que van delante"
    };
    public static Color ColorFor(int i)=>new[]{new Color(.55f,.55f,.65f),new Color(1,.7f,.1f),Color.cyan,new Color(.8f,.35f,1),new Color(.3f,1,.6f),new Color(1,.45f,.1f),new Color(1,.95f,.35f)}[i];
    public static Sprite Icon(int item)
    {
        var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);tex.name="Icono objeto "+item;tex.filterMode=FilterMode.Bilinear;
        var pixels=new Color[4096];
        Vector2[] bolt={new Vector2(.53f,.95f),new Vector2(.18f,.45f),new Vector2(.46f,.45f),new Vector2(.35f,.06f),new Vector2(.84f,.62f),new Vector2(.57f,.62f)};
        Vector2[] shield={new Vector2(.18f,.86f),new Vector2(.82f,.86f),new Vector2(.76f,.4f),new Vector2(.5f,.08f),new Vector2(.24f,.4f)};
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            Vector2 p=new Vector2((x+.5f)/64,(y+.5f)/64);float r=Vector2.Distance(p,new Vector2(.5f,.5f));bool fill=false;
            if(item==0)fill=x>12&&x<51&&y>12&&y<51&&(x<16||x>47||y<16||y>47);
            if(item==1)fill=Inside(p,bolt);
            if(item==2)fill=(r>.22f&&r<.34f)||r<.11f;
            if(item==3){float width=(.9f-p.y)*.52f;fill=p.y>.14f&&p.y<.9f&&Mathf.Abs(p.x-.5f)<width;if(Mathf.Abs(p.x-.5f)<.045f&&((p.y>.37f&&p.y<.66f)||(p.y>.23f&&p.y<.3f)))fill=false;}
            if(item==4)fill=Inside(p,shield);
            if(item==5)for(int b=0;b<3;b++){Vector2 o=new Vector2((b-1)*.27f,b==1?.08f:-.06f);Vector2 q=(p-new Vector2(.5f,.5f)-o)*2.2f+new Vector2(.5f,.5f);if(Inside(q,bolt))fill=true;}
            if(item==6){Vector2 c=p-new Vector2(.5f,.68f);bool cloud=Vector2.Distance(p,new Vector2(.36f,.66f))<.15f||Vector2.Distance(p,new Vector2(.55f,.74f))<.19f||Vector2.Distance(p,new Vector2(.72f,.64f))<.13f||(p.y>.52f&&p.y<.66f&&p.x>.26f&&p.x<.8f);
                Vector2 q=new Vector2((p.x-.5f)*1.6f+.5f,(p.y)*1.6f-.02f);fill=cloud||(p.y<.52f&&Inside(q,bolt));}
            pixels[y*64+x]=fill?Color.white:Color.clear;
        }
        tex.SetPixels(pixels);tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,64,64),new Vector2(.5f,.5f),64);
    }
    static bool Inside(Vector2 p,Vector2[] v)
    {
        bool inside=false;
        for(int i=0,j=v.Length-1;i<v.Length;j=i++)
            if((v[i].y>p.y)!=(v[j].y>p.y)&&p.x<(v[j].x-v[i].x)*(p.y-v[i].y)/(v[j].y-v[i].y)+v[i].x)inside=!inside;
        return inside;
    }
}
