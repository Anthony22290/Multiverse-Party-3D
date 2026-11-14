# Arena del Caos: revisión de jugabilidad local

## Cambios

- Equilibrio del cuerpo con torque máximo configurable, menos corrección en el aire y recuperación gradual de postura al salir del aturdimiento. Se mantiene el muñeco físico y sus colisiones.
- Dos manos con anclajes separados durante el levantamiento: ya no intentan llevar dos contactos al mismo punto.
- E y F del jugador apuntan en la dirección horizontal de la cámara, incluso si el personaje estaba mirando hacia otro lado.
- Bots que cargan buscan una abertura real en las barandillas, usan la navegación existente entre cubiertas, se detienen dentro del barco y lanzan hacia fuera. Conservan una salida por tiempo y la resistencia del agarre. Evitan la zona de cañonazo y forcejean cuando están conscientes.
- HUD con resistencia del agarre, golpes acumulados, KO con cuenta atrás, recuperación protegida, progreso de escape y estado de cada mano. Señales sobre los personajes aturdidos. Conserva controles y resultado de ronda.

Los ajustes están en PirateArenaSettings. No se modificaron escenas guardadas, modelos ni controles de multijugador. Los detalles del nuevo HUD son para el jugador local con autoridad; sincronizarlos en red queda fuera de esta revisión.

## Verificación realizada

- 11 comprobaciones de `Tools/PiratePolishPlaytest.cs`: eventos reales del Input System para W, Espacio, E, ambos botones de ratón, Shift y F; cuatro saltos seguidos; caídas inclinadas desde 3, 8 y 12 metros; lanzamiento autónomo de bot por una abertura y posición del portador dentro de cubierta.
- 15 comprobaciones de `Tools/PirateHybridPlaytest.cs`: salto/caída, KO/recuperación/protección, agarres, lanzamiento, agotamiento/recarga, ganador, empate y reinicio.
- La antigua prueba de lanzamiento suponía que el bot siempre miraba al frente. La nueva navegación cambia su orientación: se fijó una dirección explícita para aislar esa prueba; la estrategia autónoma se comprueba por separado.
- Ronda normal con seis bots, cañones y tiburones: terminó a los 15,18 segundos con Pirata 4 como único superviviente.
- Captura revisada visualmente del HUD en KO: `Arena_HUD_2026-11-03.png`.
- Consola sin errores actuales ni errores de compilación. Editor dejado fuera de Play, velocidad de tiempo restaurada a 1 y dispositivos de prueba retirados.

Resultados completos: `Arena_Polish_Test_2026-11-03.txt` y `Arena_Hybrid_Test_2026-11-03.txt`.

## Límites pendientes

Las entradas fueron inyectadas por una prueba sobre el mismo recorrido de lectura de teclado y ratón, no fue una sesión humana de juego. Falta valorar sensación, legibilidad en movimiento, duración deseada de las rondas y equilibrio con jugadores reales. No se garantiza ausencia de toda combinación física inestable; las caídas y saltos probados no reprodujeron el vuelo. La locomoción conserva el cuerpo central con muñeco físico; no es una copia exacta de Gang Beasts. No se probó red ni se creó un ejecutable.
