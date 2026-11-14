using UnityEngine;
public static class ArenaProbe
{
    public static string Run()
    {
        var d = Object.FindAnyObjectByType<PirateArenaDirector>();
        if (d == null) return "sin director";
        string s = "art=" + (d.Art != null) + " map=" + (d.Art != null && d.Art.Map != null) + " fighters=" + (d.Fighters != null ? d.Fighters.Count : -1);
        s += " piezas=" + Object.FindObjectsByType<PirateCrewPart>().Length + " estilos=" + Object.FindObjectsByType<PirateCrewStyle>().Length;
        return s;
    }
}
