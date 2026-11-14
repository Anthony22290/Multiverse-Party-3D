# Carreras Locas · mejoras de jugabilidad (2026-11-10)

## Nuevo
- **Salida con turbo**: acelerar cuando sale el «1» da turbo de 1,4 s («¡SALIDA PERFECTA!»). Acelerar durante el «3» cala el coche 0,9 s («¡CALADO!»). Solo cuenta si la cuenta atrás corre de verdad. Los bots consiguen la salida perfecta un 40 % de las veces.
- **Rebufo**: ir pegado detrás de un rival (menos de 11 m, alineados, a más de 43 km/h) durante 1,1 s da un turbo corto. Una barra fina bajo los avisos muestra la carga.
- **Objetos según posición**: el primero recibe sobre todo trampas y escudos, y el último sobre todo turbos y pulsos (último: turbo 45 %, trampa 5 %).
- **Pulso teledirigido**: persigue al rival más cercano que tengas delante (hasta 60 m, cono de 50°) y deja una estela.
- **Bots con personalidad**: cada uno tiene su ritmo (96,5–100 %), se acercan o se alejan un poco del jugador (±7 %) para que la carrera vaya ajustada, derrapan en las curvas cerradas para ganar miniturbo y usan los objetos con criterio (trampa o escudo si llevan a alguien detrás, pulso si tienen blanco, turbo en las rectas).
- **Tiempos y récords**: el HUD muestra la vuelta actual o la última y el récord. El récord de vuelta y el de carrera se guardan en PlayerPrefs (`CarrerasLocas_RecordVuelta`, `CarrerasLocas_RecordCarrera`; solo cuenta el jugador humano) y aparecen avisos «¡RÉCORD DE VUELTA!» y «¡NUEVO RÉCORD DE CARRERA!».
- **Resultados**: los rivales que siguen en pista muestran un tiempo estimado (≈) según su ritmo. Aparecen tu mejor vuelta y los récords, y sale «¡HAS GANADO!» si ganas tú.
- **Pausa**: ESC o P (Start en el mando) abre Continuar / Reiniciar / Volver a plaza. Antes, ESC te sacaba directamente a la plaza. En la pantalla de resultados, ESC sigue llevando a la plaza.
- **Mando**: RT o A acelera, LT frena, el stick izquierdo gira, RB/LB derrapa, X o B usa el objeto, Y vuelve a la pista, Start pausa, y A en resultados lanza otra carrera.
- **Música y avisos sonoros originales** (`Tools/generate_race_audio.py` → `Resources/RaceAudio/`): bucle de 140 bpm que se acelera en la última vuelta, pitidos de cuenta atrás, salida, vuelta, última vuelta, récord, rebufo y fanfarria de victoria o de final. M silencia todo y se recuerda entre sesiones.

## Archivos
- Modificados: CrazyKartController, CrazyKartDirector, CrazyKartHUD, CrazyKartHazard, CrazyKartItemBox, CrazyKartItemArt (texto del pulso), CrazyKartFeedback (el silencio pasa a CrazyRaceAudio).
- Nuevos: `CrazyRacers/CrazyRaceAudio.cs`, `CrazyRacers/Resources/RaceAudio/*.wav`, `Tools/generate_race_audio.py`, `_Game/Editor/Playtests/RaceImprovementsPlaytest.cs` (Run, FullRace).
- La escena no cambia: todo se añade en tiempo de ejecución.
- Copia previa de los scripts: `Docs/Claude_Backups/Carreras_2026-11-10/`.

## Pruebas
- `Claude_Pruebas/Carreras_Mejoras_2026-11-10.txt`: 14/14 PASS (salida perfecta con W en el 1, calado, pausa, reparto de objetos, pulso teledirigido contra un rival desplazado 3,5 m, récords que ignoran a los bots y tiempos estimados).
- `Claude_Pruebas/Carreras_CarreraCompleta_2026-11-10.txt`: carrera con los 4 coches en IA a tiempo ×3. Terminan los 4 (el último en 93,3 s), con 16 rebufos, 6 impactos y 1 recuperación automática.
- Race345Regression: 18/18 PASS (vuelve a escribir su informe de 2026-11-04).
- Consola sin errores nuevos. Capturas en `Claude_Capturas/Carreras/`.

## Pendiente
- Falta una partida a mano para ajustar sensaciones: la ventana de salida, la fuerza del rebufo y el ±7 % de los bots.
