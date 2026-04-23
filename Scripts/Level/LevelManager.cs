using Godot;
using Selbram.Core;
using Selbram.Player;

namespace Selbram.Level;

/// <summary>
/// Manages level-specific logic like triggers, checkpoints, and completion.
/// </summary>
public partial class LevelManager : Node
{
    [Export]
    public NodePath? FinishAreaPath { get; set; }

    [Export]
    public NodePath? KillZonePath { get; set; }

    private Area3D? _finishArea;
    private Area3D? _killZone;

    public override void _Ready()
    {
        // Connect to GameManager signals
        if (GameManager.IsInitialized)
        {
            GameManager.Instance.MarbleSpawned += OnMarbleSpawned;
        }

        // Get finish area
        if (FinishAreaPath != null && !FinishAreaPath.IsEmpty)
        {
            _finishArea = GetNodeOrNull<Area3D>(FinishAreaPath);
            if (_finishArea != null)
            {
                _finishArea.BodyEntered += OnFinishAreaEntered;
            }
        }

        // Get kill zone
        if (KillZonePath != null && !KillZonePath.IsEmpty)
        {
            _killZone = GetNodeOrNull<Area3D>(KillZonePath);
            if (_killZone != null)
            {
                _killZone.BodyEntered += OnKillZoneEntered;
            }
        }

        // Auto-spawn marble and start level after a short delay
        CallDeferred(nameof(StartLevel));
    }

    private void StartLevel()
    {
        if (GameManager.IsInitialized)
        {
            GameManager.Instance.SpawnLocalMarble();
            GameManager.Instance.StartLevel();
        }
    }

    private void OnMarbleSpawned(MarbleController marble)
    {
        GD.Print("Marble spawned at: ", marble.GlobalPosition);
    }

    private void OnFinishAreaEntered(Node3D body)
    {
        if (body is MarbleController marble && marble == GameManager.Instance.LocalMarble)
        {
            GameManager.Instance.CompleteLevel();
            GD.Print($"Level completed in {GameManager.Instance.LevelTime:F2} seconds!");
        }
    }

    private void OnKillZoneEntered(Node3D body)
    {
        if (body is MarbleController marble && marble == GameManager.Instance.LocalMarble)
        {
            GameManager.Instance.RespawnLocalMarble();
        }
    }
}
