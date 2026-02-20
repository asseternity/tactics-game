// GameManager.cs
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class GameManager : Node3D
{
	[Export] public PackedScene UnitScene;
	[Export] public PackedScene TileScene;
	[Export] public Camera3D Cam;
	
	[ExportGroup("UI References")]
	[Export] public Control ActionMenu;
	[Export] public Button AttackButton;
	[Export] public Button EndTurnButton;
	[Export] public Label StatsLabel;
	
	// Tile Settings
	private Dictionary<Vector2I, Tile> _grid = new();
	private const float TileSize = 2f;
	private const int GridWidth = 10;   // adjust to cover your whole play area
	private const int GridDepth = 10;

	// Game State
	private enum State { PlayerTurn, EnemyTurn, SelectingAttackTarget }
	private State _currentState = State.PlayerTurn;

	private List<Unit> _units = new List<Unit>();
	private Unit _selectedUnit;

	private const int GridSize = 2;
	private const float MaxMoveDistance = 5f;     // ~2 grid tiles (adjust as needed)
	private const float AttackRange = 3.5f;       // ~1.5 grid tiles

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
		
		// Generate Grid
		GenerateGrid();

		// UI Setup
		AttackButton.Pressed += OnAttackButtonPressed;
		EndTurnButton.Pressed += OnEndTurnPressed;
		ActionMenu.Visible = false;

		CallDeferred("UpdateStatsUI");
	}

	private void SpawnUnit(bool isFriendly, Vector3 pos)
	{
		Unit u = UnitScene.Instantiate<Unit>();
		AddChild(u);
		u.GlobalPosition = pos;
		u.IsFriendly = isFriendly;
		u.UpdateVisuals();
		_units.Add(u);
	}
	
	private void GenerateGrid()
	{
		for (int x = 0; x < GridWidth; x++)
		{
			for (int z = 0; z < GridDepth; z++)
			{
				Tile tile = TileScene.Instantiate<Tile>();
				AddChild(tile);
				tile.Setup(new Vector2I(x, z), TileSize);
				_grid[new Vector2I(x, z)] = tile;
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_currentState == State.EnemyTurn) return;

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

		if (result.Count == 0) 
		{
			if (_selectedUnit != null && _currentState != State.SelectingAttackTarget)
				DeselectUnit();
			return;
		}

		Node collider = (Node)result["collider"];
		Vector3 hitPos = (Vector3)result["position"];

		if (collider.GetParent() is Unit clickedUnit)
		{
			if (clickedUnit.IsFriendly)
			{
				SelectUnit(clickedUnit);
			}
			else 
			{
				if (_currentState == State.SelectingAttackTarget)
					TryAttackTarget(clickedUnit);
				else
					ShowUnitInfo(clickedUnit);
			}
		}
		else // Clicked ground
		{
			if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved)
			{
				// New: we hit a Tile directly → position is already perfectly snapped!
				if (collider.GetParent() is Tile tile)
				{
					TryMoveTo(tile.GlobalPosition);
				}
			}
		}
	}

	private void TryMoveTo(Vector3 targetPos)
	{
		if (_selectedUnit == null) return;

		float distance = _selectedUnit.GlobalPosition.DistanceTo(targetPos);
		if (distance > MaxMoveDistance)
		{
			GD.Print("Movement too far!");
			return;
		}

		if (IsTileFree(targetPos))
		{
			_selectedUnit.MoveTo(targetPos);
			_selectedUnit.HasMoved = true;
			_selectedUnit.UpdateVisuals();
			UpdateStatsUI();
			ShowActions(true);

			if (_selectedUnit.HasAttacked)
				CallDeferred("DeselectUnit");
		}
	}

	private bool IsTileFree(Vector3 pos)
	{
		// Much faster and exact now
		foreach (var u in _units)
		{
			if (IsInstanceValid(u) && u.GlobalPosition.DistanceTo(pos) < 0.1f)
				return false;
		}
		return true;
	}

	private void TryAttackTarget(Unit target)
	{
		if (_selectedUnit == null) return;

		float distance = _selectedUnit.GlobalPosition.DistanceTo(target.GlobalPosition);
		
		if (distance <= AttackRange)
		{
			target.TakeDamage(_selectedUnit.AttackDamage);
			_selectedUnit.HasAttacked = true;
			_selectedUnit.UpdateVisuals();

			CancelAttackMode();
			UpdateStatsUI();
			ShowActions(true);

			if (_selectedUnit.HasMoved)
				CallDeferred("DeselectUnit");
		}
		else
		{
			GD.Print("Target out of range!");
		}
	}

	private void SelectUnit(Unit u)
	{
		if (u.HasMoved && u.HasAttacked) 
		{
			ShowUnitInfo(u);
			return;
		}

		if (_selectedUnit != null && _selectedUnit != u)
			_selectedUnit.SetSelected(false);

		_selectedUnit = u;
		_selectedUnit.SetSelected(true);

		UpdateStatsUI();
		ShowActions(true);
		_currentState = State.PlayerTurn;
	}

	private void DeselectUnit()
	{
		if (_selectedUnit != null)
		{
			_selectedUnit.SetSelected(false);
			_selectedUnit = null;
		}
		UpdateStatsUI();
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
		if (show && _selectedUnit != null)
		{
			AttackButton.Text = "Attack";
			AttackButton.Disabled = _selectedUnit.HasAttacked;
		}
	}

	private void OnAttackButtonPressed()
	{
		if (_currentState == State.SelectingAttackTarget)
		{
			CancelAttackMode();
		}
		else if (_selectedUnit != null && !_selectedUnit.HasAttacked)
		{
			EnterAttackMode();
		}
	}

	private void EnterAttackMode()
	{
		_currentState = State.SelectingAttackTarget;
		AttackButton.Text = "Cancel";
	}

	private void CancelAttackMode()
	{
		_currentState = State.PlayerTurn;
		AttackButton.Text = "Attack";
		ShowActions(true);
	}

	private void OnEndTurnPressed()
	{
		StartEnemyTurn();
	}

	private async void StartEnemyTurn()
	{
		_currentState = State.EnemyTurn;
		ActionMenu.Visible = false;
		DeselectUnit();
		
		if (StatsLabel != null) 
			StatsLabel.Text = "Enemy Turn...";

		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

		// Enemy Phase - TODO: Add real AI here (move/attack toward player units)
		foreach (var u in _units.Where(x => IsInstanceValid(x) && !x.IsFriendly))
		{
			u.NewTurn();
		}

		// Reset player units for new turn
		foreach (var u in _units.Where(x => IsInstanceValid(x) && x.IsFriendly))
		{
			u.NewTurn();
		}

		_currentState = State.PlayerTurn;
		ActionMenu.Visible = true;
		if (StatsLabel != null) StatsLabel.Text = "Your Turn";
	}

	private Vector3 SnapToGrid(Vector3 rawPos)
	{
		float x = Mathf.Round(rawPos.X / GridSize) * GridSize;
		float z = Mathf.Round(rawPos.Z / GridSize) * GridSize;
		return new Vector3(x, 0, z);
	}
}
