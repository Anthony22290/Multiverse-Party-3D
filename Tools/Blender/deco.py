"""Decoraciones estilo juguete (chibi low-poly redondeado) para los 5 mapas de MULTIVERSE PARTY 3D.
Uso: python3 deco.py <carpeta_salida>
Salida por mapa:  Deco_<Mapa>.fbx (piezas sueltas en fila, origen en la base de cada una)
                  Diorama_<Mapa>.fbx (mini escenario armado, ~10 m de diámetro)
                  Decoracion_BaseColor.png (atlas 8x8 compartido)
Las partes que brillan van en un objeto hijo "<Pieza>_Brillo" (material emisivo en Unity)."""
import bpy, bmesh, math, os, sys, random
from mathutils import Matrix, Vector, Euler
OUT = sys.argv[1] if len(sys.argv) > 1 else "/home/claude/blender/dout"
os.makedirs(OUT, exist_ok=True)
random.seed(7)

PAL = {  # nombre: color (64 celdas máx.)
    "wood": (.62, .42, .26), "wood_dark": (.42, .27, .17), "wood_light": (.82, .64, .42), "sand": (.96, .86, .6), "water": (.25, .62, .86),
    "water_deep": (.15, .42, .72), "grass": (.45, .76, .35), "grass_dark": (.3, .58, .28), "leaf": (.38, .72, .3), "rock": (.6, .6, .62),
    "rock_dark": (.42, .42, .46), "metal": (.74, .77, .82), "metal_dark": (.36, .39, .45), "gold": (.98, .78, .26), "red": (.9, .26, .24),
    "white": (.96, .95, .92), "black": (.15, .14, .16), "orange": (.98, .55, .18), "yellow": (.99, .85, .25), "blue": (.25, .5, .9),
    "green": (.35, .75, .4), "purple": (.55, .35, .82), "pink": (.95, .5, .7), "cyan": (.35, .9, 1.0), "asphalt": (.3, .31, .35),
    "concrete": (.72, .72, .7), "brick": (.72, .36, .28), "stone": (.6, .57, .52), "stone_dark": (.44, .41, .38), "rust": (.66, .38, .22),
    "teal": (.2, .62, .62), "cream": (.95, .9, .78), "flame": (1.0, .66, .18), "tentacle": (.72, .38, .66), "skin_zombie": (.55, .72, .45),
    "navy": (.2, .24, .42), "paper": (.92, .88, .76), "leather": (.55, .34, .2), "magic": (.45, .95, 1.0), "glass": (.6, .85, .95),
}
CELLS = list(PAL); C = {n: i for i, n in enumerate(CELLS)}; G = 8

def write_atlas(path, size=256):
    from PIL import Image
    img = Image.new("RGB", (size, size), (128, 128, 128)); cs = size // G; px = img.load()
    for i, n in enumerate(CELLS):
        r, g, b = PAL[n]; col, row = i % G, i // G
        for y in range(cs):
            k = .8 + .3 * (y / (cs - 1))
            c = tuple(max(0, min(255, int(255 * v * k))) for v in (r, g, b))
            for x in range(cs): px[col * cs + x, size - 1 - (row * cs + y)] = c
    img.save(path)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- geometría
def _sbox(sx, sy, sz, soft, taper):
    big = max(sx, sy, sz); seg, rings = (20, 12) if big > .8 else (14, 9) if big > .25 else (10, 7)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    e = .18 + .7 * max(0, min(1, soft))
    for v in bm.verts:
        n = v.co.normalized(); q = Vector([math.copysign(abs(c) ** e, c) for c in n])
        if taper: k = max(0, -q.z); q.x *= 1 + (taper[0] - 1) * k; q.y *= 1 + (taper[1] - 1) * k
        v.co = Vector((q.x * sx / 2, q.y * sy / 2, q.z * sz / 2))
    return bm
def _sph(rx, ry, rz, seg=12, rings=8):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0); bmesh.ops.scale(bm, vec=(rx, ry, rz), verts=bm.verts); return bm
def _cyl(r1, r2, h, seg=12):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=h); return bm

PARTS = []      # piezas de la pieza en construcción
def piece(bm, at, cell, rot=(0, 0, 0), glow=False):
    m = Matrix.Translation(Vector(at)) @ Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    uv = bm.loops.layers.uv.verify(); zs = [v.co.z for v in bm.verts]; z0, z1 = min(zs), max(zs); sp = max(1e-4, z1 - z0)
    i = C[cell]; cw = 1 / G; u0, v0 = (i % G) * cw, (i // G) * cw
    for f in bm.faces:
        for l in f.loops: l[uv].uv = (u0 + cw * .5, v0 + cw * (.12 + .76 * (l.vert.co.z - z0) / sp))
    me = bpy.data.meshes.new("p"); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("p", me); scene.collection.objects.link(ob); PARTS.append((ob, glow)); return ob
def box(s, at, cell, soft=.5, taper=None, **k): return piece(_sbox(*s, soft, taper), at, cell, **k)
def ball(r, at, cell, seg=12, rings=8, **k): return piece(_sph(*r, seg, rings), at, cell, **k)
def cyl(r1, r2, h, at, cell, seg=12, **k): return piece(_cyl(r1, r2, h, seg), at, cell, **k)

def _join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active; ob.name = name; ob.data.name = name
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50)); return ob

PROPS = {}   # mapa -> [objetos]
MAT, GLOW = None, None
def prop(mapa, name):
    """Decorador: ejecuta la función que crea piezas y las une en un objeto con origen en (0,0,0)."""
    def deco(fn):
        PARTS.clear(); fn()
        normal = [o for o, g in PARTS if not g]; glow = [o for o, g in PARTS if g]
        ob = _join(normal, name); ob.data.materials.append(MAT)
        if glow:
            gl = _join(glow, name + "_Brillo"); gl.data.materials.append(GLOW); gl.parent = ob
        PROPS.setdefault(mapa, []).append(ob); return fn
    return deco

