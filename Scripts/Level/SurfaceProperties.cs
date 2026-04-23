using Godot;

namespace Selbram.Level;

/// <summary>
/// Resource defining how a surface affects marble physics.
/// Attach to physics materials or use for collision layer detection.
/// </summary>
[GlobalClass]
public partial class SurfaceProperties : Resource
{
    public enum SurfaceType
    {
        Normal,
        Ice,
        Mud,
        Bumper,
        SpeedPad
    }

    /// <summary>
    /// Type of surface for special handling.
    /// </summary>
    [Export]
    public SurfaceType Type { get; set; } = SurfaceType.Normal;

    /// <summary>
    /// Friction multiplier. Lower = more slippery. Default = 1.0.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.1")]
    public float Friction { get; set; } = 1.0f;

    /// <summary>
    /// Bounciness multiplier. Higher = more bouncy. Default = 0.3.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.1")]
    public float Bounce { get; set; } = 0.3f;

    /// <summary>
    /// Speed multiplier. Applied to max speed when on this surface.
    /// </summary>
    [Export(PropertyHint.Range, "0.5,3.0,0.1")]
    public float SpeedModifier { get; set; } = 1.0f;

    /// <summary>
    /// Roll resistance. Higher values slow the marble more.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,2.0,0.1")]
    public float RollResistance { get; set; } = 1.0f;

    /// <summary>
    /// Default surface properties.
    /// </summary>
    public static SurfaceProperties Default => new()
    {
        Type = SurfaceType.Normal,
        Friction = 1.0f,
        Bounce = 0.3f,
        SpeedModifier = 1.0f,
        RollResistance = 1.0f
    };

    /// <summary>
    /// Ice surface preset.
    /// </summary>
    public static SurfaceProperties Ice => new()
    {
        Type = SurfaceType.Ice,
        Friction = 0.1f,
        Bounce = 0.1f,
        SpeedModifier = 1.2f,
        RollResistance = 0.2f
    };

    /// <summary>
    /// Mud surface preset.
    /// </summary>
    public static SurfaceProperties Mud => new()
    {
        Type = SurfaceType.Mud,
        Friction = 2.0f,
        Bounce = 0.1f,
        SpeedModifier = 0.6f,
        RollResistance = 2.0f
    };

    /// <summary>
    /// Bumper surface preset.
    /// </summary>
    public static SurfaceProperties Bumper => new()
    {
        Type = SurfaceType.Bumper,
        Friction = 1.0f,
        Bounce = 1.5f,
        SpeedModifier = 1.0f,
        RollResistance = 1.0f
    };
}
