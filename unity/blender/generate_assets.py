"""Ember Vale low-poly asset generator.

Run headless with the bpy wheel or Blender itself:
    python generate_assets.py --out ../Assets/Art/Models --renders ../Art/Previews
    blender -b -P generate_assets.py -- --out ... --renders ...

Scale: 1 Blender unit = 1 map tile = 1 Unity unit. Models sit on z=0 (Unity y=0),
centred on the origin. Every material is a flat colour from PALETTE (sRGB display colours, converted to linear on the way out), so the whole
set shares the same look and Unity can swap them for one shared atlas later.
"""
import argparse
import math
import os
import sys

import bpy
import bmesh
from mathutils import Vector

PALETTE = {
    "wood":       (0.45, 0.29, 0.16),
    "wood_dark":  (0.30, 0.19, 0.11),
    "plaster":    (0.86, 0.80, 0.66),
    "stone":      (0.60, 0.61, 0.62),
    "stone_dark": (0.42, 0.43, 0.45),
    "thatch":     (0.78, 0.62, 0.28),
    "roof_red":   (0.64, 0.24, 0.17),
    "roof_slate": (0.30, 0.34, 0.40),
    "leaf":       (0.24, 0.50, 0.23),
    "leaf_dark":  (0.17, 0.38, 0.19),
    "skin":       (0.90, 0.70, 0.55),
    "cloth":      (0.52, 0.40, 0.28),
    "steel":      (0.68, 0.72, 0.76),
    "banner":     (0.89, 0.64, 0.27),   # tinted per faction in Unity via _BaseColor
    "canvas":     (0.70, 0.60, 0.42),
}

_mats = {}


def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def linear(rgb):
    """PALETTE values are the colours you see (sRGB). Blender materials and the FBX hold linear values;
    Unity's importer turns those back into display colours. Without this the whole set comes out washed out
    (red roofs turn salmon pink)."""
    return tuple(srgb_to_linear(c) for c in rgb)


def mat(name):
    if name not in _mats:
        m = bpy.data.materials.new("EV_" + name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*linear(PALETTE[name]), 1)
        bsdf.inputs["Roughness"].default_value = 1.0
        m.diffuse_color = (*linear(PALETTE[name]), 1)
        _mats[name] = m
    return _mats[name]


def reset():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for c in list(bpy.data.collections):
        bpy.data.collections.remove(c)


