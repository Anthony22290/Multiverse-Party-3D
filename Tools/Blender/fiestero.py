"""Personaje de fiesta modular (ropa y disfraces chistosos) estilo chibi low-poly (proporciones tipo KayKit), hecho 100% por código con Blender (bpy).

Salida (carpeta out/):
  Aventurero.fbx            malla + esqueleto, todas las piezas de ropa como objetos separados (Categoria_Nombre)
  Aventurero@<Clip>.fbx     una animación por archivo (esqueleto solo)
  Aventurero_BaseColor.png  atlas de paleta 4x4 por defecto (Unity puede regenerarlo con otros colores)

Convenciones:
  - El personaje mira a -Y en Blender (+Z en Unity). L = +X.
  - Huesos rectos hacia arriba y roll 0: la rotación local X = inclinar adelante/atrás, Z = abrir hacia el lado.
  - Cada pieza va pesada al 100 % a un hueso (piel rígida, como los personajes de juguete).
  - UV: cada pieza cae en una celda del atlas 4x4; dentro de la celda, la V sigue la altura de la pieza (degradado).
"""
import bpy, bmesh, math, os, sys
from mathutils import Matrix, Vector, Euler

OUT = sys.argv[sys.argv.index("--out") + 1] if "--out" in sys.argv else "/home/claude/blender/out"
os.makedirs(OUT, exist_ok=True)
NAME = "Fiestero"

# ---------------------------------------------------------------- paleta (debe coincidir con CharacterLook.cs)
CELLS = ["skin", "skin_shade", "dark", "white", "hair", "primary", "secondary", "accent",
         "leather", "metal", "metal_dark", "wood", "gold", "boots", "cheek", "magic"]
C = {n: i for i, n in enumerate(CELLS)}
DEFAULT = {
    "skin": (0.98, 0.78, 0.62), "skin_shade": (0.86, 0.58, 0.46), "dark": (0.16, 0.12, 0.14), "white": (0.97, 0.96, 0.93),
    "hair": (0.45, 0.27, 0.16), "primary": (0.22, 0.52, 0.86), "secondary": (0.93, 0.86, 0.68), "accent": (0.92, 0.35, 0.24),
    "leather": (0.55, 0.34, 0.2), "metal": (0.72, 0.76, 0.82), "metal_dark": (0.38, 0.41, 0.47), "wood": (0.62, 0.42, 0.26),
    "gold": (0.98, 0.78, 0.26), "boots": (0.42, 0.27, 0.18), "cheek": (0.98, 0.56, 0.55), "magic": (0.45, 0.95, 1.0),
}

def write_atlas(path, colors, size=128):
    from PIL import Image
    img = Image.new("RGB", (size, size)); cs = size // 4; px = img.load()
    for i, n in enumerate(CELLS):
        r, g, b = colors[n]; col, row = i % 4, i // 4
        for y in range(cs):
            t = y / (cs - 1)                    # 0 abajo .. 1 arriba dentro de la celda
            k = 0.78 + 0.34 * t                 # abajo más oscuro, arriba más claro
            c = tuple(max(0, min(255, int(255 * v * k))) for v in (r, g, b))
            for x in range(cs):
                px[col * cs + x, size - 1 - (row * cs + y)] = c   # PIL: y=0 arriba
    img.save(path)

# ---------------------------------------------------------------- escena limpia
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ---------------------------------------------------------------- esqueleto
BONES = [  # nombre, cabeza (x,y,z), padre
    ("Root", (0, 0, 0), None), ("Hips", (0, 0, 0.58), "Root"), ("Chest", (0, 0, 0.68), "Hips"), ("Head", (0, 0, 0.99), "Chest"),
    ("UpperArm.L", (0.36, 0, 0.94), "Chest"), ("LowerArm.L", (0.40, 0, 0.74), "UpperArm.L"), ("Hand.L", (0.42, 0, 0.58), "LowerArm.L"),
    ("UpperArm.R", (-0.36, 0, 0.94), "Chest"), ("LowerArm.R", (-0.40, 0, 0.74), "UpperArm.R"), ("Hand.R", (-0.42, 0, 0.58), "LowerArm.R"),
    ("UpperLeg.L", (0.14, 0, 0.55), "Hips"), ("LowerLeg.L", (0.14, 0, 0.32), "UpperLeg.L"), ("Foot.L", (0.14, 0, 0.12), "LowerLeg.L"),
    ("UpperLeg.R", (-0.14, 0, 0.55), "Hips"), ("LowerLeg.R", (-0.14, 0, 0.32), "UpperLeg.R"), ("Foot.R", (-0.14, 0, 0.12), "LowerLeg.R"),
]
arm_data = bpy.data.armatures.new("Armature"); arm = bpy.data.objects.new("Armature", arm_data)
scene.collection.objects.link(arm); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
for n, h, p in BONES:
    b = arm_data.edit_bones.new(n); b.head = h; b.tail = (h[0], h[1], h[2] + 0.1); b.roll = 0
    if p: b.parent = arm_data.edit_bones[p]
bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------- piezas
def _bm_box(sx, sy, sz, bevel, segs=2, taper=None, soft=0.5):
    """Caja redondeada como superelipsoide: soft 0 = casi cubo, 1 = esfera. Sin pellizcos ni caras degeneradas."""
    big = max(sx, sy, sz)
    seg, rings = (24, 14) if big > 0.45 else (16, 10) if big > 0.18 else (12, 8)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    e = 0.18 + 0.7 * max(0.0, min(1.0, soft))
    for v in bm.verts:
        n = v.co.normalized()
        q = Vector([math.copysign(abs(c) ** e, c) for c in n])
        if taper:
            k = max(0.0, -q.z)              # 0 en el centro, 1 en la base
            q.x *= 1 + (taper[0] - 1) * k; q.y *= 1 + (taper[1] - 1) * k
        v.co = Vector((q.x * sx / 2, q.y * sy / 2, q.z * sz / 2))
    return bm

