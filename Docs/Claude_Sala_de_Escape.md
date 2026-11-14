# Sala de Escape · Torre del Multiverso (2026-11-05)

Minijuego cooperativo nuevo: **10 habitaciones** en fila, **4–6 jugadores** (tú + compañeros IA), 15 minutos.
Escena: `Assets/001_Scenes/SalaDeEscape.unity` (en Build Settings). Entrada desde el Hub: portal **ESCAPE** (tecla 6), al este de la plaza.

## Salas
| # | Sala | Mecánica |
|---|------|----------|
| 1 | Vestíbulo de las Placas | Una placa por jugador (con su color); todos a la vez. |
| 2 | Sala de las Palancas | El tablero de colores solo se ve si alguien pisa el atril; otro baja 4 palancas en ese orden. |
| 3 | Bodega de los Cofres | 6 cofres (mantén E): uno tiene la llave, otro es sorpresa (te lanza). Llave → candado de la puerta. |
| 4 | Puente de Baldosas | El camino seguro brilla al empezar y cada 12 s; las falsas se caen al vacío. |
| 5 | Biblioteca del Código | Cuenta los libros que brillan por color; el panel da el orden; teclado con 4 dígitos (error = bloqueo 3 s). |
| 6 | Almacén de Cajas | Empuja 3 cajas físicas sobre placas doradas; botón rojo para reiniciarlas. |
| 7 | Pasillo de Láseres | 5 filas que se encienden por turnos (parpadean antes); botón al fondo. |
| 8 | Cripta de las Antorchas | 5 antorchas encendidas a la vez; cada una dura 16 s. |
| 9 | Balanza del Equilibrio | Mismo número de jugadores en cada plato (mín. 2) durante 2,5 s. |
| 10 | Portal del Multiverso | 3 células de energía al generador (una en lo alto de una escalinata); todo el equipo dentro del portal. |

**Modo ayudante:** los compañeros IA cooperan, pero dejan el paso clave al jugador durante un rato (tu placa, la caja y las antorchas con marca celeste, la célula alta, el botón de los láseres, cruzar el puente primero…) y solo lo resuelven ellos si tardas (20–40 s).

Controles: WASD mover · Espacio saltar · E usar (mantener) · Q soltar · clic derecho / Z C girar cámara · rueda zoom · M música · Esc pausa.

## Archivos
- `Assets/003_Scripts/EscapeRoom/`: EscapeDirector (flujo, tiempo, resultados), EscapeLevel (construye la torre en runtime), EscapeRoom + EscapeRooms (las 10 salas), EscapeActor / EscapeBot (jugadores e IA), EscapeCamera, EscapeHUD (menú, HUD, teclado, pausa, resultados), EscapeParts (interactuables, placas, objetos, puertas), EscapeKit (bloques biselados, texto 3D, props), EscapeAudio, EscapeArtSet.
- `Resources/EscapeArtSet.asset`: reutiliza prefabs propios (Maceta, Banco, Cajas, Tesoro, Antorcha tiki…), la paleta y el material de texto 3D.
- `Resources/EscapeAudio/`: 20 sonidos + música originales generados con `Tools/generate_escape_audio.py` (pasos/saltos/cuenta atrás reutilizan los de la Arena).
- Personajes: el mismo look "tripulantes blanditos" (`PirateCrewStyle.Apply` ahora acepta cualquier anfitrión).
- Editor: `Assets/_Game/Editor/EscapeRoomSetup.cs` (menú Multiverse → Sala de escape → Crear o actualizar escena) y `Playtests/EscapePlaytest.cs`.
- Hub: `MultiverseHub.cs` añade el portal en runtime (`BuildEscapePortal`), botón 6 y la carga de `SalaDeEscape`. Copia previa en `~/backup_claude/MultiverseHub_before_escape.cs`.
- UI: nuevo tema `MultiverseUI.Mode.Escape`.

## Pruebas
- EscapePlaytest con 4, 5 y 6 jugadores (IA controlando a todos, tiempo ×4): 10/10 salas, todo el equipo en el portal, resultados mostrados, consola sin errores (87–94 s de juego).
- Hub: portal construido, se selecciona al acercarse, plataforma transitable + HubPlazaPlaytest + ArtPassPlaytest.Hub: 33/33.
- Arena tras el cambio de personajes: PirateHybridPlaytest 15/15.
- Capturas: `Docs/Claude_Capturas/Escape/` (Sala_01..10, Torre_completa, Sala_XX_juego, Hub_Portal_Escape). Informes: `Docs/Claude_Pruebas/Escape_N_jugadores.txt`.

## Límites conocidos
- Modo local (tú + IA), como Código Mortal; no hay red Fusion para varios humanos todavía.
- Los indicadores de progreso bajo el reloj no se ven (el HUD ya muestra "SALA n/10").
