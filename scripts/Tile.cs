using Godot;

[Tool]
public partial class Tile : Node3D
{
	[Export] public Vector2I GridCoords { get; set; } = Vector2I.Zero;

	public void Setup(Vector2I coords, float tileSize = 2f)
	{
		GridCoords = coords;
		Position = new Vector3(coords.X * tileSize, 0.01f, coords.Y * tileSize);
		
		var mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		if (mesh.MaterialOverlay != null)
		{
			mesh.MaterialOverlay = (Material)mesh.MaterialOverlay.Duplicate();
		}
	}

	// Now accepts a custom color! (Defaults to green if none is provided)
	public void SetHighlight(bool active, Color? customColor = null)
	{
		var mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		if (mesh.MaterialOverlay is not StandardMaterial3D mat)
			return;

		if (active)
		{
			Color colorToUse = customColor ?? new Color(0, 1f, 0, 0.55f);
			mat.AlbedoColor = colorToUse;
		}
		else
		{
			mat.AlbedoColor = new Color(1f, 1f, 1f, 0f);   // ← white + zero alpha (no black tint!)
		}

		mesh.MaterialOverlay = mat; // force Godot to update
	}
}
