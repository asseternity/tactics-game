// Unit.cs
using Godot;
using System.Threading.Tasks;

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
	
	// === SUBVIEWPORT UI ===
	private SubViewport _uiViewport;
	private Sprite3D _uiSprite;
	private ProgressBar _hpBar;
	private Label _hpLabel;
	private ProgressBar _xpBar;
	private Label _xpLabel;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite3D>("Sprite3D");
		
		// 1. Target Icon (Moved slightly higher to clear the new UI)
		_targetIcon = new Label3D { Text = "▼", FontSize = 120, Modulate = new Color(1, 0.2f, 0.2f), OutlineModulate = new Color(0, 0, 0), OutlineSize = 10, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = new Vector3(0, 2.9f, 0), Visible = false, RenderPriority = 20 };
		AddChild(_targetIcon);

		Tween bobTween = CreateTween().SetLoops();
		bobTween.TweenProperty(_targetIcon, "position:y", 3.2f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		bobTween.TweenProperty(_targetIcon, "position:y", 2.9f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		// === 2. BUILD THE 2D UI IN CODE ===
		_uiViewport = new SubViewport { TransparentBg = true, Size = new Vector2I(200, 100), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		AddChild(_uiViewport);

		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		vbox.AddThemeConstantOverride("separation", 6); // Gap between HP and XP
		_uiViewport.AddChild(vbox);

		// HP BAR
		_hpBar = new ProgressBar { CustomMinimumSize = new Vector2(180, 35), ShowPercentage = false, Step = 0.1 };
		_hpBar.AddThemeStyleboxOverride("background", CreateRoundedStyle(new Color(0.1f, 0.1f, 0.1f, 0.9f)));
		_hpBar.AddThemeStyleboxOverride("fill", CreateRoundedStyle(new Color(0.2f, 0.9f, 0.2f, 1f)));
		
		_hpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_hpLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_hpLabel.AddThemeFontSizeOverride("font_size", 22);
		_hpLabel.AddThemeConstantOverride("outline_size", 6);
		_hpBar.AddChild(_hpLabel);
		vbox.AddChild(_hpBar);

		// XP BAR
		_xpBar = new ProgressBar { CustomMinimumSize = new Vector2(180, 25), ShowPercentage = false, Step = 0.1 };
		_xpBar.AddThemeStyleboxOverride("background", CreateRoundedStyle(new Color(0.1f, 0.1f, 0.1f, 0.9f)));
		_xpBar.AddThemeStyleboxOverride("fill", CreateRoundedStyle(new Color(1f, 0.8f, 0f, 1f)));

		_xpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_xpLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_xpLabel.AddThemeFontSizeOverride("font_size", 16);
		_xpLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f));
		_xpLabel.AddThemeConstantOverride("outline_size", 5);
		_xpBar.AddChild(_xpLabel);
		vbox.AddChild(_xpBar);

		// === 3. PROJECT THE UI INTO 3D ===
		_uiSprite = new Sprite3D();
		_uiSprite.Texture = _uiViewport.GetTexture();
		_uiSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		_uiSprite.NoDepthTest = true;
		_uiSprite.RenderPriority = 10;
		_uiSprite.PixelSize = 0.008f; 
		_uiSprite.Position = new Vector3(0, 2.1f, 0); // Perfectly centered over the unit's origin!
		AddChild(_uiSprite);
		
		if (HasNode("Label3D")) GetNode("Label3D").QueueFree(); // Cleanup old prototype text
	}

	// Helper to make gorgeous rounded borders dynamically!
	private StyleBoxFlat CreateRoundedStyle(Color color)
	{
		return new StyleBoxFlat
		{
			BgColor = color,
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
			BorderWidthBottom = 3, BorderWidthTop = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
			BorderColor = new Color(0, 0, 0, 1f)
		};
	}
	
	public void Setup(PersistentUnit data, bool isFriendly)
	{
		Data = data;
		IsFriendly = isFriendly;

		_hpBar.MaxValue = Data.MaxHP;
		_hpBar.Value = Data.CurrentHP;

		if (IsFriendly)
		{
			_xpBar.Visible = true;
			_xpBar.MaxValue = Data.MaxXP;
			_xpBar.Value = Data.CurrentXP;
		}
		else
		{
			_xpBar.Visible = false;
			// Enemies get red HP bars
			_hpBar.AddThemeStyleboxOverride("fill", CreateRoundedStyle(new Color(0.9f, 0.2f, 0.2f, 1f)));
		}

		Texture2D tex = GD.Load<Texture2D>(data.Profile.SpritePath);
		if (tex != null)
		{
			_sprite.Texture = tex;
			float targetHeight = 1.8f; 
			float scaleFactor = targetHeight / (tex.GetHeight() * _sprite.PixelSize);
			_sprite.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		}

		UpdateVisuals();
	}

	public void UpdateVisuals()
	{
		if (_sprite == null || Data == null) return;

		// 1. Text updates
		_hpLabel.Text = $"HP {Data.CurrentHP}/{Data.MaxHP}";
		_xpLabel.Text = $"Level {Data.Level}";

		// 2. Smoothly animate HP changes
		CreateTween().TweenProperty(_hpBar, "value", (double)Data.CurrentHP, 0.15f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		// 3. Unit Tinting
		Color spriteColor;
		if (IsSelected) spriteColor = new Color(1.0f, 0.95f, 0.4f, 1.0f);
		else if (IsFriendly && HasMoved && HasAttacked) spriteColor = new Color(0.85f, 0.95f, 0.85f, 0.55f);
		else if (IsFriendly) spriteColor = new Color(0.9f, 1.0f, 0.9f, 1.0f);
		else spriteColor = new Color(1.0f, 0.72f, 0.72f, 1.0f); 

		_sprite.Modulate = spriteColor;
	}

	public async Task TakeDamage(int dmg, Unit attacker = null)
	{
		Data.CurrentHP -= dmg;
		if (Data.CurrentHP < 0) Data.CurrentHP = 0;
		UpdateVisuals(); 
		
		if (Data.CurrentHP <= 0)
		{
			if (attacker != null && !this.IsFriendly)
			{
				await attacker.GainXP(60); 
			}

			OnDied?.Invoke(this); 
			
			Tween deathTween = CreateTween();
			deathTween.TweenProperty(this, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			deathTween.Finished += () => QueueFree();
		}
	}

	public async Task GainXP(int amount)
	{
		if (!IsFriendly) return;
		Data.CurrentXP += amount;
		
		// Fill XP Bar Smoothly
		Tween xpTween = CreateTween();
		xpTween.TweenProperty(_xpBar, "value", (double)Data.CurrentXP, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		
		await ToSignal(GetTree().CreateTimer(0.6f), "timeout");

		if (Data.CurrentXP >= Data.MaxXP)
		{
			Data.Level++;
			Data.CurrentXP -= Data.MaxXP;
			Data.MaxXP = Mathf.RoundToInt(Data.MaxXP * 1.5f); 
			
			Data.MaxHP += 5;
			Data.CurrentHP = Data.MaxHP;
			Data.AttackDamage += 2;
			
			Label3D lvlUp = new Label3D { Text = "LEVEL UP!", Modulate = new Color(1, 0.8f, 0), FontSize = 80, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, OutlineModulate = new Color(0,0,0), OutlineSize = 10, Position = GlobalPosition + new Vector3(0, 3f, 0) };
			GetParent().AddChild(lvlUp);
			
			Tween lvlTween = CreateTween();
			lvlTween.TweenProperty(lvlUp, "position:y", lvlUp.Position.Y + 1.5f, 1.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			lvlTween.Parallel().TweenProperty(lvlUp, "modulate:a", 0f, 1.5f).SetEase(Tween.EaseType.In);
			lvlTween.Finished += () => lvlUp.QueueFree();

			// Instant reset for the next level
			_xpBar.MaxValue = Data.MaxXP;
			_xpBar.Value = 0; 
			UpdateVisuals();
			
			await GainXP(0); 
		}
	}

	public void MoveTo(Vector3 targetPos)
	{
		HasMoved = true;
		UpdateVisuals();
		
		Tween moveTween = CreateTween();
		moveTween.Parallel().TweenProperty(this, "position:x", targetPos.X, 0.3f);
		moveTween.Parallel().TweenProperty(this, "position:z", targetPos.Z, 0.3f);

		Tween hopTween = CreateTween();
		float originalY = Position.Y;
		float hopHeight = originalY + 1.2f; 
		hopTween.TweenProperty(this, "position:y", hopHeight, 0.15f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		hopTween.TweenProperty(this, "position:y", originalY, 0.15f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
	}

	public void SetSelected(bool selected) { IsSelected = selected; UpdateVisuals(); }
	public void NewTurn() { HasMoved = false; HasAttacked = false; IsSelected = false; UpdateVisuals(); }
	public void SetTargetable(bool isTargetable) { if (_targetIcon != null) _targetIcon.Visible = isTargetable; }
}
