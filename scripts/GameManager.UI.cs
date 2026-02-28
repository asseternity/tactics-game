using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	private void SetupUnifiedUI()
	{
		StyleBoxFlat baseStyle = new StyleBoxFlat {
			BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f),
			CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
			BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4,
			BorderColor = new Color(0.3f, 0.3f, 0.35f, 1f),
			ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 16, ContentMarginBottom = 16,
			ShadowColor = new Color(0, 0, 0, 0.7f), ShadowSize = 8, ShadowOffset = new Vector2(0, 6)
		};
		BaseUIStyle = baseStyle;

		BadgeStyle = new StyleBoxFlat {
			BgColor = new Color(0.6f, 0.1f, 0.1f, 0.95f), 
			CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 0, CornerRadiusBottomRight = 8, 
			BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			BorderColor = new Color(0.9f, 0.4f, 0.4f, 1f),
			ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 4, ContentMarginBottom = 4,
			ShadowColor = new Color(0, 0, 0, 0.5f), ShadowSize = 4, ShadowOffset = new Vector2(0, 3)
		};

		StyleBoxFlat btnNormal = (StyleBoxFlat)baseStyle.Duplicate();
		StyleBoxFlat btnHover = (StyleBoxFlat)baseStyle.Duplicate();
		btnHover.BgColor = new Color(0.18f, 0.18f, 0.22f, 1f); btnHover.BorderColor = new Color(1f, 0.85f, 0.3f, 1f); 
		btnHover.ShadowSize = 14; btnHover.ShadowOffset = new Vector2(0, 10);
		StyleBoxFlat btnPressed = (StyleBoxFlat)baseStyle.Duplicate();
		btnPressed.BgColor = new Color(0.04f, 0.04f, 0.04f, 1f); btnPressed.BorderColor = new Color(0.6f, 0.5f, 0.1f, 1f);
		btnPressed.ShadowSize = 2; btnPressed.ShadowOffset = new Vector2(0, 2);

		MasterTheme = new Theme();
		_fantasyFont = GD.Load<Font>("res://fonts/yoster.ttf"); 
		if (_fantasyFont != null)
		{
			MasterTheme.DefaultFont = _fantasyFont;
			MasterTheme.SetFont("normal_font", "RichTextLabel", _fantasyFont);
			MasterTheme.SetFont("bold_font", "RichTextLabel", _fantasyFont);
		}
		
		MasterTheme.SetStylebox("panel", "PanelContainer", baseStyle);
		MasterTheme.SetStylebox("normal", "Button", btnNormal); MasterTheme.SetStylebox("hover", "Button", btnHover);
		MasterTheme.SetStylebox("pressed", "Button", btnPressed); MasterTheme.SetStylebox("focus", "Button", new StyleBoxEmpty());
		MasterTheme.SetFontSize("normal_font_size", "RichTextLabel", 26); MasterTheme.SetColor("default_color", "RichTextLabel", new Color(0.95f, 0.95f, 0.95f, 1f));
		MasterTheme.SetFontSize("font_size", "Label", 22); MasterTheme.SetColor("font_color", "Label", new Color(0.95f, 0.95f, 0.95f, 1f));
		
		if (ActionMenu != null) ActionMenu.Theme = MasterTheme;
		if (StatsLabel != null)
		{
			StatsLabel.Theme = MasterTheme;
			StatsLabel.AddThemeStyleboxOverride("normal", baseStyle);
			StatsLabel.AddThemeColorOverride("default_color", new Color(0.95f, 0.95f, 0.95f, 1f));
			StatsLabel.AddThemeFontSizeOverride("normal_font_size", 24); StatsLabel.AddThemeFontSizeOverride("bold_font_size", 28); 
			StatsLabel.BbcodeEnabled = true; StatsLabel.FitContent = true; StatsLabel.ScrollActive = false;
			StatsLabel.CustomMinimumSize = new Vector2(350, 0); StatsLabel.ClipContents = false;
		}

		Button[] buttons = { AttackButton, EndTurnButton, PartyButton };
		foreach (Button btn in buttons)
		{
			if (btn == null) continue;
			btn.AddThemeStyleboxOverride("normal", btnNormal); btn.AddThemeStyleboxOverride("hover", btnHover);
			btn.AddThemeStyleboxOverride("pressed", btnPressed); btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			btn.AddThemeFontSizeOverride("font_size", 24); 
			AddButtonJuice(btn);
		}
		
		ItemSlotStyle = new StyleBoxFlat { BgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2, BorderColor = new Color(0.25f, 0.25f, 0.3f, 1f) };
		ItemSlotHoverStyle = (StyleBoxFlat)ItemSlotStyle.Duplicate(); ItemSlotHoverStyle.BgColor = new Color(0.2f, 0.2f, 0.25f, 1f); ItemSlotHoverStyle.BorderColor = new Color(1f, 0.85f, 0.3f, 1f);
	}

	private void UpdateStatsUI()
	{
		if (StatsLabel == null) return;

		if (_selectedUnit != null && _selectedUnit.Data != null)
		{
			string moveStr = _selectedUnit.HasMoved ? "[color=#666666]Used[/color]" : "[color=#44ff44][pulse freq=1.5 color=#ffffff40]READY[/pulse][/color]";
			string atkStr = _selectedUnit.HasAttacked ? "[color=#666666]Used[/color]" : "[color=#ffaa44][pulse freq=1.5 color=#ffffff40]READY[/pulse][/color]";
			StatsLabel.Text = $"[center][b][wave amp=20 freq=3]{_selectedUnit.Data.Profile.Name}[/wave][/b]\n[color=gold]Lv.{_selectedUnit.Data.Level}[/color] | HP: [color=#ff4444]{_selectedUnit.Data.CurrentHP}[/color]/{_selectedUnit.Data.GetTotalMaxHP()}\nMove: {moveStr} | Attack: {atkStr}[/center]";
		}
		else StatsLabel.Text = "[center]\nSelect a Unit...[/center]";

		StatsLabel.PivotOffset = StatsLabel.Size / 2;
		Tween popTween = CreateTween();
		popTween.TweenProperty(StatsLabel, "scale", new Vector2(1.05f, 1.05f), 0.08f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		popTween.TweenProperty(StatsLabel, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
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

	private void AddButtonJuice(Button btn)
	{
		btn.MouseEntered += () => { btn.PivotOffset = btn.Size / 2; CreateTween().TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); };
		btn.MouseExited += () => { CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out); };
		btn.ButtonDown += () => { CreateTween().TweenProperty(btn, "scale", new Vector2(0.9f, 0.9f), 0.1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out); };
		btn.ButtonUp += () => { CreateTween().TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); };
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

	private void ShowUnitInfo(Unit u) { 
		if (StatsLabel != null && u.Data != null) 
			StatsLabel.Text = $"Enemy Unit\nLv.{u.Data.Level} HP: {u.Data.CurrentHP}/{u.Data.GetTotalMaxHP()}\nDmg: {u.Data.GetTotalDamage()}"; 
	}

	private async Task ShowTurnAnnouncer(string text, Color color)
	{
		Label announcer = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		announcer.AddThemeFontSizeOverride("font_size", 100); announcer.AddThemeColorOverride("font_color", color);
		announcer.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0)); announcer.AddThemeConstantOverride("outline_size", 20);
		if (_fantasyFont != null) announcer.AddThemeFontOverride("font", _fantasyFont);
		
		announcer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(announcer); else AddChild(announcer);

		announcer.PivotOffset = GetViewport().GetVisibleRect().Size / 2;
		announcer.Scale = Vector2.Zero; announcer.Modulate = new Color(1, 1, 1, 0);

		Tween tween = CreateTween();
		tween.Parallel().TweenProperty(announcer, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 1.0f, 0.3f);
		tween.Chain().TweenInterval(1.0f);
		tween.Chain().TweenProperty(announcer, "scale", new Vector2(1.5f, 1.5f), 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Parallel().TweenProperty(announcer, "modulate:a", 0.0f, 0.3f);
		tween.Finished += () => announcer.QueueFree();

		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private void SpawnFloatingText(Vector3 targetPosition, string text, Color color, float size = 30)
	{
		Label3D label = new Label3D { Text = text, PixelSize = 0.02f, FontSize = (int)size, Modulate = color, OutlineModulate = new Color(0, 0, 0), OutlineSize = 6, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true };
		if (_fantasyFont != null) label.Font = _fantasyFont;
		AddChild(label);
		label.GlobalPosition = targetPosition + new Vector3(0, 1.5f, 0);

		Tween tween = CreateTween();
		tween.TweenProperty(label, "global_position", label.GlobalPosition + new Vector3(0, 1.2f, 0), 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Finished += () => label.QueueFree();
	}

	public async Task ShowLevelUpScreen(Unit unit)
	{
		_levelUpActive = true; ShowActions(false);
		if (DimOverlay != null) DimOverlay.Visible = true;

		ActiveLevelUpTcs = new TaskCompletionSource<bool>();

		Control uiRoot = new Control(); uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (MasterTheme != null) uiRoot.Theme = MasterTheme;
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(uiRoot); else AddChild(uiRoot);

		CenterContainer center = new CenterContainer(); center.SetAnchorsPreset(Control.LayoutPreset.FullRect); uiRoot.AddChild(center);

		PanelContainer panel = new PanelContainer();
		StyleBoxFlat panelStyle = new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f), CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15, CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15, BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4, BorderColor = new Color(1f, 0.8f, 0f, 1f), ContentMarginBottom = 30, ContentMarginTop = 30, ContentMarginLeft = 40, ContentMarginRight = 40 };
		panel.AddThemeStyleboxOverride("panel", panelStyle); center.AddChild(panel);

		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; vbox.AddThemeConstantOverride("separation", 15); panel.AddChild(vbox);

		Label title = new Label { Text = $"{unit.Data.Profile.Name} Reached Level {unit.Data.Level}!", HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 30); vbox.AddChild(title);

		List<string> options = new List<string> { "FullHeal", "MaxHP", "Movement", "AttackDamage" }.OrderBy(x => new System.Random().Next()).Take(3).ToList();

		foreach (string opt in options)
		{
			Button btn = new Button { CustomMinimumSize = new Vector2(300, 60) }; btn.AddThemeFontSizeOverride("font_size", 20);
			if (opt == "FullHeal") btn.Text = "Fully Restore HP"; else if (opt == "MaxHP") btn.Text = "+5 Max HP"; else if (opt == "Movement") btn.Text = "+1 Movement Range"; else if (opt == "AttackDamage") btn.Text = "+2 Attack Damage";

			btn.Pressed += () => {
				if (opt == "FullHeal") unit.Data.CurrentHP = unit.Data.MaxHP; else if (opt == "MaxHP") { unit.Data.MaxHP += 5; unit.Data.CurrentHP += 5; } else if (opt == "Movement") unit.Data.Movement += 1; else if (opt == "AttackDamage") unit.Data.AttackDamage += 2;
				unit.UpdateVisuals();

				Tween outTween = CreateTween();
				outTween.TweenProperty(panel, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
				outTween.Finished += () => {
					uiRoot.QueueFree(); if (DimOverlay != null && !_dialogueActive) DimOverlay.Visible = false;
					_levelUpActive = false; if (_currentState == State.PlayerTurn && _selectedUnit != null) ShowActions(true);
					
					// === THE FIX: Complete the task safely ===
					ActiveLevelUpTcs?.TrySetResult(true);
					ActiveLevelUpTcs = null;
				};
			};
			vbox.AddChild(btn);
		}

		await ToSignal(GetTree(), "process_frame");
		panel.PivotOffset = panel.Size / 2; panel.Scale = Vector2.Zero;
		CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		try { await ActiveLevelUpTcs.Task; } catch { /* Ignore task cancellation safely */ } 
	}

	public async Task RollForLoot() { if (GD.Randf() > 0.8f) return; var items = ItemDatabase.Values.ToList(); await ShowLootScreen(items[GD.RandRange(0, items.Count - 1)].Clone()); }

	public async Task ShowLootScreen(Equipment item)
	{
		_lootScreenActive = true; ShowActions(false);
		if (DimOverlay != null) DimOverlay.Visible = true;

		ActiveLootTcs = new TaskCompletionSource<bool>();
		Control uiRoot = new Control(); uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(uiRoot); else AddChild(uiRoot);

		PanelContainer panel = new PanelContainer { Theme = MasterTheme, CustomMinimumSize = new Vector2(400, 0) }; uiRoot.AddChild(panel);
		panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		panel.Position = new Vector2((GetViewport().GetVisibleRect().Size.X / 2f) - 200f, -300f); 

		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; vbox.AddThemeConstantOverride("separation", 15); panel.AddChild(vbox);
		Label title = new Label { Text = "Loot Found!", HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 28); title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.2f)); vbox.AddChild(title);
		vbox.AddChild(new TextureRect { Texture = GD.Load<Texture2D>(item.IconPath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(80, 80), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });

		RichTextLabel itemName = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Text = $"[center]{item.Name}\n[color=#aaaaaa]{item.Description}[/color][/center]", CustomMinimumSize = new Vector2(350, 0) };
		itemName.AddThemeFontSizeOverride("normal_font_size", 20); vbox.AddChild(itemName);

		Button grabBtn = new Button { Text = "Grab", CustomMinimumSize = new Vector2(200, 60) }; AddButtonJuice(grabBtn); vbox.AddChild(grabBtn);
		CreateTween().TweenProperty(panel, "position:y", 150f, 0.6f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);

		grabBtn.Pressed += () => {
			Inventory.Add(item);
			Tween outTween = CreateTween(); outTween.TweenProperty(panel, "position:y", -300f, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			outTween.Finished += () => { 
				uiRoot.QueueFree(); if (DimOverlay != null && !_levelUpActive && !_dialogueActive) DimOverlay.Visible = false; _lootScreenActive = false; 
				
				// === THE FIX: Complete the task safely ===
				ActiveLootTcs?.TrySetResult(true); 
				ActiveLootTcs = null;
			};
		};
		try { await ActiveLootTcs.Task; } catch { /* Ignore task cancellation safely */ }
	}

	private void TogglePartyMenu()
	{
		if (_currentState != State.PlayerTurn && _currentState != State.PartyMenu) return;

		if (_activePartyMenu != null)
		{
			Tween outTween = CreateTween();
			outTween.TweenProperty(_activePartyMenu, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			outTween.Finished += () => { _activePartyMenu.QueueFree(); _activePartyMenu = null; if (DimOverlay != null) DimOverlay.Visible = false; _currentState = State.PlayerTurn; ShowActions(true); };
			return;
		}

		_currentState = State.PartyMenu; ShowActions(false);
		if (DimOverlay != null) DimOverlay.Visible = true;

		_activePartyMenu = new CenterContainer(); _activePartyMenu.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(_activePartyMenu); else AddChild(_activePartyMenu);

		VBoxContainer menuWrapper = new VBoxContainer(); menuWrapper.AddThemeConstantOverride("separation", 20); _activePartyMenu.AddChild(menuWrapper);
		PanelContainer mainPanel = new PanelContainer { CustomMinimumSize = new Vector2(950, 480), Theme = MasterTheme }; menuWrapper.AddChild(mainPanel);
		HBoxContainer mainHBox = new HBoxContainer(); mainPanel.AddChild(mainHBox);
		VBoxContainer leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) }; mainHBox.AddChild(leftCol);

		Label rosterTitle = new Label { Text = "Party Roster", HorizontalAlignment = HorizontalAlignment.Center }; rosterTitle.AddThemeFontSizeOverride("font_size", 28); leftCol.AddChild(rosterTitle); leftCol.AddChild(new HSeparator());
		ScrollContainer rosterScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, VerticalScrollMode = ScrollContainer.ScrollMode.Auto, SizeFlagsVertical = Control.SizeFlags.ExpandFill }; leftCol.AddChild(rosterScroll);
		MarginContainer rosterMargin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rosterMargin.AddThemeConstantOverride("margin_left", 16); rosterMargin.AddThemeConstantOverride("margin_right", 16); rosterMargin.AddThemeConstantOverride("margin_top", 10); rosterMargin.AddThemeConstantOverride("margin_bottom", 10); rosterScroll.AddChild(rosterMargin);
		VBoxContainer rosterBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rosterBox.AddThemeConstantOverride("separation", 8); rosterMargin.AddChild(rosterBox);

		VSeparator splitLine = new VSeparator(); splitLine.AddThemeConstantOverride("separation", 30); mainHBox.AddChild(splitLine);
		_rightMenuPanel = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; mainHBox.AddChild(_rightMenuPanel);

		foreach (PersistentUnit unit in _party)
		{
			Button btn = new Button { Text = unit.Profile.Name, CustomMinimumSize = new Vector2(0, 60) }; AddButtonJuice(btn);
			btn.Pressed += () => RefreshPartyDetailsAndInventory(unit); rosterBox.AddChild(btn);
		}

		leftCol.AddChild(new HSeparator());
		Button closeBtn = new Button { Text = "Close Menu", CustomMinimumSize = new Vector2(0, 50) }; closeBtn.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f)); AddButtonJuice(closeBtn); closeBtn.Pressed += TogglePartyMenu; leftCol.AddChild(closeBtn);

		menuWrapper.PivotOffset = new Vector2(475, 350); menuWrapper.Scale = Vector2.Zero;
		CreateTween().TweenProperty(menuWrapper, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		if (_party.Count > 0) RefreshPartyDetailsAndInventory(_party[0]);
	}

	private void RefreshPartyDetailsAndInventory(PersistentUnit unit)
	{
		_viewedPartyMember = unit;
		if (_rightMenuPanel != null)
		{
			foreach (Node child in _rightMenuPanel.GetChildren()) child.QueueFree();
			_rightMenuPanel.AddChild(BuildPartyMemberDetails(unit));
			_rightMenuPanel.AddChild(BuildInventoryUI());
			_rightMenuPanel.Modulate = new Color(1, 1, 1, 0); CreateTween().TweenProperty(_rightMenuPanel, "modulate:a", 1.0f, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		}
	}

	private Control BuildPartyMemberDetails(PersistentUnit unit)
	{
		HBoxContainer layout = new HBoxContainer(); layout.AddThemeConstantOverride("separation", 40);
		layout.AddChild(new TextureRect { Texture = GD.Load<Texture2D>(unit.Profile.SpritePath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(240, 350), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });

		VBoxContainer rightCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center }; rightCol.AddThemeConstantOverride("separation", 15); layout.AddChild(rightCol);
		rightCol.AddChild(new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Text = $"[left][font_size=32][b][wave amp=20 freq=2]{unit.Profile.Name}[/wave][/b][/font_size]\n[color=gold]Level {unit.Level}[/color][/left]" });
		rightCol.AddChild(new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Text = $"[left]{(unit.IsPlayerCharacter ? "[color=#44ff44]Player Character[/color]" : "[color=#44ccff]Companion[/color]")}\n\n[color=#aaaaaa]Max HP:[/color] {unit.GetTotalMaxHP()}\n[color=#aaaaaa]Damage:[/color] {unit.GetTotalDamage()}\n[color=#aaaaaa]Movement:[/color] {unit.GetTotalMovement()}[/left]" });

		rightCol.AddChild(new HSeparator());
		Label equipTitle = new Label { Text = "Equipment (Click to Remove)" }; equipTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f)); rightCol.AddChild(equipTitle);

		HBoxContainer equipBox = new HBoxContainer(); equipBox.AddThemeConstantOverride("separation", 20); rightCol.AddChild(equipBox);
		equipBox.AddChild(CreateEquipmentSlot("Weapon", unit.EquippedWeapon, EquipSlot.Weapon)); equipBox.AddChild(CreateEquipmentSlot("Armor", unit.EquippedArmor, EquipSlot.Armor));

		if (!unit.IsPlayerCharacter)
		{
			rightCol.AddChild(new HSeparator());
			Label relTitle = new Label { Text = "Dynamics with You" }; relTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f)); rightCol.AddChild(relTitle);

			foreach (var rel in unit.Relationships)
			{
				VBoxContainer barBox = new VBoxContainer();
				Color barColor = rel.Key == "Fear" ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.3f, 0.8f, 0.5f);
				ProgressBar bar = new ProgressBar { CustomMinimumSize = new Vector2(0, 22), ShowPercentage = false, MaxValue = 100 };
				bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f), CornerRadiusTopLeft=6, CornerRadiusTopRight=6, CornerRadiusBottomLeft=6, CornerRadiusBottomRight=6 });
				bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = barColor, CornerRadiusTopLeft=6, CornerRadiusTopRight=6, CornerRadiusBottomLeft=6, CornerRadiusBottomRight=6 });

				Label barLabel = new Label { Text = $"  {rel.Key}: {rel.Value}%", VerticalAlignment = VerticalAlignment.Center }; barLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect); barLabel.AddThemeFontSizeOverride("font_size", 14); barLabel.AddThemeColorOverride("font_color", Color.Color8(255,255,255,220)); bar.AddChild(barLabel);
				barBox.AddChild(bar); rightCol.AddChild(barBox); bar.Value = 0;
				bar.TreeEntered += () => { CreateTween().TweenProperty(bar, "value", (double)rel.Value, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out); };
			}
		}
		return layout;
	}

	private Control CreateEquipmentSlot(string slotName, Equipment equip, EquipSlot slotType)
	{
		VBoxContainer slotBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		Label title = new Label { Text = slotName, HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 14); title.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f)); slotBox.AddChild(title);

		Button slotBtn = new Button { CustomMinimumSize = new Vector2(80, 80), IconAlignment = HorizontalAlignment.Center, ExpandIcon = true };
		slotBtn.AddThemeStyleboxOverride("normal", ItemSlotStyle); slotBtn.AddThemeStyleboxOverride("hover", ItemSlotHoverStyle); slotBtn.AddThemeStyleboxOverride("pressed", ItemSlotStyle); slotBtn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

		if (equip != null) 
		{
			slotBtn.Icon = GD.Load<Texture2D>(equip.IconPath); slotBtn.TooltipText = $"{equip.Name}\n{equip.Description}";
			slotBtn.Pressed += () => { 
				if (slotType == EquipSlot.Weapon) _viewedPartyMember.EquippedWeapon = null; 
				else _viewedPartyMember.EquippedArmor = null; 
				Inventory.Add(equip); 
				
				// === NEW: Force the 3D board unit and Stats UI to catch the change! ===
				Unit activeUnit = _units.FirstOrDefault(u => u.Data == _viewedPartyMember);
				if (activeUnit != null) activeUnit.UpdateVisuals();
				UpdateStatsUI();

				RefreshPartyDetailsAndInventory(_viewedPartyMember); 
			};
		}
		else { slotBtn.Text = "+"; slotBtn.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.3f)); }

		slotBox.AddChild(slotBtn); AddSlotJuice(slotBtn, 0.1f); return slotBox;
	}

	private PanelContainer BuildInventoryUI()
	{
		PanelContainer invPanel = new PanelContainer { Theme = MasterTheme }; VBoxContainer invBox = new VBoxContainer(); invPanel.AddChild(invBox);
		Label invTitle = new Label { Text = "Party Inventory (Click to Equip to Current Unit)" }; invTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f)); invBox.AddChild(invTitle); invBox.AddChild(new HSeparator());

		GridContainer grid = new GridContainer { Columns = 10 }; grid.AddThemeConstantOverride("h_separation", 10); grid.AddThemeConstantOverride("v_separation", 10); invBox.AddChild(grid);

		for (int i = 0; i < 20; i++)
		{
			Button slotBtn = new Button { CustomMinimumSize = new Vector2(70, 70), IconAlignment = HorizontalAlignment.Center, ExpandIcon = true };
			slotBtn.AddThemeStyleboxOverride("normal", ItemSlotStyle); slotBtn.AddThemeStyleboxOverride("hover", ItemSlotHoverStyle); slotBtn.AddThemeStyleboxOverride("pressed", ItemSlotStyle); slotBtn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
			
			if (i < Inventory.Count && Inventory[i] is Equipment equipItem)
			{
				slotBtn.Icon = GD.Load<Texture2D>(equipItem.IconPath); slotBtn.TooltipText = $"{equipItem.Name}\n{equipItem.Description}";
				slotBtn.Pressed += () => {
					Inventory.Remove(equipItem);
					Equipment oldItem = equipItem.Slot == EquipSlot.Weapon ? _viewedPartyMember.EquippedWeapon : _viewedPartyMember.EquippedArmor;
					if (oldItem != null) Inventory.Add(oldItem);
					if (equipItem.Slot == EquipSlot.Weapon) _viewedPartyMember.EquippedWeapon = equipItem; else _viewedPartyMember.EquippedArmor = equipItem;
					
					// === NEW: Force the 3D board unit and Stats UI to catch the change! ===
					Unit activeUnit = _units.FirstOrDefault(u => u.Data == _viewedPartyMember);
					if (activeUnit != null) activeUnit.UpdateVisuals();
					UpdateStatsUI();

					RefreshPartyDetailsAndInventory(_viewedPartyMember);
				};
			}
			grid.AddChild(slotBtn); AddSlotJuice(slotBtn, i * 0.015f);
		}
		return invPanel;
	}

	private void AddSlotJuice(Button slot, float delay = 0f)
	{
		slot.PivotOffset = slot.CustomMinimumSize / 2; slot.Scale = Vector2.Zero;
		CreateTween().TweenProperty(slot, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(delay);
		slot.MouseEntered += () => { slot.PivotOffset = slot.Size / 2; CreateTween().TweenProperty(slot, "scale", new Vector2(1.1f, 1.1f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); };
		slot.MouseExited += () => { CreateTween().TweenProperty(slot, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out); };
		slot.ButtonDown += () => { CreateTween().TweenProperty(slot, "scale", new Vector2(0.9f, 0.9f), 0.1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out); };
	}

	private void ShowRelationshipNotification(string charName, string relType, int amount)
	{
		string sign = amount > 0 ? "+" : ""; Color themeColor = amount > 0 ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3, BorderColor = themeColor, ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.5f) });

		MarginContainer margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 20); margin.AddThemeConstantOverride("margin_right", 20); margin.AddThemeConstantOverride("margin_top", 10); margin.AddThemeConstantOverride("margin_bottom", 10); panel.AddChild(margin);
		Label lbl = new Label { Text = $"{charName}: {sign}{amount} {relType}", HorizontalAlignment = HorizontalAlignment.Center }; lbl.AddThemeFontSizeOverride("font_size", 22); lbl.AddThemeColorOverride("font_color", themeColor);
		if (MasterTheme != null && MasterTheme.DefaultFont != null) lbl.AddThemeFontOverride("font", MasterTheme.DefaultFont);
		margin.AddChild(lbl);

		if (DimOverlay != null) DimOverlay.GetParent().AddChild(panel); else AddChild(panel);
		CallDeferred(MethodName.AnimateRelationshipNotification, panel, GetViewport().GetVisibleRect().Size);
	}

	private void AnimateRelationshipNotification(PanelContainer panel, Vector2 screenSize)
	{
		float startX = screenSize.X + 50f; float endX = screenSize.X - panel.Size.X - 40f;
		panel.Position = new Vector2(startX, 40f);

		Tween t = CreateTween();
		t.TweenProperty(panel, "position:x", endX, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenInterval(2.5f);
		t.TweenProperty(panel, "position:x", startX, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		t.Finished += () => panel.QueueFree();
	}
	
	private void ShowNextMissionScreen()
	{
		_currentState = State.Cutscene;
		ShowActions(false);
		if (DimOverlay != null) DimOverlay.Visible = true;

		Control uiRoot = new Control(); 
		uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(uiRoot); else AddChild(uiRoot);

		CenterContainer center = new CenterContainer { Theme = MasterTheme }; 
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect); 
		uiRoot.AddChild(center);

		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", BaseUIStyle); 
		center.AddChild(panel);

		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; 
		vbox.AddThemeConstantOverride("separation", 20); 
		panel.AddChild(vbox);

		Label title = new Label { Text = "MISSION COMPLETE", HorizontalAlignment = HorizontalAlignment.Center }; 
		title.AddThemeFontSizeOverride("font_size", 40); 
		title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.5f)); 
		vbox.AddChild(title);

		Label subtitle = new Label { Text = "The party rests and recovers some HP.", HorizontalAlignment = HorizontalAlignment.Center };
		subtitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		vbox.AddChild(subtitle);

		Button nextBtn = new Button { Text = "Start Next Mission", CustomMinimumSize = new Vector2(250, 60) }; 
		AddButtonJuice(nextBtn); 
		vbox.AddChild(nextBtn);

		// Entrance Animation
		panel.PivotOffset = new Vector2(200, 100); // Approximate center
		panel.Scale = Vector2.Zero;
		CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		nextBtn.Pressed += () => {
			Tween outTween = CreateTween();
			outTween.TweenProperty(panel, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			outTween.Finished += () => {
				uiRoot.QueueFree();
				if (DimOverlay != null) DimOverlay.Visible = false;
				
				// Clean up the old board and load the next JSON!
				ClearBoard();
				LoadMission(_currentMissionIndex + 1);
			};
		};
	}
}