def _bm_sphere(rx, ry, rz, seg=10, rings=6):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=(rx, ry, rz), verts=bm.verts); return bm

def _bm_cyl(r1, r2, h, seg=10):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=r2, depth=h); return bm

def piece(bm, at, bone, cell, rot=(0, 0, 0), pivot=None, sub=0):
    """Coloca la geometría (creada centrada en el origen), le da UV de celda con degradado y la pesa a un hueso."""
    m = Matrix.Translation(Vector(at))
    if any(rot):
        R = Euler([math.radians(a) for a in rot]).to_matrix().to_4x4()
        pv = Vector(pivot) if pivot else Vector(at)
        m = Matrix.Translation(pv) @ R @ Matrix.Translation(Vector(at) - pv)
    bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    uv = bm.loops.layers.uv.verify()
    zs = [v.co.z for v in bm.verts]; z0, z1 = min(zs), max(zs); span = max(1e-4, z1 - z0)
    i = C[cell]; u0, v0 = (i % 4) * 0.25, (i // 4) * 0.25
    for f in bm.faces:
        for l in f.loops:
            t = (l.vert.co.z - z0) / span
            l[uv].uv = (u0 + 0.125 + (l.vert.co.x * 0.02), v0 + 0.25 * (0.12 + 0.76 * t))
    me = bpy.data.meshes.new("p"); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new("p", me); scene.collection.objects.link(ob)
    vg = ob.vertex_groups.new(name=bone); vg.add(list(range(len(me.vertices))), 1.0, "REPLACE")
    if sub:
        md = ob.modifiers.new("S", "SUBSURF"); md.levels = md.render_levels = sub; md.uv_smooth = "NONE"
        dg = bpy.context.evaluated_depsgraph_get()
        nm = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
        ob.modifiers.clear(); old = ob.data; ob.data = nm; bpy.data.meshes.remove(old)
    return ob

def box(size, at, bone, cell, bevel=0.035, segs=2, taper=None, soft=0.5, sub=None, **kw):
    return piece(_bm_box(*size, bevel, segs, taper, soft), at, bone, cell, sub=0, **kw)
def ball(r, at, bone, cell, seg=10, rings=6, sub=0, **kw): return piece(_bm_sphere(*r, max(seg, 12), max(rings, 8)), at, bone, cell, sub=sub, **kw)
def cyl(r1, r2, h, at, bone, cell, seg=10, **kw): return piece(_bm_cyl(r1, r2, h, seg), at, bone, cell, **kw)

def mirror(fn):
    """Llama fn(sign, side) para L (+X) y R (-X) y devuelve todas las piezas."""
    return fn(1, "L") + fn(-1, "R")

ITEMS = {}
def item(name, parts):
    parts = [p for p in parts if p]
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts: p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active; ob.name = name; ob.data.name = name
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(55))
    ob.parent = arm
    mod = ob.modifiers.new("Armature", "ARMATURE"); mod.object = arm
    ITEMS[name] = ob
    return ob

# ---------------------------------------------------------------- cuerpo base
HX, HZ = 0.0, 1.335          # centro de la cabeza
HW, HD, HH = 0.72, 0.66, 0.68
FRONT = -HD / 2              # cara frontal (mira a -Y)

item("Cuerpo_Cabeza", [
    box((HW, HD, HH), (0, 0, HZ), "Head", "skin", soft=0.62, taper=(1.04, 1.0), sub=3),
    ball((0.058, 0.035, 0.08), (-0.15, FRONT + 0.012, HZ - 0.02), "Head", "dark", seg=10, rings=6),
    ball((0.058, 0.035, 0.08), (0.15, FRONT + 0.012, HZ - 0.02), "Head", "dark", seg=10, rings=6),
    ball((0.022, 0.012, 0.024), (-0.13, FRONT - 0.016, HZ + 0.015), "Head", "white", seg=6, rings=4),
    ball((0.022, 0.012, 0.024), (0.17, FRONT - 0.016, HZ + 0.015), "Head", "white", seg=6, rings=4),
    ball((0.055, 0.02, 0.03), (-0.24, FRONT + 0.012, HZ - 0.11), "Head", "cheek", seg=8, rings=4),
    ball((0.055, 0.02, 0.03), (0.24, FRONT + 0.012, HZ - 0.11), "Head", "cheek", seg=8, rings=4),
    ball((0.045, 0.04, 0.035), (0, FRONT - 0.01, HZ - 0.08), "Head", "skin_shade", seg=8, rings=5),
    box((0.11, 0.03, 0.025), (0, FRONT + 0.008, HZ - 0.17), "Head", "dark", bevel=0.01),
    ball((0.06, 0.05, 0.09), (-HW / 2 - 0.01, 0.02, HZ - 0.03), "Head", "skin", seg=8, rings=5),
    ball((0.06, 0.05, 0.09), (HW / 2 + 0.01, 0.02, HZ - 0.03), "Head", "skin", seg=8, rings=5),
    box((0.17, 0.15, 0.08), (0, 0, 0.99), "Head", "skin", bevel=0.03),              # cuello
])
item("Cuerpo_Brazos", mirror(lambda s, k: [
    box((0.17, 0.17, 0.22), (s * 0.385, 0, 0.84), "UpperArm." + k, "skin", taper=(0.88, 0.88), soft=0.9),
    box((0.16, 0.16, 0.19), (s * 0.405, 0, 0.67), "LowerArm." + k, "skin", taper=(0.9, 0.9), soft=0.9),
]))
item("Cuerpo_Manos", mirror(lambda s, k: [
    ball((0.105, 0.1, 0.1), (s * 0.42, 0, 0.53), "Hand." + k, "skin", seg=12, rings=8),
    ball((0.04, 0.04, 0.045), (s * 0.4, -0.08, 0.56), "Hand." + k, "skin", seg=6, rings=4),
]))

