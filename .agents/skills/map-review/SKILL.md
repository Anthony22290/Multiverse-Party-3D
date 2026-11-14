---
name: map-review
description: Revisar y corregir mapas de MULTIVERSE PARTY 3D en Unity cuando se soliciten pruebas de jugabilidad o haya fallos de colisiones, caídas, reapariciones, atajos o jugadores atrapados. No aplicar una auditoría completa por un cambio únicamente decorativo.
---

# Revisión de mapas

Revisa el mapa con el personaje o vehículo real del minijuego. Distingue una inspección del escenario de una prueba física en Play. Ajusta el alcance al problema solicitado; una corrección puntual no exige revisar todos los mapas.

## Contexto del proyecto

- Escenas: `Assets/001_Scenes/`. Lógica: `Assets/003_Scripts/`.
- Consulta `AGENTS.md` para operar el Editor y las skills `unity-architecture` y, si cambias el aspecto, `art-style-guide` de esta misma carpeta de skills.
- Usa el Editor conectado mediante Unity CLI. Después de entrar en Play, espera `unity status --until-ready --format json` antes del siguiente comando.
- Identifica primero escena activa, modo Play, cambios sin guardar, controlador y reglas de caída. Conserva el trabajo del usuario; no recargues una escena con cambios pendientes ni guardes como definitivos objetos de prueba.
- No crees builds ni amplíes el trabajo a multijugador por una revisión local. Si el usuario pide verificar red, usa `multiplayer-testing`.

## Elegir pruebas según el mapa

| Área | Qué comprobar |
|---|---|
| Suelos, puentes y carretera | Aterrizar desde arriba; chocar lateralmente; intentar volver desde debajo cuando esa zona sea accesible. Revisar juntas, rampas, bordes y transiciones entre colliders. |
| Movimiento y estabilidad | Saltar, caer y golpear obstáculos con velocidades alcanzables en el juego, incluidos turbo, empujones o ragdoll si corresponden. Buscar penetración, rebotes desproporcionados y aceleraciones sin entrada. |
| Caídas | Caer por cada tipo de borde relevante, debajo del puente y fuera del mapa. Comprobar que sucede la eliminación o recuperación prevista, sin bucles ni esperas indefinidas. |
| Reaparición | Volver sobre suelo válido, orientado hacia una ruta transitable, sin solaparse con coches, personajes u obstáculos. Restablecer velocidades y estados temporales que causen otra caída inmediata. |
| Atajos y progreso | Recorrer la ruta normal y el atajo en ambos sentidos cuando corresponda. Caer, reaparecer o retroceder no debe regalar checkpoints, vueltas, puntos ni victorias. |
| Zonas atrapables | Probar huecos estrechos, esquinas, escaleras, decorados cercanos y plataformas móviles. Debe existir una salida o recuperación coherente con el modo. |
| Presentación | La cámara no queda bajo el suelo ni cruza estructuras durante la recuperación; estelas y efectos no dibujan líneas desde la caída hasta el respawn. Los límites peligrosos son legibles. |
| Bots y ronda | Los bots pueden usar las rutas necesarias. Si cambian recorrido, checkpoints o salida, completar una ronda y verificar resultado, reinicio o regreso. |

No impongas respawn a todos los modos: en Arena del Caos caer puede eliminar; en Carreras Locas debe recuperar según las reglas vigentes; en el hub debe evitarse quedar atrapado fuera de la zona recorrible.

## Diagnóstico y corrección

- Reproduce el fallo con pasos y ubicación concretos antes de atribuirle una causa. Registra posición, velocidad y estado del controlador cuando ayuden a distinguir geometría, fuerzas y recuperación.
- Inspecciona collider efectivo, capas, matriz de colisión, triggers, normales de la malla, espesor, Rigidbody, detección de colisión y escrituras directas al Transform. No confundas la malla visible con la colisión real.
- Una superficie de una sola cara puede permitir entrar desde debajo. Corrige la geometría de colisión cuando deba bloquear ambos lados; un teletransporte de recuperación no demuestra que la colisión quedó arreglada.
- Para aislar una prueba física, desactiva temporalmente recuperación automática o control únicamente en la instancia de prueba y restáuralos al terminar. Después prueba el flujo completo con todos los sistemas activos.
- Corrige la causa más pequeña compatible con la mecánica. No cambies globalmente gravedad, fuerzas, capas o materiales para ocultar un problema localizado sin evidencia de que sea un fallo global.
- Conserva anchura útil, gálibo y tamaños de personaje/vehículo al ampliar un mapa. Mover o escalar decorados no garantiza que checkpoints, respawns y límites sigan alineados.

## Evidencia y cierre

- Reutiliza pruebas existentes en `Tools/` y reportes de `Docs/` si siguen correspondiendo al mapa actual; revisa sus efectos antes de ejecutarlas. Añade automatización solo cuando permita comprobar una regresión física significativa.
- Tras una corrección de gameplay, verifica compilación y ejecuta en Play el caso que fallaba y los casos próximos afectados. Amplía a ronda completa cuando cambie la progresión o el trazado.
- Si una herramienta agota su espera, comprueba si la tarea sigue ejecutándose antes de repetir acciones o lanzar otra prueba sobre la misma escena.
- Retira objetos y entradas simuladas de prueba, restaura tiempo, controles y ajustes temporales. Guarda únicamente cambios intencionales del mapa.
- Para una revisión extensa, guarda en `Docs/` un informe fechado con escena, caso, pasos, resultado esperado, observado y evidencia. Separa **aprobado**, **fallido** y **no probado**; un raycast o una compilación no sustituyen una prueba de movimiento.
- Informa de qué se corrigió, qué se probó y los límites pendientes. No anuncies que el mapa es imposible de atravesar por haber probado unos pocos puntos.
