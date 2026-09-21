using Godot;

/// <summary>
/// The script attached to <c>Scn/BoxRot.tscn</c>.
/// </summary>
/// <remarks>
/// Rotates continuously so the script's execution is observable on screen, and logs on ready so the e2e
/// probe can assert that a packed scene's script actually ran, without a human watching the viewport.
/// </remarks>
public partial class BoxRot : Node3D
{
    public override void _Ready()
    {
        GD.Print("BOXROT_READY");
    }

    public override void _Process(double delta)
    {
        this.RotateY((float)delta);
    }
}
