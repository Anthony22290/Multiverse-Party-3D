# Arena del Caos — cubierta pirata

Escena: `Assets/001_Scenes/ArenaDelCaos.unity`. El mapa se construye al entrar en Play.
Si la escena estaba abierta durante los cambios, salir de Play y volver a cargarla desde el panel Project.
Aceptar Reload si Unity detecta cambios externos; la escena guardada contiene las nuevas referencias de materiales.

## Distribución

El barco original se conserva, con su textura Atlas_Pirate y su orientación de importación.
Un objeto padre alinea la proa con +Z; el eje vertical queda nivelado. La posición se calcula
con la superficie real de la cubierta, sin utilizar la altura del mástil como altura del suelo.

| Zona | Tamaño o posición | Uso |
| --- | --- | --- |
| Barco completo, incluido el bauprés | 44 m de longitud | Doble longitud respecto al primer prototipo |
| Cubierta jugable | Aproximadamente 35 × 12,9 m | Espacio para seis tripulantes |
| Patio central | Z −10,35 a +5,59 m; altura 3 m | Aparición de jugadores, empujones, escotilla y carga |
| Castillo de proa | Z +5,59 a +17,25 m; altura 4,51 m | Dos cañones automáticos, anclas y faroles |
| Alcázar de popa | Z −17,68 a −10,35 m; altura 5,65 m | Timón, cofres y faroles |
| Cuatro rampas | Dos hacia proa y dos hacia popa; ancho 2,4 m | Conexiones sin escalón entre cubiertas |
| Bordas | Barandas bajas, postes y cuerda; huecos laterales | Agarrarse, escapar y lanzar al agua |
| Entorno | Tres islotes con rocas y palmeras | Fondo visual fuera de la zona jugable |

Los tablones se agrupan en una malla por cubierta. Las juntas son visuales: el suelo físico es continuo.
Las cubiertas altas tienen una base sólida hasta el nivel inferior. Las cuatro rampas usan cajas
inclinadas continuas, con 15 cm de prolongación en cada extremo para solapar los descansillos.
Los bots buscan las rampas cuando su objetivo está en otra cubierta.
Las cuerdas, faroles y adornos no añaden colisiones pequeñas que traben al personaje.
La escotilla es decorativa y está a ras del suelo. Las bases de los mástiles sí tienen colisión.
El casco original funciona como apariencia; las superficies y barandas jugables tienen colisiones propias.

## Materiales

Carpeta: `Assets/_Game/PirateArena/Materials`.

- `Pirate_Atlas_URP`: asigna la textura original a barco, piratas, tiburón, cañones y accesorios.
- `Deck_WarmWood` y `Hull_DarkWood`: tablones cálidos y juntas oscuras.
- `Brass_Trim`, `Iron_Black`, `Rope_Sand` y `Lantern_Cream`: remates, metal, cuerdas y faroles.
- `Ocean_Turquoise`: shader propio de agua con olas suaves, reflejos y trazos de espuma.
- `Foam_Ivory` y `Cannon_RedZone`: espuma, indicador del jugador y aviso transparente de impacto.
- `Fighter_Grip` y `Props_Grip`: fricción distinta para personajes y carga, sin rebote artificial.

La iluminación usa sol cálido, ambiente azul suave, sombras y niebla distante.

## Física y controles

| Acción | Control |
| --- | --- |
| Moverse respecto a la cámara | WASD |
| Saltar; subir desde una borda | Espacio |
| Agarrar personaje, objeto o borda en el aire | Mantener clic izquierdo |
| Levantar lo agarrado | Shift mientras se mantiene el agarre |
| Lanzar en la dirección del personaje | Soltar clic izquierdo |
| Soltar sin lanzar | F |
| Empujar | Clic derecho |
| Escapar cuando otro te carga | Pulsar Espacio tres veces |
| Mirar y girar la cámara | Ratón; Q / E como alternativa |
| Acercar o alejar la cámara | Rueda del ratón |
| Nueva ronda, después del resultado | R |
| Abrir sala / menú | Esc |

El movimiento usa aceleración y conserva el impulso externo. Después de un impacto se suspende
brevemente el control para que el cuerpo caiga y recupere el equilibrio con fuerzas, sin enderezarlo
instantáneamente. El agarre usa una unión flexible que acerca la carga gradualmente; tiene un límite
de duración y puede romperse. El peso influye en la distancia de lanzamiento.

