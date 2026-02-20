// Unit.cs
using Godot;

public partial class Unit : Node3D
{
	[Export] public int MaxHP = 10;
	[Export] public int AttackDamage = 3;
	[Export] public bool IsFriendly = true;

	public int CurrentHP;
	public bool HasMoved = false;
	public bool HasAttacked = false;
	public bool IsSelected { get; private set; } = false;

	private Sprite3D _sprite;
	private Label3D _statsLabel;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite3D>("Sprite3D");
		_statsLabel = GetNode<Label3D>("Label3D");
		CurrentHP = MaxHP;
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
		IsSelected = false; // Remove highlight on turn end
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
