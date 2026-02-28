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
	private enum State { PlayerTurn, EnemyTurn, SelectingAttackTarget, Cutscene, PartyMenu }
	private State _currentState = State.Cutscene;
	private List<string> _campaignMissions = new();
	private int _currentMissionIndex = 0;
	
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

	// === GAME DATA ===
	private System.Collections.Generic.Dictionary<string, UnitProfile> _unitDatabase = new();
	private List<PersistentUnit> _party = new(); 
	public List<GameItem> Inventory = new();
	public System.Collections.Generic.Dictionary<string, Equipment> ItemDatabase = new();
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
		if (Cam != null) _initialCamPos = Cam.GlobalPosition;

		CallDeferred(MethodName.SetupUnifiedUI);

		_unitDatabase["Ambrose"] = new UnitProfile("Ambrose", "res://assets/HighRes3.png", 25, 15, 1, 3, 0, UnitFacing.Right);
		_unitDatabase["Dougal"] = new UnitProfile("Dougal", "res://assets/HighRes5.png", 18, 18, 1, 3, 0, UnitFacing.Right);
		_unitDatabase["Guard"] = new UnitProfile("Goblin", "res://assets/HighRes4.png", 10, 3, 1, 3, 140, UnitFacing.Right);
		_unitDatabase["Orc"]   = new UnitProfile("Ogre", "res://assets/HR_ORC2.png", 25, 8, 1, 2, 120, UnitFacing.Center);

		ItemDatabase["IronSword"] = new Equipment("IronSword", "Iron Sword", "res://icons/sword.png", EquipSlot.Weapon, bonusDmg: 1);
		ItemDatabase["FineHelmet"] = new Equipment("FineHelmet", "Fine Helmet", "res://icons/helmet.png", EquipSlot.Armor, bonusHp: 3);

		GenerateGrid();
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

		CallDeferred(MethodName.UpdateStatsUI);

		// === THE FIX: ONLY load the campaign here! No raw AdvanceScript() calls! ===
		CampaignData campaign = StoryLoader.GetCampaignData();
		if (campaign != null && campaign.Missions.Count > 0)
		{
			_campaignMissions = campaign.Missions;
			LoadMission(0); // This handles GenerateGrid() and AdvanceScript() internally!
		}
		else
		{
			GD.PrintErr("CRITICAL: No campaign data found!");
		}
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;

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
		if (_dialogueActive || _levelUpActive || _currentState == State.Cutscene || _currentState == State.PartyMenu) return;

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
			
			float limitLeft = 0f; float limitRight = 10f;
			float limitUp = 0f; float limitDown = 15f;

			newPos.X = Mathf.Clamp(newPos.X, _initialCamPos.X - limitLeft, _initialCamPos.X + limitRight);
			newPos.Z = Mathf.Clamp(newPos.Z, _initialCamPos.Z - limitUp, _initialCamPos.Z + limitDown);

			Cam.GlobalPosition = newPos;
			StartAmbientCloudSystem();
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_dialogueActive || _levelUpActive || _currentState == State.EnemyTurn || _currentState == State.Cutscene) return;

		if (@event is InputEventMouseMotion mouseMotion) HandleHover(mouseMotion.Position);
		else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left) HandleClick(mouseEvent.Position);
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
			_party.Add(new PersistentUnit(_unitDatabase[currentEvent.ProfileId], currentEvent.IsPlayer));
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
			StartDialogue(currentEvent.TimelinePath);
		}
		else if (currentEvent.Type == EventType.Battle) 
		{
			StartBattle(currentEvent.BattleData);
		}
	}

	public void StartDialogue(string timelinePath)
	{
		if (_dialogueActive || _dialogic == null) return;
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
		
		if (_isMidBattleDialogue)
		{
			_isMidBattleDialogue = false;
			CheckMidBattleEvents();
		}
		else AdvanceScript(); 
	}

	private void OnDialogicSignal(string argument)
	{
		if (string.IsNullOrEmpty(argument)) return;
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
			// Next timeline_ended will be when the jumped-to timeline (e.g. Loud_Approach) ends — we AdvanceScript() then.
		}
		else if (argument.StartsWith("Rel:"))
		{
			string[] parts = argument.Split(':');
			if (parts.Length == 4 && int.TryParse(parts[3], out int amount))
			{
				UpdateRelationship(parts[1], parts[2], amount);
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

	private void CheckMidBattleEvents()
	{
		var evt = _activeMidBattleEvents.FirstOrDefault(e => e.Turn == _currentTurnNumber);
		
		if (!string.IsNullOrEmpty(evt.TimelinePath))
		{
			_activeMidBattleEvents.Remove(evt);
			_isMidBattleDialogue = true; 
			StartDialogue(evt.TimelinePath);
		}
		else
		{
			_currentState = State.PlayerTurn;
			DeselectUnit();
			_ = ShowTurnAnnouncer("YOUR TURN", new Color(0.2f, 0.8f, 1.0f));
			ShowActions(true);
		}
	}

	private void UpdateRelationship(string charName, string relType, int amount)
	{
		PersistentUnit companion = _party.FirstOrDefault(u => u.Profile.Name == charName && !u.IsPlayerCharacter);
		if (companion != null && companion.Relationships.ContainsKey(relType))
		{
			companion.Relationships[relType] = Mathf.Clamp(companion.Relationships[relType] + amount, 0, 100);
			ShowRelationshipNotification(charName, relType, amount);
		}
	}
	
	private void LoadMission(int index)
	{
		_currentMissionIndex = index;
		string missionPath = _campaignMissions[index];
		GD.Print($"[CAMPAIGN] Loading Mission {index + 1}: {missionPath}");

		_scriptDatabase = StoryLoader.LoadFromJSON(missionPath);
		_currentSection = _scriptDatabase.Keys.First();
		_currentScriptIndex = -1;
		_pendingSection = "";
		_currentTurnNumber = 1;
		_activeMidBattleEvents.Clear();

		foreach (var unit in _party) unit.HealBetweenBattles();

		// THE FIX: Always clear the board before generating a new one
		ClearBoard(); 
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
}
