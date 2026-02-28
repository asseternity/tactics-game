using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	private List<Unit> _units = new();
	private Unit _selectedUnit;
	private Unit _previewedEnemy;
	private Unit _hoveredUnit;
	private List<Tile> _movementHighlightTiles = new();

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
			SpawnUnit(new PersistentUnit(_unitDatabase[e.ProfileId]), false, finalPos);
		}
		
		SpawnRandomObstacles(new System.Random().Next(8, 16));

		_currentTurnNumber = 1;
		_activeMidBattleEvents = new List<MidBattleEvent>(data.MidBattleEvents);
		
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
		CreateTween().TweenProperty(u, "scale", Vector3.One, 0.35f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
	}

	private async Task PerformAttackAsync(Unit attacker, Unit target)
	{
		await attacker.FaceDirection(target.GlobalPosition);

		Vector3 startPos = attacker.GlobalPosition;
		Vector3 attackDirection = (target.GlobalPosition - startPos).Normalized();
		Vector3 lungePos = startPos + (attackDirection * 0.8f);

		Tween tween = CreateTween();
		tween.TweenProperty(attacker, "global_position", lungePos, 0.1f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		tween.TweenProperty(attacker, "global_position", startPos, 0.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);

		await ToSignal(GetTree().CreateTimer(0.1f), "timeout");

		int damage = GD.RandRange(attacker.GetMinDamage(), attacker.GetMaxDamage());
		
		if (attacker.GlobalPosition.Y > target.GlobalPosition.Y + 0.15f) 
		{
			damage += 1;
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
		Input.SetCustomMouseCursor(null);
		_units.Remove(deadUnit); 

		bool enemiesAlive = _units.Any(u => IsInstanceValid(u) && !u.IsFriendly);
		if (!enemiesAlive)
		{
			_currentState = State.Cutscene; 
			ClearMovementRange();
			ClearHover();
			DeselectUnit();
			ShowActions(false);

			await ToSignal(GetTree().CreateTimer(0.4f), "timeout");
			await ClearBoardAsync(); 
			AdvanceScript(); 
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
		ClearAttackPreview();
		Input.SetCustomMouseCursor(null);
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
			ClearMovementRange();

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
		Unit newlyHoveredUnit = null;

		if (collider.GetParent() is Unit u) 
		{
			newlyHoveredUnit = u;
			Vector2I gridPos = new Vector2I(Mathf.RoundToInt(u.GlobalPosition.X / TileSize), Mathf.RoundToInt(u.GlobalPosition.Z / TileSize));
			if (_grid.TryGetValue(gridPos, out Tile underlyingTile)) targetTile = underlyingTile;
		}
		else if (collider.GetParent() is Tile tile) targetTile = tile;

		if (newlyHoveredUnit != _hoveredUnit)
		{
			if (_hoveredUnit != null && IsInstanceValid(_hoveredUnit)) 
				_hoveredUnit.SetHovered(false);
			
			ClearAttackPreview();
			_hoveredUnit = newlyHoveredUnit;
			
			if (_hoveredUnit != null)
			{
				bool isTargetable = !_hoveredUnit.IsFriendly && _selectedUnit != null && !_selectedUnit.HasAttacked && 
									(_currentState == State.SelectingAttackTarget || _currentState == State.PlayerTurn) && 
									GetGridDistance(_selectedUnit.GlobalPosition, _hoveredUnit.GlobalPosition) <= _selectedUnit.Data.AttackRange;
				
				_hoveredUnit.SetHovered(true, isTargetable);

				if (isTargetable)
				{
					_previewedEnemy = _hoveredUnit;
					_previewedEnemy.PreviewDamage(_selectedUnit.GetMinDamage());
					
					if (_currentState == State.PlayerTurn && AttackCursorIcon != null)
						Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16));
				}
			}
		}

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
			if (_movementHighlightTiles.Contains(_hoveredTile)) 
				_hoveredTile.SetHighlight(true, new Color(0.35f, 0.72f, 1.0f, 0.28f));
			else 
				_hoveredTile.SetHighlight(false); 
			_hoveredTile = null;
		}
	}

	private async void StartEnemyTurn()
	{
		_currentState = State.EnemyTurn;
		ShowActions(false);
		DeselectUnit();
		ClearMovementRange();
		RefreshTargetIcons();
		if (StatsLabel != null) StatsLabel.Text = "Enemy Turn...";

		_ = ShowTurnAnnouncer("ENEMY TURN", new Color(1.0f, 0.2f, 0.2f));
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

			Vector2I enemyCoords = new Vector2I(Mathf.RoundToInt(enemy.GlobalPosition.X / TileSize), Mathf.RoundToInt(enemy.GlobalPosition.Z / TileSize));
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
				await enemy.MoveAlongPath(path);
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
		
		_currentTurnNumber++;
		CheckMidBattleEvents();
	}

	private void OnEndTurnPressed() 
	{ 
		if (_currentState != State.PlayerTurn || _levelUpActive) return; 
		if (!_units.Any(u => IsInstanceValid(u) && !u.IsFriendly)) return;
		StartEnemyTurn(); 
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
			if (kvp.Key == startCoords) continue;
			if (_grid.TryGetValue(kvp.Key, out Tile tile))
			{
				tile.SetHighlight(true, moveColor);
				_movementHighlightTiles.Add(tile);
			}
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
					u.SetTargetable(true);
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
			if (IsInstanceValid(_previewedEnemy)) _previewedEnemy.ClearPreview(); 
			_previewedEnemy = null; 
		}
		if (_currentState != State.SelectingAttackTarget) Input.SetCustomMouseCursor(null);
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
			float height = _grid.ContainsKey(current) ? _grid[current].Position.Y : 0.01f;
			path.Add(new Vector3(current.X * TileSize, height, current.Y * TileSize));
			current = cameFrom[current];
		}
		path.Reverse(); 
		return path;
	}
}