# ---------------------------------------------------------------- cejas (siempre, color del pelo)
item("Cuerpo_Cejas", [
    box((0.12, 0.03, 0.03), (-0.15, FRONT - 0.002, HZ + 0.08), "Head", "hair", bevel=0.01, rot=(0, 8, 0)),
    box((0.12, 0.03, 0.03), (0.15, FRONT - 0.002, HZ + 0.08), "Head", "hair", bevel=0.01, rot=(0, -8, 0)),
])

# ---------------------------------------------------------------- utilidades de forma
TOP = HZ + HH / 2
def ring(r, tube, at, bone, cell, n=12, squash=1.0):
    """Donut hecho de bolas solapadas (flotadores, tutús, aros)."""
    out = []
    for i in range(n):
        a = i * 2 * math.pi / n
        out.append(ball((tube, tube, tube * squash), (at[0] + math.cos(a) * r, at[1] + math.sin(a) * r, at[2]), bone, cell, seg=10, rings=7))
    return out
HR = (-0.42, -0.03, 0.53)
def held(parts_local, rot=(72, 0, 0)):
    out = []
    for fn, args, lz, cell, extra in parts_local:
        at = (HR[0] + extra.pop("dx", 0), HR[1] + extra.pop("dy", 0), HR[2] + lz)
        out.append(fn(*args, at, "Hand.R", cell, rot=rot, pivot=HR, **extra))
    return out

# ---------------------------------------------------------------- pelo
item("Pelo_Corto", [
    box((HW + 0.06, HD + 0.06, 0.3), (0, 0.01, TOP - 0.09), "Head", "hair", soft=0.75),
    box((HW + 0.05, 0.2, 0.46), (0, HD / 2 - 0.05, HZ + 0.05), "Head", "hair", soft=0.8),
    ball((0.2, 0.11, 0.12), (-0.15, FRONT + 0.04, TOP - 0.13), "Head", "hair", rot=(0, -18, 0)),
    ball((0.17, 0.1, 0.1), (0.12, FRONT + 0.04, TOP - 0.11), "Head", "hair", rot=(0, 20, 0)),
    ball((0.06, 0.12, 0.13), (-HW / 2 - 0.01, -0.06, HZ + 0.05), "Head", "hair"),
    ball((0.06, 0.12, 0.13), (HW / 2 + 0.01, -0.06, HZ + 0.05), "Head", "hair"),
])
item("Pelo_Afro", [
    ball((0.5, 0.46, 0.38), (0, 0.06, TOP - 0.02), "Head", "hair", seg=16, rings=10),
    ball((0.2, 0.2, 0.2), (-0.36, 0.06, HZ + 0.1), "Head", "hair"), ball((0.2, 0.2, 0.2), (0.36, 0.06, HZ + 0.1), "Head", "hair"),
    ball((0.26, 0.2, 0.22), (0, 0.24, HZ + 0.02), "Head", "hair"),
])
item("Pelo_Cresta", [box((0.14, HD * 0.7, 0.1), (0, 0.02, TOP - 0.01), "Head", "hair", soft=0.9)] +
     [box((0.12, 0.17, 0.34 - abs(i - 2) * 0.05), (0, -0.26 + i * 0.13, TOP + 0.13 - abs(i - 2) * 0.03), "Head", "hair", soft=0.95, taper=(1.3, 1.2), rot=(-14 + i * 7, 0, 0)) for i in range(5)])
item("Pelo_Coleta", [
    box((HW + 0.06, HD + 0.06, 0.3), (0, 0.01, TOP - 0.09), "Head", "hair", soft=0.75),
    box((HW + 0.05, 0.2, 0.48), (0, HD / 2 - 0.05, HZ + 0.04), "Head", "hair", soft=0.8),
    ball((0.3, 0.1, 0.1), (0.02, FRONT + 0.05, TOP - 0.12), "Head", "hair", rot=(0, -8, 0)),
    ball((0.1, 0.1, 0.1), (0, HD / 2 + 0.07, TOP - 0.1), "Head", "accent"),
    ball((0.13, 0.13, 0.16), (0, HD / 2 + 0.14, TOP - 0.28), "Head", "hair"),
    ball((0.1, 0.1, 0.14), (0, HD / 2 + 0.18, TOP - 0.5), "Head", "hair"),
])

# ---------------------------------------------------------------- cabeza (gorros y disfraces)
item("Cabeza_Fiesta", [cyl(0.17, 0.0, 0.44, (0.06, 0, TOP + 0.2), "Head", "primary", seg=12, rot=(0, 14, 0), pivot=(0.06, 0, TOP - 0.02)),
                       cyl(0.15, 0.12, 0.06, (0.075, 0, TOP + 0.06), "Head", "accent", seg=12, rot=(0, 14, 0), pivot=(0.06, 0, TOP - 0.02)),
                       ball((0.07, 0.07, 0.07), (0.16, 0, TOP + 0.43), "Head", "white")])
