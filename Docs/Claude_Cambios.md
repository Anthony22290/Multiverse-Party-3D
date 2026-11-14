# Pase visual de MULTIVERSE PARTY 3D (Claude, 4 de noviembre de 2026)

Mejora del diseño visual de MundoDeInicio, ArenaDelCaos, CarrerasLocas y CodigoMortal, más las interfaces, **sin descargar ni comprar assets**. Todo lo nuevo es original: mallas low-poly procedurales, materiales URP y prefabs generados por scripts de Editor. Se reutilizan los modelos del proyecto (pack pirata, materiales de la Arena), la fuente **Kenney Future** y los sonidos de clic del *UI Pack* de Kenney (CC0), que ya estaban en `Assets/002_Modelos/interfaces`.

Gang Beasts y Human Fall Flat se usaron **solo como inspiración general** (menús de party game con mucho color, botones gruesos y animaciones con rebote). No se descompilaron ni se copiaron sus archivos, sprites ni diseños concretos. No se usaron REA ni Ghidra.

No se generó build. No se tocaron personajes, rigs, animaciones, mecánicas, controles, red ni archivos `.meta` existentes.

## Cómo regenerar o editar

Menú **Multiverse › Arte** en el Editor:

| Menú | Qué hace |
|---|---|
| 0 · Crear skin de interfaz | Crea `Assets/_Game/Resources/MultiverseUISkin.asset` (fuentes y sonidos editables en el Inspector). |
| 1 · Generar biblioteca de arte | Genera o actualiza paleta, mallas y prefabs. Conserva los GUID: las escenas que ya los usan se actualizan solas. |
| 2 · Decorar MundoDeInicio / 4 · CarrerasLocas / 5 · CodigoMortal | Rehace solo la raíz `ArtPass · Claude` de cada escena. Es idempotente y no toca el resto del mapa. |
| Capturar catálogo de prefabs | Captura cada categoría en `Docs/Claude_Capturas/Biblioteca_*.png`. |

Las recetas de cada objeto están en `Assets/_Game/Art/Editor/MultiverseArtLibrary.cs`: medidas, colores y colisionadores, organizados por categoría. Se editan ahí y se vuelve a ejecutar el menú 1. Los prefabs también se pueden abrir y retocar a mano en Unity.

## Biblioteca generada (`Assets/_Game/Art/`)

`Meshes/<categoría>`, `Materials/Paleta`, `Materials/Texto`, `Prefabs/<categoría>`, `Shaders/`. Son 65 prefabs entre 40 y 2.700 triángulos.

- **Común (19):** farola, banco, barandilla, valla, jardinera, árbol, pino, arbusto, poste indicador, banderas en 6 colores, guirnalda de banderines, cajas, nube, maceta.
- **Hub (14):** 3 casas (zócalo, tejado a dos aguas con vuelo, ventanas con marco, contraventanas y jardinera, puerta, chimenea), 3 puestos de mercado con toldo de rayas y fruta, Faro del Multiverso, fuente, kiosco de información, 5 estandartes de portal (uno por modo).
- **Piratas (9), con los materiales del propio barco:** bandera pirata, choza de playa sobre pilotes, torre vigía, faro de piedra, bote de remos, boya, antorcha tiki, tesoro (cofre y bolsa originales del pack + monedas), muelle con salvavidas y farol.
- **Carreras (12):** grada con techo y banderas, pila de neumáticos, 3 vallas publicitarias, torre de control, torre de socorrista, 2 sombrillas, boxes con puertas de colores, bandera a cuadros, carrito de helados.
- **CódigoMortal (11):** 4 edificios (rejilla de ventanas encendidas, maquinaria en la azotea, depósito de agua, antena y letrero de neón), farola moderna, banco de hormigón, 2 kits de kiosco (toldo, rótulo luminoso, remate y aparatos en el techo), pantalla gigante, maceta moderna, flecha de neón.

**Textos 3D:** el shader de fuente de Unity dibujaba los carteles `TextMesh` a través de las paredes. El nuevo shader `Multiverse/Text3D` respeta la profundidad. Los carteles de las escenas pasan a usar la fuente Kenney Future (8 en el Hub y 24 en Carreras, con tamaño ×0,82; los 8 de Código Mortal se cambiaron en una pasada anterior y no consta su tamaño).

## Colocación segura

