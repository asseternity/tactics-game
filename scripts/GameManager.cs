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
	private Vector3 _initialCamPos;
	
	[ExportGroup("UI References")]
	[Export] public Control ActionMenu;
	[Export] public Button AttackButton;
	[Export] public Button EndTurnButton;
	[Export] public Button PartyButton; // <-- NEW!
	[Export] public RichTextLabel StatsLabel;
	[Export] public ColorRect DimOverlay;
	[Export] public Texture2D AttackCursorIcon;

	[ExportGroup("Environment References")]
	[Export] public DirectionalLight3D MainLight;
	[Export] public WorldEnvironment WorldEnv;
	[Export] public Godot.Collections.Array<PackedScene> BackgroundDioramas;
	private MeshInstance3D _backgroundPlane;
	private List<Node3D> _activeDioramas = new();
	
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

	private enum State { PlayerTurn, EnemyTurn, SelectingAttackTarget, Cutscene, PartyMenu } // <-- Added PartyMenu
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
		if (Cam != null) _initialCamPos = Cam.GlobalPosition;
		
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
		if (PartyButton != null) PartyButton.Pressed += TogglePartyMenu; // <-- NEW!
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
			
			// === THE FIX: RELATIVE CAMERA LEASH ===
			// The camera is allowed to move exactly this many units away from where it started.
			float limitLeftRight = 5f; 
			float limitUp = 0f;    // Keep the camera from shoving its lens into the background dioramas!
			float limitDown = 15f; // More room needed going down so you can see the bottom of the grid

			newPos.X = Mathf.Clamp(newPos.X, _initialCamPos.X - limitLeftRight, _initialCamPos.X + limitLeftRight);
			newPos.Z = Mathf.Clamp(newPos.Z, _initialCamPos.Z - limitUp, _initialCamPos.Z + limitDown);

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
		ApplyEnvironment(data);
		GD.Print("⚔️ Starting Battle!");
		StatsLabel.Text = "Setting up the board...";
		
		// Clear just in case, though HandleUnitDeath usually handles this now
		await ClearBoardAsync(); 

		// === NEW: Await the rising terrain! ===
		await SetupGridElevation(data);
		
		SpawnDioramas();

		foreach (var hero in _party) hero.HealBetweenBattles();

		// Perfect Snapping for Friendlies 
		for (int i = 0; i < Mathf.Min(_party.Count, data.FriendlySpawns.Count); i++)
		{
			Vector3 finalPos = data.FriendlySpawns[i];
			if (_grid.TryGetValue(GetGridPos(finalPos), out Tile tile))
				finalPos = new Vector3(tile.Position.X, tile.Position.Y, tile.Position.Z);
			
			SpawnUnit(_party[i], true, finalPos);
		}

		// Perfect Snapping for Enemies 
		foreach (var e in data.Enemies) 
		{
			Vector3 finalPos = e.Position;
			if (_grid.TryGetValue(GetGridPos(finalPos), out Tile tile))
				finalPos = new Vector3(tile.Position.X, tile.Position.Y, tile.Position.Z);
			
			SpawnUnit(new PersistentUnit(_unitDatabase[e.ProfileId]), false, finalPos);
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

		int damage = GD.RandRange(attacker.GetMinDamage(), attacker.GetMaxDamage());

		// === NEW GAME JUICE: HIGH GROUND ADVANTAGE! ===
		if (attacker.GlobalPosition.Y > target.GlobalPosition.Y + 0.15f) // 0.15f threshold prevents flat tiles from triggering it
		{
			damage += 1;
			// Spawn a special golden "High Ground!" text just above the damage numbers!
			SpawnFloatingText(target.GlobalPosition + new Vector3(0, 0.6f, 0), "HIGH GROUND!", new Color(1f, 0.85f, 0.2f), 22);
		}

		SpawnFloatingText(target.GlobalPosition, damage.ToString(), new Color(1, 0.2f, 0.2f));

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
			_currentState = State.Cutscene; 
			ClearMovementRange();
			ClearHover();
			DeselectUnit();
			ShowActions(false);

			GD.Print("🏆 Battle Won!");
			
			await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
			
			// === NEW: Scrub the board and flatten the earth BEFORE the next script event! ===
			await ClearBoardAsync(); 
			
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
	
	private void SpawnFloatingText(Vector3 targetPosition, string text, Color color, float size = 30)
	{
		Label3D label = new Label3D { Text = text, PixelSize = 0.02f, FontSize = (int)size, Modulate = color, OutlineModulate = new Color(0, 0, 0), OutlineSize = 6, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
		AddChild(label);
		label.GlobalPosition = targetPosition + new Vector3(0, 1.5f, 0);

		Tween tween = CreateTween();
		Vector3 floatUpPosition = label.GlobalPosition + new Vector3(0, 1.2f, 0);
		tween.TweenProperty(label, "global_position", floatUpPosition, 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Finished += () => label.QueueFree();
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
			// === CRITICAL FIX: This line defines who the main character is! ===
			// If this is missing, the Knight will think they are a companion.
			bool isPlayer = currentEvent.ProfileId == "Knight"; 
			
			// Pass that 'isPlayer' bool into the new unit
			_party.Add(new PersistentUnit(_unitDatabase[currentEvent.ProfileId], isPlayer));
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
		
		// === NEW: Shrink and clear the decorative dioramas ===
		foreach (var diorama in _activeDioramas)
		{
			if (IsInstanceValid(diorama))
			{
				Tween shrinkTween = CreateTween();
				shrinkTween.TweenProperty(diorama, "scale", Vector3.Zero, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
				shrinkTween.Finished += () => diorama.QueueFree();
			}
		}
		_activeDioramas.Clear();
		
		// Flatten the Terrain Juicily!
		Tween flattenTween = CreateTween();
		flattenTween.SetParallel(true);
		bool hasElevatedTiles = false;

		foreach (var tile in _grid.Values)
		{
			if (tile.Position.Y > 0.02f) // Only tween tiles that are actually raised
			{
				hasElevatedTiles = true;
				// Add a tiny random delay to each tile for a cascading ripple effect
				float delay = (float)GD.RandRange(0.0f, 0.15f);
				flattenTween.TweenProperty(tile, "position:y", 0.01f, 0.4f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out).SetDelay(delay);
			}
		}

		if (hasElevatedTiles)
		{
			await ToSignal(GetTree().CreateTimer(0.6f), "timeout"); // Wait for the longest tween + delay
		}
		else
		{
			flattenTween.Kill(); // <--- THE FIX: Destroy the empty tween so Godot doesn't complain!
			await ToSignal(GetTree().CreateTimer(0.25f), "timeout");
		}
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
			// THE FIX: Check if the enemy is still alive and in memory!
			if (IsInstanceValid(_previewedEnemy))
			{
				_previewedEnemy.ClearPreview(); 
			}
			
			// Always null out the reference whether they are alive or dead
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

			// === THE FIX: Snap rocks/trees to elevation ===
			float height = _grid[gridPos].Position.Y;
			Vector3 worldPos = new Vector3(gridPos.X * TileSize, height, gridPos.Y * TileSize);
			
			bool isTall = rnd.Next(2) == 0; 
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
			// === THE FIX: Units walk UP the hills! ===
			float height = _grid.ContainsKey(current) ? _grid[current].Position.Y : 0.01f;
			path.Add(new Vector3(current.X * TileSize, height, current.Y * TileSize));
			current = cameFrom[current];
		}
		path.Reverse(); 
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

		Button[] buttons = { AttackButton, EndTurnButton, PartyButton };
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
	
	private Control _activePartyMenu;

	// === UPGRADED SEAMLESS PARTY MENU ===
	private void TogglePartyMenu()
	{
		if (_currentState != State.PlayerTurn && _currentState != State.PartyMenu) return;

		// --- CLOSE MENU ANIMATION ---
		if (_activePartyMenu != null)
		{
			Tween outTween = CreateTween();
			outTween.TweenProperty(_activePartyMenu, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			outTween.Finished += () => {
				_activePartyMenu.QueueFree();
				_activePartyMenu = null;
				if (DimOverlay != null) DimOverlay.Visible = false;
				_currentState = State.PlayerTurn;
				ShowActions(true);
			};
			return;
		}

		// --- OPEN MENU ---
		_currentState = State.PartyMenu;
		ShowActions(false);
		if (DimOverlay != null) DimOverlay.Visible = true;

		_activePartyMenu = new CenterContainer();
		_activePartyMenu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(_activePartyMenu);
		else AddChild(_activePartyMenu);

		// ONLY ONE PANEL. The entire menu shares this single, gorgeous dark rounded background.
		PanelContainer mainPanel = new PanelContainer { CustomMinimumSize = new Vector2(950, 550), Theme = MasterTheme };
		_activePartyMenu.AddChild(mainPanel);

		HBoxContainer mainHBox = new HBoxContainer();
		mainPanel.AddChild(mainHBox);

		// === LEFT SIDE: ROSTER & CLOSE BUTTON ===
		VBoxContainer leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
		mainHBox.AddChild(leftCol);

		Label rosterTitle = new Label { Text = "Party Roster", HorizontalAlignment = HorizontalAlignment.Center };
		rosterTitle.AddThemeFontSizeOverride("font_size", 28);
		leftCol.AddChild(rosterTitle);
		leftCol.AddChild(new HSeparator());

		// FIX 2: ScrollContainer must expand vertically to allow scrolling!
		ScrollContainer rosterScroll = new ScrollContainer { 
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill // <--- This allows it to scroll!
		};
		// Stylish dark scrollbar
		StyleBoxFlat scrollStyle = new StyleBoxFlat { BgColor = new Color(0.3f, 0.3f, 0.35f), CornerRadiusTopLeft=4, CornerRadiusTopRight=4, CornerRadiusBottomLeft=4, CornerRadiusBottomRight=4 };
		rosterScroll.GetVScrollBar().AddThemeStyleboxOverride("grabber", scrollStyle);
		rosterScroll.GetVScrollBar().AddThemeStyleboxOverride("grabber_hover", scrollStyle);
		rosterScroll.GetVScrollBar().AddThemeStyleboxOverride("grabber_pressed", scrollStyle);
		leftCol.AddChild(rosterScroll);

		// === THE FIX: MarginContainer to give the hover animations breathing room! ===
		MarginContainer rosterMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		rosterMargin.AddThemeConstantOverride("margin_left", 16);
		rosterMargin.AddThemeConstantOverride("margin_right", 16);
		rosterMargin.AddThemeConstantOverride("margin_top", 10);
		rosterMargin.AddThemeConstantOverride("margin_bottom", 10);
		rosterScroll.AddChild(rosterMargin);

		VBoxContainer rosterBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		rosterBox.AddThemeConstantOverride("separation", 8); // Adds a nice little gap between the buttons, too!
		rosterMargin.AddChild(rosterBox);

		// === MIDDLE: SEAMLESS DIVIDER ===
		VSeparator splitLine = new VSeparator();
		splitLine.AddThemeConstantOverride("separation", 30);
		mainHBox.AddChild(splitLine);

		// === RIGHT SIDE: DETAILS CONTAINER (No Background!) ===
		// FIX 1: This is just a layout wrapper now. No ugly nested boxes.
		MarginContainer detailsContainer = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		detailsContainer.AddThemeConstantOverride("margin_right", 20);
		mainHBox.AddChild(detailsContainer);

		// Populate Roster Buttons
		foreach (PersistentUnit unit in _party)
		{
			Button btn = new Button { Text = unit.Profile.Name, CustomMinimumSize = new Vector2(0, 60) };
			AddButtonJuice(btn);
			btn.Pressed += () => ShowPartyMemberDetails(unit, detailsContainer);
			rosterBox.AddChild(btn);
		}

		// FIX 3: Dedicated Close Button at the bottom of the roster!
		leftCol.AddChild(new HSeparator());
		Button closeBtn = new Button { Text = "Close Menu", CustomMinimumSize = new Vector2(0, 50) };
		closeBtn.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f)); // Make it slightly red so it pops
		AddButtonJuice(closeBtn);
		closeBtn.Pressed += TogglePartyMenu; // Clicking this fires the close animation!
		leftCol.AddChild(closeBtn);

		// Juice: Pop in animation
		mainPanel.PivotOffset = new Vector2(475, 275);
		mainPanel.Scale = Vector2.Zero;
		CreateTween().TweenProperty(mainPanel, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		if (_party.Count > 0) ShowPartyMemberDetails(_party[0], detailsContainer);
	}

	// === UPGRADED DETAILS SCREEN ===
	private void ShowPartyMemberDetails(PersistentUnit unit, MarginContainer detailsContainer)
	{
		foreach (Node child in detailsContainer.GetChildren()) child.QueueFree();

		HBoxContainer layout = new HBoxContainer();
		layout.AddThemeConstantOverride("separation", 40);
		detailsContainer.AddChild(layout);

		// Big, bold portrait on the left of the details pane
		TextureRect portrait = new TextureRect {
			Texture = GD.Load<Texture2D>(unit.Profile.SpritePath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(240, 350), 
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		layout.AddChild(portrait);

		// Left-aligned stats block for a cleaner, professional look
		VBoxContainer statsBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		statsBox.AddThemeConstantOverride("separation", 15);
		layout.AddChild(statsBox);

		RichTextLabel nameLabel = new RichTextLabel {
			BbcodeEnabled = true, FitContent = true, ScrollActive = false,
			Text = $"[left][font_size=32][b][wave amp=20 freq=2]{unit.Profile.Name}[/wave][/b][/font_size]\n[color=gold]Level {unit.Level}[/color][/left]"
		};
		statsBox.AddChild(nameLabel);

		string typeText = unit.IsPlayerCharacter ? "[color=#44ff44]Player Character (You)[/color]" : "[color=#44ccff]Companion[/color]";
		RichTextLabel combatStats = new RichTextLabel {
			BbcodeEnabled = true, FitContent = true, ScrollActive = false,
			Text = $"[left]{typeText}\n\n[color=#aaaaaa]Max HP:[/color] {unit.MaxHP}\n[color=#aaaaaa]Damage:[/color] {unit.AttackDamage}\n[color=#aaaaaa]Movement:[/color] {unit.Movement}[/left]"
		};
		statsBox.AddChild(combatStats);

		// CoG Relationship Bars
		if (!unit.IsPlayerCharacter)
		{
			statsBox.AddChild(new HSeparator());
			Label relTitle = new Label { Text = "Dynamics with You" };
			relTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
			statsBox.AddChild(relTitle);

			foreach (var rel in unit.Relationships)
			{
				VBoxContainer barBox = new VBoxContainer();
				Color barColor = rel.Key == "Fear" ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.3f, 0.8f, 0.5f);
				
				ProgressBar bar = new ProgressBar { CustomMinimumSize = new Vector2(0, 22), ShowPercentage = false, MaxValue = 100 };
				StyleBoxFlat bgStyle = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f), CornerRadiusTopLeft=6, CornerRadiusTopRight=6, CornerRadiusBottomLeft=6, CornerRadiusBottomRight=6 };
				StyleBoxFlat fillStyle = new StyleBoxFlat { BgColor = barColor, CornerRadiusTopLeft=6, CornerRadiusTopRight=6, CornerRadiusBottomLeft=6, CornerRadiusBottomRight=6 };
				bar.AddThemeStyleboxOverride("bg", bgStyle);
				bar.AddThemeStyleboxOverride("fill", fillStyle);

				Label barLabel = new Label { Text = $"  {rel.Key}: {rel.Value}%", VerticalAlignment = VerticalAlignment.Center };
				barLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
				barLabel.AddThemeFontSizeOverride("font_size", 14);
				barLabel.AddThemeColorOverride("font_color", Color.Color8(255,255,255,220));
				bar.AddChild(barLabel);

				barBox.AddChild(bar);
				statsBox.AddChild(barBox);

				bar.Value = 0;
				CreateTween().TweenProperty(bar, "value", (double)rel.Value, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			}
		}

		// === THE FIX: Layout-Safe Juice ===
		// We fade the inner layout box in, completely respecting the HBoxContainer's borders!
		layout.Modulate = new Color(1, 1, 1, 0);
		
		Tween fadeTween = CreateTween();
		fadeTween.TweenProperty(layout, "modulate:a", 1.0f, 0.25f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);;
	}
	
	private void ApplyEnvironment(BattleSetup data)
	{
		// 1. === THE GODOT 4 JUICE PIPELINE ===
		if (MainLight != null && WorldEnv != null)
		{
			if (WorldEnv.Environment == null) 
			{
				WorldEnv.Environment = new Godot.Environment();
			}
			
			Godot.Environment env = WorldEnv.Environment;

			// CRITICAL FIX: Force the camera to use this environment so it cannot silently fail!
			if (Cam != null) Cam.Environment = env;
			
			env.BackgroundMode = Godot.Environment.BGMode.Color;
			env.AmbientLightSource = Godot.Environment.AmbientSource.Color;

			// --- COLOR GRADING ---
			env.TonemapMode = Godot.Environment.ToneMapper.Aces;
			env.TonemapExposure = 1.05f; 
			env.AdjustmentEnabled = true;
			env.AdjustmentContrast = 1.05f; 
			env.AdjustmentSaturation = 1.1f; // Subtle, clean pop
			
			// --- GLOW & BLOOM ---
			env.GlowEnabled = true; 
			// THE FIX: Raise the threshold so ONLY your green/blue emissive tiles glow, not the dirt!
			env.GlowHdrThreshold = 0.9f; 
			env.GlowIntensity = 0.8f; 
			env.GlowStrength = 0.9f;
			env.GlowBloom = 0.0f; // Removes the blurry full-screen smear
			env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive;
			
			// --- SHADOWS ---
			env.SsaoEnabled = true; 
			env.SsaoRadius = 1.0f;
			env.SsaoIntensity = 1.5f; // Softens the pure black crevices
			env.SsilEnabled = true; 

			// --- VOLUMETRIC FOG ---
			env.FogEnabled = true;
			env.VolumetricFogEnabled = true; 
			env.VolumetricFogDensity = 0.005f; // A tasteful atmospheric haze, not pea-soup

			// Apply specific moods
			switch (data.Light)
			{
				case LightingMood.Noon:
					MainLight.Visible = true;
					MainLight.LightColor = new Color(1f, 0.98f, 0.95f);
					MainLight.LightEnergy = 1.2f;
					MainLight.RotationDegrees = new Vector3(-75, 45, 0);
					env.BackgroundColor = new Color(0.4f, 0.6f, 0.9f); 
					env.AmbientLightColor = new Color(0.6f, 0.8f, 1f);   
					env.AmbientLightEnergy = 0.5f;
					env.FogLightColor = env.BackgroundColor; // Match background for seamless horizon
					env.VolumetricFogAlbedo = new Color(0.6f, 0.75f, 1.0f);
					break;
					
				case LightingMood.Morning:
					MainLight.Visible = true;
					MainLight.LightColor = new Color(1f, 0.85f, 0.65f);   
					MainLight.LightEnergy = 1.2f;
					MainLight.RotationDegrees = new Vector3(-25, 60, 0); 
					env.BackgroundColor = new Color(0.8f, 0.5f, 0.4f); 
					env.AmbientLightColor = new Color(0.9f, 0.6f, 0.7f); 
					env.AmbientLightEnergy = 0.4f;
					env.FogLightColor = env.BackgroundColor; 
					env.VolumetricFogAlbedo = new Color(0.9f, 0.6f, 0.5f); // Dusty, glowing sunrise air
					break;
					
				case LightingMood.Night:
					MainLight.Visible = true;
					// Shifted to a slightly brighter, silvery-blue moonlight
					MainLight.LightColor = new Color(0.5f, 0.6f, 0.9f);  
					MainLight.LightEnergy = 0.6f; // Increased from 0.3f so the highlights pop more
					MainLight.RotationDegrees = new Vector3(-60, -30, 0);
					
					// Lifted the crushed blacks just a tiny bit
					env.BackgroundColor = new Color(0.08f, 0.08f, 0.12f); 
					
					// THIS IS THE MAGIC: A richer ambient blue with much higher energy
					// It fills in the pitch-black shadows so you can actually see your units!
					env.AmbientLightColor = new Color(0.2f, 0.3f, 0.5f); 
					env.AmbientLightEnergy = 0.8f; // Increased from 0.2f!
					
					env.FogLightColor = env.BackgroundColor; 
					// Lightened the fog slightly so it feels like glowing mist
					env.VolumetricFogAlbedo = new Color(0.15f, 0.15f, 0.25f); 
					break;
					
				case LightingMood.Indoors:
					MainLight.Visible = false; 
					env.BackgroundColor = new Color(0.02f, 0.02f, 0.02f); 
					env.AmbientLightColor = new Color(0.8f, 0.7f, 0.5f); 
					env.AmbientLightEnergy = 0.8f;
					env.FogLightColor = env.BackgroundColor;
					env.VolumetricFogAlbedo = new Color(0.05f, 0.04f, 0.03f); 
					env.VolumetricFogDensity = 0.08f; // Very smoky indoors
					break;
			}
		}

		// 2. === PROCEDURAL TEXTURES ===
		StandardMaterial3D groundMaterial = new StandardMaterial3D();
		
		FastNoiseLite noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.03f };
		NoiseTexture2D normalTex = new NoiseTexture2D { Noise = noise, AsNormalMap = true, BumpStrength = 3.0f };
		
		groundMaterial.NormalEnabled = true;
		groundMaterial.NormalTexture = normalTex;

		switch (data.Ground)
		{
			case GroundType.Grass:
				groundMaterial.AlbedoColor = new Color(0.2f, 0.35f, 0.15f); 
				groundMaterial.Roughness = 0.95f; 
				break;
			case GroundType.Dirt:
				groundMaterial.AlbedoColor = new Color(0.3f, 0.22f, 0.15f); 
				groundMaterial.Roughness = 1.0f; 
				break;
			case GroundType.Marble:
				groundMaterial.AlbedoColor = new Color(0.85f, 0.85f, 0.9f); 
				groundMaterial.Roughness = 0.15f; 
				break;
		}

		// 3. === APPLY TO GRID ===
		foreach (var tile in _grid.Values)
		{
			if (tile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D") is MeshInstance3D mesh)
			{
				mesh.MaterialOverride = null; 
				mesh.SetSurfaceOverrideMaterial(0, (StandardMaterial3D)groundMaterial.Duplicate());
			}
		}
		
		// 4. === THE "INFINITE" GROUND PLANE ===
		if (_backgroundPlane == null)
		{
			_backgroundPlane = new MeshInstance3D();
			
			PlaneMesh plane = new PlaneMesh { Size = new Vector2(300, 300) }; // Made even bigger to guarantee it hits the fog horizon!
			_backgroundPlane.Mesh = plane;
			
			float centerWidth = (GridWidth * TileSize) / 2f;
			float centerDepth = (GridDepth * TileSize) / 2f;
			_backgroundPlane.Position = new Vector3(centerWidth, -0.05f, centerDepth);
			
			AddChild(_backgroundPlane);
		}

		StandardMaterial3D planeMat = (StandardMaterial3D)groundMaterial.Duplicate();
		planeMat.Uv1Scale = new Vector3(150, 150, 1);
		_backgroundPlane.MaterialOverride = planeMat;
	}
	
	private async Task SetupGridElevation(BattleSetup data)
	{
		FastNoiseLite noise = new FastNoiseLite { 
			NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, 
			Seed = (int)GD.Randi(), // Naturally fulfills your request for random terrain per battle!
			Frequency = 0.15f 
		};

		Tween riseTween = CreateTween();
		riseTween.SetParallel(true);
		bool rising = false;

		foreach (var kvp in _grid)
		{
			Vector2I pos = kvp.Key;
			Tile tile = kvp.Value;
			
			float targetHeight = 0.01f;

			if (data.ElevationEnabled)
			{
				int distX = Mathf.Min(pos.X, GridWidth - 1 - pos.X);
				int distZ = Mathf.Min(pos.Y, GridDepth - 1 - pos.Y);
				int distToEdge = Mathf.Min(distX, distZ);

				if (distToEdge > 0)
				{
					float rawNoise = (noise.GetNoise2D(pos.X, pos.Y) + 1f) / 2f; 
					float falloff = distToEdge == 1 ? 0.4f : 1.0f; 
					
					targetHeight = 0.01f + (rawNoise * 0.7f * falloff); 
				}
			}

			// === NEW: Animate the rise instead of snapping instantly! ===
			if (Mathf.Abs(tile.Position.Y - targetHeight) > 0.001f)
			{
				rising = true;
				float delay = (float)GD.RandRange(0.0f, 0.2f);
				riseTween.TweenProperty(tile, "position:y", targetHeight, 0.5f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out).SetDelay(delay);
			}
		}

		if (rising)
		{
			// Wait for the terrain to finish shifting so units don't spawn incorrectly
			await ToSignal(GetTree().CreateTimer(0.7f), "timeout");
		}
		else
		{
			riseTween.Kill(); // <--- THE FIX: Destroy the empty tween!
		}
	}
	
	private void SpawnDioramas()
	{
		if (BackgroundDioramas == null || BackgroundDioramas.Count == 0) return;

		float gridSize = GridWidth * TileSize; // 10 * 2 = 20 world units
		float gap = 4.0f; // The space between the edge of the grid and the slot
		float offset = gridSize + gap; // Distance from the center of the grid to the center of the slot

		// The true mathematical center of the grid
		Vector3 center = new Vector3((GridWidth - 1) * TileSize / 2f, 0, (GridDepth - 1) * TileSize / 2f);

		// The 4 compass directions around the main grid
		Vector3[] slotPositions = {
			center + new Vector3(0, 0, -offset), // North
			center + new Vector3(0, 0, offset),  // South
			center + new Vector3(offset, 0, 0),  // East
			center + new Vector3(-offset, 0, 0)  // West
		};

		// === THE FIX: The "Theater Stage" Effect ===
		// Find out where the camera is looking from (ignoring height)
		Vector3 camDir = (Cam.GlobalPosition - center);
		camDir.Y = 0; 
		camDir = camDir.Normalized();

		List<Vector3> validSlots = new List<Vector3>();
		foreach (Vector3 pos in slotPositions)
		{
			Vector3 slotDir = (pos - center).Normalized();
			
			// Dot product tells us if the slot is pointing toward the camera.
			// If it's < 0.2f, the slot is safely to the side or behind the board!
			if (slotDir.Dot(camDir) < 0.2f) 
			{
				validSlots.Add(pos);
			}
		}

		System.Random rnd = new System.Random();

		foreach (Vector3 pos in validSlots)
		{
			// Grab a random 3D model from the Inspector array
			PackedScene randomScene = BackgroundDioramas[rnd.Next(BackgroundDioramas.Count)];
			Node3D diorama = randomScene.Instantiate<Node3D>();
			AddChild(diorama);
			
			// === DYNAMIC AUTO-SCALING ===
			// Look through all meshes inside the 3D object to find its true boundaries
			Aabb bounds = new Aabb();
			bool first = true;
			
			// Godot 4 C# requires 'true, false' for recursive and owned search flags
			foreach (Node child in diorama.FindChildren("*", "MeshInstance3D", true, false))
			{
				if (child is MeshInstance3D meshInstance)
				{
					// Godot 4 lets us multiply a Transform3D by an Aabb directly!
					Aabb transformedAabb = meshInstance.Transform * meshInstance.GetAabb();
					
					if (first) 
					{ 
						bounds = transformedAabb; 
						first = false; 
					}
					else 
					{
						bounds = bounds.Merge(transformedAabb);
					}
				}
			}

			// Find the longest base dimension (Width or Depth)
			float maxBaseDimension = Mathf.Max(bounds.Size.X, bounds.Size.Z);
			if (maxBaseDimension == 0) maxBaseDimension = 1f; // Prevent dividing by zero just in case

			// Scale it so the longest dimension perfectly matches the GridSize
			float targetScaleFactor = gridSize / maxBaseDimension;
			Vector3 targetScale = new Vector3(targetScaleFactor, targetScaleFactor, targetScaleFactor);

			// Position it and prepare for the juice
			diorama.Position = pos;
			diorama.Scale = Vector3.Zero;
			_activeDioramas.Add(diorama);

			// Juiciness: Pop them in with a slight random delay for a cascading build-up
			float delay = (float)GD.RandRange(0.0f, 0.4f);
			CreateTween().TweenProperty(diorama, "scale", targetScale, 0.7f)
				.SetTrans(Tween.TransitionType.Back)
				.SetEase(Tween.EaseType.Out)
				.SetDelay(delay);
		}
	}
}