item("Cabeza_Cono", [cyl(0.3, 0.08, 0.6, (0, 0, TOP + 0.24), "Head", "accent", seg=14),
                     cyl(0.245, 0.215, 0.07, (0, 0, TOP + 0.13), "Head", "white", seg=14),
                     cyl(0.165, 0.14, 0.06, (0, 0, TOP + 0.35), "Head", "white", seg=14),
                     box((0.66, 0.66, 0.06), (0, 0, TOP - 0.04), "Head", "accent", soft=0.3)])
def hood(cell, extra):
    return [box((HW + 0.12, HD + 0.12, 0.3), (0, 0.02, TOP - 0.03), "Head", cell, soft=0.8),
            box((HW + 0.12, 0.16, HH + 0.06), (0, HD / 2 + 0.04, HZ - 0.02), "Head", cell, soft=0.7),
            box((0.1, HD + 0.1, HH + 0.02), (-HW / 2 - 0.05, 0.03, HZ - 0.03), "Head", cell, soft=0.8),
            box((0.1, HD + 0.1, HH + 0.02), (HW / 2 + 0.05, 0.03, HZ - 0.03), "Head", cell, soft=0.8)] + extra
eyes_on = lambda x, z, y: [ball((0.07, 0.06, 0.07), (x, y, z), "Head", "white"), ball((0.035, 0.03, 0.04), (x, y - 0.05, z), "Head", "dark")]
item("Cabeza_Dino", hood("primary",
    [box((0.56, 0.4, 0.2), (0, FRONT - 0.1, TOP + 0.04), "Head", "primary", soft=0.8)] +
    [cyl(0.03, 0.0, 0.07, (x, FRONT - 0.27, TOP - 0.04), "Head", "white", seg=6, rot=(180, 0, 0)) for x in (-0.18, -0.06, 0.06, 0.18)] +
    eyes_on(-0.15, TOP + 0.15, FRONT + 0.0) + eyes_on(0.15, TOP + 0.15, FRONT + 0.0) +
    [cyl(0.09, 0.0, 0.17, (0, 0.05 + i * 0.13, TOP + 0.13 - i * 0.12), "Head", "secondary", seg=6, rot=(-20 - i * 25, 0, 0)) for i in range(4)]))
item("Cabeza_Pollo", hood("white",
    [cyl(0.1, 0.0, 0.2, (0, FRONT - 0.12, TOP - 0.02), "Head", "gold", seg=8, rot=(90, 0, 0)),
     ball((0.06, 0.05, 0.08), (0, FRONT - 0.08, TOP - 0.13), "Head", "accent")] +
    [ball((0.07, 0.09, 0.09), (0, -0.12 + i * 0.12, TOP + 0.12 - abs(i - 1) * 0.03), "Head", "accent") for i in range(3)] +
    eyes_on(-0.2, TOP + 0.02, FRONT + 0.02) + eyes_on(0.2, TOP + 0.02, FRONT + 0.02)))
item("Cabeza_Vaquero", [box((1.0, 0.9, 0.05), (0, 0, TOP - 0.03), "Head", "leather", soft=0.95),
                        box((0.52, 0.46, 0.3), (0, 0.02, TOP + 0.12), "Head", "leather", soft=0.6, taper=(1.05, 1.05)),
                        box((0.54, 0.48, 0.07), (0, 0.02, TOP + 0.02), "Head", "dark", soft=0.6)])
item("Cabeza_Casco", [ball((HW / 2 + 0.1, HD / 2 + 0.1, HH / 2 + 0.08), (0, 0.01, HZ + 0.03), "Head", "primary", seg=18, rings=12),
                      box((0.6, 0.1, 0.24), (0, FRONT - 0.08, HZ + 0.02), "Head", "dark", soft=0.7),
                      box((0.12, 0.62, 0.05), (0, 0.02, TOP + 0.1), "Head", "white", soft=0.6, rot=(0, 0, 0)),
                      box((0.08, 0.08, 0.1), (-HW / 2 - 0.1, -0.05, HZ + 0.02), "Head", "accent", soft=0.6),
                      box((0.08, 0.08, 0.1), (HW / 2 + 0.1, -0.05, HZ + 0.02), "Head", "accent", soft=0.6)])
item("Cabeza_Chef", [cyl(0.3, 0.3, 0.16, (0, 0.02, TOP + 0.04), "Head", "white", seg=14),
                     ball((0.38, 0.36, 0.22), (0, 0.02, TOP + 0.24), "Head", "white", seg=14, rings=9),
                     ball((0.2, 0.2, 0.16), (-0.18, 0, TOP + 0.3), "Head", "white"), ball((0.2, 0.2, 0.16), (0.18, 0.04, TOP + 0.3), "Head", "white")])
item("Cabeza_Corona", [cyl(0.33, 0.33, 0.1, (0, 0, TOP + 0.02), "Head", "gold", seg=16)] +
     [cyl(0.07, 0.0, 0.16, (math.cos(i * 1.2566) * 0.31, math.sin(i * 1.2566) * 0.31, TOP + 0.15), "Head", "gold", seg=6) for i in range(5)] +
     [ball((0.035, 0.02, 0.035), (0, -0.33, TOP + 0.02), "Head", "accent")])

