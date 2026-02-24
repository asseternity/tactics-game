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
	private const int MaxMoveTiles = 3;
	
	// === DIALOGIC INTEGRATION ===
	private Node _dialogic;
	private bool _dialogueActive = false;
	private bool _hasPlayedFirstTurnDialogue = false;

	public override void _Ready()
	{
		// 1. Fill the Database
		_unitDatabase["Knight"] = new UnitProfile("Knight", "res://assets/knight.png", 15, 4, 1);
		_unitDatabase["Archer"] = new UnitProfile("Archer", "res://assets/archer.png", 8, 5, 2);
		_unitDatabase["Goblin"] = new UnitProfile("Goblin", "res://assets/goblin.png", 10, 3, 1);
		_unitDatabase["Ogre"]   = new UnitProfile("Ogre", "res://assets/ogre.png", 25, 8, 1);

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
		// Start the whole unit practically invisible instead of true zero
		u.Scale = new Vector3(0.001f, 0.001f, 0.001f);
		
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

	private async void TryMoveTo(Vector3 targetPos)
	{
		if (_selectedUnit == null) return;

		if (GetGridDistance(_selectedUnit.GlobalPosition, targetPos) > MaxMoveTiles)
		{
			GD.Print("Movement too far!");
			return;
		}

		if (IsTileFree(targetPos))
		{
			_selectedUnit.MoveTo(targetPos);
			_selectedUnit.HasMoved = true; // Instantly lock out spam-clicking
			
			// Hide the menu and clear old icons while the unit is hopping
			ShowActions(false);
			RefreshTargetIcons();

			// === THE FIX: WAIT FOR THE ANIMATION ===
			// Wait for the 0.3s movement hop to physically finish!
			await ToSignal(GetTree().CreateTimer(0.35f), "timeout");

			// Ensure the player didn't deselect the unit while it was hopping
			if (_selectedUnit != null)
			{
				// Now that the unit is on the new tile, calculate the math!
				CheckAutoExhaust(_selectedUnit);
				_selectedUnit.UpdateVisuals();
				UpdateStatsUI();
				ShowActions(true);
				
				RefreshTargetIcons();

				if (_selectedUnit.HasAttacked)
					DeselectUnit();
			}
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

		if (GetGridDistance(_selectedUnit.GlobalPosition, target.GlobalPosition) <= _selectedUnit.AttackRange)
		{
			// Wait for the animation and damage to apply
			await PerformAttackAsync(_selectedUnit, target);

			// === THE FIX ===
			// If the attack killed the last enemy, the Director triggered dialogue
			// and cleared _selectedUnit while we were waiting. Abort cleanly!
			if (_selectedUnit == null || !IsInstanceValid(_selectedUnit)) return;

			CancelAttackMode();
			UpdateStatsUI();
			ShowActions(true);

			if (_selectedUnit.HasMoved) DeselectUnit();
		}
		else GD.Print("Target out of range!");
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
		RefreshTargetIcons();
	}

	private void DeselectUnit()
	{
		if (_selectedUnit != null)
		{
			_selectedUnit.SetSelected(false);
			_selectedUnit = null;
		}
		UpdateStatsUI();
		RefreshTargetIcons();
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
		if (StatsLabel != null) StatsLabel.Text = "Enemy Turn...";

		ShowTurnAnnouncer("ENEMY TURN", new Color(1.0f, 0.2f, 0.2f));
		await ToSignal(GetTree().CreateTimer(1.5f), "timeout");

		var enemies = _units.Where(x => IsInstanceValid(x) && !x.IsFriendly).ToList();

		foreach (var enemy in enemies)
		{
			if (!IsInstanceValid(enemy)) continue;

			var players = _units.Where(x => IsInstanceValid(x) && x.IsFriendly).ToList();
			if (players.Count == 0) break; // Game over, all friendlies dead

			Unit bestTarget = null;
			Vector3 bestMovePos = enemy.GlobalPosition;
			float bestScore = -9999f; // Start with a terrible score

			// === 1. TACTICAL EVALUATION ===
			// Look at every single player on the board and score them
			foreach (var player in players)
			{
				float targetScore = 0;
				Vector3 optimalTile = enemy.GlobalPosition;
				bool canAttackThisTurn = false;
				int distToPlayer = GetGridDistance(enemy.GlobalPosition, player.GlobalPosition);

				// If we are ALREADY in range, we don't need to move
				if (distToPlayer <= enemy.AttackRange)
				{
					canAttackThisTurn = true;
				}
				else
				{
					// Scan the grid to see if we can reach a tile that puts us in attack range
					int closestWeCanGet = distToPlayer;
					
					foreach (var tilePos in _grid.Keys)
					{
						Vector3 worldPos = new Vector3(tilePos.X * TileSize, 0.01f, tilePos.Y * TileSize);
						if (GetGridDistance(enemy.GlobalPosition, worldPos) <= MaxMoveTiles && IsTileFree(worldPos))
						{
							int distFromTileToPlayer = GetGridDistance(worldPos, player.GlobalPosition);
							
							if (distFromTileToPlayer <= enemy.AttackRange)
							{
								canAttackThisTurn = true;
								optimalTile = worldPos;
								break; // We found a tile that lets us attack! Good enough.
							}
							else if (distFromTileToPlayer < closestWeCanGet)
							{
								// If we can't reach them, find the tile that gets us the closest
								closestWeCanGet = distFromTileToPlayer;
								optimalTile = worldPos;
							}
						}
					}
				}

				// === 2. THE SCORING MATH ===
				// Huge bonus if we can actually hit them this turn
				if (canAttackThisTurn) targetScore += 1000;
				
				// Prioritize targets with lower HP (Subtracting their HP makes lower HP worth more points)
				targetScore -= player.CurrentHP * 10;
				
				// Massive bonus if this attack will kill the target outright!
				if (canAttackThisTurn && player.CurrentHP <= enemy.AttackDamage) targetScore += 2000;
				
				// Tie-breaker: Prefer closer targets so we don't waste time walking across the map
				targetScore -= distToPlayer;

				// Is this the best target we've seen so far?
				if (targetScore > bestScore)
				{
					bestScore = targetScore;
					bestTarget = player;
					bestMovePos = optimalTile;
				}
			}

			// === 3. EXECUTE THE BEST PLAN ===
			
			// Move to the optimal tile
			if (bestMovePos != enemy.GlobalPosition)
			{
				enemy.MoveTo(bestMovePos);
				await ToSignal(GetTree().CreateTimer(0.35f), "timeout"); 
			}

			// Attack if our best target is in range!
			if (bestTarget != null && GetGridDistance(enemy.GlobalPosition, bestTarget.GlobalPosition) <= enemy.AttackRange)
			{
				await PerformAttackAsync(enemy, bestTarget); 
				
				// Abort early if the player won during this attack animation
				if (_currentState != State.EnemyTurn) return;
				
				await ToSignal(GetTree().CreateTimer(0.4f), "timeout"); 
			}
			
			// Exhaust the enemy visually
			enemy.HasMoved = true;
			enemy.HasAttacked = true;
			enemy.UpdateVisuals();
		}

		// Reset friendlies for the new turn
		foreach (var u in _units.Where(x => IsInstanceValid(x) && x.IsFriendly)) u.NewTurn();
		
		ShowTurnAnnouncer("YOUR TURN", new Color(0.2f, 0.8f, 1.0f));

		_currentState = State.PlayerTurn;
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
					if (GetGridDistance(_selectedUnit.GlobalPosition, _hoveredTile.GlobalPosition) <= MaxMoveTiles && IsTileFree(_hoveredTile.GlobalPosition))
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
		ShowTurnAnnouncer("YOUR TURN", new Color(0.2f, 0.8f, 1.0f));
		ShowActions(true);
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
				shrinkTween.TweenProperty(u, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.2f)
						   .SetTrans(Tween.TransitionType.Back)
						   .SetEase(Tween.EaseType.In);
						   
				shrinkTween.Finished += () => u.QueueFree();
			}
		}
		_units.Clear();

		// Wait for the shrink animations to finish before continuing the code
		await ToSignal(GetTree().CreateTimer(0.25f), "timeout");
	}
	
	private async Task PerformAttackAsync(Unit attacker, Unit target)
	{
		Vector3 startPos = attacker.GlobalPosition;
		Vector3 attackDirection = (target.GlobalPosition - startPos).Normalized();
		Vector3 lungePos = startPos + (attackDirection * 0.8f);

		Tween tween = CreateTween();
		tween.TweenProperty(attacker, "global_position", lungePos, 0.1f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(attacker, "global_position", startPos, 0.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);

		await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
		
		// Damage & Visuals
		target.TakeDamage(attacker.AttackDamage);
		SpawnFloatingDamage(target.GlobalPosition, attacker.AttackDamage);

		if (target.GetNodeOrNull<Sprite3D>("Sprite3D") is Sprite3D targetSprite)
		{
			Tween flashTween = CreateTween();
			flashTween.TweenProperty(targetSprite, "modulate", new Color(5, 0.5f, 0.5f), 0.05f);
			flashTween.TweenProperty(targetSprite, "modulate", new Color(1, 1, 1), 0.1f);
		}

		attacker.HasAttacked = true;
		attacker.UpdateVisuals();
	}
	
	private void CheckAutoExhaust(Unit u)
	{
		if (!u.IsFriendly || u.HasAttacked) return;

		// Is there ANY enemy within this specific unit's attack range?
		bool enemyInRange = _units.Any(enemy => !enemy.IsFriendly && IsInstanceValid(enemy) && GetGridDistance(u.GlobalPosition, enemy.GlobalPosition) <= u.AttackRange);

		// If no enemies to hit, automatically use up their attack action so they dim
		if (!enemyInRange)
		{
			u.HasAttacked = true;
			u.UpdateVisuals();
		}
	}
	
	private void ShowTurnAnnouncer(string text, Color color)
	{
		Label announcer = new Label();
		announcer.Text = text;
		
		// Style the text to be massive and punchy
		announcer.AddThemeFontSizeOverride("font_size", 100);
		announcer.AddThemeColorOverride("font_color", color);
		announcer.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		announcer.AddThemeConstantOverride("outline_size", 20);
		
		// Perfectly center the text inside the bounds
		announcer.HorizontalAlignment = HorizontalAlignment.Center;
		announcer.VerticalAlignment = VerticalAlignment.Center;
		
		// Make the label's bounds fill the entire screen
		announcer.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		// Attach it to the UI CanvasLayer (using DimOverlay's parent so it draws over 3D)
		if (DimOverlay != null) 
			DimOverlay.GetParent().AddChild(announcer);
		else 
			AddChild(announcer);

		// Set the pivot to the dead center of the screen so it scales properly
		Vector2 screenSize = GetViewport().GetVisibleRect().Size;
		announcer.PivotOffset = screenSize / 2;

		// Start invisible and tiny
		announcer.Scale = Vector2.Zero;
		announcer.Modulate = new Color(1, 1, 1, 0);

		Tween tween = CreateTween();
		
		// 1. Pop In (Scale up and fade in at the same time)
		tween.Parallel().TweenProperty(announcer, "scale", Vector2.One, 0.4f)
			 .SetTrans(Tween.TransitionType.Back)
			 .SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 1.0f, 0.3f);
		
		// 2. Hold on screen for 1 second
		tween.Chain().TweenInterval(1.0f);
		
		// 3. Zoom out and fade away
		tween.Chain().TweenProperty(announcer, "scale", new Vector2(1.5f, 1.5f), 0.3f)
			 .SetTrans(Tween.TransitionType.Cubic)
			 .SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 0.0f, 0.3f);
		
		// Clean up!
		tween.Finished += () => announcer.QueueFree();
	}
	
	private int GetGridDistance(Vector3 posA, Vector3 posB)
	{
		int ax = Mathf.RoundToInt(posA.X / TileSize);
		int az = Mathf.RoundToInt(posA.Z / TileSize);
		int bx = Mathf.RoundToInt(posB.X / TileSize);
		int bz = Mathf.RoundToInt(posB.Z / TileSize);

		// Chebyshev Distance: Diagonals count as exactly 1 tile
		return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(az - bz));
	}
	
	private void RefreshTargetIcons()
	{
		foreach (var u in _units)
		{
			if (!IsInstanceValid(u)) continue;
			u.SetTargetable(false); // Default to off

			// If it's our turn, we have a unit selected, and that unit hasn't attacked yet
			if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasAttacked)
			{
				// If the unit is an enemy AND within our exact attack range, turn the icon on!
				if (!u.IsFriendly && GetGridDistance(_selectedUnit.GlobalPosition, u.GlobalPosition) <= _selectedUnit.AttackRange)
				{
					u.SetTargetable(true);
				}
			}
		}
	}
}

// === GAME DATA STRUCTURES ===

public struct UnitProfile
{
	public string Name;
	public string SpritePath;
	public int MaxHP;
	public int AttackDamage;
	public int AttackRange; 

	public UnitProfile(string name, string spritePath, int maxHp, int attackDmg, int attackRange)
	{
		Name = name; SpritePath = spritePath; MaxHP = maxHp; 
		AttackDamage = attackDmg; AttackRange = attackRange;
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
