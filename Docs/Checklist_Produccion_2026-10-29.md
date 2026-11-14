# Checklist de producción de MULTIVERSE PARTY 3D

Estado: 29 de octubre de 2026. El GDD 4.0 contiene el diseño completo. `[x]` significa comprobado con el alcance indicado; `[ ]` indica trabajo pendiente.

## Base y mundo de inicio

- [x] Proyecto Unity 6000.5.6f1 con URP y assets importados.
- [x] Mundo de inicio 3D recorrible con tres portales y selección por teclado/interfaz.
- [x] Portal Piratas abre Arena del Caos; el menú pirata permite volver al mundo.
- [ ] Convertir el hub en lobby social con grupo persistente, votación y selección de mapas.
- [ ] Añadir personalización modular de personaje y guardado local.

## Arena del Caos

- [x] Barco pirata jugable con seis plazas, bots y eliminación al caer al agua.
- [x] Empujar, agarrar, cargar, levantar, lanzar y agarrarse a la borda.
- [x] Cañones con aviso rojo, proyectil y explosión; tiburón con aviso y salto.
- [x] Animaciones, IK de brazos, ragdoll temporal, recuperación y reinicio.
- [x] Trece clips de audio importados y reproducción técnica comprobada.
- [x] Ronda local completa, ganador y limpieza de peligros comprobados en Play.
- [ ] Escuchar y mezclar audio en el juego con auriculares y altavoces.
- [ ] Probar cambios recientes de física y audio con host y cliente en dos ejecutables.
- [ ] Medir latencia, pérdida de paquetes y respuesta de agarres/objetos.
- [ ] Probar seis personas reales y ajustar duración, empuje y cañones.
- [ ] Perfilar CPU/memoria del ragdoll y pulir arte final del barco.
- [ ] Soporte de mando, remapeo, volumen y subtítulos de avisos.

## Ocho minijuegos diseñados pero sin implementar

- [ ] Escondidas Extremas: disfraces, detector, señuelos, tiempo y captura autorizada.
- [ ] Carreras Locas: kart, derrape, turbo, checkpoints y vueltas.
- [ ] Escape Cooperativo y Física Total: dos manos, objetos, puzles, salida y checkpoints.
- [ ] Terror Entre Amigos: linterna, ruido, IA, objetivos, rescate y salida.
- [ ] Cocina en Desastre: recetas, estaciones, pedidos, incendios y puntos.
- [ ] Traidor Secreto: roles ocultos, tareas, sabotajes, reuniones y voto.
- [ ] Construye y Defiende: recursos, piezas, reparación, torres y núcleos.
- [ ] Código Mortal: códigos, terminal, línea de visión, errores y protector.
- [ ] Definir si Zombis y Criaturas serán modos adicionales; sus portales aún no tienen reglas aprobadas.

## Entrega

- [ ] Registrar fuente y licencia de cada asset distribuido.
- [ ] Crear una build nueva solo cuando haga falta probar los cambios recientes fuera de Unity.
- [ ] Para cada modo: entrada, ronda completa, resultado, reinicio/regreso y prueba de host y cliente.
