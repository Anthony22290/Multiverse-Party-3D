"""Animaciones del Aventurero y exportación FBX para Unity.
Uso: python3 animar.py <blend> <carpeta_salida>
Convención de signos (huesos rectos hacia arriba, roll 0):
  extremidades colgantes (brazos/piernas): rx < 0 = hacia adelante; rodilla: LowerLeg rx > 0 dobla; codo: LowerArm rx < 0 dobla
  Chest/Head/Root: rx > 0 = inclinar adelante · UpperArm.L rz > 0 = abrir hacia fuera (R: rz < 0) · ry = girar sobre el eje vertical
  Hips "y" = subir/bajar (local Y del hueso = arriba)."""
import bpy, sys, math, os
blend, OUT = sys.argv[1], sys.argv[2]
KART = "--kart" in sys.argv
NAME = "Piloto" if KART else "Fiestero"
bpy.ops.wm.open_mainfile(filepath=blend)
sc = bpy.context.scene; sc.render.fps = 30
arm = bpy.data.objects["Armature"]
bpy.context.view_layer.objects.active = arm
arm.animation_data_create()
BONES = [b.name for b in arm.pose.bones]

def P(**kw):
    """Pose: claves con '_' en vez de '.', p. ej. UpperArm_L=(rx,ry,rz); y=altura de cadera."""
    return {k.replace("_", "."): v for k, v in kw.items()}

def mirror(p):
    out = {}
    for k, v in p.items():
        if k.endswith(".L"): nk = k[:-2] + ".R"
        elif k.endswith(".R"): nk = k[:-2] + ".L"
        else: nk = k
        if isinstance(v, tuple) and len(v) == 3: v = (v[0], -v[1], -v[2])  # el giro lateral y la torsión se invierten
        out[nk] = v
    if "Chest" in out and isinstance(out["Chest"], tuple): pass
    return out

CLIPS = {}
def clip(name, keys, loop):
    CLIPS[name] = (keys, loop)

# --- Idle
base = P(UpperArm_L=(0, 0, 4), UpperArm_R=(0, 0, -4))
breath = P(UpperArm_L=(0, 0, 7), UpperArm_R=(0, 0, -7), LowerArm_L=(-8, 0, 0), LowerArm_R=(-8, 0, 0), Chest=(2.5, 0, 0), Head=(-3, 0, 0), y=-0.012)
clip("Idle", [(0, base), (30, breath), (60, base)], True)
# --- Walk
w0 = P(UpperLeg_L=(-28, 0, 0), LowerLeg_L=(6, 0, 0), Foot_L=(10, 0, 0), UpperLeg_R=(24, 0, 0), LowerLeg_R=(28, 0, 0), Foot_R=(-12, 0, 0),
       UpperArm_L=(24, 0, 5), UpperArm_R=(-24, 0, -5), LowerArm_L=(-10, 0, 0), LowerArm_R=(-22, 0, 0), Chest=(3, 6, 0), Head=(-2, -5, 0), y=0.0)
w1 = P(UpperLeg_L=(-6, 0, 0), LowerLeg_L=(4, 0, 0), UpperLeg_R=(-14, 0, 0), LowerLeg_R=(50, 0, 0), Foot_R=(-10, 0, 0),
       UpperArm_L=(4, 0, 5), UpperArm_R=(-4, 0, -5), LowerArm_L=(-14, 0, 0), LowerArm_R=(-14, 0, 0), Chest=(3, 0, 0), y=0.035)
clip("Walk", [(0, w0), (8, w1), (16, mirror(w0)), (24, mirror(w1)), (32, w0)], True)
# --- Run
r0 = P(UpperLeg_L=(-52, 0, 0), LowerLeg_L=(22, 0, 0), Foot_L=(12, 0, 0), UpperLeg_R=(34, 0, 0), LowerLeg_R=(72, 0, 0), Foot_R=(-10, 0, 0),
       UpperArm_L=(48, 0, 8), UpperArm_R=(-58, 0, -8), LowerArm_L=(-70, 0, 0), LowerArm_R=(-80, 0, 0), Chest=(12, 8, 0), Head=(-8, -6, 0), y=-0.01)
