using Godot;

namespace Selbram.Player;

/// <summary>
/// Represents the complete state of a marble at a point in time.
/// Designed to be serializable for networking and replay systems.
/// </summary>
public struct MarbleState
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 AngularVelocity;
    public Quaternion Rotation;
    public bool IsGrounded;
    public Vector3 GroundNormal;
    public PowerUps.PowerUpType ActivePowerUp;
    public float PowerUpTimer;
    public uint Tick;

    public static MarbleState Default => new()
    {
        Position = Vector3.Zero,
        Velocity = Vector3.Zero,
        AngularVelocity = Vector3.Zero,
        Rotation = Quaternion.Identity,
        IsGrounded = false,
        GroundNormal = Vector3.Up,
        ActivePowerUp = PowerUps.PowerUpType.None,
        PowerUpTimer = 0,
        Tick = 0
    };

    /// <summary>
    /// Interpolates between two states for smooth rendering.
    /// </summary>
    public static MarbleState Lerp(MarbleState a, MarbleState b, float t)
    {
        return new MarbleState
        {
            Position = a.Position.Lerp(b.Position, t),
            Velocity = a.Velocity.Lerp(b.Velocity, t),
            AngularVelocity = a.AngularVelocity.Lerp(b.AngularVelocity, t),
            Rotation = a.Rotation.Slerp(b.Rotation, t),
            IsGrounded = t < 0.5f ? a.IsGrounded : b.IsGrounded,
            GroundNormal = a.GroundNormal.Lerp(b.GroundNormal, t).Normalized(),
            ActivePowerUp = t < 0.5f ? a.ActivePowerUp : b.ActivePowerUp,
            PowerUpTimer = Mathf.Lerp(a.PowerUpTimer, b.PowerUpTimer, t),
            Tick = t < 0.5f ? a.Tick : b.Tick
        };
    }

    /// <summary>
    /// Serializes the state to bytes for network transmission.
    /// </summary>
    public readonly byte[] Serialize()
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);

        writer.Write(Position.X);
        writer.Write(Position.Y);
        writer.Write(Position.Z);
        writer.Write(Velocity.X);
        writer.Write(Velocity.Y);
        writer.Write(Velocity.Z);
        writer.Write(AngularVelocity.X);
        writer.Write(AngularVelocity.Y);
        writer.Write(AngularVelocity.Z);
        writer.Write(Rotation.X);
        writer.Write(Rotation.Y);
        writer.Write(Rotation.Z);
        writer.Write(Rotation.W);
        writer.Write(IsGrounded);
        writer.Write(GroundNormal.X);
        writer.Write(GroundNormal.Y);
        writer.Write(GroundNormal.Z);
        writer.Write((byte)ActivePowerUp);
        writer.Write(PowerUpTimer);
        writer.Write(Tick);

        return stream.ToArray();
    }

    /// <summary>
    /// Deserializes a state from bytes.
    /// </summary>
    public static MarbleState Deserialize(byte[] data)
    {
        using var stream = new System.IO.MemoryStream(data);
        using var reader = new System.IO.BinaryReader(stream);

        return new MarbleState
        {
            Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            Velocity = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            AngularVelocity = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            Rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            IsGrounded = reader.ReadBoolean(),
            GroundNormal = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
            ActivePowerUp = (PowerUps.PowerUpType)reader.ReadByte(),
            PowerUpTimer = reader.ReadSingle(),
            Tick = reader.ReadUInt32()
        };
    }
}
