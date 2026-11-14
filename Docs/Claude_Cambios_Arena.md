# Arena del Caos: física, sonido y presentación (Claude, 4 de noviembre de 2026)

No se descargaron ni compraron recursos. Todo el audio nuevo es **sintetizado y original**, generado por `Tools/generate_pirate_audio_v2.py`. Los controles, la autoridad de red, los personajes, los rigs y las animaciones no cambian. No se generó build.

## Sonido

`Tools/generate_pirate_audio_v2.py` crea 44 clips (44,1 kHz) en `Assets/003_Scripts/PirateArena/Resources/PirateAudio/v2/`. Para regenerarlos o retocarlos: `python Tools/generate_pirate_audio_v2.py`.

- **Acciones con varias versiones** (se elige una al azar y se varía el tono): golpe (`punch`), impacto de cuerpo (`impact`), madera (`wood`), barrido de brazo/lanzamiento (`whoosh`), agarre, salto, aterrizaje, paso, 2 chapuzones.
- **Peligros:** mecha chisporroteante con dos avisos de campanilla, disparo de cañón, explosión con chasquidos y astillas, aviso del tiburón (burbujas y rumor grave) y su salto con mordisco.
- **Ronda:** pitido de cuenta atrás, campana de barco al empezar ("¡AL ABORDAJE!"), golpe cómico de KO con pajaritos, fanfarria de victoria y "trombón triste" al ser eliminado.
- **Ambiente:** mar en bucle sin cortes, crujidos de madera bajo la cubierta, gaviotas a lo lejos.
- **Música:** una giga marinera original en re dórico (cuerda pulsada, bajo, acordeón y tambor), en bucle. **M** la activa o desactiva y se recuerda entre sesiones; baja de volumen durante la fanfarria.

`PirateArenaAudio` conserva su API y su enum (los valores nuevos se añaden al final). Ahora tiene 24 voces, niveles de mezcla por sonido y vuelve a los clips antiguos si falta alguno nuevo. Los pasos CC0 de Kenney siguen igual.

## Efectos visuales y cámara

- `PirateArenaFx`: los efectos se disparan con los mismos eventos de sonido. Por eso también se ven en los clientes en línea, que ya reproducen esos sonidos. Incluye polvo al aterrizar e impactar, chispas al golpear, salpicadura con espuma al caer al agua, rocío del tiburón, fogonazo y humo del cañón, explosión con humo, astillas y polvo, y confeti al ganar. Son partículas sin colisión ni fuerzas.
- **Temblor de cámara** con atenuación por distancia: explosión fuerte, disparo medio, golpe o impacto leve.
- `PirateArenaPresentation`:
  - Estrellas giratorias y sonido de KO sobre quien queda aturdido mucho tiempo.
  - Estelas cuando un pirata o un barril sale volando.
  - Cuenta atrás con sonido y campana de salida.
  - Fanfarria y confeti para el ganador.
  - Post-procesado suave (bloom en faroles y fuego, un poco más de saturación y contraste, viñeta ligera).
- Materiales de partículas en `Assets/_Game/Resources/ArenaFx/` (menú `Multiverse › Arena › Crear materiales de efectos`).

## Física

Ajustable en `Settings/ArenaBalance` → `PirateArenaSettings`.

| Cambio | Detalle |
|---|---|
| Empujón con carrera (`pushRunUpBonus`, 0,3) | Golpear corriendo hacia el rival da hasta +30 % de fuerza. |
| Retroceso (`pushRecoil`, 0,9) | Quien acierta un golpe pierde algo de velocidad (más si venía corriendo): acción y reacción. |
| Flotación al caer al agua | Los eliminados flotan unos 3 s en la superficie y luego se hunden. Antes caían en línea recta. Solo afecta a cuerpos ya eliminados. |
| Golpes de madera | Barriles y cubos suenan (y levantan polvo) al chocar con fuerza contra algo. |

## HUD

La barra de controles se ensanchó para que el texto quepa con la fuente nueva y ahora incluye "M música". Las etiquetas "Aturdido"/"KO" sobre los personajes están centradas y no se cortan.

## Archivos modificados

`PirateArenaAudio.cs` (reescrito, misma API), `PirateFighter.cs` (empujón, retroceso, flotación), `PirateArenaCamera.cs` (temblor), `PirateArenaDirector.cs` (getters `Countdown`/`Finished` y HUD), `GrabbableProp.cs` (sonido de madera), `PirateArenaSettings.cs` (2 parámetros).

Nuevos: `Assets/_Game/Arena/` (`PirateArenaFx`, `PirateArenaPresentation`, `Editor/ArenaFxSetup`), `Resources/PirateAudio/v2/`, `Resources/ArenaFx/`, `Tools/generate_pirate_audio_v2.py` y las pruebas `Assets/_Game/Editor/Playtests/Arena*.cs`.

## Pruebas en Play

| Prueba | Resultado |
|---|---|
| `PirateHybridPlaytest` (salto, caída, KO, agarres, lanzamiento, resistencia, ganador, empate, reinicio) | 15/15 PASS |
| `PiratePolishPlaytest` (entradas reales W/Espacio/E/ratón/Shift/F, caídas inclinadas, bot que lanza por la borda) | 11/11 PASS |
| `PirateMapPlaytest` (rampas, laterales, bases) | 39/39 PASS |
| Decorado sin colisiones | PASS |
| Rondas con 6 bots y menú cerrado (`ArenaRoundDiag`) | 3 rondas limpias terminadas en 11,2 s, 14,9 s y 17,0 s; antes, con y sin retroceso, 18–22 s (sin diferencia apreciable) |
| Flotación de eliminados | Entre 0,6 y 2,6 s después de caer, los cuerpos quedan entre −1,6 y +0,4 m. Tras una caída muy alta pueden hundirse hasta −3,5 m antes de salir a flote |
| Efectos en pantalla (`ArenaFxShowcase`) | Capturas `Docs/Claude_Capturas/ArenaFx_*.png` |

