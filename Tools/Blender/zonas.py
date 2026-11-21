"""Edificios y piezas grandes en estilo juguete para MULTIVERSE PARTY 3D (2026-11-17).
 · Zonas_Zombi.fbx      -> los 4 sectores de Invasión Zombi (Gasolinera, Desguace, Almacén, Bomberos)
 · Rodillos_Calabozo.fbx -> rodillos con pinchos, eje central y decoración del minijuego Rodillos del Calabozo
 · Zonas_BaseColor.png  -> atlas 8x8 propio (material Zonas / Zonas_Brillo en Unity)
Convenciones: origen de cada pieza en su base, el frente mira a -Y (Blender). Las partes que brillan van en el hijo
"<Pieza>_Brillo"; las cajas de colisión van en hijos "<Pieza>_Col<n>" (Unity las convierte en BoxCollider).
Uso: python3 zonas.py <carpeta_salida>"""
import bpy, bmesh, math, os, sys, random
from mathutils import Matrix, Vector, Euler
OUT = sys.argv[1] if len(sys.argv) > 1 else "/home/claude/blender/zout"
os.makedirs(OUT, exist_ok=True)
random.seed(11)

PAL = {
    "white": (.96, .95, .92), "cream": (.95, .88, .72), "red": (.9, .22, .2), "red_dark": (.58, .12, .12), "black": (.13, .13, .15),
    "metal": (.74, .77, .82), "metal_dark": (.34, .37, .43), "chrome": (.9, .92, .95), "glass": (.55, .8, .95), "window": (1.0, .82, .45),
    "yellow": (.99, .82, .2), "yellow_dark": (.8, .6, .12), "orange": (.98, .52, .16), "blue": (.22, .48, .9), "blue_dark": (.14, .28, .58),
    "navy": (.18, .22, .38), "teal": (.2, .6, .6), "green": (.3, .72, .38), "green_dark": (.18, .45, .25), "concrete": (.72, .72, .7),
    "concrete_dark": (.5, .5, .5), "asphalt": (.28, .29, .33), "brick": (.72, .3, .24), "brick_dark": (.5, .2, .17), "rust": (.66, .36, .2),
    "rust_light": (.82, .52, .3), "corr": (.55, .66, .74), "corr_dark": (.38, .47, .55), "dirt": (.55, .42, .3), "wood": (.62, .42, .26),
    "wood_dark": (.4, .26, .16), "cardboard": (.8, .62, .4), "rubber": (.2, .2, .22), "gold": (.98, .78, .26), "light": (1.0, .97, .88),
    "siren_red": (1.0, .2, .15), "siren_blue": (.2, .5, 1.0), "price": (.5, 1.0, .4), "lava": (1.0, .45, .1), "lava_hot": (1.0, .85, .3),
    "stone": (.58, .55, .52), "stone_dark": (.38, .36, .35), "stone_light": (.74, .7, .64), "moss": (.42, .55, .3), "purple": (.55, .35, .85),
    "cyan": (.35, .9, 1.0), "bone": (.93, .9, .8), "pink": (.95, .5, .7), "skin_zombie": (.55, .72, .45), "sand": (.9, .8, .58),
    "grey": (.6, .62, .65), "hazard": (.98, .8, .1), "leaf": (.38, .7, .3), "magic": (.6, .4, 1.0),
}
assert len(PAL) <= 64, len(PAL)
CELLS = list(PAL); C = {n: i for i, n in enumerate(CELLS)}; G = 8

def write_atlas(path, size=256):
    from PIL import Image
    img = Image.new("RGB", (size, size), (128, 128, 128)); cs = size // G; px = img.load()
    for i, n in enumerate(CELLS):
        r, g, b = PAL[n]; col, row = i % G, i // G
        for y in range(cs):
            k = .82 + .26 * (y / (cs - 1))
            c = tuple(max(0, min(255, int(255 * v * k))) for v in (r, g, b))
            for x in range(cs): px[col * cs + x, size - 1 - (row * cs + y)] = c
    img.save(path)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- geometría
def _sbox(sx, sy, sz, soft, taper):
    big = max(sx, sy, sz); seg, rings = (18, 10) if big > 1.5 else (14, 9) if big > .4 else (10, 7)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    e = .18 + .7 * max(0, min(1, soft))
    for v in bm.verts:
        n = v.co.normalized(); q = Vector([math.copysign(abs(c) ** e, c) for c in n])
        if taper: k = max(0, -q.z); q.x *= 1 + (taper[0] - 1) * k; q.y *= 1 + (taper[1] - 1) * k
        v.co = Vector((q.x * sx / 2, q.y * sy / 2, q.z * sz / 2))
    return bm
def _cube(sx, sy, sz):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0); bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts); return bm
def _sph(rx, ry, rz, seg=12, rings=8):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0); bmesh.ops.scale(bm, vec=(rx, ry, rz), verts=bm.verts); return bm
def _cyl(r1, r2, h, seg=12):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=h); return bm

PARTS = []; COLS = []
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
def cube(s, at, cell, **k): return piece(_cube(*s), at, cell, **k)
def ball(r, at, cell, seg=12, rings=8, **k): return piece(_sph(*r, seg, rings), at, cell, **k)
def cyl(r1, r2, h, at, cell, seg=12, **k): return piece(_cyl(r1, r2, h, seg), at, cell, **k)
def col(size, at):
    """Caja de colisión (alineada a los ejes de la pieza): size=(x,y,z), at=centro."""
    COLS.append((size, at))

# Fuente de píxeles 5x7 para letreros
FONT = {
    "A": [".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"], "B": ["####.", "#...#", "#...#", "####.", "#...#", "#...#", "####."],
    "C": [".###.", "#...#", "#....", "#....", "#....", "#...#", ".###."], "D": ["####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####."],
    "E": ["#####", "#....", "#....", "####.", "#....", "#....", "#####"], "G": [".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###."],
    "H": ["#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#"], "I": ["###", ".#.", ".#.", ".#.", ".#.", ".#.", "###"],
    "L": ["#....", "#....", "#....", "#....", "#....", "#....", "#####"], "M": ["#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#"],
    "N": ["#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#"], "O": [".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."],
    "P": ["####.", "#...#", "#...#", "####.", "#....", "#....", "#...."], "R": ["####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#"],
    "S": [".####", "#....", "#....", ".###.", "....#", "....#", "####."], "T": ["#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.."],
    "U": ["#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."], "1": [".#.", "##.", ".#.", ".#.", ".#.", ".#.", "###"],
    "2": [".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"], "4": ["...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."],
    "5": ["#####", "#....", "####.", "....#", "....#", "#...#", ".###."], "9": [".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.."],
    ".": [".", ".", ".", ".", ".", ".", "#"], " ": ["..", "..", "..", "..", "..", "..", ".."],
}
def text(s, center, height, cell, glow=True, depth=.06, face=-1):
    """Letras de bloques en el plano XZ, centradas en center, mirando a -Y (face=-1) o +Y (face=1)."""
    px = height / 7; widths = [len(FONT[ch][0]) for ch in s]; total = (sum(widths) + len(s) - 1) * px
    x = -total / 2
    for ch, w in zip(s, widths):
        for r, row in enumerate(FONT[ch]):
            for cidx, v in enumerate(row):
                if v != "#": continue
                lx = x + (cidx + .5) * px
                if face > 0: lx = -lx
                cube((px * .92, depth, px * .92), (center[0] + lx, center[1], center[2] + height / 2 - (r + .5) * px), cell, glow=glow)
        x += (w + 1) * px

