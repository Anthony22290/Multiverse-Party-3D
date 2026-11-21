import bpy, sys, math, os
from mathutils import Vector
blend, out, names = sys.argv[1], sys.argv[2], sys.argv[3].split(",")
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene
sc.render.engine = "CYCLES"; sc.cycles.samples = 24; sc.cycles.device = "CPU"
sc.render.resolution_x, sc.render.resolution_y = 640, 480
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (.5, .6, .75, 1); w.node_tree.nodes["Background"].inputs[1].default_value = .8
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun); sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sun.data.energy = 3
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
for o in bpy.data.objects:
    if "_Col" in o.name: o.hide_render = True
for n in names:
    ob = bpy.data.objects[n]
    for o in bpy.data.objects:
        if o.type == "MESH" and "_Col" not in o.name: o.hide_render = not (o == ob or o.parent == ob)
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    ctr = sum(pts, Vector()) / 8; size = max((p - ctr).length for p in pts)
    d = Vector((.55, -1.0, .55)).normalized()
    cam.location = ctr + d * size * 2.3; cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(out, n + ".png"); bpy.ops.render.render(write_still=True)
