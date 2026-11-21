using UnityEngine;

/// <summary>Catalogue of the minigames the party can vote for.</summary>
public static class PartyGames
{
    public struct Info
    {
        public string Scene, Title, Tagline, Description, Image, Mode;
        public Color Color;
        public bool Coop;
    }

    public const int Arena = 0, Zombie = 1, Race = 2, Mortal = 3, Escape = 4, Rollers = 5, Dig = 6;

    public static readonly Info[] All =
    {
        new Info { Scene = "ArenaDelCaos", Title = "ARENA DEL CAOS", Tagline = "Todos contra todos",
            Description = "Empuja, agarra y lanza a los demás al agua desde la cubierta del barco. Cuidado con los cañones y el kraken.\nGana: el último pirata en pie.",
            Image = "Arena", Mode = "Combate", Color = new Color(1f, .36f, .58f) },
        new Info { Scene = "InvasionZombi", Title = "INVASIÓN ZOMBI", Tagline = "Cooperativo · roguelike",
            Description = "Oleadas de zombis, esqueletos y lanzabombas. Busca las piezas que custodian los monstruos y repara la camioneta.\nGana: el mejor jugador si el equipo escapa.",
            Image = "Zombie", Mode = "Supervivencia", Color = new Color(.5f, 1f, .38f), Coop = true },
        new Info { Scene = "CarrerasLocas", Title = "CARRERAS LOCAS", Tagline = "3 vueltas al Gran Premio",
            Description = "Derrapa, usa objetos y encuentra los atajos del circuito más grande del multiverso.\nGana: el primero en cruzar la meta.",
            Image = "Race", Mode = "Carrera", Color = new Color(1f, .8f, .18f) },
        new Info { Scene = "CodigoMortal", Title = "CÓDIGO MORTAL", Tagline = "Memoria y sigilo",
            Description = "Lee los códigos de la plaza, apúntalos y tecléalos en la terminal correcta antes que nadie.\nGana: el último superviviente.",
            Image = "Mortal", Mode = "Supervivencia", Color = new Color(.32f, 1f, .86f) },
        new Info { Scene = "SalaDeEscape", Title = "SALA DE ESCAPE", Tagline = "Primera persona · puzles",
            Description = "Once salas en primera persona: relojes, campanas, espejos, cifrados, tuberías… Nadie te dirá la respuesta.\nGana: quien más puzles resuelva si escapáis.",
            Image = "Escape", Mode = "Puzles", Color = new Color(.74f, .55f, 1f), Coop = true },
        new Info { Scene = "RodillosDelCalabozo", Title = "RODILLOS DEL CALABOZO", Tagline = "Salta y agáchate",
            Description = "Dos rodillos con pinchos giran sobre la lava: salta el de fuego, agáchate bajo el de hielo. Cada vez más rápido… y el suelo se rompe.\nGana: el último en pie.",
            Image = "Rodillos", Mode = "Esquivar", Color = new Color(1f, .5f, .2f) },
        new Info { Scene = "PicoPalaTijera", Title = "PICO, PALA Y TIJERA", Tagline = "Carrera hacia abajo",
            Description = "Cava hacia el tesoro: pala para la tierra, pico para la piedra y tijera para las hojas. Elige bien con 1, 2 y 3… si fallas, te mareas.\nGana: el primero en llegar al tesoro.",
            Image = "Mina", Mode = "Reflejos", Color = new Color(.95f, .75f, .25f) },
    };

    public static int Count => All.Length;

    /// <summary>Controls shown on the loading screen.</summary>
    public static readonly string[] Controls =
    {
        "WASD moverte · Ratón mirar · Clic izq./der. agarrar con cada mano · E golpear · Shift levantar · F lanzar · Espacio saltar",
        "WASD moverte · Clic izq./J golpear · E mantener (instalar pieza / reanimar) · Q soltar pieza · Clic der. o Z/C girar cámara",
        "WASD o flechas conducir · Shift derrapar (mantén para el turbo) · Espacio/F usar objeto · R volver a la pista",
        "WASD moverte · Espacio saltar · Clic der. cámara · E terminal · Q protector · Lee los códigos de los demás",
        "WASD moverte · Ratón mirar · E usar lo que apuntas (mantén) · Q soltar · H pedir pista (−30 s)",
        "WASD moverte · Espacio o clic izq. SALTAR (rodillo de fuego) · Shift, Ctrl o clic der. AGACHARSE (rodillo de hielo) · Q/E girar cámara",
        "1 PALA (tierra) · 2 PICO (piedra) · 3 TIJERA (hojas) · Clic izq. o Espacio cavar · Rueda cambiar herramienta",
    };

    /// <summary>Tips shown on the loading screen (one at random).</summary>
    public static readonly string[][] Tips =
    {
        new[] { "Agarra con las dos manos para levantar a un rival y lánzalo por la borda.", "Cuando el kraken golpea la cubierta, aléjate de los tentáculos.", "Colgado del borde puedes subir con Shift: ¡no te rindas!" },
        new[] { "Las piezas que custodian los monstruos valen la pena: no esperes a que caigan solas.", "Un compañero derribado se reanima manteniendo E cerca de él.", "Los lanzabombas avisan antes de lanzar: muévete en cuanto veas la sombra." },
        new[] { "Derrapa más tiempo para cambiar el color de las chispas: rosa es el turbo más fuerte.", "Los atajos ahorran mucho, pero alguno termina en caída.", "Acelera justo cuando se apaga la última luz para una salida perfecta." },
        new[] { "Apunta los códigos que veas: tu libreta los guarda.", "Si alguien te mira demasiado, usa el protector.", "En muerte súbita los códigos se leen desde más lejos." },
        new[] { "Nadie te dará la respuesta: observa toda la sala, también arriba y detrás de ti.", "Las pistas llegan solas con el tiempo; pedirlas antes cuesta 30 segundos.", "Habla con tu equipo: muchas salas necesitan que uno lea y otro actúe." },
        new[] { "Cerca del centro los rodillos pasan más despacio: es más fácil saltarlos.", "Cuando una losa se pone roja, sal de ella: va a caer a la lava.", "Si los dos rodillos llegan juntos, primero salta y luego agáchate." },
        new[] { "Mira el bloque de abajo antes de hacer clic: un fallo te marea.", "La piedra dura (azul oscura) necesita dos golpes de pico.", "El oro se rompe con cualquier herramienta." },
    };

    public static string Tip(int game) => game >= 0 && game < Tips.Length ? Tips[game][Random.Range(0, Tips[game].Length)] : "";

    /// <summary>Selectable characters (shared pirate rigs).</summary>
    public static readonly string[] Characters = { "Anne", "Captain_Barbarossa", "Henry", "Mako", "Skeleton" };
    public static readonly string[] CharacterNames = { "Anne", "Barbarroja", "Henry", "Mako", "Huesitos" };
    public static string ModelName(int character) => Characters[Mathf.Abs(character) % Characters.Length];

    public static int FromScene(string scene)
    {
        for (int i = 0; i < All.Length; i++) if (All[i].Scene == scene) return i;
        return -1;
    }

    static readonly System.Collections.Generic.Dictionary<int, Texture2D> images = new System.Collections.Generic.Dictionary<int, Texture2D>();
    public static Texture2D Image(int game)
    {
        if (game < 0 || game >= All.Length) return null;
        if (!images.TryGetValue(game, out var tex) || tex == null)
        {
            tex = Resources.Load<Texture2D>("PartyCards/" + All[game].Image);
            images[game] = tex;
        }
        return tex;
    }
}
