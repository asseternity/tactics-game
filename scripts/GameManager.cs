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
	
	private Tween _uiTween;
	private List<Tile> _movementHighlightTiles = new();
	private Unit _previewedEnemy;
	
	private Godot.Collections.Dictionary<Vector2I, Tile> _grid = new();
	private const float TileSize = 2f;
	private const int GridWidth = 10;
	private const int GridDepth = 10;
	private Tile _hoveredTile;
	
	private System.Collections.Generic.Dictionary<string, UnitProfile> _unitDatabase = new();
	private List<ScriptEvent> _mainScript = new();
	private List<PersistentUnit> _party = new(); 
	private int _currentScriptIndex = -1;

	private enum State { PlayerTurn, EnemyTurn, SelectingAttackTarget, Cutscene }
	private State _currentState = State.Cutscene; // Start in cutscene mode

	private List<Unit> _units = new List<Unit>();
	private Unit _selectedUnit;
	
	private Node _dialogic;
	private bool _dialogueActive = false;
	private bool _levelUpActive = false; // Block inputs during Level Up

public override void _Ready()
	{
		Instance = this; 

		// Add XP Rewards (the final number) to all units!
		_unitDatabase["Knight"] = new UnitProfile("Knight", "res://assets/knight.png", 25, 7, 1, 3, 0);
		_unitDatabase["Archer"] = new UnitProfile("Archer", "res://assets/archer.png", 18, 8, 2, 3, 0);
		_unitDatabase["Goblin"] = new UnitProfile("Goblin", "res://assets/goblin.png", 10, 3, 1, 3, 40);
		_unitDatabase["Ogre"]   = new UnitProfile("Ogre", "res://assets/ogre.png", 25, 8, 1, 2, 120);

		_mainScript = GameScript.GetMainScript();

		GenerateGrid();
		AttackButton.Pressed += OnAttackButtonPressed;
		EndTurnButton.Pressed += OnEndTurnPressed; 
		ActionMenu.Visible = false;

		_dialogic = GetNodeOrNull<Node>("/root/Dialogic");
		_dialogic.Connect("timeline_ended", new Callable(this, MethodName.OnTimelineEnded));
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

		_currentState = State.PlayerTurn;
		StatsLabel.Text = "Battle Start! Your Turn.";
		ShowTurnAnnouncer("YOUR TURN", new Color(0.2f, 0.8f, 1.0f));
		ShowActions(true);
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
		
		await target.TakeDamage(attacker.Data.AttackDamage, attacker);
		SpawnFloatingDamage(target.GlobalPosition, attacker.Data.AttackDamage);

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
			await ToSignal(GetTree().CreateTimer(1.8f), "timeout");
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

	private async void TryMoveTo(Vector3 targetPos)
	{
		if (_selectedUnit == null) return;

		// UPDATE: Uses dynamic Movement stat!
		if (GetGridDistance(_selectedUnit.GlobalPosition, targetPos) > _selectedUnit.Data.Movement)
		{
			GD.Print("Movement too far!");
			return;
		}

		if (IsTileFree(targetPos))
		{
			_selectedUnit.MoveTo(targetPos);
			_selectedUnit.HasMoved = true; 
			
			ShowActions(false);
			RefreshTargetIcons();
			ClearMovementRange();

			await ToSignal(GetTree().CreateTimer(0.35f), "timeout");

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
	}

	private bool IsTileFree(Vector3 pos)
	{
		foreach (var u in _units)
		{
			if (IsInstanceValid(u) && u.GlobalPosition.DistanceTo(pos) < 0.1f) return false;
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

	private void UpdateStatsUI()
	{
		if (StatsLabel == null) return;

		if (_selectedUnit != null && _selectedUnit.Data != null)
		{
			string moveStr = _selectedUnit.HasMoved ? "[USED]" : "[READY]";
			string atkStr = _selectedUnit.HasAttacked ? "[USED]" : "[READY]";
			
			StatsLabel.Text = $"Selected: {_selectedUnit.Data.Profile.Name}\n" +
							  $"Lv.{_selectedUnit.Data.Level} HP: {_selectedUnit.Data.CurrentHP}/{_selectedUnit.Data.MaxHP}\n" +
							  $"Move: {moveStr}\n" +
							  $"Attack: {atkStr}";
		}
		else StatsLabel.Text = "Select a Unit...";
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
					
					foreach (var tilePos in _grid.Keys)
					{
						Vector3 worldPos = new Vector3(tilePos.X * TileSize, 0.01f, tilePos.Y * TileSize);
						// UPDATE: Uses dynamic Movement stat!
						if (GetGridDistance(enemy.GlobalPosition, worldPos) <= enemy.Data.Movement && IsTileFree(worldPos))
						{
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
				enemy.MoveTo(bestMovePos);
				await ToSignal(GetTree().CreateTimer(0.35f), "timeout"); 
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
		
		ShowTurnAnnouncer("YOUR TURN", new Color(0.2f, 0.8f, 1.0f));

		_currentState = State.PlayerTurn;
		if (StatsLabel != null) StatsLabel.Text = "Your Turn";
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
		AdvanceScript(); 
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
						hoveredUnit.PreviewDamage(_selectedUnit.Data.AttackDamage);
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
				// UPDATE: Uses dynamic Movement stat!
				bool canMoveHere = GetGridDistance(_selectedUnit.GlobalPosition, _hoveredTile.GlobalPosition) <= _selectedUnit.Data.Movement && IsTileFree(_hoveredTile.GlobalPosition);

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
		_currentScriptIndex++;
		
		if (_currentScriptIndex >= _mainScript.Count)
		{
			GD.Print("🎉 GAME OVER! You won!");
			StatsLabel.Text = "YOU WIN!";
			return;
		}

		ScriptEvent currentEvent = _mainScript[_currentScriptIndex];

		if (currentEvent.Type == EventType.AddPartyMember)
		{
			// Add them to the persistent roster, then immediately jump to the next script event!
			_party.Add(new PersistentUnit(_unitDatabase[currentEvent.ProfileId]));
			AdvanceScript();
		}
		else if (currentEvent.Type == EventType.Dialogue) 
		{
			_currentState = State.Cutscene; // Lock the board
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
	
	private void ShowMovementRange(Unit u)
	{
		ClearMovementRange();
		if (u == null || u.HasMoved) return;

		Color moveColor = new Color(0.35f, 0.72f, 1.0f, 0.28f);
		
		foreach (var kvp in _grid)
		{
			Vector3 tilePos = new Vector3(kvp.Key.X * TileSize, 0.01f, kvp.Key.Y * TileSize);
			// UPDATE: Uses dynamic Movement stat!
			if (GetGridDistance(u.GlobalPosition, tilePos) <= u.Data.Movement && IsTileFree(tilePos))
			{
				kvp.Value.SetHighlight(true, moveColor);
				_movementHighlightTiles.Add(kvp.Value);
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
}
