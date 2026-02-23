// GameManager.cs
using Godot;
using Godot.Collections;
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
	private Godot.Collections.Dictionary<Vector2I, Tile> _grid = new();
	private const float TileSize = 2f;
	private const int GridWidth = 10;
	private const int GridDepth = 10;

	// Game State
	private enum State { PlayerTurn, EnemyTurn, SelectingAttackTarget }
	private State _currentState = State.PlayerTurn;

	private List<Unit> _units = new List<Unit>();
	private Unit _selectedUnit;

	private const float MaxMoveDistance = 5f;
	private const float AttackRange = 3.5f;
	
	// === DIALOGIC INTEGRATION ===
	private Node _dialogic;
	private bool _dialogueActive = false;
	private bool _hasPlayedFirstTurnDialogue = false;

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
		ActionMenu.Visible = true;
		
		// === DIALOGIC SETUP ===
		_dialogic = GetNodeOrNull<Node>("/root/Dialogic");
		if (_dialogic == null)
		{
			GD.PrintErr("❌ Dialogic autoload NOT found! Restart Godot completely and re-enable the plugin.");
			return;
		}
		GD.Print("✅ Dialogic autoload found.");

		_dialogic.Connect("timeline_ended", new Callable(this, MethodName.OnTimelineEnded));

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
		if (_dialogueActive || _currentState == State.EnemyTurn)
			return;

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
		else if (collider.GetParent() is Tile tile)
		{
			if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved)
			{
				TryMoveTo(tile.GlobalPosition);
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

	private async void StartEnemyTurn()
	{
		_currentState = State.EnemyTurn;
		ActionMenu.Visible = false;
		DeselectUnit();
		
		if (StatsLabel != null) 
			StatsLabel.Text = "Enemy Turn...";

		await ToSignal(GetTree().CreateTimer(1.0f), "timeout");

		// Enemy Phase
		foreach (var u in _units.Where(x => IsInstanceValid(x) && !x.IsFriendly))
		{
			u.NewTurn();
		}

		// Reset player units
		foreach (var u in _units.Where(x => IsInstanceValid(x) && x.IsFriendly))
		{
			u.NewTurn();
		}

		_currentState = State.PlayerTurn;
		ActionMenu.Visible = true;
		if (StatsLabel != null) StatsLabel.Text = "Your Turn";
	}
	
	private void OnEndTurnPressed()
	{
		// If the dialogue hasn't been played yet, play it and return
		if (!_hasPlayedFirstTurnDialogue)
		{
			_hasPlayedFirstTurnDialogue = true;
			StartDialogue("res://dialogic_timelines/PostFirstBattle.dtl");
			return;
		}

		// Otherwise, proceed to the enemy turn as normal
		StartEnemyTurn();
	}

	// === DIALOGIC METHODS ===
	public void StartDialogue(string timelinePath)
	{
		if (_dialogueActive || _dialogic == null) return;
		
		GD.Print($"🎙 Starting dialogue: {timelinePath}");
		
		_dialogueActive = true;
		ActionMenu.Visible = false;
		DeselectUnit();
		
		// REMOVED: GetTree().Paused = true; 
		// (Your input logic already protects against clicking during dialogue)
		
		StatsLabel.Text = "Dialogue...";
		
		_dialogic.Call("start", timelinePath);
		GD.Print("✅ Dialogic.start called");
	}

	private void OnTimelineEnded()
	{
		GD.Print("✅ Dialogue finished — timeline_ended signal received");
		
		_dialogueActive = false;
		// REMOVED: GetTree().Paused = false;
		ActionMenu.Visible = true;
		UpdateStatsUI();
		
		// Transition straight into the Enemy Turn since they clicked "End Turn" to trigger this
		if (_currentState == State.PlayerTurn)
		{
			StartEnemyTurn();
		}
	}
}
