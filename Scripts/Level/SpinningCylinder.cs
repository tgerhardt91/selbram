using System.Collections.Generic;
using Godot;

namespace Selbram.Level;

public partial class SpinningCylinder : AnimatableBody3D
{
	[Export(PropertyHint.Range, "-5.0,5.0,0.1")]
	public float SpinSpeed { get; set; } = -1.5f;

	public override void _Ready()
	{
		var meshInstances = new List<MeshInstance3D>();
		CollectMeshInstances(this, meshInstances);

		foreach (var meshInst in meshInstances)
		{
			if (meshInst.Mesh == null) continue;
			AddChild(new CollisionShape3D
			{
				Shape = meshInst.Mesh.CreateTrimeshShape(),
				Transform = GlobalTransform.AffineInverse() * meshInst.GlobalTransform
			});
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		RotateZ((float)(delta * SpinSpeed));
	}

	private static void CollectMeshInstances(Node node, List<MeshInstance3D> result)
	{
		foreach (var child in node.GetChildren())
		{
			if (child is MeshInstance3D meshInst)
				result.Add(meshInst);
			CollectMeshInstances(child, result);
		}
	}
}
