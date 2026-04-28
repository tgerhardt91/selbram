---
name: Spinning Cylinder Asset Specs
description: Construction details for SpinningCylinder.glb — the hollow tube obstacle with gap rings
type: project
---

**Asset:** `Assets/SpinningCylinder.glb`

**Geometry specs:**
- Inner radius: 5.5 units, Outer radius: 6.0 units (wall thickness 0.5)
- Length: 70 units along Z axis, origin at center
- 24 radial segments around circumference
- 2 gap rings: at Z=21 (0° offset) and Z=49 (60° offset, staggered)
- 3 gaps per ring at 120° apart, each 45° wide and 12 units long
- 600 triangles, 288 verts — well within game budget

**Material:** `mat_spinning_cylinder` — Principled BSDF, base color (0.2, 0.6, 0.95), metallic 0.7, roughness 0.25

**Construction approach:** Built entirely via bmesh in Python — no booleans. Rings of inner/outer vertex pairs at each Z-slice boundary. Gap detection per angular segment per Z-midpoint determines which quads to skip. Angular and Z-direction edge walls added at all gap/solid transitions. End caps are annular quads.

**Why this approach:** Booleans on cylinders tend to produce N-gons and bad topology. Direct bmesh construction gives full control over face winding and ensures 0 non-manifold edges.

**Godot usage:** StaticBody3D with ConcavePolygonShape3D collision (since marble rolls on inside surface). Spinning handled by Godot AnimationPlayer or a script rotating the node around its local Z axis.
