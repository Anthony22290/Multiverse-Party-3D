# Invasión Zombi · Calle del Fin (2026-11-05)

Minijuego cooperativo de supervivencia por oleadas, **2–4 jugadores** (tú + compañeros IA), unos 6 minutos.
Escena: `Assets/001_Scenes/InvasionZombi.unity` (en Build Settings). Entrada desde el Hub: portal **ZOMBIS** (tecla 2) → botón **¡SOBREVIVIR!**

## Reglas
- 10 oleadas. En cada una, un **zombi brillante** (aura verde, «LLEVA: …») o un **jefe** suelta una pieza de la camioneta.
- La pieza cae con un haz dorado: pasa por encima para cogerla, llévala a la **zona de reparación** y mantén **E** (1 s). Con la pieza encima no puedes golpear (Q la suelta).
- La oleada acaba cuando no queda ningún zombi **y** la pieza está instalada. Descanso de 9 s: los derribados se levantan y todos recuperan vida.
- Con las 10 piezas: **escape**. Horda sin fin; todo el equipo en pie dentro de la zona durante 6 s → la camioneta arranca y sale por la puerta norte.
- Vida 100, regeneración a los 4 s sin daño. A 0 quedas **derribado**: un compañero te reanima manteniendo E (1,8 s) o te levantas solo a los 16 s junto a la camioneta. **Si todo el equipo está derribado a la vez, se pierde.**

| Oleada | Portador | Pieza |
|---|---|---|
| 1 | Caminante brillante | Rueda delantera |
| 2 | Corredor brillante | Rueda trasera |
| 3 | **Jefe: El Gordo** (pisotón con círculo rojo) | Batería |
| 4 | Gordito brillante | Bujías |
| 5 | Brazudo brillante | Radiador |
| 6 | **Jefe: Brazos Largos** (embestida con franja roja, se aturde si choca) | Correa del motor |
| 7 | Corredor brillante | Faros |
| 8 | Gordito brillante | Volante |
| 9 | **Jefe: Costillas** (invoca corredores) | Bidón de gasolina |
| 10 | **Jefe final: El Rey Zombi** (pisotón + invocaciones; furioso al 50 %: embestidas) | Llave de contacto |

Zombis: Caminante, Corredor (rápido, frágil), Gordito (lento, resistente, empuja fuerte), Brazudo (más alcance). Más numerosos y resistentes en cada oleada; con 2–3 jugadores hacen un 20/10 % menos de daño.

Combate: clic izquierdo / J / F. Combo de 3 golpes (el tercero, por encima de la cabeza, hace más daño y empuja más) con auto-apuntado suave. Armas del paquete zombi: bate con pinchos, hacha, guitarra y bate con sierra (una por jugador).

Controles: WASD · Espacio saltar · Clic izq./J golpear · E mantener (instalar / reanimar) · Q soltar pieza · Clic der./Z C girar cámara · Rueda zoom · M música · Esc pausa.

## Archivos
- `Assets/003_Scripts/ZombieSurvival/`: SurvivalDirector (flujo, oleadas, piezas, escape), SurvivalZombie (IA, jefes), SurvivalPlayer / SurvivalBot (jugadores e IA), SurvivalPart, SurvivalTruck, SurvivalLevel (mapa construido en runtime), SurvivalCamera, SurvivalHUD, SurvivalArtSet (+ SurvivalKit).
- `Resources/SurvivalArtSet.asset` y `Resources/SurvivalZombies/`: los 4 zombis del paquete con su controlador de animación (Idle, Walk, Run, Punch, HitReact, Death…), y 32 modelos (calle, vehículos, armas). `Animations/` contiene los clips copiados de los FBX.
- `Resources/SurvivalAudio/`: gruñidos, rugido, derrota, motor y música originales, generados con `Tools/generate_zombie_audio.py`.
- Editor: `Assets/_Game/Editor/SurvivalSetup.cs` (menú **Multiverse → Invasión Zombi → Crear o actualizar escena**) y `Playtests/SurvivalPlaytest.cs` (Run4, Run2, HubPortal).
- Cambios en compartidos: `MultiverseUI.Mode.Zombie` (tema verde), `EscapeAudio` también busca en `Resources/SurvivalAudio`, `MultiverseHub` habilita el portal ZOMBIS. Copias previas en `Docs/Claude_Backups/Zombis/`.

## Pruebas
- SurvivalPlaytest.Run4 (IA controlando a los 4, tiempo ×4): 10/10 oleadas, 4/4 jefes, 10/10 piezas, escape y resultados; 385 s de juego, consola sin errores.
- SurvivalPlaytest.Run2: ganada en la última ejecución (368 s) tras suavizar el daño para equipos pequeños; las dos anteriores se perdieron en la oleada 10. Es un final ajustado a propósito, pero conviene probarlo a mano.
- HubPortal: el portal ZOMBIS muestra «¡SOBREVIVIR!» y carga InvasionZombi.
- Capturas: `Docs/Claude_Capturas/Zombis/` · Informes: `Docs/Claude_Pruebas/Zombis_N_jugadores.txt`.

## Límites conocidos
- Modo local (tú + IA), como Sala de Escape y Código Mortal; sin red Fusion todavía.
- El control con teclado y ratón compila y usa los mismos atajos que los otros modos, pero las pruebas automáticas usan la IA también para tu personaje: falta una partida a mano para ajustar sensaciones (daño, alcance del golpe, ritmo).
- Los personajes del equipo no tienen animación de ataque (el rig compartido no la trae): el arma gira de forma procedural con una estela.
