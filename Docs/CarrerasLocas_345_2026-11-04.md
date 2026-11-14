# Carreras Locas: puntos 3, 4 y 5 — 2026-11-04

## Circuito
- Curvas enlazadas en el lateral oriental; desvío y mirador al sur.
- Subida y bajada progresivas, con cota máxima de pista de 3 m.
- Superficie de carretera con MeshCollider, barreras adaptadas a cada tramo.
- Atajo señalizado: puente de 4 m de ancho, sin barandillas y con rampas. 39.9 m frente a 50.2 m de ruta principal.
- Controles alternativos 19/20/21, reincorporación por 22; mantiene la obligación de pasar los checkpoints en orden y en el sentido de carrera.
- Fallar la trazada del puente deja el coche sobre el terreno inferior; R recupera al último control válido.
- Minimapa muestra el atajo amarillo.

## Objetos
- Iconos propios para turbo, pulso, trampa y escudo, con color y descripción de uso.
- Efecto de destello al recoger cajas e impactos.
- Pulso y trampa colocados según la altura de la superficie: no aparecen bajo las pendientes.
- La interfaz explica duración o función y recuerda Espacio/F.

## Presentación
- Atlas del coche existente adaptado a cuatro colores de carrocería, preservando cristales, ruedas y detalles.
- Ruedas originales animadas por velocidad; delanteras giran con la dirección.
- Confeti al terminar la carrera y ganador destacado en resultados.
- Coches que terminan despejan la línea y desactivan su colisión para no bloquear a corredores pendientes.

## Pruebas
- Race345_Test_2026-11-04.txt: 15 PASS, incluido recorrer realmente el atajo con W y cruzar 19..22 sin ganar una vuelta.
- Race345_Regression_2026-11-04.txt: 18 PASS de controles, derrape, objetos, suelo y checkpoints.
- Race345_FinishSafety_2026-11-04.txt: PASS de despeje de meta y colisiones de finalistas.
- Race345_FullRound_2026-11-04.txt: los cuatro coches terminaron tres vueltas en la ruta nueva; último a 89.9 s. Esta carrera se ejecutó antes del ajuste final de despeje de meta; ese ajuste se verificó separadamente con FinishSafety y la regresión.
- La llamada de carrera completa agotó el tiempo de respuesta del runner, pero la tarea siguió y guardó el resultado PASS; se confirmó Finished y timeScale=1 antes de continuar.
- Se corrigió una primera obstrucción de la entrada al atajo y se repitió la prueba hasta atravesar correctamente toda la conexión.
- La prueba de controles se aisló de objetos de combate previos para medir aceleración sin impactos.
- Consola final y compilación sin errores actuales.
- No se creó build ni se añadió multijugador.

Respaldo previo: Temp/CrazyRaceBefore345.
Escena guardada: Assets/001_Scenes/CarrerasLocas.unity.
