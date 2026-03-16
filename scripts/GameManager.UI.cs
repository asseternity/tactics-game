// GameManager.UI.cs — Stats panel, bond notifications, card celebrations
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class GameManager
{
	// === STATS PANEL ===
	private PanelContainer _statsPanel;
	private TextureRect _statsPortrait;
	private Label _statsName;
	private ProgressBar _statsHpBar;
	private Label _statsHpLabel;
	private PanelContainer _statsMoveOrb;
	private PanelContainer _statsAttackOrb;
	private Label _statsMoveLabel;
	private Label _statsAttackLabel;
	private PanelContainer _activeTooltip;

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

		BuildStatsPanel();
	}

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

		HBoxContainer mainRow = new HBoxContainer();
		mainRow.AddThemeConstantOverride("separation", 12);
		_statsPanel.AddChild(mainRow);

		_statsPortrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(70, 80), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		mainRow.AddChild(_statsPortrait);

		VBoxContainer infoCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		infoCol.AddThemeConstantOverride("separation", 6);
		mainRow.AddChild(infoCol);

		_statsName = new Label { Text = "—", HorizontalAlignment = HorizontalAlignment.Left };
		_statsName.AddThemeFontSizeOverride("font_size", 22);
		_statsName.AddThemeColorOverride("font_outline_color", Colors.Black);
		_statsName.AddThemeConstantOverride("outline_size", 4);
		if (_fantasyFont != null) _statsName.AddThemeFontOverride("font", _fantasyFont);
		infoCol.AddChild(_statsName);

		MarginContainer hpMargin = new MarginContainer { CustomMinimumSize = new Vector2(0, 28) };
		_statsHpBar = new ProgressBar { CustomMinimumSize = new Vector2(0, 28), ShowPercentage = false, MaxValue = 100, Value = 100 };
		_statsHpBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.15f, 0.05f, 0.05f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2, BorderColor = new Color(0.3f, 0.1f, 0.1f) });
		_statsHpBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.2f, 0.85f, 0.3f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
		_statsHpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_statsHpLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_statsHpLabel.AddThemeFontSizeOverride("font_size", 15);
		_statsHpLabel.AddThemeConstantOverride("outline_size", 4);
		_statsHpLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		if (_fantasyFont != null) _statsHpLabel.AddThemeFontOverride("font", _fantasyFont);
		_statsHpBar.AddChild(_statsHpLabel);
		hpMargin.AddChild(_statsHpBar);
		infoCol.AddChild(hpMargin);

		HBoxContainer orbRow = new HBoxContainer();
		orbRow.AddThemeConstantOverride("separation", 16);
		infoCol.AddChild(orbRow);

		VBoxContainer moveCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		moveCol.AddThemeConstantOverride("separation", 2);
		_statsMoveOrb = CreateOrbIndicator(new Color(0.2f, 0.8f, 1.0f));
		_statsMoveLabel = new Label { Text = "MOVE", HorizontalAlignment = HorizontalAlignment.Center };
		_statsMoveLabel.AddThemeFontSizeOverride("font_size", 11); _statsMoveLabel.AddThemeColorOverride("font_outline_color", Colors.Black); _statsMoveLabel.AddThemeConstantOverride("outline_size", 3);
		if (_fantasyFont != null) _statsMoveLabel.AddThemeFontOverride("font", _fantasyFont);
		moveCol.AddChild(_statsMoveOrb); moveCol.AddChild(_statsMoveLabel); orbRow.AddChild(moveCol);

		VBoxContainer atkCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		atkCol.AddThemeConstantOverride("separation", 2);
		_statsAttackOrb = CreateOrbIndicator(new Color(1.0f, 0.5f, 0.2f));
		_statsAttackLabel = new Label { Text = "ATTACK", HorizontalAlignment = HorizontalAlignment.Center };
		_statsAttackLabel.AddThemeFontSizeOverride("font_size", 11); _statsAttackLabel.AddThemeColorOverride("font_outline_color", Colors.Black); _statsAttackLabel.AddThemeConstantOverride("outline_size", 3);
		if (_fantasyFont != null) _statsAttackLabel.AddThemeFontOverride("font", _fantasyFont);
		atkCol.AddChild(_statsAttackOrb); atkCol.AddChild(_statsAttackLabel); orbRow.AddChild(atkCol);

		_statsPanel.Visible = false;
		if (StatsLabel != null && StatsLabel.GetParent() != null)
		{
			StatsLabel.GetParent().AddChild(_statsPanel);
			_statsPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
			_statsPanel.Position = new Vector2(20, -180);
		}
		else AddChild(_statsPanel);
	}

	private PanelContainer CreateOrbIndicator(Color litColor)
	{
		PanelContainer orb = new PanelContainer { CustomMinimumSize = new Vector2(32, 32) };
		orb.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = litColor, CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2, BorderColor = new Color(1f, 1f, 1f, 0.3f), ShadowSize = 8, ShadowColor = new Color(litColor.R, litColor.G, litColor.B, 0.6f), ShadowOffset = new Vector2(0, 2) });
		return orb;
	}

	private void SetOrbLit(PanelContainer orb, Color litColor, bool isLit)
	{
		StyleBoxFlat style = (StyleBoxFlat)orb.GetThemeStylebox("panel");
		if (isLit) { style.BgColor = litColor; style.ShadowSize = 8; style.ShadowColor = new Color(litColor.R, litColor.G, litColor.B, 0.6f); style.BorderColor = new Color(1f, 1f, 1f, 0.3f); }
		else { style.BgColor = new Color(0.12f, 0.12f, 0.15f); style.ShadowSize = 0; style.BorderColor = new Color(0.2f, 0.2f, 0.25f); }
	}

	private void UpdateStatsUI()
	{
		if (_statsPanel == null) return;
		if (_selectedUnit != null && _selectedUnit.Data != null && GodotObject.IsInstanceValid(_selectedUnit))
		{
			_statsPanel.Visible = true;
			var d = _selectedUnit.Data;
			_statsPortrait.Texture = GD.Load<Texture2D>(d.Profile.SpritePath);
			_statsName.Text = $"{d.Profile.Name}  Lv.{d.Level}";
			_statsName.AddThemeColorOverride("font_color", _selectedUnit.IsFriendly ? new Color(1f, 0.95f, 0.8f) : new Color(1f, 0.5f, 0.5f));
			int maxHp = d.GetTotalMaxHP(); _statsHpBar.MaxValue = maxHp;
			CreateTween().TweenProperty(_statsHpBar, "value", (double)d.CurrentHP, 0.2f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			_statsHpLabel.Text = $"HP  {d.CurrentHP} / {maxHp}";
			float ratio = maxHp > 0 ? (float)d.CurrentHP / maxHp : 0;
			Color hpColor = ratio > 0.6f ? new Color(0.2f, 0.85f, 0.3f) : ratio > 0.3f ? new Color(0.9f, 0.75f, 0.15f) : new Color(0.9f, 0.2f, 0.2f);
			((StyleBoxFlat)_statsHpBar.GetThemeStylebox("fill")).BgColor = hpColor;
			if (_selectedUnit.IsFriendly)
			{
				SetOrbLit(_statsMoveOrb, new Color(0.2f, 0.8f, 1.0f), !_selectedUnit.HasMoved);
				SetOrbLit(_statsAttackOrb, new Color(1.0f, 0.5f, 0.2f), !_selectedUnit.HasAttacked);
				_statsMoveLabel.AddThemeColorOverride("font_color", _selectedUnit.HasMoved ? new Color(0.3f, 0.3f, 0.35f) : new Color(0.5f, 0.9f, 1f));
				_statsAttackLabel.AddThemeColorOverride("font_color", _selectedUnit.HasAttacked ? new Color(0.3f, 0.3f, 0.35f) : new Color(1f, 0.7f, 0.4f));
				_statsMoveOrb.Visible = true; _statsAttackOrb.Visible = true; _statsMoveLabel.Visible = true; _statsAttackLabel.Visible = true;
			}
			else { _statsMoveOrb.Visible = false; _statsAttackOrb.Visible = false; _statsMoveLabel.Visible = false; _statsAttackLabel.Visible = false; }
			_statsPanel.PivotOffset = _statsPanel.Size / 2;
			Tween pop = CreateTween();
			pop.TweenProperty(_statsPanel, "scale", new Vector2(1.04f, 1.04f), 0.08f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			pop.TweenProperty(_statsPanel, "scale", Vector2.One, 0.12f).SetTrans(Tween.TransitionType.Sine);
		}
		else _statsPanel.Visible = false;
	}

	private void ShowUnitInfo(Unit u)
	{
		if (u == null || u.Data == null) return;
		var prev = _selectedUnit; _selectedUnit = u; UpdateStatsUI(); _selectedUnit = prev;
	}

	private void ShowActions(bool show)
	{
		if (show && _selectedUnit != null) { AttackButton.Text = "Attack"; AttackButton.Disabled = _selectedUnit.HasAttacked; }
		if (_uiTween != null && _uiTween.IsValid()) _uiTween.Kill();
		_uiTween = CreateTween(); ActionMenu.PivotOffset = ActionMenu.Size / 2;
		if (show) { if (!ActionMenu.Visible) ActionMenu.Scale = new Vector2(0.01f, 0.01f); ActionMenu.Visible = true; _uiTween.TweenProperty(ActionMenu, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); }
		else { _uiTween.TweenProperty(ActionMenu, "scale", new Vector2(0.01f, 0.01f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); _uiTween.Finished += () => ActionMenu.Visible = false; }
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

	private void EnterAttackMode() { _currentState = State.SelectingAttackTarget; AttackButton.Text = "Cancel"; if (AttackCursorIcon != null) Input.SetCustomMouseCursor(AttackCursorIcon, Input.CursorShape.Arrow, new Vector2(16, 16)); }
	private void CancelAttackMode() { _currentState = State.PlayerTurn; AttackButton.Text = "Attack"; ShowActions(true); Input.SetCustomMouseCursor(null); }

	// ============================================================
	// TURN ANNOUNCER + FLOATING TEXT
	// ============================================================

	private async Task ShowTurnAnnouncer(string text, Color color)
	{
		Label announcer = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		announcer.AddThemeFontSizeOverride("font_size", 100); announcer.AddThemeColorOverride("font_color", color);
		announcer.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0)); announcer.AddThemeConstantOverride("outline_size", 20);
		if (_fantasyFont != null) announcer.AddThemeFontOverride("font", _fantasyFont);
		announcer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(announcer); else AddChild(announcer);
		announcer.PivotOffset = GetViewport().GetVisibleRect().Size / 2; announcer.Scale = Vector2.Zero; announcer.Modulate = new Color(1, 1, 1, 0);
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
		AddChild(label); label.GlobalPosition = targetPosition + new Vector3(0, 1.5f, 0);
		Tween tween = CreateTween();
		tween.TweenProperty(label, "global_position", label.GlobalPosition + new Vector3(0, 1.2f, 0), 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.8f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tween.Finished += () => label.QueueFree();
	}

	// ============================================================
	// BOND NOTIFICATION — Slide-in for bond gain/loss
	// ============================================================
	// NOTE: This is called "ShowBondNotification" to match AddBond() in GameManager.cs

	private void ShowBondNotification(string charName, int amount, int oldXP, int newXP, int rankChange)
	{
		bool positive = amount > 0;
		Color themeColor = positive ? new Color(0.3f, 0.85f, 0.4f) : new Color(1f, 0.35f, 0.35f);
	 
		PanelContainer panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.06f, 0.1f, 0.95f),
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
			BorderColor = themeColor,
			ShadowSize = 8, ShadowColor = new Color(themeColor.R, themeColor.G, themeColor.B, 0.3f), ShadowOffset = new Vector2(0, 3),
			ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8
		});
	 
		HBoxContainer hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(hbox);
	 
		PersistentUnit companion = _party.FirstOrDefault(u => u.Profile.Name == charName);
		if (companion != null)
		{
			Texture2D portrait = GD.Load<Texture2D>(companion.Profile.SpritePath);
			if (portrait != null)
				hbox.AddChild(new TextureRect { Texture = portrait, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(32, 32) });
		}
	 
		VBoxContainer infoCol = new VBoxContainer();
		infoCol.AddThemeConstantOverride("separation", 3);
		hbox.AddChild(infoCol);
	 
		string prefix = positive ? "+" : "";
		Label title = new Label { Text = $"{charName}  {prefix}{amount}", HorizontalAlignment = HorizontalAlignment.Left };
		title.AddThemeFontSizeOverride("font_size", 16);
		title.AddThemeColorOverride("font_color", themeColor);
		title.AddThemeConstantOverride("outline_size", 4);
		title.AddThemeColorOverride("font_outline_color", Colors.Black);
		if (_fantasyFont != null) title.AddThemeFontOverride("font", _fantasyFont);
		infoCol.AddChild(title);
	 
		ProgressBar bar = new ProgressBar { CustomMinimumSize = new Vector2(180, 14), MaxValue = PersistentUnit.BondXPMax, Value = oldXP, ShowPercentage = false };
		StyleBoxFlat barFill = new StyleBoxFlat { BgColor = themeColor, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
		bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
		bar.AddThemeStyleboxOverride("fill", barFill);
		infoCol.AddChild(bar);
	 
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(panel); else AddChild(panel);
	 
		_activeBondNotifications.RemoveAll(p => !GodotObject.IsInstanceValid(p));
	 
		Vector2 ss = GetViewport().GetVisibleRect().Size;
		float slotHeight = 62f;
		int slotIndex = _activeBondNotifications.Count;
		float targetY = 16f + slotIndex * slotHeight;
	 
		panel.Position = new Vector2(ss.X + 10f, targetY);
		panel.PivotOffset = new Vector2(120, 25);
		panel.Scale = new Vector2(0.5f, 0.5f);
	 
		_activeBondNotifications.Add(panel);
	 
		Tween seq = CreateTween();
		seq.TweenProperty(panel, "position:x", ss.X - 270f, 0.3f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		seq.Parallel().TweenProperty(panel, "scale", Vector2.One, 0.25f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		seq.TweenInterval(0.1f);
	 
		if (rankChange == 1)
		{
			seq.TweenProperty(bar, "value", (double)PersistentUnit.BondXPMax, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
			seq.TweenCallback(Callable.From(() => barFill.BgColor = new Color(1f, 0.85f, 0.2f)));
			seq.TweenInterval(0.2f);
			seq.TweenCallback(Callable.From(() => { bar.Value = 0; barFill.BgColor = themeColor; }));
			seq.TweenProperty(bar, "value", (double)newXP, 0.2f);
		}
		else if (rankChange == -1)
		{
			seq.TweenProperty(bar, "value", 0.0, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
			seq.TweenCallback(Callable.From(() => barFill.BgColor = new Color(1f, 0.2f, 0.2f)));
			seq.TweenInterval(0.2f);
			seq.TweenCallback(Callable.From(() => { bar.Value = 0; barFill.BgColor = themeColor; }));
			seq.TweenProperty(bar, "value", (double)newXP, 0.2f);
		}
		else
		{
			seq.TweenProperty(bar, "value", (double)newXP, 0.3f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		}
	 
		seq.TweenInterval(1.5f);
		seq.TweenCallback(Callable.From(() =>
		{
			_activeBondNotifications.Remove(panel);
			Tween outT = CreateTween();
			outT.TweenProperty(panel, "position:x", ss.X + 10f, 0.25f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			outT.Parallel().TweenProperty(panel, "scale", new Vector2(0.5f, 0.5f), 0.2f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			outT.Finished += () => { if (GodotObject.IsInstanceValid(panel)) panel.QueueFree(); };
		}));
	}

	// ============================================================
	// CARD RANK CELEBRATIONS — Up and Down
	// ============================================================

	private void ShowCardRankUpCelebration(PersistentUnit unit, CardRank oldRank, CardRank newRank)
	{
		ShowCardRankCelebration(unit, oldRank, newRank, true);
	}

	private void ShowCardRankDownCelebration(PersistentUnit unit, CardRank oldRank, CardRank newRank)
	{
		ShowCardRankCelebration(unit, oldRank, newRank, false);
	}

	private void ShowCardRankCelebration(PersistentUnit unit, CardRank oldRank, CardRank newRank, bool isUpgrade)
	{
		CanvasLayer celebLayer = new CanvasLayer { Layer = 95 };
		AddChild(celebLayer);

		ColorRect dim = new ColorRect();
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dim.Color = new Color(0, 0, 0, 0);
		dim.MouseFilter = Control.MouseFilterEnum.Ignore;
		celebLayer.AddChild(dim);

		Vector2 ss = GetViewport().GetVisibleRect().Size;
		Vector2 center = ss / 2f;

		Control oldCard = CreateCelebrationCard(unit, oldRank);
		oldCard.Position = center - new Vector2(80, 110);
		oldCard.PivotOffset = new Vector2(80, 110);
		oldCard.Scale = Vector2.Zero;
		celebLayer.AddChild(oldCard);

		Control newCard = CreateCelebrationCard(unit, newRank);
		newCard.Position = center - new Vector2(80, 110);
		newCard.PivotOffset = new Vector2(80, 110);
		newCard.Scale = new Vector2(0.01f, 1f);
		newCard.Visible = false;
		celebLayer.AddChild(newCard);

		Color titleColor = isUpgrade ? new Color(1f, 0.9f, 0.3f) : new Color(1f, 0.3f, 0.3f);
		string titleText = isUpgrade ? "BOND RANK UP!" : "BOND RANK DOWN...";

		Label titleLabel = new Label { Text = titleText, HorizontalAlignment = HorizontalAlignment.Center };
		titleLabel.AddThemeFontSizeOverride("font_size", 52);
		titleLabel.AddThemeColorOverride("font_color", titleColor);
		titleLabel.AddThemeConstantOverride("outline_size", 16);
		titleLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		if (_fantasyFont != null) titleLabel.AddThemeFontOverride("font", _fantasyFont);
		titleLabel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		titleLabel.Position = new Vector2(0, center.Y - 160f);
		titleLabel.Scale = Vector2.Zero;
		titleLabel.PivotOffset = new Vector2(ss.X / 2f, 28f);
		celebLayer.AddChild(titleLabel);

		Tween seq = CreateTween();
		seq.TweenProperty(dim, "color:a", 0.5f, 0.3f);
		seq.TweenProperty(oldCard, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		seq.Parallel().TweenProperty(titleLabel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(0.15f);
		seq.TweenInterval(0.7f);

		if (isUpgrade)
		{
			seq.TweenProperty(oldCard, "scale:x", 0.01f, 0.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
			seq.TweenCallback(Callable.From(() => { oldCard.Visible = false; newCard.Visible = true; }));
			seq.TweenProperty(newCard, "scale", new Vector2(1.15f, 1.15f), 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			seq.TweenProperty(newCard, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
			seq.TweenCallback(Callable.From(() => SpawnCardSparkles(celebLayer, center, true)));
		}
		else
		{
			for (int i = 0; i < 6; i++)
			{
				float offsetX = (i % 2 == 0 ? 8f : -8f);
				seq.TweenProperty(oldCard, "position:x", oldCard.Position.X + offsetX, 0.04f);
			}
			seq.TweenProperty(oldCard, "position:x", oldCard.Position.X, 0.04f);
			seq.TweenProperty(oldCard, "scale", new Vector2(0.8f, 0.3f), 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			seq.TweenCallback(Callable.From(() => { oldCard.Visible = false; newCard.Visible = true; newCard.Scale = Vector2.Zero; }));
			seq.TweenProperty(newCard, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
			seq.TweenCallback(Callable.From(() => SpawnCardSparkles(celebLayer, center, false)));
		}

		string resultText = newRank == CardRank.None
			? $"{unit.Profile.Name}: Bond Lost..."
			: $"{unit.Profile.Name}: {CardImageHelper.GetSuitSymbol(unit.CardSuit)} {newRank.DisplayName()}";
		seq.TweenCallback(Callable.From(() => {
			titleLabel.Text = resultText;
			titleLabel.AddThemeColorOverride("font_color", isUpgrade ? new Color(1f, 0.95f, 0.7f) : new Color(0.7f, 0.4f, 0.4f));
			titleLabel.Scale = new Vector2(0.8f, 0.8f);
		}));
		seq.TweenProperty(titleLabel, "scale", Vector2.One, 0.25f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

		seq.TweenInterval(1.3f);
		seq.TweenProperty(newCard, "scale", Vector2.Zero, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		seq.Parallel().TweenProperty(titleLabel, "scale", Vector2.Zero, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		seq.Parallel().TweenProperty(dim, "color:a", 0f, 0.3f);
		seq.Finished += () => { celebLayer.QueueFree(); PlayNextCelebration(); };
	}

	private Control CreateCelebrationCard(PersistentUnit unit, CardRank rank)
	{
		float cardW = 160f, cardH = 220f;
		PanelContainer card = new PanelContainer { CustomMinimumSize = new Vector2(cardW, cardH) };
		Color borderColor = rank == CardRank.None ? new Color(0.3f, 0.3f, 0.35f) : CardImageHelper.GetSuitColor(unit.CardSuit);
		card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.05f, 0.07f, 0.97f),
			CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
			BorderWidthLeft = 4, BorderWidthRight = 4, BorderWidthTop = 4, BorderWidthBottom = 4,
			BorderColor = borderColor,
			ShadowSize = 16, ShadowColor = new Color(borderColor.R, borderColor.G, borderColor.B, 0.5f),
			ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 8, ContentMarginBottom = 8
		});
		VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		vbox.AddThemeConstantOverride("separation", 4);
		card.AddChild(vbox);

		if (rank == CardRank.None)
		{
			Label qm = new Label { Text = "?", HorizontalAlignment = HorizontalAlignment.Center };
			qm.AddThemeFontSizeOverride("font_size", 80); qm.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.35f));
			if (_fantasyFont != null) qm.AddThemeFontOverride("font", _fantasyFont);
			vbox.AddChild(qm);
			Label noLabel = new Label { Text = "No Bond", HorizontalAlignment = HorizontalAlignment.Center };
			noLabel.AddThemeFontSizeOverride("font_size", 16); noLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.45f));
			if (_fantasyFont != null) noLabel.AddThemeFontOverride("font", _fantasyFont);
			vbox.AddChild(noLabel);
		}
		else
		{
			string imgPath = CardImageHelper.GetCardImagePath(unit.CardSuit, rank);
			Texture2D tex = !string.IsNullOrEmpty(imgPath) ? GD.Load<Texture2D>(imgPath) : null;
			if (tex != null) vbox.AddChild(new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(cardW - 20, cardH - 50), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });
			Label rankLabel = new Label { Text = $"{CardImageHelper.GetSuitSymbol(unit.CardSuit)} {rank.DisplayName()}", HorizontalAlignment = HorizontalAlignment.Center };
			rankLabel.AddThemeFontSizeOverride("font_size", 16); rankLabel.AddThemeColorOverride("font_color", borderColor);
			rankLabel.AddThemeConstantOverride("outline_size", 4); rankLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
			if (_fantasyFont != null) rankLabel.AddThemeFontOverride("font", _fantasyFont);
			vbox.AddChild(rankLabel);
		}
		return card;
	}

	private void SpawnCardSparkles(CanvasLayer layer, Vector2 center, bool isUpgrade)
	{
		var rng = new System.Random();
		Color[] colors = isUpgrade
			? new[] { new Color(1f, 0.9f, 0.3f), new Color(1f, 0.6f, 0.1f), new Color(1f, 1f, 0.7f), new Color(0.4f, 1f, 0.5f) }
			: new[] { new Color(0.5f, 0.5f, 0.6f), new Color(0.3f, 0.3f, 0.4f), new Color(0.6f, 0.4f, 0.4f) };
		int count = isUpgrade ? 20 : 10;
		for (int i = 0; i < count; i++)
		{
			Label spark = new Label { Text = isUpgrade ? "✦" : "✧" };
			spark.AddThemeFontSizeOverride("font_size", rng.Next(18, 45));
			spark.AddThemeColorOverride("font_color", colors[rng.Next(colors.Length)]);
			spark.AddThemeConstantOverride("outline_size", 4); spark.AddThemeColorOverride("font_outline_color", Colors.Black);
			if (_fantasyFont != null) spark.AddThemeFontOverride("font", _fantasyFont);
			layer.AddChild(spark);
			spark.PivotOffset = new Vector2(15, 15); spark.Position = center; spark.Scale = Vector2.Zero;
			float angle = (float)(rng.NextDouble() * Mathf.Pi * 2);
			float dist = 80f + (float)rng.NextDouble() * 120f;
			Vector2 target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
			float fallY = isUpgrade ? 0f : 80f;
			Tween t = layer.GetTree().CreateTween().SetParallel(true);
			t.TweenProperty(spark, "position", target + new Vector2(0, fallY), isUpgrade ? 0.5f : 0.7f).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.Out);
			t.TweenProperty(spark, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			t.TweenProperty(spark, "rotation", (float)(rng.NextDouble() - 0.5) * 4f, 0.5f);
			t.SetParallel(false);
			t.TweenProperty(spark, "modulate:a", 0f, 0.3f).SetDelay(0.2f);
			t.Finished += () => spark.QueueFree();
		}
	}

	// ============================================================
	// LEVEL UP / LOOT / PARTY
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
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f, 0.95f), CornerRadiusTopLeft = 15, CornerRadiusTopRight = 15, CornerRadiusBottomLeft = 15, CornerRadiusBottomRight = 15, BorderWidthBottom = 4, BorderWidthTop = 4, BorderWidthLeft = 4, BorderWidthRight = 4, BorderColor = new Color(1f, 0.8f, 0f, 1f), ContentMarginBottom = 30, ContentMarginTop = 30, ContentMarginLeft = 40, ContentMarginRight = 40 });
		center.AddChild(panel);
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
		foreach (PersistentUnit unit in _party) { Button btn = new Button { Text = unit.Profile.Name, CustomMinimumSize = new Vector2(0, 60) }; AddButtonJuice(btn); btn.Pressed += () => RefreshPartyDetailsAndInventory(unit); rosterBox.AddChild(btn); }
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

	// ============================================================
	// PARTY MEMBER DETAILS — Card + Bond bar
	// ============================================================

	private Control BuildPartyMemberDetails(PersistentUnit unit)
	{
		HBoxContainer layout = new HBoxContainer();
		layout.AddThemeConstantOverride("separation", 40);
		layout.AddChild(new TextureRect { Texture = GD.Load<Texture2D>(unit.Profile.SpritePath), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(240, 350), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });

		VBoxContainer rightCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
		rightCol.AddThemeConstantOverride("separation", 15);
		layout.AddChild(rightCol);

		rightCol.AddChild(new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Text = $"[left][font_size=32][b][wave amp=20 freq=2]{unit.Profile.Name}[/wave][/b][/font_size]\n[color=gold]Level {unit.Level}[/color][/left]" });
		rightCol.AddChild(new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, Text = $"[left]{(unit.IsPlayerCharacter ? "[color=#44ff44]Player Character[/color]" : "[color=#44ccff]Companion[/color]")}\n\n[color=#aaaaaa]Max HP:[/color] {unit.GetTotalMaxHP()}\n[color=#aaaaaa]Damage:[/color] {unit.GetTotalDamage()}\n[color=#aaaaaa]Movement:[/color] {unit.GetTotalMovement()}[/left]" });
		rightCol.AddChild(new HSeparator());

		if (!unit.IsPlayerCharacter)
		{
			Label bondTitle = new Label { Text = "Battle Bond" };
			bondTitle.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.5f));
			bondTitle.AddThemeFontSizeOverride("font_size", 22);
			if (_fantasyFont != null) bondTitle.AddThemeFontOverride("font", _fantasyFont);
			rightCol.AddChild(bondTitle);

			HBoxContainer bondRow = new HBoxContainer();
			bondRow.AddThemeConstantOverride("separation", 20);
			rightCol.AddChild(bondRow);

			PanelContainer cardPanel = new PanelContainer { CustomMinimumSize = new Vector2(80, 110) };
			Color cardBorder = unit.CardRank == CardRank.None ? new Color(0.3f, 0.3f, 0.35f) : CardImageHelper.GetSuitColor(unit.CardSuit);
			cardPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.04f, 0.04f, 0.06f, 0.95f), CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3, BorderColor = cardBorder, ShadowSize = 6, ShadowColor = new Color(cardBorder.R, cardBorder.G, cardBorder.B, 0.4f), ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4 });
			VBoxContainer cardInner = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			cardPanel.AddChild(cardInner);

			if (unit.CardRank == CardRank.None)
			{
				Label qm = new Label { Text = "?", HorizontalAlignment = HorizontalAlignment.Center };
				qm.AddThemeFontSizeOverride("font_size", 48); qm.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.35f));
				if (_fantasyFont != null) qm.AddThemeFontOverride("font", _fantasyFont);
				cardInner.AddChild(qm);
			}
			else
			{
				string imgPath = CardImageHelper.GetCardImagePath(unit.CardSuit, unit.CardRank);
				Texture2D cardTex = !string.IsNullOrEmpty(imgPath) ? GD.Load<Texture2D>(imgPath) : null;
				if (cardTex != null) cardInner.AddChild(new TextureRect { Texture = cardTex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(68, 95), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });
			}
			bondRow.AddChild(cardPanel);

			VBoxContainer bondInfo = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
			bondInfo.AddThemeConstantOverride("separation", 8);

			string rankText = unit.CardRank == CardRank.None ? "No card yet" : $"{CardImageHelper.GetSuitSymbol(unit.CardSuit)} {unit.CardRank.DisplayName()}";
			Label rankLabel = new Label { Text = rankText, HorizontalAlignment = HorizontalAlignment.Left };
			rankLabel.AddThemeFontSizeOverride("font_size", 18); rankLabel.AddThemeColorOverride("font_color", cardBorder);
			rankLabel.AddThemeConstantOverride("outline_size", 4); rankLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
			if (_fantasyFont != null) rankLabel.AddThemeFontOverride("font", _fantasyFont);
			bondInfo.AddChild(rankLabel);

			CardRank nextRank = unit.CardRank == CardRank.None ? CardRank.Two : unit.CardRank.NextRank();
			Label nextLabel = new Label { Text = $"Next: {nextRank.DisplayName()}", HorizontalAlignment = HorizontalAlignment.Left };
			nextLabel.AddThemeFontSizeOverride("font_size", 13); nextLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.6f));
			if (_fantasyFont != null) nextLabel.AddThemeFontOverride("font", _fantasyFont);
			bondInfo.AddChild(nextLabel);

			ProgressBar bondBar = new ProgressBar { CustomMinimumSize = new Vector2(0, 24), MaxValue = PersistentUnit.BondXPMax, Value = 0, ShowPercentage = false };
			bondBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.1f, 0.1f, 0.15f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
			bondBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.3f, 0.85f, 0.4f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6 });
			Label xpText = new Label { Text = $"{unit.BondXP} / {PersistentUnit.BondXPMax}", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
			xpText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			xpText.AddThemeFontSizeOverride("font_size", 13); xpText.AddThemeConstantOverride("outline_size", 4); xpText.AddThemeColorOverride("font_outline_color", Colors.Black);
			if (_fantasyFont != null) xpText.AddThemeFontOverride("font", _fantasyFont);
			bondBar.AddChild(xpText);
			bondInfo.AddChild(bondBar);
			bondBar.TreeEntered += () => { CreateTween().TweenProperty(bondBar, "value", (double)unit.BondXP, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out); };
			bondRow.AddChild(bondInfo);

			cardPanel.PivotOffset = cardPanel.CustomMinimumSize / 2f; cardPanel.Scale = Vector2.Zero;
			cardPanel.TreeEntered += () => { CreateTween().TweenProperty(cardPanel, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(0.1f); };
		}

		rightCol.AddChild(new HSeparator());
		Label equipTitle = new Label { Text = "Equipment (Click to Remove)" };
		equipTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		rightCol.AddChild(equipTitle);
		HBoxContainer equipBox = new HBoxContainer();
		equipBox.AddThemeConstantOverride("separation", 20);
		rightCol.AddChild(equipBox);
		equipBox.AddChild(CreateEquipmentSlot("Weapon", unit.EquippedWeapon, EquipSlot.Weapon));
		equipBox.AddChild(CreateEquipmentSlot("Armor", unit.EquippedArmor, EquipSlot.Armor));
		return layout;
	}

	private Control CreateEquipmentSlot(string slotName, Equipment equip, EquipSlot slotType)
	{
		VBoxContainer slotBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		Label title = new Label { Text = slotName, HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 14);
		title.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
		slotBox.AddChild(title);
	 
		Button slotBtn = new Button { CustomMinimumSize = new Vector2(80, 80), IconAlignment = HorizontalAlignment.Center, ExpandIcon = true };
		slotBtn.AddThemeStyleboxOverride("normal", ItemSlotStyle);
		slotBtn.AddThemeStyleboxOverride("hover", ItemSlotHoverStyle);
		slotBtn.AddThemeStyleboxOverride("pressed", ItemSlotStyle);
		slotBtn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
	 
		if (equip != null)
		{
			slotBtn.Icon = GD.Load<Texture2D>(equip.IconPath);
			slotBtn.MouseEntered += () => ShowItemTooltip(equip, slotBtn);
			slotBtn.MouseExited += () => HideItemTooltip();
			slotBtn.Pressed += () => {
				HideItemTooltip();
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
		PanelContainer invPanel = new PanelContainer { Theme = MasterTheme };
		VBoxContainer invBox = new VBoxContainer(); invPanel.AddChild(invBox);
		Label invTitle = new Label { Text = "Party Inventory (Click to Equip)" };
		invTitle.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		invBox.AddChild(invTitle); invBox.AddChild(new HSeparator());
	 
		GridContainer grid = new GridContainer { Columns = 10 };
		grid.AddThemeConstantOverride("h_separation", 10);
		grid.AddThemeConstantOverride("v_separation", 10);
		invBox.AddChild(grid);
	 
		for (int i = 0; i < 20; i++)
		{
			Button slotBtn = new Button { CustomMinimumSize = new Vector2(70, 70), IconAlignment = HorizontalAlignment.Center, ExpandIcon = true };
			slotBtn.AddThemeStyleboxOverride("normal", ItemSlotStyle);
			slotBtn.AddThemeStyleboxOverride("hover", ItemSlotHoverStyle);
			slotBtn.AddThemeStyleboxOverride("pressed", ItemSlotStyle);
			slotBtn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
	 
			if (i < Inventory.Count && Inventory[i] is Equipment equipItem)
			{
				slotBtn.Icon = GD.Load<Texture2D>(equipItem.IconPath);
				slotBtn.MouseEntered += () => ShowItemTooltip(equipItem, slotBtn);
				slotBtn.MouseExited += () => HideItemTooltip();
				slotBtn.Pressed += () => {
					HideItemTooltip();
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
		nextBtn.Pressed += () => { Tween ot = CreateTween(); ot.TweenProperty(panel, "scale", Vector2.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); ot.Finished += () => { uiRoot.QueueFree(); if (DimOverlay != null) DimOverlay.Visible = false; LoadMission(_currentMissionIndex + 1); }; };
	}
	
	private void ShowItemTooltip(Equipment item, Button sourceBtn)
	{
		HideItemTooltip();
		_activeTooltip = new PanelContainer();
		_activeTooltip.AddThemeStyleboxOverride("panel", new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.08f, 0.12f, 0.97f),
			CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
			BorderWidthLeft = 3, BorderWidthRight = 3, BorderWidthTop = 3, BorderWidthBottom = 3,
			BorderColor = item.Slot == EquipSlot.Weapon ? new Color(1f, 0.6f, 0.2f) : new Color(0.3f, 0.7f, 1f),
			ShadowSize = 12, ShadowColor = new Color(0, 0, 0, 0.7f), ShadowOffset = new Vector2(0, 4),
			ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12
		});
		if (MasterTheme != null) _activeTooltip.Theme = MasterTheme;
	 
		VBoxContainer vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		_activeTooltip.AddChild(vbox);
	 
		Label nameLabel = new Label { Text = item.Name };
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		nameLabel.AddThemeColorOverride("font_color", item.Slot == EquipSlot.Weapon ? new Color(1f, 0.75f, 0.3f) : new Color(0.5f, 0.85f, 1f));
		nameLabel.AddThemeConstantOverride("outline_size", 4);
		nameLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		if (_fantasyFont != null) nameLabel.AddThemeFontOverride("font", _fantasyFont);
		vbox.AddChild(nameLabel);
	 
		Label slotLabel = new Label { Text = item.Slot == EquipSlot.Weapon ? "Weapon" : "Armor" };
		slotLabel.AddThemeFontSizeOverride("font_size", 12);
		slotLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f));
		if (_fantasyFont != null) slotLabel.AddThemeFontOverride("font", _fantasyFont);
		vbox.AddChild(slotLabel);
	 
		void AddStat(string text, Color color) {
			Label s = new Label { Text = text }; s.AddThemeFontSizeOverride("font_size", 14);
			s.AddThemeColorOverride("font_color", color);
			if (_fantasyFont != null) s.AddThemeFontOverride("font", _fantasyFont);
			vbox.AddChild(s);
		}
		if (item.BonusDamage > 0) AddStat($"+{item.BonusDamage} Damage", new Color(1f, 0.4f, 0.3f));
		if (item.BonusMaxHP > 0) AddStat($"+{item.BonusMaxHP} Max HP", new Color(0.3f, 1f, 0.4f));
		if (item.BonusMovement > 0) AddStat($"+{item.BonusMovement} Movement", new Color(0.4f, 0.8f, 1f));
		if (item.JokerEffects != JokerEffect.None) AddStat($"Joker: {item.JokerEffects}", new Color(1f, 0.85f, 0.3f));
	 
		if (DimOverlay != null) DimOverlay.GetParent().AddChild(_activeTooltip); else AddChild(_activeTooltip);
		_activeTooltip.Position = new Vector2(sourceBtn.GlobalPosition.X - 200f, sourceBtn.GlobalPosition.Y - 20f);
		_activeTooltip.PivotOffset = new Vector2(100, 0);
		_activeTooltip.Scale = new Vector2(0.7f, 0.7f);
		_activeTooltip.Modulate = new Color(1, 1, 1, 0);
		Tween t = CreateTween().SetParallel(true);
		t.TweenProperty(_activeTooltip, "scale", Vector2.One, 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(_activeTooltip, "modulate:a", 1f, 0.1f);
	}
	 
	private void HideItemTooltip()
	{
		if (_activeTooltip != null && GodotObject.IsInstanceValid(_activeTooltip))
		{
			PanelContainer dying = _activeTooltip; _activeTooltip = null;
			Tween t = CreateTween();
			t.TweenProperty(dying, "scale", new Vector2(0.8f, 0.8f), 0.1f);
			t.Parallel().TweenProperty(dying, "modulate:a", 0f, 0.1f);
			t.Finished += () => dying.QueueFree();
		}
	}
}
