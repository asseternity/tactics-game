// Tile.cs
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
		
		// Clean up any old broken overlays from the previous attempts!
		mesh.MaterialOverlay = null; 
	}

	public void SetHighlight(bool active, Color? customColor = null)
	{
		var mesh = GetNode<MeshInstance3D>("MeshInstance3D");
		
		// === THE FIX: Emission Glow! ===
		// We grab the unique dirt/grass material that GameManager just assigned to this tile
		if (mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
		{
			if (active)
			{
				Color glowColor = customColor ?? new Color(0, 1f, 0, 0.55f);
				
				mat.EmissionEnabled = true;
				
				// Strip the alpha out to use as a pure, solid light color
				mat.Emission = new Color(glowColor.R, glowColor.G, glowColor.B);
				
				// JUICE: We use the alpha value of the color to determine how BRIGHT it glows!
				// The soft blue movement tiles will emit a soft light, while the green hover will glow brightly!
				mat.EmissionEnergyMultiplier = glowColor.A * 0.35f; 
			}
			else
			{
				// Turn off the light!
				mat.EmissionEnabled = false;
			}
		}
	}
}