Cada pieza se coloca sobre el suelo real detectado por raycast. Se descarta si su volumen choca con cualquier colisionador existente: paseos, pasarelas, barreras, checkpoints, triggers, decorados u otras piezas. Además, cada escena tiene sus zonas prohibidas: en Carreras, una distancia mínima al borde de la pista calculada con `CrazyKartRoadSurface`. Lo descartado queda registrado en `Docs/Claude_Capturas/Pase_<escena>.txt`.

## Escenas

### MundoDeInicio (guardada)
- Calle del mercado al norte del paseo: 5 casas, 4 puestos (FRUTAS, MAPAS, DULCES, RECUERDOS), farolas y guirnaldas.
- Faro del Multiverso en la esquina noreste, fuente al suroeste y kiosco de información al sureste.
- Farolas, bancos y jardineras en el césped junto a los paseos; guirnaldas sobre el paseo este-oeste.
- Estandartes del color de cada modo en los portales (piratas rojo, criaturas morado, carreras amarillo, código turquesa, zombis verde).
- Poste indicador y macetas en la plaza de inicio, árboles y arbustos en las esquinas, nubes altas.
- 72 piezas colocadas, 17 descartadas por seguridad, unos 30.000 triángulos añadidos (con batching estático).

### ArenaDelCaos (se construye en Play)
`PirateArenaArtDressing` añade, al construir el mapa, un archipiélago con el mismo estilo pirata. Usa los materiales del barco y los modelos del pack:
- Isla oeste: pueblo de pesca con choza, antorchas, bandera pirata, calaveras y muelle que apunta al barco.
- Isla noreste: faro, casa-barco y casa del pack, tesoro, antorcha, bandera y muelle.
- Isla suroeste: torre vigía, acantilado y antorchas.
- Dos barcos pequeños en el horizonte, dos botes y seis boyas que flotan, lejos de la cubierta y de la ruta del tiburón.

**Sin colisionadores** (verificado): no afecta a la física, los bots, los cañones, el tiburón, la cámara ni la red. No se tocó nada de la cubierta.

### CarrerasLocas (guardada)
- Grada frente a la meta, boxes y torre de control, banderas a cuadros.
- Pilas de neumáticos en el exterior de las curvas, vallas publicitarias mirando a la pista.
- Playa con torres de socorrista, sombrillas y carrito de helados; árboles en el jardín central y nubes.
- 53 piezas colocadas, 4 descartadas, unos 26.000 triángulos añadidos.

### CodigoMortal (guardada)
- Anillo de 30 edificios fuera de los muros, sobre una calle decorativa sin colisión que tapa el vacío.
- Kit visual en los 12 kioscos existentes (toldo, remate ámbar, rótulo luminoso con nombre propio, aparatos en el techo).
- Farolas modernas y flechas de neón en los muros, guirnaldas entre kioscos (por encima de la cabeza), pantalla gigante sobre el observatorio, nubes altas.
- **Ninguna pieza nueva tiene colisionador** dentro del recinto: el NavMesh guardado y las rutas de los bots siguen igual (comprobado 30/30). Unos 84.000 triángulos en total, sobre todo en los edificios de fondo, que son estáticos y se combinan con el batching.

## Interfaces

`Assets/_Game/UI/MultiverseUI.cs` es un kit común que se aplica en tiempo de ejecución sobre los canvas que ya construía cada modo. **Los callbacks, la distribución y la lógica no cambian.**

- Paneles redondeados (sprite generado en tiempo de ejecución, 9-slice), con borde grueso de color y sombra.
- Botones-píldora con "labio" inferior, rebote al pasar el ratón y al pulsar, y sonido de clic.
- Fuente Kenney Future: títulos con contorno, textos que se ajustan al espacio en lugar de cortarse, titulares grandes con un ligero balanceo.
- Paneles grandes con animación de entrada.
- Un tema por modo: Hub azul con borde dorado; Arena violeta y rosa con botones amarillos; Carreras azul con borde amarillo y botones coral; Código Mortal azul noche con borde menta y botones cian.
- El HUD IMGUI de la Arena usa los mismos paneles redondeados, las barras de agarre y golpes en forma de píldora y las mismas fuentes. La barra de controles inferior se ensanchó de 740 a 930 px para que el texto quepa con la fuente nueva.

## Archivos existentes modificados

Se hizo una copia previa de los scripts y escenas en la carpeta de trabajo de la terminal remota (no visible en tu proyecto). Para volver atrás, lo más fiable es tu control de versiones.

