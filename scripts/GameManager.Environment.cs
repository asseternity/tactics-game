// GameManager.Environment.cs — 30x30 grid with all systems scaled
// FIX v3: Marble roughness was 0.15 (mirror) reflecting empty dark blue sky = invisible tiles.
//         Raised to 0.65 so albedo/normal/elevation are all visible.
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	private Godot.Collections.Dictionary<Vector2I, Tile> _grid = new();
	private List<Node3D> _obstacles = new();
	private List<Node3D> _activeDioramas = new();
	private List<OmniLight3D> _nightLights = new();
	private MeshInstance3D _backgroundPlane;
	private Tile _hoveredTile;

	private const float TileSize = 2f;
	private const int GridWidth = 30;
	private const int GridDepth = 30;

	private void GenerateGrid()
	{
		Tween spawnTween = CreateTween().SetParallel(true);
		for (int x = 0; x < GridWidth; x++)
		{
			for (int z = 0; z < GridDepth; z++)
			{
				Tile tile = TileScene.Instantiate<Tile>();
				AddChild(tile);
				tile.Setup(new Vector2I(x, z), TileSize);
				tile.Scale = new Vector3(0.001f, 0.001f, 0.001f);
				Vector3 targetScale = new Vector3(0.96f, 1.0f, 0.96f);
				float delay = (x + z) * 0.008f;
				spawnTween.TweenProperty(tile, "scale", targetScale, 0.5f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(delay);
				_grid[new Vector2I(x, z)] = tile;
			}
		}
	}

	private Vector2I GetGridPos(Vector3 pos) => new Vector2I(Mathf.RoundToInt(pos.X / TileSize), Mathf.RoundToInt(pos.Z / TileSize));

	private bool IsTileFree(Vector2I gridPos)
	{
		foreach (var u in _units) if (IsInstanceValid(u) && GetGridPos(u.GlobalPosition) == gridPos) return false;
		foreach (var obs in _obstacles) if (IsInstanceValid(obs) && GetGridPos(obs.GlobalPosition) == gridPos) return false;
		return true;
	}

	private void ApplyEnvironment(BattleSetup data)
	{
		if (MainLight != null && WorldEnv != null)
		{
			if (WorldEnv.Environment == null) WorldEnv.Environment = new Godot.Environment();
			Godot.Environment env = WorldEnv.Environment;
			if (Cam != null) Cam.Environment = env;

			env.BackgroundMode = Godot.Environment.BGMode.Color;
			env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
			env.TonemapMode = Godot.Environment.ToneMapper.Aces;
			env.TonemapExposure = 1.05f;
			env.AdjustmentEnabled = true; env.AdjustmentContrast = 1.05f; env.AdjustmentSaturation = 1.1f;
			env.GlowEnabled = true; env.GlowHdrThreshold = 0.9f; env.GlowIntensity = 0.8f; env.GlowStrength = 0.9f; env.GlowBloom = 0.0f; env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive;
			env.SsaoEnabled = true; env.SsaoRadius = 1.0f; env.SsaoIntensity = 1.5f; env.SsilEnabled = true;
			env.FogEnabled = false;
			env.VolumetricFogEnabled = true;
			env.VolumetricFogDensity = 0.001f;

			switch (data.Light)
			{
				case LightingMood.Noon:
					MainLight.Visible = true; MainLight.LightColor = new Color(1f, 0.98f, 0.95f); MainLight.LightEnergy = 1.2f; MainLight.RotationDegrees = new Vector3(-75, 45, 0);
					env.BackgroundColor = new Color(0.4f, 0.6f, 0.9f); env.AmbientLightColor = new Color(0.6f, 0.8f, 1f); env.AmbientLightEnergy = 0.5f;
					env.VolumetricFogAlbedo = new Color(0.6f, 0.75f, 1.0f); env.VolumetricFogDensity = 0.001f;
					break;
				case LightingMood.Morning:
					MainLight.Visible = true; MainLight.LightColor = new Color(1f, 0.85f, 0.65f); MainLight.LightEnergy = 1.2f; MainLight.RotationDegrees = new Vector3(-25, 60, 0);
					env.BackgroundColor = new Color(0.8f, 0.5f, 0.4f); env.AmbientLightColor = new Color(0.9f, 0.6f, 0.7f); env.AmbientLightEnergy = 0.4f;
					env.VolumetricFogAlbedo = new Color(0.9f, 0.6f, 0.5f); env.VolumetricFogDensity = 0.001f;
					break;
				case LightingMood.Night:
					MainLight.Visible = true; MainLight.LightColor = new Color(0.5f, 0.6f, 0.9f); MainLight.LightEnergy = 1.0f; MainLight.RotationDegrees = new Vector3(-60, -30, 0);
					env.BackgroundColor = new Color(0.05f, 0.05f, 0.1f); env.AmbientLightColor = new Color(0.25f, 0.35f, 0.55f); env.AmbientLightEnergy = 1.0f;
					env.VolumetricFogAlbedo = new Color(0.1f, 0.1f, 0.2f); env.VolumetricFogDensity = 0.0008f;
					SpawnNightCornerLights();
					break;
				case LightingMood.Indoors:
					MainLight.Visible = false;
					env.BackgroundColor = new Color(0.02f, 0.02f, 0.02f); env.AmbientLightColor = new Color(0.8f, 0.7f, 0.5f); env.AmbientLightEnergy = 0.8f;
					env.VolumetricFogAlbedo = new Color(0.05f, 0.04f, 0.03f); env.VolumetricFogDensity = 0.02f;
					break;
			}
		}

		StandardMaterial3D groundMaterial = new StandardMaterial3D();
		
		// === THE FIX 4: Adding visual texture ===
		// Cranked up frequency and bump strength so the light catches a bumpy texture
		FastNoiseLite noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.08f }; 
		NoiseTexture2D normalTex = new NoiseTexture2D { Noise = noise, AsNormalMap = true, BumpStrength = 8.0f };
		groundMaterial.NormalEnabled = true; 
		groundMaterial.NormalTexture = normalTex;

		switch (data.Ground)
		{
			case GroundType.Grass: groundMaterial.AlbedoColor = new Color(0.2f, 0.35f, 0.15f); groundMaterial.Roughness = 0.95f; break;
			case GroundType.Dirt: groundMaterial.AlbedoColor = new Color(0.3f, 0.22f, 0.15f); groundMaterial.Roughness = 1.0f; break;
			case GroundType.Marble: groundMaterial.AlbedoColor = new Color(0.85f, 0.85f, 0.9f); groundMaterial.Roughness = 0.65f; break;
		}
		
		if (WorldEnv?.Environment != null) WorldEnv.Environment.BackgroundColor = groundMaterial.AlbedoColor;

		foreach (var tile in _grid.Values)
		{
			tile.Visible = true;
			if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh)
			{
				mesh.Visible = true;
				mesh.MaterialOverride = null;
				mesh.SetSurfaceOverrideMaterial(0, (StandardMaterial3D)groundMaterial.Duplicate());
			}
		}

		float gridWorldW = GridWidth * TileSize;
		float gridWorldD = GridDepth * TileSize;
		float planeSize = 2000f;

		if (_backgroundPlane == null)
		{
			_backgroundPlane = new MeshInstance3D();
			PlaneMesh plane = new PlaneMesh { Size = new Vector2(planeSize, planeSize) };
			_backgroundPlane.Mesh = plane;
			AddChild(_backgroundPlane);
		}
		_backgroundPlane.Position = new Vector3(gridWorldW / 2f, -0.15f, gridWorldD / 2f);

		// === THE FIX 1: Blending the ground perfectly ===
		StandardMaterial3D planeMat = (StandardMaterial3D)groundMaterial.Duplicate();
		planeMat.Uv1Scale = new Vector3(planeSize / 2f, planeSize / 2f, 1);
		_backgroundPlane.MaterialOverride = planeMat;
	}

	public async Task BuildEnvironmentSceneryAsync(BattleSetup data)
	{
		System.Random rnd = new();
		// Lowered frequency to 0.04f for smooth, rolling, natural hills
		FastNoiseLite noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = rnd.Next(), Frequency = 0.04f };

		Tween envTween = CreateTween().SetParallel(true);
		bool itemsAnimating = false;

		// =========================================================
		// 1. ELEVATE PLAYABLE GRID
		// =========================================================
		foreach (var kvp in _grid)
		{
			Vector2I pos = kvp.Key; Tile tile = kvp.Value;
			float targetHeight = 0.01f;
			if (data.ElevationEnabled)
			{
				int distToEdge = Mathf.Min(Mathf.Min(pos.X, GridWidth - 1 - pos.X), Mathf.Min(pos.Y, GridDepth - 1 - pos.Y));
				if (distToEdge > 1)
				{
					float rawNoise = (noise.GetNoise2D(pos.X, pos.Y) + 1f) / 2f;
					// Smooth gradient over 10 rings using smoothstep instead of binary 0.4/1.0
					float transitionRings = 10f;
					float t = Mathf.Clamp((distToEdge - 1f) / transitionRings, 0f, 1f);
					float falloff = t * t * (3f - 2f * t); // smoothstep curve
					targetHeight = 0.01f + (rawNoise * 3.8f * falloff);
				}
			}
			
			if (data.ElevationEnabled && targetHeight > 0.02f)
			{
				envTween.TweenProperty(tile, "scale", new Vector3(1.01f, 1.0f, 1.01f), 0.5f)
					.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			}

			if (Mathf.Abs(tile.Position.Y - targetHeight) > 0.001f)
			{
				itemsAnimating = true;
				float delay = (float)rnd.NextDouble() * 0.15f;
				envTween.TweenProperty(tile, "position:y", targetHeight, 0.5f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out).SetDelay(delay);

				if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh && mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
				{
					float hr = Mathf.Clamp(targetHeight / 3.8f, 0f, 1f); // Adjusted ratio match
					Color bc = mat.AlbedoColor;
					Color finalC = bc.Lerp(new Color(bc.R * 1.4f, bc.G * 1.4f, bc.B * 1.2f), hr);
					envTween.TweenProperty(mat, "albedo_color", finalC, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(delay);
				}
			}
		}

		// =========================================================
		// 2. TRANSLUCENT GREEN BOUNDARY WALLS
		// =========================================================
		float thickness = 0.4f;
		float wallHeight = 1.2f;
		float cx = (GridWidth * TileSize) / 2f - (TileSize / 2f); // Center X = 29
		float cz = (GridDepth * TileSize) / 2f - (TileSize / 2f); // Center Z = 29
		float wallSpanX = GridWidth * TileSize; // 60
		float wallSpanZ = GridDepth * TileSize; // 60

		// Define the 4 perfectly tight borders around the grid
		(Vector3 pos, Vector3 size)[] walls = {
			(new Vector3(cx, 0, -TileSize/2f), new Vector3(wallSpanX, wallHeight, thickness)), // North
			(new Vector3(cx, 0, wallSpanZ - TileSize/2f), new Vector3(wallSpanX, wallHeight, thickness)), // South
			(new Vector3(-TileSize/2f, 0, cz), new Vector3(thickness, wallHeight, wallSpanZ)), // West
			(new Vector3(wallSpanX - TileSize/2f, 0, cz), new Vector3(thickness, wallHeight, wallSpanZ)) // East
		};

		StandardMaterial3D wallMat = new StandardMaterial3D {
			AlbedoColor = new Color(0.0f, 0.0f, 0.0f, 0.0f), // Invisible base, pure light
			EmissionEnabled = true, Emission = new Color(0.1f, 1.0f, 0.4f), EmissionEnergyMultiplier = 0.5f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha, 
			BlendMode = BaseMaterial3D.BlendModeEnum.Add, // Hologram/Ghost glow effect
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			ProximityFadeEnabled = true, ProximityFadeDistance = 1.5f // Softens intersections with ground/houses
		};

		// GAME JUICE: Soft Misty Pulse
		Tween shimmerTween = CreateTween().SetLoops();
		shimmerTween.TweenProperty(wallMat, "emission_energy_multiplier", 0.2f, 1.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		shimmerTween.TweenProperty(wallMat, "emission_energy_multiplier", 0.9f, 1.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		foreach (var w in walls)
		{
			// Pivot node at the bottom to make the wall rise up out of the ground
			Node3D pivot = new Node3D { Position = w.pos };
			MeshInstance3D mesh = new MeshInstance3D { Mesh = new BoxMesh { Size = w.size }, MaterialOverride = wallMat };
			mesh.Position = new Vector3(0, wallHeight / 2f, 0); // Shift mesh up so pivot is at the bottom
			
			pivot.AddChild(mesh);
			AddChild(pivot);
			_activeDioramas.Add(pivot); // Added to this list so ClearEnvironmentSceneryAsync cleans it up automatically!

			pivot.Scale = new Vector3(1, 0.001f, 1);
			envTween.TweenProperty(pivot, "scale:y", 1f, 0.8f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out).SetDelay((float)rnd.NextDouble() * 0.3f);
			itemsAnimating = true;
		}

		// =========================================================
		// 3. SPAWN PERIPHERAL SCENERY (Dioramas + 3D Rolling Hills)
		// =========================================================
		float blockSize = 6.0f;
		float safeMin = -4f;
		float safeMax = (GridWidth * TileSize) + 4f; 
		float dioramaMin = -18f;
		float dioramaMax = safeMax + 12f; 
		float outerMin = -80f;
		float outerMax = dioramaMax + 60f;

		Texture2D rockTex = GD.Load<Texture2D>("res://assets/rock.png");
		Texture2D treeTex = GD.Load<Texture2D>("res://assets/tree.png");

		for (float x = outerMin; x <= outerMax; x += blockSize)
		{
			for (float z = outerMin; z <= outerMax; z += blockSize)
			{
				if (x > safeMin && x < safeMax && z > safeMin && z < safeMax) continue; 
				if ((x == dioramaMin && z == dioramaMin) || (x == dioramaMax && z == dioramaMax) ||
					(x == dioramaMin && z == dioramaMax) || (x == dioramaMax && z == dioramaMin)) continue;

				Node3D blockParent = new Node3D { Position = new Vector3(x, 0, z) };
				AddChild(blockParent);
				_activeDioramas.Add(blockParent); 

				// --- ZONE A: DIORAMA BUILDINGS ---
				if (x >= dioramaMin && x <= dioramaMax && z >= dioramaMin && z <= dioramaMax)
				{
					if (BackgroundDioramas != null && BackgroundDioramas.Count > 0)
					{
						Node3D prop = InstantiateAndScaleRandomModel(rnd, blockSize * 0.85f, out _);
						prop.Position = Vector3.Zero;
						prop.RotationDegrees = new Vector3(0, rnd.Next(0, 4) * 90, 0);
						float scaleVar = (float)rnd.NextDouble() * 0.35f + 0.7f;
						prop.Scale *= scaleVar;
						blockParent.AddChild(prop);
					}
				}
				// --- ZONE B: OUTER WILDS (3D CUBE HILLS) ---
				else
				{
					Texture2D gridNormalTex = null;
					if (_grid.Count > 0 && _grid.Values.First().GetNodeOrNull<MeshInstance3D>("MeshInstance3D")?.GetSurfaceOverrideMaterial(0) is StandardMaterial3D m) {
						gridNormalTex = m.NormalTexture;
					}

					for (float subX = 0; subX < blockSize; subX += TileSize)
					{
						for (float subZ = 0; subZ < blockSize; subZ += TileSize)
						{
							float worldX = x + subX;
							float worldZ = z + subZ;

							float distFromCenter = Mathf.Max(Mathf.Abs(worldX - 29f), Mathf.Abs(worldZ - 29f));
							
							float normalizedDist = Mathf.Clamp((distFromCenter - 31f) / 60f, 0f, 1f);
							float slopeMultiplier = normalizedDist * normalizedDist;
							float rawWorldNoise = (noise.GetNoise2D(worldX, worldZ) + 1f) / 2f;
							float targetH = rawWorldNoise * 10.0f * slopeMultiplier;

							MeshInstance3D dirtCube = new MeshInstance3D();
							float totalCubeHeight = targetH + 12f; 
							
							// 3. Expand the cube size by 0.1f to force them to overlap and seal all micro-gaps
							dirtCube.Mesh = new BoxMesh { Size = new Vector3(TileSize + 0.1f, totalCubeHeight, TileSize + 0.1f) };

							// 4. Disable SpecularMode to permanently kill the sharp white shimmering edge lighting
							StandardMaterial3D patchMat = new StandardMaterial3D { 
								Roughness = 1.0f, 
								SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled 
							};
							if (gridNormalTex != null) {
								patchMat.NormalEnabled = true;
								patchMat.NormalTexture = gridNormalTex;
							}

							Color baseDirt = new Color(0.3f, 0.22f, 0.15f); 
							patchMat.AlbedoColor = baseDirt.Lerp(new Color(0.15f, 0.1f, 0.05f), rawWorldNoise); 
							dirtCube.MaterialOverride = patchMat;

							dirtCube.Position = new Vector3(subX, targetH - (totalCubeHeight / 2f), subZ);
							blockParent.AddChild(dirtCube);

							// FIX 2: Restored the Obstacles! (40% spawn chance per 2x2 tile)
							if (rnd.NextDouble() > 0.6)
							{
								bool isTree = rnd.NextDouble() > 0.4;
								Texture2D tex = isTree ? treeTex : rockTex;
								float obsHeight = isTree ? 2.5f : 1.2f;

								Sprite3D sprite = new Sprite3D { Texture = tex, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass };
								float sf = obsHeight / (tex.GetHeight() * sprite.PixelSize);
								sprite.Scale = new Vector3(sf, sf, sf) * ((float)rnd.NextDouble() * 0.4f + 0.8f);
								
								// Place perfectly on top of the newly sized cubes
								sprite.Position = new Vector3(subX, targetH + (obsHeight / 2f), subZ);
								blockParent.AddChild(sprite);
							}
						}
					}
				}

				// GAME JUICE: Entire world bounces out of the ground at random intervals
				blockParent.Scale = Vector3.Zero;
				envTween.TweenProperty(blockParent, "scale", Vector3.One, 0.7f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
					.SetDelay((float)rnd.NextDouble() * 0.7f);
				itemsAnimating = true;
			}
		}

		if (itemsAnimating) await ToSignal(GetTree().CreateTimer(0.9f), "timeout");
	}

	public async Task ClearEnvironmentSceneryAsync()
	{
		DeselectUnit();

		Tween clearTween = CreateTween().SetParallel(true);
		bool itemsAnimating = false;

		// Shrink Grid Units
		foreach (var u in _units.Where(IsInstanceValid)) { itemsAnimating = true; clearTween.TweenProperty(u, "scale", Vector3.Zero, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In).SetDelay((float)GD.RandRange(0f, 0.2f)); }
		// Shrink Grid Obstacles
		foreach (var o in _obstacles.Where(IsInstanceValid)) { itemsAnimating = true; clearTween.TweenProperty(o, "scale", Vector3.Zero, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In).SetDelay((float)GD.RandRange(0f, 0.2f)); }
		// Shrink Dioramas & Outer Scenery ALL AT ONCE
		foreach (var d in _activeDioramas.Where(IsInstanceValid)) { itemsAnimating = true; clearTween.TweenProperty(d, "scale", Vector3.Zero, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In).SetDelay((float)GD.RandRange(0f, 0.3f)); }
		// Fade Lights
		foreach (var l in _nightLights.Where(IsInstanceValid)) { itemsAnimating = true; clearTween.TweenProperty(l, "light_energy", 0f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In); }

		// Flatten and recolor Grid
		foreach (var tile in _grid.Values)
		{
			if (tile.Position.Y > 0.02f)
			{
				itemsAnimating = true;
				float d = (float)GD.RandRange(0f, 0.15f);
				clearTween.TweenProperty(tile, "position:y", 0.01f, 0.4f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out).SetDelay(d);
				if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh && mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
				{
					Color dc = new Color(mat.AlbedoColor.R * 0.7f, mat.AlbedoColor.G * 0.7f, mat.AlbedoColor.B * 0.8f, mat.AlbedoColor.A);
					clearTween.TweenProperty(mat, "albedo_color", dc, 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In).SetDelay(d);
				}
			}
		}

		if (itemsAnimating) await ToSignal(GetTree().CreateTimer(0.7f), "timeout");
		else clearTween.Kill();

		// Memory Cleanup
		foreach (var u in _units.Where(IsInstanceValid)) u.QueueFree(); _units.Clear();
		foreach (var o in _obstacles.Where(IsInstanceValid)) o.QueueFree(); _obstacles.Clear();
		foreach (var d in _activeDioramas.Where(IsInstanceValid)) d.QueueFree(); _activeDioramas.Clear();
		foreach (var l in _nightLights.Where(IsInstanceValid)) l.QueueFree(); _nightLights.Clear();
	}

	private Node3D InstantiateAndScaleRandomModel(System.Random rnd, float targetSize, out float finalH)
	{
		PackedScene scene = BackgroundDioramas[rnd.Next(BackgroundDioramas.Count)];
		Node3D model = scene.Instantiate<Node3D>();
		Aabb bounds = new(); bool first = true;
		foreach (Node child in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (child is MeshInstance3D mi)
			{
				Aabb ab = mi.Transform * mi.GetAabb();
				if (first) { bounds = ab; first = false; } else bounds = bounds.Merge(ab);
			}
		}
		float maxBase = Mathf.Max(bounds.Size.X, bounds.Size.Z);
		if (maxBase == 0) maxBase = 1f;
		float sf = targetSize / maxBase;
		model.Scale = new Vector3(sf, sf, sf);
		finalH = bounds.Size.Y * sf;
		return model;
	}

	// Night lights — grid pattern covering the full 60x60 battlefield
	private void SpawnNightCornerLights()
	{
		float maxX = (GridWidth - 1) * TileSize;
		float maxZ = (GridDepth - 1) * TileSize;

		Vector3[] positions = {
			new(0, 10f, 0), new(maxX, 10f, 0), new(0, 10f, maxZ), new(maxX, 10f, maxZ),
			new(maxX / 2, 10f, 0), new(maxX / 2, 10f, maxZ),
			new(0, 10f, maxZ / 2), new(maxX, 10f, maxZ / 2),
			new(maxX / 2, 12f, maxZ / 2),
			new(maxX / 4, 10f, maxZ / 4), new(maxX * 3 / 4, 10f, maxZ / 4),
			new(maxX / 4, 10f, maxZ * 3 / 4), new(maxX * 3 / 4, 10f, maxZ * 3 / 4),
		};

		foreach (var pos in positions)
		{
			OmniLight3D light = new OmniLight3D
			{
				LightColor = new Color(0.55f, 0.65f, 1.0f),
				OmniRange = 40.0f,
				OmniAttenuation = 1.0f,
				ShadowEnabled = false,
				Position = pos
			};
			AddChild(light); _nightLights.Add(light);
			light.LightEnergy = 0f;
			CreateTween().TweenProperty(light, "light_energy", 0.8f, 1.5f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
	}

	private void StartAmbientCloudSystem()
	{
		if (!IsInsideTree()) return;
		SpawnProceduralCloud();
		if (_cloudTimer != null && IsInstanceValid(_cloudTimer))
		{
			_cloudTimer.WaitTime = (float)GD.RandRange(2.0f, 4.0f);
			_cloudTimer.Start();
		}
	}

	private void SpawnProceduralCloud()
	{
		float gridWorldW = GridWidth * TileSize;
		float gridWorldD = GridDepth * TileSize;

		Vector3 camRight = Cam.GlobalTransform.Basis.X; camRight.Y = 0; camRight = camRight.Normalized();

		Vector3 midPoint = new Vector3(
			(float)GD.RandRange(0f, gridWorldW),
			(float)GD.RandRange(6.0f, 12.0f),
			(float)GD.RandRange(0f, gridWorldD));

		float travelDist = 100f;
		Vector3 startPos = midPoint - (camRight * (travelDist / 2f));
		Vector3 endPos = midPoint + (camRight * (travelDist / 2f));

		Node3D gustParent = new Node3D { Position = startPos };
		AddChild(gustParent);

		MeshInstance3D windGust = new MeshInstance3D();
		windGust.Mesh = new SphereMesh { Radius = 1.0f, Height = 1.0f };
		windGust.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(1f, 1f, 1f, 0.2f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = false,
			DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha,
			DistanceFadeMaxDistance = 40f, DistanceFadeMinDistance = 15f
		};
		windGust.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		windGust.RotationDegrees = new Vector3(0, (float)GD.RandRange(-30, 30), (float)GD.RandRange(-15, 15));
		gustParent.AddChild(windGust);

		float duration = (float)GD.RandRange(18f, 30f);
		Tween moveTween = CreateTween().SetParallel(true);
		moveTween.TweenProperty(gustParent, "position", endPos, duration).SetTrans(Tween.TransitionType.Linear);
		moveTween.TweenProperty(windGust, "position:z", (float)GD.RandRange(-5f, 5f), duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		moveTween.TweenProperty(windGust, "position:y", (float)GD.RandRange(-2f, 2f), duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		gustParent.Scale = Vector3.Zero;
		moveTween.TweenProperty(gustParent, "scale", Vector3.One, 3.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		moveTween.TweenProperty(gustParent, "scale", Vector3.Zero, 3.0f).SetDelay(duration - 3.0f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

		Tween shapeTween = CreateTween().SetLoops();
		float morphSpeed = duration / 3f;
		Vector3 ms1 = new((float)GD.RandRange(4f, 8f), (float)GD.RandRange(0.3f, 0.6f), (float)GD.RandRange(2f, 4f));
		Vector3 ms2 = new((float)GD.RandRange(3f, 6f), (float)GD.RandRange(0.5f, 1f), (float)GD.RandRange(2.5f, 5f));
		Vector3 r1 = windGust.RotationDegrees + new Vector3((float)GD.RandRange(-20, 20), (float)GD.RandRange(-45, 45), (float)GD.RandRange(-10, 10));
		Vector3 r2 = windGust.RotationDegrees + new Vector3((float)GD.RandRange(-20, 20), (float)GD.RandRange(-45, 45), (float)GD.RandRange(-10, 10));
		shapeTween.TweenProperty(windGust, "scale", ms1, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		shapeTween.Parallel().TweenProperty(windGust, "rotation_degrees", r1, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		shapeTween.TweenProperty(windGust, "scale", ms2, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		shapeTween.Parallel().TweenProperty(windGust, "rotation_degrees", r2, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		moveTween.Finished += () => { shapeTween.Kill(); gustParent.QueueFree(); };
	}

	private void SpawnRandomObstacles(int obstacleCount)
	{
		int adjustedCount = Mathf.Max(obstacleCount, GridWidth * GridDepth / 30);
		List<Vector2I> validTiles = _grid.Keys.Where(IsTileFree).ToList();
		System.Random rnd = new();
		validTiles = validTiles.OrderBy(x => rnd.Next()).ToList();

		int spawned = 0;
		foreach (var gridPos in validTiles)
		{
			if (spawned >= adjustedCount) break;
			SpawnObstacle(new Vector3(gridPos.X * TileSize, _grid[gridPos].Position.Y, gridPos.Y * TileSize), rnd.Next(2) == 0);
			spawned++;
		}
	}

	private void SpawnObstacle(Vector3 pos, bool isTall)
	{
		Node3D obsNode = new Node3D();
		Sprite3D sprite = new Sprite3D { Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass };

		Texture2D tex = isTall ? GD.Load<Texture2D>("res://assets/tree.png") : GD.Load<Texture2D>("res://assets/rock.png");
		float targetHeight = isTall ? 1.8f : 0.9f;

		sprite.Texture = tex;
		float scaleFactor = targetHeight / (tex.GetHeight() * sprite.PixelSize);
		sprite.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		sprite.Position = new Vector3(0, targetHeight / 2.0f, 0);

		obsNode.AddChild(sprite);
		AddChild(obsNode);
		_obstacles.Add(obsNode);

		obsNode.GlobalPosition = pos;
		obsNode.Scale = Vector3.Zero;
		CreateTween().TweenProperty(obsNode, "scale", Vector3.One, 0.4f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
	}
}
