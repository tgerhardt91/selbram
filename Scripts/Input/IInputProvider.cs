namespace Selbram.Input;

/// <summary>
/// Interface for input providers. This abstraction allows swapping between
/// local input, network input, and replay input without changing game logic.
/// </summary>
public interface IInputProvider
{
    /// <summary>
    /// Polls and returns the current input state.
    /// Should be called once per physics frame.
    /// </summary>
    /// <param name="tick">The current simulation tick.</param>
    /// <returns>The current input state.</returns>
    MarbleInput GetInput(uint tick);

    /// <summary>
    /// Returns true if this provider is currently active and can provide input.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Called when the input provider should reset its state (e.g., on respawn).
    /// </summary>
    void Reset();
}