**Fallos que ya existían:** `PiratePhysicsPlaytest` ("un tripulante quieto no desliza") y `PirateRoundPlaytest` ("ronda en 70 s") fallan también con los scripts originales, comprobado restaurándolos. Medido por separado, un tripulante quieto se desplaza 0,04–0,18 m en 2 s, así que el desliz de esa prueba viene de su montaje (cuerpos eliminados alrededor del centro). Los informes vigentes del proyecto usan las pruebas híbrida y de pulido.

## Pendiente

- **Escuchar la mezcla.** Los sonidos se revisaron por forma de onda y espectrograma (sin saturación ni chasquidos), pero el volumen relativo debe ajustarse de oído: los niveles están en `PirateArenaAudio.Level`.
- **Probar en línea.** Las chispas de golpe dependen del anfitrión, y en los clientes los golpes se ven con el polvo de impacto.
- **Valorar con jugadores reales** la sensación del empujón con carrera y el retroceso.

## Personajes: postura, animación y movimiento

**Problema medido.** El personaje visible es un muñeco físico que copia la animación. Sus "músculos" eran pares de fuerza calculados a mano con un tope muy bajo (unos 16 N·m en la cabeza), así que la gravedad ganaba. `Assets/_Game/Editor/Playtests/ArenaPuppetDiag.cs` mide el ángulo medio entre cada hueso del muñeco y el de la animación:

| Situación | Antes | Ahora |
|---|---|---|
| Quieto | 61° (cabeza colgando ~130° y 23 cm más baja, torso 67° fuera de su pose) | **1,7°** |
| Andando | 84° | **6,7°** |
| Corriendo | 85° | **15°** (las piernas van 1–2 fotogramas por detrás del ciclo rápido) |

Capturas antes/después: `Docs/Claude_Capturas/Personaje_antes_*.png` y `Personaje_despues_*.png`. Datos: `Docs/Claude_Pruebas/Personajes_Postura.txt`.

**Cambios** (`PirateActivePuppet.cs`, `PirateFighterAnimation.cs`):
- **Músculos nuevos.** Se usan los motores internos de las articulaciones de PhysX (*Slerp drive*) hacia la pose animada (incluido el IK de las manos). Al ser implícitos aguantan mucha rigidez sin inestabilidad y, al ser internos, no empujan al personaje. La rigidez va por zonas: torso y cuello firmes para una silueta clara, brazos más sueltos para que golpes y agarres sigan viéndose físicos, y piernas firmes para el ciclo de paso.
- **Límites de las articulaciones** ampliados en brazos y piernas para que puedan alcanzar las poses de agarre, levantamiento y carrera.
- **Tono muscular progresivo.** Al quedar aturdido el cuerpo se relaja rápido (caída blanda) y al recuperarse recupera la firmeza en unos 0,35 s, en lugar de enderezarse de golpe.
- **Capa procedural sobre los clips.** El personaje se inclina hacia delante al acelerar y correr, hacia dentro en las curvas y hacia atrás al levantar a alguien. Su cabeza sigue al rival más cercano que tenga delante (hasta 7 m y 40°).

**Pruebas tras el cambio:** híbrida 15/15, pulido 11/11 (saltos, caídas de 3/8/12 m sin rebote, agarre con dos manos, lanzamientos) y rondas de 6 bots terminadas (por ejemplo en 10,5 s). Capturas en ronda: `Personajes_ronda_*.png`.

**Pendiente:** ajustar a gusto con jugadores reales la rigidez por zona (valores en `PirateActivePuppet.Initialize`: más alto, más firme; más bajo, más "Gang Beasts").

## Tripulación blandita (personajes originales) — 2026-11-05

Look original de "muñecos de trapo piratas" para la Arena (inspirado en el género de brawlers físicos, sin copiar ni extraer nada de otros juegos).

- `Assets/_Game/Arena/PirateCrewStyle.cs`: construye en runtime mallas suaves (elipsoides, cápsulas, toros) pegadas a los huesos del títere activo. 6 estilos: tricornio, pañuelo, bicornio de capitán, calvo con arete, turbante, gorro de cocinero; barba, parche, rayas, cinturón con hebilla, manos de manopla y botas. Oculta los renderers originales.
- El esqueleto, las animaciones, la física (PirateActivePuppet) y la red NO cambian: solo es la capa visual.
- `PirateFighter.SetTint` ahora multiplica el color propio de cada pieza (`PirateCrewPart.baseColor`) en vez de pisarlo con blanco.
- Interruptor: `PirateArenaSettings.softCrew` (true por defecto). En false vuelven los modelos originales.
- `PirateArenaPresentation`: confeti a prueba de nulos si el mapa aún no existe.
- Capturas: `Docs/Claude_Capturas/Tripulacion_grupo.png` y `Tripulante_0..5.png` (generadas con `ArenaCrewLineup.Run`).
- Pruebas: PirateHybridPlaytest 15/15, PiratePolishPlaytest 11/11.
