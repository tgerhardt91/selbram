"""
build_spinning_tube.py
Generates the SpinningTube GLB asset for Selbram.

Geometry overview:
  - 12-sided (dodecagonal) hollow cylinder
  - Inner radius: 5.0 units, outer radius: 5.4 units (0.4 wall thickness)
  - Total length: 80 units along Z axis, centered at origin
  - 11 rings of planks along the length, 10 ring-gaps between them
  - Each ring: 3 planks at 0°, 120°, 240° start angles
    Plank angular span: ~100°  (gap between planks: ~20°)
  - Ring plank Z length: 5.5 units, Z-gap between rings: 2.0 units
    Total occupied: 11*5.5 + 10*2.0 = 60.5 + 20 = 80.5 -> nudge slightly
    Actual: use 11 planks of 5.2 units + 10 gaps of 2.3 units = 57.2 + 23 = 80.2 ~ 80
  - Mesh built from individual quad faces (quads where possible)
  - Material: Principled BSDF, blue metallic sci-fi aesthetic
  - Exported as GLB

Run with:
  /Applications/Blender.app/Contents/MacOS/Blender --background --python build_spinning_tube.py
"""

import bpy
import bmesh
import math
import os

# ── Parameters ──────────────────────────────────────────────────────────────
INNER_RADIUS   = 5.0
WALL_THICKNESS = 0.4
OUTER_RADIUS   = INNER_RADIUS + WALL_THICKNESS
TOTAL_LENGTH   = 80.0          # along Z
N_SIDES        = 12            # dodecagonal cross-section
N_RINGS        = 11            # rings of planks
N_GAPS_BETWEEN = N_RINGS - 1  # 10 gaps between rings

PLANK_Z        = 5.2           # plank length along Z
RING_GAP_Z     = 2.3           # gap between rings along Z
# Total: 11*5.2 + 10*2.3 = 57.2 + 23.0 = 80.2  (centers at 0, slight overshoot is fine)

PLANKS_PER_RING = 3
# Angular geometry: 3 planks + 3 circumferential gaps = 360 deg
# Plank spans ~100 deg, gap spans ~20 deg
PLANK_ANG_DEG  = 100.0
GAP_ANG_DEG    = (360.0 - PLANKS_PER_RING * PLANK_ANG_DEG) / PLANKS_PER_RING  # 20 deg

OUTPUT_PATH = "/Users/travisgerhardt/gadot/selbram/Assets/Models/SpinningTube.glb"

# ── Helpers ──────────────────────────────────────────────────────────────────

def ring_start_angles():
    """Returns the start angle (degrees) of each plank in a ring."""
    angles = []
    for i in range(PLANKS_PER_RING):
        start = i * (PLANK_ANG_DEG + GAP_ANG_DEG)
        angles.append(start)
    return angles

def z_ring_positions():
    """
    Returns list of (z_start, z_end) for each ring of planks.
    Centered so the full tube spans roughly -40 to +40.
    """
    step = PLANK_Z + RING_GAP_Z
    total_span = N_RINGS * PLANK_Z + (N_RINGS - 1) * RING_GAP_Z
    z_offset = -total_span / 2.0
    positions = []
    for i in range(N_RINGS):
        z_start = z_offset + i * step
        z_end   = z_start + PLANK_Z
        positions.append((z_start, z_end))
    return positions

def angle_range_verts(radius, ang_start_deg, ang_end_deg, z, n_steps):
    """
    Generate vertices along an arc at a given radius and Z level.
    n_steps: number of edge SEGMENTS along the arc (returns n_steps+1 verts).
    """
    verts = []
    for i in range(n_steps + 1):
        t = i / n_steps
        ang = math.radians(ang_start_deg + t * (ang_end_deg - ang_start_deg))
        x = math.cos(ang) * radius
        y = math.sin(ang) * radius
        verts.append((x, y, z))
    return verts

# ── Scene setup ──────────────────────────────────────────────────────────────
# Clear scene
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# ── Material ─────────────────────────────────────────────────────────────────
mat = bpy.data.materials.new(name="SpinningTube_Metal")
mat.use_nodes = True
bsdf = mat.node_tree.nodes["Principled BSDF"]
bsdf.inputs["Base Color"].default_value     = (0.2, 0.6, 0.95, 1.0)
bsdf.inputs["Metallic"].default_value       = 0.5
bsdf.inputs["Roughness"].default_value      = 0.2
# Slight specular tint for sci-fi look
bsdf.inputs["Specular IOR Level"].default_value = 0.6

# ── Build mesh with bmesh ────────────────────────────────────────────────────
bm = bmesh.new()

plank_start_angles = ring_start_angles()   # [0, 120, 240] degrees
z_positions        = z_ring_positions()

# For each plank segment, we build a quad strip across the arc,
# with inner and outer faces + side walls to give wall thickness.
#
# Each "plank" is a curved rectangular solid:
#   - inner curved face  (faces toward tube center)
#   - outer curved face  (visible from outside)
#   - two Z-end caps
#   - two angular-end side walls
#
# Arc resolution: we subdivide each plank's angular span into segments
# that match the 12-sided cylinder resolution. With 12 sides over 360 deg,
# each side is 30 deg. A plank spans 100 deg ≈ 3.33 sides → use 4 segments.
ARC_SEGS = 4  # segments per plank arc (gives 5 loop verts per edge)

