// GameManager.cs
using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
	[Export] public ColorRect DimOverlay;
	[Export] public Texture2D AttackCursorIcon;
	
	// UI Utils
	private Tween _uiTween;
	
	// Tile Settings
	private Godot.Collections.Dictionary<Vector2I, Tile> _grid = new();
	private const float TileSize = 2f;
	private const int GridWidth = 10;
	private const int GridDepth = 10;
	private Tile _hoveredTile;
	
	// === STORY SYSTEM ===
	private System.Collections.Generic.Dictionary<string, UnitProfile> _unitDatabase = new();
	private List<ScriptEvent> _mainScript = new();
	private int _currentScriptIndex = -1;

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
		// 1. Fill the Database
		_unitDatabase["Knight"] = new UnitProfile("Knight", "res://assets/knight.png", 15, 4);
		_unitDatabase["Archer"] = new UnitProfile("Archer", "res://assets/archer.png", 8, 5);
		_unitDatabase["Goblin"] = new UnitProfile("Goblin", "res://assets/goblin.png", 10, 3);
		_unitDatabase["Ogre"]   = new UnitProfile("Ogre", "res://assets/ogre.png", 25, 8);

		// 2. Write the Script! (Look how clean this is)
		_mainScript = new List<ScriptEvent>
		{
			ScriptEvent.Dialogue("res://dialogic_timelines/Intro.dtl"),
			
			ScriptEvent.Battle(new BattleSetup 
			{
				Friendlies = { new UnitSpawn("Knight", new Vector3(0,0,0)), new UnitSpawn("Archer", new Vector3(2,0,0)) },
				Enemies = { new UnitSpawn("Goblin", new Vector3(4,0,4)) }
			}),
			
			ScriptEvent.Dialogue("res://dialogic_timelines/PostFirstBattle.dtl"),
			
			ScriptEvent.Battle(new BattleSetup 
			{
				Friendlies = { new UnitSpawn("Knight", new Vector3(0,0,0)), new UnitSpawn("Archer", new Vector3(2,0,0)) },
				Enemies = { new UnitSpawn("Ogre", new Vector3(6,0,6)), new UnitSpawn("Goblin", new Vector3(4,0,4)) }
			})
		};

		// 3. Setup standard game stuff
		GenerateGrid();
		AttackButton.Pressed += OnAttackButtonPressed;
		EndTurnButton.Pressed += StartEnemyTurn; // End turn now ONLY ends the turn!
		ActionMenu.Visible = false;

		_dialogic = GetNodeOrNull<Node>("/root/Dialogic");
		_dialogic.Connect("timeline_ended", new Callable(this, MethodName.OnTimelineEnded));
		CallDeferred("UpdateStatsUI");

		// 4. Action! Start the game
		AdvanceScript();
	}

	private void SpawnUnit(UnitProfile profile, bool isFriendly, Vector3 pos)
	{
		Unit u = UnitScene.Instantiate<Unit>();
		AddChild(u);
		u.GlobalPosition = pos;
		u.Setup(profile, isFriendly);
		
		u.OnDied += HandleUnitDeath; 
		
		_units.Add(u);

		// === GAME JUICE: SPAWN POP-IN ===
		// Start the whole unit invisible/tiny
		u.Scale = Vector3.Zero; 
		
		Tween spawnTween = CreateTween();
		
		// Pop them up to full size with a bouncy rubber-band effect!
		spawnTween.TweenProperty(u, "scale", Vector3.One, 0.35f)
				  .SetTrans(Tween.TransitionType.Bounce)
				  .SetEase(Tween.EaseType.Out);
		// ================================
	}
	
	private void HandleUnitDeath(Unit deadUnit)
	{
		_units.Remove(deadUnit); // Remove from our active roster
		
		// Check if any enemies are left
		bool enemiesAlive = _units.Any(u => !u.IsFriendly && IsInstanceValid(u));
		
		if (!enemiesAlive)
		{
			GD.Print("🏆 Battle Won!");
			AdvanceScript(); // Trigger the next event in the script!
		}
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

		// Detect mouse movement for hovering
		if (@event is InputEventMouseMotion mouseMotion)
		{
			HandleHover(mouseMotion.Position);
		}
		// Detect mouse clicks (your existing code)
		else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
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

	private async void TryAttackTarget(Unit target)
	{
		if (_selectedUnit == null) return;

		float distance = _selectedUnit.GlobalPosition.DistanceTo(target.GlobalPosition);
		
		if (distance <= AttackRange)
		{
			// === GAME JUICE: LUNGE ANIMATION ===
			Vector3 startPos = _selectedUnit.GlobalPosition;
			
			// Find the direction to the target and normalize it (make its length exactly 1)
			Vector3 attackDirection = (target.GlobalPosition - startPos).Normalized();
			
			// Calculate a point slightly in front of the attacker (e.g., 0.8 units forward)
			Vector3 lungePos = startPos + (attackDirection * 0.8f);

			Tween tween = CreateTween();
			
			// Lunge forward fast (0.1 seconds)
			tween.TweenProperty(_selectedUnit, "global_position", lungePos, 0.1f)
				 .SetTrans(Tween.TransitionType.Sine)
				 .SetEase(Tween.EaseType.Out);
				 
			// Bounce back a little slower (0.2 seconds)
			tween.TweenProperty(_selectedUnit, "global_position", startPos, 0.2f)
				 .SetTrans(Tween.TransitionType.Quad)
				 .SetEase(Tween.EaseType.InOut);

			// Optional: Wait exactly the length of the forward lunge (0.1s) before dealing damage 
			// so it feels like the hit physically connects!
			await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
			// ===================================
			
			// Apply the damage
			target.TakeDamage(_selectedUnit.AttackDamage);
			
			if (target.GetNodeOrNull<Sprite3D>("Sprite3D") is Sprite3D targetSprite)
			{
				Tween flashTween = CreateTween();
				// Blast it to bright red/white (RGB values > 1 create a glow effect!)
				flashTween.TweenProperty(targetSprite, "modulate", new Color(5, 0.5f, 0.5f), 0.05f);
				// Snap it back to normal
				flashTween.TweenProperty(targetSprite, "modulate", new Color(1, 1, 1), 0.1f);
			}
			
			// === GAME JUICE: FLOATING DAMAGE ===
			SpawnFloatingDamage(target.GlobalPosition, _selectedUnit.AttackDamage);
			// ===================================

			_selectedUnit.HasAttacked = true;
			_selectedUnit.UpdateVisuals();

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
		// === DYNAMIC SQUASH & STRETCH ===
		// Grab the sprite dynamically
		Node3D sprite = _selectedUnit.GetNode<Node3D>("Sprite3D");

		// The first time we select this unit, save its current scale as metadata
		if (!sprite.HasMeta("BaseScale"))
		{
			sprite.SetMeta("BaseScale", sprite.Scale);
		}

		// Read that base scale back
		Vector3 baseScale = sprite.GetMeta("BaseScale").AsVector3();

		// Calculate the stretch dynamically relative to the base scale
		Vector3 stretchedScale = new Vector3(
			baseScale.X * 0.8f,
			baseScale.Y * 1.3f,
			baseScale.Z * 0.8f
		);

		// Animate it!
		Tween tween = CreateTween();
		tween.TweenProperty(sprite, "scale", stretchedScale, 0.1f);
		tween.TweenProperty(sprite, "scale", baseScale, 0.2f).SetTrans(Tween.TransitionType.Bounce);
		// ================================
		ShowUnitInfo(u);

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
		ShowActions(false); // <-- ADD THIS to pop the menu out!
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
		// 1. Update the button text/state first
		if (show && _selectedUnit != null)
		{
			AttackButton.Text = "Attack";
			AttackButton.Disabled = _selectedUnit.HasAttacked;
		}

		// 2. Kill any active UI tween so they don't fight
		if (_uiTween != null && _uiTween.IsValid())
		{
			_uiTween.Kill();
		}

		_uiTween = CreateTween();
		ActionMenu.PivotOffset = ActionMenu.Size / 2;

		if (show)
		{
			// === THE FIX ===
			// If it was completely hidden, crush the scale down BEFORE making it visible
			if (!ActionMenu.Visible)
			{
				ActionMenu.Scale = new Vector2(0.01f, 0.01f);
			}
			
			ActionMenu.Visible = true;

			// Pop IN
			_uiTween.TweenProperty(ActionMenu, "scale", Vector2.One, 0.2f)
				   .SetTrans(Tween.TransitionType.Back)
				   .SetEase(Tween.EaseType.Out);
		}
		else
		{
			// Pop OUT
			_uiTween.TweenProperty(ActionMenu, "scale", new Vector2(0.01f, 0.01f), 0.15f)
				   .SetTrans(Tween.TransitionType.Back)
				   .SetEase(Tween.EaseType.In);

			// ONLY hide it completely after the shrink animation finishes
			_uiTween.Finished += () => ActionMenu.Visible = false;
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
		if (AttackCursorIcon != null)
		{
			Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16));
		}
	}

	private void CancelAttackMode()
	{
		_currentState = State.PlayerTurn;
		AttackButton.Text = "Attack";
		ShowActions(true);
		Input.SetCustomMouseCursor(null);
	}

	private async void StartEnemyTurn()
	{
		_currentState = State.EnemyTurn;
		ShowActions(false);
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
		ShowActions(true);
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
		ShowActions(false);
		DeselectUnit();
		if (DimOverlay != null) DimOverlay.Visible = true;
		
		StatsLabel.Text = "Dialogue...";
		
		_dialogic.Call("start", timelinePath);
		GD.Print("✅ Dialogic.start called");
	}

	private void OnTimelineEnded()
	{
		_dialogueActive = false;
		if (DimOverlay != null) DimOverlay.Visible = false;
		
		AdvanceScript(); // When dialogue finishes, move to the next script event!
	}
	
	private void HandleHover(Vector2 mousePos)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var from = Cam.ProjectRayOrigin(mousePos);
		var to = from + Cam.ProjectRayNormal(mousePos) * 1000;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithAreas = true;

		var result = spaceState.IntersectRay(query);

		if (result.Count == 0)
		{
			ClearHover();
			return;
		}

		Node collider = (Node)result["collider"];
		Tile targetTile = null;

		if (collider.GetParent() is Tile tile)
		{
			targetTile = tile;
		}
		else if (collider.GetParent() is Unit unit)
		{
			Vector2I gridPos = new Vector2I(
				Mathf.RoundToInt(unit.GlobalPosition.X / TileSize),
				Mathf.RoundToInt(unit.GlobalPosition.Z / TileSize)
			);
			
			if (_grid.TryGetValue(gridPos, out Tile underlyingTile))
			{
				targetTile = underlyingTile;
			}
		}

		// If we are looking at a new tile, evaluate if it can be highlighted
		if (targetTile != _hoveredTile)
		{
			ClearHover();
			_hoveredTile = targetTile;
			
			if (_hoveredTile != null)
			{
				// === NEW LOGIC: Check if it's a valid move ===
				bool isValidMove = false;

				// Only allow movement highlights during the Player Turn when a unit is ready to move
				if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved)
				{
					float distance = _selectedUnit.GlobalPosition.DistanceTo(_hoveredTile.GlobalPosition);
					
					// Check distance AND if the tile is free of other units
					if (distance <= MaxMoveDistance && IsTileFree(_hoveredTile.GlobalPosition))
					{
						isValidMove = true;
					}
				}

				// Only light up if the move is actually legal!
				if (isValidMove)
				{
					_hoveredTile.SetHighlight(true);
				}
			}
		}
	}

	private void ClearHover()
	{
		if (_hoveredTile != null)
		{
			_hoveredTile.SetHighlight(false);
			_hoveredTile = null;
		}
	}
	
	private void SpawnFloatingDamage(Vector3 targetPosition, int damageAmount)
	{
		// Create the 3D text node dynamically
		Label3D damageLabel = new Label3D();
		
		// Style the text
		damageLabel.Text = damageAmount.ToString();
		damageLabel.PixelSize = 0.02f; // Scale of the text
		damageLabel.FontSize = 30; // High resolution font
		damageLabel.Modulate = new Color(1, 0.2f, 0.2f); // Bright red
		damageLabel.OutlineModulate = new Color(0, 0, 0); // Black outline
		damageLabel.OutlineSize = 6;
		
		// Make it always face the camera and draw on top of everything
		damageLabel.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
		damageLabel.NoDepthTest = true; 

		// Add it to the world BEFORE setting its position
		AddChild(damageLabel);
		
		// Start it slightly above the enemy's head
		damageLabel.GlobalPosition = targetPosition + new Vector3(0, 1.5f, 0);

		// === ANIMATE IT ===
		Tween tween = CreateTween();
		
		// Float it up by 1.5 units over 0.8 seconds
		Vector3 floatUpPosition = damageLabel.GlobalPosition + new Vector3(0, 1.5f, 0);
		tween.TweenProperty(damageLabel, "global_position", floatUpPosition, 0.8f)
			 .SetTrans(Tween.TransitionType.Cubic)
			 .SetEase(Tween.EaseType.Out);

		// At the same time (Parallel), fade its alpha (transparency) to 0
		tween.Parallel().TweenProperty(damageLabel, "modulate:a", 0.0f, 0.8f)
			 .SetTrans(Tween.TransitionType.Cubic)
			 .SetEase(Tween.EaseType.In);

		// When the animation finishes, delete the label so we don't leak memory
		tween.Finished += () => damageLabel.QueueFree();
	}
	
	private void AdvanceScript()
	{
		_currentScriptIndex++;
		
		if (_currentScriptIndex >= _mainScript.Count)
		{
			GD.Print("🎉 GAME OVER! You won!");
			StatsLabel.Text = "YOU WIN!";
			return;
		}

		ScriptEvent currentEvent = _mainScript[_currentScriptIndex];

		if (currentEvent.Type == EventType.Dialogue)
		{
			StartDialogue(currentEvent.TimelinePath);
		}
		else if (currentEvent.Type == EventType.Battle)
		{
			StartBattle(currentEvent.BattleData);
		}
	}

	private async void StartBattle(BattleSetup data)
	{
		GD.Print("⚔️ Starting Battle!");
		StatsLabel.Text = "Setting up the board...";
		
		// 1. Wait for the old pieces to pop off the board
		await ClearBoardAsync();

		// 2. A tiny dramatic pause while the board is empty
		await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

		// 3. Spawn the new pieces!
		foreach (var f in data.Friendlies) SpawnUnit(_unitDatabase[f.ProfileId], true, f.Position);
		foreach (var e in data.Enemies) SpawnUnit(_unitDatabase[e.ProfileId], false, e.Position);

		_currentState = State.PlayerTurn;
		StatsLabel.Text = "Battle Start! Your Turn.";
	}

	private async Task ClearBoardAsync()
	{
		DeselectUnit();
		
		// If the board is already empty, just return immediately
		if (_units.Count == 0) return;

		// Tween every unit on the board down to scale 0
		foreach (var u in _units)
		{
			if (IsInstanceValid(u))
			{
				Tween shrinkTween = CreateTween();
				shrinkTween.TweenProperty(u, "scale", Vector3.Zero, 0.2f)
						   .SetTrans(Tween.TransitionType.Back)
						   .SetEase(Tween.EaseType.In);
						   
				shrinkTween.Finished += () => u.QueueFree();
			}
		}
		_units.Clear();

		// Wait for the shrink animations to finish before continuing the code
		await ToSignal(GetTree().CreateTimer(0.25f), "timeout");
	}
}

// === GAME DATA STRUCTURES ===

public struct UnitProfile
{
	public string Name;
	public string SpritePath;
	public int MaxHP;
	public int AttackDamage;

	public UnitProfile(string name, string spritePath, int maxHp, int attackDmg)
	{
		Name = name; SpritePath = spritePath; MaxHP = maxHp; AttackDamage = attackDmg;
	}
}

public struct UnitSpawn
{
	public string ProfileId;
	public Vector3 Position;
	public UnitSpawn(string profileId, Vector3 position)
	{
		ProfileId = profileId; Position = position;
	}
}

public class BattleSetup
{
	public List<UnitSpawn> Friendlies = new();
	public List<UnitSpawn> Enemies = new();
}

public enum EventType { Dialogue, Battle }

public class ScriptEvent
{
	public EventType Type;
	public string TimelinePath;
	public BattleSetup BattleData;

	// Helper methods to make writing the script incredibly clean
	public static ScriptEvent Dialogue(string path) => new ScriptEvent { Type = EventType.Dialogue, TimelinePath = path };
	public static ScriptEvent Battle(BattleSetup battle) => new ScriptEvent { Type = EventType.Battle, BattleData = battle };
}
