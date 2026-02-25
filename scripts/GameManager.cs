// GameManager.cs
using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager : Node3D
{
	// === SINGLETON ACCESS ===
	public static GameManager Instance { get; private set; }
	public Theme MasterTheme { get; private set; }
	public StyleBoxFlat BaseUIStyle { get; private set; }
	public StyleBoxFlat BadgeStyle { get; private set; } // <-- NEW: For the NPC Nameplate

	[Export] public PackedScene UnitScene;
	[Export] public PackedScene TileScene;
	[Export] public Camera3D Cam;
	
	[ExportGroup("UI References")]
	[Export] public Control ActionMenu;
	[Export] public Button AttackButton;
	[Export] public Button EndTurnButton;
	[Export] public RichTextLabel StatsLabel; // <-- UPGRADED!
	[Export] public ColorRect DimOverlay;
	[Export] public Texture2D AttackCursorIcon;
	
	private Tween _uiTween;
	private List<Tile> _movementHighlightTiles = new();
	private Unit _previewedEnemy;
	
	private Godot.Collections.Dictionary<Vector2I, Tile> _grid = new();
	private const float TileSize = 2f;
	private const int GridWidth = 10;
	private const int GridDepth = 10;
	private Tile _hoveredTile;
	
	private System.Collections.Generic.Dictionary<string, UnitProfile> _unitDatabase = new();
	private List<PersistentUnit> _party = new(); 
	private System.Collections.Generic.Dictionary<string, List<ScriptEvent>> _scriptDatabase = new();
	private string _currentSection = "Intro"; // Always start here!
	private int _currentScriptIndex = -1;
	private string _pendingSection = ""; // Holds the choice Dialogic sends us

	private enum State { PlayerTurn, EnemyTurn, SelectingAttackTarget, Cutscene }
	private State _currentState = State.Cutscene; // Start in cutscene mode

	private List<Unit> _units = new List<Unit>();
	private Unit _selectedUnit;
	private List<Node3D> _obstacles = new List<Node3D>();
	
	private Node _dialogic;
	private bool _dialogueActive = false;
	private bool _levelUpActive = false; // Block inputs during Level Up
	
	// === MID-BATTLE EVENT TRACKING ===
	private int _currentTurnNumber = 1;
	private List<MidBattleEvent> _activeMidBattleEvents = new();
	private bool _isMidBattleDialogue = false;

public override void _Ready()
	{
		Instance = this; 
		
		// === NEW: Initialize our gorgeous UI ===
		CallDeferred(MethodName.SetupUnifiedUI);

		// Add XP Rewards (the final number) to all units!
		_unitDatabase["Knight"] = new UnitProfile("Knight", "res://assets/knight.png", 25, 15, 1, 3, 0);
		_unitDatabase["Archer"] = new UnitProfile("Archer", "res://assets/archer.png", 18, 18, 2, 3, 0);
		_unitDatabase["Goblin"] = new UnitProfile("Goblin", "res://assets/goblin.png", 10, 3, 1, 3, 140);
		_unitDatabase["Ogre"]   = new UnitProfile("Ogre", "res://assets/ogre.png", 25, 8, 1, 2, 120);

		// === UPDATED SCRIPT INIT ===
		_scriptDatabase = GameScript.GetMainScript();

		GenerateGrid();
		AttackButton.Pressed += OnAttackButtonPressed;
		EndTurnButton.Pressed += OnEndTurnPressed; 
		ActionMenu.Visible = false;

		_dialogic = GetNodeOrNull<Node>("/root/Dialogic");
		_dialogic.Connect("timeline_ended", new Callable(this, MethodName.OnTimelineEnded));
		
		// === NEW: Listen for Dialogic branching signals! ===
		_dialogic.Connect("signal_event", new Callable(this, MethodName.OnDialogicSignal));

		CallDeferred("UpdateStatsUI");
		AdvanceScript();
	}

	// === RTS CAMERA LOGIC ===
	public override void _Process(double delta)
	{
		if (_dialogueActive || _levelUpActive) return;

		Vector2 mousePos = GetViewport().GetMousePosition();
		Vector2 screenSize = GetViewport().GetVisibleRect().Size;
		Vector2 moveDir = Vector2.Zero;
		float margin = 20f; 

		if (mousePos.X < margin) moveDir.X -= 1;
		if (mousePos.X > screenSize.X - margin) moveDir.X += 1;
		if (mousePos.Y < margin) moveDir.Y += 1; 
		if (mousePos.Y > screenSize.Y - margin) moveDir.Y -= 1; 

		if (moveDir != Vector2.Zero)
		{
			moveDir = moveDir.Normalized();
			Vector3 forward = -Cam.GlobalTransform.Basis.Z; forward.Y = 0; forward = forward.Normalized();
			Vector3 right = Cam.GlobalTransform.Basis.X; right.Y = 0; right = right.Normalized();
			Vector3 finalMove = (right * moveDir.X) + (forward * moveDir.Y);

			Vector3 newPos = Cam.GlobalPosition + (finalMove * 15f * (float)delta);
			
			// === BUG 3 FIX: DYNAMIC & GENEROUS BOUNDARIES ===
			// Give the camera exactly 15 Godot-units of "padding" on all 4 sides of whatever your grid size is
			float pad = 15f; 
			newPos.X = Mathf.Clamp(newPos.X, -pad, (GridWidth * TileSize) + pad);
			newPos.Z = Mathf.Clamp(newPos.Z, -pad, (GridDepth * TileSize) + pad);

			Cam.GlobalPosition = newPos;
		}
	}

	// === DYNAMIC LEVEL UP UI ===
	public async Task ShowLevelUpScreen(Unit unit)
	{
		_levelUpActive = true;
		ShowActions(false); // Hide the action menu so you can't accidentally end the turn!
		if (DimOverlay != null) DimOverlay.Visible = true;

		TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

		Control uiRoot = new Control();
		uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		
		// === THE FIX: ATTACH TO CANVAS LAYER ===
		// This puts the menu explicitly ON TOP of the DimOverlay so it is clickable!
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(uiRoot);
		else AddChild(uiRoot);

		// === THE FIX: PERFECT CENTERING ===
		CenterContainer center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		uiRoot.AddChild(center);

		PanelContainer panel = new PanelContainer();
		StyleBoxFlat panelStyle = new StyleBoxFlat {
			BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f),
			CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15,
			CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15,
			BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4,
			BorderColor = new Color(1f, 0.8f, 0f, 1f),
			ContentMarginBottom = 30, ContentMarginTop = 30, ContentMarginLeft = 40, ContentMarginRight = 40
		};
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(panel); // Add to the center container!

		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.AddThemeConstantOverride("separation", 15);
		panel.AddChild(vbox);

		Label title = new Label { Text = $"{unit.Data.Profile.Name} Reached Level {unit.Data.Level}!", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 30);
		vbox.AddChild(title);

		List<string> options = new List<string> { "FullHeal", "MaxHP", "Movement", "AttackDamage" };
		System.Random rnd = new System.Random();
		options = options.OrderBy(x => rnd.Next()).Take(3).ToList();

		foreach (string opt in options)
		{
			Button btn = new Button { CustomMinimumSize = new Vector2(300, 60) };
			btn.AddThemeFontSizeOverride("font_size", 20);

			if (opt == "FullHeal") btn.Text = "Fully Restore HP";
			else if (opt == "MaxHP") btn.Text = "+5 Max HP";
			else if (opt == "Movement") btn.Text = "+1 Movement Range";
			else if (opt == "AttackDamage") btn.Text = "+2 Attack Damage";

			btn.Pressed += () => 
			{
				if (opt == "FullHeal") unit.Data.CurrentHP = unit.Data.MaxHP;
				else if (opt == "MaxHP") { unit.Data.MaxHP += 5; unit.Data.CurrentHP += 5; }
				else if (opt == "Movement") unit.Data.Movement += 1;
				else if (opt == "AttackDamage") unit.Data.AttackDamage += 2;

				unit.UpdateVisuals();

				Tween outTween = CreateTween();
				outTween.TweenProperty(panel, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
				outTween.Finished += () => 
				{
					uiRoot.QueueFree();
					if (DimOverlay != null && !_dialogueActive) DimOverlay.Visible = false;
					_levelUpActive = false;
					
					// Re-show the action menu if it's still the player's turn
					if (_currentState == State.PlayerTurn && _selectedUnit != null) ShowActions(true);
					
					tcs.SetResult(true);
				};
			};
			vbox.AddChild(btn);
		}

		// Wait exactly 1 frame for Godot to calculate the sizes of the buttons/text
		await ToSignal(GetTree(), "process_frame");
		
		// Now set the pivot to the true center and animate!
		panel.PivotOffset = panel.Size / 2;
		panel.Scale = Vector2.Zero;
		CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		await tcs.Task; 
	}

	private async void StartBattle(BattleSetup data)
	{
		GD.Print("⚔️ Starting Battle!");
		StatsLabel.Text = "Setting up the board...";
		await ClearBoardAsync();
		await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

		foreach (var hero in _party) hero.HealBetweenBattles();

		for (int i = 0; i < Mathf.Min(_party.Count, data.FriendlySpawns.Count); i++)
		{
			SpawnUnit(_party[i], true, data.FriendlySpawns[i]);
		}

		foreach (var e in data.Enemies) 
		{
			SpawnUnit(new PersistentUnit(_unitDatabase[e.ProfileId]), false, e.Position);
		}
		
		System.Random rnd = new System.Random();
		SpawnRandomObstacles(rnd.Next(8, 16));

		_currentTurnNumber = 1;
		_activeMidBattleEvents = new List<MidBattleEvent>(data.MidBattleEvents);
		CheckMidBattleEvents();
	}

	private void SpawnUnit(PersistentUnit data, bool isFriendly, Vector3 pos)
	{
		Unit u = UnitScene.Instantiate<Unit>();
		AddChild(u);
		u.GlobalPosition = pos;
		
		u.Setup(data, isFriendly);
		u.OnDied += HandleUnitDeath; 
		_units.Add(u);

		u.Scale = new Vector3(0.001f, 0.001f, 0.001f);
		Tween spawnTween = CreateTween();
		spawnTween.TweenProperty(u, "scale", Vector3.One, 0.35f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
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

		// === FIXED ORDER: Floating damage FIRST, then damage + possible death ===
		int damage = GD.RandRange(attacker.GetMinDamage(), attacker.GetMaxDamage());
		SpawnFloatingDamage(target.GlobalPosition, damage);

		await target.TakeDamage(damage, attacker);

		if (target.GetNodeOrNull<Sprite3D>("Sprite3D") is Sprite3D targetSprite)
		{
			Tween flashTween = CreateTween();
			flashTween.TweenProperty(targetSprite, "modulate", new Color(5, 0.5f, 0.5f), 0.05f);
			flashTween.TweenProperty(targetSprite, "modulate", new Color(1, 1, 1), 0.1f);
		}

		attacker.HasAttacked = true;
		attacker.UpdateVisuals();
		RefreshTargetIcons();
	}
	
	private async void HandleUnitDeath(Unit deadUnit)
	{
		_units.Remove(deadUnit); 

		bool enemiesAlive = _units.Any(u => IsInstanceValid(u) && !u.IsFriendly);
		if (!enemiesAlive)
		{
			// === BUG 1 FIX: LOCK AND SCRUB THE BOARD ===
			_currentState = State.Cutscene; 
			ClearMovementRange();
			ClearHover();
			DeselectUnit();
			ShowActions(false);
			// ==========================================

			GD.Print("🏆 Battle Won!");
			
			// === THE FIX: REDUCED DELAY ===
			// Shrunk from 1.8f to 0.4f to eliminate dead time while still
			// allowing the 0.25s death shrink animation to finish.
			await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
			AdvanceScript(); 
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
			if (_selectedUnit != null && _currentState != State.SelectingAttackTarget) DeselectUnit();
			return;
		}

		Node collider = (Node)result["collider"];

		if (collider.GetParent() is Unit clickedUnit)
		{
			if (clickedUnit.IsFriendly) SelectUnit(clickedUnit);
			else 
			{
				bool canSmartAttack = _currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasAttacked && GetGridDistance(_selectedUnit.GlobalPosition, clickedUnit.GlobalPosition) <= _selectedUnit.Data.AttackRange;

				if (_currentState == State.SelectingAttackTarget || canSmartAttack)
				{
					ClearAttackPreview(); 
					TryAttackTarget(clickedUnit);
				}
				else ShowUnitInfo(clickedUnit);
			}
		}
		else if (collider.GetParent() is Tile tile)
		{
			if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved) TryMoveTo(tile.GlobalPosition);
		}
	}

	private Vector2I GetGridPos(Vector3 pos)
	{
		return new Vector2I(Mathf.RoundToInt(pos.X / TileSize), Mathf.RoundToInt(pos.Z / TileSize));
	}

	private bool IsTileFree(Vector2I gridPos)
	{
		foreach (var u in _units)
		{
			if (IsInstanceValid(u) && GetGridPos(u.GlobalPosition) == gridPos) return false;
		}
		
		foreach (var obs in _obstacles)
		{
			if (IsInstanceValid(obs) && GetGridPos(obs.GlobalPosition) == gridPos) return false;
		}
		
		return true;
	}

	private async void TryAttackTarget(Unit target)
	{
		if (_selectedUnit == null) return;

		if (GetGridDistance(_selectedUnit.GlobalPosition, target.GlobalPosition) <= _selectedUnit.Data.AttackRange)
		{
			await PerformAttackAsync(_selectedUnit, target);
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

		if (_selectedUnit != null && _selectedUnit != u) _selectedUnit.SetSelected(false);

		_selectedUnit = u;
		_selectedUnit.SetSelected(true);
		
		Node3D sprite = _selectedUnit.GetNode<Node3D>("Sprite3D");
		if (!sprite.HasMeta("BaseScale")) sprite.SetMeta("BaseScale", sprite.Scale);
		Vector3 baseScale = sprite.GetMeta("BaseScale").AsVector3();
		Vector3 stretchedScale = new Vector3(baseScale.X * 0.8f, baseScale.Y * 1.3f, baseScale.Z * 0.8f);

		Tween tween = CreateTween();
		tween.TweenProperty(sprite, "scale", stretchedScale, 0.1f);
		tween.TweenProperty(sprite, "scale", baseScale, 0.2f).SetTrans(Tween.TransitionType.Bounce);
		
		ShowUnitInfo(u);
		UpdateStatsUI();
		ShowActions(true);
		_currentState = State.PlayerTurn;
		RefreshTargetIcons();
		ShowMovementRange(_selectedUnit);
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
		ClearMovementRange();
	}

	private void ShowUnitInfo(Unit u)
	{
		if (StatsLabel != null && u.Data != null)
			StatsLabel.Text = $"Enemy Unit\nLv.{u.Data.Level} HP: {u.Data.CurrentHP}/{u.Data.MaxHP}\nDmg: {u.Data.AttackDamage}";
	}

	private void ShowActions(bool show)
	{
		if (show && _selectedUnit != null)
		{
			AttackButton.Text = "Attack";
			AttackButton.Disabled = _selectedUnit.HasAttacked;
		}

		if (_uiTween != null && _uiTween.IsValid()) _uiTween.Kill();

		_uiTween = CreateTween();
		ActionMenu.PivotOffset = ActionMenu.Size / 2;

		if (show)
		{
			if (!ActionMenu.Visible) ActionMenu.Scale = new Vector2(0.01f, 0.01f);
			ActionMenu.Visible = true;
			_uiTween.TweenProperty(ActionMenu, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
		else
		{
			_uiTween.TweenProperty(ActionMenu, "scale", new Vector2(0.01f, 0.01f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			_uiTween.Finished += () => ActionMenu.Visible = false;
		}
	}

	private void OnAttackButtonPressed()
	{
		if (_currentState == State.SelectingAttackTarget) CancelAttackMode();
		else if (_selectedUnit != null && !_selectedUnit.HasAttacked) EnterAttackMode();
	}

	private void EnterAttackMode()
	{
		_currentState = State.SelectingAttackTarget;
		AttackButton.Text = "Cancel";
		if (AttackCursorIcon != null) Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16));
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
		ClearMovementRange();
		RefreshTargetIcons();
		if (StatsLabel != null) StatsLabel.Text = "Enemy Turn...";

		ShowTurnAnnouncer("ENEMY TURN", new Color(1.0f, 0.2f, 0.2f));
		await ToSignal(GetTree().CreateTimer(1.5f), "timeout");

		var enemies = _units.Where(x => IsInstanceValid(x) && !x.IsFriendly).ToList();

foreach (var enemy in enemies)
		{
			if (!IsInstanceValid(enemy)) continue;

			var players = _units.Where(x => IsInstanceValid(x) && x.IsFriendly).ToList();
			if (players.Count == 0) break; 

			Unit bestTarget = null;
			Vector3 bestMovePos = enemy.GlobalPosition;
			float bestScore = -9999f; 

			// === NEW AI PATHING PREP ===
			Vector2I enemyCoords = new Vector2I(Mathf.RoundToInt(enemy.GlobalPosition.X / TileSize), Mathf.RoundToInt(enemy.GlobalPosition.Z / TileSize));
			var reachable = GetReachableTiles(enemyCoords, enemy.Data.Movement);
			reachable[enemyCoords] = enemyCoords; // Include self so it can choose to stay still

			foreach (var player in players)
			{
				float targetScore = 0;
				Vector3 optimalTile = enemy.GlobalPosition;
				bool canAttackThisTurn = false;
				int distToPlayer = GetGridDistance(enemy.GlobalPosition, player.GlobalPosition);

				if (distToPlayer <= enemy.Data.AttackRange) canAttackThisTurn = true;
				else
				{
					int closestWeCanGet = distToPlayer;
					
					// === NEW: AI only considers truly reachable tiles ===
					foreach (var tilePos in reachable.Keys)
					{
						Vector3 worldPos = new Vector3(tilePos.X * TileSize, 0.01f, tilePos.Y * TileSize);
						int distFromTileToPlayer = GetGridDistance(worldPos, player.GlobalPosition);
						
						if (distFromTileToPlayer <= enemy.Data.AttackRange)
						{
							canAttackThisTurn = true;
							optimalTile = worldPos;
							break; 
						}
						else if (distFromTileToPlayer < closestWeCanGet)
						{
							closestWeCanGet = distFromTileToPlayer;
							optimalTile = worldPos;
						}
					}
				}

				if (canAttackThisTurn) targetScore += 1000;
				targetScore -= player.Data.CurrentHP * 10; 
				if (canAttackThisTurn && player.Data.CurrentHP <= enemy.Data.AttackDamage) targetScore += 2000; 
				targetScore -= distToPlayer;

				if (targetScore > bestScore)
				{
					bestScore = targetScore;
					bestTarget = player;
					bestMovePos = optimalTile;
				}
			}
			
			if (bestMovePos != enemy.GlobalPosition)
			{
				Vector2I targetCoords = new Vector2I(Mathf.RoundToInt(bestMovePos.X / TileSize), Mathf.RoundToInt(bestMovePos.Z / TileSize));
				var path = ExtractPath(reachable, enemyCoords, targetCoords);
				await enemy.MoveAlongPath(path); // <-- NEW: Use multi-hop path
			}

			if (bestTarget != null && GetGridDistance(enemy.GlobalPosition, bestTarget.GlobalPosition) <= enemy.Data.AttackRange)
			{
				await PerformAttackAsync(enemy, bestTarget); 
				if (_currentState != State.EnemyTurn) return;
				await ToSignal(GetTree().CreateTimer(0.4f), "timeout"); 
			}
			
			enemy.HasMoved = true;
			enemy.HasAttacked = true;
			enemy.UpdateVisuals();
		}

		foreach (var u in _units.Where(x => IsInstanceValid(x) && x.IsFriendly)) u.NewTurn();
		
		// === NEW: Increment turn and check for cutscenes! ===
		_currentTurnNumber++;
		CheckMidBattleEvents();
	}
	
	public override void _UnhandledInput(InputEvent @event)
	{
		// Block input entirely if it isn't the player's turn!
		if (_dialogueActive || _levelUpActive || _currentState == State.EnemyTurn || _currentState == State.Cutscene) return;

		if (@event is InputEventMouseMotion mouseMotion) HandleHover(mouseMotion.Position);
		else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left) HandleClick(mouseEvent.Position);
	}
	
	private void OnEndTurnPressed() 
	{ 
		// Bug 2 Fix: Double-check safety before starting the AI turn!
		if (_currentState != State.PlayerTurn || _levelUpActive) return; 

		bool enemiesAlive = _units.Any(u => IsInstanceValid(u) && !u.IsFriendly);
		if (!enemiesAlive) return; // Prevent ending turn if battle is actively wrapping up

		StartEnemyTurn(); 
	}

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
	}

	private void OnTimelineEnded()
	{
		_dialogueActive = false;
		if (DimOverlay != null) DimOverlay.Visible = false;
		
		// === NEW: Route the game back to the battle if we paused for a cutscene ===
		if (_isMidBattleDialogue)
		{
			_isMidBattleDialogue = false;
			CheckMidBattleEvents(); // This will naturally trigger the "YOUR TURN" announcer!
		}
		else
		{
			AdvanceScript(); 
		}
	}
	
	private void ShowMovementRange(Unit u)
	{
		ClearMovementRange();
		if (u == null || u.HasMoved) return;

		Vector2I startCoords = new Vector2I(Mathf.RoundToInt(u.GlobalPosition.X / TileSize), Mathf.RoundToInt(u.GlobalPosition.Z / TileSize));
		var reachable = GetReachableTiles(startCoords, u.Data.Movement);

		Color moveColor = new Color(0.35f, 0.72f, 1.0f, 0.28f);
		
		foreach (var kvp in reachable)
		{
			if (kvp.Key == startCoords) continue; // Don't highlight the tile standing on
			if (_grid.TryGetValue(kvp.Key, out Tile tile))
			{
				tile.SetHighlight(true, moveColor);
				_movementHighlightTiles.Add(tile);
			}
		}
	}

	private async void TryMoveTo(Vector3 targetPos)
	{
		if (_selectedUnit == null) return;

		Vector2I startCoords = new Vector2I(Mathf.RoundToInt(_selectedUnit.GlobalPosition.X / TileSize), Mathf.RoundToInt(_selectedUnit.GlobalPosition.Z / TileSize));
		Vector2I targetCoords = new Vector2I(Mathf.RoundToInt(targetPos.X / TileSize), Mathf.RoundToInt(targetPos.Z / TileSize));

		var reachable = GetReachableTiles(startCoords, _selectedUnit.Data.Movement);

		if (reachable.ContainsKey(targetCoords))
		{
			var path = ExtractPath(reachable, startCoords, targetCoords);

			ShowActions(false);
			RefreshTargetIcons();
			ClearMovementRange(); // Clear visual blue tiles before moving

			// Await the new multi-hop path!
			await _selectedUnit.MoveAlongPath(path);

			if (_selectedUnit != null)
			{
				CheckAutoExhaust(_selectedUnit);
				_selectedUnit.UpdateVisuals();
				UpdateStatsUI();
				ShowActions(true);
				RefreshTargetIcons();

				if (_selectedUnit.HasAttacked) DeselectUnit();
			}
		}
		else GD.Print("Movement blocked or too far!");
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
			ClearAttackPreview(); 
			return;
		}

		Node collider = (Node)result["collider"];
		Tile targetTile = null;
		Unit hoveredUnit = null;

		if (collider.GetParent() is Unit u) 
		{
			hoveredUnit = u;
			Vector2I gridPos = new Vector2I(Mathf.RoundToInt(u.GlobalPosition.X / TileSize), Mathf.RoundToInt(u.GlobalPosition.Z / TileSize));
			if (_grid.TryGetValue(gridPos, out Tile underlyingTile)) targetTile = underlyingTile;
		}
		else if (collider.GetParent() is Tile tile) targetTile = tile;

		if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasAttacked)
		{
			if (hoveredUnit != null && !hoveredUnit.IsFriendly)
			{
				if (GetGridDistance(_selectedUnit.GlobalPosition, hoveredUnit.GlobalPosition) <= _selectedUnit.Data.AttackRange)
				{
					Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16));
					
					if (_previewedEnemy != hoveredUnit)
					{
						ClearAttackPreview(); 
						_previewedEnemy = hoveredUnit;
						hoveredUnit.PreviewDamage(_selectedUnit.GetMinDamage());
					}
					
					ClearHover(); 
					return;
				}
			}
		}

		ClearAttackPreview();

		if (targetTile != _hoveredTile)
		{
			ClearHover();                  
			_hoveredTile = targetTile;

			if (_hoveredTile != null && _currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved)
			{
				bool canMoveHere = _movementHighlightTiles.Contains(_hoveredTile);
				if (canMoveHere) _hoveredTile.SetHighlight(true, new Color(0f, 1f, 0f, 0.7f)); 
			}
		}
	}

	private void ClearHover()
	{
		if (_hoveredTile != null)
		{
			// If it's a movement tile, restore it to the EXACT SAME soft blue
			if (_movementHighlightTiles.Contains(_hoveredTile)) 
			{
				_hoveredTile.SetHighlight(true, new Color(0.35f, 0.72f, 1.0f, 0.28f));
			}
			else 
			{
				// If it's just a normal floor tile, turn the highlight completely off
				_hoveredTile.SetHighlight(false); 
			}
			
			_hoveredTile = null;
		}
	}
	
	private void SpawnFloatingDamage(Vector3 targetPosition, int damageAmount)
	{
		Label3D damageLabel = new Label3D { Text = damageAmount.ToString(), PixelSize = 0.02f, FontSize = 30, Modulate = new Color(1, 0.2f, 0.2f), OutlineModulate = new Color(0, 0, 0), OutlineSize = 6, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
		AddChild(damageLabel);
		damageLabel.GlobalPosition = targetPosition + new Vector3(0, 1.5f, 0);

		Tween tween = CreateTween();
		Vector3 floatUpPosition = damageLabel.GlobalPosition + new Vector3(0, 1.5f, 0);
		tween.TweenProperty(damageLabel, "global_position", floatUpPosition, 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(damageLabel, "modulate:a", 0.0f, 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Finished += () => damageLabel.QueueFree();
	}
	
	private void AdvanceScript()
	{
		// === NEW: Did Dialogic tell us to switch paths? ===
		if (!string.IsNullOrEmpty(_pendingSection))
		{
			_currentSection = _pendingSection;
			_currentScriptIndex = -1; // Reset to the beginning of the new section
			_pendingSection = "";     // Clear the queue
		}

		_currentScriptIndex++;
		
		// End of the game or end of the path!
		if (!_scriptDatabase.ContainsKey(_currentSection) || _currentScriptIndex >= _scriptDatabase[_currentSection].Count)
		{
			GD.Print("🎉 GAME OVER! You reached the end of this path!");
			StatsLabel.Text = "[center][b][wave]YOU WIN![/wave][/b][/center]";
			return;
		}

		// Fetch the current event from the active section
		ScriptEvent currentEvent = _scriptDatabase[_currentSection][_currentScriptIndex];

		if (currentEvent.Type == EventType.AddPartyMember)
		{
			_party.Add(new PersistentUnit(_unitDatabase[currentEvent.ProfileId]));
			AdvanceScript();
		}
		// === NEW: Catch the Jump Command! ===
		else if (currentEvent.Type == EventType.JumpToSection)
		{
			GD.Print($"➡️ Natural script jump from {_currentSection} to {currentEvent.TargetSection}...");
			_pendingSection = currentEvent.TargetSection;
			AdvanceScript(); // Instantly recursively loop back to process the jump!
		}
		else if (currentEvent.Type == EventType.Dialogue) 
		{
			_currentState = State.Cutscene; 
			StartDialogue(currentEvent.TimelinePath);
		}
		else if (currentEvent.Type == EventType.Battle) 
		{
			StartBattle(currentEvent.BattleData);
		}
	}

	private async Task ClearBoardAsync()
	{
		DeselectUnit();
		if (_units.Count == 0) return;

		foreach (var u in _units)
		{
			if (IsInstanceValid(u))
			{
				Tween shrinkTween = CreateTween();
				shrinkTween.TweenProperty(u, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
				shrinkTween.Finished += () => u.QueueFree();
			}
		}
		_units.Clear();
		
		// === NEW: Clear obstacles ===
		foreach (var obs in _obstacles)
		{
			if (IsInstanceValid(obs))
			{
				Tween shrinkTween = CreateTween();
				shrinkTween.TweenProperty(obs, "scale", Vector3.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
				shrinkTween.Finished += () => obs.QueueFree();
			}
		}
		_obstacles.Clear();
		
		await ToSignal(GetTree().CreateTimer(0.25f), "timeout");
	}

	private void CheckAutoExhaust(Unit u)
	{
		if (!u.IsFriendly || u.HasAttacked) return;

		bool enemyInRange = _units.Any(enemy => !enemy.IsFriendly && IsInstanceValid(enemy) && GetGridDistance(u.GlobalPosition, enemy.GlobalPosition) <= u.Data.AttackRange);
		if (!enemyInRange)
		{
			u.HasAttacked = true;
			u.UpdateVisuals();
		}
	}
	
	private void ShowTurnAnnouncer(string text, Color color)
	{
		Label announcer = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		announcer.AddThemeFontSizeOverride("font_size", 100);
		announcer.AddThemeColorOverride("font_color", color);
		announcer.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		announcer.AddThemeConstantOverride("outline_size", 20);
		announcer.SetAnchorsPreset(Control.LayoutPreset.FullRect);

		if (DimOverlay != null) DimOverlay.GetParent().AddChild(announcer);
		else AddChild(announcer);

		announcer.PivotOffset = GetViewport().GetVisibleRect().Size / 2;
		announcer.Scale = Vector2.Zero;
		announcer.Modulate = new Color(1, 1, 1, 0);

		Tween tween = CreateTween();
		tween.Parallel().TweenProperty(announcer, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 1.0f, 0.3f);
		tween.Chain().TweenInterval(1.0f);
		tween.Chain().TweenProperty(announcer, "scale", new Vector2(1.5f, 1.5f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 0.0f, 0.3f);
		tween.Finished += () => announcer.QueueFree();
	}
	
	private int GetGridDistance(Vector3 posA, Vector3 posB)
	{
		int ax = Mathf.RoundToInt(posA.X / TileSize);
		int az = Mathf.RoundToInt(posA.Z / TileSize);
		int bx = Mathf.RoundToInt(posB.X / TileSize);
		int bz = Mathf.RoundToInt(posB.Z / TileSize);
		return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(az - bz));
	}
	
	private void RefreshTargetIcons()
	{
		foreach (var u in _units)
		{
			if (!IsInstanceValid(u)) continue;
			u.SetTargetable(false); 

			if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasAttacked)
			{
				if (!u.IsFriendly && GetGridDistance(_selectedUnit.GlobalPosition, u.GlobalPosition) <= _selectedUnit.Data.AttackRange)
				{
					u.SetTargetable(true);
				}
			}
		}
	}

	private void ClearMovementRange()
	{
		foreach (var t in _movementHighlightTiles) t.SetHighlight(false);
		_movementHighlightTiles.Clear();
	}

	private void ClearAttackPreview()
	{
		if (_previewedEnemy != null) 
		{ 
			_previewedEnemy.ClearPreview(); 
			_previewedEnemy = null; 
		}
		if (_currentState != State.SelectingAttackTarget) Input.SetCustomMouseCursor(null);
	}
	
	private void SpawnRandomObstacles(int obstacleCount)
	{
		List<Vector2I> validTiles = new List<Vector2I>();
		
		// Find all tiles that don't have a unit on them
		foreach (var pos in _grid.Keys)
		{
			// === THE FIX ===
			// 'pos' is already a Vector2I, so we can pass it directly!
			if (IsTileFree(pos)) validTiles.Add(pos);
		}

		// Shuffle the available tiles
		System.Random rnd = new System.Random();
		validTiles = validTiles.OrderBy(x => rnd.Next()).ToList();

		int spawned = 0;
		foreach (var gridPos in validTiles)
		{
			if (spawned >= obstacleCount) break;

			Vector3 worldPos = new Vector3(gridPos.X * TileSize, 0.01f, gridPos.Y * TileSize);
			bool isTall = rnd.Next(2) == 0; // 50/50 chance for rock vs tree
			
			SpawnObstacle(worldPos, isTall);
			spawned++;
		}
	}

	private void SpawnObstacle(Vector3 pos, bool isTall)
	{
		// 1. Create the node without setting its position yet
		Node3D obsNode = new Node3D(); 
		
		Sprite3D sprite = new Sprite3D 
		{ 
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			AlphaCut = SpriteBase3D.AlphaCutMode.OpaquePrepass
		};
		
		Texture2D tex;
		float targetHeight;

		if (isTall)
		{
			tex = GD.Load<Texture2D>("res://assets/tree.png");
			targetHeight = 1.8f; 
		}
		else
		{
			tex = GD.Load<Texture2D>("res://assets/rock.png");
			targetHeight = 0.9f; 
		}

		sprite.Texture = tex;
		
		float scaleFactor = targetHeight / (tex.GetHeight() * sprite.PixelSize);
		sprite.Scale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
		sprite.Position = new Vector3(0, targetHeight / 2.0f, 0);

		obsNode.AddChild(sprite);

		// === THE FIX ===
		// 2. Add it to the tree FIRST
		AddChild(obsNode);
		_obstacles.Add(obsNode);
		
		// 3. NOW set its GlobalPosition because Godot knows where it lives!
		obsNode.GlobalPosition = pos;
		
		obsNode.Scale = Vector3.Zero;
		CreateTween().TweenProperty(obsNode, "scale", Vector3.One, 0.4f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
	}
	
	// === NEW: Pathfinding Logic ===
	private System.Collections.Generic.Dictionary<Vector2I, Vector2I> GetReachableTiles(Vector2I start, int maxMovement)
	{
		var frontier = new System.Collections.Generic.Queue<Vector2I>();
		var cameFrom = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
		var costSoFar = new System.Collections.Generic.Dictionary<Vector2I, int>();

		frontier.Enqueue(start);
		cameFrom[start] = start;
		costSoFar[start] = 0;

		// 4-way movement only (no diagonal corner-cutting)
		Vector2I[] directions = {
			new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1),
			new Vector2I(1, 1), new Vector2I(-1, -1), new Vector2I(1, -1), new Vector2I(-1, 1)
		};

		while (frontier.Count > 0)
		{
			var current = frontier.Dequeue();

			foreach (var dir in directions)
			{
				var next = current + dir;
				int newCost = costSoFar[current] + 1;

				if (newCost > maxMovement) continue;
				if (!_grid.ContainsKey(next)) continue;
				
				// === THE FIX ===
				// Directly pass the 2D 'next' coordinate!
				if (!IsTileFree(next)) continue;

				if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
				{
					costSoFar[next] = newCost;
					cameFrom[next] = current;
					frontier.Enqueue(next);
				}
			}
		}
		return cameFrom;
	}

	private System.Collections.Generic.List<Vector3> ExtractPath(System.Collections.Generic.Dictionary<Vector2I, Vector2I> cameFrom, Vector2I start, Vector2I end)
	{
		var path = new System.Collections.Generic.List<Vector3>();
		var current = end;
		while (current != start)
		{
			path.Add(new Vector3(current.X * TileSize, 0.01f, current.Y * TileSize));
			current = cameFrom[current];
		}
		path.Reverse(); // Reverse so it goes from Start -> End
		return path;
	}
	
	// === UI & JUICE SYSTEM ===
	private void SetupUnifiedUI()
	{
		StyleBoxFlat baseStyle = new StyleBoxFlat {
			BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f),
			CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16,
			CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
			BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4,
			BorderColor = new Color(0.3f, 0.3f, 0.35f, 1f),
			ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 16, ContentMarginBottom = 16,
			ShadowColor = new Color(0, 0, 0, 0.7f), ShadowSize = 8, ShadowOffset = new Vector2(0, 6)
		};

		// === NEW: A sleek, contrasting badge for the NPC Nameplate! ===
		BadgeStyle = new StyleBoxFlat {
			BgColor = new Color(0.6f, 0.1f, 0.1f, 0.95f), // Crimson Red
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 0, CornerRadiusBottomRight = 8, // Cool folder-tab shape
			BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			BorderColor = new Color(0.9f, 0.4f, 0.4f, 1f),
			ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 4, ContentMarginBottom = 4,
			ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 4, ShadowOffset = new Vector2(0, 3)
		};

		StyleBoxFlat btnNormal = (StyleBoxFlat)baseStyle.Duplicate();
		StyleBoxFlat btnHover = (StyleBoxFlat)baseStyle.Duplicate();
		btnHover.BgColor = new Color(0.18f, 0.18f, 0.22f, 1f);
		btnHover.BorderColor = new Color(1f, 0.85f, 0.3f, 1f); 
		btnHover.ShadowSize = 14; btnHover.ShadowOffset = new Vector2(0, 10);

		StyleBoxFlat btnPressed = (StyleBoxFlat)baseStyle.Duplicate();
		btnPressed.BgColor = new Color(0.04f, 0.04f, 0.04f, 1f);
		btnPressed.BorderColor = new Color(0.6f, 0.5f, 0.1f, 1f);
		btnPressed.ShadowSize = 2; btnPressed.ShadowOffset = new Vector2(0, 2);

		MasterTheme = new Theme();
		MasterTheme.SetStylebox("panel", "PanelContainer", baseStyle);
		MasterTheme.SetStylebox("normal", "Button", btnNormal);
		MasterTheme.SetStylebox("hover", "Button", btnHover);
		MasterTheme.SetStylebox("pressed", "Button", btnPressed);
		MasterTheme.SetStylebox("focus", "Button", new StyleBoxEmpty());

		// === NEW: Fix Dialogic's tiny black text globally! ===
		MasterTheme.SetFontSize("normal_font_size", "RichTextLabel", 26);
		MasterTheme.SetColor("default_color", "RichTextLabel", new Color(0.95f, 0.95f, 0.95f, 1f));
		MasterTheme.SetFontSize("font_size", "Label", 22);
		MasterTheme.SetColor("font_color", "Label", new Color(0.95f, 0.95f, 0.95f, 1f));

		if (StatsLabel != null)
		{
			StatsLabel.AddThemeStyleboxOverride("normal", baseStyle);
			StatsLabel.AddThemeColorOverride("default_color", new Color(0.95f, 0.95f, 0.95f, 1f));
			
			// === THE FIX: Force the sizes for both normal AND bold text! ===
			StatsLabel.AddThemeFontSizeOverride("normal_font_size", 24);
			StatsLabel.AddThemeFontSizeOverride("bold_font_size", 28); // Make the Name slightly bigger!
			
			StatsLabel.BbcodeEnabled = true;
			StatsLabel.FitContent = true;
			StatsLabel.ScrollActive = false;
			StatsLabel.CustomMinimumSize = new Vector2(350, 0);
			StatsLabel.ClipContents = false;
		}

		Button[] buttons = { AttackButton, EndTurnButton };
		foreach (Button btn in buttons)
		{
			if (btn == null) continue;
			btn.AddThemeStyleboxOverride("normal", btnNormal);
			btn.AddThemeStyleboxOverride("hover", btnHover);
			btn.AddThemeStyleboxOverride("pressed", btnPressed);
			btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			btn.AddThemeFontSizeOverride("font_size", 24); 
			AddButtonJuice(btn);
		}
	}

	private void UpdateStatsUI()
	{
		if (StatsLabel == null) return;

		if (_selectedUnit != null && _selectedUnit.Data != null)
		{
			// BBCode magic! Grey out used actions, pulse ready actions with color!
			string moveStr = _selectedUnit.HasMoved ? "[color=#666666]Used[/color]" : "[color=#44ff44][pulse freq=1.5 color=#ffffff40]READY[/pulse][/color]";
			string atkStr = _selectedUnit.HasAttacked ? "[color=#666666]Used[/color]" : "[color=#ffaa44][pulse freq=1.5 color=#ffffff40]READY[/pulse][/color]";
			
			StatsLabel.Text = $"[center][b][wave amp=20 freq=3]{_selectedUnit.Data.Profile.Name}[/wave][/b]\n" +
							  $"[color=gold]Lv.{_selectedUnit.Data.Level}[/color] | HP: [color=#ff4444]{_selectedUnit.Data.CurrentHP}[/color]/{_selectedUnit.Data.MaxHP}\n" +
							  $"Move: {moveStr} | Attack: {atkStr}[/center]";
		}
		else StatsLabel.Text = "[center]\nSelect a Unit...[/center]";

		StatsLabel.PivotOffset = StatsLabel.Size / 2;
		Tween popTween = CreateTween();
		popTween.TweenProperty(StatsLabel, "scale", new Vector2(1.05f, 1.05f), 0.08f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		popTween.TweenProperty(StatsLabel, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void AddButtonJuice(Button btn)
	{
		// Dynamically center the pivot point every time we hover so it scales from the center perfectly
		btn.MouseEntered += () => {
			btn.PivotOffset = btn.Size / 2;
			CreateTween().TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
		
		btn.MouseExited += () => {
			CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		};
		
		btn.ButtonDown += () => {
			CreateTween().TweenProperty(btn, "scale", new Vector2(0.9f, 0.9f), 0.1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		};
		
		btn.ButtonUp += () => {
			CreateTween().TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		};
	}
	
	private void CheckMidBattleEvents()
	{
		// Look for an event scheduled for the current turn
		var evt = _activeMidBattleEvents.FirstOrDefault(e => e.Turn == _currentTurnNumber);
		
		if (!string.IsNullOrEmpty(evt.TimelinePath))
		{
			// We found a cutscene! 
			_activeMidBattleEvents.Remove(evt); // Remove it so it doesn't loop infinitely
			_isMidBattleDialogue = true;        // Flag that we are pausing a battle
			StartDialogue(evt.TimelinePath);
		}
		else
		{
			// No cutscene for this turn? Start the player's turn normally!
			_currentState = State.PlayerTurn;
			DeselectUnit(); // Safely refresh the UI
			ShowTurnAnnouncer("YOUR TURN", new Color(0.2f, 0.8f, 1.0f));
			ShowActions(true);
		}
	}
	
	private void OnDialogicSignal(string argument)
	{
		// We expect Dialogic to send strings like "JumpTo:Path_Ogre"
		if (argument.StartsWith("JumpTo:"))
		{
			_pendingSection = argument.Split(":")[1];
			GD.Print($"🔀 Branch chosen! Queuing up: {_pendingSection}");
		}
	}
}
