"""Rebuild the project GDD from the original design and verified Unity state."""
from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(r"C:\Users\antho\Downloads\Multiverse_Party_3D_GDD_Final_Gratis_Fusion2.docx")
OUTPUT = ROOT / "Docs/Multiverse_Party_3D_GDD_Actualizado_2026-10-29.docx"
original = Document(SOURCE)
assert sum(p.style.name == "Heading 2" for p in original.paragraphs) == 9

doc = Document()
sec = doc.sections[0]
sec.page_width, sec.page_height = Inches(8.5), Inches(11)
sec.top_margin, sec.bottom_margin = Inches(.73), Inches(.69)
sec.left_margin = sec.right_margin = Inches(.78)
styles = doc.styles
for key, size, bold in [("Normal", 10, False), ("Title", 24, True), ("Heading 1", 15, True), ("Heading 2", 11.5, True)]:
    style = styles[key]
    style.font.name = "Aptos"
    style.font.size = Pt(size)
    style.font.bold = bold
    style.font.color.rgb = RGBColor(0, 0, 0)
    style.paragraph_format.space_after = Pt(5 if key == "Normal" else 8)
    style.paragraph_format.space_before = Pt(0 if key == "Normal" else 10)
    style.paragraph_format.keep_with_next = key != "Normal"
styles["Normal"].paragraph_format.line_spacing = 1.12
title_props = styles["Title"]._element.get_or_add_pPr()
for border in title_props.findall(qn("w:pBdr")):
    title_props.remove(border)

def p(text, style=None):
    return doc.add_paragraph(text, style)

def h(text, level=1):
    return doc.add_heading(text, level)

def bullets(items):
    for item in items: p(item, "List Bullet")

def cell_fill(cell, color):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), color)
    tc_pr.append(shd)

def borders(cell):
    tc_pr = cell._tc.get_or_add_tcPr()
    edge = OxmlElement("w:tcBorders")
    for side in ("top", "left", "bottom", "right"):
        part = OxmlElement(f"w:{side}")
        part.set(qn("w:val"), "single")
        part.set(qn("w:sz"), "4")
        part.set(qn("w:color"), "D9D9D9")
        edge.append(part)
    tc_pr.append(edge)

def table(headers, rows, widths=None):
    t = doc.add_table(rows=1, cols=len(headers))
    t.autofit = False
    t.style = "Table Grid"
    for i, text in enumerate(headers): t.rows[0].cells[i].text = text
    for row in rows:
        cells = t.add_row().cells
        for i, text in enumerate(row): cells[i].text = str(text)
    for ri, row in enumerate(t.rows):
        for ci, cell in enumerate(row.cells):
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            borders(cell)
            cell_fill(cell, "EAF0F5" if ri == 0 else ("F7F9FA" if ri % 2 == 0 else "FFFFFF"))
            if widths: cell.width = Inches(widths[ci])
            for para in cell.paragraphs:
                para.paragraph_format.space_after = Pt(2)
                para.paragraph_format.space_before = Pt(2)
                for run in para.runs:
                    run.font.name = "Aptos"
                    run.font.size = Pt(9)
                    if ri == 0: run.bold = True
    t.rows[0]._tr.get_or_add_trPr().append(OxmlElement("w:tblHeader"))
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return t

title = doc.add_paragraph(style="Title")
title.add_run("MULTIVERSE PARTY 3D")
p("Documento de diseño del juego  Versión 4.0  29 de octubre de 2026")
p("Diseño completo de nueve minijuegos y estado verificable del proyecto Unity. Los portales de Zombis y Criaturas son conceptos de expansión.")
p("Este documento distingue funciones implementadas, propuestas y pendientes. Sirve para decidir el siguiente trabajo sin confundir el alcance final con el prototipo actual.")

