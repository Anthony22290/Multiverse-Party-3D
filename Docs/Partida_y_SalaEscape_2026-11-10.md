# Partida (fiesta a 5 puntos) y Sala de Escape en primera persona — 2026-11-10

## Flujo de partida
- Escena **MenuPrincipal** (la primera del juego): nombre, crear sala en línea (4/5/6 jugadores), unirse con código, jugar sin conexión.
- `Assets/_Game/Party/MultiverseParty.cs` (DontDestroyOnLoad): miembros 4–6 (bots rellenan), votación de 3 minijuegos al azar (todos votan, empate al azar, bots votan solos), +1 punto solo al ganar un minijuego, campeón al llegar a 5, vuelta automática a la plaza.
- En línea con Photon Fusion: `MultiversePartyNet` replica el estado (RPC de unirse, votar, posición en la plaza y resultado). Los minijuegos aún corren en local con bots; cada cliente informa su resultado y el anfitrión decide.
- `PartyGames.cs`: catálogo con imagen (`Resources/PartyCards`) y descripción.
- Si abres un minijuego directamente en el editor, funciona como antes (con sus menús, 6 jugadores, sin volver a la plaza).
- Editor: `Multiverse/Partida/Preparar todo` (prefab de red, imágenes, plaza, menú, Build Settings).

## Mundo de Inicio
- Plaza circular con 3 portales de votación (pantalla con imagen, título, orbes de votos). Pisar el círculo vota; también teclas 1-3 o las cartas de la pantalla.
- Torre de puntos (un pilar por jugador con 5 discos y corona del líder) y podio del campeón al sur de la plaza.
- Jugadores visibles con nombre y color; los bots caminan al portal que votan y suben al podio al final.

## Minijuegos integrados (sin menús propios durante la partida)
| Minijuego | Jugadores | Gana el punto |
|---|---|---|
| Arena del Caos | 4–6 | último pirata en pie |
| Carreras Locas | 4–6 (se clonan karts) | primero en la meta |
| Código Mortal | 4–6 | último en pie (o más eliminaciones al acabar el tiempo) |
| Invasión Zombi | 4–6 | el mejor jugador si el equipo escapa |
| Sala de Escape | 4–6 | quien resolvió más salas si el equipo escapa |

## Sala de Escape
- Primera persona: ratón para mirar, mira central, E usa lo que apuntas, H pide pista (−30 s).
- Salas de 22 × 20 m con muros de 3,6 m y vigas.
- **Torre de 11 salas** por partida (unos 30 min): 8 nuevas al azar de 10 + 2 clásicas de 4 + portal final.
- Pistas vagas: la 1.ª a los 100 s y la 2.ª a los 200 s. Los bots esperan 110–150 s antes de resolver por ti.
- Salas nuevas:
  1. Vestíbulo del Reloj: hora vista en espejo.
  2. Campanario: melodía con la partitura partida.
  3. Galería de Espejos: rayo de luz y un espejo señuelo.
  4. Gabinete del Cifrado: César; las velas encendidas dan el desplazamiento.
  5. Taller del Rompecabezas: deslizante 3×3 con plano.
  6. **Muro de los Faroles**: lights out 4×4.
  7. **Fuente de las Medidas**: jarras sin marcas; la cantidad viene en números romanos.
  8. **Sala de las Tuberías**: girar tubos del grifo al molino.
  9. **Observatorio de los Números**: series; la I está en el techo, la II escondida tras una columna y la III es un engaño.
  10. **Pasillo de las Huellas**: suelo con trampas; el farol revela las huellas.
- Clásicas renovadas: Sala de los Símbolos (formas → colores con tapiz, palanca señuelo), Biblioteca, Puente de Baldosas, Cripta de las Antorchas. Portal final.

## Pruebas (2026-11-10)
- `EscapePlaytest` (bots):
  - Las 5 salas nuevas + rompecabezas + portal: PASS.
  - Clásicas: PASS.
  - Torre completa de 11 salas con 6 jugadores: PASS.
- `PartyPlaytest.FullMatch`: menú → plaza → 7 rondas → campeón a 5 puntos: PASS, ningún menú propio visible en los minijuegos.
- Regresión:
  - Carreras con 6 karts: PASS.
  - Código Mortal: 23/23.
  - Arena: 15/15.
  - Invasión Zombi con 4 bots: 1 victoria y 2 derrotas. Una derrota fue en la oleada 8 por los lanzabombas (dificultad real); la otra, en la oleada 2, no se repitió.
- Capturas en `Docs/Claude_Capturas/Partida` y `Docs/Claude_Capturas/Escape/PrimeraPersona`.

## Pendiente
- Red real dentro de cada minijuego (por fases).
