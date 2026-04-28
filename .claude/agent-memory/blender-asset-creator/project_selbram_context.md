---
name: Selbram Project Context
description: Scale conventions, style, pipeline, and naming established for Selbram marble game assets
type: project
---

Selbram is a 3D marble platformer (Godot 4.3, C#) inspired by Marble Blast Ultra. 1 Blender unit = 1 Godot meter. Marble radius is 0.5–1 unit.

**Scale reference:** Marble moves at ~5–8 units/sec. A 10–15 second obstacle section = 60–80 units long.

**Export pipeline:** GLB format (`export_yup=True` required). Export to `Assets/` subdirectory within project root. Blender GLTF exporter parameters that work: `filepath`, `export_format='GLB'`, `use_selection`, `export_apply=True`, `export_materials='EXPORT'`, `export_normals=True`, `export_texcoords=True`, `export_yup=True`, `export_cameras=False`, `export_lights=False`. The `export_colors` param does NOT exist in this Blender version — omit it.

**Style:** Low-poly, clean geometry, smooth shading with edge split modifier (45 deg threshold). Principled BSDF materials only.

**Axis convention:** Level obstacles that spin around a horizontal axis should be aligned along Z in Blender (Godot SpinningCylinder script rotates around Z).

**Why:** Godot imports GLB with Y-up and expects Z as the forward/spin axis for the SpinningCylinder script.

**How to apply:** Always use `export_yup=True`. Align rotating obstacle meshes along Z axis in Blender.
