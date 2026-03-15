// GameManager.UI.cs — Stats panel overhaul: portrait, HP bar, glowing orbs
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	// === STATS PANEL (replaces text-only StatsLabel for unit display) ===
	private PanelContainer _statsPanel;
	private TextureRect _statsPortrait;
	private Label _statsName;
	private ProgressBar _statsHpBar;
	private Label _statsHpLabel;
	private PanelContainer _statsMoveOrb;
	private PanelContainer _statsAttackOrb;
	private Label _statsMoveLabel;
	private Label _statsAttackLabel;

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

		// Hide the old text-only StatsLabel — we'll use our new panel
		if (StatsLabel != null) StatsLabel.Visible = false;

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

		// Build new stats panel
		BuildStatsPanel();
	}

	// ============================================================
	// STATS PANEL — Modern RPG style with portrait, HP bar, orbs
	// ============================================================

	private void BuildStatsPanel()
	{
		_statsPanel = new PanelContainer { CustomMinimumSize = new Vector2(360, 0) };
		_statsPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f),
			CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14, CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
			BorderWidthBottom = 3, BorderWidthTop = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
			BorderColor = new Color(0.35f, 0.35f, 0.4f),
			ShadowSize = 10, ShadowColor = new Color(0, 0, 0, 0.6f), ShadowOffset = new Vector2(0, 5),
			ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10, ContentMarginBottom = 10
		});

		if (MasterTheme != null) _statsPanel.Theme = MasterTheme;

		// Layout: [Portrait | Info Column]
		HBoxContainer mainRow = new HBoxContainer();
		mainRow.AddThemeConstantOverride("separation", 12);
		_statsPanel.AddChild(mainRow);

		// Portrait
		_statsPortrait = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(70, 80),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
		};
		mainRow.AddChild(_statsPortrait);

		// Info column
		VBoxContainer infoCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		infoCol.AddThemeConstantOverride("separation", 6);
		mainRow.AddChild(infoCol);

		// Name + Level row
		_statsName = new Label { Text = "—", HorizontalAlignment = HorizontalAlignment.Left };
		_statsName.AddThemeFontSizeOverride("font_size", 22);
		_statsName.AddThemeColorOverride("font_outline_color", Colors.Black);
		_statsName.AddThemeConstantOverride("outline_size", 4);
		if (_fantasyFont != null) _statsName.AddThemeFontOverride("font", _fantasyFont);
		infoCol.AddChild(_statsName);

		// HP Bar
		MarginContainer hpMargin = new MarginContainer { CustomMinimumSize = new Vector2(0, 28) };
		_statsHpBar = new ProgressBar { CustomMinimumSize = new Vector2(0, 28), ShowPercentage = false, MaxValue = 100, Value = 100 };
		_statsHpBar.AddThemeStyleboxOverride("background", new StyleBoxFlat
		{
			BgColor = new Color(0.15f, 0.05f, 0.05f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
			BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			BorderColor = new Color(0.3f, 0.1f, 0.1f)
		});
		_statsHpBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat
		{
			BgColor = new Color(0.2f, 0.85f, 0.3f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
		});

		_statsHpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_statsHpLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_statsHpLabel.AddThemeFontSizeOverride("font_size", 15);
		_statsHpLabel.AddThemeConstantOverride("outline_size", 4);
		_statsHpLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		if (_fantasyFont != null) _statsHpLabel.AddThemeFontOverride("font", _fantasyFont);
		_statsHpBar.AddChild(_statsHpLabel);

		hpMargin.AddChild(_statsHpBar);
		infoCol.AddChild(hpMargin);

		// Action orbs row: [Move Orb] [Attack Orb]
		HBoxContainer orbRow = new HBoxContainer();
		orbRow.AddThemeConstantOverride("separation", 16);
		infoCol.AddChild(orbRow);

		// Move orb
		VBoxContainer moveCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		moveCol.AddThemeConstantOverride("separation", 2);
		_statsMoveOrb = CreateOrbIndicator(new Color(0.2f, 0.8f, 1.0f));
		_statsMoveLabel = new Label { Text = "MOVE", HorizontalAlignment = HorizontalAlignment.Center };
		_statsMoveLabel.AddThemeFontSizeOverride("font_size", 11);
		_statsMoveLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_statsMoveLabel.AddThemeConstantOverride("outline_size", 3);
		if (_fantasyFont != null) _statsMoveLabel.AddThemeFontOverride("font", _fantasyFont);
		moveCol.AddChild(_statsMoveOrb);
		moveCol.AddChild(_statsMoveLabel);
		orbRow.AddChild(moveCol);

		// Attack orb
		VBoxContainer atkCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		atkCol.AddThemeConstantOverride("separation", 2);
		_statsAttackOrb = CreateOrbIndicator(new Color(1.0f, 0.5f, 0.2f));
		_statsAttackLabel = new Label { Text = "ATTACK", HorizontalAlignment = HorizontalAlignment.Center };
		_statsAttackLabel.AddThemeFontSizeOverride("font_size", 11);
		_statsAttackLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_statsAttackLabel.AddThemeConstantOverride("outline_size", 3);
		if (_fantasyFont != null) _statsAttackLabel.AddThemeFontOverride("font", _fantasyFont);
		atkCol.AddChild(_statsAttackOrb);
		atkCol.AddChild(_statsAttackLabel);
		orbRow.AddChild(atkCol);

		_statsPanel.Visible = false;

		// Position it where StatsLabel was
		if (StatsLabel != null && StatsLabel.GetParent() != null)
		{
			StatsLabel.GetParent().AddChild(_statsPanel);
			_statsPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
			_statsPanel.Position = new Vector2(20, -180);
		}
		else
		{
			AddChild(_statsPanel);
		}
	}

	private PanelContainer CreateOrbIndicator(Color litColor)
	{
		PanelContainer orb = new PanelContainer { CustomMinimumSize = new Vector2(32, 32) };
		orb.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = litColor,
			CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16,
			CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16,
			BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
			BorderColor = new Color(1f, 1f, 1f, 0.3f),
			ShadowSize = 8,
			ShadowColor = new Color(litColor.R, litColor.G, litColor.B, 0.6f),
			ShadowOffset = new Vector2(0, 2)
		});
		return orb;
	}

	private void SetOrbLit(PanelContainer orb, Color litColor, bool isLit)
	{
		StyleBoxFlat style = (StyleBoxFlat)orb.GetThemeStylebox("panel");
		if (isLit)
		{
			style.BgColor = litColor;
			style.ShadowSize = 8;
			style.ShadowColor = new Color(litColor.R, litColor.G, litColor.B, 0.6f);
			style.BorderColor = new Color(1f, 1f, 1f, 0.3f);
		}
		else
		{
			style.BgColor = new Color(0.12f, 0.12f, 0.15f);
			style.ShadowSize = 0;
			style.BorderColor = new Color(0.2f, 0.2f, 0.25f);
		}
	}

	// ============================================================
	// UPDATE STATS UI — visual panel update
	// ============================================================

	private void UpdateStatsUI()
	{
		if (_statsPanel == null) return;

		if (_selectedUnit != null && _selectedUnit.Data != null && GodotObject.IsInstanceValid(_selectedUnit))
		{
			_statsPanel.Visible = true;
			var d = _selectedUnit.Data;

			// Portrait
			Texture2D tex = GD.Load<Texture2D>(d.Profile.SpritePath);
			_statsPortrait.Texture = tex;

			// Name + Level
			string levelColor = _selectedUnit.IsFriendly ? "gold" : "#ff6666";
			_statsName.Text = $"{d.Profile.Name}  Lv.{d.Level}";
			_statsName.AddThemeColorOverride("font_color",
				_selectedUnit.IsFriendly ? new Color(1f, 0.95f, 0.8f) : new Color(1f, 0.5f, 0.5f));

			// HP Bar
			int maxHp = d.GetTotalMaxHP();
			_statsHpBar.MaxValue = maxHp;

			// Animate HP bar smoothly
			CreateTween().TweenProperty(_statsHpBar, "value", (double)d.CurrentHP, 0.2f)
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_statsHpLabel.Text = $"HP  {d.CurrentHP} / {maxHp}";

			// Color HP bar based on ratio
			float ratio = maxHp > 0 ? (float)d.CurrentHP / maxHp : 0;
			Color hpColor;
			if (ratio > 0.6f) hpColor = new Color(0.2f, 0.85f, 0.3f);
			else if (ratio > 0.3f) hpColor = new Color(0.9f, 0.75f, 0.15f);
			else hpColor = new Color(0.9f, 0.2f, 0.2f);
			((StyleBoxFlat)_statsHpBar.GetThemeStylebox("fill")).BgColor = hpColor;

			// Orbs
			if (_selectedUnit.IsFriendly)
			{
				SetOrbLit(_statsMoveOrb, new Color(0.2f, 0.8f, 1.0f), !_selectedUnit.HasMoved);
				SetOrbLit(_statsAttackOrb, new Color(1.0f, 0.5f, 0.2f), !_selectedUnit.HasAttacked);
				_statsMoveLabel.AddThemeColorOverride("font_color",
					_selectedUnit.HasMoved ? new Color(0.3f, 0.3f, 0.35f) : new Color(0.5f, 0.9f, 1f));
				_statsAttackLabel.AddThemeColorOverride("font_color",
					_selectedUnit.HasAttacked ? new Color(0.3f, 0.3f, 0.35f) : new Color(1f, 0.7f, 0.4f));
				_statsMoveOrb.Visible = true; _statsAttackOrb.Visible = true;
				_statsMoveLabel.Visible = true; _statsAttackLabel.Visible = true;
			}
			else
			{
				// Enemy — hide orbs
				_statsMoveOrb.Visible = false; _statsAttackOrb.Visible = false;
				_statsMoveLabel.Visible = false; _statsAttackLabel.Visible = false;
			}

			// Juicy pop
			_statsPanel.PivotOffset = _statsPanel.Size / 2;
			Tween pop = CreateTween();
			pop.TweenProperty(_statsPanel, "scale", new Vector2(1.04f, 1.04f), 0.08f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			pop.TweenProperty(_statsPanel, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Sine);
		}
		else
		{
			_statsPanel.Visible = false;
		}
	}

	// ============================================================
	// SHOW UNIT INFO (clickable info for non-selected units)
	// ============================================================

	private void ShowUnitInfo(Unit u)
	{
		if (u == null || u.Data == null) return;

		// Temporarily set as selected for display, then clear
		var prev = _selectedUnit;
		_selectedUnit = u;
		UpdateStatsUI();
		_selectedUnit = prev;
	}

	// ============================================================
	// ACTIONS UI (unchanged logic, kept)
	// ============================================================

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

	// ============================================================
	// TURN ANNOUNCER + FLOATING TEXT (font applied)
	// ============================================================

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

	// ============================================================
	// LEVEL UP / LOOT / PARTY (preserved exactly from original)
	// ============================================================

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
				outTween.Finished += () => { uiRoot.QueueFree(); if (DimOverlay != null && !_dialogueActive) DimOverlay.Visible = false; _levelUpActive = false; if (_currentState == State.PlayerTurn && _selectedUnit != null) ShowActions(true); ActiveLevelUpTcs?.TrySetResult(true); ActiveLevelUpTcs = null; };
			};
			vbox.AddChild(btn);
		}

		await ToSignal(GetTree(), "process_frame");
		panel.PivotOffset = panel.Size / 2; panel.Scale = Vector2.Zero;
		CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		try { await ActiveLevelUpTcs.Task; } catch { }
	}

	public async Task RollForLoot()
	{
		if (GD.Randf() > 0.8f) return;
		var items = GameDatabase.Items.Values.ToList();
		await ShowLootScreen(items[GD.RandRange(0, items.Count - 1)].Clone());
	}

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
			outTween.Finished += () => { uiRoot.QueueFree(); if (DimOverlay != null && !_levelUpActive && !_dialogueActive) DimOverlay.Visible = false; _lootScreenActive = false; ActiveLootTcs?.TrySetResult(true); ActiveLootTcs = null; };
		};
		try { await ActiveLootTcs.Task; } catch { }
	}

	private void TogglePartyMenu()
	{
		if (_currentState != State.PlayerTurn && _currentState != State.PartyMenu && _currentState != State.Camp) return;

		if (_activePartyMenu != null)
		{
			Tween outTween = CreateTween();
			outTween.TweenProperty(_activePartyMenu, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			outTween.Finished += () => {
				_activePartyMenu.QueueFree(); _activePartyMenu = null;
				if (DimOverlay != null) DimOverlay.Visible = false;
				if (_campNodes.Count > 0) { _currentState = State.Camp; if (_campUIRoot != null) _campUIRoot.Visible = true; }
				else { _currentState = State.PlayerTurn; ShowActions(true); }
			};
			return;
		}

		if (_currentState == State.Camp && _campUIRoot != null) _campUIRoot.Visible = false;

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
		{ Button btn = new Button { Text = unit.Profile.Name, CustomMinimumSize = new Vector2(0, 60) }; AddButtonJuice(btn); btn.Pressed += () => RefreshPartyDetailsAndInventory(unit); rosterBox.AddChild(btn); }

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
				Color barColor = rel.Key == "Fear" ? new Color(0.8f, 0.3f, 0.3f) : new Color(0.3f, 0.8f, 0.5f);
				ProgressBar bar = new ProgressBar { CustomMinimumSize = new Vector2(0, 22), ShowPercentage = false, MaxValue = 100 };
				bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f), CornerRadiusTopLeft=6, CornerRadiusTopRight=6, CornerRadiusBottomLeft=6, CornerRadiusBottomRight=6 });
				bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = barColor, CornerRadiusTopLeft=6, CornerRadiusTopRight=6, CornerRadiusBottomLeft=6, CornerRadiusBottomRight=6 });
				Label barLabel = new Label { Text = $"  {rel.Key}: {rel.Value}%", VerticalAlignment = VerticalAlignment.Center }; barLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect); barLabel.AddThemeFontSizeOverride("font_size", 14); barLabel.AddThemeColorOverride("font_color", Color.Color8(255,255,255,220)); bar.AddChild(barLabel);
				rightCol.AddChild(bar); bar.Value = 0;
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
				if (slotType == EquipSlot.Weapon) _viewedPartyMember.EquippedWeapon = null; else _viewedPartyMember.EquippedArmor = null;
				Inventory.Add(equip);
				Unit activeUnit = _units.FirstOrDefault(u => u.Data == _viewedPartyMember);
				if (activeUnit != null) activeUnit.UpdateVisuals(); UpdateStatsUI();
				RefreshPartyDetailsAndInventory(_viewedPartyMember);
			};
		}
		else { slotBtn.Text = "+"; slotBtn.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.3f)); }

		slotBox.AddChild(slotBtn); AddSlotJuice(slotBtn, 0.1f); return slotBox;
	}

	private PanelContainer BuildInventoryUI()
	{
		PanelContainer invPanel = new PanelContainer { Theme = MasterTheme }; VBoxContainer invBox = new VBoxContainer(); invPanel.AddChild(invBox);
		Label invTitle = new Label { Text = "Party Inventory (Click to Equip)" }; invTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f)); invBox.AddChild(invTitle); invBox.AddChild(new HSeparator());

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
					Unit activeUnit = _units.FirstOrDefault(u => u.Data == _viewedPartyMember);
					if (activeUnit != null) activeUnit.UpdateVisuals(); UpdateStatsUI();
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

	private void ShowChoicePickedJuice()
	{
		Vector2 clickPos = GetViewport().GetMousePosition();
		CanvasLayer fxLayer = new CanvasLayer { Layer = 105 }; AddChild(fxLayer);
		if (Cam != null) { Tween ct = CreateTween(); if (Cam.Projection == Camera3D.ProjectionType.Orthogonal) { float os = Cam.Size; ct.TweenProperty(Cam, "size", os * 0.98f, 0.05f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out); ct.TweenProperty(Cam, "size", os, 0.15f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out); } else { float of2 = Cam.Fov; ct.TweenProperty(Cam, "fov", of2 - 1f, 0.05f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out); ct.TweenProperty(Cam, "fov", of2, 0.15f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out); } }
		var rng = new System.Random(); int pc = 12;
		Color[] colors = { new Color(1f, 0.85f, 0.2f), new Color(1f, 0.5f, 0.2f), new Color(1f, 0.95f, 0.6f) };
		for (int i = 0; i < pc; i++) { Label spark = new Label { Text = "✦", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
			spark.AddThemeFontSizeOverride("font_size", rng.Next(24, 50)); spark.AddThemeColorOverride("font_color", colors[rng.Next(colors.Length)]);
			spark.AddThemeColorOverride("font_outline_color", Colors.Black); spark.AddThemeConstantOverride("outline_size", 6);
			if (MasterTheme != null && MasterTheme.DefaultFont != null) spark.AddThemeFontOverride("font", MasterTheme.DefaultFont);
			fxLayer.AddChild(spark); CallDeferred(MethodName.AnimateChoiceSpark, spark, clickPos, rng.NextDouble(), rng.Next(60, 160)); }
		GetTree().CreateTimer(0.8f).Timeout += () => fxLayer.QueueFree();
	}

	private void AnimateChoiceSpark(Label spark, Vector2 origin, double rv, int dist)
	{
		spark.PivotOffset = spark.Size / 2f; spark.Position = origin - spark.PivotOffset; spark.Scale = Vector2.Zero;
		float angle = (float)(rv * Mathf.Pi * 2); Vector2 tp = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist - spark.PivotOffset;
		Tween t = CreateTween().SetParallel(true); float dur = 0.4f + (float)(rv * 0.2f);
		t.TweenProperty(spark, "position:x", tp.X, dur).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.Out);
		t.TweenProperty(spark, "position:y", tp.Y - 30f, dur * 0.3f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		t.TweenProperty(spark, "position:y", tp.Y + 60f, dur * 0.7f).SetDelay(dur * 0.3f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
		t.TweenProperty(spark, "scale", Vector2.One, 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(spark, "scale", Vector2.Zero, dur - 0.1f).SetDelay(0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		t.TweenProperty(spark, "rotation", (float)(rv - 0.5f) * 6f, dur);
	}

	private void ShowRelationshipNotification(string charName, string relType, int amount, int oldVal, int newVal)
	{
		bool isPositive = amount > 0; Color themeColor = isPositive ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f); Color baseColor = new Color(0.3f, 0.6f, 0.9f);
		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12, BorderWidthLeft = 4, BorderWidthRight = 4, BorderWidthTop = 4, BorderWidthBottom = 4, BorderColor = new Color(0.2f, 0.2f, 0.25f), ShadowSize = 12, ShadowColor = new Color(0, 0, 0, 0.6f), ShadowOffset = new Vector2(0, 6) });
		MarginContainer margin = new MarginContainer(); margin.AddThemeConstantOverride("margin_left", 25); margin.AddThemeConstantOverride("margin_right", 25); margin.AddThemeConstantOverride("margin_top", 15); margin.AddThemeConstantOverride("margin_bottom", 15); panel.AddChild(margin);
		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; vbox.AddThemeConstantOverride("separation", 12); margin.AddChild(vbox);
		Label title = new Label { Text = $"{charName} - {relType}", HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 22); if (MasterTheme?.DefaultFont != null) title.AddThemeFontOverride("font", MasterTheme.DefaultFont); vbox.AddChild(title);
		ProgressBar bar = new ProgressBar { CustomMinimumSize = new Vector2(280, 25), MaxValue = 100, Value = oldVal, ShowPercentage = false };
		StyleBoxFlat barFill = new StyleBoxFlat { BgColor = baseColor, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 };
		bar.AddThemeStyleboxOverride("fill", barFill); bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 }); vbox.AddChild(bar);
		string pfx = isPositive ? "YAY!" : "BOO!"; string amtStr = isPositive ? $"+{amount}" : $"{amount}";
		Label effectLabel = new Label { Text = $"{pfx} {amtStr}", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0) };
		effectLabel.AddThemeFontSizeOverride("font_size", 26); effectLabel.AddThemeColorOverride("font_color", themeColor); effectLabel.AddThemeConstantOverride("outline_size", 6); effectLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		if (MasterTheme?.DefaultFont != null) effectLabel.AddThemeFontOverride("font", MasterTheme.DefaultFont); vbox.AddChild(effectLabel);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(panel); else AddChild(panel);
		CallDeferred(MethodName.RunRelationshipTweenSequence, panel, bar, barFill, effectLabel, themeColor, newVal);
	}

	private void RunRelationshipTweenSequence(PanelContainer panel, ProgressBar bar, StyleBoxFlat barFill, Label effectLabel, Color themeColor, int newVal)
	{
		Vector2 ss = GetViewport().GetVisibleRect().Size; panel.Position = new Vector2(ss.X + 50f, 60f); panel.PivotOffset = panel.Size / 2f;
		Tween seq = CreateTween();
		seq.TweenProperty(panel, "position:x", ss.X - panel.Size.X - 40f, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		seq.TweenInterval(0.3f);
		seq.TweenProperty(bar, "value", (double)newVal, 0.6f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		seq.TweenCallback(Callable.From(() => {
			StyleBoxFlat ps = (StyleBoxFlat)panel.GetThemeStylebox("panel"); ps.BorderColor = themeColor; barFill.BgColor = themeColor;
			effectLabel.Modulate = new Color(1, 1, 1, 1); effectLabel.Scale = new Vector2(0.01f, 0.01f); effectLabel.PivotOffset = effectLabel.Size / 2f;
			Tween pop = CreateTween(); pop.TweenProperty(effectLabel, "scale", new Vector2(1.3f, 1.3f), 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			pop.TweenProperty(effectLabel, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
			Tween bump = CreateTween(); bump.TweenProperty(panel, "scale", new Vector2(1.08f, 1.08f), 0.1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			bump.TweenProperty(panel, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		}));
		seq.TweenInterval(1.8f);
		seq.TweenProperty(panel, "position:x", ss.X + 50f, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		seq.Finished += () => panel.QueueFree();
	}

	private void ShowNextMissionScreen()
	{
		_currentState = State.Cutscene; ShowActions(false); if (DimOverlay != null) DimOverlay.Visible = true;
		Control uiRoot = new Control(); uiRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(uiRoot); else AddChild(uiRoot);
		CenterContainer center = new CenterContainer { Theme = MasterTheme }; center.SetAnchorsPreset(Control.LayoutPreset.FullRect); uiRoot.AddChild(center);
		PanelContainer panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", BaseUIStyle); center.AddChild(panel);
		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; vbox.AddThemeConstantOverride("separation", 20); panel.AddChild(vbox);
		Label title = new Label { Text = "MISSION COMPLETE", HorizontalAlignment = HorizontalAlignment.Center }; title.AddThemeFontSizeOverride("font_size", 40); title.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.5f)); vbox.AddChild(title);
		Label sub = new Label { Text = "The party rests and recovers some HP.", HorizontalAlignment = HorizontalAlignment.Center }; sub.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f)); vbox.AddChild(sub);
		Button nextBtn = new Button { Text = "Start Next Mission", CustomMinimumSize = new Vector2(250, 60) }; AddButtonJuice(nextBtn); vbox.AddChild(nextBtn);
		panel.PivotOffset = new Vector2(200, 100); panel.Scale = Vector2.Zero;
		CreateTween().TweenProperty(panel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		nextBtn.Pressed += () => { 
			Tween ot = CreateTween(); ot.TweenProperty(panel, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			ot.Finished += () => { 
				uiRoot.QueueFree(); 
				if (DimOverlay != null) DimOverlay.Visible = false; 
				LoadMission(_currentMissionIndex + 1); 
			}; 
		};
	}

	private void ShowCardRankUpNotification(PersistentUnit unit, CardRank newRank)
	{
		string ss = CardImageHelper.GetSuitSymbol(unit.CardSuit);
		Color sc = CardImageHelper.GetSuitColor(unit.CardSuit);
		Label label = new Label { Text = $"BOND RANK UP!\n{unit.Profile.Name}: {ss} {newRank.DisplayName()}", HorizontalAlignment = HorizontalAlignment.Center };
		label.AddThemeFontSizeOverride("font_size", 48); label.AddThemeColorOverride("font_color", sc);
		label.AddThemeColorOverride("font_outline_color", Colors.Black); label.AddThemeConstantOverride("outline_size", 14);
		if (_fantasyFont != null) label.AddThemeFontOverride("font", _fantasyFont);
		label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(label); else AddChild(label);
		label.PivotOffset = GetViewport().GetVisibleRect().Size / 2; label.Scale = Vector2.Zero; label.Modulate = new Color(1, 1, 1, 0);
		Tween t = CreateTween();
		t.Parallel().TweenProperty(label, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.Parallel().TweenProperty(label, "modulate:a", 1f, 0.3f);
		t.Chain().TweenInterval(1.5f); t.Chain().TweenProperty(label, "modulate:a", 0f, 0.4f);
		t.Finished += () => label.QueueFree();
	}
}
