using Godot;

namespace Selbram.Level;

/// <summary>
/// Procedurally generates a hollow cylindrical tube from StaticBody3D segments.
/// Segments are short horizontal rings stacked along the tube's length.
/// Evenly-spaced gaps in each ring rotate with the tube, forcing the player
/// to stay on solid sections as the tube spins.
/// </summary>
public partial class SpinningTube : Node3D
{
	#region Exports

	[ExportGroup("Geometry")]
	[Export(PropertyHint.Range, "2.0,15.0,0.5")]
	public float Radius { get; set; } = 5.0f;

	[Export(PropertyHint.Range, "5.0,60.0,1.0")]
	public float Length { get; set; } = 24.0f;

	[Export(PropertyHint.Range, "0.1,1.0,0.05")]
	public float WallThickness { get; set; } = 0.4f;

	[Export(PropertyHint.Range, "6,24,1")]
	public int SegmentCount { get; set; } = 12;

	[ExportGroup("Gaps")]
	[Export(PropertyHint.Range, "1,6,1")]
	public int GapCount { get; set; } = 3;

	// Number of horizontal rings stacked along the tube length.
	[Export(PropertyHint.Range, "2,20,1")]
	public int RingCount { get; set; } = 8;

	[ExportGroup("Rotation")]
	// Positive = counterclockwise from the entrance end (+Z).
	// Negative = clockwise from the entrance end, which is what the player sees.
	[Export(PropertyHint.Range, "-5.0,5.0,0.1")]
	public float SpinSpeed { get; set; } = -0.4f;

	#endregion

	public override void _Ready()
	{
		BuildTube();
	}

	public override void _PhysicsProcess(double delta)
	{
		RotateZ((float)(delta * SpinSpeed));
	}

	private void BuildTube()
	{
		float angleStep = Mathf.Tau / SegmentCount;
		// Chord width of each segment — leave a small visual gap between pieces
		float segmentWidth = 2.0f * Radius * Mathf.Sin(Mathf.Pi / SegmentCount) * 0.94f;

		float slotLength = Length / RingCount;
		// Each ring is shorter than its slot, leaving a visible gap between rings
		float plankLength = slotLength * 0.75f;

		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.2f, 0.6f, 0.95f),
			Metallic = 0.5f,
			Roughness = 0.2f
		};

		for (int ring = 0; ring < RingCount; ring++)
		{
			float zPos = -Length * 0.5f + (ring + 0.5f) * slotLength;

			for (int seg = 0; seg < SegmentCount; seg++)
			{
				if (IsGap(seg)) continue;

				float angle = seg * angleStep;
				var body = new StaticBody3D();

				var collision = new CollisionShape3D
				{
					Shape = new BoxShape3D { Size = new Vector3(segmentWidth, WallThickness, plankLength) }
				};
				body.AddChild(collision);

				var meshInst = new MeshInstance3D();
				meshInst.Mesh = new BoxMesh { Size = new Vector3(segmentWidth, WallThickness, plankLength) };
				meshInst.MaterialOverride = material;
				body.AddChild(meshInst);

				// Radial position + rotation so each segment faces inward
				body.Position = new Vector3(Mathf.Cos(angle) * Radius, Mathf.Sin(angle) * Radius, zPos);
				body.Rotation = new Vector3(0.0f, 0.0f, angle);

				AddChild(body);
			}
		}
	}

	// Distributes GapCount gaps evenly around the SegmentCount ring.
	private bool IsGap(int segmentIndex)
	{
		for (int g = 0; g < GapCount; g++)
		{
			int gapPos = Mathf.RoundToInt((float)g / GapCount * SegmentCount);
			if (segmentIndex == gapPos) return true;
		}
		return false;
	}
}