r1 = P(UpperLeg_L=(-14, 0, 0), LowerLeg_L=(18, 0, 0), UpperLeg_R=(-32, 0, 0), LowerLeg_R=(95, 0, 0), Foot_R=(-20, 0, 0),
       UpperArm_L=(8, 0, 8), UpperArm_R=(-8, 0, -8), LowerArm_L=(-75, 0, 0), LowerArm_R=(-75, 0, 0), Chest=(12, 0, 0), Head=(-8, 0, 0), y=0.07)
clip("Run", [(0, r0), (5, r1), (10, mirror(r0)), (15, mirror(r1)), (20, r0)], True)
# --- Jump
crouch = P(UpperLeg_L=(-48, 0, 0), UpperLeg_R=(-48, 0, 0), LowerLeg_L=(85, 0, 0), LowerLeg_R=(85, 0, 0), Foot_L=(-37, 0, 0), Foot_R=(-37, 0, 0),
           UpperArm_L=(38, 0, 8), UpperArm_R=(38, 0, -8), Chest=(16, 0, 0), Head=(-10, 0, 0), y=-0.13)
up = P(UpperArm_L=(-155, 0, 12), UpperArm_R=(-155, 0, -12), LowerLeg_L=(10, 0, 0), LowerLeg_R=(10, 0, 0), Foot_L=(25, 0, 0), Foot_R=(25, 0, 0), Chest=(-6, 0, 0), y=0.2)
tuck = P(UpperLeg_L=(-60, 0, 0), UpperLeg_R=(-50, 0, 0), LowerLeg_L=(75, 0, 0), LowerLeg_R=(80, 0, 0), Foot_L=(-10, 0, 0), Foot_R=(-15, 0, 0),
         UpperArm_L=(-100, 0, 30), UpperArm_R=(-100, 0, -30), LowerArm_L=(-30, 0, 0), LowerArm_R=(-30, 0, 0), Chest=(6, 0, 0), y=0.16)
land = P(UpperLeg_L=(-38, 0, 0), UpperLeg_R=(-38, 0, 0), LowerLeg_L=(65, 0, 0), LowerLeg_R=(65, 0, 0), Foot_L=(-27, 0, 0), Foot_R=(-27, 0, 0),
         UpperArm_L=(20, 0, 25), UpperArm_R=(20, 0, -25), Chest=(12, 0, 0), y=-0.09)
clip("Jump", [(0, base), (5, crouch), (10, up), (16, tuck), (22, land), (28, base)], False)
# --- Attack (mano derecha)
wind = P(UpperArm_R=(-155, 0, -10), LowerArm_R=(-35, 0, 0), UpperArm_L=(-25, 0, 10), Chest=(-6, 28, 0), Head=(0, -15, 0), UpperLeg_L=(-15, 0, 0), UpperLeg_R=(10, 0, 0), y=0.0)
strike = P(UpperArm_R=(-25, 0, -5), LowerArm_R=(-5, 0, 0), UpperArm_L=(25, 0, 15), Chest=(14, -32, 0), Head=(-6, 18, 0), UpperLeg_L=(-25, 0, 0), LowerLeg_L=(20, 0, 0), UpperLeg_R=(15, 0, 0), y=-0.035)
clip("Attack", [(0, base), (6, wind), (10, strike), (14, strike), (22, base)], False)
# --- Hit
hit = P(Chest=(-18, 6, 0), Head=(-20, 0, 0), UpperArm_L=(-10, 0, 32), UpperArm_R=(-10, 0, -32), LowerArm_L=(-30, 0, 0), LowerArm_R=(-30, 0, 0), y=-0.02)
clip("Hit", [(0, base), (3, hit), (14, base)], False)
# --- Death
d1 = P(Chest=(-14, 0, 0), Head=(-18, 0, 0), UpperArm_L=(-20, 0, 28), UpperArm_R=(-20, 0, -28), UpperLeg_L=(-8, 0, 0), LowerLeg_L=(16, 0, 0))
d2 = P(Root=(-92, 0, 0), Chest=(-4, 0, 0), Head=(-12, 0, 0), UpperArm_L=(-10, 0, 75), UpperArm_R=(-10, 0, -70), UpperLeg_L=(-14, 0, 0), UpperLeg_R=(-4, 0, 0), LowerLeg_L=(10, 0, 0))
d3 = dict(d2); d3["Root"] = (-84, 0, 0)
d4 = dict(d2); d4["Root"] = (-90, 0, 0)
clip("Death", [(0, base), (7, d1), (20, d2), (25, d3), (31, d4), (44, d4)], False)
# --- Victory
v0 = P(UpperArm_L=(0, 0, 155), UpperArm_R=(0, 0, -155), LowerArm_L=(0, 0, 10), LowerArm_R=(0, 0, -10), Head=(-10, 0, 0), y=0.0)
v1 = P(UpperArm_L=(0, 0, 165), UpperArm_R=(0, 0, -165), UpperLeg_L=(-20, 0, 0), UpperLeg_R=(-20, 0, 0), LowerLeg_L=(30, 0, 0), LowerLeg_R=(30, 0, 0), Head=(-14, 0, 0), y=0.15)
clip("Victory", [(0, v0), (9, v1), (18, v0), (27, v1), (36, v0)], True)
# --- Wave (saludo, para el menú)
wv = lambda a: P(UpperArm_R=(-10, 0, -150), LowerArm_R=(0, 0, a), UpperArm_L=(0, 0, 5), Chest=(0, -8, 0), Head=(-5, -6, 0))
clip("Wave", [(0, wv(20)), (10, wv(-25)), (20, wv(20)), (30, wv(-25)), (40, wv(20))], True)

