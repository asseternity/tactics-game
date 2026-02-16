using Godot;

public partial class Unit : Node3D
{
	[Export] public int MaxHP = 10;
	[Export] public int AttackDamage = 3;
	[Export] public bool IsFriendly = true;

	public int CurrentHP;
	public bool HasMoved = false;
	public bool HasAttacked = false;

	private Sprite3D _sprite;
	private Label3D _statsLabel;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite3D>("Sprite3D");
		_statsLabel = GetNode<Label3D>("Label3D");
		CurrentHP = MaxHP;
		
		// Initial visual setup
		UpdateVisuals();
	}

	public void UpdateVisuals()
	{
		if (_sprite == null) return; // Guard against running before Ready

		_statsLabel.Text = $"{CurrentHP}/{MaxHP}";

		// 1. Color Logic (Fixes the "All Green" bug)
		if (IsFriendly)
			_statsLabel.Modulate = new Color(0.2f, 1.0f, 0.2f); // Green
		else
			_statsLabel.Modulate = new Color(1.0f, 0.3f, 0.3f); // Red

		// 2. Turn Status Logic (Brightness)
		// "Exhausted" only if BOTH move and attack are used.
		bool isExhausted = HasMoved && HasAttacked;
		
		// Reset color first
		Color c = new Color(1, 1, 1, 1);
		
		if (!IsFriendly)
		{
			c = new Color(1, 0.5f, 0.5f); // Keep enemies slightly red tinted
		}
		
		if (isExhausted && IsFriendly)
		{
			c.A = 0.5f; // Dim / Dark
			_sprite.Modulate = c;
		}
		else
		{
			c.A = 1.0f; // Bright
			_sprite.Modulate = c;
		}
	}

	public void TakeDamage(int dmg)
	{
		CurrentHP -= dmg;
		if (CurrentHP < 0) CurrentHP = 0;
		UpdateVisuals();

		if (CurrentHP <= 0)
			QueueFree();
	}

	public void NewTurn()
	{
		HasMoved = false;
		HasAttacked = false;
		UpdateVisuals();
	}

	public void MoveTo(Vector3 targetPos)
	{
		var tween = CreateTween();
		tween.TweenProperty(this, "position", targetPos, 0.3f);
		HasMoved = true;
		UpdateVisuals();
	}
}
