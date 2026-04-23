using Godot;

namespace Selbram.Input;

/// <summary>
/// Provides input from local gamepad/keyboard.
/// This is the primary input provider for single-player and the local player in multiplayer.
/// </summary>
public class LocalInputProvider : IInputProvider
{
    private bool _jumpWasPressedLastFrame;
    private bool _powerUpWasPressedLastFrame;

    /// <summary>
    /// Mouse sensitivity for camera rotation (used when mouse is captured).
    /// </summary>
    public float MouseSensitivity { get; set; } = 0.002f;

    /// <summary>
    /// Stick sensitivity for camera rotation.
    /// </summary>
    public float StickSensitivity { get; set; } = 3.0f;

    /// <summary>
    /// Accumulated mouse motion since last poll.
    /// </summary>
    private Vector2 _mouseMotion;

    public bool IsActive => true;

    /// <summary>
    /// Call this from _Input to accumulate mouse motion.
    /// </summary>
    public void HandleMouseMotion(InputEventMouseMotion motion)
    {
        _mouseMotion += motion.Relative;
    }

    public MarbleInput GetInput(uint tick)
    {
        var input = new MarbleInput { Tick = tick };

        // Movement from left stick or WASD
        var moveInput = Godot.Input.GetVector(
            "move_left", "move_right",
            "move_forward", "move_backward"
        );
        input.Movement = moveInput;

        // Jump
        bool jumpPressed = Godot.Input.IsActionPressed("jump");
        input.JumpHeld = jumpPressed;
        input.JumpPressed = jumpPressed && !_jumpWasPressedLastFrame;
        _jumpWasPressedLastFrame = jumpPressed;

        // Power-up
        bool powerUpPressed = Godot.Input.IsActionPressed("use_powerup");
        input.UsePowerUp = powerUpPressed && !_powerUpWasPressedLastFrame;
        _powerUpWasPressedLastFrame = powerUpPressed;

        // Camera rotation from right stick
        var cameraInput = new Vector2(
            Godot.Input.GetAxis("camera_left", "camera_right"),
            Godot.Input.GetAxis("camera_up", "camera_down")
        ) * StickSensitivity;

        // Add mouse motion if any
        if (_mouseMotion.LengthSquared() > 0)
        {
            cameraInput += _mouseMotion * MouseSensitivity * 60f; // Normalize to ~60fps equivalent
            _mouseMotion = Vector2.Zero;
        }

        input.CameraRotation = cameraInput;

        return input;
    }

    public void Reset()
    {
        _jumpWasPressedLastFrame = false;
        _powerUpWasPressedLastFrame = false;
        _mouseMotion = Vector2.Zero;
    }
}