h("1 Resumen y estado actual")
p("MULTIVERSE PARTY 3D es un juego social 3D de partidas cortas con estética cartoon low poly. La meta es recorrer un mundo de inicio, elegir minijuego, jugar con amigos y volver a elegir. El alcance completo contempla nueve modos; hoy solo Arena del Caos tiene una ronda jugable.")
table(["Sistema", "Estado al 29 de octubre de 2026"], [
    ("Mundo de inicio", "Escena 3D recorrible con tres portales. Piratas abre Arena del Caos; Zombis y Criaturas muestran contenido futuro."),
    ("Arena del Caos", "Barco pirata, seis plazas con bots, ronda local completa, cañones, tiburón, agarres, lanzamientos, ragdoll temporal y 13 sonidos."),
    ("Red pirata", "Photon Fusion 2.1.3 Host Mode: sala privada por código y dos jugadores comprobados en procesos separados; cuatro bots completan plazas."),
    ("Otros ocho modos", "Diseño de reglas en este GDD. Sin escenas ni partidas funcionales."),
    ("Metajuego", "Personalización, votación, grupo persistente entre escenas, progresión y mando: pendientes."),
], [1.5, 5.25])

h("2 Pilares de diseño")
bullets([
    "Juego social claro: entrar con amigos, entender una regla en segundos y terminar una ronda con resultado visible.",
    "Variedad real: nueve modos con objetivos y controles propios, reutilizando personaje, interfaz y sesión cuando convenga.",
    "Física expresiva: empujar, caer, agarrar y lanzar con consecuencias legibles; priorizar control y sincronización sobre caos impredecible.",
    "Arte coherente: modelos cartoon low poly, formas redondeadas, materiales simples y peligros distinguibles del decorado.",
    "Producción por etapas: cada modo se considera terminado solo tras una ronda completa local y una verificación multijugador.",
    "Juego justo: cosméticos sin ventajas; la autoridad valida victorias, eliminaciones y recursos críticos.",
])

h("3 Personaje y dirección artística")
p("El objetivo final es un humanoide modular compartido entre modos: cara, piel, pelo, ropa, accesorios, colores y gestos. Todas las opciones conservarán collider y estadísticas. Ese sistema de personalización todavía no está implementado. Arena del Caos usa cinco modelos piratas animados y seis plazas; no debe describirse como el personaje modular final.")
p("Los escenarios combinan siluetas sencillas, paleta saturada, iluminación cálida y lectura clara de zonas peligrosas. El mundo de inicio es un archipiélago de pasarelas y portales, con barco, barriles, tiburón, zombi y criatura de los assets existentes. Se deben revisar procedencia y licencias de cada pack antes de distribuirlo.")

h("4 Bucle principal y mundo de inicio")
p("Bucle final previsto: abrir juego → personalizar → crear o unirse a grupo → recorrer hub → votar o escoger modo y mapa → jugar → mostrar resultado → revancha u otro modo → regresar con el grupo. El grupo persistente, el voto y la personalización no existen aún.")
p("Bucle actual: abrir MundoDeInicio → recorrer la plaza con WASD y cámara orbital → seleccionar Piratas → cargar ArenaDelCaos → jugar local con bots o crear/entrar a una sala Fusion → terminar o reiniciar la ronda → volver al mundo de inicio desde el menú del barco. Los portales Zombis y Criaturas están señalados como futuros y no ofrecen botón Jugar.")

h("5 Sistemas compartidos y controles")
p("La arquitectura futura compartirá estados Preparación, Carga, Cuenta atrás, Partida y Resultados; además de equipos, cronómetro, puntuación, espectador, respawn, HUD y desconexiones. Arena del Caos ya tiene fases de ronda, ganador, cámara espectadora, bots y reinicio. No existe todavía un servicio común para los nueve modos.")
table(["Acción", "Mundo de inicio", "Arena del Caos"], [
    ("Mover y cámara", "WASD, ratón derecho, rueda", "WASD, ratón, rueda"),
    ("Saltar", "Sin salto", "Espacio; tres pulsaciones para escapar de una carga"),
    ("Agarrar y lanzar", "Sin interacción física", "Mantener y soltar clic izquierdo"),
    ("Levantar o soltar", "Sin interacción física", "Shift para levantar; F para soltar sin lanzar"),
    ("Empujar", "Sin combate", "Clic derecho"),
    ("Elegir o entrar", "1 a 3 y E junto al portal pirata", "Botones del lobby; R para nueva ronda tras resultado"),
    ("Menú", "Esc vuelve a Piratas", "Esc abre sala y permite volver al mundo"),
], [1.45, 2.55, 2.75])
p("Soporte de mando, remapeo, subtítulos de señales críticas y opciones de volumen son objetivos pendientes. Los controles de los ocho modos futuros son propuestas sujetas a pruebas.")

