# Prueba de Arena del Caos — 27 de octubre de 2026

Prueba realizada en el Editor Unity 6000.5.6f1 mediante Computer Use, en la escena ArenaDelCaos.
Al terminar se salió de Play. No se generó un ejecutable ni se probó multijugador.

## Resultado observado

- Arranque del mapa con materiales, agua, islas y seis tripulantes.
- Cuenta atrás y avance del reloj de partida; los bots se movieron y hubo eliminaciones.
- Aviso de cañón y zona roja visibles sobre la cubierta.
- Dos partidas terminaron mostrando un ganador, aproximadamente a los 18 y 28 segundos.
- Se observó una nueva cuenta atrás con seis tripulantes después del primer resultado.
  Hubo entrada del usuario durante esa transición: no se atribuye este reinicio a las teclas automatizadas.
- No se encontraron nuevas excepciones, errores C# ni errores de shader en el tramo del registro
  revisado después del arranque de la primera prueba.
- El verificador de referencias pasó para ambas escenas, sus modelos, materiales y configuración.

## Correcciones realizadas

El registro anterior mostraba `CreateImpl is not allowed to be called from a MonoBehaviour constructor`
en PirateFighter, seguido de NullReferenceException al eliminar o reiniciar personajes. Se movió la
creación de MaterialPropertyBlock al método Initialize, con recuperación en SetTint. El error no
se repitió en las partidas observadas.

Se bajó 0,18 m el casco decorativo para separarlo del suelo jugable. Esta modificación no eliminó
todos los defectos visuales observados: persisten líneas/puntos oscuros en la cubierta, cuyo origen
exacto requiere otra inspección de geometría, sombras y antialiasing.

## Pendiente

No se verificaron de manera controlada el recorrido de las cuatro rampas, saltos, agarre,
levantamiento, lanzamiento, escape de un agarre ni captura del tiburón. Las pulsaciones R enviadas
por Computer Use no causaron una transición visible inmediata; el reinicio por teclado automatizado
queda inconcluso. No se declara aprobado el conjunto completo de físicas ni el acabado visual.

El CLI detectó que falta el paquete Pipeline y avisó de actualización de beta.13 a beta.14.
No se instaló ni actualizó para esta prueba: el acceso visual a Unity funcionó directamente.
