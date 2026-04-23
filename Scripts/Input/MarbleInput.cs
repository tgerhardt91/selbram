using Godot;

namespace Selbram.Input;

/// <summary>
/// Represents a single frame of marble input.
/// This struct is designed to be serializable for network transmission.
/// </summary>
public struct MarbleInput
{
	/// <summary>
	/// Movement direction (normalized). X = left/right, Y = forward/backward.
	/// </summary>
	public Vector2 Movement;

	/// <summary>
	/// Whether the jump button was pressed this frame.
	/// </summary>
	public bool JumpPressed;

	/// <summary>
	/// Whether the jump button is currently held.
	/// </summary>
	public bool JumpHeld;

	/// <summary>
	/// Whether the power-up button was pressed this frame.
	/// </summary>
	public bool UsePowerUp;

	/// <summary>
	/// Camera rotation input. X = yaw, Y = pitch.
	/// </summary>
	public Vector2 CameraRotation;

	/// <summary>
	/// The simulation tick this input corresponds to (for networking).
	/// </summary>
	public uint Tick;

	/// <summary>
	/// Creates an empty input with no buttons pressed.
	/// </summary>
	public static MarbleInput Empty => new()
	{
		Movement = Vector2.Zero,
		JumpPressed = false,
		JumpHeld = false,
		UsePowerUp = false,
		CameraRotation = Vector2.Zero,
		Tick = 0
	};

	/// <summary>
	/// Returns true if there is any movement input.
	/// </summary>
	public readonly bool HasMovement => Movement.LengthSquared() > 0.01f;

	/// <summary>
	/// Serializes this input to a byte array for network transmission.
	/// </summary>
	public readonly byte[] Serialize()
	{
		// Pack booleans into a single byte
		byte flags = 0;
		if (JumpPressed) flags |= 1;
		if (JumpHeld) flags |= 2;
		if (UsePowerUp) flags |= 4;

		using var stream = new System.IO.MemoryStream();
		using var writer = new System.IO.BinaryWriter(stream);

		writer.Write(Movement.X);
		writer.Write(Movement.Y);
		writer.Write(CameraRotation.X);
		writer.Write(CameraRotation.Y);
		writer.Write(flags);
		writer.Write(Tick);

		return stream.ToArray();
	}

	/// <summary>
	/// Deserializes input from a byte array.
	/// </summary>
	public static MarbleInput Deserialize(byte[] data)
	{
		using var stream = new System.IO.MemoryStream(data);
		using var reader = new System.IO.BinaryReader(stream);

		var input = new MarbleInput
		{
			Movement = new Vector2(reader.ReadSingle(), reader.ReadSingle()),
			CameraRotation = new Vector2(reader.ReadSingle(), reader.ReadSingle())
		};

		byte flags = reader.ReadByte();
		input.JumpPressed = (flags & 1) != 0;
		input.JumpHeld = (flags & 2) != 0;
		input.UsePowerUp = (flags & 4) != 0;
		input.Tick = reader.ReadUInt32();

		return input;
	}
}