# ---------------------------------------------------------------- cara
EY = FRONT - 0.03; EZ = HZ - 0.02
item("Cara_GafasSol", [box((0.2, 0.05, 0.13), (-0.15, EY, EZ), "Head", "dark", soft=0.6),
                       box((0.2, 0.05, 0.13), (0.15, EY, EZ), "Head", "dark", soft=0.6),
                       box((0.12, 0.03, 0.03), (0, EY, EZ + 0.04), "Head", "dark", soft=0.5),
                       box((0.03, 0.34, 0.03), (-0.34, -0.12, EZ + 0.03), "Head", "dark", soft=0.5),
                       box((0.03, 0.34, 0.03), (0.34, -0.12, EZ + 0.03), "Head", "dark", soft=0.5)])
stache = lambda c: [box((0.17, 0.06, 0.06), (-0.08, FRONT - 0.02, HZ - 0.12), "Head", c, soft=0.95, rot=(0, -15, 0)),
                    box((0.17, 0.06, 0.06), (0.08, FRONT - 0.02, HZ - 0.12), "Head", c, soft=0.95, rot=(0, 15, 0))]
item("Cara_Bigote", stache("hair"))
item("Cara_Broma", [cyl(0.085, 0.085, 0.025, (-0.15, EY - 0.02, EZ), "Head", "dark", seg=14, rot=(90, 0, 0)),
                    cyl(0.085, 0.085, 0.025, (0.15, EY - 0.02, EZ), "Head", "dark", seg=14, rot=(90, 0, 0)),
                    ball((0.07, 0.08, 0.08), (0, FRONT - 0.07, HZ - 0.06), "Head", "skin_shade"),
                    box((0.16, 0.04, 0.05), (-0.15, EY - 0.01, EZ + 0.11), "Head", "dark", soft=0.9),
                    box((0.16, 0.04, 0.05), (0.15, EY - 0.01, EZ + 0.11), "Head", "dark", soft=0.9)] + stache("dark"))
item("Cara_Payaso", [ball((0.07, 0.07, 0.07), (0, FRONT - 0.05, HZ - 0.07), "Head", "accent")])

# ---------------------------------------------------------------- torso
CHEST = (0.6, 0.42, 0.38)
def chest(cell): return box(CHEST, (0, 0, 0.83), "Chest", cell, taper=(0.86, 0.92), soft=0.7)
def sleeves(cell, long=False):
    return mirror(lambda s, k: [box((0.21, 0.21, 0.23), (s * 0.385, 0, 0.85), "UpperArm." + k, cell, soft=0.85, taper=(0.9, 0.9))] +
                  ([box((0.2, 0.2, 0.16), (s * 0.405, 0, 0.68), "LowerArm." + k, cell, soft=0.85)] if long else []))
def belt(c="leather"): return [box((0.58, 0.43, 0.075), (0, 0, 0.66), "Hips", c, soft=0.6)]
item("Torso_Camiseta", [chest("primary"), box((0.24, 0.05, 0.08), (0, -0.2, 0.97), "Chest", "secondary", soft=0.8)] + sleeves("primary"))
item("Torso_Hawaiana", [chest("primary")] + sleeves("primary") +
     [ball((0.05, 0.02, 0.05), (x, -0.215 if y < 0 else 0.215, z), "Chest", c) for (x, y, z, c) in
      [(-0.18, -1, 0.9, "white"), (0.12, -1, 0.78, "accent"), (-0.06, -1, 0.7, "secondary"), (0.2, -1, 0.95, "secondary"), (-0.2, -1, 0.74, "accent"), (0.0, 1, 0.85, "white"), (-0.18, 1, 0.72, "accent"), (0.18, 1, 0.92, "secondary")]] +
     [box((0.12, 0.05, 0.1), (-0.07, -0.2, 0.98), "Chest", "secondary", soft=0.6, rot=(0, 0, 25)), box((0.12, 0.05, 0.1), (0.07, -0.2, 0.98), "Chest", "secondary", soft=0.6, rot=(0, 0, -25))])
item("Torso_Sudadera", [box((0.64, 0.46, 0.4), (0, 0, 0.82), "Chest", "primary", taper=(0.9, 0.95), soft=0.8),
                        box((0.5, 0.22, 0.2), (0, 0.24, 1.0), "Chest", "primary", soft=0.9),
                        box((0.36, 0.05, 0.13), (0, -0.23, 0.72), "Chest", "secondary", soft=0.6),
                        box((0.025, 0.02, 0.14), (-0.06, -0.22, 0.9), "Chest", "white", soft=0.5), box((0.025, 0.02, 0.14), (0.06, -0.22, 0.9), "Chest", "white", soft=0.5)] + sleeves("primary", True))
item("Torso_Piloto", [chest("primary"), box((0.12, 0.05, 0.36), (0, -0.2, 0.83), "Chest", "white", soft=0.5),
                      box((0.07, 0.43, 0.36), (-0.29, 0, 0.83), "Chest", "accent", soft=0.5), box((0.07, 0.43, 0.36), (0.29, 0, 0.83), "Chest", "accent", soft=0.5),
                      box((0.28, 0.22, 0.06), (0, 0, 1.0), "Chest", "white", soft=0.6)] + sleeves("primary", True) + belt("dark"))
item("Torso_Heroe", [chest("primary"), cyl(0.12, 0.12, 0.03, (0, -0.215, 0.86), "Chest", "gold", seg=6, rot=(90, 0, 0)),
                     cyl(0.07, 0.07, 0.03, (0, -0.23, 0.86), "Chest", "accent", seg=6, rot=(90, 0, 0))] + sleeves("primary") + belt("gold"))
