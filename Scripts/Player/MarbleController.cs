using Godot;
using Selbram.Input;
using Selbram.Level;
using Selbram.PowerUps;

namespace Selbram.Player;

/// <summary>
/// Main marble physics controller. Uses RigidBody3D with custom force application.
/// Designed to be driven by any IInputProvider for multiplayer compatibility.
/// </summary>
public partial class MarbleController : RigidBody3D
{
	#region Exports - Physics Tuning

	[ExportGroup("Movement")]
	[Export(PropertyHint.Range, "10.0,100.0,1.0")]
	public float RollForce { get; set; } = 14.0f;

	[Export(PropertyHint.Range, "5.0,50.0,1.0")]
	public float MaxSpeed { get; set; } = 18.0f;

	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float AirControlFactor { get; set; } = 0.40f;

	[Export]
	public float GroundDeceleration { get; set; } = 7.0f;

	[Export(PropertyHint.Range, "0.0,2.0,0.1")]
	public float SlopeAssist { get; set; } = 0.1f;

	[Export(PropertyHint.Range, "0.0,3.0,0.1")]
	public float SteeringFactor { get; set; } = 0.2f;

	[ExportGroup("Jump")]
	[Export(PropertyHint.Range, "5.0,30.0,1.0")]
	public float JumpImpulse { get; set; } = 15.0f;

	[Export(PropertyHint.Range, "0.0,0.3,0.01")]
	public float JumpCoyoteTime { get; set; } = 0.1f;

	[Export(PropertyHint.Range, "0.0,0.3,0.01")]
	public float JumpBufferTime { get; set; } = 0.1f;

	[Export(PropertyHint.Range, "0.0,15.0,0.5")]
	public float JumpCutSpeed { get; set; } = 4.0f;

	[ExportGroup("Ground Detection")]
	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float GroundCheckDistance { get; set; } = 0.2f;

	[Export(PropertyHint.Range, "0.0,90.0,5.0")]
	public float MaxSlopeAngle { get; set; } = 60.0f;

	[ExportGroup("Power-Up Modifiers")]
	[Export]
	public float SuperJumpMultiplier { get; set; } = 2.0f;

	[Export]
	public float SuperSpeedMultiplier { get; set; } = 2.0f;

	#endregion

	#region State

	/// <summary>
	/// Current marble state (for networking/replay).
	/// </summary>
	public MarbleState CurrentState { get; private set; }

	/// <summary>
	/// The input provider driving this marble.
	/// </summary>
	public IInputProvider? InputProvider { get; set; }

	/// <summary>
	/// Reference to camera for camera-relative movement.
	/// </summary>
	public Node3D? CameraReference { get; set; }

	/// <summary>
	/// Current simulation tick.
	/// </summary>
	public uint CurrentTick { get; private set; }

	/// <summary>
	/// Active power-up type.
	/// </summary>
	public PowerUpType ActivePowerUp { get; private set; } = PowerUpType.None;

	/// <summary>
	/// Remaining duration of active power-up.
	/// </summary>
	public float PowerUpTimer { get; private set; }

	// Internal state
	private float _timeSinceGrounded;
	private float _jumpBufferTimer;
	private bool _hasJumped;
	private Vector3 _groundNormal = Vector3.Up;
	private SurfaceProperties _currentSurface = SurfaceProperties.Default;

	// Cached references
	private RayCast3D? _groundRay;

	#endregion

	#region Signals

	[Signal]
	public delegate void JumpedEventHandler();

	[Signal]
	public delegate void GroundedEventHandler(bool isGrounded);

	[Signal]
	public delegate void PowerUpActivatedEventHandler(PowerUpType type);

	[Signal]
	public delegate void PowerUpExpiredEventHandler(PowerUpType type);

	#endregion