def finish(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(mat(material))
    for p in obj.data.polygons:
        p.use_smooth = False
    return obj


def box(name, size, loc, material, rot=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    o.rotation_euler[2] = rot
    bpy.ops.object.transform_apply(scale=True, rotation=True)
    return finish(o, material)


def prism_roof(name, w, d, h, loc, material, overhang=0.08):
    """Gabled roof: a triangular prism whose ridge runs along X."""
    hw, hd = w / 2 + overhang, d / 2 + overhang
    verts = [(-hw, -hd, 0), (hw, -hd, 0), (hw, hd, 0), (-hw, hd, 0), (-hw, 0, h), (hw, 0, h)]
    faces = [(0, 3, 2, 1), (0, 1, 5, 4), (3, 4, 5, 2), (0, 4, 3), (1, 2, 5)]  # wound counter-clockwise seen from outside
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    o.location = loc
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    return finish(o, material)


def pyramid(name, r, h, loc, material, sides=4, rot=math.pi / 4):
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r, radius2=0, depth=h, location=(loc[0], loc[1], loc[2] + h / 2))
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler[2] = rot
    bpy.ops.object.transform_apply(rotation=True)
    return finish(o, material)


def cyl(name, r, h, loc, material, sides=8, r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r, radius2=r if r2 is None else r2, depth=h, location=(loc[0], loc[1], loc[2] + h / 2))
    o = bpy.context.active_object
    o.name = name
    return finish(o, material)


def ico(name, r, loc, material, squash=1.0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale.z = squash
    bpy.ops.object.transform_apply(scale=True)
    return finish(o, material)


def mount(owner, name, loc):
    """An empty child that marks a spot on a model (for example where the faction flag stands).
    It exports to FBX as a child transform, so Unity can find it by name whatever the axis conversion."""
    e = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(e)
    e.parent = owner
    e.location = loc
    return e


def join(name, parts):
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    return o


# ---------------------------------------------------------------- the models

def make_house():
    p = [box("walls", (0.8, 0.6, 0.45), (0, 0, 0.225), "plaster"),
         prism_roof("roof", 0.8, 0.6, 0.4, (0, 0, 0.45), "roof_red"),
         box("door", (0.14, 0.02, 0.25), (0.1, -0.31, 0.125), "wood_dark"),
         box("chimney", (0.1, 0.1, 0.3), (0.25, 0.1, 0.65), "stone_dark")]
    return join("house", p)


def make_hut():
    p = [box("walls", (0.55, 0.5, 0.3), (0, 0, 0.15), "wood"),
         prism_roof("roof", 0.55, 0.5, 0.3, (0, 0, 0.3), "thatch", 0.1),
         box("door", (0.12, 0.02, 0.2), (0, -0.26, 0.1), "wood_dark")]
    return join("hut", p)


def make_hall():
    """Town centre / first hall: long timber hall with a stone footing and a small turret."""
    p = [box("footing", (1.7, 1.0, 0.12), (0, 0, 0.06), "stone"),
         box("walls", (1.6, 0.9, 0.55), (0, 0, 0.395), "plaster"),
         prism_roof("roof", 1.6, 0.9, 0.55, (0, 0, 0.67), "roof_red", 0.12),
         box("beam_l", (0.06, 0.92, 0.55), (-0.8, 0, 0.395), "wood_dark"),
         box("beam_r", (0.06, 0.92, 0.55), (0.8, 0, 0.395), "wood_dark"),
         box("door", (0.26, 0.03, 0.36), (0, -0.46, 0.3), "wood_dark"),
         box("turret", (0.28, 0.28, 0.5), (0.5, 0.1, 1.0), "stone"),
         pyramid("turret_roof", 0.26, 0.28, (0.5, 0.1, 1.25), "roof_slate")]
    hall = join("hall", p)
    mount(hall, "flag_mount", (-0.5, 0, 1.22))   # west end of the roof ridge; the turret is at the east end
    return hall


def make_barracks():
    p = [box("base", (1.3, 0.9, 0.5), (0, 0, 0.25), "stone"),
         box("upper", (1.3, 0.9, 0.08), (0, 0, 0.54), "stone_dark")]
    for i in range(5):
        x = -0.55 + i * 0.275
        p.append(box("merlon%d" % i, (0.15, 0.9, 0.12), (x, 0, 0.64), "stone"))
    p += [box("gate", (0.3, 0.03, 0.34), (0, -0.46, 0.17), "wood_dark"),
          box("rack", (0.5, 0.1, 0.1), (0.35, -0.55, 0.12), "wood"),
          cyl("pole", 0.015, 0.7, (-0.55, 0.3, 0.64), "wood_dark", 4),
          box("flag", (0.22, 0.02, 0.14), (-0.44, 0.3, 1.25), "banner")]
    return join("barracks", p)


def make_tree():
    p = [cyl("trunk", 0.06, 0.35, (0, 0, 0), "wood", 5, 0.045),
         cyl("leaf_low", 0.32, 0.38, (0, 0, 0.22), "leaf_dark", 6, 0.06),
         cyl("leaf_mid", 0.25, 0.34, (0, 0, 0.45), "leaf", 6, 0.04),
         cyl("leaf_top", 0.16, 0.3, (0, 0, 0.68), "leaf", 6, 0.0)]
    return join("tree", p)


def make_oak():
    p = [cyl("trunk", 0.07, 0.4, (0, 0, 0), "wood", 5, 0.05),
         ico("crown_a", 0.3, (0, 0, 0.62), "leaf", 0.85),
         ico("crown_b", 0.2, (0.17, 0.05, 0.5), "leaf_dark", 0.85),
         ico("crown_c", 0.18, (-0.15, -0.08, 0.52), "leaf_dark", 0.85)]
    return join("oak", p)


def make_rock():
    p = [ico("rock_a", 0.28, (0, 0, 0.1), "stone", 0.7),
         ico("rock_b", 0.18, (0.22, 0.08, 0.06), "stone_dark", 0.7),
         ico("rock_c", 0.14, (-0.18, -0.12, 0.05), "stone", 0.7)]
    return join("rock", p)


def make_soldier():
    p = [cyl("legs", 0.07, 0.2, (0, 0, 0), "cloth", 6),
         cyl("torso", 0.1, 0.22, (0, 0, 0.2), "steel", 6, 0.08),
         ico("head", 0.07, (0, 0, 0.5), "skin"),
         cyl("helm", 0.085, 0.07, (0, 0, 0.52), "steel", 6, 0.05),
         cyl("spear", 0.01, 0.62, (0.13, 0, 0), "wood", 4),
         pyramid("spear_tip", 0.025, 0.07, (0.13, 0, 0.62), "steel", 4)]
    return join("soldier", p)


def make_settler():
    p = [cyl("legs", 0.065, 0.18, (0, 0, 0), "wood_dark", 6),
         cyl("torso", 0.095, 0.22, (0, 0, 0.18), "cloth", 6, 0.075),
         ico("head", 0.07, (0, 0, 0.47), "skin"),
         box("pack", (0.12, 0.08, 0.16), (0, 0.1, 0.3), "canvas")]
    return join("settler", p)


def make_banner():
    p = [cyl("pole", 0.018, 1.0, (0, 0, 0), "wood_dark", 5),
         box("cloth", (0.34, 0.015, 0.24), (0.18, 0, 0.84), "banner"),
         pyramid("tip", 0.04, 0.08, (0, 0, 1.0), "steel", 4)]
    # swallow-tail notch on the flag
    return join("banner", p)


def make_tent():
    p = [prism_roof("tent_body", 0.7, 0.6, 0.45, (0, 0, 0), "canvas", 0.0),
         cyl("pole", 0.012, 0.15, (0.35, 0, 0.42), "wood_dark", 4)]
    # rotate so the ridge points along the prism's long side
    return join("tent", p)


MODELS = {
    "house": make_house, "hut": make_hut, "hall": make_hall, "barracks": make_barracks,
    "tree": make_tree, "oak": make_oak, "rock": make_rock, "soldier": make_soldier, "settler": make_settler,
    "banner": make_banner, "tent": make_tent,
}

# ---------------------------------------------------------------- output

def setup_render(res):
    sc = bpy.context.scene
    # Cycles on the CPU: previews must render on machines with no GPU / EGL (CI, cloud containers)
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 24
    sc.cycles.use_denoising = False
    sc.view_settings.view_transform = "Standard"
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.film_transparent = False
    w = bpy.data.worlds.new("w") if not sc.world else sc.world
    sc.world = w
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (*linear((0.50, 0.66, 0.42)), 1)
    bg.inputs["Strength"].default_value = 0.35
    sun_d = bpy.data.lights.new("sun", "SUN")
    sun_d.energy = 3.2
    sun = bpy.data.objects.new("sun", sun_d)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    bpy.context.collection.objects.link(sun)
    cam_d = bpy.data.cameras.new("cam")
    cam_d.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_d)
    bpy.context.collection.objects.link(cam)
    sc.camera = cam
    return cam


def look(cam, target, dist=4.0, scale=2.0):
    cam.data.ortho_scale = scale
    cam.location = (target[0] + dist * 0.6, target[1] - dist * 0.8, target[2] + dist * 0.75)
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def render_to(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def export_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    for child in obj.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="COPY", embed_textures=False)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--renders", required=True)
    ap.add_argument("--res", type=int, default=384)
    a = ap.parse_args(argv)
    os.makedirs(a.out, exist_ok=True)
    os.makedirs(a.renders, exist_ok=True)

    # 1. each model on its own: export FBX + a preview render
    for name, fn in MODELS.items():
        reset()
        obj = fn()
        export_fbx(obj, os.path.join(a.out, name + ".fbx"))
        cam = setup_render(a.res)
        bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
        top = max(v.z for v in bb)
        wide = max(max(v.x for v in bb) - min(v.x for v in bb), max(v.y for v in bb) - min(v.y for v in bb), top)
        look(cam, (0, 0, top / 2), scale=max(1.0, wide * 1.6))
        render_to(os.path.join(a.renders, name + ".png"))
        print("exported", name, "faces", len(obj.data.polygons))

    # 2. a village sheet with everything on a lawn
    reset()
    placed = []
    layout = [("hall", 0, 0, 0), ("house", 2.0, -0.4, 0.2), ("house", 2.1, 0.8, -0.3), ("hut", -1.9, 0.9, 0.5),
              ("hut", -1.8, -0.7, -0.2), ("barracks", 0.2, 2.3, 0), ("tent", -2.9, 0.1, 0.4), ("tent", -3.0, 1.2, -0.3),
              ("tree", 3.4, 1.8, 0), ("tree", 3.0, 2.4, 0), ("oak", -3.4, 2.4, 0), ("tree", 3.9, 1.1, 0), ("rock", -3.6, -1.0, 0), ("rock", 3.5, -1.6, 0.8),
              ("oak", 1.9, 3.1, 0), ("tree", -2.4, 3.0, 0),
              ("soldier", -0.6, 1.3, 0), ("soldier", -0.1, 1.2, 0), ("soldier", 0.4, 1.3, 0), ("settler", 1.0, -1.2, 0),
              ("settler", -1.0, -1.4, 0), ("banner", -1.2, 2.0, 0), ("banner", 1.6, 2.0, 0)]
    for n, x, y, r in layout:
        o = MODELS[n]()
        o.location = (x, y, 0)
        o.rotation_euler[2] = r
        placed.append(o)
    bpy.ops.mesh.primitive_plane_add(size=14, location=(0, 0.5, -0.001))
    g = bpy.context.active_object
    g.name = "lawn"
    PALETTE["lawn"] = (0.45, 0.64, 0.38)
    finish(g, "lawn")
    cam = setup_render(1024)
    look(cam, (0.2, 1.0, 0.3), dist=9, scale=7.0)
    render_to(os.path.join(a.renders, "village_sheet.png"))
    print("done")


if __name__ == "__main__":
    main()