h("6 Minijuegos y mecánicas")
p("Los nueve modos siguientes forman el catálogo principal del GDD. Los rangos de jugadores y tiempos de los ocho modos sin implementar son metas de diseño, no capacidad comprobada. Cada modo empieza con un solo mapa y una partida completa antes de añadir variantes.")

GAMES = [
    ("6.1 Arena del Caos", "Implementado como primer minijuego jugable. Hasta seis plazas; el anfitrión o el modo local rellenan plazas libres con bots. Una ronda comienza con cuenta atrás y termina cuando queda un superviviente o nadie sobrevive. El barco pirata es el mapa actual; otros mapas del GDD anterior (azoteas, fábrica y lava) no existen.",
     "El personaje corre y salta, empuja, agarra rivales u objetos, los levanta con Shift, los lanza al soltar y puede sujetarse a la borda. No hay armas. Barriles y cubos responden a masa e impulso; golpes fuertes producen aturdimiento y ragdoll temporal de torso, cabeza y extremidades, mientras la cápsula conserva la autoridad física. Caer al agua elimina.",
     "Dos cañones eligen periódicamente un objetivo vivo, marcan el radio rojo durante 2,2 s y disparan un proyectil con 0,8 s de vuelo. El tiburón avisa con espuma, salta hacia un jugador cercano a la orilla y puede capturarlo. Los bots persiguen, empujan, agarran, usan escaleras y evitan avisos de cañón. Hay 13 efectos originales para acciones, impactos, peligros y agua. Fusion replica personajes, objetos, peligros y resultado; el host valida acciones."),
    ("6.2 Escondidas Extremas", "Propuesta: 4 a 12 jugadores, objetivo aproximado de seis minutos. Una fase breve permite a los escondidos elegir posición y transformarse en un objeto del escenario. Los buscadores entran después. Ganan si descubren a todos; los escondidos ganan si resisten hasta el final.",
     "Los escondidos seleccionan disfraces de una lista controlada, se mueven despacio para cambiar de lugar, gastan una carga de señuelo y una habilidad corta de escape. Los buscadores emplean un detector de alcance limitado y realizan intentos de identificación; fallar consume energía o revela temporalmente al buscador. Debe existir un límite que impida disparar a todos los objetos sin pensar.",
     "Primer mapa propuesto: casa gigante; supermercado y museo son expansiones. El host comprueba disfraz, visibilidad, distancia, detección y captura. HUD: tiempo, equipo, cargas y pistas. Primera prueba: un buscador y varios escondidos en un mapa pequeño."),
    ("6.3 Carreras Locas", "Propuesta: 2 a 12 jugadores, carreras de cuatro a siete minutos. El mismo personaje conduce un kart. Gana quien complete vueltas y checkpoints en orden; los atajos solo cuentan si no omiten checkpoints obligatorios.",
     "Acelerar, frenar, girar y derrapar alimentan una barra de turbo. Trampas y escudos dan interacción sin bloquear de forma permanente. Una caída o choque grave devuelve al último checkpoint con una penalización breve. El circuito incluye zonas de agarre, curvas y riesgos que se leen antes de alcanzarlos.",
     "Primer prototipo: un kart y un circuito corto. Ciudad neón, volcán y pista espacial quedan como arte futuro. El host valida orden de checkpoints, vueltas, objetos y resultado; se medirá la sensación de control con latencia antes de aumentar la velocidad."),
    ("6.4 Escape Cooperativo y Física Total", "Propuesta: 2 a 8 jugadores y niveles de 15 a 30 minutos. Todo el equipo gana al resolver mecanismos y alcanzar la salida. No se basa en eliminación; un checkpoint permite recuperar intentos.",
     "Cada mano puede agarrar una superficie o un objeto. Los jugadores arrastran cajas, levantan pesos entre varios, mueven palancas, trepan y se balancean para activar puertas y puentes. Los puzles deben admitir soluciones físicas comprensibles, con una ruta de recuperación si un objeto cae fuera del nivel.",
     "Primer mapa propuesto: fábrica con tres puzles encadenados. Castillo flotante y laboratorio son expansiones. La física de objetos y agarres requiere autoridad de red y pruebas con dos instancias antes de aumentar el tamaño del nivel. La referencia a Human Fall Flat describe interacción física, no copia de arte ni niveles."),
    ("6.5 Terror Entre Amigos", "Propuesta: 2 a 8 jugadores, sesión de 12 a 20 minutos. El equipo busca objetivos y una salida mientras una criatura controlada por IA patrulla. Ganar exige completar objetivos y escapar con las reglas de rescate definidas; la criatura vence si captura a todos o se agota el tiempo.",
     "La linterna consume batería, el ruido atrae a la criatura y algunos muebles permiten esconderse. Llaves y tareas abren rutas; un compañero caído puede ser rescatado durante una ventana limitada. La IA debe comunicar su búsqueda con sonido y animación para que el peligro resulte entendible.",
     "Primera versión: una mansión pequeña, IA sencilla y chat de texto. Hospital, bosque y monstruo controlado por jugador quedan para después. El host decide percepción de la IA, capturas, rescates y victoria."),
    ("6.6 Cocina en Desastre", "Propuesta: 2 a 8 jugadores y rondas de cinco a ocho minutos. En cooperativo se alcanza una meta de pedidos; en versus gana el equipo con más puntos al terminar el tiempo. Los pedidos tienen temporizador y valor visible.",
     "Los jugadores recogen ingredientes, cortan, cocinan, transportan, sirven y lavan. Hay tres recetas iniciales y cuatro estaciones. Comidas quemadas, incendios y estaciones averiadas obligan a reorganizar tareas; entregar pedidos seguidos crea combos. Los objetos deben poder recuperarse si caen fuera del mostrador.",
     "Primer mapa: cocina compacta. Restaurante espacial, barco y cocina móvil son temas futuros. El host valida ingredientes, cocción, entrega y puntos. HUD: pedidos, tiempo, estado de estaciones y puntuación."),
    ("6.7 Traidor Secreto", "Propuesta: 5 a 12 jugadores, ocho a quince minutos. La autoridad asigna roles secretos. Tripulantes ganan al completar tareas o expulsar a todos los traidores; los traidores ganan si reducen la tripulación hasta igualar o superar a los inocentes vivos.",
     "Tripulantes realizan tareas verificables; traidores sabotean sistemas y aprovechan pasadizos. Una reunión permite discutir y votar; durante ella se bloquean acciones de mapa. Las cámaras dan información parcial, no vigilancia total. La expulsión revela el resultado según la opción elegida para la sala.",
     "Primer mapa: nave pequeña; estación científica posterior. El host nunca replica roles secretos a destinatarios no autorizados y valida tareas, sabotajes, votos, empates, desconexiones y condición de victoria."),
    ("6.8 Construye y Defiende", "Propuesta: 4 a 12 jugadores, diez a quince minutos. Dos equipos protegen un núcleo; gana quien destruya el contrario o conserve más vida al terminar. El empate necesita una regla explícita de desempate antes de producción.",
     "Se recolectan recursos compartidos, se colocan piezas modulares en una cuadrícula, se reparan daños y se construyen torres o trampas. La partida alterna preparación y ataque para que haya tiempo de entender la fortaleza. Cañones y dispositivos tienen costo, alcance y tiempo de recarga visibles.",
     "Primer mapa: dos fortalezas simétricas. Islas flotantes después. El host valida recursos, ocupación de celdas, colisiones, daño y reparación; se evita construcción totalmente libre en la primera versión."),
    ("6.9 Código Mortal", "Propuesta: 4 a 12 jugadores, seis a diez minutos. Cada participante recibe un código de cuatro dígitos que puede verse sobre otros personajes, nunca sobre sí mismo. Introducir el código ajeno en una terminal elimina al objetivo si el host valida las condiciones. Gana el último superviviente.",
     "Para leer un código hay que acercarse y tener línea de visión. Un error bloquea nuevos intentos durante cinco segundos. Escondites y rutas obligan a exponerse. Cada jugador dispone de un protector con una carga por ronda que oculta su código durante ocho segundos; mientras esté activo no puede eliminar a otros, según la regla inicial propuesta.",
     "Primer mapa: centro comercial compacto. Laberinto neón y ciudad nocturna después. El host guarda código, protector, alcance, visibilidad, cooldown y vida; el cliente solo solicita un intento. Debe diseñarse replicación por destinatario para evitar filtrar el código propio."),
]

