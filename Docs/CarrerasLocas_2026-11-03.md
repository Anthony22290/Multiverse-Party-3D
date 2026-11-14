# Carreras Locas — Costa del Caos (2026-11-03)

Escena: Assets/001_Scenes/CarrerasLocas.unity. Acceso desde el cuarto portal de MundoDeInicio.
Modo local: un jugador, tres bots, tres vueltas. No se añadió multijugador ni se creó build.

## Cambios
- Circuito tropical original: suelo continuo, 96 tramos, 32 puertas ordenadas, barreras sólidas, meta y costa redondeada.
- Assets existentes: coches deportivos, camión, conos, palmeras, rocas y atlas de zombies/piratas.
- Roles, nombres y ruta guardados correctamente. Se eliminaron dependencias de tags inexistentes.
- Aceleración limitada, frenado y marcha atrás, control lateral, fricción de contacto corregida.
- Derrape con miniturbo al soltar: carga mínima 0.8 s; segundo nivel a 1.8 s.
- Turbo, pulso recto, trampa y escudo; 12 cajas con reaparición a 5 s y 2 pads de turbo.
- Salida con cuenta atrás; checkpoints secuenciales y rechazo del cruce hacia atrás.
- Clasificación, resultados, reinicio, cámara de persecución, minimapa y HUD uGUI.
- R recupera al último checkpoint sin avanzar la carrera. Escape vuelve a la plaza.
- El generador antiguo del menú ahora abre la escena guardada y evita reemplazarla por el prototipo roto.
- Prototipo anterior conservado desactivado en la escena; copia previa en Temp/CrazyRacersBefore.

## Controles
W / flecha arriba: acelerar.
S / flecha abajo: frenar y marcha atrás.
A/D o flechas: girar.
Shift + giro: derrapar; soltar al cargar para obtener miniturbo.
Espacio o F: usar objeto.
R: recuperar coche.
Escape: plaza.
Enter en resultados o botón Otra carrera: reiniciar.

## Verificación en Editor
- CrazyRace_Playtest_2026-11-03.txt: 18 PASS, con teclado virtual mediante Input System y frames reales.
- CrazyRace_Interactions_2026-11-03.txt: 5 PASS; pulso, trampa, pad, barrera y reinicio.
- CrazyRace_FullRound_2026-11-03.txt: carrera real de 3 vueltas con los 4 coches controlados temporalmente por IA, sin teletransportarlos para completar vueltas. Los 4 terminaron; último a 55.1 s. Altura máxima observada 1.31 m y velocidad máxima 30.73 m/s. La escena conserva 1 humano/3 bots.
- CrazyRace_Portal_2026-11-03.txt: botón del hub -> carrera -> hub PASS.
- Total: 25 comprobaciones/escenarios satisfactorios.
- Compilación y consola actual sin errores; quedan advertencias no bloqueantes.
- Revisión visual de HUD, resultados y circuito. La costa redondeada es un cambio visual posterior; mantiene los mismos colliders de las pruebas.

## Alcance de REA
Ver ReferenceAnalysis/MarioKart64-reference-2026-11-03.md y JSON de evidencia. REA identificó el paquete y la instalación; rechazó la ROM .z64 por formato no soportado. Las mecánicas son una implementación propia, no una reproducción verificada de las fórmulas originales.

## Límites
Primer circuito local completo. El equilibrio fino de derrape, dificultad de bots y objetos puede ajustarse tras jugar manualmente varias partidas. No incluye selección de personajes, campeonato, multijugador ni sonidos/música del juego de referencia.
