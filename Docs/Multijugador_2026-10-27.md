# Cámara y multijugador — 27 de octubre de 2026

## Cómo jugar

1. Abrir `Builds/MultiverseParty3D/MultiverseParty3D.exe`, o la escena `Assets/001_Scenes/ArenaDelCaos.unity` en Unity y pulsar Play.
2. Elegir **Jugar local con bots**, o **Crear sala** para jugar por Internet.
3. Compartir el código con el invitado. Este abre la misma versión del juego y pulsa **Entrar con código**.
4. El anfitrión pulsa **Iniciar / reiniciar ronda**. Las plazas libres son bots.
5. Ratón para mirar, rueda para zoom, WASD para mover, Espacio para saltar o forcejear, clic izquierdo para agarrar, Shift para levantar, soltar clic para lanzar, F para soltar sin lanzar y clic derecho para empujar.
6. Esc abre el menú sin pausar una ronda en curso. R reinicia al terminar, exclusivamente en el anfitrión.

Para llevar el juego a otro ordenador, copiar **toda la carpeta** `Builds/MultiverseParty3D`, incluyendo `MultiverseParty3D_Data`, `MonoBleedingEdge` y las DLL. El archivo `.exe` solo no basta.

## Implementación

- Unity 6000.5.6f1; Photon Fusion 2.1.3; modo Host/Client; región `sa`; protocolo `pirate-arena-1`.
- Salas no listadas, con código de 4 a 12 letras o números y capacidad de seis jugadores.
- Un `PirateNetworkWorld` replica seis propietarios y personajes, ocho objetos, dos cañones, un tiburón y el resultado de la ronda.
- Física autoritativa mediante `FixedUpdateNetwork` y simulación explícita de PhysX. Se restaura el modo de física local al salir o fallar la conexión.
- Controles con secuencias para salto, empuje y soltar; elección de objetivos exclusivamente en el anfitrión. Validación de alcance, obstrucción, enfriamiento y estado vivo.
- Los clientes reciben posición, rotación, velocidad visual, agarre, carga, aturdimiento, eliminación, acción de animación y punto de agarre para IK.
- Reingreso a una sala existente y reemplazo por bot al salir. Sin migración de anfitrión.
- Cámara orbital cercana con seguimiento suave, zoom, retracción ante colisiones y seguimiento de supervivientes.
- Menú uGUI con controles separados de la simulación. Las ayudas de automatización de pruebas solo se compilan en Editor o Development Build y requieren `-pirate-test`.

## Verificación ejecutada

Dos ejecutables Windows **en procesos separados**, conectados realmente a Photon en Sudamérica desde el mismo PC. No se sustituyó la red por mocks.

| Área | Resultado | Evidencia |
| --- | --- | --- |
| Salas, propietario local, inicio, movimiento, agarre de jugador, carga, ganador, reinicio, cañones, desconexión, reingreso y salida del anfitrión | 22 comprobaciones aprobadas | `Multijugador_Resultados_2026-10-27.txt` |
| Salto, empuje, agarre/carga/lanzamiento de un objeto, alcance y acciones de un jugador eliminado | 13 comprobaciones aprobadas | `Multijugador_Acciones_2026-10-27.txt` |
| Seguimiento, órbita, zoom, obstáculo, recuperación, cursor y espectador | 9 comprobaciones aprobadas en Play, con avance de fotogramas comprobado | `Camara_Resultados_2026-10-27.txt` |
| Entrada inválida | Valores NaN/infinito convertidos en movimiento cero; eje de magnitud excesiva limitado a uno | Inspección en Play de `ApplyInput`, con ronda activa |
| Sala inexistente | Photon devolvió `GameNotFound`; el juego mostró el error y permitió reintentar sin reiniciar el proceso | Registro de la prueba de acciones |
| Referencias del proyecto | Dos escenas, 14 raíces de modelos, 10 materiales, atlas y ajustes correctos | `Tools/validate_pirate_arena.py` |
| Compilación Windows de uso normal | Correcta, cero errores y cero advertencias; sonda de pruebas excluida de Assembly-CSharp | `Temp/PirateReleaseResult.txt` y comprobación del ensamblado |
| Arranque Release y conexión adicional | El ejecutable Release creó una sala; un cliente Editor entró y recibió el mundo con dos participantes y un personaje local | `Temp/PirateRelease.log` y estado de `PirateSession` en Play |

Los escenarios de acciones se aíslan de peligros aleatorios para verificar cada mecánica. Los cañones se prueban por separado con advertencia y proyectil activos en ambos procesos. Las primeras pasadas detectaron carreras del propio arnés al leer archivos y al reiniciar rondas; se corrigieron antes de las pasadas aprobadas. El aviso de Fusion sobre no cargar una escena de red es esperado: cada proceso carga ArenaDelCaos y el anfitrión crea el prefab de estado compartido.

## Límites reales

- La capacidad configurada es seis; se verificaron dos humanos simulados por entradas del arnés y cuatro bots, no seis equipos reales.
- No se midió experiencia bajo latencia o pérdida de paquetes artificial. Los clientes interpolan estados, sin predicción local de física.
- Se comprobó la posición replicada del tiburón; el salto/captura ya existía en el prototipo local, pero no se aisló una captura completa en red en esta pasada.
- No hay migración de anfitrión, cuentas de usuario, reconexión automática ni recuperación de una sala cerrada. Reingresar usa el botón y el código.
- No es un ragdoll articulado como el de los juegos de referencia; mantiene la cápsula física, equilibrio y animación con IK existentes.

## Repetir las pruebas

`Tools/BuildPiratePlayer.cs` expone `Run` para Development Build y `Release` para el ejecutable de uso normal. Ambos se ejecutan mediante Unity Pipeline `script run`, fuera de Play.

La versión Development acepta `-pirate-host CODIGO` o `-pirate-join CODIGO`, junto con `-pirate-test RUTA_ABSOLUTA`. El host debe crear la sala antes de arrancar el cliente. Cada sonda escribe `status.json` y consume `command.json` en su propia carpeta. Los scripts Python documentan los escenarios. Estos canales de prueba **no existen en el ejecutable Release**.

`PirateCameraPlaytest.Run` requiere Play y modifica temporalmente la ronda. Salir de Play al terminar.
