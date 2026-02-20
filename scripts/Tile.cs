using Godot;

[Tool]
public partial class Tile : Node3D
{
	[Export] public Vector2I GridCoords { get; set; } = Vector2I.Zero;

	public Unit OccupyingUnit { get; set; } = null;

	public void Setup(Vector2I coords, float tileSize = 2f)
	{
		GridCoords = coords;
		
		// X stays X, but grid "Z" is stored in Vector2I.Y
		Position = new Vector3(
			coords.X * tileSize, 
			0.01f, 
			coords.Y * tileSize   // ← this was coords.Z → now coords.Y
		);
	}

	// Optional hover highlight (you can call this later)
	public void SetHighlight(bool active)
	{
		var mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		if (mesh.MaterialOverlay is StandardMaterial3D mat)
		{
			mat.AlbedoColor = active ? new Color(0, 1, 0, 0.4f) : new Color(0, 0, 0, 0);
		}
	}
}