item("Torso_Peto", [chest("secondary"), box((0.4, 0.06, 0.26), (0, -0.2, 0.78), "Chest", "primary", soft=0.5),
                    box((0.08, 0.44, 0.06), (-0.16, 0, 0.98), "Chest", "primary", soft=0.5, rot=(0, 0, 0)), box((0.08, 0.44, 0.06), (0.16, 0, 0.98), "Chest", "primary", soft=0.5),
                    ball((0.03, 0.02, 0.03), (-0.16, -0.235, 0.88), "Chest", "gold"), ball((0.03, 0.02, 0.03), (0.16, -0.235, 0.88), "Chest", "gold"),
                    box((0.56, 0.42, 0.12), (0, 0, 0.66), "Hips", "primary", soft=0.6)] + sleeves("secondary"))

# ---------------------------------------------------------------- cintura
item("Cintura_Flotador", ring(0.33, 0.11, (0, 0, 0.66), "Hips", "gold", n=14) +
     [ball((0.12, 0.12, 0.12), (0, -0.48, 0.8), "Hips", "gold"), cyl(0.06, 0.03, 0.12, (0, -0.6, 0.79), "Hips", "accent", seg=8, rot=(90, 0, 0)),
      ball((0.02, 0.02, 0.025), (-0.05, -0.57, 0.84), "Hips", "dark"), ball((0.02, 0.02, 0.025), (0.05, -0.57, 0.84), "Hips", "dark")])
item("Cintura_Tutu", [cyl(0.5, 0.3, 0.1, (0, 0, 0.62), "Hips", "accent", seg=18), cyl(0.45, 0.32, 0.08, (0, 0, 0.67), "Hips", "accent", seg=18)])
item("Cintura_Campeon", [box((0.6, 0.45, 0.1), (0, 0, 0.66), "Hips", "leather", soft=0.5), box((0.26, 0.05, 0.16), (0, -0.23, 0.66), "Hips", "gold", soft=0.5),
                         ball((0.05, 0.02, 0.05), (0, -0.26, 0.66), "Hips", "accent"),
                         box((0.09, 0.05, 0.11), (-0.18, -0.21, 0.66), "Hips", "gold", soft=0.5), box((0.09, 0.05, 0.11), (0.18, -0.21, 0.66), "Hips", "gold", soft=0.5)])

# ---------------------------------------------------------------- piernas y pies
def pelvis(cell): return box((0.52, 0.4, 0.17), (0, 0, 0.6), "Hips", cell, soft=0.8)
item("Piernas_Pantalon", [pelvis("secondary")] + mirror(lambda s, k: [
    box((0.22, 0.24, 0.24), (s * 0.14, 0, 0.44), "UpperLeg." + k, "secondary", taper=(0.9, 0.9), soft=0.9),
    box((0.2, 0.22, 0.22), (s * 0.14, 0, 0.24), "LowerLeg." + k, "secondary", taper=(0.92, 0.92), soft=0.9)]))
item("Piernas_Corto", [pelvis("secondary")] + mirror(lambda s, k: [
    box((0.23, 0.25, 0.2), (s * 0.14, 0, 0.46), "UpperLeg." + k, "secondary", soft=0.9),
    box((0.16, 0.17, 0.24), (s * 0.14, 0, 0.25), "LowerLeg." + k, "skin", soft=0.9)]))
item("Pies_Zapatillas", mirror(lambda s, k: [
    box((0.21, 0.31, 0.05), (s * 0.14, -0.04, 0.025), "Foot." + k, "white", soft=0.6),
    box((0.2, 0.29, 0.13), (s * 0.14, -0.035, 0.09), "Foot." + k, "boots", soft=0.95),
    box((0.21, 0.07, 0.04), (s * 0.14, -0.1, 0.13), "Foot." + k, "white", soft=0.5)]))
item("Pies_Botas", mirror(lambda s, k: [
    box((0.22, 0.31, 0.17), (s * 0.14, -0.045, 0.085), "Foot." + k, "boots", soft=0.95, taper=(1.05, 1.0)),
    box((0.24, 0.26, 0.08), (s * 0.14, 0, 0.2), "LowerLeg." + k, "boots", soft=0.7)]))
item("Pies_Chanclas", mirror(lambda s, k: [
    box((0.22, 0.34, 0.04), (s * 0.14, -0.05, 0.02), "Foot." + k, "boots", soft=0.7),
    box((0.17, 0.26, 0.1), (s * 0.14, -0.04, 0.08), "Foot." + k, "skin", soft=0.95),
    box((0.19, 0.04, 0.05), (s * 0.14, -0.1, 0.1), "Foot." + k, "accent", soft=0.5)]))
item("Pies_Pato", mirror(lambda s, k: [
    box((0.32, 0.52, 0.07), (s * 0.15, -0.14, 0.035), "Foot." + k, "gold", soft=0.85, taper=(0.5, 1.0)),
    box((0.2, 0.24, 0.14), (s * 0.14, 0.0, 0.1), "Foot." + k, "gold", soft=0.95)]))

# ---------------------------------------------------------------- espalda
item("Espalda_Capa", [box((0.62, 0.05, 0.8), (0, 0.25, 0.62), "Chest", "accent", soft=0.3, rot=(8, 0, 0), pivot=(0, 0.23, 1.0)),
                      cyl(0.2, 0.24, 0.07, (0, 0.02, 1.0), "Chest", "accent", seg=10)])
item("Espalda_Mochila", [box((0.44, 0.22, 0.42), (0, 0.32, 0.82), "Chest", "accent", soft=0.8),
                         box((0.46, 0.08, 0.2), (0, 0.42, 0.9), "Chest", "secondary", soft=0.7),
                         box((0.3, 0.06, 0.12), (0, 0.44, 0.72), "Chest", "secondary", soft=0.7)])