	public override void _Ready()
	{
		// Set up physics properties
		PhysicsMaterialOverride = new PhysicsMaterial
		{
			Friction = 0.5f,
			Bounce = 0.3f
		};

		// Create ground detection raycast
		_groundRay = new RayCast3D
		{
			TargetPosition = new Vector3(0, -0.5f - GroundCheckDistance, 0),
			Enabled = true,
			CollideWithAreas = false,
			CollideWithBodies = true
		};
		AddChild(_groundRay);

		// Initialize state
		SyncStateFromPhysics();
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		CurrentTick++;

		// Get input
		var input = InputProvider?.GetInput(CurrentTick) ?? MarbleInput.Empty;

		// Update ground detection
		UpdateGroundState();

		// Update timers
		UpdateTimers(dt, input);

		// Apply movement
		ApplyMovement(input, dt);

		// Handle jump
		HandleJump(input);
		HandleVariableJump(input);

		// Update power-ups
		UpdatePowerUp(dt);

		// Clamp velocity
		ClampVelocity();

		// Sync state for networking
		SyncStateFromPhysics();
	}

	#region Ground Detection

	private void UpdateGroundState()
	{
		if (_groundRay == null) return;

		bool wasGrounded = CurrentState.IsGrounded;
		bool isGrounded = false;
		_groundNormal = Vector3.Up;

		if (_groundRay.IsColliding())
		{
			_groundNormal = _groundRay.GetCollisionNormal();
			float slopeAngle = Mathf.RadToDeg(Mathf.Acos(_groundNormal.Dot(Vector3.Up)));

			if (slopeAngle <= MaxSlopeAngle)
			{
				isGrounded = true;

				// Try to get surface properties from collider
				var collider = _groundRay.GetCollider();
				if (collider is StaticBody3D body && body.PhysicsMaterialOverride is PhysicsMaterial)
				{
					// Could extend this to read custom surface properties
				}
			}
		}

		// Fallback: check if we have any contacts (simplified check)
		if (!isGrounded && GetContactCount() > 0)
		{
			// If raycast missed but we have contacts, assume grounded on flat surface
			// This handles edge cases where the marble is resting on geometry
			isGrounded = true;
			_groundNormal = Vector3.Up;
		}

		var state = CurrentState;
		state.IsGrounded = isGrounded;
		state.GroundNormal = _groundNormal;
		CurrentState = state;

		if (wasGrounded != isGrounded)
		{
			EmitSignal(SignalName.Grounded, isGrounded);
		}

		if (isGrounded)
		{
			_timeSinceGrounded = 0;
			_hasJumped = false;
		}
	}

	#endregion

	#region Movement

	private void ApplyMovement(MarbleInput input, float delta)
	{
		// Apply deceleration when grounded with no input
		if (!input.HasMovement)
		{
			if (CurrentState.IsGrounded)
			{
				ApplyDeceleration(delta);
			}
			return;
		}

		// Get camera-relative movement direction
		Vector3 moveDirection = GetCameraRelativeDirection(input.Movement);

		// Calculate force
		float force = RollForce;
		float maxSpd = MaxSpeed;

		// Apply surface modifiers
		force *= _currentSurface.Friction;
		maxSpd *= _currentSurface.SpeedModifier;

		// Apply power-up modifiers
		if (ActivePowerUp == PowerUpType.SuperSpeed)
		{
			force *= SuperSpeedMultiplier;
			maxSpd *= SuperSpeedMultiplier;
		}

		// Reduce control in air
		if (!CurrentState.IsGrounded)
		{
			force *= AirControlFactor;
		}

		// Apply slope assist when grounded
		if (CurrentState.IsGrounded && _groundNormal != Vector3.Up)
		{
			// Project movement onto slope
			moveDirection = moveDirection.Slide(_groundNormal).Normalized();

			// Add downhill assist
			Vector3 downhill = Vector3.Down.Slide(_groundNormal);
			float alignment = moveDirection.Dot(downhill.Normalized());
			if (alignment > 0)
			{
				force += force * alignment * SlopeAssist;
			}
		}

		// Check if we're under max speed in the movement direction
		Vector3 horizontalVelocity = new(LinearVelocity.X, 0, LinearVelocity.Z);
		float currentSpeedInDirection = horizontalVelocity.Dot(moveDirection);

		if (currentSpeedInDirection < maxSpd)
		{
			// Apply force as torque for realistic rolling
			Vector3 torqueAxis = Vector3.Up.Cross(moveDirection).Normalized();
			ApplyTorque(torqueAxis * force * delta * 60);

			// Apply direct force for responsiveness
			ApplyCentralForce(moveDirection * force * 0.5f);
		}

		// Counter lateral drift so direction changes feel tight rather than slidey
		//Vector3 lateralVel = horizontalVelocity - moveDirection * currentSpeedInDirection;
		//ApplyCentralForce(-lateralVel * force * SteeringFactor);
	}