Los cinco modelos usan los esqueletos originales de sus archivos glTF. Tienen reposo, caminar,
correr, saltar, caer, aterrizar, reacción al golpe, eliminación y celebración. Los brazos se adaptan
al agarre, levantamiento, empuje, lanzamiento, forcejeo y sujeción a la borda mediante Animation Rigging y dos restricciones Two Bone IK por personaje.
Las animaciones no desplazan la cápsula física. El equilibrio aumenta al cargar peso para evitar que
el portador se incline hasta el suelo. Los golpes fuertes activan temporalmente un ragdoll físico de diez huesos; al recuperarse, vuelve el Animator y el control de la cápsula. La cápsula sigue siendo la autoridad de juego.

Prefabs animados: `Assets/003_Scripts/PirateArena/Resources/PirateCharacters/`.
Clips y controladores: `Assets/003_Scripts/PirateArena/Animations/`.
Para regenerarlos desde los originales: menú `Multiverse > Piratas > Preparar personajes animados`.
El proceso conserva las armas en los archivos fuente, pero las excluye de los prefabs del minijuego.

Los cañones avisan 2,2 segundos antes de disparar, más 0,8 segundos de vuelo. La zona roja representa
el radio de impacto de 3,4 m y se coloca sobre la cubierta correspondiente. Los bots intentan salir
de la zona marcada. El tiburón anuncia su salto con espuma y captura cerca del borde; el jugador puede
retirarse hacia el centro. Entrar en el agua elimina al tripulante. Barriles y cubos se restablecen al reiniciar.

## Ajustes y validación

Editar `Assets/003_Scripts/PirateArena/Settings/ArenaBalance.asset` en el Inspector para ajustar tamaño,
velocidad, salto, equilibrio, empuje, agarre, lanzamiento y cadencia de cañones.

La compilación C# se comprobó con Roslyn y las referencias del Unity 6000.5.6f1 instalado.
`python Tools/validate_pirate_arena.py` comprueba el enlace de las dos escenas, modelos, materiales,
textura y configuración. Los identificadores de los modelos se contrastaron con sus datos importados.
El plano adjunto representa la distribución; no es una captura del juego.

Las pruebas de Play están en `Tools/PirateAnimationPlaytest.cs`, `Tools/PirateMapPlaytest.cs` y
`Tools/PirateRoundPlaytest.cs`. Se ejecutan desde Unity Pipeline con `script run`, estando en Play.
Cambian temporalmente la partida para aislar las comprobaciones: salir de Play después de ejecutarlas.
Los resultados se guardan en `Temp/AnimationChecks/`; el informe estable está en `Docs`.

## Partida en línea

Photon Fusion 2.1.3 usa modo anfitrión. El menú permite crear una sala privada, entrar con su código,
iniciar otra ronda y volver al modo local. La sala admite hasta seis jugadores y completa las plazas
libres con bots. Todos deben usar la misma versión del ejecutable; la región está fijada en Sudamérica.

El anfitrión pulsa **Crear sala**, comparte el código y pulsa **Iniciar / reiniciar ronda**.
Los invitados escriben ese código y pulsan **Entrar con código**. Un jugador que entra tarde toma una
plaza de bot; si ese personaje ya estaba eliminado, espera la siguiente ronda. Al desconectarse un
invitado, su plaza vuelve a ser un bot. Si sale el anfitrión, la sala se cierra: no hay migración de host.
El menú no pausa una ronda en curso. Solo el anfitrión puede reiniciarla, desde el menú o con R al terminar.

La cámara usa Cinemachine para seguir al jugador local, permite órbita con ratón, zoom de 3,5 a 14 m y retrae la distancia
ante los obstáculos físicos del mapa. Tras una eliminación sigue a un superviviente. La geometría
puramente decorativa del casco y el aparejo conserva su tratamiento visual y no bloquea la cámara.

El anfitrión simula física, bots, agarres, objetos, cañones, tiburón y eliminaciones. Los clientes
envían controles; no posiciones ni objetivos de agarre. La autoridad limita la magnitud del movimiento,
rechaza valores no finitos, comprueba alcance, obstáculos y enfriamientos. Los clientes interpolan los
estados recibidos y reproducen las animaciones. No se ha implementado predicción local ni rollback de
física: una conexión con mucha latencia hará que los controles del invitado respondan más tarde.

Pruebas de red: `Tools/pirate_network_playtest.py`, `Tools/pirate_network_actions_test.py`.
Pruebas de cámara: `Tools/PirateCameraPlaytest.cs`. Informes completos y límites de validación en
`Docs/Multijugador_2026-10-27.md`.

Hay trece efectos originales para pasos, agarre, empuje, lanzamiento, salto, aterrizaje, golpes, cañones, tiburón y agua. Se generan con `Tools/generate_pirate_audio.py` y se cargan desde `Resources/PirateAudio`.

Siguen pendientes mando, pruebas en dos equipos y con latencia o pérdida de paquetes, una sesión completa con seis humanos y el pase final de arte, mezcla de audio y rendimiento.
