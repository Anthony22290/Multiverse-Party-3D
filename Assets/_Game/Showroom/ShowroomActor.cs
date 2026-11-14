using System.Collections;
using UnityEngine;

/// <summary>Personaje "extra" dentro de un diorama del Probador: elige disfraz (al azar + fijos) y reproduce una animación en bucle.</summary>
public class ShowroomActor : MonoBehaviour
{
    public string state = "Idle";
    [Tooltip("Piezas fijas, p. ej. \"Cabeza=Casco,Torso=Piloto,Kart=Clasico\". El resto se elige al azar.")]
    public string outfit = "";
    public int seed;

    IEnumerator Start()
    {
        yield return null;
        var look = GetComponent<CharacterLook>();
        if (look != null)
        {
            var old = Random.state; Random.InitState(seed);
            look.Randomize();
            Random.state = old;
            foreach (var pair in outfit.Split(','))
            {
                var kv = pair.Split('='); if (kv.Length == 2) look.Set(kv[0].Trim(), kv[1].Trim());
            }
        }
        var anim = GetComponent<Animator>();
        if (anim != null && !string.IsNullOrEmpty(state)) anim.CrossFadeInFixedTime(state, 0f, 0, Random.value);
    }
}