def make_plank(bm, ang_start_deg, ang_end_deg, z_bot, z_top):
    """
    Create one curved plank segment and add it to the bmesh.
    Returns the faces added.
    """
    # Generate the 4 corner arc loops
    # Bottom = z_bot, Top = z_top
    # Inner = INNER_RADIUS, Outer = OUTER_RADIUS
    # Each loop has ARC_SEGS+1 verts
    n = ARC_SEGS + 1  # verts per loop

    inner_bot = [bm.verts.new(v) for v in angle_range_verts(INNER_RADIUS, ang_start_deg, ang_end_deg, z_bot, ARC_SEGS)]
    inner_top = [bm.verts.new(v) for v in angle_range_verts(INNER_RADIUS, ang_start_deg, ang_end_deg, z_top, ARC_SEGS)]
    outer_bot = [bm.verts.new(v) for v in angle_range_verts(OUTER_RADIUS, ang_start_deg, ang_end_deg, z_bot, ARC_SEGS)]
    outer_top = [bm.verts.new(v) for v in angle_range_verts(OUTER_RADIUS, ang_start_deg, ang_end_deg, z_top, ARC_SEGS)]

    bm.verts.ensure_lookup_table()

    faces = []

    # Inner face (facing inward toward tube axis — normal points inward)
    for i in range(ARC_SEGS):
        f = bm.faces.new([inner_bot[i], inner_bot[i+1], inner_top[i+1], inner_top[i]])
        faces.append(f)

    # Outer face (normal points outward)
    for i in range(ARC_SEGS):
        f = bm.faces.new([outer_bot[i+1], outer_bot[i], outer_top[i], outer_top[i+1]])
        faces.append(f)

    # Bottom Z cap (z_bot face, normal points in -Z direction)
    for i in range(ARC_SEGS):
        f = bm.faces.new([inner_bot[i+1], inner_bot[i], outer_bot[i], outer_bot[i+1]])
        faces.append(f)

    # Top Z cap (z_top face, normal points in +Z direction)
    for i in range(ARC_SEGS):
        f = bm.faces.new([inner_top[i], inner_top[i+1], outer_top[i+1], outer_top[i]])
        faces.append(f)

    # Angular start side wall (at ang_start_deg)
    f = bm.faces.new([inner_bot[0], outer_bot[0], outer_top[0], inner_top[0]])
    faces.append(f)

    # Angular end side wall (at ang_end_deg)
    f = bm.faces.new([outer_bot[-1], inner_bot[-1], inner_top[-1], outer_top[-1]])
    faces.append(f)

    return faces

# Build all planks
all_faces = []
for (z_bot, z_top) in z_positions:
    for ang_start in plank_start_angles:
        ang_end = ang_start + PLANK_ANG_DEG
        faces = make_plank(bm, ang_start, ang_end, z_bot, z_top)
        all_faces.extend(faces)

# Recalculate normals to ensure consistency
bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

# ── UV Unwrap ────────────────────────────────────────────────────────────────
# Use smart UV project via bmesh uv layer
uv_layer = bm.loops.layers.uv.new("UVMap")

# Simple planar/box UV: use cylinder projection per face based on face normal
# For a clean result we'll do a simple per-face UV based on local coords
for face in bm.faces:
    n = face.normal
    # Determine dominant axis
    ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
    for loop in face.loops:
        co = loop.vert.co
        if az >= ax and az >= ay:
            # Z-facing face: use XY
            u = (co.x / (OUTER_RADIUS * 2)) + 0.5
            v = (co.y / (OUTER_RADIUS * 2)) + 0.5
        elif ax >= ay:
            # X-facing: use YZ
            u = (co.y / (OUTER_RADIUS * 2)) + 0.5
            v = (co.z / TOTAL_LENGTH) + 0.5
        else:
            # Y-facing: use XZ
            u = (co.x / (OUTER_RADIUS * 2)) + 0.5
            v = (co.z / TOTAL_LENGTH) + 0.5
        loop[uv_layer].uv = (u, v)

# For inner faces: map arc position and Z cleanly
# Override inner/outer curved faces with proper cylindrical UV
for face in bm.faces:
    # Check if all verts are at same radius (approximately)
    radii = [math.sqrt(v.co.x**2 + v.co.y**2) for v in face.verts]
    min_r, max_r = min(radii), max(radii)
    if (max_r - min_r) < 0.01:  # all verts at same radius = curved face
        r = sum(radii) / len(radii)
        for loop in face.loops:
            co = loop.vert.co
            ang = math.atan2(co.y, co.x)  # -pi to pi
            u = (ang / (2 * math.pi)) + 0.5
            v = co.z / TOTAL_LENGTH + 0.5
            loop[uv_layer].uv = (u, v)

# ── Finalize mesh ─────────────────────────────────────────────────────────────
mesh = bpy.data.meshes.new("spinning_tube_01")
bm.to_mesh(mesh)
bm.free()

mesh.validate()

obj = bpy.data.objects.new("spinning_tube_01", mesh)
obj.data.materials.append(mat)

bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)

# Apply transforms (already at origin with no rotation/scale, but do it formally)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# ── Stats ─────────────────────────────────────────────────────────────────────
tri_count = sum(len(p.vertices) - 2 for p in mesh.polygons)
print(f"[SpinningTube] Polygons: {len(mesh.polygons)}, Triangles: {tri_count}")
print(f"[SpinningTube] Vertices: {len(mesh.vertices)}")
print(f"[SpinningTube] Z range: {min(v.co.z for v in mesh.vertices):.2f} to {max(v.co.z for v in mesh.vertices):.2f}")
print(f"[SpinningTube] Inner radius check: {INNER_RADIUS}, Outer: {OUTER_RADIUS}")

# ── Export GLB ────────────────────────────────────────────────────────────────
os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)

bpy.ops.export_scene.gltf(
    filepath=OUTPUT_PATH,
    export_format='GLB',
    use_selection=True,
    export_apply=True,
    export_normals=True,
    export_texcoords=True,
    export_materials='EXPORT',
    export_colors=False,
    export_cameras=False,
    export_lights=False,
)

print(f"[SpinningTube] Exported to: {OUTPUT_PATH}")
