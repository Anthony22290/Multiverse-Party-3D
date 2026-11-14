# Carreras Locas · circuito único «Gran Premio Multiverso» (2026-11-10)

Solo queda un circuito, en `Assets/001_Scenes/CarrerasLocas.unity` (la escena del portal del Hub). Costa del Caos y la escena separada `GranPremioMultiverso.unity` se han eliminado, igual que las mallas `Generated/CostaXL_*`.
Copia de la escena y de los scripts anteriores: `Docs/Claude_Backups/Carreras_2026-11-10_mapa/`.

Se regenera con el menú **Multiverse → Carreras Locas → Reconstruir circuito** (`CrazyRacers/Editor/GranPremioBuilder.cs`). Mallas y materiales en `Generated/GranPremio/`.

## Recorrido (1746 m, 69 controles, 3 vueltas)
1. Playa de salida: recta ancha, semáforo y arco de meta reutilizados.
2. Pueblo pirata: curvas en S entre casas, cañones y barriles. Grupos de barriles en mitad de la carretera.
3. Puente de madera estrecho, con baranda baja, sobre la laguna. Al lado, barco grande y muelle.
4. Subida a la montaña: curvas cerradas entre rocas, con rocas caídas en la calzada. Aserradero al pie.
5. Túnel de la cumbre, con faroles. Solo tiene techo donde la montaña lo cubre.
6. Caída del acantilado: hueco de 17,5 m con un desnivel de 6 m. Si vas lento, caes al barranco.
7. Viaducto estrecho y sin barandillas sobre el río.
8. Selva: curvas con vegetación densa y una roca en la trazada.
9. Salto del río: hueco de 15 m con rampa y zona de aterrizaje ancha.
10. Recta de los peñascos: tres peñascos que ruedan de lado a lado (dejan siempre hueco junto a la barandilla).
11. Horquilla noroeste y vuelta a la salida.

## Atajos y lugar oculto
- **Tablón del barranco**: 78 m frente a 122 m. Estrecho, sin baranda y con caída.
- **Camino de tierra**: 189 m frente a 235 m. La tierra frena salvo con turbo, y hay una placa de turbo a la entrada.
- **Cueva del tesoro** (sin señalizar): la entrada está en el pueblo, tapada por palmeras. Pasa bajo la colina, con faroles, cofre de oro, monedas, huesos y calaveras, y tiene 3 cajas dentro.

## Cambios de juego
- Coches más rápidos: velocidad máxima 30 m/s (≈108 km/h; ≈150 km/h con turbo), aceleración 26 y giro 112.
- En el aire: algo más de gravedad y corrección de la deriva lateral (saltos más secos).
- `CrazyKartObstacle`: un choque provoca trompo y empujón (el escudo lo bloquea). Los bots esquivan los obstáculos, también los móviles, frenan antes de las curvas cerradas y dan marcha atrás si se atascan.
- Recuperación: sobre un hueco de salto solo cuenta como caída si el coche baja 4 m. Límite de altura de reaparición: 60 m.
- Terreno pintado por vértice con el shader propio `RaceFX/Terrain`: arena, hierba, selva, roca y cumbres.
- Récords con claves nuevas (`CarrerasLocas_RecordVuelta_GP`, `CarrerasLocas_RecordCarrera_GP`). Se quitó el botón «Otro circuito».
- Corregida la inestabilidad de la suspensión de las ruedas y del aplastamiento con fotogramas largos.

## Pruebas
- `RaceImprovementsPlaytest.Run` en el circuito nuevo: 18/18 PASS.
- `RaceImprovementsPlaytest.FullRace` (4 coches en IA, tiempo ×3): los 4 en meta en las dos últimas ejecuciones, 209–244 s y 217–294 s, con 2 y 4 reapariciones respectivamente. Vuelta de unos 69–79 s.
- `Race345Regression` y las demás pruebas del circuito antiguo quedan obsoletas: dependían de sus 32 controles y su geometría.
- Capturas en `Docs/Claude_Capturas/Carreras/50–68_*`.

## Pendiente
- Una partida a mano para ajustar sensaciones: velocidad, ventana de salida y dificultad de los saltos.
- Si las 3 vueltas (unos 3,5 min) se hacen largas, poner `laps` a 2 en el Director.
