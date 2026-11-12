"""Round shield's back fitted to a forearm (Forge session 40).

Lucian Pavel's CC0 round shield is strapped: a hand grip 18 cm off the centre and a forearm strap as far the other
side. As made, the grip's bar stands 3.9 cm off the board and the strap 3.3 cm, so a hand on the grip put the
forearm into the board and through the strap, and the hold was pushed 9 cm off the grip to hide it. Measured on
VikingF1's drawn stance, a hand on the grip lays the forearm's axis 1.1 cm behind the grip toward the board and
6.5 cm below the strap's middle; the characters' forearms reach 3.7 (ElfF1) to 5.9 cm (VikingM1) from that axis,
8.6 cm on the armoured VikingHero. So the back is fitted to a 5.5 cm reach: the board moves 3.1 cm further from the
grip (the grip's legs stretch, the bar and the pivot stay: the pivot is the grip Forge holds), and the strap's bar
stands 8.3 cm further out and 6.5 cm lower, over the forearm. The collision hull follows the board.

Run: blender --background --factory-startup --python round_shield_fit_arm.py -- <in.fbx> <out.fbx>
Blender's local axes are the FBX's with x mirrored against Unity's mesh: y and z (board normal, boss at +z) match.
"""
import sys

import bmesh
import bpy

BOARD_SHIFT = 0.031  # the board, away from the grip's bar (+z)
STRAP_OUT = 0.083    # the strap's bar, further out from the board (-z)
STRAP_DOWN = 0.065   # the strap, along the board toward the arm's side (-y)


def islands(bm):
    seen, found = set(), []
    for start in bm.verts:
        if start.index in seen:
            continue
        seen.add(start.index)
        stack, part = [start], []
        while stack:
            vert = stack.pop()
            part.append(vert)
            for edge in vert.link_edges:
                other = edge.other_vert(vert)
                if other.index not in seen:
                    seen.add(other.index)
                    stack.append(other)
        found.append(part)
    return found


def ramp(z, fixed, moved):
    """0 at `fixed` (a foot on the board), 1 at `moved` (the bar) and beyond, linear between."""
    t = (fixed - z) / (fixed - moved)
    return max(0.0, min(1.0, t))


def fit(mesh_object):
    bm = bmesh.new()
    bm.from_mesh(mesh_object.data)
    parts = sorted(islands(bm), key=len, reverse=True)
    board, strap, grip = parts[0], None, None
    for part in parts[1:]:
        xs = [v.co.x for v in part]
        centre = (min(xs) + max(xs)) / 2
        if abs(centre) < 0.05:
            grip = part
        else:
            strap = part
    assert grip is not None and strap is not None, "expected the board, a grip near the pivot and a strap"
    for v in board:
        v.co.z += BOARD_SHIFT
    for v in grip:
        # Feet on the board (z >= 0.042) move with it; the bar (z <= 0) stays on the pivot.
        v.co.z += BOARD_SHIFT * (1.0 - ramp(v.co.z, 0.042, 0.0))
    for v in strap:
        out = ramp(v.co.z, 0.043, 0.013)
        v.co.z += BOARD_SHIFT - STRAP_OUT * out
        v.co.y -= STRAP_DOWN
    bm.to_mesh(mesh_object.data)
    bm.free()
    mesh_object.data.update()


def main():
    source, target = sys.argv[sys.argv.index("--") + 1:][:2]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=source)
    for ob in bpy.context.scene.objects:
        if ob.type != 'MESH':
            continue
        if ob.name.endswith("_COL"):
            for v in ob.data.vertices:
                v.co.z += BOARD_SHIFT
            ob.data.update()
        else:
            fit(ob)
    # Scale baked into the meshes (unit scale 1, objects unscaled), as Forge's model contract has them; Blender's
    # default writes centimetres under a 100x object scale, which the hold's offsets would be multiplied by.
    bpy.ops.export_scene.fbx(filepath=target, use_selection=False, object_types={'MESH'}, apply_scale_options='FBX_SCALE_ALL')
    print("FIT_DONE", target)


main()