def _join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active; ob.name = name; ob.data.name = name
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(45)); return ob

PROPS = {}; MAT = GLOW = None
def prop(pack, name):
    def deco(fn):
        PARTS.clear(); COLS.clear(); fn()
        normal = [o for o, g in PARTS if not g]; glow = [o for o, g in PARTS if g]
        ob = _join(normal, name); ob.data.materials.append(MAT)
        if glow:
            gl = _join(glow, name + "_Brillo"); gl.data.materials.append(GLOW); gl.parent = ob
        for i, (s, at) in enumerate(COLS):
            bm = _cube(*s); bmesh.ops.translate(bm, vec=Vector(at), verts=bm.verts)
            me = bpy.data.meshes.new(f"{name}_Col{i}"); bm.to_mesh(me); bm.free()
            c = bpy.data.objects.new(f"{name}_Col{i}", me); scene.collection.objects.link(c); c.parent = ob
        PROPS.setdefault(pack, []).append(ob); return fn
    return deco

atlas = os.path.join(OUT, "Zonas_BaseColor.png"); write_atlas(atlas)
def _mat(name, emit):
    m = bpy.data.materials.new(name); m.use_nodes = True
    t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(atlas)
    b = m.node_tree.nodes["Principled BSDF"]; m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = .8
    if emit: m.node_tree.links.new(t.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 3
    return m
MAT, GLOW = _mat("Zonas", False), _mat("Zonas_Brillo", True)

def ribs(x0, x1, y, z0, z1, cell, step=.32, depth=.08, along="x"):
    """Chapa ondulada: listones verticales."""
    n = int(abs(x1 - x0) / step)
    for i in range(n + 1):
        t = x0 + (x1 - x0) * i / max(1, n)
        if along == "x": cube((.1, depth, z1 - z0), (t, y, (z0 + z1) / 2), cell)
        else: cube((depth, .1, z1 - z0), (y, t, (z0 + z1) / 2), cell)
def wheel(at, r=.42, w=.3, hub="metal", axis="x"):
    rot = (0, 90, 0) if axis == "x" else (90, 0, 0)
    cyl(r, r, w, at, "rubber", seg=16, rot=rot); cyl(r * .5, r * .5, w + .03, at, hub, seg=10, rot=rot)

# =================================================================== INVASIÓN ZOMBI · LA GASOLINERA
Z = "Zombi"
@prop(Z, "Gasolinera_Techo")
def _():
    cube((13, 9, .45), (0, 0, 4.85), "white")
    cube((13.3, 9.3, .75), (0, 0, 4.45), "red")                       # faldón rojo
    cube((13.4, 9.4, .12), (0, 0, 4.86), "white")                      # filo blanco
    cube((13.36, 9.36, .1), (0, 0, 4.12), "yellow")                    # franja amarilla
    for x in (-4, 0, 4):
        for y in (-2.4, 2.4): cube((2.2, .9, .06), (x, y, 4.04), "light", glow=True)  # focos bajo el techo
    text("GASOLINERA", (0, -4.72, 4.45), .5, "light")
    text("GASOLINERA", (0, 4.72, 4.45), .5, "light", face=1)
    # gota en el techo
    ball((.55, .3, .65), (0, -4.2, 5.75), "red"); cyl(.42, .02, .7, (0, -4.2, 6.35), "red", seg=12)
    ball((.18, .1, .2), (-.18, -4.52, 5.82), "white")
@prop(Z, "Gasolinera_Surtidores")
def _():
    for x in (-5.6, 5.6):
        for y in (-3.4, 3.4):
            cyl(.28, .28, 4.2, (x, y, 2.1), "white", seg=14); cyl(.36, .36, .7, (x, y, .35), "red", seg=14); col((.7, .7, 4.2), (x, y, 2.1))
    for y in (-2.2, 2.2):
        box((6.4, 1.2, .26), (0, y, .13), "concrete", soft=.25)
        for x in (-3.1, 3.1): cube((.2, 1.22, .27), (x, y, .135), "hazard")
        for x in (-3.0, 3.0): cyl(.12, .12, 1.0, (x, y, .75), "hazard", seg=10); cyl(.13, .13, .12, (x, y, .9), "black", seg=10)
        col((6.4, 1.2, .26), (0, y, .13))
        for x in (-1.5, 1.5):
            box((1.0, .62, 1.8), (x, y, 1.16), "red", soft=.3)
            box((1.04, .66, .5), (x, y, 1.86), "white", soft=.3)
            box((1.0, .62, .22), (x, y, 2.18), "red_dark", soft=.4)
            for s in (-1, 1):
                cube((.6, .04, .34), (x, y + s * .33, 1.86), "price", glow=True)
                cube((.16, .06, .3), (x - .3, y + s * .33, 1.25), "black"); box((.12, .14, .22), (x - .3, y + s * .38, 1.35), "rubber", soft=.6)
                for k in range(6):  # manguera
                    t = k / 5; cyl(.035, .035, .26, (x + .32, y + s * (.36 + .18 * math.sin(t * math.pi)), 1.55 - t * 1.0), "rubber", seg=6)
            col((1.05, .7, 2.3), (x, y, 1.15))
@prop(Z, "Gasolinera_Tienda")
def _():
    W, D, H = 10, 6, 3.6
    box((W, D, H), (0, 0, H / 2), "cream", soft=.12)
    box((W + .1, D + .1, .5), (0, 0, .25), "red", soft=.15)
    box((W + .8, D + .8, .35), (0, 0, H + .1), "white", soft=.2)     # alero
    box((W + .9, D + .9, .5), (0, 0, H - .25), "red", soft=.2)
    text("TIENDA", (0, -D / 2 - .5, H - .25), .38, "light")
    for x in (-3.4, 3.4): cube((2.8, .1, 1.7), (x, -D / 2 - .02, 1.65), "window", glow=True)
    for x in (-4.85, -1.95, 1.95, 4.85): cube((.14, .14, 1.9), (x, -D / 2 - .06, 1.65), "white")
    cube((1.5, .12, 2.3), (0, -D / 2 - .03, 1.4), "glass"); cube((1.7, .16, .14), (0, -D / 2 - .06, 2.6), "white")
    cube((.08, .14, 2.3), (0, -D / 2 - .1, 1.4), "metal")
    for i in range(10):  # toldo a rayas
        x = -4.75 + i * 1.055; cube((1.05, 1.0, .1), (x, -D / 2 - .5, 3.0), "red" if i % 2 else "white", rot=(-14, 0, 0))
    box((1.4, .9, 1.0), (-4.2, -D / 2 - .7, .5), "white", soft=.35); cube((1.2, .05, .3), (-4.2, -D / 2 - 1.16, .72), "blue")  # arcón de hielo
    cyl(.3, .26, .9, (4.3, -D / 2 - .6, .45), "green_dark", seg=12); cyl(.32, .32, .08, (4.3, -D / 2 - .6, .92), "black", seg=12)
    box((1.6, 1.2, .8), (2.4, 0, H + .65), "metal", soft=.35); cyl(.35, .35, .1, (2.4, 0, H + 1.07), "metal_dark", seg=14)
    col((W + .2, D + .2, H + .3), (0, 0, (H + .3) / 2))
@prop(Z, "Gasolinera_Totem")
def _():
    for x in (-.9, .9): box((.4, .4, 7.4), (x, 0, 3.7), "metal_dark", soft=.3)
    box((2.6, .7, 2.0), (0, 0, 6.4), "red", soft=.25)
    ball((.55, .25, .7), (0, -.38, 6.25), "white"); cyl(.42, .02, .55, (0, -.38, 6.8), "white", seg=12)
    box((2.6, .66, 3.0), (0, 0, 3.85), "black", soft=.2)
    text("1.99", (0, -.36, 4.75), .5, "hazard"); text("1.45", (0, -.36, 3.95), .5, "price"); text("2.15", (0, -.36, 3.15), .5, "siren_red")
    text("1.99", (0, .36, 4.75), .5, "hazard", face=1); text("1.45", (0, .36, 3.95), .5, "price", face=1)
    cube((2.7, .72, .12), (0, 0, 5.4), "white"); cube((2.7, .72, .12), (0, 0, 2.35), "white")
    box((2.8, 1.0, .5), (0, 0, .25), "concrete", soft=.3)
    col((2.8, 1.0, 7.4), (0, 0, 3.7))
@prop(Z, "Gasolinera_Aire")
def _():
    box((.7, .5, 1.3), (0, 0, .65), "blue", soft=.35); cube((.4, .04, .3), (0, -.26, 1.0), "price", glow=True)
    for k in range(10): a = k * .7; cyl(.035, .035, .25, (math.cos(a) * .25 + .45, math.sin(a) * .2, .4 + k * .06), "rubber", seg=6)
    col((.8, .6, 1.3), (0, 0, .65))
@prop(Z, "Gasolinera_Aceite")
def _():
    box((1.3, 1.1, .16), (0, 0, .08), "wood", soft=.2)
    for j, (n, z) in enumerate(((3, .36), (2, .76), (1, 1.16))):
        for i in range(n):
            for yy in (-.25, .25): cyl(.17, .17, .4, ((i - (n - 1) / 2) * .38, yy, z), ["red", "yellow", "blue"][(i + j) % 3], seg=10)
    col((1.3, 1.1, 1.4), (0, 0, .7))
@prop(Z, "Gasolinera_Cisterna")
def _():
    box((2.4, 2.4, 2.3), (0, -3.4, 1.6), "red", soft=.4)                                  # cabina
    cube((2.0, .1, .9), (0, -4.62, 2.15), "glass")
    for x in (-.7, .7): ball((.15, .06, .12), (x, -4.62, 1.1), "light", glow=True)
    box((2.3, 6.2, .5), (0, .6, .9), "metal_dark", soft=.2)
    cyl(1.15, 1.15, 6.0, (0, .8, 2.2), "chrome", seg=20, rot=(90, 0, 0))
    for y in (-1.8, .8, 3.4): cyl(1.18, 1.18, .14, (0, y, 2.2), "red", seg=20, rot=(90, 0, 0))
    for y in (-3.4, -.6, 2.6): wheel((-1.1, y, .45), .48, .32); wheel((1.1, y, .45), .48, .32)
    cube((.1, 4.0, .5), (-1.17, .8, 2.2), "red"); cube((.1, 4.0, .5), (1.17, .8, 2.2), "red")
    col((2.5, 8.4, 3.4), (0, .4, 1.7))

# =================================================================== EL DESGUACE
CAR = ["red", "blue", "teal", "yellow", "green", "purple", "orange", "white"]
def crushed(at, color, rz=0, s=1.0):
    x, y, z = at
    box((2.0 * s, 4.0 * s, .7), (x, y, z + .35), color, soft=.45, rot=(0, 0, rz))
    box((1.7 * s, 2.0 * s, .3), (x, y + .2, z + .78), color, soft=.6, rot=(0, 0, rz))
    box((.8, .9, .1), (x + .4, y - .8, z + .72), "rust", soft=.8, rot=(0, 0, rz))
    cube((1.6 * s, .06, .18), (x, y - 2.0 * s, z + .42), "glass", rot=(0, 0, rz))
@prop(Z, "Desguace_Pila")
def _():
    cols = random.sample(CAR, 3)
    for k in range(3): crushed((random.uniform(-.15, .15), random.uniform(-.2, .2), k * .78), cols[k], rz=random.uniform(-6, 6))
    for y in (-1.4, 1.4): cyl(.32, .32, .25, (1.0, y, .32), "rubber", seg=12, rot=(0, 90, 0))
    col((2.4, 4.4, 2.4), (0, 0, 1.2))
@prop(Z, "Desguace_Grua")
def _():
    for x in (-1.3, 1.3): box((.8, 4.2, .9), (x, 0, .45), "rubber", soft=.5)
    box((3.0, 3.4, .6), (0, 0, 1.15), "metal_dark", soft=.25)
    box((2.6, 2.6, 2.2), (0, .6, 2.55), "yellow", soft=.25)
    cube((2.0, .1, 1.0), (0, -.72, 2.9), "glass"); box((2.0, 1.4, 1.4), (0, 1.6, 2.2), "yellow_dark", soft=.3)
    ang = math.radians(42); L = 11.5; base = Vector((0, -.4, 3.2)); tip = base + Vector((0, -math.sin(ang) * L, math.cos(ang) * L))
    mid = (base + tip) / 2
    box((.55, .55, L), tuple(mid), "yellow", soft=.3, rot=(math.degrees(ang), 0, 0))
    for i in range(1, 9):
        p = base + (tip - base) * (i / 9); cube((.7, .1, .1), tuple(p), "black", rot=(math.degrees(ang), 0, 0))
    cyl(.35, .35, .7, tuple(tip), "metal_dark", seg=12, rot=(0, 90, 0))
    cyl(.04, .04, 3.0, (tip.x, tip.y, tip.z - 1.7), "black", seg=6)
    cyl(.95, .95, .35, (tip.x, tip.y, tip.z - 3.3), "metal_dark", seg=18)
    cyl(.6, .6, .1, (tip.x, tip.y, tip.z - 3.12), "siren_red", seg=14, glow=True)
    crushed((tip.x, tip.y, tip.z - 4.35), "red", rz=8)
    col((3.2, 4.2, 3.7), (0, 0, 1.85))
@prop(Z, "Desguace_Caseta")
def _():
    W, D, H = 6, 3, 2.8
    box((W, D, H), (0, 0, H / 2 + .3), "corr", soft=.08)
    ribs(-W / 2 + .1, W / 2 - .1, -D / 2 - .03, .4, H + .2, "corr_dark", step=.35)
    for x in (-1.6, 1.4): cube((1.2, .12, .8), (x, -D / 2 - .08, 2.0), "window", glow=True)
    cube((.9, .14, 1.9), (-.1, -D / 2 - .06, 1.3), "blue_dark")
    for k in range(3): cube((1.2, .45, .12), (-.1, -D / 2 - .45 - k * .38, .3 - k * .1 + .0), "metal_dark")
    box((W + .3, D + .3, .2), (0, 0, H + .4), "metal_dark", soft=.2)
    for x in (-2.6, 2.6): cube((.18, .18, 1.6), (x, 0, H + 1.2), "metal_dark")
    box((6.4, .3, 1.1), (0, 0, H + 1.6), "black", soft=.15)
    text("DESGUACE", (0, -.18, H + 1.6), .72, "orange")
    text("DESGUACE", (0, .18, H + 1.6), .72, "orange", face=1)
    for x in (-2.8, -1.8, 1.8, 2.8): cube((.25, .2, .2), (x, -D / 2 - .1, .2), "concrete_dark")
    col((W + .2, D + .2, H + .5), (0, 0, (H + .5) / 2))
@prop(Z, "Desguace_Valla")
def _():
    L = 6
    for x in (-3, 0, 3): cyl(.09, .09, 2.6, (x, 0, 1.3), "metal_dark", seg=8)
    for i in range(int(L / .3)):
        x = -L / 2 + .15 + i * .3; h = 2.3 + .15 * math.sin(i * 1.7)
        cube((.28, .07 if i % 2 else .1, h), (x, 0, h / 2 + .05), "rust" if (i // 4) % 3 == 0 else ("corr" if i % 2 else "corr_dark"))
    for x in (-2.2, 1.1): box((.7, .14, .6), (x, -.04, 1.4), "rust_light", soft=.6)
    col((L, .3, 2.4), (0, 0, 1.2))
@prop(Z, "Desguace_Neumaticos")
def _():
    for layer, n in enumerate((7, 5, 3, 1)):
        for i in range(n):
            a = i * 2 * math.pi / max(1, n) + layer; r = .9 - layer * .28 if n > 1 else 0
            cyl(.42, .42, .28, (math.cos(a) * r, math.sin(a) * r, .15 + layer * .3), "rubber", seg=14); cyl(.2, .2, .29, (math.cos(a) * r, math.sin(a) * r, .15 + layer * .3), "black", seg=10)
    col((2.6, 2.6, 1.3), (0, 0, .65))

# =================================================================== EL ALMACÉN
@prop(Z, "Almacen_Nave")
def _():
    W, D, H = 12, 8, 5.2
    box((W, D, H), (0, 0, H / 2), "corr", soft=.06)
    box((W + .1, D + .1, .6), (0, 0, .3), "concrete_dark", soft=.1)
    for s in (-1, 1):   # tejado a dos aguas
        cube((W + .6, D / 2 + .6, .25), (0, s * D / 4, H + 1.0), "corr_dark", rot=(s * 22, 0, 0))
    cube((W + .2, .2, 1.6), (0, -D / 2 + .02, H + .55), "corr")
    ribs(-W / 2 + .2, W / 2 - .2, -D / 2 - .04, .6, H, "corr_dark", step=.4)
    cube((4.8, .2, 4.4), (-1.5, -D / 2 - .1, 2.2), "hazard")                     # marco de la persiana
    cube((4.2, .24, 4.0), (-1.5, -D / 2 - .1, 2.0), "black")                       # hueco oscuro
    for k in range(8): cube((4.2, .28, .2), (-1.5, -D / 2 - .12, 4.0 - k * .25), "metal")  # persiana medio subida
    for k in range(9): cube((.42, .3, .22), (-3.6 + k * .5, -D / 2 - .14, .2), "black" if k % 2 else "hazard")
    cube((1.0, .16, 2.1), (3.6, -D / 2 - .06, 1.05), "blue_dark"); cube((1.2, .5, .1), (3.6, -D / 2 - .3, 2.25), "metal_dark")
    for x in (-5, -3, 3, 5): cube((1.3, .12, .5), (x, -D / 2 - .06, 4.6), "window", glow=True)
    box((7.6, .3, 1.0), (1.2, -D / 2 - .25, 5.85), "navy", soft=.15)
    text("ALMACEN", (1.2, -D / 2 - .42, 5.85), .66, "hazard")
    for x in (-4.6, 2.0): cube((.5, .3, .25), (x, -D / 2 - .2, 4.5), "light", glow=True)
    for x in (-3.8, .8): box((.4, .5, .6), (x, -D / 2 - .3, .9), "rubber", soft=.5)   # topes del muelle
    col((W + .2, D + .2, H + 1.4), (0, 0, (H + 1.4) / 2))
@prop(Z, "Almacen_Estanteria")
def _():
    L, D, H = 6, 1.3, 3.8
    for x in (-L / 2, 0, L / 2):
        for y in (-D / 2, D / 2): cube((.12, .12, H), (x, y, H / 2), "orange")
    for z in (.15, 1.4, 2.65):
        for y in (-D / 2, D / 2): cube((L, .1, .14), (0, y, z), "blue")
        for x in (-1.5, 1.5):
            box((1.3, 1.1, .14), (x, 0, z + .14), "wood", soft=.2)
            for i in range(random.randint(2, 4)):
                bx = x + random.uniform(-.35, .35); by = random.uniform(-.25, .25); s = random.uniform(.35, .55)
                box((s, s, s * .85), (bx, by, z + .21 + s * .42 + (.0 if i < 2 else s * .85)), "cardboard", soft=.2, rot=(0, 0, random.uniform(-10, 10)))
    col((L + .2, D + .2, H), (0, 0, H / 2))
@prop(Z, "Almacen_Carretilla")
def _():
    box((1.3, 2.0, .9), (0, .3, .7), "yellow", soft=.4)
    box((1.2, .9, .5), (0, .95, 1.25), "black", soft=.5)
    for x in (-.55, .55):
        cube((.08, .08, 2.0), (x, .9, 2.0), "black"); cube((.08, .08, 2.0), (x, -.2, 2.0), "black")
    box((1.3, 1.3, .1), (0, .35, 3.0), "yellow_dark", soft=.3)
    for x in (-.35, .35): cube((.12, .12, 2.6), (x, -.75, 1.3), "metal_dark")
    for x in (-.3, .3): cube((.12, 1.2, .08), (x, -1.4, .3), "metal_dark")
    box((1.2, 1.1, .14), (0, -1.5, .42), "wood", soft=.2)
    for i in range(3): box((.5, .5, .45), (-.27 + (i % 2) * .54, -1.5 + (.0 if i < 2 else .0), .74 + (i // 2) * .46), "cardboard", soft=.2)
    wheel((-.62, .9, .32), .34, .2); wheel((.62, .9, .32), .34, .2); wheel((-.62, -.4, .28), .28, .2); wheel((.62, -.4, .28), .28, .2)
    ball((.12, .12, .08), (0, .3, 3.1), "orange", glow=True)
    col((1.4, 3.2, 3.1), (0, -.2, 1.55))
@prop(Z, "Almacen_Cajas")
def _():
    box((1.4, 1.4, .16), (0, 0, .08), "wood", soft=.2)
    for i in range(4): box((.62, .62, .6), (-.33 + (i % 2) * .66, -.33 + (i // 2) * .66, .47), "cardboard", soft=.15)
    for i in range(2): box((.62, .62, .6), (-.33 + i * .66, 0, 1.08), "cardboard", soft=.15, rot=(0, 0, 8))
    cube((1.3, .02, 1.2), (0, -.71, .78), "glass")
    col((1.4, 1.4, 1.4), (0, 0, .7))

# =================================================================== PARQUE DE BOMBEROS
@prop(Z, "Bomberos_Cuartel")
def _():
    W, D, H = 12, 8, 6.2
    box((W, D, H), (0, 0, H / 2), "brick", soft=.06)
    for z in range(1, 12):  # hileras de ladrillo
        cube((W + .04, D + .04, .05), (0, 0, z * .52), "brick_dark")
    box((W + .5, D + .5, .45), (0, 0, H + .1), "white", soft=.15)
    box((W + .2, D + .2, .5), (0, 0, .25), "concrete", soft=.12)
    for x in (-3, 3):     # portones
        cube((4.6, .2, 4.5), (x, -D / 2 - .05, 2.25), "white")
        for k in range(4): cube((4.0, .24, .95), (x, -D / 2 - .08, .55 + k * 1.0), "red")
        for i in range(4): cube((.8, .26, .5), (x - 1.4 + i * .93, -D / 2 - .1, 3.15), "window", glow=True)
    cube((1.2, .3, 4.8), (0, -D / 2 - .1, 2.4), "brick_dark")
    box((W + .2, .5, 1.1), (0, -D / 2 - .2, 5.2), "red_dark", soft=.12)
    text("BOMBEROS", (0, -D / 2 - .5, 5.2), .78, "hazard")
    # emblema
    cyl(.95, .95, .2, (0, -D / 2 - .3, H + 1.15), "gold", seg=24, rot=(90, 0, 0))
    cyl(.7, .7, .24, (0, -D / 2 - .3, H + 1.15), "red", seg=24, rot=(90, 0, 0))
    for a in (0, 45, 90, 135): cube((1.1, .3, .26), (0, -D / 2 - .35, H + 1.15), "gold", rot=(0, a, 0))
    box((2.4, .6, 1.4), (0, -D / 2 + .2, H + .9), "white", soft=.2)
    for x in (-5.6, 5.6): ball((.22, .22, .22), (x, -D / 2 - .25, 5.0), "siren_red", glow=True)
    for x in (-5.3, -.9, .9, 5.3): cube((.5, .4, .25), (x, -D / 2 - .3, 4.75), "light", glow=True)
    for x in (-4, 0, 4):
        for side in (-1, 1): cube((.08 if True else 0, 1.2, 1.0), (side * (W / 2 + .04), x * .7, 3.6), "window", glow=True)
    col((W + .3, D + .3, H + .4), (0, 0, (H + .4) / 2))
@prop(Z, "Bomberos_Torre")
def _():
    S, H = 3.2, 11
    box((S, S, H), (0, 0, H / 2), "brick", soft=.06)
    for z in range(1, 21): cube((S + .04, S + .04, .05), (0, 0, z * .52), "brick_dark")
    for z in (3.0, 6.0, 8.6):
        for (x, y, sx, sy) in ((0, -S / 2 - .02, .8, .1), (0, S / 2 + .02, .8, .1), (-S / 2 - .02, 0, .1, .8), (S / 2 + .02, 0, .1, .8)):
            cube((sx, sy, 1.2), (x, y, z), "window", glow=True)
    box((S + .6, S + .6, .4), (0, 0, H + .2), "white", soft=.2)
    cyl(2.5, .1, 1.8, (0, 0, H + 1.3), "red_dark", seg=4, rot=(0, 0, 45))
    cyl(.05, .05, 2.4, (0, 0, H + 3.0), "metal", seg=6)
    box((1.0, .05, .6), (.55, 0, H + 3.85), "red", soft=.3)
    ball((.3, .3, .3), (0, -S / 2 - .2, H - .5), "siren_red", glow=True)
    col((S + .2, S + .2, H), (0, 0, H / 2))
@prop(Z, "Bomberos_Camion")
def _():
    box((2.5, 2.4, 2.5), (0, -2.6, 1.6), "red", soft=.35)
    cube((2.1, .1, 1.0), (0, -3.82, 2.15), "glass")
    for x in (-1.26, 1.26): cube((.08, 1.2, .9), (x, -2.8, 2.15), "glass")
    box((2.3, .3, .5), (0, -3.85, .8), "chrome", soft=.4)
    for x in (-.8, .8): ball((.16, .06, .13), (x, -3.85, 1.2), "light", glow=True)
    box((2.5, 5.2, 2.3), (0, 1.2, 1.55), "red", soft=.25)
    cube((2.56, 5.2, .25), (0, 1.2, 1.3), "white")                     # franja blanca
    for y in (-.2, 1.3, 2.8):
        for x in (-1.27, 1.27): cube((.06, 1.2, .9), (x, y, 2.1), "metal")
    for x in (-.45, .45): cube((.1, 6.0, .1), (x, .6, 3.05), "chrome")    # escalera
    for k in range(13): cube((.95, .08, .08), (0, -2.3 + k * .48, 3.05), "chrome")
    for x in (-.7, -.25, .25, .7): cube((.38, .3, .22), (x, -2.4, 3.0), "siren_red" if x < 0 else "siren_blue", glow=True)
    cyl(.5, .5, .5, (0, 3.6, 2.1), "metal_dark", seg=14, rot=(0, 90, 0)); cyl(.55, .55, .3, (0, 3.6, 2.1), "hazard", seg=14, rot=(0, 90, 0))
    for y in (-2.6, .6, 2.5): wheel((-1.15, y, .5), .5, .35, hub="chrome"); wheel((1.15, y, .5), .5, .35, hub="chrome")
    col((2.6, 7.8, 3.2), (0, .1, 1.6))
@prop(Z, "Bomberos_Carrete")
def _():
    for x in (-.45, .45): cube((.08, .9, 1.1), (x, 0, .55), "metal_dark")
    cyl(.5, .5, .8, (0, 0, .75), "red", seg=16, rot=(0, 90, 0)); cyl(.42, .42, .82, (0, 0, .75), "hazard", seg=16, rot=(0, 90, 0))
    for x in (-.5, .5): wheel((x, .35, .2), .2, .08)
    col((1.1, 1.0, 1.3), (0, 0, .65))

# =================================================================== RODILLOS DEL CALABOZO
R = "Rodillos"
def roller(length, band, spikes_cell="metal_dark"):
    """A lo largo de +X desde x=0. Eje de giro: X."""
    cyl(.36, .36, length, (length / 2, 0, 0), "wood_dark", seg=16, rot=(0, 90, 0))
    n = int(length / .55)
    for i in range(n):
        x = .3 + i * .55
        if i % 3 == 0: cyl(.4, .4, .12, (x, 0, 0), band, seg=16, rot=(0, 90, 0), glow=True)
        for k in range(6):
            a = k * math.pi / 3 + (i % 2) * math.pi / 6
            d = Vector((0, math.cos(a), math.sin(a)))
            p = d * .54; rx = math.degrees(math.atan2(d.y, d.z))
            cyl(.1, .015, .38, (x, p.y, p.z), spikes_cell, seg=7, rot=(-rx, 0, 0))
    cyl(.46, .46, .3, (length + .1, 0, 0), "metal", seg=14, rot=(0, 90, 0))
    ball((.22, .22, .22), (length + .3, 0, 0), band, glow=True)
@prop(R, "Rodillo_Bajo")
def _(): roller(9.4, "lava_hot")
@prop(R, "Rodillo_Alto")
def _(): roller(9.4, "cyan", "stone_dark")
@prop(R, "Eje_Central")
def _():
    cyl(1.6, 1.8, .6, (0, 0, .3), "stone_dark", seg=20)
    cyl(1.25, 1.25, 2.8, (0, 0, 2.0), "metal_dark", seg=20)
    for k in range(16):
        a = k * 2 * math.pi / 16; cube((.32, .32, .5), (math.cos(a) * 1.32, math.sin(a) * 1.32, 1.1), "metal", rot=(0, 0, math.degrees(a)))
    cyl(1.32, 1.32, .25, (0, 0, 2.6), "gold", seg=20); cyl(1.32, 1.32, .25, (0, 0, 1.6), "gold", seg=20)
    cyl(.9, .5, 1.0, (0, 0, 3.9), "metal_dark", seg=16)
    ball((.55, .55, .55), (0, 0, 4.5), "lava", glow=True)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4; ball((.22, .1, .3), (math.cos(a) * 1.26, math.sin(a) * 1.26, 2.1), "lava_hot", glow=True)
@prop(R, "Brasero")
def _():
    cyl(.18, .3, 1.0, (0, 0, .5), "metal_dark", seg=10)
    cyl(.6, .35, .45, (0, 0, 1.2), "metal_dark", seg=14)
    for k in range(5): a = k * 1.25; ball((.22, .22, .38), (math.cos(a) * .22, math.sin(a) * .22, 1.6), "lava" if k % 2 else "lava_hot", glow=True)
    ball((.18, .18, .5), (0, 0, 1.9), "lava_hot", glow=True)
@prop(R, "Columna")
def _():
    box((1.6, 1.6, .5), (0, 0, .25), "stone_dark", soft=.3)
    for k in range(5): box((1.1, 1.1, 1.2), (0, 0, .5 + .6 + k * 1.2), "stone" if k % 2 else "stone_light", soft=.35, rot=(0, 0, k * 4))
    box((1.7, 1.7, .5), (0, 0, 6.75), "stone_dark", soft=.3)
    box((.6, .3, .5), (.4, -.6, 2.4), "moss", soft=.8)
@prop(R, "Columna_Rota")
def _():
    box((1.6, 1.6, .5), (0, 0, .25), "stone_dark", soft=.3)
    box((1.1, 1.1, 1.3), (0, 0, 1.1), "stone", soft=.35); box((1.0, 1.0, .9), (.1, 0, 2.15), "stone_light", soft=.5, rot=(8, 0, 12))
    box((1.0, 1.0, .9), (1.4, .6, .45), "stone_light", soft=.5, rot=(80, 0, 30))
@prop(R, "Reja")
def _():
    box((4.2, .8, .6), (0, 0, 4.3), "stone_dark", soft=.3)
    for x in (-2.0, 2.0): box((.7, .8, 4.6), (x, 0, 2.3), "stone", soft=.25)
    for i in range(7): cyl(.06, .06, 3.6, (-1.35 + i * .45, 0, 2.1), "metal_dark", seg=8); cyl(.09, 0, .25, (-1.35 + i * .45, 0, .2), "metal_dark", seg=8)
    for z in (1.2, 2.6): cube((3.0, .1, .1), (0, 0, z), "metal_dark")
    cube((3.0, .3, 3.9), (0, .3, 1.95), "black")
@prop(R, "Craneos")
def _():
    for (x, y, z) in ((0, 0, .25), (.45, .1, .22), (-.42, .15, .22), (.05, .3, .6)):
        ball((.26, .24, .24), (x, y, z), "bone"); box((.18, .14, .1), (x, y - .12, z - .17), "bone", soft=.5)
        for s in (-1, 1): ball((.07, .03, .07), (x + s * .1, y - .23, z + .02), "black")
@prop(R, "Cadenas")
def _():
    for k in range(10): cyl(.12, .12, .05, (0, 0, 3.0 - k * .26), "metal_dark", seg=8, rot=(90 if k % 2 else 0, 0, 90 if k % 2 else 0))
    box((.4, .2, .3), (0, 0, .25), "metal_dark", soft=.4)
@prop(R, "Barril")
def _():
    box((.7, .7, .9), (0, 0, .45), "wood", soft=.75, taper=(.86, .86))
    for z in (.16, .74): cyl(.35, .35, .06, (0, 0, z), "metal_dark", seg=14)

# =================================================================== ARMA TU NAVE · nave por piezas (origen común)
N = "Nave"
@prop(N, "Nave_Chasis")
def _():
    box((1.5, 3.6, 1.1), (0, 0, 1.15), "white", soft=.6)
    box((1.54, 3.0, .22), (0, .1, 1.15), "orange", soft=.5)
    for x in (-.55, .55):
        for y in (-1.1, 1.1):
            cyl(.07, .07, .7, (x, y, .38), "metal_dark", seg=8, rot=(0, (1 if x > 0 else -1) * 12, 0)); cyl(.2, .22, .08, (x * 1.15, y, .04), "metal_dark", seg=10)
    col((1.6, 3.7, 1.7), (0, 0, .85))
@prop(N, "Nave_Cabina")
def _():
    cyl(.72, .08, 1.3, (0, -2.35, 1.15), "red", seg=18, rot=(90, 0, 0))
    ball((.62, .9, .5), (0, -1.1, 1.75), "glass")
    box((1.3, .3, .3), (0, -1.75, 1.15), "white", soft=.6)
    ball((.12, .06, .12), (0, -2.98, 1.15), "light", glow=True)
@prop(N, "Nave_Motor")
def _():
    for x in (-.42, .42):
        cyl(.36, .42, 1.0, (x, 2.2, 1.1), "metal_dark", seg=16, rot=(90, 0, 0))
        cyl(.3, .3, .1, (x, 2.72, 1.1), "lava_hot", seg=16, rot=(90, 0, 0), glow=True)
        cyl(.38, .38, .14, (x, 1.85, 1.1), "orange", seg=16, rot=(90, 0, 0))
    box((1.2, .6, .7), (0, 1.75, 1.1), "metal", soft=.5)
@prop(N, "Nave_AlaIzq")
def _():
    box((1.9, 1.3, .16), (1.55, .3, 1.05), "blue", soft=.4, taper=(1, .55), rot=(0, 0, -8))
    box((.3, .9, .18), (2.45, .55, 1.08), "orange", soft=.5)
    ball((.09, .09, .09), (2.55, .9, 1.12), "siren_red", glow=True)
@prop(N, "Nave_AlaDer")
def _():
    box((1.9, 1.3, .16), (-1.55, .3, 1.05), "blue", soft=.4, taper=(1, .55), rot=(0, 0, 8))
    box((.3, .9, .18), (-2.45, .55, 1.08), "orange", soft=.5)
    ball((.09, .09, .09), (-2.55, .9, 1.12), "green", glow=True)
@prop(N, "Nave_Cola")
def _():
    box((.16, 1.1, 1.3), (0, 1.35, 2.15), "blue", soft=.4, taper=(1, .5), rot=(-15, 0, 0))
    box((1.4, .5, .12), (0, 1.55, 1.65), "white", soft=.5)
    ball((.08, .08, .08), (0, 1.7, 2.8), "light", glow=True)
@prop(N, "Nave_Plataforma")
def _():
    cyl(2.8, 3.0, .14, (0, 0, .07), "concrete", seg=32)
    cyl(2.4, 2.4, .02, (0, 0, .15), "metal_dark", seg=32)
    for k in range(8):
        a = k * math.pi / 4; cube((.5, .16, .03), (math.cos(a) * 2.6, math.sin(a) * 2.6, .155), "hazard", rot=(0, 0, math.degrees(a)))

# =================================================================== ARMA TU NAVE · pueblo
P = "Pueblo"
@prop(P, "Puesto")
def _():
    box((2.6, .7, 1.0), (0, -.45, .5), "wood", soft=.2)
    cube((2.7, .75, .08), (0, -.45, 1.02), "wood_light" if False else "cardboard")
    for x in (-1.25, 1.25):
        for y in (-.8, .55): cube((.12, .12, 2.3), (x, y, 1.15), "wood_dark")
    for i in range(6): cube((.46, 1.7, .08), (-1.15 + i * .46, -.15, 2.35), "red" if i % 2 else "white", rot=(12, 0, 0))
    for i in range(7): ball((.13, .13, .13), (-1.0 + i * .33, -.5 + (i % 2) * .15, 1.17), ["red", "orange", "yellow", "green"][i % 4])
    box((2.4, .5, .6), (0, .55, .3), "wood_dark", soft=.2)
    col((2.7, .8, 1.1), (0, -.45, .55)); col((2.5, .6, .7), (0, .55, .35))
@prop(P, "Pozo")
def _():
    for k in range(12):
        a = k * math.pi / 6; box((.62, .4, .8), (math.cos(a) * .85, math.sin(a) * .85, .4), "stone" if k % 2 else "stone_light", soft=.4, rot=(0, 0, math.degrees(a) + 90))
    cyl(.7, .7, .05, (0, 0, .55), "blue_dark", seg=16)
    for x in (-.95, .95): cube((.14, .14, 2.0), (x, 0, 1.0), "wood_dark")
    cube((2.2, .14, .14), (0, 0, 1.95), "wood_dark")
    for s in (-1, 1): cube((2.4, .8, .1), (0, s * .3, 2.2), "red_dark", rot=(s * 35, 0, 0))
    cyl(.18, .15, .28, (.2, 0, 1.3), "wood", seg=10); cyl(.01, .01, .6, (.2, 0, 1.7), "black", seg=4)
    col((2.0, 2.0, 1.0), (0, 0, .5))
@prop(P, "Carreta")
def _():
    box((1.5, 2.4, .5), (0, 0, .75), "wood", soft=.2)
    for x in (-.8, .8): wheel((x, .2, .5), .5, .12, hub="wood_dark")
    for y in (-1.35, -1.75): cube((.08, .9, .08), ((.35 if y < -1.5 else -.35), y, .65), "wood_dark")
    for i in range(6): box((.6, .5, .4), (-.35 + (i % 2) * .7, -.7 + (i // 2) * .7, 1.15), "yellow_dark" if i % 2 else "yellow", soft=.8)
    col((1.8, 2.6, 1.4), (0, 0, .7))
@prop(P, "Seto")
def _():
    box((4.0, .9, 1.2), (0, 0, .6), "green_dark", soft=.7)
    for i in range(7): ball((.42, .4, .38), (-1.7 + i * .57, (i % 2) * .1 - .05, 1.15 + (i % 3) * .06), "leaf" if i % 2 else "green")
    col((4.0, .9, 1.5), (0, 0, .75))
@prop(P, "Arbol")
def _():
    box((.45, .45, 2.4), (0, 0, 1.2), "wood", soft=.7, taper=(.75, .75))
    for (x, y, z, r) in ((0, 0, 3.0, 1.3), (.8, .3, 2.6, .9), (-.7, -.4, 2.7, .95), (.2, -.6, 3.6, .85), (-.3, .6, 3.4, .9)): ball((r, r, r * .9), (x, y, z), "leaf" if r > 1 else "green", seg=14, rings=9)
    col((.6, .6, 2.2), (0, 0, 1.1))
@prop(P, "Mesa")
def _():
    box((1.5, .95, .1), (0, 0, .76), "wood_light" if False else "wood", soft=.3)
    for x in (-.62, .62):
        for y in (-.36, .36): cube((.09, .09, .72), (x, y, .36), "wood_dark")
    cyl(.12, .1, .2, (.3, .1, .9), "white", seg=10); ball((.14, .14, .08), (-.3, -.1, .84), "red")
    col((1.5, .95, .82), (0, 0, .41))
@prop(P, "Cama")
def _():
    box((1.2, 2.1, .45), (0, 0, .32), "wood_dark", soft=.2)
    box((1.1, 1.9, .2), (0, .05, .62), "white", soft=.5)
    box((1.12, 1.3, .18), (0, .35, .7), "blue", soft=.6)
    box((.8, .4, .2), (0, -.72, .78), "cream", soft=.8)
    cube((1.2, .12, .9), (0, -1.05, .5), "wood_dark")
    col((1.2, 2.2, .9), (0, 0, .45))
@prop(P, "Armario")
def _():
    box((1.3, .65, 2.1), (0, 0, 1.05), "wood", soft=.15)
    cube((.04, .67, 1.8), (0, 0, 1.05), "wood_dark")
    for x in (-.12, .12): ball((.05, .05, .05), (x, -.34, 1.1), "gold")
    col((1.3, .65, 2.1), (0, 0, 1.05))
@prop(P, "Estante")
def _():
    for x in (-.8, .8): cube((.08, .4, 1.8), (x, 0, .9), "wood_dark")
    for z in (.1, .7, 1.3, 1.78): cube((1.66, .4, .06), (0, 0, z), "wood")
    for z in (.13, .73, 1.33):
        for i in range(9): cube((.12, .3, .4 + (i % 3) * .06), (-.68 + i * .17, 0, z + .23), ["red", "blue", "green", "yellow", "purple", "orange"][i % 6])
    col((1.7, .45, 1.8), (0, 0, .9))
@prop(P, "Valla")
def _():
    for x in (-1.4, 0, 1.4): cube((.12, .12, 1.1), (x, 0, .55), "wood_dark")
    for z in (.4, .85): cube((3.0, .08, .14), (0, 0, z), "wood")
    col((3.0, .25, 1.1), (0, 0, .55))
@prop(P, "Fuente")
def _():
    cyl(2.3, 2.4, .6, (0, 0, .3), "stone_light", seg=28)
    cyl(2.0, 2.0, .05, (0, 0, .58), "glass", seg=28, glow=True)
    cyl(.35, .45, 1.4, (0, 0, 1.0), "stone", seg=14)
    cyl(.9, .5, .3, (0, 0, 1.7), "stone_light", seg=18)
    ball((.25, .25, .5), (0, 0, 2.1), "cyan", glow=True)
    col((3.6, 3.6, 1.0), (0, 0, .5))
@prop(P, "Silla")
def _():
    box((.5, .5, .08), (0, 0, .46), "wood", soft=.3)
    for x in (-.2, .2):
        for y in (-.2, .2): cube((.06, .06, .44), (x, y, .22), "wood_dark")
    cube((.5, .06, .5), (0, .22, .72), "wood")
@prop(P, "Barriles")
def _():
    for (x, y, z) in ((-.38, 0, 0), (.38, 0, 0), (0, .55, 0), (0, .2, .9)):
        box((.66, .66, .86), (x, y, z + .43), "wood", soft=.75, taper=(.86, .86)); cyl(.33, .33, .05, (x, y, z + .15), "metal_dark", seg=12); cyl(.33, .33, .05, (x, y, z + .7), "metal_dark", seg=12)
    col((1.5, 1.4, 1.7), (0, .25, .85))

# ------------------------------------------------------------------- export
def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
        for c in o.children: c.select_set(True)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="STRIP", bake_anim=False)
summary = {}
for pack, objs in PROPS.items():
    x = 0
    for o in objs:
        w = max(v.co.x for v in o.data.vertices) - min(v.co.x for v in o.data.vertices)
        o.location = (x + w / 2, 0, 0); x += w + 2.0
    fname = {"Zombi": "Zonas_Zombi.fbx", "Rodillos": "Rodillos_Calabozo.fbx", "Nave": "Nave_Piezas.fbx", "Pueblo": "Pueblo_Props.fbx"}[pack]
    export(objs, os.path.join(OUT, fname))
    summary[pack] = [(o.name, len(o.data.polygons) + sum(len(c.data.polygons) for c in o.children if c.name.endswith("_Brillo"))) for o in objs]
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Zonas.blend"))
for k, v in summary.items(): print("SUMMARY", k, v)
