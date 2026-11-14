"""Renderiza combinaciones de ropa del Aventurero (Cycles CPU) y las junta en una tira.
Uso: python3 preview.py <blend> <png_salida> [pose_action frame]"""
import bpy, sys, math, os
from mathutils import Vector
blend, outp = sys.argv[1], sys.argv[2]
action = sys.argv[3] if len(sys.argv) > 3 else None
frame = int(sys.argv[4]) if len(sys.argv) > 4 else 0
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene
COMBOS = [
    ["Pelo_Corto", "Torso_Camiseta", "Piernas_Pantalon", "Pies_Zapatillas", "Cabeza_Fiesta", "Mano_PolloGoma"],
    ["Torso_Hawaiana", "Piernas_Corto", "Pies_Chanclas", "Cara_GafasSol", "Cintura_Flotador", "Pelo_Afro"],
    ["Cabeza_Dino", "Torso_Sudadera", "Piernas_Pantalon", "Pies_Pato", "Mano_Pescado"],
    ["Cabeza_Casco", "Torso_Piloto", "Piernas_Pantalon", "Pies_Botas", "Espalda_Cohete"],
    ["Pelo_Cresta", "Torso_Heroe", "Piernas_Corto", "Pies_Botas", "Espalda_Capa", "Cara_Broma", "Cintura_Campeon"],
    ["Cabeza_Pollo", "Torso_Peto", "Piernas_Pantalon", "Pies_Zapatillas", "Mano_Baguette"],
    ["Pelo_Coleta", "Torso_Camiseta", "Cintura_Tutu", "Piernas_Corto", "Pies_Zapatillas", "Espalda_Alas", "Cara_Payaso", "Cabeza_Corona"],
    ["Cabeza_Vaquero", "Pelo_Corto", "Torso_Peto", "Piernas_Pantalon", "Pies_Botas", "Cara_Bigote", "Mano_Martillo"],
]
arm = bpy.data.objects["Armature"]
if action:
    arm.animation_data_create(); arm.animation_data.action = bpy.data.actions[action]
    sl = arm.animation_data.action_slot or (bpy.data.actions[action].slots[0] if bpy.data.actions[action].slots else None)
    if sl: arm.animation_data.action_slot = sl
    sc.frame_set(frame)
sc.render.engine = "CYCLES"; sc.cycles.samples = 24; sc.cycles.device = "CPU"
sc.render.resolution_x, sc.render.resolution_y = 380, 480; sc.render.film_transparent = False
world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.32, 0.38, 0.46, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 3.2; sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 60
cam.location = (1.6, -4.6, 1.55)
d = Vector((0, 0, 0.85)) - cam.location; cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
files = []
for i, combo in enumerate(COMBOS):
    for ob in bpy.data.objects:
        if ob.type == "MESH":
            show = ob.name.startswith("Cuerpo_") or ob.name in combo
            ob.hide_render = not show
    f = f"/tmp/prev_{i}.png"; sc.render.filepath = f; bpy.ops.render.render(write_still=True); files.append(f)
from PIL import Image
ims = [Image.open(f) for f in files]
W = Image.new("RGB", (sum(i.width for i in ims), ims[0].height)); x = 0
for im in ims: W.paste(im, (x, 0)); x += im.width
W.save(outp); print("OK", outp)
