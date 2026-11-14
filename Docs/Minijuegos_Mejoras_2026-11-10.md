# Mejoras de minijuegos (2026-11-10)

Mejoras sin juegos nuevos. No se tocaron Carreras Locas ni el mapa de selección (MundoDeInicio).
Audio original generado con `Tools/generate_minigame_audio.py`. Kit de efectos compartido en `Assets/_Game/FX/MultiverseFX.cs` (ráfagas, ondas, rayos, textos flotantes; usa los shaders `Resources/RaceFX`).

## Código Mortal
- **Audio** (`MortalCodeAudio`, `Resources/MortalAudio`):
  - Música de espías y cuenta atrás.
  - Avisos de código anotado, teclas de la terminal, acceso concedido o denegado, protector, eliminación, alarma, objetos y victoria o derrota.
  - M silencia.
- **Libreta de códigos** a la izquierda: los códigos leídos (★ el nuevo); los eliminados aparecen tachados.
- **Objetos holográficos** (`MortalCodePickup`), uno cada 20 s y como máximo 3 a la vez:
  - **Escáner**: revela el código del rival más cercano que no conozcas, aunque haya paredes.
  - **Turbo**: 6 s de velocidad extra.
  - Los bots también van a por ellos.
- **Muerte súbita** en los últimos 30 s: los códigos se leen desde 26 m (antes 18), con alarma y música más tensa.
- **Efectos**:
  - Terminales con pantalla holográfica que destella en verde o rojo (`MortalCodeTerminalFX`).
  - Protector convertido en burbuja.
  - Eliminación con rayo, fragmentos y texto flotante.
  - Temblor de cámara.
  - Registro de eliminaciones.
- **Resultados** con clasificación (supervivientes y eliminaciones).
- Prueba: `TestMortalCode` 23/23 PASS. Ronda natural con bots terminada (ganó Lila con 4 eliminaciones).

## Invasión Zombi
- **Objetos que sueltan los zombis** (`SurvivalPowerUp`):
  - **Botiquín**: +45 de vida, y +20 a los compañeros cercanos.
  - **Furia**: 9 s con doble daño, más velocidad y aura roja.
  - **Bomba**: explosión que elimina a los zombis cercanos; a los jefes les quita 45.
  - Los jefes siempre sueltan un botiquín y una bomba. Los objetos parpadean antes de desaparecer (18 s).
- Salpicadura verde en cada golpe (más grande con furia).
- Sonidos nuevos: `zmedkit`, `zrage`, `zbomb`, `zdrop`.
- Prueba: `SurvivalPlaytest.Run4` PASS (10/10 oleadas, 4/4 jefes, escape, 377 s).

## Sala de Escape
- `EscapeAtmosphere`: polvo en suspensión alrededor de la cámara y luces cálidas que titilan. Respeta las luces que cambian los puzles.
- Celebración al superar cada sala: ráfaga arcoíris en la salida, onda, texto «¡SALA N SUPERADA!» y vítores (`cheer`).
- Récord de tiempo por número de jugadores (`SalaEscape_Mejor_N`), con aviso y sonido al batirlo. Aparece en los resultados.
- Prueba: `EscapePlaytest.Run4` PASS (10/10 salas, 136 s).

## Arena del Caos
- **Kraken** (`Assets/_Game/Arena/PirateKrakenHazard.cs`), solo en partidas locales; las online no cambian, porque sus peligros van replicados.
  - Cada 15–22 s (el primero entre los 11 y 15 s) un tentáculo sale del agua junto al barco.
  - Un círculo rojo avisa durante 1,7 s y luego golpea la cubierta: tumba y lanza a quien esté dentro.
  - Sonidos `kraken_rise` y `kraken_slam`.
- **Marcador de victorias** entre revanchas de la misma sesión (`PirateArenaScoreboard`), abajo a la izquierda.
- Pruebas: `PirateHybridPlaytest` 15/15 PASS. Ataque del kraken comprobado: desplazó a 5 de 6 piratas dentro del círculo.

Capturas en `Docs/Claude_Capturas/Minijuegos/`. Copias de seguridad: el control de versiones (los scripts originales se modificaron con cambios acotados).

## Pendiente
Probar a mano el equilibrio de dificultad:
- Escáner/turbo y muerte súbita en Código Mortal.
- Frecuencia de botiquines y bombas en Zombis (con 4 jugadores no cayó nadie).
- Frecuencia del kraken.