	private Vector3 GetCameraRelativeDirection(Vector2 input)
	{
		if (CameraReference == null)
		{
			// No camera, use world-relative
			return new Vector3(input.X, 0, input.Y).Normalized();
		}

		// Get camera's forward and right vectors (flattened to horizontal)
		Vector3 camForward = -CameraReference.GlobalBasis.Z;
		camForward.Y = 0;
		camForward = camForward.Normalized();

		Vector3 camRight = CameraReference.GlobalBasis.X;
		camRight.Y = 0;
		camRight = camRight.Normalized();

		// Combine into movement direction
		Vector3 direction = camRight * input.X + camForward * -input.Y;
		return direction.LengthSquared() > 0.01f ? direction.Normalized() : Vector3.Zero;
	}

	private void ClampVelocity()
	{
		float maxSpd = MaxSpeed;
		if (ActivePowerUp == PowerUpType.SuperSpeed)
		{
			maxSpd *= SuperSpeedMultiplier;
		}
		maxSpd *= _currentSurface.SpeedModifier;

		// Only clamp horizontal velocity
		Vector3 vel = LinearVelocity;
		Vector2 horizontalVel = new(vel.X, vel.Z);

		if (horizontalVel.Length() > maxSpd)
		{
			horizontalVel = horizontalVel.Normalized() * maxSpd;
			LinearVelocity = new Vector3(horizontalVel.X, vel.Y, horizontalVel.Y);
		}
	}

	private void ApplyDeceleration(float delta)
	{
		Vector3 vel = LinearVelocity;
		Vector2 horizontalVel = new(vel.X, vel.Z);
		float speed = horizontalVel.Length();

		// Stop completely at low speeds to prevent jittering
		if (speed < 0.1f)
		{
			LinearVelocity = new Vector3(0, vel.Y, 0);
			return;
		}

		// Reduce speed by deceleration amount
		float decelAmount = GroundDeceleration * _currentSurface.RollResistance * delta;
		float newSpeed = Mathf.Max(0, speed - decelAmount);

		// Apply new velocity, preserving direction
		horizontalVel = horizontalVel.Normalized() * newSpeed;
		LinearVelocity = new Vector3(horizontalVel.X, vel.Y, horizontalVel.Y);

		// Slow rotation as well
		AngularVelocity = AngularVelocity.Lerp(Vector3.Zero, delta * 3.0f);
	}

	#endregion

	#region Jump

	private void UpdateTimers(float delta, MarbleInput input)
	{
		_timeSinceGrounded += delta;

		if (input.JumpPressed)
		{
			_jumpBufferTimer = JumpBufferTime;
		}
		else
		{
			_jumpBufferTimer = Mathf.Max(0, _jumpBufferTimer - delta);
		}
	}

	private void HandleJump(MarbleInput input)
	{
		// Check if we can jump (coyote time) and want to jump (buffer)
		bool canJump = _timeSinceGrounded <= JumpCoyoteTime && !_hasJumped;
		bool wantsJump = _jumpBufferTimer > 0;

		if (canJump && wantsJump)
		{
			PerformJump();
		}
	}

	private void HandleVariableJump(MarbleInput input)
	{
		// If jump released early while still rising, clamp upward velocity for a shorter jump
		if (_hasJumped && !CurrentState.IsGrounded && !input.JumpHeld && LinearVelocity.Y > JumpCutSpeed)
		{
			var vel = LinearVelocity;
			vel.Y = JumpCutSpeed;
			LinearVelocity = vel;
		}
	}

