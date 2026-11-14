import bpy, os, sys
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True); sc = bpy.context.scene
bpy.ops.import_scene.fbx(filepath="dout/Deco_EscapeJuguete.fbx")
img = bpy.data.images.load(os.path.abspath("dout/Decoracion_BaseColor.png"))
for mat in bpy.data.materials:
    mat.use_nodes = True; nt = mat.node_tree; b = nt.nodes.get("Principled BSDF")
    if not b: continue
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    if "Brillo" in mat.name: nt.links.new(t.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 2
xs = [o.matrix_world.translation.x for o in bpy.data.objects if o.type == "MESH"]; cx = (min(xs) + max(xs)) / 2
sc.render.engine = "CYCLES"; sc.cycles.samples = 16; sc.render.resolution_x, sc.render.resolution_y = 1600, 420
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.45, .58, .7, 1)
sun = bpy.data.objects.new("s", bpy.data.lights.new("s", "SUN")); sc.collection.objects.link(sun); sun.data.energy = 3; sun.rotation_euler = (.8, .2, -.4)
cam = bpy.data.objects.new("c", bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = "ORTHO"; cam.data.ortho_scale = max(xs) - min(xs) + 3
cam.location = (cx, -12, 5); cam.rotation_euler = (Vector((cx, 0, 1.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = os.path.abspath("dout/escape_props.png"); bpy.ops.render.render(write_still=True); print("OK")
