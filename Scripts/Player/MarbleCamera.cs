using Godot;
using Selbram.Input;

namespace Selbram.Player;

/// <summary>
/// Third-person orbit camera that follows the marble.
/// Handles smooth following, rotation, and collision avoidance.
/// </summary>
public partial class MarbleCamera : Node3D
{
	#region Exports

	[ExportGroup("Target")]
	[Export]
	public NodePath? TargetPath { get; set; }

	[ExportGroup("Distance")]
	[Export(PropertyHint.Range, "2.0,20.0,0.5")]
	public float Distance { get; set; } = 12.0f;

	[Export(PropertyHint.Range, "0.0,10.0,0.5")]
	public float Height { get; set; } = 4.0f;

	[Export(PropertyHint.Range, "1.0,15.0,0.5")]
	public float MinDistance { get; set; } = 3.0f;

	[ExportGroup("Rotation")]
	[Export(PropertyHint.Range, "0.5,5.0,0.1")]
	public float RotationSpeed { get; set; } = 2.0f;

	[Export(PropertyHint.Range, "-80.0,0.0,5.0")]
	public float MinPitch { get; set; } = -60.0f;

	[Export(PropertyHint.Range, "0.0,80.0,5.0")]
	public float MaxPitch { get; set; } = 30.0f;

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
	/// Current pitch rotation in radians.
	/// </summary>
	public float Pitch { get; set; }

	/// <summary>
	/// The input provider for camera rotation.
	/// </summary>
	public IInputProvider? InputProvider { get; set; }

	// Internal
	private Camera3D? _camera;
	private Vector3 _currentPosition;
	private float _currentDistance;

	#endregion

	public override void _Ready()
	{
		// Get or create camera
		_camera = GetNodeOrNull<Camera3D>("Camera3D");
		if (_camera == null)
		{
			_camera = new Camera3D();
			AddChild(_camera);
		}

		// Get target from path if set
		if (TargetPath != null && !TargetPath.IsEmpty)
		{
			Target = GetNodeOrNull<Node3D>(TargetPath);
		}

		// Initialize rotation
		Pitch = Mathf.DegToRad(-20.0f); // Slight downward angle by default
		_currentDistance = Distance;

		// Initialize position
		if (Target != null)
		{
			_currentPosition = Target.GlobalPosition;
		}

		// Make this the current camera
		_camera.Current = true;
	}

	public override void _Process(double delta)
	{
		if (Target == null || _camera == null) return;

		float dt = (float)delta;

		// Handle rotation input
		HandleRotationInput(dt);

		// Calculate desired position
		Vector3 targetPos = Target.GlobalPosition;

		// Add look-ahead based on velocity
		if (Target is RigidBody3D rb && LookAheadFactor > 0)
		{
			Vector3 velocity = rb.LinearVelocity;
			velocity.Y = 0; // Only horizontal look-ahead
			targetPos += velocity * LookAheadFactor * 0.1f;
		}

		// Smooth follow the target
		_currentPosition = _currentPosition.Lerp(targetPos, dt * PositionSmoothing);

		// Calculate camera offset based on rotation
		Vector3 offset = CalculateCameraOffset();

		// Check for collisions and adjust distance
		float desiredDistance = Distance;
		if (EnableCollision)
		{
			desiredDistance = CheckCollision(_currentPosition, offset, Distance);
		}

		// Smooth distance changes (slow for stability)
		_currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, dt * DistanceSmoothing);

		// Apply position
		Vector3 cameraPos = _currentPosition + offset.Normalized() * _currentDistance;
		cameraPos.Y = _currentPosition.Y + Height + Mathf.Sin(Pitch) * _currentDistance * 0.5f;

		GlobalPosition = cameraPos;

		// Keep our basis aligned with Yaw so MarbleController can read it for camera-relative movement
		GlobalRotation = new Vector3(0, Yaw, 0);

		// Look at target
		_camera.LookAt(_currentPosition, Vector3.Up);
	}

	private void HandleRotationInput(float delta)
	{
		// Get input from provider or return
		if (InputProvider == null) return;

		var input = InputProvider.GetInput(0); // Tick doesn't matter for camera

		if (input.CameraRotation.LengthSquared() > 0.01f)
		{
			Yaw -= input.CameraRotation.X * RotationSpeed * delta;
			Pitch += input.CameraRotation.Y * RotationSpeed * delta;

			// Clamp pitch
			Pitch = Mathf.Clamp(Pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
		}
	}

	/// <summary>
	/// Handles mouse motion for camera rotation.
	/// Call this from _Input when mouse is captured.
	/// </summary>
	public void HandleMouseMotion(Vector2 motion, float sensitivity = 0.002f)
	{
		Yaw += motion.X * sensitivity;
		Pitch -= motion.Y * sensitivity;
		Pitch = Mathf.Clamp(Pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
	}

	private Vector3 CalculateCameraOffset()
	{
		// Calculate offset based on yaw and pitch
		float horizontalDistance = Mathf.Cos(Pitch);
		float verticalOffset = Mathf.Sin(Pitch);

		Vector3 offset = new(
			Mathf.Sin(Yaw) * horizontalDistance,
			verticalOffset,
			Mathf.Cos(Yaw) * horizontalDistance
		);

		return offset * Distance;
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

		// Exclude the target from collision checks
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
			_currentDistance = Distance;
		}
	}
}
