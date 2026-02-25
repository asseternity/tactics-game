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
		
		UpdateVisuals();
	}

	public void UpdateVisuals()
	{
		if (_sprite == null || Data == null) return;

		if (!_isPreviewing)
		{
			_hpLabel.Text = $"{Data.CurrentHP}/{Data.MaxHP}";   
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

		if (Data.CurrentHP <= 0)
		{
			if (attacker != null && !this.IsFriendly)
				await attacker.GainXP(this.Data.XPReward);

			OnDied?.Invoke(this);

			Tween deathTween = CreateTween();
			deathTween.TweenProperty(this, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			deathTween.Finished += () => QueueFree();
		}
	}

	// (rest of file unchanged: GainXP, MoveTo, SetSelected, NewTurn, SetTargetable, etc.)
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

	public void SetSelected(bool selected) { IsSelected = selected; UpdateVisuals(); }
	public void NewTurn() { HasMoved = false; HasAttacked = false; IsSelected = false; UpdateVisuals(); }
	public void SetTargetable(bool isTargetable) { if (_targetIcon != null) _targetIcon.Visible = isTargetable; }
}
