# Arena del Caos: combate físico combinado

Implementación original inspirada en los subsistemas identificados con REA en Human: Fall Flat y Gang Beasts. La investigación estática y sus evidencias están en `ReferenceAnalysis/Mechanics-reference-2026-11-03.md`. No se importó código, modelos ni animaciones de esos juegos. No es una reproducción exacta de sus físicas.

## Comportamiento implementado

- Conserva las dos manos independientes, orientación por cámara, agarre de objetos/rivales, balanceo y subida por bordes.
- E golpea/empuja: los golpes cercanos acumulan daño de equilibrio. Tres golpes sin recuperación causan KO temporal por defecto. Un KO en cubierta no elimina.
- El daño se recupera tras 2,5 segundos sin golpes. KO de 2,4 segundos y protección de 0,9 segundos después; los golpes de combate durante ese intervalo desplazan pero no prolongan la inconsciencia.
- El agarre consume resistencia: levantar cuesta más, un rival consciente resiste más y dos manos reducen el esfuerzo. Al agotarse se suelta sin impulso; soltar los botones permite recuperar y volver a agarrar.
- Shift levanta; F lanza. Soltar el ratón solo libera. Espacio permite saltar o forcejear cuando se está consciente. Los bots priorizan capturar rivales aturdidos y ahora lanzan en vez de limitarse a soltarlos.
- Caer al agua elimina. El último superviviente gana; una eliminación simultánea de todos produce empate. Reiniciar limpia KO, agarres y resistencia.

## Estabilidad

Los músculos de posición del muñeco competían con articulaciones de traslación bloqueadas y añadían fuerza neta. Se sustituyeron por torques relativos al padre con reacción igual y opuesta. Los impactos visuales tampoco añaden impulso neto adicional. Los cuerpos proxy o congelados no ejercen fuerzas de músculos. Se mantienen las colisiones y límites anteriores de velocidad.

La locomoción sigue usando un cuerpo central y un esqueleto físico; no se ha sustituido por la arquitectura completa de ninguno de los juegos de referencia.

## Verificación

`Tools/PirateHybridPlaytest.cs` ejecutado en el Editor en Play: 15 comprobaciones aprobadas. Resultado detallado en `Arena_Hybrid_Test_2026-11-03.txt`. Incluye salto real y aterrizaje, caída de 6 m, daño/KO/recuperación, manos físicas sobre rival, lanzamiento, resistencia, ganador, empate y reinicio.

En la ejecución aislada final: salto de aproximadamente 0,96 m; caída de 6 m sin relanzamiento ascendente apreciable. La primera comprobación de contacto se consultó demasiado pronto; se aisló de objetos sueltos y se esperó un máximo de 0,8 segundos a contacto real. La repetición confirmó las dos manos.

Sin errores actuales de compilación ni consola. No se probó multijugador ni se creó un ejecutable. Las pruebas automatizan entradas internas y acciones; no son una comparación de sensación mediante controles humanos con los originales ni prueban ausencia de todos los fallos físicos posibles.

Prueba adicional: ronda normal con seis bots, peligros y colisiones activos, finalizada en 32,92 segundos con un único superviviente (Pirata 2).