atlas = os.path.join(OUT, "Decoracion_BaseColor.png"); write_atlas(atlas)
def _mat(name, emit):
    m = bpy.data.materials.new(name); m.use_nodes = True
    t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(atlas)
    b = m.node_tree.nodes["Principled BSDF"]; m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = .85
    if emit: m.node_tree.links.new(t.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 2.5
    return m
MAT, GLOW = _mat("Decoracion", False), _mat("Decoracion_Brillo", True)

# ================================================================ ARENA DEL CAOS (piratas)
A = "Arena"
@prop(A, "Barril")
def _():
    box((.62, .62, .8), (0, 0, .4), "wood", soft=.75, taper=(.86, .86))
    for z in (.14, .66): cyl(.315, .315, .06, (0, 0, z), "metal_dark", seg=16)
@prop(A, "Cofre")
def _():
    box((.9, .6, .45), (0, 0, .23), "wood", soft=.4)
    box((.92, .62, .3), (0, 0, .5), "wood_dark", soft=.8)
    for x in (-.34, .34): box((.08, .64, .74), (x, 0, .37), "gold", soft=.4)
    box((.14, .06, .16), (0, -.32, .42), "gold", soft=.5)
    for i in range(7): cyl(.06, .06, .025, (random.uniform(-.3, .3), random.uniform(-.15, .15), .66 + i * .012), "gold", seg=10, glow=True)
@prop(A, "Canon")
def _():
    for x in (-.32, .32): cyl(.24, .24, .1, (x, .1, .24), "wood_dark", seg=14, rot=(0, 90, 0))
    box((.56, .9, .26), (0, .05, .32), "wood", soft=.5)
    cyl(.2, .16, 1.0, (0, -.25, .62), "metal_dark", seg=14, rot=(-80, 0, 0))
    cyl(.22, .22, .1, (0, -.72, .7), "black", seg=14, rot=(-80, 0, 0))
    ball((.18, .18, .18), (0, .42, .58), "metal_dark")
@prop(A, "Palmera")
def _():
    for i in range(6): box((.3 - i * .02, .3 - i * .02, .5), (math.sin(i * .5) * .12 * i * .3, 0, .25 + i * .45), "wood_light" if i % 2 else "wood", soft=.8, rot=(0, 4 * i, 0))
    top = (math.sin(2.5) * .12 * 1.5, 0, 2.75)
    for k in range(7):
        a = k * 2 * math.pi / 7
        box((.34, 1.5, .08), (top[0] + math.cos(a) * .62, math.sin(a) * .62, top[2] - .12), "leaf" if k % 2 else "grass_dark", soft=.9, taper=(.3, 1), rot=(25, 0, math.degrees(a) - 90))
    for k in range(3): ball((.13, .13, .13), (top[0] + math.cos(k * 2.1) * .14, math.sin(k * 2.1) * .14, top[2] - .2), "wood_dark")
@prop(A, "Ancla")
def _():
    box((.14, .14, 1.2), (0, 0, .75), "metal_dark", soft=.6)
    cyl(.18, .18, .06, (0, 0, 1.42), "metal_dark", seg=14, rot=(90, 0, 0))
    box((.7, .14, .14), (0, 0, 1.15), "metal_dark", soft=.7)
    box((1.0, .16, .16), (0, 0, .18), "metal_dark", soft=.8)
    for x in (-.5, .5): cyl(.11, 0, .3, (x, 0, .32), "metal_dark", seg=8)
@prop(A, "Salvavidas")
def _():
    for i in range(16):
        a = i * 2 * math.pi / 16; ball((.13, .13, .13), (math.cos(a) * .36, 0, .5 + math.sin(a) * .36), "red" if (i // 2) % 2 else "white")
    box((.08, .08, .12), (0, 0, .92), "wood", soft=.5)
@prop(A, "Bandera")
def _():
    cyl(.06, .05, 2.6, (0, 0, 1.3), "wood_dark", seg=10)
    box((.9, .04, .6), (.46, 0, 2.25), "black", soft=.3)
    ball((.12, .03, .11), (.46, -.03, 2.29), "white"); box((.3, .03, .05), (.46, -.03, 2.12), "white", soft=.5, rot=(0, 25, 0)); box((.3, .03, .05), (.46, -.03, 2.12), "white", soft=.5, rot=(0, -25, 0))
    ball((.08, .08, .08), (0, 0, 2.62), "gold")
@prop(A, "Roca")
def _():
    box((1.1, .9, .7), (0, 0, .3), "rock", soft=.85); box((.6, .55, .5), (.35, .2, .55), "rock_dark", soft=.85); box((.5, .5, .32), (-.4, -.3, .12), "rock", soft=.9)
@prop(A, "Cajas")
def _():
    box((.6, .6, .6), (0, 0, .3), "wood_light", soft=.25); box((.5, .5, .5), (.1, .05, .85), "wood", soft=.25, rot=(0, 0, 18)); box((.45, .45, .45), (.62, -.1, .23), "wood", soft=.25, rot=(0, 0, -12))
@prop(A, "Tentaculo")
def _():
    for i in range(8):
        t = i / 7; x = math.sin(t * 2.6) * .5; z = .25 + t * 1.9
        r = .32 * (1 - t * .75); ball((r, r, .3), (x, 0, z), "tentacle", seg=14)
        if i % 2 == 0: ball((r * .45, .05, r * .45), (x, -r * .92, z), "pink")
    ball((.1, .1, .1), (math.sin(2.6) * .5 + .06, 0, 2.2), "tentacle")
@prop(A, "Aleta")
def _():
    box((.12, .9, .9), (0, 0, .3), "metal_dark", soft=.6, taper=(1, 2.2), rot=(18, 0, 0))
    cyl(.6, .7, .04, (0, 0, .02), "white", seg=16)
@prop(A, "Balas")
def _():
    for (x, y, z) in [(-.2, 0, .16), (.2, 0, .16), (0, .3, .16), (0, .12, .44)]: ball((.16, .16, .16), (x, y, z), "black")

# ================================================================ CARRERAS LOCAS
R = "Carreras"
@prop(R, "Cono")
def _():
    box((.5, .5, .06), (0, 0, .03), "orange", soft=.3); cyl(.2, .05, .7, (0, 0, .38), "orange", seg=14)
    cyl(.15, .12, .1, (0, 0, .32), "white", seg=14)
@prop(R, "Neumaticos")
def _():
    for i, (x, y) in enumerate([(-.32, 0), (.32, 0), (0, 0)]):
        for k in range(3 if i < 2 else 1):
            z = .14 + k * .26 if i < 2 else .66
            cyl(.32, .32, .25, (x, y, z), "black", seg=16); cyl(.17, .17, .26, (x, y, z), "red" if (k + i) % 2 else "white", seg=12)
@prop(R, "Meta")
def _():
    for x in (-2.6, 2.6): box((.32, .32, 3.2), (x, 0, 1.6), "red", soft=.6)
    for i in range(14):
        for j in range(2): box((.4, .3, .3), (-2.6 + .4 * i + .2 - .0, 0, 3.0 + j * .3), "white" if (i + j) % 2 else "black", soft=.15)
    box((5.6, .36, .1), (0, 0, 3.5), "red", soft=.5)
@prop(R, "Semaforo")
def _():
    box((.14, .14, 2.2), (0, 0, 1.1), "metal_dark", soft=.6)
    box((.42, .3, 1.0), (0, -.05, 2.2), "black", soft=.6)
    for k, c in enumerate(("red", "yellow", "green")): ball((.13, .07, .13), (0, -.2, 2.52 - k * .3), c, glow=True)
@prop(R, "CajaSorpresa")
def _():
    box((.8, .8, .8), (0, 0, .7), "yellow", soft=.45, glow=False)
    for b in [((.2, .06, .08), (0, -.4, .95)), ((.08, .06, .16), (.12, -.4, .86)), ((.08, .06, .1), (0, -.4, .72)), ((.08, .06, .07), (0, -.4, .55))]: box(b[0], b[1], "white", soft=.5, glow=True)
@prop(R, "Turbo")
def _():
    box((1.5, 2.0, .06), (0, 0, .03), "purple", soft=.3)
    for k in range(3): box((.9, .25, .04), (0, -.6 + k * .55, .07), "cyan", soft=.3, taper=(.2, 1), glow=True)
@prop(R, "Valla")
def _():
    for i in range(6): box((.5, .2, .4), (-1.25 + i * .5, 0, .25), "red" if i % 2 else "white", soft=.35)
    for x in (-1.4, 1.4): box((.12, .3, .1), (x, 0, .05), "metal_dark", soft=.5)
@prop(R, "BanderaCuadros")
def _():
    cyl(.04, .04, 1.8, (0, 0, .9), "metal", seg=8)
    for i in range(4):
        for j in range(3): box((.18, .03, .18), (.12 + i * .18, 0, 1.45 + j * .18), "black" if (i + j) % 2 else "white", soft=.1)
@prop(R, "Tribuna")
def _():
    for k in range(3): box((3.2, .6, .4), (0, k * .6, .2 + k * .4), "concrete", soft=.2)
    box((3.4, .1, 1.2), (0, 1.75, 1.3), "blue", soft=.4)
    for i in range(6): ball((.16, .16, .2), (-1.2 + i * .48, .2 + (i % 3) * .6, .62 + (i % 3) * .4), random.choice(["red", "yellow", "green", "pink", "blue"]))

# ================================================================ INVASIÓN ZOMBI
Z = "Zombis"
@prop(Z, "CocheRoto")
def _():
    box((1.8, 3.2, .7), (0, 0, .6), "teal", soft=.55)
    box((1.5, 1.6, .65), (0, .25, 1.2), "teal", soft=.6, taper=(1.1, 1.15))
    box((1.42, .08, .45), (0, -.56, 1.22), "glass", soft=.4, rot=(-20, 0, 0))
    for x, y in ((-.82, -1.0), (.82, -1.0), (-.82, 1.05), (.82, 1.05)): cyl(.32, .32, .26, (x, y, .3), "black", seg=14, rot=(0, 90, 0))
    box((.08, .9, .55), (1.02, -.3, .65), "teal", soft=.5, rot=(0, 0, -35))
    box((1.7, .14, .2), (0, -1.6, .42), "metal", soft=.6)
    for x in (-.6, .6): ball((.13, .05, .1), (x, -1.62, .7), "yellow", glow=True)
    box((.6, .5, .1), (.3, .6, 1.55), "rust", soft=.8)
@prop(Z, "Bidones")
def _():
    for x, y, r in ((-.32, 0, 0), (.32, 0, 0), (0, .45, 0)):
        cyl(.27, .27, .8, (x, y, .4), "red", seg=14); cyl(.275, .275, .05, (x, y, .3), "black", seg=14); cyl(.275, .275, .05, (x, y, .55), "black", seg=14)
    ball((.27, .27, .27), (.6, -.35, .27), "red", rot=(90, 0, 0))
@prop(Z, "Contenedor")
def _():
    box((2.4, 1.4, 1.3), (0, 0, .65), "green", soft=.3)
    for i in range(8): box((.06, 1.44, 1.2), (-1.05 + i * .3, 0, .65), "grass_dark", soft=.2)
    box((2.44, 1.44, .08), (0, 0, 1.3), "grass_dark", soft=.3, rot=(0, -8, 0))
@prop(Z, "VallaObra")
def _():
    box((2.0, .1, .3), (0, 0, .9), "orange", soft=.35); box((2.0, .1, .3), (0, 0, .45), "white", soft=.35)
    for x in (-.9, .9): box((.1, .1, 1.1), (x, 0, .55), "metal_dark", soft=.5); box((.12, .5, .06), (x, 0, .03), "metal_dark", soft=.5)
    ball((.08, .08, .08), (-.9, 0, 1.15), "orange", glow=True)
@prop(Z, "Hidrante")
def _():
    box((.36, .36, .65), (0, 0, .35), "red", soft=.7); ball((.22, .22, .16), (0, 0, .7), "red")
    for x in (-.22, .22): cyl(.08, .08, .14, (x, 0, .45), "yellow", seg=10, rot=(0, 90, 0))
    cyl(.24, .24, .06, (0, 0, .05), "red", seg=14)
@prop(Z, "Farola")
def _():
    cyl(.08, .06, 3.2, (0, 0, 1.6), "metal_dark", seg=10); box((.12, .9, .12), (0, -.4, 3.2), "metal_dark", soft=.5)
    box((.36, .5, .18), (0, -.85, 3.08), "metal_dark", soft=.6); box((.3, .4, .06), (0, -.85, 2.97), "yellow", soft=.4, glow=True)
    cyl(.22, .26, .1, (0, 0, .05), "metal_dark", seg=12)
@prop(Z, "Stop")
def _():
    box((.08, .08, 2.0), (0, 0, 1.0), "metal", soft=.5); cyl(.38, .38, .06, (0, -.06, 2.0), "red", seg=8, rot=(90, 22.5, 0))
    box((.42, .04, .1), (0, -.1, 2.0), "white", soft=.4)
@prop(Z, "Basura")
def _():
    cyl(.32, .28, .8, (0, 0, .4), "metal", seg=16); cyl(.36, .36, .06, (0, 0, .82), "metal_dark", seg=16, rot=(14, 0, 0))
    for k in range(3): ball((.12, .1, .08), (random.uniform(-.4, .4), random.uniform(-.4, .4), .06), random.choice(["paper", "green", "brown" if False else "wood"]))
@prop(Z, "Suministros")
def _():
    box((.8, .6, .5), (0, 0, .25), "green", soft=.35); box((.82, .62, .08), (0, 0, .5), "grass_dark", soft=.3)
    box((.24, .04, .07), (0, -.31, .27), "white", soft=.3); box((.07, .04, .24), (0, -.31, .27), "white", soft=.3)
@prop(Z, "Neumatico")
def _():
    cyl(.4, .4, .26, (0, 0, .13), "black", seg=18); cyl(.2, .2, .27, (0, 0, .13), "rock_dark", seg=12)
@prop(Z, "ManoZombi")
def _():
    cyl(.5, .55, .1, (0, 0, .05), "wood_dark", seg=12)
    box((.16, .16, .5), (0, 0, .35), "skin_zombie", soft=.9, rot=(0, 12, 0)); ball((.14, .1, .12), (.05, 0, .62), "skin_zombie")
    for k in range(4): box((.04, .04, .16), (-.04 + k * .035, 0, .74), "skin_zombie", soft=.9, rot=(0, -20 + k * 12, 0))

# ================================================================ CÓDIGO MORTAL (estación espacial)
M = "Codigo"
@prop(M, "Terminal")
def _():
    box((.9, .6, 1.0), (0, 0, .5), "metal", soft=.4, taper=(1.1, 1.2))
    box((.8, .4, .1), (0, -.15, 1.02), "navy", soft=.4, rot=(-25, 0, 0))
    for k in range(4): box((.12, .08, .04), (-.27 + k * .18, -.25, 1.06), random.choice(["cyan", "pink", "yellow", "green"]), soft=.4, glow=True)
    box((1.0, .04, .6), (0, .05, 1.65), "cyan", soft=.3, glow=True)
    box((.06, .06, .5), (0, .1, 1.2), "metal_dark", soft=.5)
@prop(M, "Capsula")
def _():
    cyl(.55, .55, .2, (0, 0, .1), "metal_dark", seg=18); cyl(.55, .55, .2, (0, 0, 2.1), "metal_dark", seg=18)
    cyl(.48, .48, 1.8, (0, 0, 1.1), "glass", seg=18, glow=True)
    ball((.18, .18, .22), (0, 0, 1.0), "green")
@prop(M, "CajasCarga")
def _():
    box((.8, .8, .7), (0, 0, .35), "metal", soft=.3); box((.82, .1, .72), (0, -.36, .35), "orange", soft=.3)
    box((.6, .6, .5), (.15, .05, .95), "navy", soft=.3); box((.2, .04, .2), (.15, -.26, .95), "cyan", soft=.3, glow=True)
@prop(M, "Antena")
def _():
    cyl(.3, .4, .2, (0, 0, .1), "metal_dark", seg=14); box((.12, .12, 1.4), (0, 0, .8), "metal", soft=.5)
    cyl(.7, .15, .3, (0, -.2, 1.6), "white", seg=18, rot=(-55, 0, 0)); box((.04, .04, .5), (0, -.48, 1.75), "metal_dark", soft=.5, rot=(-55, 0, 0))
    ball((.07, .07, .07), (0, -.65, 1.9), "red", glow=True)
@prop(M, "Dron")
def _():
    ball((.35, .35, .28), (0, 0, 1.4), "white"); box((.4, .06, .14), (0, -.32, 1.42), "black", soft=.6)
    for x in (-.1, .1): ball((.05, .03, .05), (x, -.36, 1.42), "cyan", glow=True)
    for x in (-.45, .45): cyl(.1, .1, .12, (x, 0, 1.4), "metal_dark", seg=12, rot=(0, 90, 0))
    cyl(.18, 0, .3, (0, 0, .15), "cyan", seg=12, glow=True)
@prop(M, "PlantaAlien")
def _():
    cyl(.35, .28, .4, (0, 0, .2), "metal", seg=14)
    for k in range(5):
        a = k * 1.256; box((.18, .18, .9), (math.cos(a) * .12, math.sin(a) * .12, .75), "purple", soft=.9, taper=(1.4, 1.4), rot=(math.sin(a) * 18, math.cos(a) * 18, 0))
        ball((.1, .1, .1), (math.cos(a) * .28, math.sin(a) * .28, 1.2), "pink", glow=True)
@prop(M, "Compuerta")
def _():
    box((2.4, .4, 2.8), (0, 0, 1.4), "metal_dark", soft=.3)
    box((1.6, .44, 2.2), (0, 0, 1.1), "metal", soft=.3)
    box((.06, .46, 2.2), (0, 0, 1.1), "black", soft=.3)
    for k in range(6): box((.3, .46, .1), (-1.05 if k % 2 else 1.05, 0, .4 + k * .4), "yellow" if k % 2 else "black", soft=.2)
    box((1.4, .46, .1), (0, 0, 2.5), "cyan", soft=.3, glow=True)
@prop(M, "Luces")
def _():
    for k in range(4): cyl(.12, .14, .06, (k * .6 - .9, 0, .03), "metal_dark", seg=12); cyl(.08, .08, .07, (k * .6 - .9, 0, .05), "cyan", seg=12, glow=True)

# ================================================================ SALA DE ESCAPE (torre misteriosa)
E = "Escape"
@prop(E, "Antorcha")
def _():
    cyl(.25, .3, .12, (0, 0, .06), "stone_dark", seg=12); cyl(.07, .06, 1.4, (0, 0, .76), "metal_dark", seg=10)
    cyl(.18, .12, .25, (0, 0, 1.5), "metal_dark", seg=10)
    ball((.14, .14, .22), (0, 0, 1.75), "flame", glow=True); ball((.08, .08, .14), (0, 0, 1.92), "yellow", glow=True)
@prop(E, "Reloj")
def _():
    box((.7, .45, 2.3), (0, 0, 1.15), "wood_dark", soft=.35); box((.8, .5, .2), (0, 0, 2.35), "wood", soft=.5)
    cyl(.27, .27, .05, (0, -.24, 1.85), "cream", seg=18, rot=(90, 0, 0))
    box((.03, .02, .2), (0, -.27, 1.92), "black", soft=.3); box((.15, .02, .03), (.06, -.27, 1.85), "black", soft=.3)
    box((.44, .04, .9), (0, -.22, .9), "glass", soft=.3); cyl(.12, .12, .03, (0, -.21, .65), "gold", seg=14, rot=(90, 0, 0))
@prop(E, "Estanteria")
def _():
    box((1.6, .5, 2.2), (0, 0, 1.1), "wood_dark", soft=.25)
    cols = ["red", "blue", "green", "purple", "yellow", "teal", "brick", "navy"]
    for s in range(4):
        z = .2 + s * .52; box((1.45, .4, .05), (0, -.03, z), "wood", soft=.3)
        x = -.62
        while x < .6:
            w = random.uniform(.08, .14); h = random.uniform(.3, .42)
            box((w, .3, h), (x + w / 2, -.06, z + h / 2 + .03), random.choice(cols), soft=.35, rot=(0, random.choice([0, 0, 0, 12]), 0)); x += w + .015
@prop(E, "Palanca")
def _():
    box((.5, .3, .6), (0, 0, .3), "stone", soft=.5); box((.08, .08, .6), (0, -.05, .75), "metal_dark", soft=.5, rot=(-30, 0, 0))
    ball((.1, .1, .1), (0, -.2, 1.02), "red")
@prop(E, "Espejo")
def _():
    box((.9, .2, 1.6), (0, 0, .95), "gold", soft=.7); box((.72, .22, 1.36), (0, 0, .95), "glass", soft=.6, glow=True)
    for x in (-.35, .35): box((.1, .5, .15), (x, 0, .07), "gold", soft=.7)
@prop(E, "MesaVelas")
def _():
    box((1.4, .8, .1), (0, 0, .8), "wood", soft=.4)
    for x, y in ((-.6, -.3), (.6, -.3), (-.6, .3), (.6, .3)): box((.1, .1, .78), (x, y, .4), "wood_dark", soft=.5)
    for k, x in enumerate((-.35, -.1, .15)):
        h = .18 + k * .06; cyl(.05, .05, h, (x, 0, .85 + h / 2), "cream", seg=10); ball((.04, .04, .06), (x, 0, .9 + h), "flame", glow=True)
    box((.5, .35, .04), (.35, .1, .88), "paper", soft=.3, rot=(0, 0, 15))
@prop(E, "Columna")
def _():
    box((.9, .9, .25), (0, 0, .12), "stone", soft=.4); cyl(.32, .32, 2.6, (0, 0, 1.5), "stone", seg=14); box((.9, .9, .25), (0, 0, 2.9), "stone", soft=.4)
    for k in range(3): box((.5, .3, .3), (random.uniform(-.1, .1), -.2, .7 + k * .7), "leaf", soft=.95)
@prop(E, "Tapiz")
def _():
    box((1.4, .06, .1), (0, 0, 2.2), "wood_dark", soft=.5)
    box((1.1, .04, 1.7), (0, 0, 1.3), "red", soft=.3); box((.9, .05, 1.4), (0, -.01, 1.3), "purple", soft=.3)
    ball((.18, .03, .18), (0, -.04, 1.45), "gold"); box((.04, .03, .4), (0, -.04, 1.05), "gold", soft=.3)
@prop(E, "Llave")
def _():
    cyl(.25, .25, .06, (0, 0, .45), "gold", seg=14, rot=(90, 0, 0)); cyl(.12, .12, .07, (0, 0, .45), "black", seg=12, rot=(90, 0, 0))
    box((.1, .06, .9), (0, 0, .95), "gold", soft=.5); box((.22, .06, .1), (.1, 0, 1.25), "gold", soft=.4); box((.16, .06, .1), (.08, 0, 1.1), "gold", soft=.4)
    cyl(.35, .4, .1, (0, 0, .05), "stone", seg=14)

# ================================================================ SALA DE ESCAPE · props del Art Set (mismas claves que EscapeArtSet)
J = "EscapeJuguete"
@prop(J, "Maceta")
def _():
    box((.6, .6, .55), (0, 0, .28), "brick", soft=.6, taper=(.75, .75)); box((.66, .66, .1), (0, 0, .55), "brick", soft=.7)
    cyl(.27, .27, .04, (0, 0, .58), "wood_dark", seg=14)
    for k in range(5):
        a = k * 1.256; box((.14, .14, .7), (math.cos(a) * .1, math.sin(a) * .1, .9), "leaf" if k % 2 else "grass_dark", soft=.95, taper=(.5, .5), rot=(math.sin(a) * 20, math.cos(a) * 20, 0))
    for k in range(3): ball((.07, .07, .07), (math.cos(k * 2.1) * .2, math.sin(k * 2.1) * .2, 1.12), "pink", glow=False)
@prop(J, "Jardinera")
def _():
    box((1.6, .55, .5), (0, 0, .25), "wood", soft=.45); box((1.66, .6, .08), (0, 0, .5), "wood_dark", soft=.5)
    for i in range(5): ball((.2, .2, .2), (-.6 + i * .3, 0, .62), "leaf" if i % 2 else "grass")
    for i in range(4): ball((.06, .06, .06), (-.45 + i * .3, -.12, .8), "yellow" if i % 2 else "pink")
@prop(J, "Arbusto")
def _():
    ball((.55, .5, .45), (0, 0, .45), "grass_dark", seg=16, rings=10); ball((.4, .38, .36), (.35, .1, .6), "leaf"); ball((.38, .36, .34), (-.3, -.15, .62), "leaf")
    for k in range(4): ball((.06, .06, .06), (math.cos(k * 1.6) * .45, math.sin(k * 1.6) * .4, .75), "red")
@prop(J, "Banco")
def _():
    for k in range(3): box((1.8, .22, .08), (0, -.25 + k * .25, .5), "wood_light" if k % 2 else "wood", soft=.5)
    for k in range(2): box((1.8, .08, .22), (0, .38, .75 + k * .26), "wood", soft=.5)
    for x in (-.75, .75): box((.1, .7, .5), (x, .05, .25), "metal_dark", soft=.5); box((.1, .1, .6), (x, .4, .8), "metal_dark", soft=.5)
@prop(J, "Cajas")
def _():
    box((.7, .7, .7), (0, 0, .35), "wood_light", soft=.25); box((.72, .1, .72), (0, -.31, .35), "wood", soft=.2, rot=(0, 45, 0))
    box((.55, .55, .55), (.1, .05, .98), "wood", soft=.25, rot=(0, 0, 18)); box((.5, .5, .5), (.68, -.1, .25), "wood", soft=.25, rot=(0, 0, -12))
@prop(J, "Guirnalda")
def _():
    for x in (-1.6, 1.6): box((.1, .1, 2.4), (x, 0, 1.2), "wood_dark", soft=.5)
    cols = ["red", "yellow", "blue", "green", "pink", "orange"]
    for i in range(9):
        t = i / 8; x = -1.45 + t * 2.9; z = 2.25 - math.sin(t * math.pi) * .35
        cyl(.14, 0, .3, (x, 0, z - .16), cols[i % len(cols)], seg=3, rot=(0, 0, 0))
    for i in range(16):
        t = i / 15; box((.2, .02, .02), (-1.5 + t * 3.0, 0, 2.3 - math.sin(t * math.pi) * .36), "white", soft=.5)
@prop(J, "Poste indicador")
def _():
    box((.14, .14, 2.0), (0, 0, 1.0), "wood_dark", soft=.6)
    box((.9, .08, .28), (.3, 0, 1.7), "wood_light", soft=.6, taper=(1.0, 1.0)); cyl(.14, 0, .2, (.82, 0, 1.7), "wood_light", seg=4, rot=(0, 90, 0))
    box((.8, .08, .26), (-.25, 0, 1.35), "wood", soft=.6, rot=(0, 0, 12)); cyl(.13, 0, .18, (-.7, 0, 1.25), "wood", seg=4, rot=(0, -90, 0))
    cyl(.25, .3, .12, (0, 0, .06), "stone", seg=12)
@prop(J, "Farola")
def _():
    cyl(.07, .06, 2.4, (0, 0, 1.2), "metal_dark", seg=10); cyl(.22, .26, .12, (0, 0, .06), "metal_dark", seg=12)
    box((.38, .38, .45), (0, 0, 2.6), "glass", soft=.5, glow=True); cyl(.28, .05, .2, (0, 0, 2.92), "metal_dark", seg=8)
    ball((.06, .06, .06), (0, 0, 3.05), "gold")
@prop(J, "Tesoro")
def _():
    box((.9, .6, .45), (0, 0, .23), "wood", soft=.4); box((.92, .62, .3), (0, .1, .62), "wood_dark", soft=.8, rot=(-35, 0, 0))
    for x in (-.34, .34): box((.08, .64, .48), (x, 0, .24), "gold", soft=.4)
    ball((.36, .24, .1), (0, 0, .47), "gold", glow=True)
    for i in range(6): cyl(.06, .06, .025, (random.uniform(-.5, .5), random.uniform(-.5, -.3), .01 + i * .01), "gold", seg=10)
    ball((.07, .07, .07), (.15, -.05, .55), "red", glow=True); ball((.06, .06, .06), (-.15, .02, .55), "cyan", glow=True)
@prop(J, "Antorcha tiki")
def _():
    box((.14, .14, 1.7), (0, 0, .85), "wood", soft=.7, taper=(.8, .8))
    for z in (.5, 1.0): cyl(.09, .09, .06, (0, 0, z), "leather", seg=10)
    cyl(.18, .1, .3, (0, 0, 1.8), "wood_dark", seg=10)
    ball((.13, .13, .22), (0, 0, 2.05), "flame", glow=True); ball((.07, .07, .13), (0, 0, 2.22), "yellow", glow=True)

# ================================================================ exportar piezas y dioramas
def dup(ob, at, rot_z=0, s=1.0):
    d = ob.copy(); scene.collection.objects.link(d); d.location = at; d.rotation_euler = (0, 0, math.radians(rot_z)); d.scale = (s, s, s)
    for ch in ob.children:
        c = ch.copy(); scene.collection.objects.link(c); c.parent = d
    return d

def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        for c in o.children: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="STRIP", bake_anim=False)

def ground(cells_fn, name):
    PARTS.clear(); cells_fn()
    ob = _join([o for o, g in PARTS if not g], name); ob.data.materials.append(MAT)
    gl = [o for o, g in PARTS if g]
    if gl: g = _join(gl, name + "_Brillo"); g.data.materials.append(GLOW); g.parent = ob
    return ob

P = {m: {o.name: o for o in objs} for m, objs in PROPS.items()}
LAYOUT = {
    "Arena": (lambda: (cyl(5.6, 5.8, .3, (0, 0, -.35), "water", seg=40), cyl(4.2, 4.6, .4, (0, 0, -.2), "sand", seg=36),
                       cyl(3.0, 3.0, .2, (0, 0, .0), "wood_light", seg=32), [box((.32, 6.0, .06), (-2.7 + i * .6, 0, .1), "wood" if i % 2 else "wood_light", soft=.2) for i in range(10)]),
              [("Palmera", (-3.6, 1.6, 0), 20), ("Palmera", (3.3, 2.4, 0), -40), ("Canon", (2.2, -1.4, .12), 210), ("Canon", (-2.2, -1.6, .12), 150), ("Barril", (-1.4, 2.3, .12), 0),
               ("Barril", (-.8, 2.6, .12), 30), ("Cofre", (.9, 2.3, .12), 190), ("Ancla", (3.9, -1.0, 0), 60), ("Salvavidas", (2.6, .9, .12), -70), ("Bandera", (0, 3.2, 0), 0),
               ("Roca", (-4.2, -1.6, -.1), 10), ("Cajas", (-2.7, .6, .12), 15), ("Tentaculo", (5.0, 2.2, -.3), -30), ("Tentaculo", (-4.6, 3.0, -.3), 50), ("Aleta", (4.6, -3.0, -.25), 70), ("Balas", (2.9, -.4, .12), 0)]),
    "Carreras": (lambda: (box((10.5, 10.5, .3), (0, 0, -.15), "grass", soft=.15), box((3.6, 10.4, .1), (0, 0, .02), "asphalt", soft=.05),
                          [box((.3, .5, .02), (0, -4.5 + i * 1.3, .08), "white", soft=.1) for i in range(8)],
                          [box((.25, 10.4, .12), (x, 0, .04), "red", soft=.2) for x in (-1.9, 1.9)],
                          [box((.45, .45, .02), (-1.575 + i * .45, -2.0 + j * .45, .085), "white" if (i + j) % 2 else "black", soft=.05) for i in range(8) for j in range(2)]),
                 [("Meta", (0, -1.8, 0), 0), ("Semaforo", (-2.6, -2.4, 0), 0), ("Neumaticos", (2.9, 1.5, 0), 0), ("Neumaticos", (-2.9, 2.6, 0), 30), ("Cono", (1.3, 2.4, .07), 0), ("Cono", (-1.2, 3.4, .07), 0),
                  ("Cono", (1.0, 4.4, .07), 0), ("CajaSorpresa", (.9, 1.0, .07), 20), ("CajaSorpresa", (-.9, 1.0, .07), -10), ("Turbo", (0, 3.6, .02), 0), ("Valla", (3.5, -1.0, 0), 90), ("Valla", (-3.5, -1.0, 0), 90),
                  ("BanderaCuadros", (2.8, -3.2, 0), -20), ("Tribuna", (-3.4, -4.0, 0), 180)]),
    "Zombis": (lambda: (box((10.5, 10.5, .3), (0, 0, -.15), "concrete", soft=.12), box((10.4, 5.0, .08), (0, 0, .01), "asphalt", soft=.05),
                        [box((.9, .18, .02), (-4.2 + i * 1.6, 0, .06), "yellow", soft=.1) for i in range(6)],
                        [box((10.4, .3, .18), (0, y, .06), "stone", soft=.2) for y in (-2.6, 2.6)]),
               [("CocheRoto", (-2.4, -.8, 0), 75), ("Bidones", (2.6, 3.5, .1), 0), ("Contenedor", (-3.0, 3.9, .1), 0), ("VallaObra", (1.8, -1.2, 0), 20), ("Hidrante", (.4, 3.2, .1), 0),
                ("Farola", (4.2, 2.9, .1), 180), ("Farola", (-4.2, -3.1, .1), 0), ("Stop", (3.6, -3.2, .1), 200), ("Basura", (-.6, -3.4, .1), 0), ("Suministros", (1.6, 1.0, 0), 25),
                ("Neumatico", (3.4, .4, 0), 0), ("ManoZombi", (-.6, 1.4, 0), 0), ("ManoZombi", (4.0, -1.2, 0), 90)]),
    "Codigo": (lambda: (box((10.5, 10.5, .3), (0, 0, -.15), "metal_dark", soft=.12),
                        [box((2.4, 2.4, .06), (-3.75 + i * 2.5, -3.75 + j * 2.5, .02), "metal" if (i + j) % 2 else "rock", soft=.15) for i in range(4) for j in range(4)],
                        [box((10.2, .06, .03), (0, y, .07), "cyan", soft=.1, glow=True) for y in (-2.5, 0, 2.5)]),
               [("Terminal", (-2.6, -1.0, .05), 30), ("Terminal", (2.4, -1.4, .05), -30), ("Terminal", (0, 2.2, .05), 180), ("Capsula", (-3.8, 3.0, .05), 0), ("Capsula", (3.9, 3.2, .05), 0),
                ("CajasCarga", (3.9, -3.4, .05), 15), ("CajasCarga", (-4.0, -3.0, .05), -10), ("Antena", (-1.6, 4.0, .05), 160), ("Dron", (1.4, .4, 0), 200), ("PlantaAlien", (2.4, 4.0, .05), 0),
                ("Compuerta", (0, 4.9, .05), 0), ("Luces", (0, -4.2, .05), 0)]),
    "Escape": (lambda: (box((10.5, 10.5, .3), (0, 0, -.15), "stone_dark", soft=.12),
                        [box((1.28, 1.28, .08), (-4.5 + i * 1.3, -4.5 + j * 1.3, .03), "stone" if (i + j) % 2 else "stone_dark", soft=.3) for i in range(8) for j in range(8)],
                        box((10.4, .6, 3.4), (0, 5.0, 1.7), "brick", soft=.12), box((.6, 10.4, 3.4), (-5.0, 0, 1.7), "brick", soft=.12),
                        box((5.0, 3.0, .03), (.6, -.4, .09), "red", soft=.3), box((4.6, 2.6, .03), (.6, -.4, .1), "gold", soft=.3), box((4.3, 2.3, .03), (.6, -.4, .11), "red", soft=.3)),
               [("Estanteria", (-2.4, 4.4, .07), 0), ("Reloj", (1.0, 4.4, .07), 0), ("Tapiz", (3.2, 4.65, .07), 0), ("Antorcha", (-4.4, 3.3, .07), 0), ("Antorcha", (4.2, 4.2, .07), 0),
                ("Antorcha", (-4.4, -2.2, .07), 0), ("Espejo", (-4.5, .6, .07), 90), ("MesaVelas", (1.8, .8, .07), 10), ("Columna", (3.8, -3.6, .07), 0), ("Columna", (-3.3, -3.8, .07), 0),
                ("Palanca", (3.6, 1.6, .07), -90), ("Llave", (-.6, -1.2, .07), 30)]),
}

def flatten(x):
    for i in (x if isinstance(x, (list, tuple)) else [x]):
        if isinstance(i, (list, tuple)): yield from flatten(i)
        elif i is not None: yield i

summary = {}
for mapa, objs in PROPS.items():
    # Pack de piezas: en fila con 1 m de separación (cada una con origen en su base)
    x = 0
    for o in objs:
        w = max((v.co.x for v in o.data.vertices), default=.5) - min((v.co.x for v in o.data.vertices), default=-.5)
        o.location = (x + w / 2, 0, 0); x += w + 1.0
    export(objs, os.path.join(OUT, f"Deco_{mapa}.fbx"))
    # Diorama
    if mapa not in LAYOUT: continue
    PARTS.clear(); LAYOUT[mapa][0]()
    gnd_parts = list(PARTS)
    normal = [o for o, g in gnd_parts if not g]; glow = [o for o, g in gnd_parts if g]
    gnd = _join(normal, f"Suelo_{mapa}"); gnd.data.materials.append(MAT)
    if glow: gg = _join(glow, f"Suelo_{mapa}_Brillo"); gg.data.materials.append(GLOW); gg.parent = gnd
    placed = [gnd] + [dup(P[mapa][n], at, rz) for n, at, rz in LAYOUT[mapa][1]]
    export(placed, os.path.join(OUT, f"Diorama_{mapa}.fbx"))
    summary[mapa] = (len(objs), sum(len(o.data.polygons) for o in placed))
    for o in placed[1:]:
        for c in list(o.children): bpy.data.objects.remove(c)
        bpy.data.objects.remove(o)
    for o in objs: o.hide_render = o.hide_viewport = True
    for o in gnd.children: o.hide_render = True
    gnd.hide_render = True

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Decoracion.blend"))
print("SUMMARY", summary)