item("Espalda_Alas", mirror(lambda s, k: [
    box((0.42, 0.04, 0.3), (s * 0.28, 0.3, 0.98), "Chest", "white", soft=0.9, rot=(0, s * -25, s * 15), pivot=(s * 0.08, 0.26, 0.88)),
    box((0.3, 0.04, 0.2), (s * 0.24, 0.3, 0.76), "Chest", "white", soft=0.9, rot=(0, s * 20, s * 15), pivot=(s * 0.08, 0.26, 0.82))]))
item("Espalda_Cohete", [cyl(0.11, 0.11, 0.42, (x, 0.3, 0.84), "Chest", "metal", seg=12) for x in (-0.13, 0.13)] +
     [cyl(0.0, 0.11, 0.12, (x, 0.3, 1.11), "Chest", "accent", seg=12) for x in (-0.13, 0.13)] +
     [cyl(0.08, 0.1, 0.07, (x, 0.3, 0.6), "Chest", "metal_dark", seg=10) for x in (-0.13, 0.13)] +
     [cyl(0.08, 0.0, 0.2, (x, 0.3, 0.47), "Chest", "magic", seg=8) for x in (-0.13, 0.13)] +
     [box((0.36, 0.1, 0.3), (0, 0.24, 0.84), "Chest", "metal_dark", soft=0.6)])

# ---------------------------------------------------------------- mano (cosas chistosas para pegar)
item("Mano_Pescado", held([(ball, ((0.09, 0.07, 0.26),), 0.16, "metal", {}),
                           (cyl, (0.0, 0.11, 0.12), -0.12, "metal", {"seg": 6}),
                           (ball, ((0.025, 0.025, 0.025),), 0.3, "dark", {"dx": 0.07}),
                           (ball, ((0.025, 0.025, 0.025),), 0.3, "dark", {"dx": -0.07})], rot=(60, 0, 0)))
item("Mano_Baguette", held([(box, ((0.12, 0.12, 0.72),), 0.2, "wood", {"soft": 0.95}),
                            (box, ((0.125, 0.04, 0.04),), 0.32, "gold", {"soft": 0.6, "dy": -0.055}),
                            (box, ((0.125, 0.04, 0.04),), 0.12, "gold", {"soft": 0.6, "dy": -0.055})]))
item("Mano_Martillo", held([(cyl, (0.035, 0.035, 0.5), 0.12, "white", {"seg": 8}),
                            (box, ((0.32, 0.2, 0.2),), 0.42, "accent", {"soft": 0.9}),
                            (box, ((0.05, 0.22, 0.22),), 0.42, "gold", {"soft": 0.9, "dx": 0.17}),
                            (box, ((0.05, 0.22, 0.22),), 0.42, "gold", {"soft": 0.9, "dx": -0.17})]))
item("Mano_PolloGoma", held([(box, ((0.12, 0.1, 0.42),), 0.2, "gold", {"soft": 0.95}),
                             (ball, ((0.07, 0.07, 0.08),), 0.46, "gold", {}),
                             (cyl, (0.03, 0.0, 0.08), 0.46, "accent", {"seg": 6, "dy": -0.08}),
                             (ball, ((0.03, 0.04, 0.04),), 0.53, "accent", {})]))