	private void PerformJump()
	{
		float impulse = JumpImpulse;

		// Apply power-up modifier
		if (ActivePowerUp == PowerUpType.SuperJump)
		{
			impulse *= SuperJumpMultiplier;
		}

		// Jump along ground normal (or up if steep)
		Vector3 jumpDirection = _groundNormal;
		if (jumpDirection.Dot(Vector3.Up) < 0.5f)
		{
			jumpDirection = (jumpDirection + Vector3.Up).Normalized();
		}

		// Clear vertical velocity and apply impulse
		Vector3 vel = LinearVelocity;
		vel.Y = 0;
		LinearVelocity = vel;

		ApplyCentralImpulse(jumpDirection * impulse);

		_hasJumped = true;
		_jumpBufferTimer = 0;
		_timeSinceGrounded = JumpCoyoteTime + 1; // Prevent double jump

		EmitSignal(SignalName.Jumped);
	}

	#endregion

	#region Power-Ups

	/// <summary>
	/// Grants a power-up to the marble.
	/// </summary>
	public void GrantPowerUp(PowerUpType type, float duration = 5.0f)
	{
		// Expire current power-up if any
		if (ActivePowerUp != PowerUpType.None)
		{
			EmitSignal(SignalName.PowerUpExpired, (int)ActivePowerUp);
		}

		ActivePowerUp = type;
		PowerUpTimer = duration;

		EmitSignal(SignalName.PowerUpActivated, (int)type);
	}

	/// <summary>
	/// Uses the currently held power-up (if any).
	/// Called when the player presses the power-up button.
	/// </summary>
	public void UsePowerUp()
	{
		if (ActivePowerUp == PowerUpType.None) return;

		switch (ActivePowerUp)
		{
			case PowerUpType.SuperJump:
				// Super jump is automatic on next jump
				break;

			case PowerUpType.SuperSpeed:
				// Super speed is automatic while held
				break;

			case PowerUpType.Gyrocopter:
				// Would apply slow fall effect
				break;
		}
	}

	private void UpdatePowerUp(float delta)
	{
		if (ActivePowerUp == PowerUpType.None) return;

		PowerUpTimer -= delta;

		if (PowerUpTimer <= 0)
		{
			var expired = ActivePowerUp;
			ActivePowerUp = PowerUpType.None;
			PowerUpTimer = 0;
			EmitSignal(SignalName.PowerUpExpired, (int)expired);
		}
	}

	#endregion

	#region State Sync

	private void SyncStateFromPhysics()
	{
		var state = CurrentState;
		state.Position = GlobalPosition;
		state.Velocity = LinearVelocity;
		state.AngularVelocity = AngularVelocity;
		state.Rotation = GlobalBasis.GetRotationQuaternion();
		state.ActivePowerUp = ActivePowerUp;
		state.PowerUpTimer = PowerUpTimer;
		state.Tick = CurrentTick;
		CurrentState = state;
	}

	/// <summary>
	/// Applies a state snapshot (for network reconciliation).
	/// </summary>
	public void ApplyState(MarbleState state)
	{
		GlobalPosition = state.Position;
		LinearVelocity = state.Velocity;
		AngularVelocity = state.AngularVelocity;
		GlobalBasis = new Basis(state.Rotation);
		ActivePowerUp = state.ActivePowerUp;
		PowerUpTimer = state.PowerUpTimer;
		CurrentTick = state.Tick;
		CurrentState = state;
	}

	/// <summary>
	/// Resets the marble to a spawn position.
	/// </summary>
	public void Respawn(Vector3 position, Vector3? lookDirection = null)
	{
		GlobalPosition = position;
		LinearVelocity = Vector3.Zero;
		AngularVelocity = Vector3.Zero;
		GlobalRotation = Vector3.Zero;

		ActivePowerUp = PowerUpType.None;
		PowerUpTimer = 0;

		_hasJumped = false;
		_jumpBufferTimer = 0;
		_timeSinceGrounded = 0;

		InputProvider?.Reset();
		SyncStateFromPhysics();
	}

	#endregion
}
