# Arena del Caos: física y audio

Fecha: 29 de octubre de 2026.

- Los impactos activan durante el aturdimiento diez cuerpos físicos en torso, cabeza, brazos y piernas. Al terminar, se destruyen esos cuerpos y vuelve el Animator con Animation Rigging. La cápsula del personaje sigue controlando posición, agarres y eliminación.
- El ragdoll se crea solo durante el golpe: mantener cuerpos cinemáticos en el esqueleto impedía que las piernas caminaran. Las uniones quedan fuera del esqueleto animado cuando el personaje está normal.
- Trece efectos mono originales, 22,05 kHz, generados por `Tools/generate_pirate_audio.py`: paso, agarre, empuje, lanzamiento, salto, aterrizaje, impacto, aviso/disparo/explosión de cañón, aviso/salto de tiburón y chapoteo.
- `PirateArenaAudio` usa 16 voces espaciales reutilizables. Las acciones y pasos de personajes replicados producen efectos en cada cliente; cañones y tiburón reproducen sus transiciones de fase en el anfitrión y los invitados.

## Verificación en Unity Play

- Compilación C# correcta y 13 clips importados/cargados.
- Activación del ragdoll: once Rigidbody dinámicos contando la cápsula; recuperación: solo la cápsula dinámica, Animator activo.
- Prueba de animaciones: reposo, caminar con huesos móviles, correr, salto, caída, aterrizaje, empuje, agarre, carga, levantamiento, lanzamiento de personaje y objeto, borda, ragdoll, recuperación, eliminación, reaparición y victoria. Todas las comprobaciones pasaron.
- Prueba de ronda local: seis tripulantes, subida de bots por ambas escaleras, cañones, tiburón activo, ganador por eliminaciones, limpieza de peligros y reinicio. Todas las comprobaciones pasaron.
- Audio: 13 clips cargados; se comprobó reproducción activa de `cannon_blast` mediante AudioSource.

Pendiente: escucha y mezcla final en altavoces/auriculares, perfilado del coste del ragdoll con seis personas, y prueba de audio/física entre dos ejecutables después de estos cambios. No se generó una build nueva.
