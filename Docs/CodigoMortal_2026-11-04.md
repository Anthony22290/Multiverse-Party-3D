# Código Mortal — Plaza Vértigo

Primera versión local jugable, 4 de noviembre de 2026. Escena: `Assets/001_Scenes/CodigoMortal.unity`.

## Partida

Un jugador y cinco bots. Cuenta atrás de cinco segundos y hasta cuatro minutos de ronda. Cada participante recibe un código único de cuatro cifras; el propio nunca se muestra. Para leer a un rival hay que estar a 18 metros o menos, mirar hacia él y tener línea de visión. También se comprueba la visibilidad desde la cámara humana para impedir leer a través de edificios. Los bots observan y recuerdan códigos mediante las mismas reglas de distancia y obstáculos.

Usar una de las cinco terminales a menos de tres metros de su punto de interacción. Introducir un código previamente observado elimina al objetivo vivo. Un código incorrecto o no observado bloquea los intentos durante cinco segundos. No se puede eliminarse a uno mismo ni puntuar dos veces al mismo rival. Los bots deben recorrer el mapa hasta una terminal y tardan 3,5 segundos en introducir un código.

Q activa un protector de ocho segundos, una sola vez por ronda. Oculta el código y bloquea eliminaciones contra el jugador; quien está protegido tampoco puede eliminar. Un intento contra un objetivo protegido se rechaza y aplica una breve espera de dos segundos.

Gana el último superviviente. Al acabarse el tiempo con varios vivos hay empate. El jugador eliminado observa la partida; TAB cambia el superviviente observado. Los resultados permiten revancha o regreso al hub. El menú no pausa a los bots.

## Mapa

Plaza de 112 × 104 metros, galerías a seis metros y observatorio a doce. Rampas transitables conectan los niveles; un puente central cruza sobre la plaza. Kioscos con cubiertas y paredes, jardineras, setos, recodos y decorados proporcionan obstáculos visuales y escondites. Se reutilizaron personajes, casas y palmeras del pack pirata existente.

Suelos, rampas y puente usan volúmenes sólidos. Los límites externos están cerrados. Caer desde una plataforma al suelo no elimina; salir por debajo del escenario recupera al jugador en una posición segura, buscando espacio libre. R mantenida 1,2 segundos permite recuperarse manualmente. Esta mecánica conserva el código y el estado de la ronda.

NavMesh guardado en `Assets/003_Scripts/MortalCode/Generated/PlazaNavigation.asset`. Las 30 combinaciones de seis salidas y cinco terminales tienen rutas completas. Tras modificar geometría de colisión, volver a hornear la navegación.

## Controles

- WASD: moverse. Espacio: saltar.
- Ratón derecho mantenido: orientar cámara.
- E: abrir o cerrar terminal; números o botones: escribir; Retroceso: borrar; Enter: enviar.
- Q: protector. R mantenida: recuperación.
- Esc: cerrar terminal o abrir menú. Enter en resultados: revancha.
- En el hub: tecla 5 o botón Código Mortal; también E junto al nuevo portal.

## Validación

39 comprobaciones/escenarios aprobados en el Editor:

- `CodigoMortal_Tests_2026-11-04.txt`: 23 pruebas de reglas, visibilidad, protección, colisión, navegación, interfaz y victoria.
- `CodigoMortal_InputTests_2026-11-04.txt`: siete pruebas usando un teclado virtual del Input System, con restauración posterior del dispositivo y ajustes de entrada.
- `CodigoMortal_LifecycleTests_2026-11-04.txt`: ocho pruebas de revancha, caída fuera del mapa, caducidad del protector, espectador, empate, hub y regreso al modo.
- `CodigoMortal_NaturalRound_2026-11-04.txt`: una ronda con decisiones de bots sin intervenir; ganó Coral, cinco eliminaciones, aproximadamente 138,6 segundos de partida. Se aceleró el tiempo a 5 durante esta prueba y se restauró a 1. La llamada agotó su espera inicial; la tarea terminó después y se verificó su reporte.

No hay errores de compilación ni errores/advertencias actuales de consola al terminar las pruebas. Los reportes no equivalen a una revisión exhaustiva de cada centímetro ni a una prueba con personas reales.

## Alcance pendiente

No implementado ni verificado online. Los códigos permanecen en la memoria del proceso local; una futura versión Fusion debe mantener la autoridad y la información secreta con replicación por destinatario. Falta balance con jugadores humanos y pulido de arte/audio. No se generó build.

Los componentes de reglas, actor, bots, cámara, terminales y HUD están separados en `Assets/003_Scripts/MortalCode/`. El constructor `Tools/BuildMortalCode.cs` se niega a sobrescribir una escena existente. Se conservaron Arena del Caos y Carreras Locas y se añadió el quinto acceso al hub sin reconstruirlo.
