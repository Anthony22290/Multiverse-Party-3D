import bpy, math, os
from mathutils import Vector
from PIL import Image, ImageDraw
files = []
for m in ["Arena", "Carreras", "Zombis", "Codigo", "Escape"]:
    bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
    bpy.ops.import_scene.fbx(filepath=f"dout/Diorama_{m}.fbx")
    img = bpy.data.images.load(os.path.abspath("dout/Decoracion_BaseColor.png"))
    for mat in bpy.data.materials:
        mat.use_nodes = True; nt = mat.node_tree; b = nt.nodes.get("Principled BSDF")
        if not b: continue
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.interpolation = "Linear"; nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
        b.inputs["Roughness"].default_value = .85
        if "Brillo" in mat.name: nt.links.new(t.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 2
    sc.render.engine = "CYCLES"; sc.cycles.samples = 16; sc.render.resolution_x, sc.render.resolution_y = 560, 420
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.45, .58, .7, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .7
    sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sc.collection.objects.link(sun); sun.data.energy = 3.2; sun.rotation_euler = (.8, .2, -.6)
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 30
    cam.location = (9.5, -10.5, 8.5); cam.rotation_euler = (Vector((0, 0, .6)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    f = f"/tmp/dio_{m}.png"; sc.render.filepath = f; bpy.ops.render.render(write_still=True); files.append((m, f))
W = Image.new("RGB", (560 * 3, 420 * 2), (50, 50, 50))
for i, (m, f) in enumerate(files):
    W.paste(Image.open(f), ((i % 3) * 560, (i // 3) * 420)); ImageDraw.Draw(W).text(((i % 3) * 560 + 10, (i // 3) * 420 + 10), m, fill=(255, 255, 255))
W.save("dout/dioramas.png"); print("OK")