# --- Punch (puñetazo rápido con la derecha, para la arena)
jab = P(UpperArm_R=(-88, 0, -4), LowerArm_R=(-6, 0, 0), UpperArm_L=(-30, 0, 10), LowerArm_L=(-80, 0, 0), Chest=(8, -24, 0), Head=(-4, 16, 0), UpperLeg_L=(-20, 0, 0), LowerLeg_L=(14, 0, 0), UpperLeg_R=(12, 0, 0), y=-0.02)
guard = P(UpperArm_R=(-30, 0, -8), LowerArm_R=(-95, 0, 0), UpperArm_L=(-30, 0, 8), LowerArm_L=(-95, 0, 0), Chest=(-2, 14, 0), y=-0.01)
clip("Punch", [(0, base), (3, guard), (6, jab), (10, jab), (16, base)], False)
# --- Dance (baile tonto en bucle)
dz = lambda s: P(Chest=(0, 10 * s, 8 * s), Head=(0, -6 * s, -10 * s), UpperArm_L=(-40 if s > 0 else 20, 0, 60), UpperArm_R=(20 if s > 0 else -40, 0, -60),
                 LowerArm_L=(-70, 0, 0), LowerArm_R=(-70, 0, 0), UpperLeg_L=(-20 if s > 0 else 0, 0, 0), LowerLeg_L=(35 if s > 0 else 0, 0, 0),
                 UpperLeg_R=(0 if s > 0 else -20, 0, 0), LowerLeg_R=(0 if s > 0 else 35, 0, 0), y=-0.04)
mid = P(UpperArm_L=(-10, 0, 40), UpperArm_R=(-10, 0, -40), LowerArm_L=(-60, 0, 0), LowerArm_R=(-60, 0, 0), y=0.03)
clip("Dance", [(0, dz(1)), (10, mid), (20, dz(-1)), (30, mid), (40, dz(1))], True)

# ---------------------------------------------------------------- Carreras: piloto sentado en el kart
SIT = dict(UpperLeg_L=(-84, 0, 4), UpperLeg_R=(-84, 0, -4), LowerLeg_L=(14, 0, 0), LowerLeg_R=(14, 0, 0), Foot_L=(-20, 0, 0), Foot_R=(-20, 0, 0))
def K(y=-0.24, **kw):
    d = dict(SIT); d.update(UpperArm_L=(-62, 0, 6), UpperArm_R=(-62, 0, -6), LowerArm_L=(-28, 0, 0), LowerArm_R=(-28, 0, 0), Chest=(6, 0, 0)); d.update(kw)
    return P(y=y, **d)
