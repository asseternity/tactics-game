// Unit.cs
using Godot;

public partial class Unit : Node3D
{
	[Export] public int MaxHP = 10;
	[Export] public int AttackDamage = 3;
	[Export] public int AttackRange = 1;
	[Export] public bool IsFriendly = true;
	
	public event System.Action<Unit> OnDied;

	public string UnitName { get; private set; }
	public int CurrentHP;
	public bool HasMoved = false;
	public bool HasAttacked = false;
	public bool IsSelected { get; private set; } = false;

	private Sprite3D _sprite;
	private Label3D _statsLabel;
	private Label3D _targetIcon;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite3D>("Sprite3D");
		_statsLabel = GetNode<Label3D>("Label3D");
		
		// === TARGET ICON SETUP ===
		_targetIcon = new Label3D();
		_targetIcon.Text = "▼";
		_targetIcon.FontSize = 120;
		_targetIcon.Modulate = new Color(1, 0.2f, 0.2f); // Bright red
		_targetIcon.OutlineModulate = new Color(0, 0, 0);
		_targetIcon.OutlineSize = 10;
		_targetIcon.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		_targetIcon.NoDepthTest = true;
		_targetIcon.Position = new Vector3(0, 2.5f, 0); // Float above their head
		_targetIcon.Visible = false;
		AddChild(_targetIcon);

		// Make it bob up and down forever
		Tween bobTween = CreateTween().SetLoops();
		bobTween.TweenProperty(_targetIcon, "position:y", 2.8f, 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		bobTween.TweenProperty(_targetIcon, "position:y", 2.5f, 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		// =========================
		
		CurrentHP = MaxHP;
		UpdateVisuals();
	}
	
	public void Setup(UnitProfile profile, bool isFriendly)
	{
		UnitName = profile.Name;
		MaxHP = profile.MaxHP;
		CurrentHP = MaxHP;
		AttackDamage = profile.AttackDamage;
		AttackRange = profile.AttackRange;
		IsFriendly = isFriendly;

		// Load the texture dynamically
		Texture2D tex = GD.Load<Texture2D>(profile.SpritePath);
		
		if (tex != null)
		{
			_sprite.Texture = tex;

			// === NORMALIZE SPRITE HEIGHT ===
			// 1. Define how tall you want ALL units to be in 3D space (e.g., 1.8 Godot units)
			float targetHeight = 1.8f; 
			
			// 2. Calculate its natural 3D height based on pixel height and the Sprite3D's PixelSize
			float naturalHeight = tex.GetHeight() * _sprite.PixelSize;
			
			// 3. Find the exact multiplier needed to make the natural height equal the target height
			float scaleFactor = targetHeight / naturalHeight;
			
			// 4. Apply the scale uniformly so it doesn't stretch weirdly
			_sprite.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		}
		else
		{
			GD.PrintErr($"❌ Failed to load sprite: {profile.SpritePath}");
		}

		UpdateVisuals();
	}

	public void SetSelected(bool selected)
	{
		IsSelected = selected;
		UpdateVisuals();
	}

	public void UpdateVisuals()
	{
		if (_sprite == null) return;

		_statsLabel.Text = $"{CurrentHP}/{MaxHP}";

		// Label color (team)
		_statsLabel.Modulate = IsFriendly 
			? new Color(0.2f, 1.0f, 0.2f) 
			: new Color(1.0f, 0.3f, 0.3f);

		// Sprite color + highlight
		Color spriteColor;

		if (IsSelected)
		{
			// Gold/yellow highlight for currently selected unit
			spriteColor = new Color(1.0f, 0.95f, 0.4f, 1.0f);
		}
		else if (IsFriendly && HasMoved && HasAttacked)
		{
			// Dimmed when exhausted (only friendlies)
			spriteColor = new Color(0.85f, 0.95f, 0.85f, 0.55f);
		}
		else if (IsFriendly)
		{
			spriteColor = new Color(0.9f, 1.0f, 0.9f, 1.0f);
		}
		else
		{
			spriteColor = new Color(1.0f, 0.72f, 0.72f, 1.0f); // Slight red tint for enemies
		}

		_sprite.Modulate = spriteColor;
	}

	public void NewTurn()
	{
		HasMoved = false;
		HasAttacked = false;
		IsSelected = false; // Remove highlight on turn end
		UpdateVisuals();
	}
	
	public void MoveTo(Vector3 targetPos)
	{
		HasMoved = true;
		UpdateVisuals();

		// === GAME JUICE: THE MOVEMENT HOP ===
		
		// Tween 1: Slide the actual unit base across the floor (0.3 seconds total)
		Tween moveTween = CreateTween();
		moveTween.TweenProperty(this, "position", targetPos, 0.3f);

		// Tween 2: Make the Sprite physically hop up and down
		Tween hopTween = CreateTween();
		
		float originalY = _sprite.Position.Y;
		float hopHeight = originalY + 1.2f; // Adjust this to make them jump higher/lower

		// Jump up (Quad Out makes it slow down at the peak of the jump)
		hopTween.TweenProperty(_sprite, "position:y", hopHeight, 0.15f)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.Out);
				
		// Fall down (Quad In makes it accelerate as it hits the ground)
		hopTween.TweenProperty(_sprite, "position:y", originalY, 0.15f)
				.SetTrans(Tween.TransitionType.Quad)
				.SetEase(Tween.EaseType.In);
	}

	public void TakeDamage(int dmg)
	{
		CurrentHP -= dmg;
		if (CurrentHP < 0) CurrentHP = 0;
		UpdateVisuals();
		
		if (CurrentHP <= 0)
		{
			OnDied?.Invoke(this); // <-- ADD THIS to announce the death!
			
			Tween deathTween = CreateTween();
			deathTween.TweenProperty(this, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.25f)
					  .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			deathTween.Finished += () => QueueFree();
		}
	}
	
	public void SetTargetable(bool isTargetable)
	{
		if (_targetIcon != null) _targetIcon.Visible = isTargetable;
	}
}