for name, setup, actions, implementation in GAMES:
    h(name, 2)
    p(setup)
    p(actions)
    p(implementation)

h("6.10 Portales de expansión", 2)
p("El hub ya muestra Invasión Zombi y Criaturas del Multiverso, pero ninguno tiene escena jugable ni reglas aprobadas. No forman parte de los nueve modos principales. Propuesta para estudiar después: Zombis como supervivencia cooperativa por oleadas con barricadas y rescate; Criaturas como captura y evasión en una arena con habilidades temporales. Sus objetivos, tamaño de sala y alcance deben decidirse antes de implementarlos.")

h("7 Código Mortal y protector")
p("El código lo genera el host al iniciar la ronda. El cliente propietario no debe recibir su propio código si la API permite una replicación segura por destinatario. El protector oculta el código visualmente para todos los rivales, no altera el valor verdadero ni protege de peligros. Una carga y ocho segundos son valores de diseño ajustables. Los intentos inválidos —jugador muerto, pared, distancia, código equivocado o protector activo— no producen eliminación. Variantes de códigos cambiantes o equipos quedan fuera del primer prototipo.")

h("8 Red con Photon Fusion 2")
p("Arena del Caos usa Fusion 2.1.3 en Host Mode: un jugador simula física, bots, objetos, cañones, tiburón y resultados. Hasta seis plazas, código privado y región Sudamérica. Un invitado tardío ocupa una plaza de bot; al salir, la plaza vuelve a bot. Si sale el anfitrión, la sala termina; no hay migración. La prueba documentada conectó dos ejecutables y cuatro bots, no seis personas reales.")
p("Los clientes envían controles, no posiciones ni objetivos de agarre. El host comprueba alcance, obstáculos, enfriamientos y estado vivo. El movimiento recibido se interpola; no hay predicción local ni rollback de física, así que se espera mayor retraso con latencia alta. La captura del tiburón y el audio/ragdoll tras los cambios recientes aún requieren una nueva prueba entre dos ejecutables.")
p("Para futuros modos, la autoridad del host debe decidir roles, códigos, recetas, checkpoints, recursos, votos y victorias. RPC se reserva para eventos apropiados; el estado de partida vive en propiedades replicadas. La compatibilidad de KCC o Physics Addon debe verificarse antes de incorporarlos; no constan como parte funcional del prototipo actual.")

