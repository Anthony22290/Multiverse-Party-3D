import bpy, sys, math
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath="fout/fbx/Fiestero_anim.blend")
sc = bpy.context.scene; arm = bpy.data.objects["Armature"]
combo = ["Pelo_Corto", "Torso_Camiseta", "Piernas_Pantalon", "Pies_Zapatillas", "Cabeza_Fiesta", "Mano_PolloGoma"]
for ob in bpy.data.objects:
    if ob.type == "MESH": ob.hide_render = not (ob.name.startswith("Cuerpo_") or ob.name in combo)
sc.render.engine = "CYCLES"; sc.cycles.samples = 12; sc.render.resolution_x, sc.render.resolution_y = 300, 380
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.32, .38, .46, 1)
sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sc.collection.objects.link(sun); sun.data.energy = 3; sun.rotation_euler = (.9, 0, -.5)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 45
cam.location = (3.6, -3.4, 1.4); cam.rotation_euler = (Vector((0, 0, .8)) - cam.location).to_track_quat("-Z", "Y").to_euler()
shots = [("Idle", 30), ("Walk", 0), ("Run", 0), ("Jump", 5), ("Jump", 10), ("Attack", 6), ("Attack", 10), ("Hit", 3), ("Death", 44), ("Victory", 9), ("Wave", 10), ("Punch", 6), ("Dance", 0)]
files = []
for i, (a, f) in enumerate(shots):
    arm.animation_data.action = bpy.data.actions[a]
    sc.frame_set(f); sc.render.filepath = f"/tmp/pose_{i}.png"; bpy.ops.render.render(write_still=True); files.append(f"/tmp/pose_{i}.png")
from PIL import Image, ImageDraw
ims = [Image.open(f) for f in files]; W = Image.new("RGB", (300 * 7, 380 * 2), (60, 60, 60))
for i, (im, (a, f)) in enumerate(zip(ims, shots)):
    x, y = (i % 7) * 300, (i // 7) * 380; W.paste(im, (x, y)); ImageDraw.Draw(W).text((x + 8, y + 8), f"{a} f{f}", fill=(255, 255, 255))
W.save("fout/poses.png"); print("OK")
