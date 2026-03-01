// Unit.cs
using Godot;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class Unit : Node3D
{
	public bool IsFriendly = true;
	public event System.Action<Unit> OnDied;
	public PersistentUnit Data { get; private set; }
	public bool HasMoved = false;
	public bool HasAttacked = false;
	public bool IsSelected { get; private set; } = false;
	public UnitFacing CurrentFacing { get; private set; }

	private Sprite3D _sprite;
	private Label3D _targetIcon;
	private SubViewport _uiViewport;
	private Sprite3D _uiSprite;
	private ProgressBar _hpBar;
	private ProgressBar _hpPreviewBar;
	private Label _hpLabel;
	private ProgressBar _xpBar;
	private Label _xpLabel;
	private Tween _previewTween;
	private bool _isPreviewing = false;
	
	private bool _isHovered = false;
	private Tween _hoverTween;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite3D>("Sprite3D");
		_targetIcon = new Label3D { Text = "▼", FontSize = 120, Modulate = new Color(1, 0.2f, 0.2f), OutlineModulate = new Color(0, 0, 0), OutlineSize = 10, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = new Vector3(0, 2.9f, 0), Visible = false, RenderPriority = 20 };
		AddChild(_targetIcon);

		Tween bobTween = CreateTween().SetLoops();
		bobTween.TweenProperty(_targetIcon, "position:y", 3.2f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		bobTween.TweenProperty(_targetIcon, "position:y", 2.9f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		_uiViewport = new SubViewport { TransparentBg = true, Size = new Vector2I(200, 100), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		AddChild(_uiViewport);

		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 6);
		_uiViewport.AddChild(vbox);

		// HP section
		MarginContainer hpContainer = new MarginContainer { CustomMinimumSize = new Vector2(180, 35) };
		_hpPreviewBar = new ProgressBar { CustomMinimumSize = new Vector2(180, 35), ShowPercentage = false, Step = 0.1 };
		_hpPreviewBar.AddThemeStyleboxOverride("background", CreateRoundedStyle(new Color(0.1f, 0.1f, 0.1f, 0.9f)));
		_hpPreviewBar.AddThemeStyleboxOverride("fill", CreateRoundedStyle(new Color(1f, 0f, 0f, 1f)));

		_hpBar = new ProgressBar { CustomMinimumSize = new Vector2(180, 35), ShowPercentage = false, Step = 0.1 };
		_hpBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
		_hpBar.AddThemeStyleboxOverride("fill", CreateRoundedStyle(new Color(0.2f, 0.9f, 0.2f, 1f)));

		_hpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_hpLabel.AddThemeFontSizeOverride("font_size", 22);
		_hpLabel.AddThemeConstantOverride("outline_size", 6);

		hpContainer.AddChild(_hpPreviewBar);
		hpContainer.AddChild(_hpBar);
		hpContainer.AddChild(_hpLabel);
		vbox.AddChild(hpContainer);

		// === XP BAR – PERFECTLY CENTERED "Level X" ===
		MarginContainer xpContainer = new MarginContainer { CustomMinimumSize = new Vector2(180, 35) };

		_xpBar = new ProgressBar { 
			CustomMinimumSize = new Vector2(180, 25), 
			ShowPercentage = false, 
			Step = 0.1f 
		};
		_xpBar.AddThemeStyleboxOverride("background", CreateRoundedStyle(new Color(0.1f, 0.1f, 0.1f, 0.9f)));
		_xpBar.AddThemeStyleboxOverride("fill", CreateRoundedStyle(new Color(1f, 0.8f, 0f, 1f)));

		_xpLabel = new Label 
		{ 
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		
		// === THE FIX ===
		// This forces the label to expand and perfectly overlay the entire Progress Bar.
		_xpLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		
		_xpLabel.AddThemeFontSizeOverride("font_size", 16);
		_xpLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
		_xpLabel.AddThemeConstantOverride("outline_size", 5);

		_xpBar.AddChild(_xpLabel);
		xpContainer.AddChild(_xpBar);
		vbox.AddChild(xpContainer);

		_uiSprite = new Sprite3D { Texture = _uiViewport.GetTexture(), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 10, PixelSize = 0.008f, Position = new Vector3(0, 2.1f, 0) };
		AddChild(_uiSprite);
	}

	private StyleBoxFlat CreateRoundedStyle(Color color)
	{
		return new StyleBoxFlat { BgColor = color, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, BorderWidthBottom = 3, BorderWidthTop = 3, BorderWidthLeft = 3, BorderWidthRight = 3, BorderColor = new Color(0, 0, 0, 1f) };
	}

	public void Setup(PersistentUnit data, bool isFriendly)
	{
		Data = data;
		IsFriendly = isFriendly;
		
		// Initialize the facing state!
		CurrentFacing = data.Profile.DefaultFacing;
		
		if (GameManager.Instance != null && GameManager.Instance.MasterTheme != null)
		{
			_hpLabel.Theme = GameManager.Instance.MasterTheme;
			_xpLabel.Theme = GameManager.Instance.MasterTheme;
		}
		
		_hpBar.MaxValue = Data.MaxHP;
		_hpPreviewBar.MaxValue = Data.MaxHP;
		
		if (IsFriendly)
		{
			_xpBar.Visible = true;
			_xpBar.MaxValue = Data.MaxXP;
			_xpBar.Value = Data.CurrentXP;
		}
		else
		{
			_xpBar.Visible = false;
			_hpBar.AddThemeStyleboxOverride("fill", CreateRoundedStyle(new Color(0.9f, 0.2f, 0.2f, 1f)));
		}

		Texture2D tex = GD.Load<Texture2D>(data.Profile.SpritePath);
		if (tex != null)
		{
			_sprite.Texture = tex;
			float scaleFactor = 1.8f / (tex.GetHeight() * _sprite.PixelSize);
			_sprite.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
			_sprite.SetMeta("BaseScale", _sprite.Scale);
			
			// Wipe out any broken outlines
			foreach (Node child in _sprite.GetChildren()) child.QueueFree();

			// === 1. NATIVE SPRITE3D SETUP ===
			_sprite.MaterialOverride = null; // Clean out the corrupted material!
			_sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Discard;
			_sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
			_sprite.Shaded = true;
			_sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
			
			// === 2. THE PERFECT OUTLINE ===
			Sprite3D outline = new Sprite3D
			{
				Texture = tex,
				PixelSize = _sprite.PixelSize,
				Modulate = new Color(0, 0, 0, 1), // Pure black
				AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, // Prevent double shadows
				Shaded = false,
				
				// CRITICAL FIX: The outline MUST NOT billboard! 
				// It natively inherits the parent's billboard rotation. This stops the matrix explosion!
				Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
				Scale = new Vector3(1.08f, 1.08f, 1.08f), // Slightly thicker
				Position = new Vector3(0, 0, -0.02f), // Push safely backwards in local space
				RenderPriority = -1 
			};
			_sprite.AddChild(outline);
		}
		
		_sprite.SetMeta("BasePos", _sprite.Position);
		
		UpdateVisuals();
	}

	public void UpdateVisuals()
	{
		if (_sprite == null || Data == null) return;

		// === NEW: Fetch total HP bounds and clamp if necessary! ===
		int totalMaxHP = Data.GetTotalMaxHP();
		if (Data.CurrentHP > totalMaxHP) Data.CurrentHP = totalMaxHP; 

		if (!_isPreviewing)
		{
			_hpLabel.Text = $"{Data.CurrentHP}/{totalMaxHP}";   
			_hpBar.MaxValue = totalMaxHP; // Ensure the bar resizes!
			_hpPreviewBar.MaxValue = totalMaxHP; 
			CreateTween().TweenProperty(_hpBar, "value", (double)Data.CurrentHP, 0.15f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_hpPreviewBar.Value = Data.CurrentHP;
		}

		if (IsFriendly) _xpLabel.Text = $"Level {Data.Level}";

		// === 3. HDR COLORS (NATIVE EMISSION) ===
		// By pushing RGB values above 1.0, Godot natively makes them emit light!
		Color spriteColor;
		if (IsSelected) 
			spriteColor = new Color(1.6f, 1.5f, 0.7f, 1.0f); // Bright glowing yellow
		else if (IsFriendly && HasMoved && HasAttacked) 
			spriteColor = new Color(0.4f, 0.4f, 0.4f, 1.0f); // True dark grey for exhausted
		else if (IsFriendly) 
			spriteColor = new Color(1.2f, 1.2f, 1.2f, 1.0f); // 20% HDR light emission
		else 
			spriteColor = new Color(1.3f, 0.9f, 0.9f, 1.0f); // Emissive red tint

		if (!_isPreviewing) _sprite.Modulate = spriteColor;
	}

	// === DAMAGE RANDOMIZATION (new) ===
	public int GetMinDamage() => Mathf.FloorToInt(Data.AttackDamage * 0.8f);
	public int GetMaxDamage() => Mathf.CeilToInt(Data.AttackDamage * 1.2f);

	public void PreviewDamage(int dmg)   // Called with MIN damage for preview
	{
		if (_isPreviewing) return;
		_isPreviewing = true;
		int newHp = Mathf.Max(0, Data.CurrentHP - dmg);

		_hpBar.Value = newHp;
		_hpLabel.Text = $"{newHp}/{Data.MaxHP} (-{Mathf.Min(dmg, Data.CurrentHP)})";

		if (_previewTween != null && _previewTween.IsValid()) _previewTween.Kill();
		_previewTween = CreateTween().SetLoops();

		StyleBoxFlat previewStyle = (StyleBoxFlat)_hpPreviewBar.GetThemeStylebox("fill");
		_previewTween.TweenProperty(previewStyle, "bg_color", new Color(1f, 1f, 1f, 1f), 0.15f);
		_previewTween.TweenProperty(previewStyle, "bg_color", new Color(1f, 0f, 0f, 1f), 0.15f);

		if (newHp <= 0)
		{
			_previewTween.Parallel().TweenProperty(_sprite, "modulate", new Color(5f, 5f, 5f, 1f), 0.15f);
			_previewTween.Parallel().TweenProperty(_sprite, "modulate", new Color(1f, 0f, 0f, 1f), 0.15f).SetDelay(0.15f);
		}
	}

	public void ClearPreview()
	{
		if (!_isPreviewing) return;
		_isPreviewing = false;
		if (_previewTween != null && _previewTween.IsValid()) _previewTween.Kill();

		StyleBoxFlat previewStyle = (StyleBoxFlat)_hpPreviewBar.GetThemeStylebox("fill");
		previewStyle.BgColor = new Color(1f, 0f, 0f, 1f);
		UpdateVisuals();
	}

	public async Task TakeDamage(int dmg, Unit attacker = null)
	{
		ClearPreview();
		Data.CurrentHP -= dmg;
		if (Data.CurrentHP < 0) Data.CurrentHP = 0;
		UpdateVisuals();

		// JUICE: Trigger hit sparks!
		if (dmg > 0) SpawnHitParticles();

		if (Data.CurrentHP <= 0)
		{
			// JUICE: Squash down flat before popping out of existence!
			Tween deathTween = CreateTween();
			Vector3 baseScale = _sprite.HasMeta("BaseScale") ? _sprite.GetMeta("BaseScale").AsVector3() : _sprite.Scale;
			
			// Squash wide and flat
			deathTween.TweenProperty(_sprite, "scale", new Vector3(baseScale.X * 1.5f, baseScale.Y * 0.2f, baseScale.Z), 0.15f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			
			// Shrink into nothingness
			deathTween.TweenProperty(this, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.15f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			
			await ToSignal(deathTween, Tween.SignalName.Finished);
			
			// JUICE: Trigger the massive death poof exactly as the unit vanishes
			SpawnDeathParticles();
			
			Visible = false; // Hide completely so they don't block the camera

			if (attacker != null && !this.IsFriendly)
			{
				await GameManager.Instance.RollForLoot();
				await attacker.GainXP(this.Data.XPReward);
			}

			OnDied?.Invoke(this);
			QueueFree();
		}
	}

	public async Task GainXP(int amount)
	{
		if (!IsFriendly) return;
		Data.CurrentXP += amount;
		Tween xpTween = CreateTween();
		xpTween.TweenProperty(_xpBar, "value", (double)Data.CurrentXP, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		await ToSignal(GetTree().CreateTimer(0.6f), "timeout");

		if (Data.CurrentXP >= Data.MaxXP)
		{
			Data.Level++;
			Data.CurrentXP -= Data.MaxXP;
			Data.MaxXP = Mathf.RoundToInt(Data.MaxXP * 1.5f);

			Label3D lvlUp = new Label3D { Text = "LEVEL UP!", Modulate = new Color(1, 0.8f, 0), FontSize = 80, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, OutlineModulate = new Color(0,0,0), OutlineSize = 10, Position = GlobalPosition + new Vector3(0, 3f, 0) };
			GetParent().AddChild(lvlUp);

			Tween lvlTween = CreateTween();
			lvlTween.TweenProperty(lvlUp, "position:y", lvlUp.Position.Y + 1.5f, 1.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			lvlTween.Parallel().TweenProperty(lvlUp, "modulate:a", 0f, 1.5f).SetEase(Tween.EaseType.In);
			lvlTween.Finished += () => lvlUp.QueueFree();

			_xpBar.MaxValue = Data.MaxXP;
			_xpBar.Value = 0;
			UpdateVisuals();

			await GameManager.Instance.ShowLevelUpScreen(this);
			await GainXP(0);
		}
	}

	public async Task MoveAlongPath(List<Vector3> path)
	{
		HasMoved = true;
		UpdateVisuals();

		foreach (Vector3 stepPos in path)
		{
			// === THE FIX: Turn to face the next tile BEFORE hopping! ===
			await FaceDirection(stepPos);
			
			Tween moveTween = CreateTween();
			// Fast horizontal movement to the next tile
			moveTween.Parallel().TweenProperty(this, "position:x", stepPos.X, 0.18f);
			moveTween.Parallel().TweenProperty(this, "position:z", stepPos.Z, 0.18f);

			// Board-game style plumbob hop
			Tween hopTween = CreateTween();
			float originalY = Position.Y;
			
			// === THE FIX: Calculate a dynamic hop peak and land on stepPos.Y ===
			// By taking the Max of the start and end heights, we ensure the unit 
			// hops cleanly OVER the lip of a hill instead of clipping through it!
			float hopPeak = Mathf.Max(originalY, stepPos.Y) + 0.8f;

			hopTween.TweenProperty(this, "position:y", hopPeak, 0.09f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			
			// Tween back down to stepPos.Y, NOT originalY!
			hopTween.TweenProperty(this, "position:y", stepPos.Y, 0.09f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

			// Wait for this specific tile hop to finish before starting the next one
			await ToSignal(moveTween, "finished");
		}
	}
	
	public void SetHovered(bool hovered, bool isTargetableEnemy = false)
	{
		if (_isHovered == hovered) return;
		
		// If we hover an enemy we can't attack, just record the hover state and exit immediately.
		if (hovered && !IsFriendly && !isTargetableEnemy)
		{
			_isHovered = hovered;
			return;
		}
		
		_isHovered = hovered;

		if (_hoverTween != null && _hoverTween.IsValid()) _hoverTween.Kill();
		_hoverTween = CreateTween();

		Vector3 baseScale = _sprite.HasMeta("BaseScale") ? _sprite.GetMeta("BaseScale").AsVector3() : _sprite.Scale;
		
		// Fetch the natural standing position
		Vector3 basePos = _sprite.HasMeta("BasePos") ? _sprite.GetMeta("BasePos").AsVector3() : _sprite.Position; 

		if (hovered)
		{
			if (IsFriendly)
			{
				_hoverTween.TweenProperty(_sprite, "scale", new Vector3(baseScale.X * 1.05f, baseScale.Y * 1.15f, baseScale.Z * 1.05f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				
				// Bounce relative to BasePos, not 0!
				_hoverTween.Parallel().TweenProperty(_sprite, "position:y", basePos.Y + 0.15f, 0.15f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			}
			else if (isTargetableEnemy)
			{
				_hoverTween.TweenProperty(_sprite, "scale", new Vector3(baseScale.X * 1.25f, baseScale.Y * 0.9f, baseScale.Z * 1.25f), 0.1f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
				_hoverTween.TweenProperty(_sprite, "rotation_degrees:z", 8f, 0.05f);
				_hoverTween.TweenProperty(_sprite, "rotation_degrees:z", -8f, 0.05f);
				_hoverTween.TweenProperty(_sprite, "rotation_degrees:z", 0f, 0.05f);
			}
		}
		else
		{
			_hoverTween.TweenProperty(_sprite, "scale", baseScale, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			
			// Return to BasePos, NOT 0f!
			_hoverTween.Parallel().TweenProperty(_sprite, "position:y", basePos.Y, 0.2f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
			_hoverTween.Parallel().TweenProperty(_sprite, "rotation_degrees:z", 0f, 0.1f);
		}
	}
	
	// === NEW: Game Juicy Flip Animation ===
// === NEW: Game Juicy Flip Animation (Isometric Screen-Space Fixed) ===
	public async Task FaceDirection(Vector3 targetGlobalPos)
	{
		if (Data.Profile.DefaultFacing == UnitFacing.Center) return;

		// 1. Get the active camera
		Camera3D cam = GetViewport().GetCamera3D();
		if (cam == null) return;

		// 2. Get the vector pointing from the unit to the target
		Vector3 moveDir = targetGlobalPos - GlobalPosition;

		// 3. THE MAGIC: Project that movement against the Camera's visual "Right" axis.
		// Positive = Screen Right. Negative = Screen Left. Zero = Straight Up or Down!
		float screenRightDiff = moveDir.Dot(cam.GlobalTransform.Basis.X);

		// If the horizontal screen difference is tiny, they are moving purely straight up or down!
		if (Mathf.Abs(screenRightDiff) < 0.1f) return; 

		UnitFacing desiredFacing = screenRightDiff > 0 ? UnitFacing.Right : UnitFacing.Left;

		if (CurrentFacing != desiredFacing)
		{
			CurrentFacing = desiredFacing;

			Vector3 basePos = _sprite.HasMeta("BasePos") ? _sprite.GetMeta("BasePos").AsVector3() : _sprite.Position;
			Vector3 baseScale = _sprite.HasMeta("BaseScale") ? _sprite.GetMeta("BaseScale").AsVector3() : _sprite.Scale;

			Tween flipTween = CreateTween();
			
			// Hop up and squash horizontally to an invisible sliver
			flipTween.Parallel().TweenProperty(_sprite, "position:y", basePos.Y + 0.6f, 0.12f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			flipTween.Parallel().TweenProperty(_sprite, "scale:x", 0.01f, 0.12f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
			
			await ToSignal(flipTween, Tween.SignalName.Finished);

			// While squashed to 0.01 width, flip the sprite!
			bool isNativeFlipped = (CurrentFacing != Data.Profile.DefaultFacing);
			_sprite.FlipH = isNativeFlipped;
			
			// Also flip the child outline if it exists
			if (_sprite.GetChildCount() > 0 && _sprite.GetChild(0) is Sprite3D outline)
			{
				outline.FlipH = isNativeFlipped;
			}

			Tween landTween = CreateTween();
			
			// Slam back down and pop back to full width
			landTween.Parallel().TweenProperty(_sprite, "position:y", basePos.Y, 0.15f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
			landTween.Parallel().TweenProperty(_sprite, "scale:x", baseScale.X, 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

			await ToSignal(landTween, Tween.SignalName.Finished);
		}
	}
	
	private void SpawnHitParticles()
	{
		CpuParticles3D particles = new CpuParticles3D {
			Emitting = false,
			OneShot = true,
			Explosiveness = 0.9f, // All particles burst at once
			Amount = 12,
			Lifetime = 0.5f,
			Position = new Vector3(0, 1.2f, 0) // Centered on the sprite's chest/head
		};

		// Make them look like little cartoony 2D impact sparks
		particles.Mesh = new BoxMesh { Size = new Vector3(0.15f, 0.15f, 0.15f) };
		particles.MaterialOverride = new StandardMaterial3D {
			AlbedoColor = new Color(1f, 0.9f, 0.2f), // Flashy yellow
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, // No shadows!
			NoDepthTest = true // Always draw on top
		};

		// Physics: Explode upwards and outwards
		particles.Direction = new Vector3(0, 1, 0);
		particles.Spread = 90f;
		particles.InitialVelocityMin = 4f;
		particles.InitialVelocityMax = 8f;
		particles.Gravity = new Vector3(0, -15f, 0); // Heavy gravity snaps them down

		// Shrink to zero over their lifetime
		Curve scaleCurve = new Curve();
		scaleCurve.AddPoint(new Vector2(0, 1));
		scaleCurve.AddPoint(new Vector2(1, 0));
		particles.ScaleAmountCurve = scaleCurve;

		AddChild(particles);
		particles.Emitting = true;

		// Clean up the node after the animation finishes
		GetTree().CreateTimer(0.6f).Timeout += () => particles.QueueFree();
	}

	private void SpawnDeathParticles()
	{
		CpuParticles3D particles = new CpuParticles3D {
			Emitting = false,
			OneShot = true,
			Explosiveness = 1.0f,
			Amount = 25,
			Lifetime = 1.0f
		};

		// Soft, cartoony poof clouds
		particles.Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f };
		particles.MaterialOverride = new StandardMaterial3D {
			AlbedoColor = IsFriendly ? new Color(0.2f, 0.5f, 1f) : new Color(0.8f, 0.2f, 0.2f), // Blue for friends, Red for enemies
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};

		// Physics: 360-degree burst
		particles.Direction = new Vector3(0, 1, 0);
		particles.Spread = 180f; 
		particles.InitialVelocityMin = 5f;
		particles.InitialVelocityMax = 10f;
		particles.Gravity = new Vector3(0, -12f, 0); 

		Curve scaleCurve = new Curve();
		scaleCurve.AddPoint(new Vector2(0, 1));
		scaleCurve.AddPoint(new Vector2(0.7f, 0.8f)); // Hold shape...
		scaleCurve.AddPoint(new Vector2(1, 0));       // ...then shrink fast at the end
		particles.ScaleAmountCurve = scaleCurve;

		// CRITICAL: We parent this to the WORLD (GameManager), not the Unit!
		// Otherwise, when the Unit is destroyed, the particles will instantly vanish.
		GetParent().AddChild(particles);
		particles.GlobalPosition = this.GlobalPosition + new Vector3(0, 1f, 0);
		
		particles.Emitting = true;
		GetTree().CreateTimer(1.2f).Timeout += () => particles.QueueFree();
	}

	public void SetSelected(bool selected) { IsSelected = selected; UpdateVisuals(); }
	public void NewTurn() { HasMoved = false; HasAttacked = false; IsSelected = false; UpdateVisuals(); }
	public void SetTargetable(bool isTargetable) { if (_targetIcon != null) _targetIcon.Visible = isTargetable; }
}