# ---------------------------------------------------------------- karts (solo con --kart): piezas pesadas al hueso Root
if "--kart" in sys.argv:
    NAME = "Piloto"
    SEAT = 0.34                         # altura de la cadera sentada (Hips baja 0.24 en las animaciones)
    WHEEL = (0, -0.37, 0.64)            # volante, donde caen las manos
    def wheel_set(cell="dark", rf=0.19, rr=0.23):
        out = []
        for x, y, r in ((0.58, -0.62, rf), (-0.58, -0.62, rf), (0.64, 0.5, rr), (-0.64, 0.5, rr)):
            out.append(cyl(r, r, 0.2, (x, y, r), "Root", cell, seg=14, rot=(0, 90, 0)))
            out.append(cyl(r * 0.5, r * 0.5, 0.21, (x, y, r), "Root", "metal", seg=10, rot=(0, 90, 0)))
        return out
    # anillo del volante de pie (eje Y): giramos el anillo plano 90° en X
    def steering_ring():
        parts = []
        for i in range(12):
            a = i * 2 * math.pi / 12
            parts.append(ball((0.035, 0.035, 0.035), (WHEEL[0] + math.cos(a) * 0.14, WHEEL[1] - math.sin(a) * 0.05, WHEEL[2] + math.sin(a) * 0.13), "Root", "dark", seg=8, rings=6))
        return parts + [cyl(0.03, 0.03, 0.4, (0, -0.5, 0.47), "Root", "metal_dark", seg=8, rot=(-38, 0, 0)), ball((0.045, 0.03, 0.045), WHEEL, "Root", "accent")]
    item("Kart_Clasico", wheel_set() + steering_ring() + [
        box((1.0, 1.55, 0.26), (0, 0.0, 0.3), "Root", "primary", soft=0.65),
        box((0.82, 0.62, 0.26), (0, -0.55, 0.42), "Root", "primary", soft=0.8, taper=(1.1, 1.0)),
        box((1.12, 0.22, 0.14), (0, -0.86, 0.26), "Root", "accent", soft=0.8),
        box((0.6, 0.2, 0.52), (0, 0.4, 0.58), "Root", "secondary", soft=0.8),
        box((0.24, 0.9, 0.22), (0.52, 0.05, 0.36), "Root", "accent", soft=0.85), box((0.24, 0.9, 0.22), (-0.52, 0.05, 0.36), "Root", "accent", soft=0.85),
        box((1.05, 0.26, 0.06), (0, 0.72, 0.88), "Root", "accent", soft=0.6), box((0.06, 0.06, 0.32), (0.3, 0.68, 0.7), "Root", "metal_dark", soft=0.5), box((0.06, 0.06, 0.32), (-0.3, 0.68, 0.7), "Root", "metal_dark", soft=0.5),
        cyl(0.07, 0.07, 0.3, (0.22, 0.8, 0.36), "Root", "metal", seg=10, rot=(90, 0, 0)), cyl(0.07, 0.07, 0.3, (-0.22, 0.8, 0.36), "Root", "metal", seg=10, rot=(90, 0, 0)),
        cyl(0.13, 0.13, 0.03, (0, -0.86, 0.48), "Root", "white", seg=14, rot=(80, 0, 0)),
    ])
    item("Kart_Banera", steering_ring() + [
        box((1.04, 1.7, 0.5), (0, 0.04, 0.4), "Root", "white", soft=0.8, taper=(0.9, 0.92)),
        box((1.1, 1.78, 0.08), (0, 0.04, 0.66), "Root", "white", soft=0.9),
        box((0.86, 1.4, 0.06), (0, 0.06, 0.66), "Root", "magic", soft=0.9),
    ] + [ball((0.09, 0.11, 0.09), (x, y, 0.09), "Root", "gold") for x in (-0.42, 0.42) for y in (-0.62, 0.68)] + [
        cyl(0.03, 0.03, 0.9, (0.4, 0.82, 1.05), "Root", "metal", seg=8),
        cyl(0.11, 0.06, 0.08, (0.4, 0.7, 1.5), "Root", "metal", seg=10, rot=(-60, 0, 0)),
        box((0.04, 0.16, 0.04), (0.4, 0.78, 1.5), "Root", "metal", soft=0.6),
        ball((0.13, 0.13, 0.11), (0, -0.72, 0.76), "Root", "gold"), ball((0.08, 0.08, 0.08), (0, -0.78, 0.9), "Root", "gold"),
        cyl(0.04, 0.02, 0.06, (0, -0.86, 0.89), "Root", "accent", seg=8, rot=(90, 0, 0)),
    ] + [ball((0.07, 0.07, 0.05), (x, y, 0.72), "Root", "white") for x, y in ((-0.3, 0.5), (0.25, 0.62), (-0.1, 0.7), (0.32, -0.35))])
    bars = []
    for i in range(6):
        y = -0.7 + i * 0.28
        bars += [box((0.03, 0.03, 0.5), (0.5, y, 0.6), "Root", "metal", soft=0.5), box((0.03, 0.03, 0.5), (-0.5, y, 0.6), "Root", "metal", soft=0.5)]
    for i in range(4):
        x = -0.36 + i * 0.24
        bars += [box((0.03, 0.03, 0.5), (x, -0.76, 0.6), "Root", "metal", soft=0.5)]
    item("Kart_Carrito", wheel_set("dark", 0.12, 0.12) + steering_ring() + bars + [
        box((1.04, 1.56, 0.06), (0, -0.06, 0.36), "Root", "metal", soft=0.5),
        box((1.04, 0.04, 0.04), (0, -0.78, 0.85), "Root", "metal", soft=0.5), box((1.04, 0.04, 0.04), (0, 0.66, 0.85), "Root", "metal", soft=0.5),
        box((0.04, 1.46, 0.04), (0.51, -0.06, 0.85), "Root", "metal", soft=0.5), box((0.04, 1.46, 0.04), (-0.51, -0.06, 0.85), "Root", "metal", soft=0.5),
        box((0.04, 1.46, 0.04), (0.51, -0.06, 0.36), "Root", "metal", soft=0.5), box((0.04, 1.46, 0.04), (-0.51, -0.06, 0.36), "Root", "metal", soft=0.5),
        box((0.9, 0.08, 0.08), (0, 0.86, 1.02), "Root", "accent", soft=0.8),
        box((0.04, 0.04, 0.28), (0.42, 0.76, 0.92), "Root", "metal", soft=0.5, rot=(-30, 0, 0)), box((0.04, 0.04, 0.28), (-0.42, 0.76, 0.92), "Root", "metal", soft=0.5, rot=(-30, 0, 0)),
        box((0.4, 0.3, 0.16), (0.22, -0.4, 0.46), "Root", "accent", soft=0.8), cyl(0.1, 0.1, 0.24, (-0.25, -0.45, 0.5), "Root", "gold", seg=10),
    ])
    # El piloto va sentado: el cuerpo base se queda igual; las animaciones Kart_* bajan la cadera.

# ---------------------------------------------------------------- material (vista previa y FBX)
atlas_path = os.path.join(OUT, NAME + "_BaseColor.png")
write_atlas(atlas_path, DEFAULT)
mat = bpy.data.materials.new(NAME); mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(atlas_path); tex.interpolation = "Closest"
bsdf = mat.node_tree.nodes["Principled BSDF"]; mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
bsdf.inputs["Roughness"].default_value = 0.85
for ob in ITEMS.values(): ob.data.materials.append(mat)

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + ".blend"))
print("ITEMS", len(ITEMS), sorted(ITEMS))
print("TRIS", sum(sum(len(p.vertices) - 2 for p in ob.data.polygons) for ob in ITEMS.values()))
