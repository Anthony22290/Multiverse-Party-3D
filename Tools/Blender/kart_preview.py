import bpy, math, sys
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath="kout/fbx/Piloto_anim.blend")
sc = bpy.context.scene; arm = bpy.data.objects["Armature"]
sc.render.engine = "CYCLES"; sc.cycles.samples = 14; sc.render.resolution_x, sc.render.resolution_y = 420, 340
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.32, .38, .46, 1)
sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sc.collection.objects.link(sun); sun.data.energy = 3; sun.rotation_euler = (.9, 0, -.5)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 40
cam.location = (2.9, -3.4, 2.0); cam.rotation_euler = (Vector((0, 0, .55)) - cam.location).to_track_quat("-Z", "Y").to_euler()
shots = [("Kart_Clasico", ["Pelo_Corto", "Torso_Piloto", "Piernas_Pantalon", "Pies_Zapatillas", "Cabeza_Casco"], "Kart_Idle", 0),
         ("Kart_Clasico", ["Pelo_Afro", "Torso_Hawaiana", "Piernas_Corto", "Pies_Chanclas", "Cara_GafasSol"], "Kart_Left", 0),
         ("Kart_Banera", ["Cabeza_Pollo", "Torso_Camiseta", "Piernas_Corto", "Pies_Pato"], "Kart_Boost", 0),
         ("Kart_Carrito", ["Cabeza_Dino", "Torso_Sudadera", "Piernas_Pantalon", "Pies_Zapatillas"], "Kart_Spin", 0),
         ("Kart_Clasico", ["Pelo_Cresta", "Torso_Heroe", "Piernas_Pantalon", "Pies_Botas", "Espalda_Capa"], "Kart_Throw", 11),
         ("Kart_Banera", ["Pelo_Coleta", "Torso_Peto", "Piernas_Pantalon", "Pies_Botas", "Cabeza_Corona"], "Kart_Win", 8)]
files = []
for i, (kart, combo, act, f) in enumerate(shots):
    for ob in bpy.data.objects:
        if ob.type == "MESH": ob.hide_render = not (ob.name.startswith("Cuerpo_") or ob.name in combo or ob.name == kart)
    arm.animation_data.action = bpy.data.actions[act]; sc.frame_set(f)
    sc.render.filepath = f"/tmp/k{i}.png"; bpy.ops.render.render(write_still=True); files.append(f"/tmp/k{i}.png")
from PIL import Image, ImageDraw
W = Image.new("RGB", (420 * 3, 340 * 2))
for i, f in enumerate(files):
    W.paste(Image.open(f), ((i % 3) * 420, (i // 3) * 340)); ImageDraw.Draw(W).text(((i % 3) * 420 + 8, (i // 3) * 340 + 8), shots[i][2], fill=(255, 255, 255))
W.save("kout/karts.png"); print("OK")
