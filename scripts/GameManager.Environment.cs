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
	private const int GridWidth = 10;
	private const int GridDepth = 10;

	private void GenerateGrid()
	{
		// JUICE: Make the grid spawn in a wave pattern instead of popping in!
		Tween spawnTween = CreateTween().SetParallel(true);

		for (int x = 0; x < GridWidth; x++)
		{
			for (int z = 0; z < GridDepth; z++)
			{
				Tile tile = TileScene.Instantiate<Tile>();
				AddChild(tile);
				tile.Setup(new Vector2I(x, z), TileSize);
				
				// Start invisible/tiny
				tile.Scale = new Vector3(0.001f, 0.001f, 0.001f); 
				
				Vector3 targetScale = new Vector3(0.96f, 1.0f, 0.96f);
				
				// Calculate a wave delay starting from the corner
				float delay = (x + z) * 0.03f; 
				
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
			env.AdjustmentEnabled = true;
			env.AdjustmentContrast = 1.05f; 
			env.AdjustmentSaturation = 1.1f;
			
			env.GlowEnabled = true; 
			env.GlowHdrThreshold = 0.9f; 
			env.GlowIntensity = 0.8f; 
			env.GlowStrength = 0.9f;
			env.GlowBloom = 0.0f;
			env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive;
			
			env.SsaoEnabled = true; 
			env.SsaoRadius = 1.0f;
			env.SsaoIntensity = 1.5f;
			env.SsilEnabled = true; 

			env.FogEnabled = true;
			env.VolumetricFogEnabled = true; 
			env.VolumetricFogDensity = 0.005f;

			switch (data.Light)
			{
				case LightingMood.Noon:
					MainLight.Visible = true;
					MainLight.LightColor = new Color(1f, 0.98f, 0.95f);
					MainLight.LightEnergy = 1.2f;
					MainLight.RotationDegrees = new Vector3(-75, 45, 0);
					env.BackgroundColor = new Color(0.4f, 0.6f, 0.9f); 
					env.AmbientLightColor = new Color(0.6f, 0.8f, 1f);   
					env.AmbientLightEnergy = 0.5f;
					env.FogLightColor = env.BackgroundColor;
					env.VolumetricFogAlbedo = new Color(0.6f, 0.75f, 1.0f);
					break;
				case LightingMood.Morning:
					MainLight.Visible = true;
					MainLight.LightColor = new Color(1f, 0.85f, 0.65f);   
					MainLight.LightEnergy = 1.2f;
					MainLight.RotationDegrees = new Vector3(-25, 60, 0); 
					env.BackgroundColor = new Color(0.8f, 0.5f, 0.4f); 
					env.AmbientLightColor = new Color(0.9f, 0.6f, 0.7f); 
					env.AmbientLightEnergy = 0.4f;
					env.FogLightColor = env.BackgroundColor; 
					env.VolumetricFogAlbedo = new Color(0.9f, 0.6f, 0.5f);
					break;
				case LightingMood.Night:
					MainLight.Visible = true;
					MainLight.LightColor = new Color(0.5f, 0.6f, 0.9f);  
					MainLight.LightEnergy = 0.6f;
					MainLight.RotationDegrees = new Vector3(-60, -30, 0);
					env.BackgroundColor = new Color(0.08f, 0.08f, 0.12f); 
					env.AmbientLightColor = new Color(0.2f, 0.3f, 0.5f); 
					env.AmbientLightEnergy = 0.8f;
					env.FogLightColor = env.BackgroundColor; 
					env.VolumetricFogAlbedo = new Color(0.15f, 0.15f, 0.25f);
					SpawnNightCornerLights(); 
					break;
				case LightingMood.Indoors:
					MainLight.Visible = false; 
					env.BackgroundColor = new Color(0.02f, 0.02f, 0.02f); 
					env.AmbientLightColor = new Color(0.8f, 0.7f, 0.5f); 
					env.AmbientLightEnergy = 0.8f;
					env.FogLightColor = env.BackgroundColor;
					env.VolumetricFogAlbedo = new Color(0.05f, 0.04f, 0.03f); 
					env.VolumetricFogDensity = 0.08f;
					break;
			}
		}

		StandardMaterial3D groundMaterial = new StandardMaterial3D();
		FastNoiseLite noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.03f };
		NoiseTexture2D normalTex = new NoiseTexture2D { Noise = noise, AsNormalMap = true, BumpStrength = 3.0f };
		groundMaterial.NormalEnabled = true;
		groundMaterial.NormalTexture = normalTex;

		switch (data.Ground)
		{
			case GroundType.Grass: groundMaterial.AlbedoColor = new Color(0.2f, 0.35f, 0.15f); groundMaterial.Roughness = 0.95f; break;
			case GroundType.Dirt: groundMaterial.AlbedoColor = new Color(0.3f, 0.22f, 0.15f); groundMaterial.Roughness = 1.0f; break;
			case GroundType.Marble: groundMaterial.AlbedoColor = new Color(0.85f, 0.85f, 0.9f); groundMaterial.Roughness = 0.15f; break;
		}

		foreach (var tile in _grid.Values)
		{
			if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh)
			{
				mesh.MaterialOverride = null; 
				mesh.SetSurfaceOverrideMaterial(0, (StandardMaterial3D)groundMaterial.Duplicate());
			}
		}
		
		if (_backgroundPlane == null)
		{
			_backgroundPlane = new MeshInstance3D();
			PlaneMesh plane = new PlaneMesh { Size = new Vector2(300, 300) };
			_backgroundPlane.Mesh = plane;
			_backgroundPlane.Position = new Vector3((GridWidth * TileSize) / 2f, -0.05f, (GridDepth * TileSize) / 2f);
			AddChild(_backgroundPlane);
		}

		StandardMaterial3D planeMat = (StandardMaterial3D)groundMaterial.Duplicate();
		planeMat.Uv1Scale = new Vector3(150, 150, 1);
		_backgroundPlane.MaterialOverride = planeMat;
	}

	private async Task SetupGridElevation(BattleSetup data)
	{
		FastNoiseLite noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Seed = (int)GD.Randi(), Frequency = 0.15f };
		Tween riseTween = CreateTween();
		riseTween.SetParallel(true);
		bool rising = false;

		foreach (var kvp in _grid)
		{
			Vector2I pos = kvp.Key; Tile tile = kvp.Value;
			float targetHeight = 0.01f;

			if (data.ElevationEnabled)
			{
				int distX = Mathf.Min(pos.X, GridWidth - 1 - pos.X);
				int distZ = Mathf.Min(pos.Y, GridDepth - 1 - pos.Y);
				int distToEdge = Mathf.Min(distX, distZ);

				if (distToEdge > 0)
				{
					float rawNoise = (noise.GetNoise2D(pos.X, pos.Y) + 1f) / 2f; 
					float falloff = distToEdge == 1 ? 0.4f : 1.0f; 
					targetHeight = 0.01f + (rawNoise * 0.7f * falloff); 
				}
			}

			if (Mathf.Abs(tile.Position.Y - targetHeight) > 0.001f)
			{
				rising = true;
				float delay = (float)GD.RandRange(0.0f, 0.2f);
				riseTween.TweenProperty(tile, "position:y", targetHeight, 0.5f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out).SetDelay(delay);
				
				if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh && mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
				{
					float heightRatio = Mathf.Clamp(targetHeight / 0.75f, 0f, 1f);
					Color baseColor = mat.AlbedoColor;
					Color highlightColor = new Color(baseColor.R * 1.35f, baseColor.G * 1.35f, baseColor.B * 1.15f, baseColor.A);
					Color finalColor = baseColor.Lerp(highlightColor, heightRatio);
					riseTween.TweenProperty(mat, "albedo_color", finalColor, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out).SetDelay(delay);
				}
			}
		}

		if (rising) await ToSignal(GetTree().CreateTimer(0.7f), "timeout");
		else riseTween.Kill(); 
	}

	private void SpawnDioramas()
	{
		if (BackgroundDioramas == null || BackgroundDioramas.Count == 0) return;

		float gridSize = GridWidth * TileSize;
		Vector3 center = new Vector3((GridWidth - 1) * TileSize / 2f, 0, (GridDepth - 1) * TileSize / 2f);
		Vector3 camDir = (Cam.GlobalPosition - center);
		camDir.Y = 0; camDir = camDir.Normalized();

		Vector3[] compassDirs = { new Vector3(0, 0, -1), new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(-1, 0, 0) };
		System.Random rnd = new System.Random();
		List<Vector3> bgDirs = new List<Vector3>();
		List<(Vector3 pos, bool isBg)> slotsToSpawn = new();

		foreach (Vector3 dir in compassDirs)
		{
			bool isBg = dir.Dot(camDir) < -0.1f;
			float gap = isBg ? 0.5f : 4.0f;
			slotsToSpawn.Add((center + (dir * (gridSize + gap)), isBg));
			if (isBg) bgDirs.Add(dir);
		}

		if (bgDirs.Count == 2) slotsToSpawn.Add((center + (bgDirs[0] * (gridSize + 0.5f)) + (bgDirs[1] * (gridSize + 0.5f)), true)); 

		foreach (var slot in slotsToSpawn)
		{
			Node3D dioramaParent = new Node3D { Position = new Vector3(slot.pos.X, 0, slot.pos.Z) };
			AddChild(dioramaParent);
			_activeDioramas.Add(dioramaParent);

			if (slot.isBg)
			{
				float subSquareSize = (gridSize / 2f) - 0.5f;
				float subRadius = gridSize / 4f;
				Vector3[] subOffsets = { new Vector3(-subRadius, 0, -subRadius), new Vector3(subRadius, 0, -subRadius), new Vector3(-subRadius, 0, subRadius), new Vector3(subRadius, 0, subRadius) };

				foreach (Vector3 subOff in subOffsets)
				{
					Node3D model = InstantiateAndScaleRandomModel(rnd, subSquareSize, out _);
					model.Position = subOff; model.RotationDegrees = new Vector3(0, rnd.Next(0, 4) * 90, 0);
					dioramaParent.AddChild(model);
				}
			}
			else
			{
				Node3D model = InstantiateAndScaleRandomModel(rnd, gridSize, out float scaledHeight);
				dioramaParent.AddChild(model);
				dioramaParent.Position = new Vector3(slot.pos.X, -(scaledHeight * 0.88f), slot.pos.Z);
			}

			dioramaParent.Scale = Vector3.Zero;
			CreateTween().TweenProperty(dioramaParent, "scale", Vector3.One, 0.7f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay((float)GD.RandRange(0.0f, 0.4f));
		}
	}

	private Node3D InstantiateAndScaleRandomModel(System.Random rnd, float targetSquareSize, out float finalScaledHeight)
	{
		PackedScene randomScene = BackgroundDioramas[rnd.Next(BackgroundDioramas.Count)];
		Node3D model = randomScene.Instantiate<Node3D>();
		Aabb bounds = new Aabb(); bool first = true;
		
		foreach (Node child in model.FindChildren("*", "MeshInstance3D", true, false))
		{
			if (child is MeshInstance3D meshInstance)
			{
				Aabb transformedAabb = meshInstance.Transform * meshInstance.GetAabb();
				if (first) { bounds = transformedAabb; first = false; }
				else bounds = bounds.Merge(transformedAabb);
			}
		}

		float maxBaseDimension = Mathf.Max(bounds.Size.X, bounds.Size.Z);
		if (maxBaseDimension == 0) maxBaseDimension = 1f; 

		float targetScaleFactor = targetSquareSize / maxBaseDimension;
		model.Scale = new Vector3(targetScaleFactor, targetScaleFactor, targetScaleFactor);
		finalScaledHeight = bounds.Size.Y * targetScaleFactor;
		return model;
	}

	private void SpawnNightCornerLights()
	{
		float height = 8.0f; float maxX = (GridWidth - 1) * TileSize; float maxZ = (GridDepth - 1) * TileSize;
		Vector3[] corners = { new Vector3(0, height, 0), new Vector3(maxX, height, 0), new Vector3(0, height, maxZ), new Vector3(maxX, height, maxZ) };

		foreach (var pos in corners)
		{
			OmniLight3D light = new OmniLight3D { LightColor = new Color(0.6f, 0.7f, 1.0f), OmniRange = 45.0f, OmniAttenuation = 0.8f, ShadowEnabled = false, Position = pos };
			AddChild(light);
			_nightLights.Add(light);
			light.LightEnergy = 0f;
			CreateTween().TweenProperty(light, "light_energy", 1.2f, 1.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
	}

	private void StartAmbientCloudSystem()
	{
		if (!IsInsideTree()) return;
		
		SpawnProceduralCloud();
		
		if (_cloudTimer != null && IsInstanceValid(_cloudTimer))
		{
			// Shorter wait time for regular, steady, one-by-one spawning
			_cloudTimer.WaitTime = (float)GD.RandRange(1.0f, 2.5f);
			_cloudTimer.Start();
		}
	}

	private void SpawnProceduralCloud()
	{
		// 1. PATH MATH: Calculate left-to-right relative to the CAMERA, not the world grid
		Vector3 camRight = Cam.GlobalTransform.Basis.X;
		camRight.Y = 0; // Keep the flight path perfectly flat
		camRight = camRight.Normalized();
		
		// Pick a random spot floating somewhere over the grid to act as the "center" of our path
		Vector3 midPoint = new Vector3(
			(float)GD.RandRange(0f, GridWidth * TileSize),
			(float)GD.RandRange(2.0f, 4.5f),
			(float)GD.RandRange(0f, GridDepth * TileSize)
		);
		
		// Create a flight path that spans across the screen
		float travelDist = 45f; 
		Vector3 startPos = midPoint - (camRight * (travelDist / 2f)); // Way off-screen left
		Vector3 endPos = midPoint + (camRight * (travelDist / 2f));   // Way off-screen right

		// 2. THE PARENT (Handles the linear left-to-right travel)
		Node3D gustParent = new Node3D();
		gustParent.Position = startPos;
		AddChild(gustParent);

		// 3. THE MESH (Handles the shape, look, and swiveling)
		MeshInstance3D windGust = new MeshInstance3D();
		windGust.Mesh = new SphereMesh { Radius = 1.0f, Height = 1.0f }; 
		
		windGust.MaterialOverride = new StandardMaterial3D { 
			AlbedoColor = new Color(1f, 1f, 1f, 0.35f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			NoDepthTest = true, // THE FIX: This forces the cloud to draw ON TOP of all 3D objects!
			RenderPriority = 1, // Ensures it draws after standard elements
			DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha, 
			DistanceFadeMaxDistance = 15f, 
			DistanceFadeMinDistance = 5f 
		};
		windGust.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		
		windGust.RotationDegrees = new Vector3(0, (float)GD.RandRange(-30, 30), (float)GD.RandRange(-15, 15));
		gustParent.AddChild(windGust);

		// 4. SLOW, LAZY MOVEMENT TWEEN
		float duration = (float)GD.RandRange(12f, 20f);
		Tween moveTween = CreateTween().SetParallel(true);
		
		// Move the PARENT from startPos to endPos across the screen
		moveTween.TweenProperty(gustParent, "position", endPos, duration)
			.SetTrans(Tween.TransitionType.Linear);
			
		// Sine wave on the CHILD'S local Z and Y for a bobbing feel (so it doesn't fight the parent's movement)
		moveTween.TweenProperty(windGust, "position:z", (float)GD.RandRange(-3f, 3f), duration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		moveTween.TweenProperty(windGust, "position:y", (float)GD.RandRange(-1.5f, 1.5f), duration)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		// Smoothly fade/scale in and out
		gustParent.Scale = Vector3.Zero;
		moveTween.TweenProperty(gustParent, "scale", Vector3.One, 2.0f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		moveTween.TweenProperty(gustParent, "scale", Vector3.Zero, 2.0f)
			.SetDelay(duration - 2.0f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);

		// 5. INFINITE MORPHING TWEEN
		Tween shapeTween = CreateTween().SetLoops(); 
		float morphSpeed = duration / 3f; 
		
		Vector3 morphScale1 = new Vector3((float)GD.RandRange(3.0f, 5.0f), (float)GD.RandRange(0.2f, 0.5f), (float)GD.RandRange(1.0f, 2.0f));
		Vector3 morphScale2 = new Vector3((float)GD.RandRange(2.0f, 3.5f), (float)GD.RandRange(0.4f, 0.8f), (float)GD.RandRange(1.5f, 3.0f));
		
		Vector3 rot1 = windGust.RotationDegrees + new Vector3((float)GD.RandRange(-20, 20), (float)GD.RandRange(-45, 45), (float)GD.RandRange(-10, 10));
		Vector3 rot2 = windGust.RotationDegrees + new Vector3((float)GD.RandRange(-20, 20), (float)GD.RandRange(-45, 45), (float)GD.RandRange(-10, 10));

		shapeTween.TweenProperty(windGust, "scale", morphScale1, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		shapeTween.Parallel().TweenProperty(windGust, "rotation_degrees", rot1, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		
		shapeTween.TweenProperty(windGust, "scale", morphScale2, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		shapeTween.Parallel().TweenProperty(windGust, "rotation_degrees", rot2, morphSpeed).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		moveTween.Finished += () => {
			shapeTween.Kill(); 
			gustParent.QueueFree();
		};
	}

	private void SpawnRandomObstacles(int obstacleCount)
	{
		List<Vector2I> validTiles = _grid.Keys.Where(IsTileFree).ToList();
		System.Random rnd = new System.Random();
		validTiles = validTiles.OrderBy(x => rnd.Next()).ToList();

		int spawned = 0;
		foreach (var gridPos in validTiles)
		{
			if (spawned >= obstacleCount) break;
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

	private async Task ClearBoardAsync()
	{
		DeselectUnit();

		foreach (var u in _units.Where(IsInstanceValid)) { Tween t = CreateTween(); t.TweenProperty(u, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); t.Finished += () => u.QueueFree(); }
		_units.Clear();
		
		foreach (var obs in _obstacles.Where(IsInstanceValid)) { Tween t = CreateTween(); t.TweenProperty(obs, "scale", Vector3.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); t.Finished += () => obs.QueueFree(); }
		_obstacles.Clear();
		
		foreach (var diorama in _activeDioramas.Where(IsInstanceValid)) { Tween t = CreateTween(); t.TweenProperty(diorama, "scale", Vector3.Zero, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); t.Finished += () => diorama.QueueFree(); }
		_activeDioramas.Clear();
		
		foreach (var light in _nightLights.Where(IsInstanceValid)) { Tween t = CreateTween(); t.TweenProperty(light, "light_energy", 0.0f, 0.35f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In); t.Finished += () => light.QueueFree(); }
		_nightLights.Clear();
		
		Tween flattenTween = CreateTween();
		flattenTween.SetParallel(true);
		bool hasElevatedTiles = false;

		foreach (var tile in _grid.Values)
		{
			if (tile.Position.Y > 0.02f)
			{
				hasElevatedTiles = true;
				float delay = (float)GD.RandRange(0.0f, 0.15f);
				flattenTween.TweenProperty(tile, "position:y", 0.01f, 0.4f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out).SetDelay(delay);
				
				if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh && mesh.GetSurfaceOverrideMaterial(0) is StandardMaterial3D mat)
				{
					Color darkColor = new Color(mat.AlbedoColor.R * 0.75f, mat.AlbedoColor.G * 0.75f, mat.AlbedoColor.B * 0.85f, mat.AlbedoColor.A);
					flattenTween.TweenProperty(mat, "albedo_color", darkColor, 0.4f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In).SetDelay(delay);
				}
			}
		}

		if (hasElevatedTiles) await ToSignal(GetTree().CreateTimer(0.6f), "timeout"); 
		else { flattenTween.Kill(); await ToSignal(GetTree().CreateTimer(0.25f), "timeout"); }
	}
}
