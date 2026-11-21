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

    public const int Arena = 0, Zombie = 1, Race = 2, Mortal = 3, Escape = 4, Rollers = 5, Dig = 6, Ship = 7, Flap = 8, Golf = 9, Potato = 10, Farm = 11, Delivery = 12, Catch = 13, Floor = 14, Paint = 15, Hill = 16, Simon = 17;

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
        new Info { Scene = "ArmaTuNave", Title = "ARMA TU NAVE", Tagline = "Busca y construye",
            Description = "Las 5 piezas de tu nave están escondidas por el pueblo: en las casas, tras los setos, en los carros… Llévalas a tu plataforma y empuja a quien lleve la que te falta.\nGana: la primera nave en despegar.",
            Image = "Nave", Mode = "Búsqueda", Color = new Color(.45f, .8f, 1f) },
        new Info { Scene = "Propulsores", Title = "PROPULSORES", Tagline = "Todos a volar",
            Description = "Con un propulsor a la espalda, volad todos juntos entre las torres: cada torre es un punto y chocar te quita dos.\nGana: el primero en llegar a 30 puntos.",
            Image = "Propulsores", Mode = "Reflejos", Color = new Color(1f, .85f, .3f) },
        new Info { Scene = "MinigolfLoco", Title = "MINIGOLF LOCO", Tagline = "10 hoyos de locura",
            Description = "Diez hoyos con molinos, rampas, puentes y agua. Cuando alguien mete la bola, los demás tienen 1 minuto. Máximo 15 golpes: si llegas, cuentan 15 + 2 de castigo.\nGana: menos golpes en total.",
            Image = "Minigolf", Mode = "Precisión", Color = new Color(.45f, .9f, .45f) },
        new Info { Scene = "PatataCaliente", Title = "PATATA CALIENTE", Tagline = "¡Pásala ya!",
            Description = "Una patata con mecha secreta va de mano en mano: toca a otro para pasársela. Cuando explota, quien la tiene queda fuera y la valla se cierra.\nGana: el último en pie.",
            Image = "Patata", Mode = "Supervivencia", Color = new Color(1f, .6f, .3f) },
        new Info { Scene = "GallinasLocas", Title = "GALLINAS LOCAS", Tagline = "¡Al corral!",
            Description = "Las gallinas huyen de ti: espántalas hacia tu corral o cógelas en brazos. La dorada vale 5… y algunas son trampa y hacen explotar el corral.\nGana: más gallinas en tu corral en 90 s.",
            Image = "Gallinas", Mode = "Caos", Color = new Color(.95f, .85f, .4f) },
        new Info { Scene = "RepartoLoco", Title = "REPARTO LOCO", Tagline = "Cruza la calle",
            Description = "Coge un paquete en el puesto y crúzalo hasta el barrio entre camiones; luego vuelve a por otro. Si te pillan no mueres, pero sales volando y el paquete se rompe.\nGana: más entregas en 90 s.",
            Image = "Reparto", Mode = "Reflejos", Color = new Color(1f, .7f, .3f) },
        new Info { Scene = "Atrapalo", Title = "¡ATRÁPALO!", Tagline = "El más rápido",
            Description = "Un cañón dispara fruta: el primero en pulsar se la queda (+1, la piña dorada +2). ¡Pero si es una bota o una bomba, pierdes una vida! 3 vidas.\nGana: el primero a 10 puntos.",
            Image = "Atrapalo", Mode = "Reflejos", Color = new Color(.85f, .5f, 1f) },
        new Info { Scene = "SueloQueCae", Title = "SUELO QUE SE CAE", Tagline = "¡No te pares!",
            Description = "Tres pisos de baldosas hexagonales que se caen poco después de pisarlas. Muévete, salta los huecos y baja de piso solo cuando no quede otra. Abajo hay lava.\nGana: el último que quede en pie.",
            Image = "SueloQueCae", Mode = "Supervivencia", Color = new Color(1f, .5f, .3f) },
        new Info { Scene = "PinturaLoca", Title = "PINTURA LOCA", Tagline = "Pinta el suelo",
            Description = "Cada baldosa que pisas se pinta de tu color. Coge los cubos para una salpicadura enorme y empuja a los demás para frenarlos.\nGana: quien tenga más suelo pintado a los 90 s.",
            Image = "PinturaLoca", Mode = "Caos", Color = new Color(.4f, .85f, 1f) },
        new Info { Scene = "ReyColina", Title = "REY DE LA COLINA", Tagline = "¡La corona es mía!",
            Description = "Un volcán flotante con tres rampas hasta la cima. El primero que llega pinta la bandera de su color y suma 1 punto por segundo… hasta que otro sube y le quita la bandera. Bombas, empujones y caídas al vacío.\nGana: más tiempo con la bandera en 2 minutos.",
            Image = "ReyColina", Mode = "Empujones", Color = new Color(1f, .82f, .3f) },
        new Info { Scene = "MemoriaMusical", Title = "MEMORIA MUSICAL", Tagline = "Simón dice",
            Description = "El tablero toca una secuencia de colores y notas; repítela con las flechas. Cada ronda una nota más y más rápido. Un fallo o quedarse sin tiempo quita una vida (2 vidas).\nGana: el último con vidas.",
            Image = "MemoriaMusical", Mode = "Memoria", Color = new Color(1f, .55f, .95f) },
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
        "WASD moverte · E coger pieza · Q soltar · F / clic izq. empujar · Espacio saltar · Clic der. o Z/C girar cámara",
        "Espacio, clic izq. o W: propulsor (toques cortos) · pasa entre las torres · chocar resta 2 puntos",
        "Ratón o A/D apuntar (Shift fino) · Mantén clic izq. o Espacio para la fuerza y suelta para golpear · Rueda zoom · Clic der. inclinar cámara · Tab tarjeta",
        "WASD moverte · Espacio saltar · Shift o clic izq. acelerón · toca a otro para pasarle la patata · camas elásticas para escapar",
        "WASD moverte · E o clic izq. coger / soltar gallina · Shift o clic der. empujón (hace soltar la gallina) · Espacio saltar",
        "WASD moverte (W = hacia el barrio) · Shift o clic: acelerón · Espacio saltar · el paquete se coge en el puesto del centro (abajo)",
        "ESPACIO, clic o E: ¡atrapar! · solo fruta y piña dorada · nunca la bota ni la bomba · pulsar antes del disparo = fuera de ese tiro",
        "WASD moverte · Espacio saltar · las baldosas caen al pisarlas: ¡sigue moviéndote! · cuando no quede suelo, baja al piso de abajo",
        "WASD moverte · Espacio saltar · Shift o clic: empujón (frena al otro) · los cubos de pintura salpican una zona grande",
        "WASD moverte · Espacio saltar · E o clic izq. lanzar bomba · Shift o clic der. empujón · si caes al vacío reapareces abajo",
        "↑ rojo · → azul · ↓ verde · ← amarillo (también W D S A, 1 2 3 4 o la cruceta del mando) · primero mira, luego repite",
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
        new[] { "Entra en las casas: las piezas suelen estar detrás de los muebles.", "A los 55 s el radar marca con un haz de luz las piezas que te faltan.", "Si alguien lleva la pieza que te falta, ¡empújalo y se le caerá!" },
        new[] { "Toques cortos: mantener el ritmo es mejor que dar muchos golpes seguidos.", "Mira ya la siguiente torre antes de pasar la actual.", "Tras un choque tienes escudo un momento: aprovéchalo para colocarte." },
        new[] { "La línea de puntería muestra hacia dónde sale la bola: mira las paredes para rebotar.", "En el molino espera a que las aspas dejen el hueco libre.", "Si caes al agua sumas un golpe y vuelves a donde estabas.", "Cuando alguien mete la bola, ¡solo te queda un minuto!" },
        new[] { "Guarda el acelerón para escapar cuando la patata se te acerque.", "Las cajas del centro son buenas para dar vueltas y despistar.", "Al recibir la patata te quedas quieto un momento: quien te la pasó aprovechará para huir.", "Cuanto más roja y rápido hace tic-tac, más cerca está la explosión." },
        new[] { "Las gallinas huyen de ti: rodéalas para empujarlas hacia la puerta de tu corral.", "Mira los ojos: si brillan rojos un instante, es una gallina trampa.", "Si una trampa empieza a pitar en tu corral, cógela con E y tírala lejos… o al corral de otro.", "Un empujón a quien lleva una gallina se la hace soltar." },
        new[] { "Las medianas de césped son seguras: para ahí a mirar el siguiente tramo.", "Mira hacia dónde va cada carril antes de cruzar.", "Los camiones aceleran con el tiempo: ¡aprovecha el principio!", "El acelerón te saca de un apuro en el último segundo." },
        new[] { "Mira bien QUÉ sale antes de pulsar: la bota y la bomba quitan vidas.", "Pulsar antes del disparo te deja fuera de ese tiro.", "La piña dorada vale el doble.", "Tu tiempo de reacción se mide en tu propio ordenador: el lag no cuenta." },
        new[] { "Quedarte quieto es caer: la baldosa se rompe medio segundo después de pisarla.", "No bajes de piso demasiado pronto: arriba hay más sitio.", "Salta los huecos pequeños en vez de rodearlos.", "Si te persiguen, rompe el suelo delante de ellos." },
        new[] { "Pintar sobre el color de otro le quita una baldosa a él y te la da a ti.", "Los cubos salpican mucho: ve a por ellos en cuanto aparezcan.", "Un empujón deja al otro un momento quieto.", "Recorre zonas blancas: valen lo mismo y nadie las defiende." },
        new[] { "La bandera solo cambia si su dueño NO está arriba: ¡échalo!", "Las bombas vuelan en arco: lánzalas desde la terraza hacia la cima.", "En las rampas no hay barandillas: un empujón allí es una caída segura.", "Tu bandera sigue sumando aunque bajes… hasta que otro suba." },
        new[] { "Di los colores en voz baja mientras los ves: se recuerdan mejor.", "No hace falta ir rápido: el tiempo solo cuenta para desempatar.", "Si todos fallan, nadie pierde vida y se repite la ronda.", "Agrupa las notas de tres en tres." },
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
