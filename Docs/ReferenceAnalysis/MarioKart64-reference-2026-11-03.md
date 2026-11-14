# Mario Kart 64 portable — inspección REA, 2026-11-03

Objetivo: localizar la implementación de referencia para orientar Carreras Locas. No se importaron recursos, ROM, música ni código del juego al proyecto.

## Evidencia
- Paquete suministrado: nintendo-64-mario-kart-64-portable.exe (88 162 198 bytes).
- REA inspect_artifact: ev_b7b7ead9f16c06f6121c0a1f507ccfe3ced736585546c0c75ac70e71566529e9.
- SHA-256 del ejecutable: e69b347b4b27cb9bec6d0c677bc557651d4d8807b3d049efc67b0e8e2e5699b3.
- Inventario de carpeta: ev_c298139c09c487055b91a53228bc4ccba6020ba5a09493d6ccec7b05e29376f4.
- LEEME identifica RMG 0.9.0 y la instalación bajo C:/Users/antho/JuegoViejo/nintendo-64-mario-kart-64.
- Esa instalación ya existía; no se ejecutó el instalador. REA la inspeccionó (MarioKart64-installed.json).
- Se localizó roms/Mario_Kart.z64 (12 582 912 bytes). inspect_artifact devuelve unsupported_target / unsupported binary format. Error conservado en MarioKart64-ROM.json.

## Límite de la investigación
La evidencia identifica el paquete/emulador y la ROM, pero NO demuestra las fórmulas de conducción, derrape, IA, distribución de objetos o física del Mario Kart original. No se ha completado una descompilación de la ROM. No se atribuyen a REA parámetros deducidos de memoria.

## Implementación propia para Carreras Locas
Diseño arcade inspirado en la referencia solicitada, con parámetros ajustados en Unity:
- Una pista tropical original de 96 tramos visuales y 32 controles ordenados.
- 3 vueltas, salida bloqueada durante cuenta atrás, clasificación y resultados.
- 1 jugador / 3 bots; datos de pista y roles persistentes en escena.
- Aceleración controlada, frenado/marcha atrás, adherencia lateral y derrape.
- Miniturbo al soltar un derrape cargado; pads de aceleración.
- Objetos originales: turbo, pulso recto, trampa y escudo.
- Recuperación al último checkpoint, sin regalar vueltas.
- Cámara de persecución, HUD, minimapa, reinicio y regreso a la plaza.

Assets propios del proyecto: Vehicle_Sports, Vehicle_Truck, TrafficCone_1 del pack zombies; palmeras/rocas del pack piratas; sus atlas existentes. No son assets de Nintendo.
