---
name: Track Kit — Modular Straight Segment
description: Dimensions, origin convention, and connection profile for the Selbram modular track kit
type: project
---

The modular track kit uses a shared connection profile so pieces snap together with no offset math. All pieces must conform to these specs.

**Connection profile (end faces):** 4m wide (X) x 0.5m tall (Y). This is the universal snap dimension.

**Straight segment (track_straight_8m.glb):**
- Dimensions: 8m (Z) x 4m (X) x 0.5m (Y)
- Origin: center-bottom of Z=0 end face (X=0, Y=0, Z=0)
- Track extends in +Z direction
- Place at Z=0, Z=8, Z=16... to tile with no offset math
- Tris: 60, Verts: 32
- Material: track_steel_blue — Principled BSDF, Base Color (0.25, 0.28, 0.35), Roughness 0.45, Metallic 0.3
- Exported to: Assets/Track/track_straight_8m.glb

**Bevel spec:** 0.05m, 1 segment, all edges — keeps it under 80 tris for straight pieces.

**Side panel detail:** inset_individual on pure X-facing side faces, thickness=0.06, depth=-0.02. Creates the "platform slab" visual weight.

**Future pieces must match:**
- Same 4m x 0.5m end profile
- Same origin convention (center-bottom at the start/entry connection end)
- Same bevel amount (0.05m) for visual consistency
- Same material (track_steel_blue) for color matching

**Why:** The 4m x 0.5m connection profile and Z=0 origin are the kit's snap contract. If any piece deviates, placement math breaks.

**How to apply:** When building curved, banked, or ramp connectors, always start by verifying the end face profile is exactly 4m x 0.5m and the entry origin is at the connection point.
