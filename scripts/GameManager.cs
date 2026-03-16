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
	public StyleBoxFlat BadgeStyle { get; private set; }
	public StyleBoxFlat ItemSlotStyle { get; private set; }
	public StyleBoxFlat ItemSlotHoverStyle { get; private set; }

	[ExportGroup("Core References")]
	[Export] public PackedScene UnitScene;
	[Export] public PackedScene TileScene;
	[Export] public Camera3D Cam;
	private Vector3 _initialCamPos;
	private Font _fantasyFont;

	[ExportGroup("UI References")]
	[Export] public Control ActionMenu;
	[Export] public Button AttackButton;
	[Export] public Button EndTurnButton;
	[Export] public Button PartyButton;
	[Export] public RichTextLabel StatsLabel;
	[Export] public ColorRect DimOverlay;
	[Export] public Texture2D AttackCursorIcon;

	[ExportGroup("Environment References")]
	[Export] public DirectionalLight3D MainLight;
	[Export] public WorldEnvironment WorldEnv;
	[Export] public Godot.Collections.Array<PackedScene> BackgroundDioramas;
	
	// === STATE TRACKING ===
	private enum State { PlayerTurn, EnemyTurn, SelectingAttackTarget, Cutscene, PartyMenu, Camp }
	private State _currentState = State.Cutscene;
	private List<string> _campaignMissions = new();
	private int _currentMissionIndex = 0;
	public System.Collections.Generic.Dictionary<string, bool> StoryFlags = new();
	
	private Tween _uiTween;
	private bool _dialogueActive = false;
	private bool _levelUpActive = false;
	private bool _lootScreenActive = false;
	private Control _activePartyMenu;
	private Callable _onTimelineEndedCallable;
	private Callable _onDialogicSignalCallable;
	public TaskCompletionSource<bool> ActiveLevelUpTcs;
	public TaskCompletionSource<bool> ActiveLootTcs;
	private Timer _cloudTimer;
	private CanvasLayer _dialogueBgLayer;
	private TextureRect _activeDialogueBackground;
	
	private List<PanelContainer> _activeBondNotifications = new();
	private Queue<System.Action> _celebrationQueue = new();
	private bool _celebrationPlaying = false;

	// === GAME DATA ===
	public List<CampConversation> CampConversationsPool = new();
	private List<PersistentUnit> _party = new(); 
	public List<GameItem> Inventory = new();
	private PersistentUnit _viewedPartyMember;
	private VBoxContainer _rightMenuPanel; 

	// === STORY & SCRIPTING ===
	private System.Collections.Generic.Dictionary<string, List<ScriptEvent>> _scriptDatabase = new();
	private string _currentSection = "";
	private int _currentScriptIndex = -1;
	private string _pendingSection = "";
	private Node _dialogic;
	private int _currentTurnNumber = 1;
	private List<MidBattleEvent> _activeMidBattleEvents = new();
	private bool _isMidBattleDialogue = false;

	public override void _Ready()
	{
		Instance = this; 
		if (Cam != null)
		{
			if (Cam.Projection == Camera3D.ProjectionType.Orthogonal)
			{
				// === THE FIX 1: Zoom in slightly (Reduced from 40 to 34) ===
				Cam.Size = 34f; 
				Cam.Far = 3000f;
				
				// Center of 30x30 grid (TileSize 2) is (29, 0, 29)
				Vector3 gridCenter = new Vector3(29f, 0f, 29f); 
				
				// Look at the center, then push backward along local Z
				Cam.GlobalPosition = gridCenter + (Cam.GlobalTransform.Basis.Z * 150f);
			}
			else
			{
				Cam.GlobalPosition = Cam.GlobalPosition + new Vector3(15f, 30f, 25f);
			}
			_initialCamPos = Cam.GlobalPosition;
		}

		CallDeferred(MethodName.SetupUnifiedUI);
		GameJuiceUpgrade.Install(this);
		GameDatabase.Initialize();
		GenerateGrid();
		_comboVisualizer = new ComboVisualizer();
		AddChild(_comboVisualizer);
		AttackButton.Pressed += OnAttackButtonPressed;
		EndTurnButton.Pressed += OnEndTurnPressed; 
		if (PartyButton != null) PartyButton.Pressed += TogglePartyMenu;
		ActionMenu.Visible = false;

		_dialogic = GetNodeOrNull<Node>("/root/Dialogic");
		
		if (_dialogic != null)
		{
			_onTimelineEndedCallable = new Callable(this, MethodName.OnTimelineEnded);
			_onDialogicSignalCallable = new Callable(this, MethodName.OnDialogicSignal);
			
			_dialogic.Connect("timeline_ended", _onTimelineEndedCallable);
			_dialogic.Connect("signal_event", _onDialogicSignalCallable);
		}
		
		_cloudTimer = new Timer { WaitTime = 3f, Autostart = false, OneShot = true };
		_cloudTimer.Timeout += StartAmbientCloudSystem;
		AddChild(_cloudTimer);
		StartAmbientCloudSystem();

		CallDeferred(MethodName.UpdateStatsUI);

		CampaignData campaign = StoryLoader.GetCampaignData();
		if (campaign != null && campaign.Missions.Count > 0)
		{
			_campaignMissions = campaign.Missions;
			if (campaign.CampConversations != null)
			{
				CampConversationsPool = campaign.CampConversations;
			}
			LoadMission(0); 
		}
		else
		{
			GD.PrintErr("CRITICAL: No campaign data found!");
		}
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
		
		if (_comboVisualizer != null && GodotObject.IsInstanceValid(_comboVisualizer))
		{
			_comboVisualizer.ClearVisuals();			
		}
		
		// === THE FIX: Cancel hanging UI Tasks so the C# state machine can die ===
		ActiveLevelUpTcs?.TrySetCanceled();
		ActiveLootTcs?.TrySetCanceled();

		// === THE FIX: Disconnect using the exact cached Callables ===
		if (_dialogic != null)
		{
			if (_dialogic.IsConnected("timeline_ended", _onTimelineEndedCallable))
				_dialogic.Disconnect("timeline_ended", _onTimelineEndedCallable);
				
			if (_dialogic.IsConnected("signal_event", _onDialogicSignalCallable))
				_dialogic.Disconnect("signal_event", _onDialogicSignalCallable);
		}

		if (AttackButton != null) AttackButton.Pressed -= OnAttackButtonPressed;
		if (EndTurnButton != null) EndTurnButton.Pressed -= OnEndTurnPressed;
		if (PartyButton != null) PartyButton.Pressed -= TogglePartyMenu;

		foreach (var u in _units)
		{
			if (IsInstanceValid(u)) u.OnDied -= HandleUnitDeath;
		}
		
		if (_cloudTimer != null && IsInstanceValid(_cloudTimer)) _cloudTimer.Timeout -= StartAmbientCloudSystem;

		_units.Clear();
		_obstacles.Clear();
		_activeDioramas.Clear();
		_nightLights.Clear();
		_grid.Clear();
		
		if (_uiTween != null && _uiTween.IsValid()) _uiTween.Kill();
	}

	public override void _Process(double delta)
	{
		if (_dialogueActive || _levelUpActive || _lootScreenActive || _currentState == State.Cutscene || _currentState == State.PartyMenu) return;

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
			
			// === THE FIX 1: Multiply Y input by 1.8f to equalize horizontal and vertical screen pan speed ===
			Vector3 rawMove = (right * moveDir.X) + (forward * (moveDir.Y * 1.8f));
			rawMove *= 35f * (float)delta; 

			Plane groundPlane = new Plane(Vector3.Up, 0f);
			Vector2 screenCenter = screenSize / 2f;
			Vector3 rayOrigin = Cam.ProjectRayOrigin(screenCenter);
			Vector3 rayDir = Cam.ProjectRayNormal(screenCenter);
			Vector3? currentLookAt = groundPlane.IntersectsRay(rayOrigin, rayDir);

			if (currentLookAt.HasValue)
			{
				Vector3 targetLookAt = currentLookAt.Value + rawMove;

				// === THE FIX 2: Tighter left/bottom limits (increased from 0f to 12f) ===
				float minBoundX = 12f;
				float minBoundZ = 12f;
				float maxBoundX = (GridWidth - 1) * TileSize; 
				float maxBoundZ = (GridDepth - 1) * TileSize; 

				targetLookAt.X = Mathf.Clamp(targetLookAt.X, minBoundX, maxBoundX);
				targetLookAt.Z = Mathf.Clamp(targetLookAt.Z, minBoundZ, maxBoundZ);

				Vector3 allowedMove = targetLookAt - currentLookAt.Value;
				Cam.GlobalPosition += allowedMove;
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_dialogueActive || _levelUpActive || _lootScreenActive || _currentState == State.EnemyTurn || _currentState == State.Cutscene) return;

		if (@event is InputEventMouseMotion mouseMotion) HandleHover(mouseMotion.Position);
		else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Left) HandleClick(mouseEvent.Position);
			else if (_currentState == State.PlayerTurn || _currentState == State.SelectingAttackTarget)
				HandleZoom(mouseEvent);
		}
	}

	private void HandleZoom(InputEventMouseButton e)
	{
		if (Cam == null) return;
		float zoomStep = e.ButtonIndex == MouseButton.WheelUp ? -1.5f : 
						 e.ButtonIndex == MouseButton.WheelDown ? 1.5f : 0f;
		if (zoomStep == 0f) return;

		if (Cam.Projection == Camera3D.ProjectionType.Orthogonal)
		{
			float target = Mathf.Clamp(Cam.Size + zoomStep, 18f, 50f);
			CreateTween().TweenProperty(Cam, "size", target, 0.15f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
		else
		{
			float target = Mathf.Clamp(Cam.Fov + zoomStep * 2f, 30f, 90f);
			CreateTween().TweenProperty(Cam, "fov", target, 0.15f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
	}

	// === SCRIPT & STORY LOGIC ===
	private void AdvanceScript()
	{
		if (_dialogueActive) return; 

		if (!string.IsNullOrEmpty(_pendingSection))
		{
			_currentSection = _pendingSection;
			_currentScriptIndex = -1;
			_pendingSection = ""; 
		}

		_currentScriptIndex++;
		
		if (!_scriptDatabase.ContainsKey(_currentSection) || _currentScriptIndex >= _scriptDatabase[_currentSection].Count)
		{
			// === MISSION COMPLETE LOGIC ===
			if (_currentMissionIndex < _campaignMissions.Count - 1)
			{
				// Enter the cozy camp instead of moving instantly!
				_ = EnterCampStage(); 
			}
			else
			{
				GD.Print("🎉 CAMPAIGN COMPLETE! You reached the end of the game!");
				StatsLabel.Text = "[center][b][wave]CAMPAIGN WON![/wave][/b][/center]";
			}
			return;
		}

		ScriptEvent currentEvent = _scriptDatabase[_currentSection][_currentScriptIndex];

		if (currentEvent.Type == EventType.AddPartyMember)
		{
			_party.Add(new PersistentUnit(GameDatabase.Units[currentEvent.ProfileId], currentEvent.IsPlayer));
			AdvanceScript();
		}
		else if (currentEvent.Type == EventType.JumpToSection)
		{
			_pendingSection = currentEvent.TargetSection;
			AdvanceScript(); 
		}
		else if (currentEvent.Type == EventType.Dialogue) 
		{
			_currentState = State.Cutscene; 
			StartDialogue(currentEvent.TimelinePath, currentEvent.Background);
		}
		else if (currentEvent.Type == EventType.Battle) 
		{
			StartBattle(currentEvent.BattleData);
		}
	}

	public void StartDialogue(string timelinePath, string backgroundPath = null)
	{
		if (_dialogueActive || _dialogic == null) return;
		_dialogueActive = true;
		ShowActions(false);
		DeselectUnit();
		if (DimOverlay != null) DimOverlay.Visible = true;
		
		if (string.IsNullOrEmpty(backgroundPath))
		{
			if (DimOverlay != null) DimOverlay.Visible = true;
		}
		else
		{
			if (DimOverlay != null) DimOverlay.Visible = false;
			ShowDialogueBackground(backgroundPath);
		}
		
		StatsLabel.Text = "Dialogue...";
		_dialogic.Call("start", timelinePath);
	}
	
	private void OnTimelineEnded()
	{
		_dialogueActive = false;
		if (DimOverlay != null) DimOverlay.Visible = false;
		
		// === FIX: Explicitly hide Dialogic's layout node (the VN textbox panel) ===
		// Without this, the Dialogic textbox can remain visible between dialogues,
		// showing as a colored bar at the bottom of the screen during gameplay.
		try
		{
			var styles = _dialogic.Get("Styles").As<GodotObject>();
			if (styles != null)
			{
				var layoutNode = styles.Call("get_layout_node").As<CanvasItem>();
				if (layoutNode != null && IsInstanceValid(layoutNode))
					layoutNode.Visible = false;
			}
		}
		catch { /* Dialogic layout may already be freed */ }
		
		// Shrink the background away with a snappy bounce
		if (IsInstanceValid(_activeDialogueBackground))
		{
			TextureRect bg = _activeDialogueBackground; 
			CanvasLayer layer = _dialogueBgLayer;
			_activeDialogueBackground = null;
			_dialogueBgLayer = null;

			Tween t = CreateTween();
			t.TweenProperty(bg, "scale", new Vector2(0.001f, 0.001f), 0.35f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			t.Finished += () => { bg.QueueFree(); layer?.QueueFree(); };
		}
		
		if (_isMidBattleDialogue)
		{
			_isMidBattleDialogue = false;
			CheckMidBattleEvents();
		}
		// === NEW: If we are in Camp, restore the UI and refresh the bubbles! ===
		else if (_currentState == State.Camp)
		{
			if (_campUIRoot != null) _campUIRoot.Visible = true;
			RefreshCampSpeechBubbles();
		}
		else 
		{
			AdvanceScript(); 
		}
	}
	
	public bool HasFlag(string flagName)
	{
		return StoryFlags.TryGetValue(flagName, out bool val) && val;
	}
	
	private void OnDialogicSignal(string argument)
	{
		if (string.IsNullOrEmpty(argument)) return;
	 
		if (argument == "ChoicePicked")
		{
			ShowChoicePickedJuice();
			return;
		}
	 
		if (argument.StartsWith("JumpTo:"))
		{
			_pendingSection = argument.Split(":")[1];
		}
		else if (argument.StartsWith("SetSectionQuietly:"))
		{
			string[] parts = argument.Split(":");
			if (parts.Length >= 2 && _scriptDatabase.ContainsKey(parts[1]))
			{
				_currentSection = parts[1];
				_currentScriptIndex = 0;
			}
		}
		else if (argument.StartsWith("Rel:"))
		{
			// Format: "Rel:CharName:Bond:Amount" e.g. "Rel:Dougal:Bond:30" or "Rel:Dougal:Bond:-10"
			string[] parts = argument.Split(':');
			if (parts.Length >= 4 && int.TryParse(parts[3], out int amount))
			{
				AddBond(parts[1], amount);
				// Spawn arrow over the unit on the field if they exist
				Unit fieldUnit = _units.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && u.Data?.Profile.Name == parts[1]);
				if (fieldUnit != null) SpawnBondArrow(fieldUnit, amount > 0);
			}
			// Also support short format "Rel:CharName:Amount"
			else if (parts.Length >= 3 && int.TryParse(parts[2], out int amount2))
			{
				AddBond(parts[1], amount2);
				Unit fieldUnit = _units.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && u.Data?.Profile.Name == parts[1]);
				if (fieldUnit != null) SpawnBondArrow(fieldUnit, amount2 > 0);
			}
		}
		else if (argument.StartsWith("Emotion:"))
		{
			string[] parts = argument.Split(':');
			if (parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]))
			{
				Vector2? burstPos = GetDialogicPortraitBurstScreenPosition(parts[1]);
				ShowEmotionEffect(parts[1], parts[2], burstPos);
			}
		}
		else if (argument.StartsWith("SetFlag:"))
		{
			string flagName = argument.Split(':')[1];
			StoryFlags[flagName] = true;
		}
	}
	
	private void ShowDialogueBackground(string path)
	{
		Texture2D tex = GD.Load<Texture2D>(path);
		if (tex == null) return;

		// Layer 0 puts it perfectly ON TOP of the 3D board, but BEHIND Dialogic (which uses Layer 1+)
		_dialogueBgLayer = new CanvasLayer { Layer = 0 }; 
		AddChild(_dialogueBgLayer);

		Vector2 screenSize = GetViewport().GetVisibleRect().Size;

		_activeDialogueBackground = new TextureRect
		{
			Texture = tex,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			CustomMinimumSize = screenSize
		};
		
		_dialogueBgLayer.AddChild(_activeDialogueBackground);
		
		_activeDialogueBackground.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		
		// GAME JUICE: Start at size 0.001 perfectly in the center of the screen
		_activeDialogueBackground.PivotOffset = screenSize / 2;
		_activeDialogueBackground.Scale = new Vector2(0.001f, 0.001f);

		CreateTween().TweenProperty(_activeDialogueBackground, "scale", Vector2.One, 0.45f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	/// <summary>Find the current speaker's portrait and return a screen position for the burst (head/upper portrait area). Returns null if not found or if coords look wrong (e.g. Dialogic in SubViewport).</summary>
	private Vector2? GetDialogicPortraitBurstScreenPosition(string characterName)
	{
		if (string.IsNullOrEmpty(characterName)) return null;
		var containers = GetTree().GetNodesInGroup("dialogic_portrait_con_position");
		Vector2 viewSize = GetViewport().GetVisibleRect().Size;
		foreach (Node n in containers)
		{
			if (n is not Control container) continue;
			foreach (Node child in container.GetChildren())
			{
				if (child.Name != characterName) continue;
				if (child is not Node2D characterNode) continue;
				// Origin = portrait bottom-center. Use ~55% of container height up = head/upper chest (burst area).
				Vector2 originGlobal = characterNode.GlobalPosition;
				Vector2 containerSize = container.Size;
				if (containerSize.Y <= 0) containerSize = new Vector2(200, 400); // fallback
				float up = containerSize.Y * 0.55f;
				Vector2 burstPoint = originGlobal + new Vector2(0, -up);
				// Reject if clearly wrong (different viewport or bogus) so effect falls back to center
				if (burstPoint.Y < 100 || burstPoint.Y > viewSize.Y - 60 || burstPoint.X < 60 || burstPoint.X > viewSize.X - 60)
					return null;
				return burstPoint;
			}
		}
		return null;
	}

	private void ShowEmotionEffect(string characterName, string emotionType, Vector2? portraitTopScreenPosition)
	{
		PackedScene scene = GD.Load<PackedScene>("res://scenes/EmotionEffect.tscn");
		if (scene == null) return;
		Node canvasLayer = GetNodeOrNull("CanvasLayer");
		if (canvasLayer == null) return;
		var effect = scene.Instantiate<EmotionEffect>();
		if (effect == null) return;
		canvasLayer.AddChild(effect);
		effect.Play(emotionType, portraitTopScreenPosition);
	}

	private async void LoadMission(int index)
	{
		_currentMissionIndex = index;
		string missionPath = _campaignMissions[index];
		GD.Print($"[CAMPAIGN] Loading Mission {index + 1}: {missionPath}");

		_scriptDatabase = StoryLoader.LoadScriptDatabase(missionPath);
		_currentSection = _scriptDatabase.Keys.First();
		_currentScriptIndex = -1;
		_pendingSection = "";
		_currentTurnNumber = 1;
		_activeMidBattleEvents.Clear();

		foreach (var unit in _party) unit.HealBetweenBattles();

		// THE FIX: Wait for the old world to bounce away gracefully before making the new one
		await ClearEnvironmentSceneryAsync(); 
		GenerateGrid(); 
		AdvanceScript();
	}

	private void ClearBoard()
	{
		// Destroy physical nodes on the board
		foreach (var u in _units) if (IsInstanceValid(u)) u.QueueFree();
		foreach (var o in _obstacles) if (IsInstanceValid(o)) o.QueueFree();
		foreach (var g in _grid.Values) if (IsInstanceValid(g)) g.QueueFree();
		
		_units.Clear();
		_obstacles.Clear();
		_grid.Clear();
		_activeDioramas.Clear();
	}
	
	public CampConversation GetAvailableCampConversation(string characterName)
	{
		return CampConversationsPool
			.Where(c => c.CharacterName == characterName)
			.Where(c => !c.PlayOnce || !HasFlag("CampConv_" + c.TimelineName))
			.Where(c => EvaluateCondition(c.Condition))
			.OrderByDescending(c => c.Priority)
			.FirstOrDefault();
	}

	private bool EvaluateCondition(string cond)
	{
		if (string.IsNullOrWhiteSpace(cond)) return true;
		cond = cond.Trim();
	 
		// New format: "Bond:Dougal >= 60"
		if (cond.StartsWith("Bond:"))
		{
			string[] tokens = cond.Split(new char[] { ' ' }, 3, System.StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 3)
			{
				string charName = tokens[0].Split(':')[1];
				int currentVal = GetBondXP(charName);
				if (int.TryParse(tokens[2], out int target))
				{
					return tokens[1] switch
					{
						">" => currentVal > target,
						"<" => currentVal < target,
						">=" => currentVal >= target,
						"<=" => currentVal <= target,
						"==" => currentVal == target,
						_ => true
					};
				}
			}
		}
		// Legacy format: "Rel:Dougal:Respect >= 60" → just checks BondXP
		else if (cond.StartsWith("Rel:"))
		{
			string[] tokens = cond.Split(new char[] { ' ' }, 3, System.StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 3)
			{
				string[] idParts = tokens[0].Split(':');
				string charName = idParts.Length >= 2 ? idParts[1] : "";
				int currentVal = GetBondXP(charName);
				if (int.TryParse(tokens[2], out int target))
				{
					return tokens[1] switch
					{
						">" => currentVal > target,
						"<" => currentVal < target,
						">=" => currentVal >= target,
						"<=" => currentVal <= target,
						"==" => currentVal == target,
						_ => true
					};
				}
			}
		}
		else if (cond.StartsWith("!Flag:")) return !HasFlag(cond.Substring(6));
		else if (cond.StartsWith("Flag:")) return HasFlag(cond.Substring(5));
	 
		return true;
	}
	
	/// <summary>
	/// Central bond change method. Handles positive gains, negative losses, 
	/// rank-ups, rank-downs, notifications, and all animations.
	/// </summary>
	public void AddBond(string charName, int amount)
	{
		PersistentUnit companion = _party.FirstOrDefault(u => u.Profile.Name == charName && !u.IsPlayerCharacter);
		if (companion == null || amount == 0) return;
	 
		int oldXP = companion.BondXP;
		CardRank oldRank = companion.CardRank;
		int rankChange = companion.AddBondXP(amount);
	 
		ShowBondNotification(charName, amount, oldXP, companion.BondXP, rankChange);
	 
		CardRank capturedNewRank = companion.CardRank;
		CardRank capturedOldRank = oldRank;
		PersistentUnit capturedUnit = companion;
	 
		if (rankChange == 1)
			QueueCelebration(() => ShowCardRankUpCelebration(capturedUnit, capturedOldRank, capturedNewRank));
		else if (rankChange == -1)
			QueueCelebration(() => ShowCardRankDownCelebration(capturedUnit, capturedOldRank, capturedNewRank));
	}
	 
	private void QueueCelebration(System.Action celebAction)
	{
		_celebrationQueue.Enqueue(celebAction);
		if (!_celebrationPlaying)
			PlayNextCelebration();
	}
	 
	private void PlayNextCelebration()
	{
		if (_celebrationQueue.Count == 0) { _celebrationPlaying = false; return; }
		_celebrationPlaying = true;
		_celebrationQueue.Dequeue().Invoke();
	}
	 
	/// <summary>
	/// After an attack resolves, check adjacency and award bond XP.
	/// </summary>
	public void ProcessCombatBondGains(Unit attacker, Unit target, int damageDealt)
	{
		if (damageDealt <= 0 || !attacker.IsFriendly) return;
	 
		Unit playerUnit = _units.FirstOrDefault(u => GodotObject.IsInstanceValid(u) && u.IsFriendly && u.Data.IsPlayerCharacter);
		if (playerUnit == null) return;
	 
		if (attacker.Data.IsPlayerCharacter)
		{
			foreach (var ally in _units.Where(u => GodotObject.IsInstanceValid(u) && u.IsFriendly && !u.Data.IsPlayerCharacter && u != attacker))
			{
				if (GetGridDistance(attacker.GlobalPosition, ally.GlobalPosition) <= 1)
				{
					int bondAmount = Mathf.Max(1, damageDealt / 2);
					AddBond(ally.Data.Profile.Name, bondAmount);
					SpawnBondArrow(ally, true);
				}
			}
		}
		else
		{
			bool playerNearEnemy = GodotObject.IsInstanceValid(target) && GetGridDistance(playerUnit.GlobalPosition, target.GlobalPosition) <= 1;
			bool playerNearAlly = GetGridDistance(playerUnit.GlobalPosition, attacker.GlobalPosition) <= 1;
	 
			if (playerNearEnemy || playerNearAlly)
			{
				int bondAmount = Mathf.Max(1, damageDealt / 2);
				AddBond(attacker.Data.Profile.Name, bondAmount);
				SpawnBondArrow(attacker, true);
			}
		}
	}
	 
	/// <summary>Bouncy green/red arrow above a unit that just gained/lost bond XP.</summary>
	private void SpawnBondArrow(Unit unit, bool positive)
	{
		if (!GodotObject.IsInstanceValid(unit)) return;
	 
		Label3D arrow = new Label3D
		{
			Text = positive ? "▲" : "▼",
			FontSize = 90,
			Modulate = positive ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.3f, 0.3f),
			OutlineModulate = Colors.Black,
			OutlineSize = 10,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			RenderPriority = 55
		};
		if (_fantasyFont != null) arrow.Font = _fantasyFont;
	 
		AddChild(arrow);
		arrow.GlobalPosition = unit.GlobalPosition + Vector3.Up * 2.5f;
	 
		Tween t = CreateTween();
		float dir = positive ? 1.5f : -0.5f;
		t.TweenProperty(arrow, "global_position:y", arrow.GlobalPosition.Y + dir, 0.4f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.Parallel().TweenProperty(arrow, "scale", Vector3.One * 1.3f, 0.15f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(arrow, "scale", Vector3.One, 0.1f)
			.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		t.TweenInterval(0.4f);
		t.TweenProperty(arrow, "modulate:a", 0f, 0.3f);
		t.Finished += () => arrow.QueueFree();
	}
	
	public int GetBondXP(string charName)
	{
		PersistentUnit companion = _party.FirstOrDefault(u => u.Profile.Name == charName && !u.IsPlayerCharacter);
		return companion?.BondXP ?? 0;
	}
	 
	// Backwards compat for anything still calling the old signature
	public int GetRelationship(string charName, string relType)
	{
		return GetBondXP(charName);
	}
}