h("9 Código y escenas")
p("Escenas reales: Assets/001_Scenes/MundoDeInicio.unity y ArenaDelCaos.unity. El hub guarda su geometría y decorados en la escena; crea el explorador, cámara Cinemachine y UI al jugar. ArenaDelCaos levanta el barco, actores, peligros y sesión desde PirateArenaBootstrap. La lógica de ronda, personaje, animación, audio, peligros y red vive en componentes separados bajo Assets/003_Scripts/PirateArena.")
p("Arquitectura objetivo: un núcleo compartido para sesión, fases, puntuación, resultados y carga de escenas, con reglas de cada modo en módulos separados y ajustes en ScriptableObject. No crear un único GameManager para los nueve modos. La migración desde la estructura actual debe hacerse cuando empiece el segundo minijuego jugable.")

h("10 Interfaz sonido y accesibilidad")
p("Estado actual: hub 3D con selector de portales y tarjeta informativa; lobby pirata uGUI con sala, código y modo local; HUD de ronda, aviso rojo de cañón, aviso del tiburón y resultado. La cámara de arena usa Cinemachine y los brazos usan Animation Rigging. Trece sonidos originales de pasos, agarres, empujes, lanzamientos, salto, caída, golpe, cañón, tiburón y agua se cargan desde Resources/PirateAudio con dieciséis voces espaciales reutilizables.")
p("Pendiente: opciones de volumen por canal, mezcla escuchada en distintos equipos, subtítulos para avisos, mando, reasignación de controles y ajustes de accesibilidad. Menú de amigos, chat, voto, clasificación entre modos y voz no están implementados. Photon Voice 2 es una opción futura, sujeta a costos y licencia actuales.")

