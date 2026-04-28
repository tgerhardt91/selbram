using Godot;
using Selbram.Input;

namespace Selbram.Player;

/// <summary>
/// Third-person orbit camera that follows the marble.
/// Fixed distance behind and above the marble — only yaw rotation is allowed.
/// </summary>
public partial class MarbleCamera : Node3D
{
	#region Exports

	[ExportGroup("Target")]
	[Export]
	public NodePath? TargetPath { get; set; }

	[ExportGroup("Distance")]
	[Export(PropertyHint.Range, "2.0,20.0,0.5")]
	public float Distance { get; set; } = 4.0f;

	[Export(PropertyHint.Range, "0.0,10.0,0.5")]
	public float Height { get; set; } = 2.0f;

	[Export(PropertyHint.Range, "1.0,15.0,0.5")]
	public float MinDistance { get; set; } = 1.5f;

	[ExportGroup("Rotation")]
	[Export(PropertyHint.Range, "0.5,5.0,0.1")]
	public float RotationSpeed { get; set; } = 2.0f;

	[Export(PropertyHint.Range, "0.5,3.0,0.1")]
	public float PitchSpeed { get; set; } = 1.5f;

	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float MaxPitchAngle { get; set; } = 0.4f;

	[Export(PropertyHint.Range, "1.0,15.0,0.5")]
	public float PitchSnapSpeed { get; set; } = 5.0f;

	[ExportGroup("Smoothing")]
	[Export(PropertyHint.Range, "1.0,20.0,0.5")]
	public float PositionSmoothing { get; set; } = 8.0f;

	[Export(PropertyHint.Range, "0.5,10.0,0.5")]
	public float DistanceSmoothing { get; set; } = 2.0f;

	[Export(PropertyHint.Range, "0.0,2.0,0.1")]
	public float LookAheadFactor { get; set; } = 0.15f;

	[ExportGroup("Collision")]
	[Export]
	public bool EnableCollision { get; set; } = true;

	[Export]
	public uint CollisionMask { get; set; } = 1;

	[Export(PropertyHint.Range, "0.1,1.0,0.05")]
	public float CollisionPadding { get; set; } = 0.3f;

	#endregion

	#region State

	/// <summary>
	/// The target node to follow.
	/// </summary>
	public Node3D? Target { get; set; }

	/// <summary>
	/// Current yaw rotation in radians.
	/// </summary>
	public float Yaw { get; set; }

	/// <summary>
	/// The input provider for camera rotation.
	/// </summary>
	public IInputProvider? InputProvider { get; set; }

	// Internal
	private Camera3D? _camera;
	private Vector3 _currentPosition;
	private float _currentDistance;
	private float _pitchOffset;

	#endregion

	public override void _Ready()
	{
		_camera = GetNodeOrNull<Camera3D>("Camera3D");
		if (_camera == null)
		{
			_camera = new Camera3D();
			AddChild(_camera);
		}

		if (TargetPath != null && !TargetPath.IsEmpty)
		{
			Target = GetNodeOrNull<Node3D>(TargetPath);
		}

		if (Target != null)
		{
			_currentPosition = Target.GlobalPosition;
		}

		_currentDistance = Mathf.Sqrt(Distance * Distance + Height * Height);

		_camera.Current = true;
	}

	public override void _Process(double delta)
	{
		if (Target == null || _camera == null) return;

		float dt = (float)delta;

		HandleRotationInput(dt);

		// Smooth follow target with optional look-ahead
		Vector3 targetPos = Target.GlobalPosition;
		if (Target is RigidBody3D rb && LookAheadFactor > 0)
		{
			Vector3 vel = rb.LinearVelocity;
			vel.Y = 0;
			targetPos += vel * LookAheadFactor * 0.1f;
		}
		_currentPosition = _currentPosition.Lerp(targetPos, dt * PositionSmoothing);

		// Spherical orbit: pitch-adjusted position behind and above marble
		Vector3 behindDir = new Vector3(Mathf.Sin(Yaw), 0, Mathf.Cos(Yaw));
		float orbitRadius = Mathf.Sqrt(Distance * Distance + Height * Height);
		float elevation = Mathf.Atan2(Height, Distance) + _pitchOffset;
		Vector3 offset = behindDir * (orbitRadius * Mathf.Cos(elevation))
						 + Vector3.Up * (orbitRadius * Mathf.Sin(elevation));

		// Collision: cast ray along the offset to prevent clipping
		float desiredDist = orbitRadius;
		if (EnableCollision)
		{
			desiredDist = CheckCollision(_currentPosition, offset, desiredDist);
		}
		_currentDistance = Mathf.Lerp(_currentDistance, desiredDist, dt * DistanceSmoothing);

		GlobalPosition = _currentPosition + offset.Normalized() * _currentDistance;

		// Keep basis aligned with Yaw so MarbleController can read it for camera-relative movement
		GlobalRotation = new Vector3(0, Yaw, 0);

		_camera.LookAt(_currentPosition, Vector3.Up);
	}

	private void HandleRotationInput(float delta)
	{
		if (InputProvider == null) return;
		var input = InputProvider.GetInput(0);

		if (Mathf.Abs(input.CameraRotation.X) > 0.01f)
			Yaw -= input.CameraRotation.X * RotationSpeed * delta;

		if (Mathf.Abs(input.CameraRotation.Y) > 0.01f)
			_pitchOffset = Mathf.Clamp(
				_pitchOffset + input.CameraRotation.Y * PitchSpeed * delta,
				-MaxPitchAngle, MaxPitchAngle);
		else
			_pitchOffset = Mathf.Lerp(_pitchOffset, 0f, delta * PitchSnapSpeed);
	}

	/// <summary>
	/// Handles mouse motion for camera rotation. Only yaw is applied.
	/// </summary>
	public void HandleMouseMotion(Vector2 motion, float sensitivity = 0.002f)
	{
		Yaw += motion.X * sensitivity;
	}

	private float CheckCollision(Vector3 target, Vector3 direction, float maxDistance)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		if (spaceState == null) return maxDistance;

		var query = PhysicsRayQueryParameters3D.Create(
			target,
			target + direction.Normalized() * (maxDistance + CollisionPadding),
			CollisionMask
		);

		if (Target is CollisionObject3D co)
		{
			query.Exclude = new Godot.Collections.Array<Rid> { co.GetRid() };
		}

		var result = spaceState.IntersectRay(query);

		if (result.Count > 0)
		{
			Vector3 hitPoint = (Vector3)result["position"];
			float hitDistance = target.DistanceTo(hitPoint) - CollisionPadding;
			return Mathf.Max(MinDistance, hitDistance);
		}

		return maxDistance;
	}

	/// <summary>
	/// Gets the camera's forward direction (for camera-relative movement).
	/// </summary>
	public Vector3 GetForwardDirection()
	{
		return new Vector3(Mathf.Sin(Yaw), 0, Mathf.Cos(Yaw)).Normalized();
	}

	/// <summary>
	/// Sets the camera rotation to look in a specific direction.
	/// </summary>
	public void SetLookDirection(Vector3 direction)
	{
		direction.Y = 0;
		if (direction.LengthSquared() > 0.01f)
		{
			direction = direction.Normalized();
			Yaw = Mathf.Atan2(direction.X, direction.Z);
		}
	}

	/// <summary>
	/// Instantly moves the camera to the target position (no smoothing).
	/// </summary>
	public void SnapToTarget()
	{
		if (Target != null)
		{
			_currentPosition = Target.GlobalPosition;
			_currentDistance = Mathf.Sqrt(Distance * Distance + Height * Height);
		}
	}
}
