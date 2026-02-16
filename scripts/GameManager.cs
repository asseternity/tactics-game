using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node3D
{
	[Export] public PackedScene UnitScene;
	[Export] public Camera3D Cam;
	
	// --- FIXED: Used Godot's ExportGroup instead of Unity's Header ---
	[ExportGroup("UI References")] 
	[Export] public Control ActionMenu;
	[Export] public Button AttackButton;
	[Export] public Button EndTurnButton;
	[Export] public Label StatsLabel; 
	// ----------------------------------------------------------------

	// Game State
	private enum State { PlayerTurn, EnemyTurn, SelectingMoveDest, SelectingAttackTarget }
	private State _currentState = State.PlayerTurn;

	private List<Unit> _units = new List<Unit>();
	private Unit _selectedUnit;
	
	private const int GridSize = 2;

	public override void _Ready()
	{
		// Spawn Friendlies
		SpawnUnit(true, new Vector3(0, 0, 0));
		SpawnUnit(true, new Vector3(2, 0, 0));
		SpawnUnit(true, new Vector3(0, 0, 2));

		// Spawn Enemies
		SpawnUnit(false, new Vector3(4, 0, 4));
		SpawnUnit(false, new Vector3(6, 0, 4));
		SpawnUnit(false, new Vector3(4, 0, 6));

		// UI Setup
		AttackButton.Pressed += OnAttackButtonPressed;
		EndTurnButton.Pressed += OnEndTurnPressed;
		ActionMenu.Visible = false;
		
		// Wait one frame to ensure UI is ready before updating text
		CallDeferred("UpdateStatsUI");
	}

	private void SpawnUnit(bool isFriendly, Vector3 pos)
	{
		Unit u = UnitScene.Instantiate<Unit>();
		AddChild(u);
		u.GlobalPosition = pos;
		
		// Setup Team and Force Visual Update
		u.IsFriendly = isFriendly; 
		u.UpdateVisuals(); 
		
		_units.Add(u);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			HandleClick(mouseEvent.Position);
		}
	}

	private void HandleClick(Vector2 mousePos)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var from = Cam.ProjectRayOrigin(mousePos);
		var to = from + Cam.ProjectRayNormal(mousePos) * 1000;
		
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithAreas = true;
		
		var result = spaceState.IntersectRay(query);

		if (result.Count > 0)
		{
			Node collider = (Node)result["collider"];
			Vector3 hitPos = (Vector3)result["position"];

			switch (_currentState)
			{
				case State.PlayerTurn:
					// Check if we clicked a unit
					if (collider.GetParent() is Unit clickedUnit)
					{
						if (clickedUnit.IsFriendly)
							SelectUnit(clickedUnit);
						else
							ShowUnitInfo(clickedUnit);
					}
					break;

				case State.SelectingMoveDest:
					if (!_selectedUnit.HasMoved)
					{
						Vector3 gridPos = SnapToGrid(hitPos);
						if (IsTileFree(gridPos))
						{
							_selectedUnit.MoveTo(gridPos);
							_currentState = State.PlayerTurn;
							ShowActions(true);
							UpdateStatsUI();
						}
					}
					break;

				case State.SelectingAttackTarget:
					if (collider.GetParent() is Unit target && !target.IsFriendly)
					{
						if (_selectedUnit.GlobalPosition.DistanceTo(target.GlobalPosition) <= GridSize * 1.5f)
						{
							target.TakeDamage(_selectedUnit.AttackDamage);
							_selectedUnit.HasAttacked = true;
							_selectedUnit.UpdateVisuals();
							DeselectUnit();
						}
					}
					break;
			}
		}
	}

	private void SelectUnit(Unit u)
	{
		if (u.HasAttacked && u.HasMoved) return; 

		_selectedUnit = u;
		UpdateStatsUI();

		if (!u.HasMoved)
		{
			_currentState = State.SelectingMoveDest;
			ActionMenu.Visible = false;
		}
		else
		{
			ShowActions(true);
		}
	}
	
	private void ShowUnitInfo(Unit u)
	{
		if (StatsLabel != null)
			StatsLabel.Text = $"Enemy Unit\nHP: {u.CurrentHP}/{u.MaxHP}\nDmg: {u.AttackDamage}";
	}

	private void UpdateStatsUI()
	{
		if (StatsLabel == null) return;

		if (_selectedUnit != null)
		{
			string moveStr = _selectedUnit.HasMoved ? "[USED]" : "[READY]";
			string atkStr = _selectedUnit.HasAttacked ? "[USED]" : "[READY]";
			
			StatsLabel.Text = $"Selected: Friendly\n" +
							  $"HP: {_selectedUnit.CurrentHP}/{_selectedUnit.MaxHP}\n" +
							  $"Move: {moveStr}\n" +
							  $"Attack: {atkStr}";
		}
		else
		{
			StatsLabel.Text = "Select a Unit...";
		}
	}

	private void ShowActions(bool show)
	{
		ActionMenu.Visible = show;
		AttackButton.Disabled = _selectedUnit.HasAttacked;
	}

	private void OnAttackButtonPressed()
	{
		_currentState = State.SelectingAttackTarget;
		ActionMenu.Visible = false;
	}

	private void OnEndTurnPressed()
	{
		StartEnemyTurn();
	}

	private void DeselectUnit()
	{
		_selectedUnit = null;
		_currentState = State.PlayerTurn;
		ActionMenu.Visible = false;
		UpdateStatsUI();
	}

	private async void StartEnemyTurn()
	{
		_currentState = State.EnemyTurn;
		ActionMenu.Visible = false;
		if (StatsLabel != null) StatsLabel.Text = "Enemy Turn...";

		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

		// Run Enemy Turns
		foreach (var u in _units.Where(x => IsInstanceValid(x) && !x.IsFriendly))
		{
			u.NewTurn();
		}

		// Reset Friendlies
		foreach (var u in _units.Where(x => IsInstanceValid(x) && x.IsFriendly))
		{
			u.NewTurn();
		}
		
		_currentState = State.PlayerTurn;
		if (StatsLabel != null) StatsLabel.Text = "Your Turn";
	}

	private Vector3 SnapToGrid(Vector3 rawPos)
	{
		float x = Mathf.Round(rawPos.X / GridSize) * GridSize;
		float z = Mathf.Round(rawPos.Z / GridSize) * GridSize;
		return new Vector3(x, 0, z);
	}

	private bool IsTileFree(Vector3 pos)
	{
		foreach(var u in _units)
		{
			if (IsInstanceValid(u) && u.GlobalPosition.DistanceTo(pos) < 0.1f) 
				return false;
		}
		return true;
	}
}