h("11 Progresión y persistencia")
p("La versión inicial prevista guarda opciones y cosméticos locales gratuitos. XP, logros y perfil en servicio remoto quedan para después de validar el juego base. No hay tienda ni economía implementada. Los resultados competitivos deben validarse en el host; un archivo local no sirve como fuente segura de puntuación online.")

h("12 Assets y licencias")
p("El proyecto contiene paquetes de piratas, zombis, monstruos e interfaces. Se usan modelos piratas en la arena y objetos de los tres grupos en el hub. Cada recurso externo necesita registro de fuente, versión, autor, permiso de redistribución y modificaciones antes de publicar. Herramientas previstas para creación o ajuste: Blender, Krita, GIMP y Audacity; su presencia en el GDD no demuestra que estén instaladas o usadas en este equipo.")

h("13 Paquetes plugins y skills verificados")
p("Paquetes de Unity instalados y su papel. Instalado no significa aplicado a todos los modos.")
table(["Paquete o SDK", "Versión", "Estado y uso"], [
    ("Unity Editor", "6000.5.6f1", "Proyecto abierto en esta versión"),
    ("URP", "17.5.0", "Renderizado y materiales del proyecto"),
    ("Photon Fusion", "2.1.3 Stable 2390", "Arena pirata online; SDK bajo Assets/Photon"),
    ("Cinemachine", "3.1.7", "Cámaras del hub y de Arena del Caos"),
    ("Animation Rigging", "1.4.1", "IK de brazos piratas"),
    ("Input System", "1.20.0", "Teclado, ratón y UI actual"),
    ("uGUI", "2.5.0", "Selector, lobby y HUD"),
    ("glTFast", "6.20.0", "Importación de modelos glTF"),
    ("Unity Pipeline", "0.9.1 exp.1", "Control del Editor y pruebas desde CLI"),
    ("ProBuilder / AI Navigation", "6.1.4 / 2.0.14", "Instalados; uso final en niveles pendiente"),
    ("Memory Profiler", "1.2.0", "Instalado; perfilado final pendiente"),
], [2.25, 1.15, 3.35])
p("Skills locales del proyecto, presentes en .agents/skills: art-style-guide, fusion-networking, minigame-prototyping, multiplayer-testing y unity-architecture. Guían arte, red, alcance, pruebas y estructura; no añaden funciones por sí mismas. En Codex también están disponibles y se han usado caveman, unity:unity-cli, unity:ui, unity:ui-ugui, documents:documents y plugin-management:plugin-management para trabajo y documentación.")
p("Plugins de Codex empleados en el flujo: Unity aporta las skills de CLI, UI y física; Codex App Tools permite inspeccionar el entorno local; Documents aporta el flujo de edición del GDD; Plugin Management comprueba el estado de integraciones. Estos plugins asisten el desarrollo y no son dependencias del ejecutable.")
p("Integraciones externas: Git local está presente, pero este checkout no tiene remoto configurado. El plugin GitHub de Codex figura instalado en esta sesión; no implica que este proyecto esté publicado ni conectado a un repositorio remoto. Figma, Slack, Photon Voice y servicios de backend no forman parte del flujo implementado. No confundir un paquete UPM con un plugin de Codex ni con una conexión de cuenta.")

h("14 Roadmap actualizado")
table(["Hito", "Entregable", "Estado"], [
    ("M0 Base", "Proyecto Unity, URP, assets y guías", "Avanzado"),
    ("M1 Arena pirata", "Ronda, mapa, bots, peligros y audio", "Prototipo jugable"),
    ("M2 Red pirata", "Sala por código y dos procesos", "Probado; falta latencia y seis humanos"),
    ("M3 Mundo de inicio", "Plaza y acceso a Piratas", "Jugable; sin grupo persistente"),
    ("M4 Pulido", "Balance humano, mando, mezcla, rendimiento", "Pendiente"),
    ("M5 Segundo modo", "Un modo del catálogo completo y reutilización del núcleo", "Pendiente"),
    ("M6 Metajuego", "Personaje modular, grupo, voto, progreso", "Pendiente"),
    ("M7 Catálogo", "Resto de modos y pruebas por modo", "Pendiente"),
], [1.5, 3.45, 1.8])
p("No se fija fecha de lanzamiento. Una versión inicial con dos o tres modos completos y probados es un objetivo más acotado que publicar los nueve a la vez. El siguiente paso recomendado es probar Arena del Caos con personas reales, latencia y dos ejecutables después de los cambios de física y audio; luego elegir el segundo modo.")

