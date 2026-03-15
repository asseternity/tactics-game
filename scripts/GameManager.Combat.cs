// GameManager.Combat.cs — Fixed: turn counting, double ProcessNextTurn, mid-battle events
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	private InitiativeQueue _initiative = new InitiativeQueue();
	private bool _isProcessingTurn = false;
	private ComboVisualizer _comboVisualizer;

	private List<Unit> _units = new();
	private Unit _selectedUnit;
	private Unit _previewedEnemy;
	private Unit _hoveredUnit;
	private List<Tile> _movementHighlightTiles = new();
	private Tween _pulseTween;
	private bool _uiBounced = false;

	// === UI BOUNCE ===
	private void BounceUIAway()
	{
		if (_uiBounced) return; _uiBounced = true;
		if (ActionMenu != null && ActionMenu.Visible) { ActionMenu.PivotOffset = ActionMenu.Size / 2; CreateTween().TweenProperty(ActionMenu, "scale", new Vector2(0.001f, 0.001f), 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); }
		if (_statsPanel != null && _statsPanel.Visible) { _statsPanel.PivotOffset = _statsPanel.Size / 2; CreateTween().TweenProperty(_statsPanel, "scale", new Vector2(0.001f, 0.001f), 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); }
	}
	private void BounceUIBack()
	{
		if (!_uiBounced) return; _uiBounced = false;
		if (ActionMenu != null) { ActionMenu.Visible = true; ActionMenu.PivotOffset = ActionMenu.Size / 2; CreateTween().TweenProperty(ActionMenu, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); }
		if (_statsPanel != null) { _statsPanel.Visible = true; _statsPanel.PivotOffset = _statsPanel.Size / 2; CreateTween().TweenProperty(_statsPanel, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); }
	}

	private async void StartBattle(BattleSetup data)
	{
		StatsLabel.Text = ""; 
		
		// Use the new unified cleanup method
		await ClearEnvironmentSceneryAsync(); 
		
		ApplyEnvironment(data); 
		
		// Use the new unified juicy spawner (handles Grid, Dioramas, AND Outer Wilds)
		await BuildEnvironmentSceneryAsync(data); 
		
		foreach (var h in _party) h.HealBetweenBattles();
		for (int i = 0; i < Mathf.Min(_party.Count, data.FriendlySpawns.Count); i++)
		{ 
			SpawnUnit(_party[i], true, SnapToGridCenter(data.FriendlySpawns[i])); 
		}
		foreach (var e in data.Enemies)
		{ 
			SpawnUnit(new PersistentUnit(GameDatabase.Units[e.ProfileId]), false, SnapToGridCenter(e.Position)); 
		}
		
		SpawnRandomObstacles(new System.Random().Next(30, 50)); // Significantly denser obstacles
		
		if (_comboVisualizer == null) { _comboVisualizer = new ComboVisualizer(); AddChild(_comboVisualizer); }

		_activeMidBattleEvents = new List<MidBattleEvent>(data.MidBattleEvents);
		_initiative.Rebuild(_units);

		if (_initiativeBar != null) _initiativeBar.Visible = true;
		await ShowTurnAnnouncer("BATTLE START", new Color(1f, 0.85f, 0.2f));
		CheckMidBattleEvents();
	}
	
	private void SpawnUnit(PersistentUnit data, bool isFriendly, Vector3 pos)
	{
		Unit u = UnitScene.Instantiate<Unit>(); AddChild(u); u.GlobalPosition = pos; u.Setup(data, isFriendly);
		u.OnDied += HandleUnitDeath; _units.Add(u);
		u.Scale = new Vector3(0.001f, 0.001f, 0.001f);
		CreateTween().TweenProperty(u, "scale", Vector3.One, 0.35f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
	}

	// === TURN FLOW ===
	private async Task ProcessNextTurn()
	{
		if (_isProcessingTurn) return; _isProcessingTurn = true;
		Unit active = _initiative.Current;
		if (active == null || !GodotObject.IsInstanceValid(active)) { _isProcessingTurn = false; return; }
		RefreshInitiativeUI();
		if (active.IsFriendly)
		{
			_currentState = State.PlayerTurn; _selectedUnit = active; active.SetSelected(true);
			UpdateStatsUI();
			await ShowTurnAnnouncer($"{active.Data.Profile.Name}'s Turn", new Color(0.2f, 0.8f, 1.0f));
			UpdateStatsUI(); ShowActions(true); BounceUIBack(); ShowMovementRange(active); RefreshTargetIcons();
			_isProcessingTurn = false;
		}
		else
		{
			_currentState = State.EnemyTurn; BounceUIAway(); ShowActions(false); DeselectUnit();
			await RunEnemyUnitTurn(active);
			_isProcessingTurn = false; EndCurrentUnitTurn(); return;
		}
	}

	private void EndCurrentUnitTurn()
	{
		_isProcessingTurn = false;
		Unit cur = _initiative.Current;
		if (cur != null && GodotObject.IsInstanceValid(cur)) { cur.SetSelected(false); cur.NewTurn(); }
		if (_selectedUnit != null) { _selectedUnit.SetSelected(false); _selectedUnit = null; }
		UpdateStatsUI();

		// FIX: Advance the initiative queue. Round increments automatically when all units have acted.
		_initiative.Advance();

		// FIX: Check mid-battle events using _initiative.Round (per-ROUND, not per-unit).
		// This replaces the old _currentTurnNumber++ which fired events way too early.
		CheckMidBattleEvents();
	}

	// === ATTACK ===
	private async Task PerformAttackAsync(Unit attacker, Unit target)
	{
		BounceUIAway(); attacker.HasAttacked = true; attacker.UpdateVisuals();
		await attacker.FaceDirection(target.GlobalPosition);
		ComboResult combo = ComboResolver.Resolve(attacker, target, _units, Inventory);

		Vector3 oCam = Cam.GlobalPosition; float oFov = Cam.Fov; float oSize = Cam.Size;
		Vector3 center = (attacker.GlobalPosition + target.GlobalPosition) / 2f + Vector3.Up;
		Vector3 fwd = -Cam.GlobalTransform.Basis.Z.Normalized(); Vector3 tCam;
		if (Mathf.Abs(fwd.Y) >= 0.0001f) { float t2 = (center.Y - oCam.Y) / fwd.Y; tCam = new Vector3(center.X - t2 * fwd.X, oCam.Y, center.Z - t2 * fwd.Z); }
		else { Vector3 tc2 = center - oCam; float t2 = tc2.Dot(fwd) / fwd.LengthSquared(); Vector3 d = tc2 - t2 * fwd; d.Y = 0; tCam = oCam + d; }

		Tween zi = CreateTween().SetParallel(true);
		zi.TweenProperty(Cam, "global_position", tCam, 0.45f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		if (Cam.Projection == Camera3D.ProjectionType.Orthogonal) zi.TweenProperty(Cam, "size", oSize * 0.85f, 0.45f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		else zi.TweenProperty(Cam, "fov", oFov - 10f, 0.45f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		await ToSignal(zi, "finished"); await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

		bool showVis = combo.HasCombo || combo.AllFlankingAllies.Count > 0;
		if (showVis) { _comboVisualizer?.ShowComboPreview(combo, Cam, attacker); if (combo.HasCombo) await ShowComboHandAnnouncer(combo.HandResult); }

		foreach (var (et, al) in combo.AttackMap)
		{ if (!GodotObject.IsInstanceValid(et)) continue; foreach (var (au, m) in al) { if (!GodotObject.IsInstanceValid(au)) continue; await ExecuteSingleAttack(au, et, m); await ToSignal(GetTree().CreateTimer(0.05f), "timeout"); } }

		_comboVisualizer?.ClearVisuals();
		if (!GodotObject.IsInstanceValid(attacker)) return;

		Tween zo = CreateTween().SetParallel(true);
		zo.TweenProperty(Cam, "global_position", oCam, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		if (Cam.Projection == Camera3D.ProjectionType.Orthogonal) zo.TweenProperty(Cam, "size", oSize, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		else zo.TweenProperty(Cam, "fov", oFov, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		await ToSignal(zo, "finished"); await ToSignal(GetTree().CreateTimer(0.2f), "timeout");

		if (!GodotObject.IsInstanceValid(attacker)) return;
		attacker.UpdateVisuals(); UpdateStatsUI(); RefreshTargetIcons(); BounceUIBack();
	}
	private async Task ExecuteSingleAttack(Unit au, Unit et, float m)
	{
		if (!GodotObject.IsInstanceValid(au) || !GodotObject.IsInstanceValid(et)) return;
		Vector3 sp = au.GlobalPosition; Vector3 dir = (et.GlobalPosition - sp).Normalized();
		Tween l = CreateTween(); l.TweenProperty(au, "global_position", sp + dir * 0.8f, 0.1f).SetTrans(Tween.TransitionType.Sine);
		l.TweenProperty(au, "global_position", sp, 0.2f).SetTrans(Tween.TransitionType.Quad);
		await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
		if (!GodotObject.IsInstanceValid(et) || !GodotObject.IsInstanceValid(au)) return;
		int dmg = Mathf.RoundToInt(GD.RandRange(au.GetMinDamage(), au.GetMaxDamage()) * m);
		Color dc = m > 1f ? new Color(1f, 0.85f, 0.1f) : new Color(1f, 0.2f, 0.2f);
		SpawnFloatingText(et.GlobalPosition, m > 1f ? $"{dmg} ×{m:F2}" : dmg.ToString(), dc, m > 1.5f ? 40 : 30);
		await et.TakeDamage(dmg, au);
		if (!GodotObject.IsInstanceValid(au)) return;
		for (int i = 0; i < 5; i++) { Cam.HOffset = (float)GD.RandRange(-0.12f, 0.12f); Cam.VOffset = (float)GD.RandRange(-0.12f, 0.12f); await ToSignal(GetTree().CreateTimer(0.03f), "timeout"); }
		Cam.HOffset = 0; Cam.VOffset = 0;
	}
	private async Task ShowComboHandAnnouncer(HandResult r)
	{
		Label a = new Label { Text = r.DisplayName, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		a.AddThemeFontSizeOverride("font_size", 72); a.AddThemeColorOverride("font_color", r.ComboColor);
		a.AddThemeColorOverride("font_outline_color", Colors.Black); a.AddThemeConstantOverride("outline_size", 18);
		if (_fantasyFont != null) a.AddThemeFontOverride("font", _fantasyFont);
		a.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(a); else AddChild(a);
		a.PivotOffset = GetViewport().GetVisibleRect().Size / 2; a.Scale = Vector2.Zero; a.Modulate = new Color(1, 1, 1, 0);
		Tween tw = CreateTween();
		tw.Parallel().TweenProperty(a, "scale", new Vector2(1.1f, 1.1f), 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tw.Parallel().TweenProperty(a, "modulate:a", 1f, 0.2f);
		tw.Chain().TweenInterval(0.8f);
		tw.Chain().TweenProperty(a, "scale", new Vector2(1.4f, 1.4f), 0.25f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tw.Parallel().TweenProperty(a, "modulate:a", 0f, 0.25f);
		tw.Finished += () => a.QueueFree();
		await ToSignal(tw, Tween.SignalName.Finished);
	}

	// === ENEMY AI ===
	private async Task RunEnemyUnitTurn(Unit enemy)
	{
		if (!GodotObject.IsInstanceValid(enemy)) return;
		var players = _units.Where(x => GodotObject.IsInstanceValid(x) && x.IsFriendly).ToList();
		if (players.Count == 0) return;
		Unit best = null; Vector3 bestPos = enemy.GlobalPosition; float bestScore = -9999f;
		Vector2I ec = new Vector2I(Mathf.RoundToInt(enemy.GlobalPosition.X / TileSize), Mathf.RoundToInt(enemy.GlobalPosition.Z / TileSize));
		var reach = GetReachableTiles(ec, enemy.Data.Movement); reach[ec] = ec;
		foreach (var p in players) {
			float sc = 0; Vector3 opt = enemy.GlobalPosition; bool ca = false; int dist = GetGridDistance(enemy.GlobalPosition, p.GlobalPosition);
			if (dist <= enemy.Data.AttackRange) ca = true;
			else { int cl = dist; foreach (var tp in reach.Keys) { Vector3 wp = new(tp.X * TileSize, 0.01f, tp.Y * TileSize); int d = GetGridDistance(wp, p.GlobalPosition);
				if (d <= enemy.Data.AttackRange) { ca = true; opt = wp; break; } else if (d < cl) { cl = d; opt = wp; } } }
			if (ca) sc += 1000; sc -= p.Data.CurrentHP * 10; if (ca && p.Data.CurrentHP <= enemy.Data.GetTotalDamage()) sc += 2000; sc -= dist;
			if (sc > bestScore) { bestScore = sc; best = p; bestPos = opt; }
		}
		if (bestPos != enemy.GlobalPosition) { var tc = new Vector2I(Mathf.RoundToInt(bestPos.X / TileSize), Mathf.RoundToInt(bestPos.Z / TileSize)); await enemy.MoveAlongPath(ExtractPath(reach, ec, tc)); }
		if (best != null && GodotObject.IsInstanceValid(best) && GetGridDistance(enemy.GlobalPosition, best.GlobalPosition) <= enemy.Data.AttackRange)
		{ await PerformAttackAsync(enemy, best); if (_currentState != State.EnemyTurn) return; await ToSignal(GetTree().CreateTimer(0.3f), "timeout"); }
		if (GodotObject.IsInstanceValid(enemy)) { enemy.HasMoved = true; enemy.HasAttacked = true; enemy.UpdateVisuals(); }
	}

	// === PLAYER INPUT ===

	// FIX: OnEndTurnPressed no longer calls ProcessNextTurn separately.
	// EndCurrentUnitTurn → CheckMidBattleEvents → ProcessNextTurn handles the entire flow.
	// The old code called BOTH EndCurrentUnitTurn AND ProcessNextTurn, causing:
	//   - Double turn processing
	//   - ProcessNextTurn running DURING mid-battle dialogue (→ freeze)
	private void OnEndTurnPressed()
	{
		if (_currentState != State.PlayerTurn || _levelUpActive || _lootScreenActive) return;
		if (!_units.Any(u => GodotObject.IsInstanceValid(u) && !u.IsFriendly)) return;
		ClearHover(); _comboVisualizer?.ClearVisuals();
		EndCurrentUnitTurn();
		// NO ProcessNextTurn here — EndCurrentUnitTurn handles it via CheckMidBattleEvents
	}

	private void HandleClick(Vector2 mp)
	{
		if (_lootScreenActive) return;
		var ss = GetWorld3D().DirectSpaceState; var f = Cam.ProjectRayOrigin(mp); var t = f + Cam.ProjectRayNormal(mp) * 1000;
		var q = PhysicsRayQueryParameters3D.Create(f, t); q.CollideWithAreas = true; var r = ss.IntersectRay(q);
		if (r.Count == 0) { if (_selectedUnit != null && _currentState != State.SelectingAttackTarget) DeselectUnit(); return; }
		Node col = (Node)r["collider"];
		if (_currentState == State.Camp) { if (col.GetParent() is Unit cu) { var cv = GetAvailableCampConversation(cu.Data.Profile.Name); if (cv != null) { cu.SetHovered(false); TriggerCampConversation(cv); } } return; }
		if (col.GetParent() is Unit clicked)
		{
			if (clicked.IsFriendly) { if (clicked == _initiative.Current) SelectUnit(clicked); else ShowUnitInfo(clicked); }
			else { bool csa = _currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasAttacked && GetGridDistance(_selectedUnit.GlobalPosition, clicked.GlobalPosition) <= _selectedUnit.Data.AttackRange;
				if (_currentState == State.SelectingAttackTarget || csa) { ClearAttackPreview(); TryAttackTarget(clicked); } else ShowUnitInfo(clicked); }
		}
		else if (col.GetParent() is Tile tile && _currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved) TryMoveTo(tile.GlobalPosition);
	}
	private async void TryAttackTarget(Unit target)
	{
		if (_selectedUnit == null || GetGridDistance(_selectedUnit.GlobalPosition, target.GlobalPosition) > _selectedUnit.Data.AttackRange) return;
		await PerformAttackAsync(_selectedUnit, target);
		if (_selectedUnit == null || !GodotObject.IsInstanceValid(_selectedUnit)) return;
		CancelAttackMode(); UpdateStatsUI(); ShowActions(true);
	}
	private void SelectUnit(Unit u)
	{
		if (u.HasMoved && u.HasAttacked) { ShowUnitInfo(u); return; }
		if (_selectedUnit != null && _selectedUnit != u) _selectedUnit.SetSelected(false);
		_selectedUnit = u; _selectedUnit.SetSelected(true);
		Node3D spr = _selectedUnit.GetNode<Node3D>("Sprite3D");
		if (!spr.HasMeta("BaseScale")) spr.SetMeta("BaseScale", spr.Scale);
		Vector3 bs = spr.GetMeta("BaseScale").AsVector3();
		Tween tw = CreateTween(); tw.TweenProperty(spr, "scale", new Vector3(bs.X * 0.8f, bs.Y * 1.3f, bs.Z * 0.8f), 0.1f);
		tw.TweenProperty(spr, "scale", bs, 0.2f).SetTrans(Tween.TransitionType.Bounce);
		ShowUnitInfo(u); UpdateStatsUI(); ShowActions(true); _currentState = State.PlayerTurn;
		RefreshTargetIcons(); ShowMovementRange(_selectedUnit);
	}
	private void DeselectUnit()
	{ if (_selectedUnit != null) { _selectedUnit.SetSelected(false); _selectedUnit = null; } UpdateStatsUI(); RefreshTargetIcons(); ClearMovementRange(); ClearAttackPreview(); Input.SetCustomMouseCursor(null); }
	private async void TryMoveTo(Vector3 tp)
	{
		if (_selectedUnit == null) return;
		Vector2I s = new(Mathf.RoundToInt(_selectedUnit.GlobalPosition.X / TileSize), Mathf.RoundToInt(_selectedUnit.GlobalPosition.Z / TileSize));
		Vector2I t = new(Mathf.RoundToInt(tp.X / TileSize), Mathf.RoundToInt(tp.Z / TileSize));
		var reach = GetReachableTiles(s, _selectedUnit.Data.Movement); if (!reach.ContainsKey(t)) return;
		ShowActions(false); RefreshTargetIcons(); ClearMovementRange();
		await _selectedUnit.MoveAlongPath(ExtractPath(reach, s, t));
		if (_selectedUnit != null) { CheckAutoExhaust(_selectedUnit); _selectedUnit.UpdateVisuals(); UpdateStatsUI(); ShowActions(true); RefreshTargetIcons(); }
	}
	private void HandleHover(Vector2 mp)
	{
		if (_lootScreenActive) return;
		var ss = GetWorld3D().DirectSpaceState; var f = Cam.ProjectRayOrigin(mp); var t = f + Cam.ProjectRayNormal(mp) * 1000;
		var q = PhysicsRayQueryParameters3D.Create(f, t); q.CollideWithAreas = true; var r = ss.IntersectRay(q);
		if (r.Count == 0) { ClearHover(); ClearAttackPreview(); return; }
		Node col = (Node)r["collider"]; Tile tt = null; Unit nh = null;
		if (col.GetParent() is Unit u) { nh = u; var gp = new Vector2I(Mathf.RoundToInt(u.GlobalPosition.X / TileSize), Mathf.RoundToInt(u.GlobalPosition.Z / TileSize)); if (_grid.TryGetValue(gp, out Tile ut)) tt = ut; }
		else if (col.GetParent() is Tile tile) tt = tile;
		if (nh != _hoveredUnit)
		{
			if (_hoveredUnit != null && GodotObject.IsInstanceValid(_hoveredUnit)) _hoveredUnit.SetHovered(false);
			ClearAttackPreview(); _comboVisualizer?.ClearVisuals(); _hoveredUnit = nh;
			if (_hoveredUnit != null)
			{
				if (_currentState == State.Camp) { var cv = GetAvailableCampConversation(_hoveredUnit.Data.Profile.Name); if (cv != null) { _hoveredUnit.SetHovered(true); if (AttackCursorIcon != null) Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16)); } }
				else { bool tgt = !_hoveredUnit.IsFriendly && _selectedUnit != null && !_selectedUnit.HasAttacked && (_currentState == State.SelectingAttackTarget || _currentState == State.PlayerTurn) && GetGridDistance(_selectedUnit.GlobalPosition, _hoveredUnit.GlobalPosition) <= _selectedUnit.Data.AttackRange;
					_hoveredUnit.SetHovered(true, tgt);
					if (tgt) { _previewedEnemy = _hoveredUnit; _previewedEnemy.PreviewDamage(_selectedUnit.GetMinDamage());
						_comboVisualizer?.ShowComboPreview(ComboResolver.Resolve(_selectedUnit, _hoveredUnit, _units, Inventory), Cam, _selectedUnit);
						if (_currentState == State.PlayerTurn && AttackCursorIcon != null) Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16)); } }
			}
		}
		if (tt != _hoveredTile) { ClearHover(); _hoveredTile = tt; if (_hoveredTile != null && _currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasMoved && _movementHighlightTiles.Contains(_hoveredTile)) _hoveredTile.SetHighlight(true, new Color(0f, 1f, 0f, 0.7f)); }
	}

	// === DEATH ===
	private async void HandleUnitDeath(Unit d)
	{
		Input.SetCustomMouseCursor(null); _units.Remove(d); _initiative.Remove(d);
		if (!_units.Any(u => GodotObject.IsInstanceValid(u) && !u.IsFriendly))
		{ 
			_currentState = State.Cutscene; ClearMovementRange(); ClearHover(); DeselectUnit(); ShowActions(false); _comboVisualizer?.ClearVisuals(); HideInitiativeBar();
			await ToSignal(GetTree().CreateTimer(0.4f), "timeout"); 
			await ClearEnvironmentSceneryAsync(); 
			AdvanceScript(); 
		}
	}

	// === MID-BATTLE EVENTS ===
	// FIX: Uses _initiative.Round (increments per full round through all units)
	// instead of the old _currentTurnNumber (which incremented per individual unit turn).
	// With 14 units, the old system reached "Turn 2" after just 1 unit acted.
	// Now Turn 2 means all units have acted once and the second round has begun.
	private void CheckMidBattleEvents()
	{
		int currentRound = _initiative.Round;
		var evt = _activeMidBattleEvents.FirstOrDefault(e => e.Turn == currentRound);

		if (!string.IsNullOrEmpty(evt.TimelinePath))
		{
			_activeMidBattleEvents.Remove(evt);
			_isMidBattleDialogue = true;
			StartDialogue(evt.TimelinePath, evt.Background);
			// Do NOT call ProcessNextTurn here — dialogue will call OnTimelineEnded
			// which calls CheckMidBattleEvents again, which will then call ProcessNextTurn
		}
		else
		{
			_ = ProcessNextTurn();
		}
	}

	// === INITIATIVE BAR ===
	private Control _initiativeBar;
	private void HideInitiativeBar()
	{
		if (_pulseTween != null && _pulseTween.IsValid()) { _pulseTween.Kill(); _pulseTween = null; }
		if (_initiativeBar != null && IsInstanceValid(_initiativeBar)) { Tween h = CreateTween(); h.TweenProperty(_initiativeBar, "position:y", -200f, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); h.Finished += () => { if (IsInstanceValid(_initiativeBar)) _initiativeBar.Visible = false; }; }
	}
	private void RefreshInitiativeUI()
	{
		if (_initiativeBar == null) BuildInitiativeBar(); if (_initiativeBar == null) return;
		_initiativeBar.Visible = true; _initiativeBar.Position = new Vector2(0, 8);
		if (_pulseTween != null && _pulseTween.IsValid()) { _pulseTween.Kill(); _pulseTween = null; }
		foreach (Node c in _initiativeBar.GetChildren()) c.QueueFree();
		var order = _initiative.AllInOrder(); int ci = order.IndexOf(_initiative.Current);
		for (int i = 0; i < Mathf.Min(order.Count, 8); i++)
		{
			int idx = (ci + i) % Mathf.Max(1, order.Count); if (idx < 0 || idx >= order.Count) continue;
			Unit u = order[idx]; if (!GodotObject.IsInstanceValid(u)) continue;
			bool ic = i == 0; float pw = ic ? 110 : 80; float ph = ic ? 155 : 110;
			Color bc = u.IsFriendly ? new Color(0.3f, 0.85f, 1f) : new Color(1f, 0.35f, 0.35f); if (ic) bc = new Color(1f, 0.92f, 0.3f);
			PanelContainer pip = new() { CustomMinimumSize = new Vector2(pw, ph) }; if (MasterTheme != null) pip.Theme = MasterTheme;
			int bw = ic ? 5 : 3;
			pip.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = ic ? new Color(0.12f, 0.1f, 0.06f, 0.97f) : new Color(0.06f, 0.06f, 0.09f, 0.95f),
				CornerRadiusTopLeft=12,CornerRadiusTopRight=12,CornerRadiusBottomLeft=12,CornerRadiusBottomRight=12,
				BorderWidthBottom=bw,BorderWidthTop=bw,BorderWidthLeft=bw,BorderWidthRight=bw, BorderColor=bc,
				ShadowSize=ic?16:6, ShadowColor=new Color(bc.R,bc.G,bc.B,ic?0.7f:0.4f), ShadowOffset=new Vector2(0,ic?5:2),
				ContentMarginLeft=6,ContentMarginRight=6,ContentMarginTop=6,ContentMarginBottom=6 });
			VBoxContainer vb = new() { Alignment = BoxContainer.AlignmentMode.Center }; vb.AddThemeConstantOverride("separation", 3); vb.SetAnchorsPreset(Control.LayoutPreset.FullRect); pip.AddChild(vb);
			Texture2D st = GD.Load<Texture2D>(u.Data.Profile.SpritePath);
			if (st != null) vb.AddChild(new TextureRect { Texture = st, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(pw - 14, ic ? 65 : 42), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });
			string dn = u.Data.Profile.Name; if (!ic && dn.Length > 7) dn = dn.Substring(0, 6) + ".";
			Label nl = new() { Text = dn, HorizontalAlignment = HorizontalAlignment.Center };
			nl.AddThemeFontSizeOverride("font_size", ic ? 16 : 12); nl.AddThemeColorOverride("font_color", ic ? new Color(1f, 0.95f, 0.7f) : bc);
			nl.AddThemeColorOverride("font_outline_color", Colors.Black); nl.AddThemeConstantOverride("outline_size", ic ? 6 : 3);
			if (_fantasyFont != null) nl.AddThemeFontOverride("font", _fantasyFont); vb.AddChild(nl);
			float ch = ic ? 40 : 28; float cw = ch * 0.7f;
			if (u.IsFriendly && u.Data.CardRank != CardRank.None)
			{ string cp = CardImageHelper.GetCardImagePath(u.Data.CardSuit, u.Data.CardRank); Texture2D ct = !string.IsNullOrEmpty(cp) ? GD.Load<Texture2D>(cp) : null;
				if (ct != null) vb.AddChild(new TextureRect { Texture = ct, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(cw, ch), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter }); }
			else if (!u.IsFriendly) { Texture2D bt = GD.Load<Texture2D>(CardImageHelper.GetCardBackPath(i % 9));
				if (bt != null) vb.AddChild(new TextureRect { Texture = bt, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(cw, ch), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter }); }
			_initiativeBar.AddChild(pip);
			pip.PivotOffset = new Vector2(pw / 2f, ph / 2f); pip.Scale = Vector2.Zero;
			float ed = i * 0.06f;
			CreateTween().TweenProperty(pip, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(ed);
			if (ic) { _pulseTween = CreateTween().SetLoops(); _pulseTween.TweenInterval(ed + 0.4f);
				_pulseTween.TweenProperty(pip, "scale", new Vector2(1.07f, 1.07f), 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				_pulseTween.TweenProperty(pip, "scale", new Vector2(0.96f, 0.96f), 0.5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut); }
		}
	}
	private void BuildInitiativeBar()
	{ _initiativeBar = new HBoxContainer(); _initiativeBar.SetAnchorsPreset(Control.LayoutPreset.TopWide); _initiativeBar.Position = new Vector2(0, 8); _initiativeBar.AddThemeConstantOverride("separation", 12); _initiativeBar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(_initiativeBar); else AddChild(_initiativeBar); }
		
	private Vector3 SnapToGridCenter(Vector3 rawPos)
	{
		// Force the coordinate into the 0-29 grid range
		int x = Mathf.Clamp(Mathf.RoundToInt(rawPos.X / TileSize), 0, GridWidth - 1);
		int z = Mathf.Clamp(Mathf.RoundToInt(rawPos.Z / TileSize), 0, GridDepth - 1);
		Vector2I gridPos = new Vector2I(x, z);
		
		// Return the exact physical center of the valid tile
		if (_grid.TryGetValue(gridPos, out Tile t)) return new Vector3(t.Position.X, t.Position.Y, t.Position.Z);
		return rawPos;
	}

	// === HELPERS ===
	private void CheckAutoExhaust(Unit u) { if (!u.IsFriendly || u.HasAttacked) return; if (!_units.Any(e => !e.IsFriendly && GodotObject.IsInstanceValid(e) && GetGridDistance(u.GlobalPosition, e.GlobalPosition) <= u.Data.AttackRange)) { u.HasAttacked = true; u.UpdateVisuals(); } }
	private int GetGridDistance(Vector3 a, Vector3 b) { int ax=Mathf.RoundToInt(a.X/TileSize),az=Mathf.RoundToInt(a.Z/TileSize),bx=Mathf.RoundToInt(b.X/TileSize),bz=Mathf.RoundToInt(b.Z/TileSize); return Mathf.Max(Mathf.Abs(ax-bx),Mathf.Abs(az-bz)); }
	private void RefreshTargetIcons() { foreach (var u in _units) { if (!GodotObject.IsInstanceValid(u)) continue; u.SetTargetable(false); if (_currentState == State.PlayerTurn && _selectedUnit != null && !_selectedUnit.HasAttacked) if (!u.IsFriendly && GetGridDistance(_selectedUnit.GlobalPosition, u.GlobalPosition) <= _selectedUnit.Data.AttackRange) u.SetTargetable(true); } }
	private void ClearMovementRange() { foreach (var t in _movementHighlightTiles) t.SetHighlight(false); _movementHighlightTiles.Clear(); }
	private void ClearAttackPreview() { if (_previewedEnemy != null) { if (GodotObject.IsInstanceValid(_previewedEnemy)) _previewedEnemy.ClearPreview(); _previewedEnemy = null; } if (_currentState != State.SelectingAttackTarget) Input.SetCustomMouseCursor(null); }
	private void ClearHover() { if (_hoveredTile != null) { if (_movementHighlightTiles.Contains(_hoveredTile)) _hoveredTile.SetHighlight(true, new Color(0.35f, 0.72f, 1f, 0.28f)); else _hoveredTile.SetHighlight(false); _hoveredTile = null; } }
	private void ShowMovementRange(Unit u) { ClearMovementRange(); if (u == null || u.HasMoved) return; var s = new Vector2I(Mathf.RoundToInt(u.GlobalPosition.X / TileSize), Mathf.RoundToInt(u.GlobalPosition.Z / TileSize)); var r = GetReachableTiles(s, u.Data.Movement); Color mc = new(0.35f, 0.72f, 1f, 0.28f); foreach (var k in r) { if (k.Key == s) continue; if (_grid.TryGetValue(k.Key, out Tile t)) { t.SetHighlight(true, mc); _movementHighlightTiles.Add(t); } } }
	private Dictionary<Vector2I, Vector2I> GetReachableTiles(Vector2I s, int m) { var fr = new Queue<Vector2I>(); var cf = new Dictionary<Vector2I, Vector2I>(); var co = new Dictionary<Vector2I, int>(); fr.Enqueue(s); cf[s] = s; co[s] = 0; Vector2I[] ds = { new(1,0), new(-1,0), new(0,1), new(0,-1), new(1,1), new(-1,-1), new(1,-1), new(-1,1) }; while (fr.Count > 0) { var c = fr.Dequeue(); foreach (var d in ds) { var n = c + d; int nc = co[c] + 1; if (nc > m || !_grid.ContainsKey(n) || !IsTileFree(n)) continue; if (!co.ContainsKey(n) || nc < co[n]) { co[n] = nc; cf[n] = c; fr.Enqueue(n); } } } return cf; }
	private List<Vector3> ExtractPath(Dictionary<Vector2I, Vector2I> cf, Vector2I s, Vector2I e) { var p = new List<Vector3>(); var c = e; while (c != s) { float h = _grid.ContainsKey(c) ? _grid[c].Position.Y : 0.01f; p.Add(new Vector3(c.X * TileSize, h, c.Y * TileSize)); c = cf[c]; } p.Reverse(); return p; }
}
