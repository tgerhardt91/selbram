using Godot;
using Selbram.Input;
using Selbram.Player;

namespace Selbram.Core;

/// <summary>
/// Main game manager singleton. Handles game state, input routing, and tick management.
/// Designed with multiplayer architecture in mind.
/// </summary>
public partial class GameManager : Node
{
	#region Singleton

	private static GameManager? _instance;
	public static GameManager Instance => _instance ?? throw new System.InvalidOperationException("GameManager not initialized");
	public static bool IsInitialized => _instance != null;

	#endregion

	#region Exports

	[Export]
	public PackedScene? MarbleScene { get; set; }

	[Export]
	public NodePath? SpawnPointPath { get; set; }

	#endregion

	#region State

	/// <summary>
	/// Current game mode.
	/// </summary>
	public GameMode Mode { get; private set; } = GameMode.Local;

	/// <summary>
	/// Current simulation tick (for networking).
	/// </summary>
	public uint CurrentTick { get; private set; }

	/// <summary>
	/// Is the game currently paused?
	/// </summary>
	public bool IsPaused { get; private set; }

	/// <summary>
	/// The local player's marble controller.
	/// </summary>
	public MarbleController? LocalMarble { get; private set; }

	/// <summary>
	/// The local player's camera.
	/// </summary>
	public MarbleCamera? LocalCamera { get; private set; }

	/// <summary>
	/// The local input provider.
	/// </summary>
	public LocalInputProvider LocalInput { get; private set; } = new();

	/// <summary>
	/// Current level timer.
	/// </summary>
	public float LevelTime { get; private set; }

	/// <summary>
	/// Is the level timer running?
	/// </summary>
	public bool TimerRunning { get; private set; }

	// Spawn point
	private Vector3 _spawnPosition = new(0, 2, 0);
	private Vector3 _spawnDirection = Vector3.Forward;

	#endregion

	#region Signals

	[Signal]
	public delegate void GamePausedEventHandler(bool paused);

	[Signal]
	public delegate void LevelStartedEventHandler();

	[Signal]
	public delegate void LevelCompletedEventHandler(float time);

	[Signal]
	public delegate void MarbleSpawnedEventHandler(MarbleController marble);

	#endregion

	public override void _EnterTree()
	{
		_instance = this;
	}

	public override void _ExitTree()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}

	public override void _Ready()
	{
		// Get spawn point if specified
		if (SpawnPointPath != null && !SpawnPointPath.IsEmpty)
		{
			var spawnPoint = GetNodeOrNull<Node3D>(SpawnPointPath);
			if (spawnPoint != null)
			{
				_spawnPosition = spawnPoint.GlobalPosition;
				_spawnDirection = -spawnPoint.GlobalBasis.Z;
			}
		}

		// Capture mouse for camera control
		Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;
	}

	public override void _Input(InputEvent @event)
	{
		// Handle mouse motion for camera
		if (@event is InputEventMouseMotion motion && LocalCamera != null)
		{
			LocalInput.HandleMouseMotion(motion);
		}

		// Handle pause
		if (@event.IsActionPressed("pause"))
		{
			TogglePause();
		}

		// Handle respawn
		if (@event.IsActionPressed("respawn") && LocalMarble != null)
		{
			RespawnLocalMarble();
		}

		// Toggle mouse capture with escape (when paused)
		if (@event is InputEventKey key && key.Keycode == Key.Escape && key.Pressed)
		{
			if (Godot.Input.MouseMode == Godot.Input.MouseModeEnum.Captured)
			{
				Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
			}
			else
			{
				Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (TimerRunning && !IsPaused)
		{
			LevelTime += (float)delta;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsPaused)
		{
			CurrentTick++;
		}
	}

	#region Game Control

	/// <summary>
	/// Spawns the local player's marble.
	/// </summary>
	public MarbleController SpawnLocalMarble()
	{
		if (MarbleScene == null)
		{
			GD.PrintErr("MarbleScene not set on GameManager");
			throw new System.InvalidOperationException("MarbleScene not set");
		}

		// Instantiate marble
		var marble = MarbleScene.Instantiate<MarbleController>();
		marble.GlobalPosition = _spawnPosition;
		AddChild(marble);

		// Set up input
		marble.InputProvider = LocalInput;

		// Create and set up camera
		var camera = new MarbleCamera
		{
			Target = marble,
			InputProvider = LocalInput
		};
		AddChild(camera);
		camera.SnapToTarget();
		camera.SetLookDirection(_spawnDirection);

		// Link camera to marble for camera-relative movement
		marble.CameraReference = camera;

		LocalMarble = marble;
		LocalCamera = camera;

		EmitSignal(SignalName.MarbleSpawned, marble);

		return marble;
	}

	/// <summary>
	/// Respawns the local marble at the last checkpoint or spawn point.
	/// </summary>
	public void RespawnLocalMarble()
	{
		if (LocalMarble == null) return;

		LocalMarble.Respawn(_spawnPosition, _spawnDirection);
		LocalCamera?.SnapToTarget();
		LocalCamera?.SetLookDirection(_spawnDirection);
	}

	/// <summary>
	/// Sets the spawn/checkpoint position.
	/// </summary>
	public void SetSpawnPoint(Vector3 position, Vector3 direction)
	{
		_spawnPosition = position;
		_spawnDirection = direction;
	}

	/// <summary>
	/// Starts the level timer.
	/// </summary>
	public void StartLevel()
	{
		LevelTime = 0;
		TimerRunning = true;
		EmitSignal(SignalName.LevelStarted);
	}

	/// <summary>
	/// Completes the level and stops the timer.
	/// </summary>
	public void CompleteLevel()
	{
		TimerRunning = false;
		EmitSignal(SignalName.LevelCompleted, LevelTime);
	}

	/// <summary>
	/// Toggles pause state.
	/// </summary>
	public void TogglePause()
	{
		IsPaused = !IsPaused;
		GetTree().Paused = IsPaused;

		if (IsPaused)
		{
			Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Visible;
		}
		else
		{
			Godot.Input.MouseMode = Godot.Input.MouseModeEnum.Captured;
		}

		EmitSignal(SignalName.GamePaused, IsPaused);
	}

	#endregion
}

/// <summary>
/// Game modes for network state.
/// </summary>
public enum GameMode
{
	Local,
	Server,
	Client
}
