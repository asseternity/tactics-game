// GameManager.Combat.cs  — FULL REPLACEMENT
// Key changes from original:
//   1. Initiative queue replaces PlayerTurn/EnemyTurn binary
//   2. PerformAttackAsync resolves combo before dealing damage
//   3. Combo attacks (flanking allies) are animated sequentially
//   4. HandleHover previews the combo visually via ComboVisualizer
//   5. HandleUnitDeath removes from initiative queue
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	// === INITIATIVE ===
	private InitiativeQueue _initiative = new InitiativeQueue();
	private bool _isProcessingTurn = false;

	// === COMBO VISUALIZER ===
	private ComboVisualizer _comboVisualizer;

	// ── existing fields kept ────────────────────────────────────
	private List<Unit> _units = new();
	private Unit _selectedUnit;
	private Unit _previewedEnemy;
	private Unit _hoveredUnit;
	private List<Tile> _movementHighlightTiles = new();
	private Tween _pulseTween;

	// ============================================================
	// BATTLE START
	// ============================================================

	private async void StartBattle(BattleSetup data)
	{
		StatsLabel.Text = "Setting up the board...";
		await ClearBoardAsync();
		ApplyEnvironment(data);
		await SetupGridElevation(data);
		SpawnDioramas();

		foreach (var hero in _party) hero.HealBetweenBattles();

		for (int i = 0; i < Mathf.Min(_party.Count, data.FriendlySpawns.Count); i++)
		{
			Vector3 finalPos = data.FriendlySpawns[i];
			if (_grid.TryGetValue(GetGridPos(finalPos), out Tile tile))
				finalPos = new Vector3(tile.Position.X, tile.Position.Y, tile.Position.Z);
			SpawnUnit(_party[i], true, finalPos);
		}

		foreach (var e in data.Enemies)
		{
			Vector3 finalPos = e.Position;
			if (_grid.TryGetValue(GetGridPos(finalPos), out Tile tile))
				finalPos = new Vector3(tile.Position.X, tile.Position.Y, tile.Position.Z);
			SpawnUnit(new PersistentUnit(GameDatabase.Units[e.ProfileId]), false, finalPos);
		}

		SpawnRandomObstacles(new System.Random().Next(8, 16));

		// Ensure visualizer exists
		if (_comboVisualizer == null)
		{
			_comboVisualizer = new ComboVisualizer();
			AddChild(_comboVisualizer);
		}

		_currentTurnNumber = 1;
		_activeMidBattleEvents = new List<MidBattleEvent>(data.MidBattleEvents);

		// Build initiative queue
		_initiative.Rebuild(_units);

		await ShowTurnAnnouncer("BATTLE START", new Color(1.0f, 0.85f, 0.2f));
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
		CreateTween().TweenProperty(u, "scale", Vector3.One, 0.35f)
			.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
	}

	// ============================================================
	// INITIATIVE TURN FLOW
	// ============================================================

	/// <summary>Called after battle start and after each unit finishes its turn.</summary>
	private async Task ProcessNextTurn()
	{
		if (_isProcessingTurn) return;
		_isProcessingTurn = true;
		
		Unit active = _initiative.Current;

		if (active == null || !GodotObject.IsInstanceValid(active))
		{
			_isProcessingTurn = false;
			return;
		}

		// Refresh turn order UI
		RefreshInitiativeUI();

		if (active.IsFriendly)
		{
			await ShowTurnAnnouncer($"{active.Data.Profile.Name}'s Turn", new Color(0.2f, 0.8f, 1.0f));
			_currentState = State.PlayerTurn;
			_selectedUnit = active;
			active.SetSelected(true);
			ShowActions(true);
			ShowMovementRange(active);
			RefreshTargetIcons();
			UpdateStatsUI();
			// Player takes action; turn ends via EndTurn button or after move+attack
		}
		else
		{
			_currentState = State.EnemyTurn;
			ShowActions(false);
			DeselectUnit();
			if (StatsLabel != null) StatsLabel.Text = $"{active.Data.Profile.Name}'s Turn (Enemy)";
			await RunEnemyUnitTurn(active);
			_isProcessingTurn = false;
			EndCurrentUnitTurn();
		}

		_isProcessingTurn = false;
	}

	/// <summary>Called when the current unit (player or enemy) has finished acting.</summary>
	private void EndCurrentUnitTurn()
	{
		_isProcessingTurn = false;
		if (_selectedUnit != null)
		{
			_selectedUnit.SetSelected(false);
			_selectedUnit.NewTurn();
		}
		_selectedUnit = null;
		_initiative.Advance();
		_currentTurnNumber++;
		CheckMidBattleEvents();
	}

	// ============================================================
	// ATTACK WITH COMBO
	// ============================================================

	private async Task PerformAttackAsync(Unit attacker, Unit target)
	{
		await attacker.FaceDirection(target.GlobalPosition);

		// 1. Resolve combo BEFORE any damage
		ComboResult combo = ComboResolver.Resolve(attacker, target, _units, Inventory);

		// 2. Cinematic zoom to fight center
		Vector3 originalCamPos = Cam.GlobalPosition;
		float originalFov = Cam.Fov;
		float originalSize = Cam.Size;
		Vector3 actionCenter = (attacker.GlobalPosition + target.GlobalPosition) / 2f + new Vector3(0, 1.0f, 0);
		Vector3 forward = -Cam.GlobalTransform.Basis.Z.Normalized();
		Vector3 targetCamPos;

		if (Mathf.Abs(forward.Y) >= 0.0001f)
		{
			float t = (actionCenter.Y - originalCamPos.Y) / forward.Y;
			targetCamPos = new Vector3(
				actionCenter.X - t * forward.X, originalCamPos.Y, actionCenter.Z - t * forward.Z);
		}
		else
		{
			Vector3 toCenter = actionCenter - originalCamPos;
			float t = toCenter.Dot(forward) / forward.LengthSquared();
			Vector3 delta = toCenter - (t * forward); delta.Y = 0;
			targetCamPos = originalCamPos + delta;
		}

		Tween zoomIn = CreateTween().SetParallel(true);
		zoomIn.TweenProperty(Cam, "global_position", targetCamPos, 0.45f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		if (Cam.Projection == Camera3D.ProjectionType.Orthogonal)
			zoomIn.TweenProperty(Cam, "size", originalSize * 0.85f, 0.45f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		else
			zoomIn.TweenProperty(Cam, "fov", originalFov - 10f, 0.45f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

		await ToSignal(zoomIn, "finished");
		await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

		// 3. Show COMBO announcement if there's a hand
		if (combo.HasCombo)
		{
			_comboVisualizer?.ShowComboPreview(combo, Cam);
			await ShowComboHandAnnouncer(combo.HandResult);
		}

		// 4. Execute all attacks from AttackMap: attacker hits primary, allies hit their flanked enemies
		var attackTasks = new List<Task>();

		foreach (var (enemyTarget, attackerList) in combo.AttackMap)
		{
			if (!GodotObject.IsInstanceValid(enemyTarget)) continue;

			foreach (var (attackingUnit, multiplier) in attackerList)
			{
				if (!GodotObject.IsInstanceValid(attackingUnit)) continue;
				attackTasks.Add(ExecuteSingleAttack(attackingUnit, enemyTarget, multiplier));
				await ToSignal(GetTree().CreateTimer(0.05f), "timeout"); // brief stagger
			}
		}

		await Task.WhenAll(attackTasks);

		// 5. Clear visuals
		_comboVisualizer?.ClearVisuals();

		// 6. Camera zoom out
		Tween zoomOut = CreateTween().SetParallel(true);
		zoomOut.TweenProperty(Cam, "global_position", originalCamPos, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		if (Cam.Projection == Camera3D.ProjectionType.Orthogonal)
			zoomOut.TweenProperty(Cam, "size", originalSize, 0.5f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		else
			zoomOut.TweenProperty(Cam, "fov", originalFov, 0.5f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		await ToSignal(zoomOut, "finished");
		await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

		attacker.HasAttacked = true;
		attacker.UpdateVisuals();
		RefreshTargetIcons();
	}

	private async Task ExecuteSingleAttack(Unit attackingUnit, Unit enemyTarget, float multiplier)
	{
		if (!GodotObject.IsInstanceValid(attackingUnit) || !GodotObject.IsInstanceValid(enemyTarget)) return;

		Vector3 attackerStartPos = attackingUnit.GlobalPosition;
		Vector3 attackDir = (enemyTarget.GlobalPosition - attackerStartPos).Normalized();
		Vector3 lungePos = attackerStartPos + (attackDir * 0.8f);

		Tween lunge = CreateTween();
		lunge.TweenProperty(attackingUnit, "global_position", lungePos, 0.1f)
			.SetTrans(Tween.TransitionType.Sine);
		lunge.TweenProperty(attackingUnit, "global_position", attackerStartPos, 0.2f)
			.SetTrans(Tween.TransitionType.Quad);

		await ToSignal(GetTree().CreateTimer(0.1f), "timeout");

		int baseDamage = GD.RandRange(attackingUnit.GetMinDamage(), attackingUnit.GetMaxDamage());
		int finalDamage = Mathf.RoundToInt(baseDamage * multiplier);

		// Tinted float text to show combo multiplier
		Color dmgColor = multiplier > 1.0f ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.2f, 0.2f);
		string dmgText = multiplier > 1.0f ? $"{finalDamage} ×{multiplier:F2}" : finalDamage.ToString();
		SpawnFloatingText(enemyTarget.GlobalPosition, dmgText, dmgColor, multiplier > 1.5f ? 40 : 30);

		var damageTask = enemyTarget.TakeDamage(finalDamage, attackingUnit);

		// Camera shake
		for (int i = 0; i < 5; i++)
		{
			float s = 0.12f;
			Cam.HOffset = (float)GD.RandRange(-s, s);
			Cam.VOffset = (float)GD.RandRange(-s, s);
			await ToSignal(GetTree().CreateTimer(0.03f), "timeout");
		}
		Cam.HOffset = 0; Cam.VOffset = 0;
		await damageTask;
	}

	private async Task ShowComboHandAnnouncer(HandResult result)
	{
		Label announcer = new Label
		{
			Text = result.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		announcer.AddThemeFontSizeOverride("font_size", 72);
		announcer.AddThemeColorOverride("font_color", result.ComboColor);
		announcer.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		announcer.AddThemeConstantOverride("outline_size", 18);
		if (_fantasyFont != null) announcer.AddThemeFontOverride("font", _fantasyFont);
		announcer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(announcer); else AddChild(announcer);

		announcer.PivotOffset = GetViewport().GetVisibleRect().Size / 2;
		announcer.Scale = Vector2.Zero;
		announcer.Modulate = new Color(1, 1, 1, 0);

		Tween tween = CreateTween();
		tween.Parallel().TweenProperty(announcer, "scale", new Vector2(1.1f, 1.1f), 0.3f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 1.0f, 0.2f);
		tween.Chain().TweenInterval(0.8f);
		tween.Chain().TweenProperty(announcer, "scale", new Vector2(1.4f, 1.4f), 0.25f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 0.0f, 0.25f);
		tween.Finished += () => announcer.QueueFree();

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	// ============================================================
	// ENEMY AI TURN (single unit)
	// ============================================================

	private async Task RunEnemyUnitTurn(Unit enemy)
	{
		if (!GodotObject.IsInstanceValid(enemy)) return;

		var players = _units.Where(x => GodotObject.IsInstanceValid(x) && x.IsFriendly).ToList();
		if (players.Count == 0) return;

		Unit bestTarget = null;
		Vector3 bestMovePos = enemy.GlobalPosition;
		float bestScore = -9999f;

		Vector2I enemyCoords = new Vector2I(
			Mathf.RoundToInt(enemy.GlobalPosition.X / TileSize),
			Mathf.RoundToInt(enemy.GlobalPosition.Z / TileSize));
		var reachable = GetReachableTiles(enemyCoords, enemy.Data.Movement);
		reachable[enemyCoords] = enemyCoords;

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
			if (canAttackThisTurn && player.Data.CurrentHP <= enemy.Data.GetTotalDamage()) targetScore += 2000;
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
			Vector2I targetCoords = new Vector2I(
				Mathf.RoundToInt(bestMovePos.X / TileSize),
				Mathf.RoundToInt(bestMovePos.Z / TileSize));
			var path = ExtractPath(reachable, enemyCoords, targetCoords);
			await enemy.MoveAlongPath(path);
		}

		if (bestTarget != null && GodotObject.IsInstanceValid(bestTarget) &&
			GetGridDistance(enemy.GlobalPosition, bestTarget.GlobalPosition) <= enemy.Data.AttackRange)
		{
			await PerformAttackAsync(enemy, bestTarget);
			if (_currentState != State.EnemyTurn) return;
			await ToSignal(GetTree().CreateTimer(0.3f), "timeout");
		}

		enemy.HasMoved = true;
		enemy.HasAttacked = true;
		enemy.UpdateVisuals();
	}

	// ============================================================
	// PLAYER INPUT
	// ============================================================

	private void OnEndTurnPressed()
	{
		if (_currentState != State.PlayerTurn || _levelUpActive) return;
		if (!_units.Any(u => GodotObject.IsInstanceValid(u) && !u.IsFriendly)) return;

		ClearHover();
		_comboVisualizer?.ClearVisuals();
		DeselectUnit();
		EndCurrentUnitTurn();
		_ = ProcessNextTurn();
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

		// === CAMP ===
		if (_currentState == State.Camp)
		{
			if (collider.GetParent() is Unit campUnit)
			{
				string charName = campUnit.Data.Profile.Name;
				var conv = GetAvailableCampConversation(charName);
				if (conv != null)
				{
					campUnit.SetHovered(false);
					TriggerCampConversation(conv);
				}
			}
			return;
		}

		// === COMBAT ===
		if (collider.GetParent() is Unit clickedUnit)
		{
			if (clickedUnit.IsFriendly)
			{
				// Only allow clicking the current initiative unit
				if (clickedUnit == _initiative.Current) SelectUnit(clickedUnit);
				else ShowUnitInfo(clickedUnit);
			}
			else
			{
				bool canSmartAttack = _currentState == State.PlayerTurn
					&& _selectedUnit != null && !_selectedUnit.HasAttacked
					&& GetGridDistance(_selectedUnit.GlobalPosition, clickedUnit.GlobalPosition) <= _selectedUnit.Data.AttackRange;

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
			if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved)
				TryMoveTo(tile.GlobalPosition);
		}
	}

	private async void TryAttackTarget(Unit target)
	{
		if (_selectedUnit == null) return;
		if (GetGridDistance(_selectedUnit.GlobalPosition, target.GlobalPosition) > _selectedUnit.Data.AttackRange) return;

		await PerformAttackAsync(_selectedUnit, target);
		if (_selectedUnit == null || !GodotObject.IsInstanceValid(_selectedUnit)) return;

		CancelAttackMode();
		UpdateStatsUI();
		ShowActions(true);

		// Auto-end turn if fully exhausted
		if (_selectedUnit.HasMoved && _selectedUnit.HasAttacked)
		{
			EndCurrentUnitTurn();
			_ = ProcessNextTurn();
		}
	}

	private void SelectUnit(Unit u)
	{
		if (u.HasMoved && u.HasAttacked) { ShowUnitInfo(u); return; }
		if (_selectedUnit != null && _selectedUnit != u) _selectedUnit.SetSelected(false);
		_selectedUnit = u;
		_selectedUnit.SetSelected(true);

		Node3D sprite = _selectedUnit.GetNode<Node3D>("Sprite3D");
		if (!sprite.HasMeta("BaseScale")) sprite.SetMeta("BaseScale", sprite.Scale);
		Vector3 baseScale = sprite.GetMeta("BaseScale").AsVector3();
		Tween tween = CreateTween();
		tween.TweenProperty(sprite, "scale", new Vector3(baseScale.X * 0.8f, baseScale.Y * 1.3f, baseScale.Z * 0.8f), 0.1f);
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
		ClearAttackPreview();
		Input.SetCustomMouseCursor(null);
	}

	private async void TryMoveTo(Vector3 targetPos)
	{
		if (_selectedUnit == null) return;
		Vector2I startCoords = new Vector2I(
			Mathf.RoundToInt(_selectedUnit.GlobalPosition.X / TileSize),
			Mathf.RoundToInt(_selectedUnit.GlobalPosition.Z / TileSize));
		Vector2I targetCoords = new Vector2I(
			Mathf.RoundToInt(targetPos.X / TileSize),
			Mathf.RoundToInt(targetPos.Z / TileSize));
		var reachable = GetReachableTiles(startCoords, _selectedUnit.Data.Movement);

		if (!reachable.ContainsKey(targetCoords)) return;

		var path = ExtractPath(reachable, startCoords, targetCoords);
		ShowActions(false);
		RefreshTargetIcons();
		ClearMovementRange();

		await _selectedUnit.MoveAlongPath(path);

		if (_selectedUnit != null)
		{
			CheckAutoExhaust(_selectedUnit);
			_selectedUnit.UpdateVisuals();
			UpdateStatsUI();
			ShowActions(true);
			RefreshTargetIcons();

			if (_selectedUnit.HasMoved && _selectedUnit.HasAttacked)
			{
				EndCurrentUnitTurn();
				_ = ProcessNextTurn();
			}
		}
	}

	private void HandleHover(Vector2 mousePos)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var from = Cam.ProjectRayOrigin(mousePos);
		var to = from + Cam.ProjectRayNormal(mousePos) * 1000;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithAreas = true;
		var result = spaceState.IntersectRay(query);

		if (result.Count == 0) { ClearHover(); ClearAttackPreview(); return; }

		Node collider = (Node)result["collider"];
		Tile targetTile = null;
		Unit newlyHoveredUnit = null;

		if (collider.GetParent() is Unit u)
		{
			newlyHoveredUnit = u;
			Vector2I gridPos = new Vector2I(
				Mathf.RoundToInt(u.GlobalPosition.X / TileSize),
				Mathf.RoundToInt(u.GlobalPosition.Z / TileSize));
			if (_grid.TryGetValue(gridPos, out Tile underlyingTile)) targetTile = underlyingTile;
		}
		else if (collider.GetParent() is Tile tile) targetTile = tile;

		if (newlyHoveredUnit != _hoveredUnit)
		{
			if (_hoveredUnit != null && GodotObject.IsInstanceValid(_hoveredUnit))
				_hoveredUnit.SetHovered(false);
			ClearAttackPreview();
			_comboVisualizer?.ClearVisuals();
			_hoveredUnit = newlyHoveredUnit;

			if (_hoveredUnit != null)
			{
				if (_currentState == State.Camp)
				{
					var conv = GetAvailableCampConversation(_hoveredUnit.Data.Profile.Name);
					if (conv != null)
					{
						_hoveredUnit.SetHovered(true);
						if (AttackCursorIcon != null)
							Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16));
					}
				}
				else
				{
					bool isTargetable = !_hoveredUnit.IsFriendly && _selectedUnit != null
						&& !_selectedUnit.HasAttacked
						&& (_currentState == State.SelectingAttackTarget || _currentState == State.PlayerTurn)
						&& GetGridDistance(_selectedUnit.GlobalPosition, _hoveredUnit.GlobalPosition) <= _selectedUnit.Data.AttackRange;

					_hoveredUnit.SetHovered(true, isTargetable);

					if (isTargetable)
					{
						_previewedEnemy = _hoveredUnit;
						_previewedEnemy.PreviewDamage(_selectedUnit.GetMinDamage());

						// === COMBO PREVIEW ===
						ComboResult preview = ComboResolver.Resolve(_selectedUnit, _hoveredUnit, _units, Inventory);
						_comboVisualizer?.ShowComboPreview(preview, Cam);

						if (_currentState == State.PlayerTurn && AttackCursorIcon != null)
							Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16));
					}
				}
			}
		}

		if (targetTile != _hoveredTile)
		{
			ClearHover();
			_hoveredTile = targetTile;
			if (_hoveredTile != null && _currentState == State.PlayerTurn
				&& _selectedUnit != null && !_selectedUnit.HasMoved
				&& _movementHighlightTiles.Contains(_hoveredTile))
				_hoveredTile.SetHighlight(true, new Color(0f, 1f, 0f, 0.7f));
		}
	}

	// ============================================================
	// DEATH HANDLER
	// ============================================================

	private async void HandleUnitDeath(Unit deadUnit)
	{
		Input.SetCustomMouseCursor(null);
		_units.Remove(deadUnit);
		_initiative.Remove(deadUnit);

		bool enemiesAlive = _units.Any(u => GodotObject.IsInstanceValid(u) && !u.IsFriendly);
		if (!enemiesAlive)
		{
			_currentState = State.Cutscene;
			ClearMovementRange();
			ClearHover();
			DeselectUnit();
			ShowActions(false);
			_comboVisualizer?.ClearVisuals();

			await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
			await ClearBoardAsync();
			AdvanceScript();
		}
	}

	// ============================================================
	// INITIATIVE UI
	// ============================================================

	private Control _initiativeBar;

	private void RefreshInitiativeUI()
	{
		if (_initiativeBar == null) BuildInitiativeBar();
		if (_initiativeBar == null) return;

		// Kill the old pulse tween before wiping the children it targets
		if (_pulseTween != null && _pulseTween.IsValid())
		{
			_pulseTween.Kill();
			_pulseTween = null;
		}

		foreach (Node child in _initiativeBar.GetChildren()) child.QueueFree();

		var order = _initiative.AllInOrder();
		int currentIdx = order.IndexOf(_initiative.Current);

		for (int i = 0; i < Mathf.Min(order.Count, 8); i++)
		{
			int idx = (currentIdx + i) % Mathf.Max(1, order.Count);
			if (idx < 0 || idx >= order.Count) continue;
			Unit u = order[idx];
			if (!GodotObject.IsInstanceValid(u)) continue;

			bool isCurrent = i == 0;
			PanelContainer pip = new PanelContainer
			{
				CustomMinimumSize = new Vector2(isCurrent ? 70 : 50, isCurrent ? 70 : 50)
			};

			Color borderColor = u.IsFriendly
				? new Color(0.3f, 0.8f, 1.0f)
				: new Color(1.0f, 0.3f, 0.3f);
			if (isCurrent) borderColor = new Color(1.0f, 0.9f, 0.2f);

			pip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
			{
				BgColor = new Color(0.08f, 0.08f, 0.1f, 0.9f),
				CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
				CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
				BorderWidthBottom = isCurrent ? 4 : 2,
				BorderWidthTop    = isCurrent ? 4 : 2,
				BorderWidthLeft   = isCurrent ? 4 : 2,
				BorderWidthRight  = isCurrent ? 4 : 2,
				BorderColor = borderColor,
				ShadowSize  = isCurrent ? 8 : 4
			});

			VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			pip.AddChild(vbox);

			Label nameLabel = new Label
			{
				Text = u.Data.Profile.Name.Substring(0, Mathf.Min(4, u.Data.Profile.Name.Length)),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			nameLabel.AddThemeFontSizeOverride("font_size", isCurrent ? 14 : 11);
			nameLabel.AddThemeColorOverride("font_color", borderColor);

			string cardStr = u.Data.CardRank == CardRank.None
				? "—"
				: $"{SuitSymbol(u.Data.CardSuit)}{u.Data.CardRank.DisplayName()}";
			Label cardLabel = new Label
			{
				Text = cardStr,
				HorizontalAlignment = HorizontalAlignment.Center
			};
			cardLabel.AddThemeFontSizeOverride("font_size", 10);
			cardLabel.AddThemeColorOverride("font_color", SuitColor(u.Data.CardSuit));

			vbox.AddChild(nameLabel);
			vbox.AddChild(cardLabel);
			_initiativeBar.AddChild(pip);

			// Only ONE looping tween, stored so we can kill it next refresh
			if (isCurrent)
			{
				pip.PivotOffset = pip.CustomMinimumSize / 2;
				_pulseTween = CreateTween().SetLoops();
				_pulseTween.TweenProperty(pip, "scale", new Vector2(1.08f, 1.08f), 0.5f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				_pulseTween.TweenProperty(pip, "scale", Vector2.One, 0.5f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			}
		}
	}

	private void BuildInitiativeBar()
	{
		_initiativeBar = new HBoxContainer();
		_initiativeBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_initiativeBar.Position = new Vector2(0, 10);
		_initiativeBar.AddThemeConstantOverride("separation", 8);
		_initiativeBar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;

		if (DimOverlay != null) DimOverlay.GetParent().AddChild(_initiativeBar);
		else AddChild(_initiativeBar);
	}

	private static Color SuitColor(CardSuit suit) => suit switch
	{
		CardSuit.Hearts   => new Color(0.9f, 0.2f, 0.2f),
		CardSuit.Diamonds => new Color(0.9f, 0.2f, 0.2f),
		CardSuit.Clubs    => new Color(0.7f, 0.7f, 0.7f),
		CardSuit.Spades   => new Color(0.7f, 0.7f, 0.7f),
		_                 => Colors.Gray
	};

	private static string SuitSymbol(CardSuit suit) => suit switch
	{
		CardSuit.Hearts   => "♥",
		CardSuit.Diamonds => "♦",
		CardSuit.Clubs    => "♣",
		CardSuit.Spades   => "♠",
		_                 => "?"
	};

	// ============================================================
	// UNCHANGED HELPERS (kept from original)
	// ============================================================

	private void CheckAutoExhaust(Unit u)
	{
		if (!u.IsFriendly || u.HasAttacked) return;
		bool enemyInRange = _units.Any(enemy =>
			!enemy.IsFriendly && GodotObject.IsInstanceValid(enemy)
			&& GetGridDistance(u.GlobalPosition, enemy.GlobalPosition) <= u.Data.AttackRange);
		if (!enemyInRange) { u.HasAttacked = true; u.UpdateVisuals(); }
	}

	private int GetGridDistance(Vector3 posA, Vector3 posB)
	{
		int ax = Mathf.RoundToInt(posA.X / TileSize), az = Mathf.RoundToInt(posA.Z / TileSize);
		int bx = Mathf.RoundToInt(posB.X / TileSize), bz = Mathf.RoundToInt(posB.Z / TileSize);
		return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(az - bz));
	}

	private void RefreshTargetIcons()
	{
		foreach (var u in _units)
		{
			if (!GodotObject.IsInstanceValid(u)) continue;
			u.SetTargetable(false);
			if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasAttacked)
				if (!u.IsFriendly && GetGridDistance(_selectedUnit.GlobalPosition, u.GlobalPosition) <= _selectedUnit.Data.AttackRange)
					u.SetTargetable(true);
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
			if (GodotObject.IsInstanceValid(_previewedEnemy)) _previewedEnemy.ClearPreview();
			_previewedEnemy = null;
		}
		if (_currentState != State.SelectingAttackTarget) Input.SetCustomMouseCursor(null);
	}

	private void ClearHover()
	{
		if (_hoveredTile != null)
		{
			if (_movementHighlightTiles.Contains(_hoveredTile))
				_hoveredTile.SetHighlight(true, new Color(0.35f, 0.72f, 1.0f, 0.28f));
			else
				_hoveredTile.SetHighlight(false);
			_hoveredTile = null;
		}
	}

	private void ShowMovementRange(Unit u)
	{
		ClearMovementRange();
		if (u == null || u.HasMoved) return;
		Vector2I startCoords = new Vector2I(
			Mathf.RoundToInt(u.GlobalPosition.X / TileSize),
			Mathf.RoundToInt(u.GlobalPosition.Z / TileSize));
		var reachable = GetReachableTiles(startCoords, u.Data.Movement);
		Color moveColor = new Color(0.35f, 0.72f, 1.0f, 0.28f);
		foreach (var kvp in reachable)
		{
			if (kvp.Key == startCoords) continue;
			if (_grid.TryGetValue(kvp.Key, out Tile tile))
			{
				tile.SetHighlight(true, moveColor);
				_movementHighlightTiles.Add(tile);
			}
		}
	}

	private System.Collections.Generic.Dictionary<Vector2I, Vector2I> GetReachableTiles(Vector2I start, int maxMovement)
	{
		var frontier = new System.Collections.Generic.Queue<Vector2I>();
		var cameFrom = new System.Collections.Generic.Dictionary<Vector2I, Vector2I>();
		var costSoFar = new System.Collections.Generic.Dictionary<Vector2I, int>();
		frontier.Enqueue(start);
		cameFrom[start] = start;
		costSoFar[start] = 0;
		Vector2I[] directions = {
			new Vector2I(1,0), new Vector2I(-1,0), new Vector2I(0,1), new Vector2I(0,-1),
			new Vector2I(1,1), new Vector2I(-1,-1), new Vector2I(1,-1), new Vector2I(-1,1)
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

	private System.Collections.Generic.List<Vector3> ExtractPath(
		System.Collections.Generic.Dictionary<Vector2I, Vector2I> cameFrom, Vector2I start, Vector2I end)
	{
		var path = new System.Collections.Generic.List<Vector3>();
		var current = end;
		while (current != start)
		{
			float height = _grid.ContainsKey(current) ? _grid[current].Position.Y : 0.01f;
			path.Add(new Vector3(current.X * TileSize, height, current.Y * TileSize));
			current = cameFrom[current];
		}
		path.Reverse();
		return path;
	}
}
