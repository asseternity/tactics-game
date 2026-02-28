using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	private List<Node> _campNodes = new();
	private Control _campUIRoot;

	public async Task EnterCampStage()
	{
		_currentState = State.Cutscene; // Disables camera movement and input
		ShowActions(false);
		
		if (DimOverlay != null) DimOverlay.Visible = false;
		if (StatsLabel != null) StatsLabel.Visible = false;

		// Glide camera back to initial pos
		Cam.CreateTween().TweenProperty(Cam, "global_position", _initialCamPos, 1.5f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		// 1. Wait for units/obstacles to shrink away
		await ClearBoardAsync();

		// JUICE: Make the grid tiles physically plummet into the abyss!
		Tween tileDrop = CreateTween().SetParallel(true);
		foreach (var tile in _grid.Values)
		{
			float delay = (float)GD.RandRange(0f, 0.25f);
			tileDrop.TweenProperty(tile, "position:y", -20f, 0.5f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In).SetDelay(delay);
		}
		
		// Wait for the tiles to finish falling
		await ToSignal(GetTree().CreateTimer(0.75f), "timeout");

		// Hide them so the engine doesn't render them deep underground
		foreach (var tile in _grid.Values)
		{
			tile.Visible = false;
			if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh) mesh.Visible = false;
		}

		// Set the mood to Night and Dirt
		ApplyEnvironment(new BattleSetup { Light = LightingMood.Night, Ground = GroundType.Dirt });

		Vector2 screenSize = GetViewport().GetVisibleRect().Size;
		Vector3 rayOrigin = Cam.ProjectRayOrigin(screenSize / 2f);
		Vector3 rayDir = Cam.ProjectRayNormal(screenSize / 2f);
		Plane groundPlane = new Plane(Vector3.Up, 0f);
		
		Vector3 campCenter = groundPlane.IntersectsRay(rayOrigin, rayDir) ?? new Vector3((GridWidth - 1) * TileSize / 2f, 0, (GridDepth - 1) * TileSize / 2f);

		SpawnCampfire(campCenter);
		SpawnCampTrees(campCenter);
		SpawnCampParty(campCenter);
		ShowCampUI();
	}

	private void SpawnCampfire(Vector3 position)
	{
		Node3D campfireRoot = new Node3D { Position = position };
		AddChild(campfireRoot);
		_campNodes.Add(campfireRoot);

		Texture2D tex = GD.Load<Texture2D>("res://assets/campfire.png");
		Sprite3D fireSprite = new Sprite3D
		{
			Texture = tex,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
			PixelSize = 0.015f,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off 
		};
		
		float targetHeight = 1.0f; 
		float scaleFactor = targetHeight / (tex.GetHeight() * fireSprite.PixelSize);
		Vector3 baseScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		
		fireSprite.Scale = baseScale;
		fireSprite.Position = new Vector3(0, targetHeight / 2f, 0);
		campfireRoot.AddChild(fireSprite);

		// JUICE: Pop the campfire in from nothing
		campfireRoot.Scale = new Vector3(0.001f, 0.001f, 0.001f);
		campfireRoot.CreateTween().TweenProperty(campfireRoot, "scale", Vector3.One, 0.6f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		// FIRE ANIMATION: Sway side to side from the base
		fireSprite.RotationDegrees = Vector3.Zero;
		Tween swayTween = fireSprite.CreateTween().SetLoops();
		swayTween.TweenProperty(fireSprite, "rotation_degrees:z", 6f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		swayTween.TweenProperty(fireSprite, "rotation_degrees:z", -6f, 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		swayTween.TweenProperty(fireSprite, "rotation_degrees:z", 0f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		OmniLight3D fireLight = new OmniLight3D
		{
			LightColor = new Color(1f, 0.65f, 0.3f), 
			OmniRange = 25f, 
			LightEnergy = 3.5f, 
			ShadowEnabled = true,
			Position = new Vector3(0, 1.0f, 0) 
		};
		campfireRoot.AddChild(fireLight);
		_ = FlickerCampfireLight(fireLight);

		CpuParticles3D sparks = new CpuParticles3D
		{
			Amount = 12,
			Lifetime = 1.2f,
			EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.2f,
			Gravity = new Vector3(0, 2.0f, 0),
			InitialVelocityMin = 0.5f,
			InitialVelocityMax = 1.2f,
			Color = new Color(1f, 0.8f, 0.3f, 0.8f),
			Position = new Vector3(0, 0.3f, 0)
		};
		sparks.MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
		campfireRoot.AddChild(sparks);
	}

	private async Task FlickerCampfireLight(OmniLight3D light)
	{
		var rng = new System.Random();
		while (IsInstanceValid(light))
		{
			float targetEnergy = 2.5f + (float)rng.NextDouble() * 1.5f; 
			float duration = 0.1f + (float)rng.NextDouble() * 0.2f;
			Tween t = light.CreateTween();
			t.TweenProperty(light, "light_energy", targetEnergy, duration).SetTrans(Tween.TransitionType.Sine);
			await ToSignal(t, Tween.SignalName.Finished);
		}
	}

	private void SpawnCampTrees(Vector3 campCenter)
	{
		var rng = new System.Random();
		Texture2D treeTex = GD.Load<Texture2D>("res://assets/tree.png");

		SpawnTreeRing(campCenter, treeTex, rng, 8, 3.0f, 5.0f);     
		SpawnTreeRing(campCenter, treeTex, rng, 15, 6.0f, 10.0f);   
		SpawnTreeRing(campCenter, treeTex, rng, 30, 12.0f, 25.0f);  
	}

	private void SpawnTreeRing(Vector3 center, Texture2D tex, System.Random rng, int count, float minRadius, float maxRadius)
	{
		for (int i = 0; i < count; i++)
		{
			float angle = (float)(rng.NextDouble() * Mathf.Pi * 2);
			float radius = minRadius + (float)(rng.NextDouble() * (maxRadius - minRadius));
			Vector3 treePos = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);

			Sprite3D treeSprite = new Sprite3D
			{
				Texture = tex,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
				PixelSize = 0.015f
			};

			float targetHeight = 1.8f + (float)(rng.NextDouble() * 1.2f);
			float scaleFactor = targetHeight / (tex.GetHeight() * treeSprite.PixelSize);
			
			// JUICE: Trees start at scale 0 and pop in organically
			treeSprite.Scale = new Vector3(0.001f, 0.001f, 0.001f); 
			treeSprite.Position = new Vector3(treePos.X, targetHeight / 2f, treePos.Z);
			
			treeSprite.CreateTween().TweenProperty(treeSprite, "scale", new Vector3(scaleFactor, scaleFactor, scaleFactor), 0.6f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay((float)rng.NextDouble() * 0.4f);

			if (radius > 10.0f)
			{
				treeSprite.Modulate = new Color(0.6f, 0.6f, 0.7f, 1f); 
			}

			AddChild(treeSprite);
			_campNodes.Add(treeSprite);
		}
	}

	private void SpawnCampParty(Vector3 campCenter)
	{
		float leftOffset = 1.2f;
		float rightOffset = 1.2f;
		var rng = new System.Random();

		for (int i = 0; i < _party.Count; i++)
		{
			PersistentUnit unit = _party[i];
			bool placeOnLeft = (i % 2 == 0); 
			Vector3 spawnPos = campCenter;
			
			if (placeOnLeft)
			{
				spawnPos.X -= leftOffset;
				spawnPos.Z += (float)(rng.NextDouble() - 0.5) * 1.0f;
				leftOffset += 1.2f;
			}
			else
			{
				spawnPos.X += rightOffset;
				spawnPos.Z += (float)(rng.NextDouble() - 0.5) * 1.0f;
				rightOffset += 1.2f;
			}

			Texture2D tex = GD.Load<Texture2D>(unit.Profile.SpritePath);
			Sprite3D unitSprite = new Sprite3D
			{
				Texture = tex,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass,
				PixelSize = 0.015f
			};
			
			float targetHeight = 2.0f; 
			float scaleFactor = targetHeight / (tex.GetHeight() * unitSprite.PixelSize);
			unitSprite.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
			unitSprite.Position = new Vector3(spawnPos.X, targetHeight / 2f, spawnPos.Z);
			
			bool defaultFacesRight = unit.Profile.DefaultFacing != UnitFacing.Left;
			if (placeOnLeft) unitSprite.FlipH = !defaultFacesRight; 
			else unitSprite.FlipH = defaultFacesRight; 

			AddChild(unitSprite);
			_campNodes.Add(unitSprite);

			// JUICE: Party jumps in from the sky
			unitSprite.Position += new Vector3(0, 8f, 0);
			unitSprite.CreateTween().TweenProperty(unitSprite, "position:y", unitSprite.Position.Y - 8f, 0.6f)
				.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out).SetDelay((float)rng.NextDouble() * 0.3f);
		}
	}

	private void ShowCampUI()
	{
		_campUIRoot = new Control(); 
		_campUIRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (MasterTheme != null) _campUIRoot.Theme = MasterTheme; 
		
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(_campUIRoot); else AddChild(_campUIRoot);

		VBoxContainer bottomContainer = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		bottomContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bottomContainer.AddThemeConstantOverride("separation", 20);
		_campUIRoot.AddChild(bottomContainer);

		Label title = new Label { Text = "The Party Rests...", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 36);
		title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.5f));
		if (MasterTheme != null && MasterTheme.DefaultFont != null) title.AddThemeFontOverride("font", MasterTheme.DefaultFont);
		bottomContainer.AddChild(title);

		Button nextBtn = new Button { Text = "Embark on Next Mission", CustomMinimumSize = new Vector2(350, 70), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		AddButtonJuice(nextBtn);
		
		MarginContainer margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_bottom", 60);
		margin.AddChild(nextBtn);
		bottomContainer.AddChild(margin);

		_campUIRoot.Modulate = new Color(1, 1, 1, 0);
		CreateTween().TweenProperty(_campUIRoot, "modulate:a", 1.0f, 1.0f).SetDelay(1.5f); 

		// JUICE: Exiting the camp animates everything away!
		nextBtn.Pressed += async () => {
			Tween outTween = CreateTween().SetParallel(true);
			outTween.TweenProperty(_campUIRoot, "modulate:a", 0f, 0.3f);

			// Shrink the entire camp away dynamically
			foreach (Node node in _campNodes)
			{
				if (node is Node3D n3d)
				{
					float delay = (float)GD.RandRange(0f, 0.2f);
					outTween.TweenProperty(n3d, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.4f)
						.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In).SetDelay(delay);
				}
			}
			
			// Wait for the exit animation to finish
			await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
			
			ExitCampStage();
		};
	}

	private void ExitCampStage()
	{
		if (IsInstanceValid(_campUIRoot)) _campUIRoot.QueueFree();
		if (StatsLabel != null) StatsLabel.Visible = true;

		// Note: We don't manually restore grid visibility here anymore, 
		// because LoadMission will ClearBoard() and Generate a fresh one immediately!

		foreach (Node node in _campNodes)
		{
			if (IsInstanceValid(node)) node.QueueFree();
		}
		_campNodes.Clear();

		LoadMission(_currentMissionIndex + 1);
	}
}
