"""Personaje modular estilo chibi low-poly (proporciones tipo KayKit), hecho 100% por código con Blender (bpy).

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
NAME = "Aventurero"

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

# ---------------------------------------------------------------- pelo
TOP = HZ + HH / 2
item("Pelo_Corto", [
    box((HW + 0.06, HD + 0.06, 0.3), (0, 0.01, TOP - 0.09), "Head", "hair", soft=0.75, sub=3),
    box((HW + 0.05, 0.2, 0.46), (0, HD / 2 - 0.05, HZ + 0.05), "Head", "hair", soft=0.8),
    ball((0.2, 0.11, 0.12), (-0.15, FRONT + 0.04, TOP - 0.13), "Head", "hair", seg=10, rings=6, rot=(0, -18, 0)),
    ball((0.17, 0.1, 0.1), (0.12, FRONT + 0.04, TOP - 0.11), "Head", "hair", seg=10, rings=6, rot=(0, 20, 0)),
    ball((0.12, 0.09, 0.08), (0.28, FRONT + 0.06, TOP - 0.16), "Head", "hair", seg=8, rings=5, rot=(0, 35, 0)),
    ball((0.06, 0.12, 0.13), (-HW / 2 - 0.01, -0.06, HZ + 0.05), "Head", "hair", seg=8, rings=6),
    ball((0.06, 0.12, 0.13), (HW / 2 + 0.01, -0.06, HZ + 0.05), "Head", "hair", seg=8, rings=6),
    ball((0.1, 0.08, 0.14), (0.2, 0.06, TOP + 0.03), "Head", "hair", seg=8, rings=5, rot=(-30, 30, 0)),
])
spikes = []
for i, (x, y, a, b) in enumerate([(-0.2, -0.12, -25, 20), (0, -0.16, 0, 25), (0.2, -0.12, 25, 20), (-0.16, 0.12, -20, -25), (0.16, 0.12, 20, -25), (0, 0.2, 0, -35)]):
    spikes.append(cyl(0.12, 0.0, 0.3, (x, y, TOP + 0.08), "Head", "hair", seg=6, rot=(b, a, 0)))
item("Pelo_Punta", [box((HW + 0.06, HD + 0.06, 0.28), (0, 0.01, TOP - 0.08), "Head", "hair", soft=0.75, sub=3),
                    box((HW + 0.05, 0.16, 0.38), (0, HD / 2 - 0.04, HZ + 0.08), "Head", "hair", bevel=0.07)] + spikes)
item("Pelo_Coleta", [
    box((HW + 0.06, HD + 0.06, 0.3), (0, 0.01, TOP - 0.09), "Head", "hair", soft=0.75, sub=3),
    box((HW + 0.05, 0.2, 0.48), (0, HD / 2 - 0.05, HZ + 0.04), "Head", "hair", soft=0.8),
    ball((0.3, 0.1, 0.1), (0.02, FRONT + 0.05, TOP - 0.12), "Head", "hair", seg=12, rings=6, rot=(0, -8, 0)),
    ball((0.1, 0.1, 0.1), (0, HD / 2 + 0.07, TOP - 0.1), "Head", "accent", seg=8, rings=6),
    ball((0.13, 0.13, 0.16), (0, HD / 2 + 0.14, TOP - 0.28), "Head", "hair", seg=10, rings=7),
    ball((0.1, 0.1, 0.14), (0, HD / 2 + 0.18, TOP - 0.5), "Head", "hair", seg=10, rings=7),
    ball((0.06, 0.06, 0.09), (0, HD / 2 + 0.2, TOP - 0.66), "Head", "hair", seg=8, rings=5),
])

# ---------------------------------------------------------------- sombreros
item("Sombrero_Mago", [
    cyl(0.5, 0.5, 0.05, (0, 0, TOP - 0.02), "Head", "primary", seg=14),
    cyl(0.33, 0.0, 0.62, (0, 0.06, TOP + 0.3), "Head", "primary", seg=10, rot=(-14, 0, 0), pivot=(0, 0, TOP)),
    cyl(0.345, 0.32, 0.08, (0, 0.0, TOP + 0.03), "Head", "accent", seg=10),
    ball((0.06, 0.06, 0.06), (0, 0.24, TOP + 0.58), "Head", "gold", seg=6, rings=4),
])
item("Sombrero_Casco", [
    ball((HW / 2 + 0.06, HD / 2 + 0.06, 0.3), (0, 0, TOP - 0.12), "Head", "metal", seg=12, rings=7),
    cyl(HW / 2 + 0.08, HW / 2 + 0.08, 0.07, (0, 0, TOP - 0.22), "Head", "metal_dark", seg=12),
    box((0.06, 0.05, 0.22), (0, FRONT - 0.07, HZ + 0.05), "Head", "metal_dark", bevel=0.015),
    cyl(0.07, 0.0, 0.32, (-0.4, 0, TOP + 0.02), "Head", "white", seg=7, rot=(0, -45, 0), pivot=(-0.32, 0, TOP - 0.1)),
    cyl(0.07, 0.0, 0.32, (0.4, 0, TOP + 0.02), "Head", "white", seg=7, rot=(0, 45, 0), pivot=(0.32, 0, TOP - 0.1)),
])
item("Sombrero_Panuelo", [
    box((HW + 0.05, HD + 0.05, 0.11), (0, 0, HZ + 0.13), "Head", "accent", bevel=0.18, segs=3),
    ball((0.07, 0.06, 0.07), (0.12, HD / 2 + 0.03, HZ + 0.13), "Head", "accent", seg=8, rings=5),
    box((0.08, 0.04, 0.24), (0.17, HD / 2 + 0.05, HZ + 0.02), "Head", "accent", bevel=0.015, rot=(0, -25, 0)),
    box((0.08, 0.04, 0.2), (0.07, HD / 2 + 0.05, HZ + 0.03), "Head", "accent", bevel=0.015, rot=(0, 20, 0)),
])
item("Sombrero_Gorra", [
    ball((HW / 2 + 0.04, HD / 2 + 0.04, 0.24), (0, 0.02, TOP - 0.1), "Head", "accent", seg=12, rings=7),
    box((0.48, 0.26, 0.04), (0, FRONT - 0.1, TOP - 0.13), "Head", "accent", bevel=0.015, rot=(-8, 0, 0)),
    ball((0.05, 0.05, 0.03), (0, 0, TOP + 0.13), "Head", "white", seg=6, rings=4),
])

# ---------------------------------------------------------------- torso
def belt(): return [box((0.6, 0.44, 0.075), (0, 0, 0.66), "Hips", "leather", bevel=0.02),
                    box((0.1, 0.03, 0.08), (0, -0.225, 0.66), "Hips", "gold", bevel=0.012)]
def sleeves(cell, long=False):
    return mirror(lambda s, k: [box((0.21, 0.21, 0.23), (s * 0.385, 0, 0.85), "UpperArm." + k, cell, soft=0.85, taper=(0.9, 0.9))] +
                  ([box((0.2, 0.2, 0.14), (s * 0.405, 0, 0.68), "LowerArm." + k, cell, bevel=0.05)] if long else []))
CHEST = (0.6, 0.42, 0.38)
item("Torso_Camiseta", [box(CHEST, (0, 0, 0.83), "Chest", "primary", taper=(0.86, 0.92), soft=0.7),
                        box((0.3, 0.05, 0.12), (0, -0.2, 0.95), "Chest", "secondary", bevel=0.02)] + sleeves("primary") + belt())
item("Torso_Armadura", [box(CHEST, (0, 0, 0.83), "Chest", "secondary", taper=(0.86, 0.92), soft=0.7),
                        box((0.5, 0.06, 0.3), (0, -0.215, 0.83), "Chest", "metal", bevel=0.05),
                        box((0.08, 0.03, 0.14), (0, -0.25, 0.85), "Chest", "gold", bevel=0.015),
                        box((0.54, 0.38, 0.1), (0, 0.02, 0.7), "Chest", "metal_dark", bevel=0.03)]
     + mirror(lambda s, k: [ball((0.17, 0.18, 0.12), (s * 0.39, 0, 0.96), "UpperArm." + k, "metal", seg=10, rings=6),
                            box((0.19, 0.19, 0.14), (s * 0.385, 0, 0.84), "UpperArm." + k, "secondary", bevel=0.05),
                            box((0.19, 0.19, 0.11), (s * 0.405, 0, 0.66), "LowerArm." + k, "metal_dark", bevel=0.04)]) + belt())
item("Torso_Tunica", [box(CHEST, (0, 0, 0.83), "Chest", "primary", taper=(0.86, 0.92), soft=0.7),
                      box((0.08, 0.05, 0.3), (0, -0.21, 0.85), "Chest", "accent", bevel=0.015),
                      cyl(0.34, 0.44, 0.38, (0, 0, 0.47), "Hips", "primary", seg=12)] + sleeves("primary", long=True) + belt())
item("Torso_Chaqueta", [box(CHEST, (0, 0, 0.83), "Chest", "secondary", taper=(0.86, 0.92), soft=0.7),
                        box((0.22, 0.06, 0.34), (-0.19, -0.2, 0.83), "Chest", "primary", bevel=0.03),
                        box((0.22, 0.06, 0.34), (0.19, -0.2, 0.83), "Chest", "primary", bevel=0.03),
                        box((0.62, 0.06, 0.36), (0, 0.2, 0.83), "Chest", "primary", bevel=0.03),
                        cyl(0.17, 0.2, 0.1, (0, 0, 1.0), "Chest", "accent", seg=10),
                        box((0.1, 0.05, 0.2), (0.08, -0.2, 0.9), "Chest", "accent", bevel=0.02, rot=(0, 0, 10))]
     + sleeves("secondary") + belt())

# ---------------------------------------------------------------- piernas y pies
def pelvis(cell): return box((0.52, 0.4, 0.17), (0, 0, 0.6), "Hips", cell, soft=0.8)
item("Piernas_Pantalon", [pelvis("secondary")] + mirror(lambda s, k: [
    box((0.22, 0.24, 0.24), (s * 0.14, 0, 0.44), "UpperLeg." + k, "secondary", taper=(0.9, 0.9), soft=0.9),
    box((0.2, 0.22, 0.22), (s * 0.14, 0, 0.24), "LowerLeg." + k, "secondary", taper=(0.92, 0.92), soft=0.9)]))
item("Piernas_Corto", [pelvis("leather")] + mirror(lambda s, k: [
    box((0.23, 0.25, 0.2), (s * 0.14, 0, 0.46), "UpperLeg." + k, "leather", bevel=0.06),
    box((0.16, 0.17, 0.24), (s * 0.14, 0, 0.25), "LowerLeg." + k, "skin", bevel=0.05)]))
item("Pies_Botas", mirror(lambda s, k: [
    box((0.22, 0.31, 0.17), (s * 0.14, -0.045, 0.085), "Foot." + k, "boots", soft=0.95, taper=(1.05, 1.0)),
    box((0.24, 0.26, 0.08), (s * 0.14, 0, 0.2), "LowerLeg." + k, "boots", bevel=0.03)]))
item("Pies_Zapatillas", mirror(lambda s, k: [
    box((0.21, 0.3, 0.04), (s * 0.14, -0.04, 0.02), "Foot." + k, "white", bevel=0.015),
    box((0.2, 0.29, 0.13), (s * 0.14, -0.035, 0.09), "Foot." + k, "accent", soft=0.95),
    box((0.21, 0.06, 0.04), (s * 0.14, -0.1, 0.12), "Foot." + k, "white", bevel=0.01)]))

# ---------------------------------------------------------------- espalda
item("Espalda_Capa", [box((0.62, 0.05, 0.8), (0, 0.25, 0.62), "Chest", "accent", bevel=0.02, rot=(8, 0, 0), pivot=(0, 0.23, 1.0)),
                      cyl(0.2, 0.24, 0.07, (0, 0.02, 1.0), "Chest", "gold", seg=10)])
item("Espalda_Mochila", [box((0.44, 0.22, 0.42), (0, 0.32, 0.82), "Chest", "leather", bevel=0.06),
                         box((0.46, 0.08, 0.2), (0, 0.42, 0.9), "Chest", "secondary", bevel=0.03),
                         cyl(0.08, 0.08, 0.5, (0, 0.3, 1.08), "Chest", "primary", seg=8, rot=(0, 90, 0))])

# ---------------------------------------------------------------- armas (mano derecha, x = -0.42) y escudo (mano izquierda)
HR = (-0.42, -0.03, 0.53)
def held(parts_local, rot):
    """parts_local: (fn, args, local_at) construidas a lo largo de +Z desde la mano; se giran alrededor de la mano."""
    out = []
    for fn, args, lz, cell, extra in parts_local:
        at = (HR[0], HR[1], HR[2] + lz)
        out.append(fn(*args, at, "Hand.R", cell, rot=rot, pivot=HR, **extra))
    return out
FWD = (72, 0, 0)  # de vertical a adelante-abajo
item("Arma_Espada", held([(cyl, (0.03, 0.03, 0.18), 0.0, "leather", {"seg": 8}),
                          (box, ((0.24, 0.07, 0.05),), 0.11, "gold", {"bevel": 0.015}),
                          (box, ((0.1, 0.03, 0.56),), 0.41, "metal", {"bevel": 0.012}),
                          (cyl, (0.05, 0.0, 0.1, ), 0.74, "metal", {"seg": 4}),
                          (ball, ((0.045, 0.045, 0.045),), -0.11, "gold", {"seg": 6, "rings": 4})], FWD))
item("Arma_Martillo", held([(cyl, (0.035, 0.035, 0.6), 0.2, "wood", {"seg": 8}),
                            (box, ((0.34, 0.2, 0.2),), 0.52, "metal_dark", {"bevel": 0.04}),
                            (box, ((0.36, 0.22, 0.05),), 0.52, "gold", {"bevel": 0.012})], FWD))
item("Arma_Baston", held([(cyl, (0.035, 0.03, 1.1), 0.25, "wood", {"seg": 8}),
                          (cyl, (0.06, 0.06, 0.06), 0.76, "gold", {"seg": 8}),
                          (ball, ((0.11, 0.11, 0.11),), 0.88, "magic", {"seg": 10, "rings": 6})], (12, 0, 0)))
item("Arma_Hacha", held([(cyl, (0.033, 0.033, 0.62), 0.2, "wood", {"seg": 8}),
                         (box, ((0.05, 0.3, 0.24),), 0.45, "metal", {"bevel": 0.02}),
                         (box, ((0.06, 0.06, 0.26),), 0.45, "metal_dark", {"bevel": 0.012})], FWD))
HL = (0.42, -0.03, 0.53)
item("Escudo_Redondo", [cyl(0.3, 0.3, 0.06, (HL[0] + 0.1, HL[1], HL[2]), "Hand.L", "wood", seg=14, rot=(0, 90, 0)),
                        cyl(0.32, 0.32, 0.03, (HL[0] + 0.13, HL[1], HL[2]), "Hand.L", "metal_dark", seg=14, rot=(0, 90, 0)),
                        ball((0.06, 0.08, 0.08), (HL[0] + 0.16, HL[1], HL[2]), "Hand.L", "gold", seg=8, rings=5)])

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