h("15 Criterios de aceptación y evidencia")
bullets([
    "Un modo está completo cuando permite entrar, jugar una ronda, aplicar reglas correctas, mostrar ganador y reiniciar o volver al hub.",
    "Un modo online está verificado tras host y cliente en procesos separados, incluida desconexión y al menos una prueba de latencia; una compilación correcta no basta.",
    "Arena del Caos pasó pruebas locales de animaciones, ragdoll, escaleras y ronda de seis plazas con bots. Las pruebas de red anteriores cubrieron dos procesos; los cambios más recientes de física y audio aún no se repitieron en dos ejecutables.",
    "La build existente no incluye necesariamente los últimos cambios: no se generó una nueva build por decisión del usuario. La escena en el Editor es la fuente actual para Play.",
    "Antes de distribuir: verificar licencias, volumen de audio, rendimiento, soporte de mando y funcionamiento con jugadores humanos.",
])

doc.add_page_break()
h("16 Checklist de producción")
p("Marcar cada casilla solo después de guardar evidencia de la prueba. [x] significa comprobado con el alcance indicado; [ ] significa trabajo pendiente.")
table(["Estado", "Comprobación", "Evidencia o criterio para cerrar"], [
    ("[x]", "Hub recorrible y portal pirata", "MundoDeInicio abre ArenaDelCaos y permite volver"),
    ("[x]", "Ronda local pirata", "Seis plazas, ganador, reinicio y peligros limpios en Play"),
    ("[x]", "Movimiento y acciones piratas", "Caminar, salto, empuje, agarre, carga, lanzamiento y borda en Play"),
    ("[x]", "Ragdoll temporal y recuperación", "Cuerpos físicos activados por golpe; vuelve Animator y cápsula"),
    ("[x]", "Clips de audio importados", "13 efectos cargados; reproducción técnica de cañón comprobada"),
    ("[x]", "Sala Fusion básica", "Dos procesos conectados en pruebas previas al último cambio físico"),
    ("[ ]", "Audio escuchado y mezclado", "Probar auriculares y altavoces; ajustar niveles por acción y distancia"),
    ("[ ]", "Red tras cambios de física y audio", "Host y cliente en dos ejecutables; comparar ragdoll, sonidos y resultado"),
    ("[ ]", "Latencia y pérdida de paquetes", "Medir respuesta, agarres, objetos y peligros bajo condiciones adversas"),
    ("[ ]", "Seis jugadores humanos y balance", "Partida completa con seis personas; ajustar duración y fuerza"),
    ("[ ]", "Mando y accesibilidad", "Controles, remapeo, volumen y subtítulos de avisos"),
    ("[ ]", "Rendimiento y licencias", "Perfil de CPU/memoria y registro de permisos de cada asset"),
    ("[ ]", "Segundo modo jugable", "Elegir uno de los ocho; escena, reglas, resultado y red probada"),
    ("[ ]", "Metajuego persistente", "Personaje modular, grupo entre escenas, voto y progreso"),
    ("[ ]", "Catálogo completo", "Nueve modos con pruebas locales y online por separado"),
], [.65, 2.25, 3.85])

footer = sec.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.RIGHT
footer.add_run("MULTIVERSE PARTY 3D  |  GDD 4.0  |  ")
field = OxmlElement("w:fldSimple")
field.set(qn("w:instr"), "PAGE")
footer._p.append(field)

doc.core_properties.title = "MULTIVERSE PARTY 3D Documento de diseño actualizado"
doc.core_properties.subject = "Diseño de nueve minijuegos y estado del proyecto"
doc.core_properties.author = "Proyecto MULTIVERSE PARTY 3D"
doc.save(OUTPUT)
print(OUTPUT)