| Archivo | Cambio |
|---|---|
| `Assets/003_Scripts/PirateArena/PirateArenaMap.cs` | 1 llamada a `PirateArenaArtDressing.Dress` tras crear los islotes. |
| `Assets/003_Scripts/PirateArena/MultiverseHub.cs` | 1 línea: `MultiverseUI.Skin` al final de `BuildUI`. |
| `Assets/003_Scripts/PirateArena/PirateSession.cs` | 1 línea: `MultiverseUI.Skin` al final de `CreateMenu`. |
| `Assets/003_Scripts/PirateArena/PirateArenaDirector.cs` | `Panel`/`Meter` usan el kit; fuentes en los `GUIStyle`; barra de controles más ancha. |
| `Assets/003_Scripts/CrazyRacers/CrazyKartHUD.cs` | 1 línea: `MultiverseUI.Skin` al final de `Start`. |
| `Assets/003_Scripts/MortalCode/MortalCodeHUD.cs` | 1 línea: `MultiverseUI.Skin` al final de `Start`. |
| `Assets/001_Scenes/MundoDeInicio.unity`, `CarrerasLocas.unity`, `CodigoMortal.unity` | Raíz nueva `ArtPass · Claude` y fuente/material de los `TextMesh`. |

**Nuevos:** `Assets/_Game/` (Art, UI, Resources, Editor). Incluye `Editor/ClaudeBridge.cs`, un puente que ejecuta órdenes escritas en `ClaudeBridge/cmd.txt`; se puede borrar sin afectar al juego. Las pruebas reutilizadas de `Tools/` están copiadas en `Assets/_Game/Editor/Playtests/Reutilizadas`, con la salida redirigida a `Docs/Claude_Pruebas/` para no sobrescribir los informes anteriores.

## Pruebas en Play (Editor, un jugador)

| Escena | Prueba | Resultado |
|---|---|---|
| MundoDeInicio | `HubPlazaPlaytest` existente (suelo 324/324, contactos, perímetro, portales) | 14/14 PASS |
| MundoDeInicio | Recorrido real del explorador por 12 rutas + ningún colisionador sobre paseos o pasarelas | 12/12 rutas PASS, sin solapes |
| ArenaDelCaos | `PirateMapPlaytest` existente (rampas, laterales sellados, bases) | 39/39 PASS |
| ArenaDelCaos | Decorado creado y sin colisionadores | PASS |
| CarrerasLocas | `Race345Regression` existente (controles, objetos, checkpoints, recuperación, suelo continuo) | 18/18 PASS |
| CarrerasLocas | Carrera completa de 3 vueltas con 4 coches | PASS, los 4 terminan; último en 87,3 s |
| CarrerasLocas | Decorado sin solapes con pista, barreras, checkpoints u objetos | PASS |
| CodigoMortal | `TestMortalCode` existente (reglas, visibilidad, colisión, NavMesh 30/30, HUD, victoria) | 23/23 PASS |
| CodigoMortal | Decorado sin colisionadores en el recinto | PASS |

Las capturas están en `Docs/Claude_Capturas/`: `*_Vista_General`, vistas de detalle, `*_Play_HUD`/`*_Play_UI`, `Arena_*` y `Biblioteca_*`. Los informes de pruebas están en `Docs/Claude_Pruebas/`.

## Observaciones y pendientes

- Algunas rutas del Hub ya estaban bloqueadas antes de este pase, por decorados que ya tenían colisión: la nave espacial expuesta sobre el extremo oeste del paseo norte y las rocas de la plaza y de la isla de criaturas. Las rutas de prueba se ajustaron a los caminos reales y esos obstáculos no se cambiaron. Revisar si la nave debe ocupar el paseo.
- La consola muestra en cada recarga de dominio un error `You may not pass in objects that are already persistent`. Viene de `Unity.ProjectAuditor.Editor.ProjectAuditorSettings.OnDisable` (paquete del Editor), no de los scripts del juego.
- No se probó el multijugador ni el rendimiento en una build. La medición fue en el Editor: triángulos añadidos por escena y ausencia de colisionadores en las zonas de juego.
- Posibles siguientes pasos: personalizar los setos y jardineras primitivos de Código Mortal, decorar las islas zombi y de criaturas del Hub cuando esos modos existan, y añadir un perfil de post-procesado (bloom suave) para que brillen los materiales emisivos.