kart = {}
kart["Kart_Idle"] = ([(0, K()), (10, K(y=-0.255, Head=(0, 6, 0))), (20, K()), (30, K(y=-0.255, Head=(0, -6, 0))), (40, K())], True)
left = K(Chest=(6, 10, -12), Head=(0, 18, -8), UpperArm_L=(-48, 0, 14), UpperArm_R=(-80, 0, -2), LowerArm_L=(-20, 0, 0), LowerArm_R=(-40, 0, 0))
left2 = dict(left); left2["Hips"] = None; left2 = {k: v for k, v in left2.items() if v is not None}; left2["y"] = -0.25
kart["Kart_Left"] = ([(0, left), (6, left2), (12, left)], True)
kart["Kart_Right"] = ([(0, mirror(left)), (6, mirror(left2)), (12, mirror(left))], True)
boost = K(Chest=(-16, 0, 0), Head=(-12, 0, 0), UpperArm_L=(-70, 0, 4), UpperArm_R=(-70, 0, -4), LowerArm_L=(-5, 0, 0), LowerArm_R=(-5, 0, 0))
kart["Kart_Boost"] = ([(0, boost), (2, dict(boost, y=-0.25)), (4, boost)], True)
sp = lambda s: K(Chest=(-8, 20 * s, 10 * s), Head=(-6, -25 * s, 20 * s), UpperArm_L=(-40 if s > 0 else -100, 0, 110), UpperArm_R=(-100 if s > 0 else -40, 0, -110),
                 LowerArm_L=(-40, 0, 0), LowerArm_R=(-40, 0, 0))
kart["Kart_Spin"] = ([(0, sp(1)), (6, sp(-1)), (12, sp(1)), (18, sp(-1)), (24, sp(1))], True)
kart["Kart_Throw"] = ([(0, K()), (6, K(Chest=(-6, 30, 0), UpperArm_R=(40, 0, -20), LowerArm_R=(-60, 0, 0), Head=(0, -10, 0))),
                       (11, K(Chest=(10, -15, 0), UpperArm_R=(-160, 0, -10), LowerArm_R=(-5, 0, 0))), (20, K())], False)
win = lambda a: K(UpperArm_R=(-20, 0, -158), LowerArm_R=(0, 0, a), Head=(-12, -8, 0), Chest=(-4, -10, 0))
kart["Kart_Win"] = ([(0, win(10)), (8, win(-35)), (16, win(10))], True)
if KART:
    CLIPS.clear(); CLIPS.update(kart)

def apply_pose(p):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        r = p.get(pb.name, (0, 0, 0))
        pb.rotation_euler = tuple(math.radians(a) for a in r)
        pb.location = (0, p.get("y", 0.0), 0) if pb.name == "Hips" else (0, 0, 0)

actions = {}
for name, (keys, loop) in CLIPS.items():
    act = bpy.data.actions.new(name); act.use_fake_user = True
    arm.animation_data.action = act
    for f, pose in keys:
        apply_pose(pose)
        for pb in arm.pose.bones:
            pb.keyframe_insert("rotation_euler", frame=f)
            if pb.name == "Hips": pb.keyframe_insert("location", frame=f)
    actions[name] = (act, keys[-1][0])
apply_pose({})

def export(path, with_mesh, act=None, end=0):
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    if with_mesh:
        for ob in bpy.data.objects:
            if ob.type == "MESH": ob.select_set(True)
    if act:
        arm.animation_data.action = act
        sc.frame_start, sc.frame_end = 0, end
    else:
        arm.animation_data.action = None; apply_pose({})
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"} if with_mesh else {"ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y", add_leaf_bones=False, use_mesh_modifiers=True,
        mesh_smooth_type="FACE", bake_anim=act is not None, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False)

os.makedirs(OUT, exist_ok=True)
export(os.path.join(OUT, NAME + ".fbx"), True)
for name, (act, end) in actions.items():
    export(os.path.join(OUT, f"{NAME}@{name}.fbx"), True, act, end)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, NAME + "_anim.blend"))
print("CLIPS", {n: e for n, (a, e) in actions.items()})
