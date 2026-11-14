# Revisión de animaciones, escaleras y ronda local

Fecha: 27 de octubre de 2026. Unity 6000.5.6f1. Escena: `ArenaDelCaos`.

## Correcciones

- Las cubiertas de proa y popa ahora son volúmenes sólidos hasta el nivel inferior. Se cerró el acceso al vacío bajo sus placas.
- Las cuatro escaleras tienen un collider de caja inclinado continuo, prolongado 15 cm en ambos extremos. El apoyo se solapa con los descansillos.
- Los bots buscan una escalera cuando el objetivo está en otra cubierta.
- El equilibrio del portador compensa el peso de lo agarrado. Antes se inclinaba unos 59 grados al levantar otro personaje; en la prueba corregida se mantuvo prácticamente vertical y elevó al cautivo unos 90 cm respecto al portador.
- Los cañones limpian proyectiles y avisos al terminar la ronda. El tiburón libera al personaje capturado y reinicia su fase, evitando que quede congelado al terminar la partida.
- Cinco modelos originales con rig y diez clips por modelo; brazos con IK para empujar, agarrar, cargar, levantar, lanzar y colgarse. Las armas de los glTF se excluyen de los prefabs jugables.

## Comprobaciones realizadas en Play

- Animaciones y mecánicas: caminar y correr con movimiento de huesos, salto, caída, aterrizaje, empuje con impulso físico, agarrar y forcejear, levantar y lanzar personajes y objetos, colgarse de la borda y saltar desde ella, golpe, recuperación, eliminación, reaparición y celebración.
- Mapa: 492 muestras de apoyo sobre las cuatro rampas, incluyendo tres líneas por rampa. Ocho recorridos físicos de jugador: subida y bajada en cada escalera. Todos llegaron al descansillo sin atravesar el suelo y con apoyo estable. También se comprobó que ambas bases de cubierta bloquean la entrada inferior.
- Bots: ascenso a proa y popa usando las escaleras.
- Ronda normal: seis participantes, avisos y disparos de cañón, actividad del tiburón, eliminaciones y final en 52,1 segundos. Limpieza de peligros y reinicio con la tecla R, recuperando seis participantes y eliminando agarres/cuerpos congelados.
- Referencias: el validador de escenas, modelos, materiales, textura, configuración y shaders pasó.

Resultados completos: `Animaciones_Resultados_2026-10-27.txt`, `Mapa_Resultados_2026-10-27.txt` y `Ronda_Resultados_2026-10-27.txt`.
La llamada de consola de la prueba larga agotó su espera de 30 segundos, pero la tarea continuó dentro de Unity y guardó el resultado completo exitoso. No se lanzó una segunda prueba simultánea.

## Límites y trabajo pendiente

Esto valida el prototipo local y los recorridos indicados; no demuestra ausencia de todos los fallos posibles.

1. Integrar Photon Fusion: salas, autoridad, sincronización de personajes/objetos/peligros y pruebas con dos o más clientes. El SDK instalado todavía no convierte este prototipo en multijugador en línea.
2. Ragdoll físico completo por huesos. Actualmente el cuerpo físico usa una cápsula y equilibrio activo; brazos y piernas son animaciones visuales.
3. Audio y respuesta audiovisual: pasos, esfuerzo, empujones, agua, aviso de cañón y tiburón.
4. Pulido de arte y cámara, incluidos los artefactos oscuros visibles en algunas superficies del barco; revisión de sombras y geometría decorativa superpuesta.
5. Menú/lobby, soporte de mando, balance con jugadores humanos, perfilado de rendimiento y prueba de un ejecutable fuera del Editor.

No se ha probado todavía una partida en red ni se ha generado una build final distribuible en esta revisión.
