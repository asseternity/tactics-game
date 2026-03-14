// ComboVisualizer.cs
// Draws colored lines between combo units and shows the hand type label floating over enemies.
// Add as a child of GameManager in the scene. Attach to the same Node3D root.
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class ComboVisualizer : Node3D
{
	// One arc per (attacker, enemy, ally) triple
	private List<Node3D> _activeVisuals = new();
	private Label3D _handLabel;
	private CanvasLayer _cardPreviewLayer;
	private List<Control> _cardWidgets = new();

	public override void _Ready()
	{
		_handLabel = new Label3D
		{
			FontSize = 80,
			OutlineSize = 12,
			OutlineModulate = Colors.Black,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			RenderPriority = 50,
			Visible = false
		};
		AddChild(_handLabel);

		_cardPreviewLayer = new CanvasLayer { Layer = 90 };
		AddChild(_cardPreviewLayer);
	}

	/// <summary>
	/// Called from GameManager when the player hovers over an enemy they can attack.
	/// Pass null to clear all visuals.
	/// </summary>
	public void ShowComboPreview(ComboResult combo, Camera3D cam)
	{
		ClearVisuals();
		if (combo == null || !combo.HasCombo) return;

		Color c = combo.HandResult.ComboColor;

		// 1. Draw lines between each (attacker ↔ enemy ↔ ally) triple
		foreach (var kvp in combo.AttackMap)
		{
			Unit enemy = kvp.Key;
			foreach (var (attacker, _) in kvp.Value)
			{
				if (!GodotObject.IsInstanceValid(attacker) || !GodotObject.IsInstanceValid(enemy)) continue;
				DrawArcLine(attacker.GlobalPosition + Vector3.Up * 1.2f, enemy.GlobalPosition + Vector3.Up * 1.2f, c);
			}
		}

		// 2. Hand label floats above the primary (first) enemy
		Unit primaryEnemy = combo.AttackMap.Keys.FirstOrDefault();
		if (primaryEnemy != null && GodotObject.IsInstanceValid(primaryEnemy))
		{
			_handLabel.Text = $"{combo.HandResult.DisplayName}\n×{combo.HandResult.DamageMultiplier:F2} DMG";
			_handLabel.Modulate = c;
			_handLabel.GlobalPosition = primaryEnemy.GlobalPosition + Vector3.Up * 3.5f;
			_handLabel.Visible = true;

			// Bounce the label in
			_handLabel.Scale = Vector3.Zero;
			CreateTween().TweenProperty(_handLabel, "scale", Vector3.One, 0.25f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		// 3. Mini card widgets in screen-space
		ShowCardWidgets(combo, cam);
	}

	public void ClearVisuals()
	{
		foreach (var n in _activeVisuals) if (GodotObject.IsInstanceValid(n)) n.QueueFree();
		_activeVisuals.Clear();
		_handLabel.Visible = false;

		foreach (var w in _cardWidgets) if (GodotObject.IsInstanceValid(w)) w.QueueFree();
		_cardWidgets.Clear();
	}

	// --------------------------------------------------------
	// PRIVATE HELPERS
	// --------------------------------------------------------

	private void DrawArcLine(Vector3 from, Vector3 to, Color color)
	{
		// Use a series of MeshInstance3D "beads" along the line for a dotted glowing arc
		Vector3 dir = to - from;
		float dist = dir.Length();
		int beadCount = Mathf.Max(4, (int)(dist * 2));

		Node3D lineParent = new Node3D();
		AddChild(lineParent);
		_activeVisuals.Add(lineParent);

		for (int i = 0; i <= beadCount; i++)
		{
			float t = i / (float)beadCount;
			// Slight arc peak
			float arcHeight = Mathf.Sin(t * Mathf.Pi) * 0.4f;
			Vector3 pos = from.Lerp(to, t) + Vector3.Up * arcHeight;

			MeshInstance3D bead = new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = 0.07f, Height = 0.14f },
				GlobalPosition = pos,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
			};

			var mat = new StandardMaterial3D
			{
				AlbedoColor = color,
				EmissionEnabled = true,
				Emission = color,
				EmissionEnergyMultiplier = 2.0f,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha
			};
			// Fade the ends
			mat.AlbedoColor = new Color(color.R, color.G, color.B,
				0.3f + 0.7f * Mathf.Sin(t * Mathf.Pi));
			bead.MaterialOverride = mat;

			lineParent.AddChild(bead);

			// Stagger pop-in
			bead.Scale = Vector3.Zero;
			CreateTween().TweenProperty(bead, "scale", Vector3.One * 0.8f, 0.15f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
				.SetDelay(t * 0.1f);
		}

		// Pulse animation on the whole line
		Tween pulse = CreateTween().SetLoops();
		pulse.TweenProperty(lineParent, "modulate:a", 0.4f, 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		pulse.TweenProperty(lineParent, "modulate:a", 1.0f, 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void ShowCardWidgets(ComboResult combo, Camera3D cam)
	{
		if (cam == null) return;

		// Show small card faces at the bottom of the screen for each contributing unit
		var cards = combo.HandResult.ContributingCards;
		float cardW = 80f, cardH = 110f, spacing = 10f;
		float totalW = cards.Count * (cardW + spacing) - spacing;
		Vector2 screenSize = GetViewport().GetVisibleRect().Size;
		float startX = (screenSize.X - totalW) / 2f;
		float startY = screenSize.Y - cardH - 20f;

		for (int i = 0; i < cards.Count; i++)
		{
			var entry = cards[i];
			float x = startX + i * (cardW + spacing);

			PanelContainer card = new PanelContainer
			{
				Position = new Vector2(x, startY + 60f),
				CustomMinimumSize = new Vector2(cardW, cardH)
			};

			// Card background style - suit color
			Color suitColor = SuitColor(entry.Suit);
			var style = new StyleBoxFlat
			{
				BgColor = new Color(0.95f, 0.95f, 0.95f),
				CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
				CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
				BorderWidthBottom = 3, BorderWidthTop = 3, BorderWidthLeft = 3, BorderWidthRight = 3,
				BorderColor = combo.HandResult.ComboColor,
				ShadowSize = 6, ShadowColor = new Color(0, 0, 0, 0.5f)
			};
			card.AddThemeStyleboxOverride("panel", style);

			VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			card.AddChild(vbox);

			// Suit symbol + rank
			Label suitLabel = new Label
			{
				Text = SuitSymbol(entry.Suit),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			suitLabel.AddThemeFontSizeOverride("font_size", 26);
			suitLabel.AddThemeColorOverride("font_color", suitColor);

			Label rankLabel = new Label
			{
				Text = entry.Rank.DisplayName(),
				HorizontalAlignment = HorizontalAlignment.Center
			};
			rankLabel.AddThemeFontSizeOverride("font_size", 14);
			rankLabel.AddThemeColorOverride("font_color", new Color(0.1f, 0.1f, 0.1f));

			Label ownerLabel = new Label
			{
				Text = entry.Owner?.Data?.Profile.Name ?? "?",
				HorizontalAlignment = HorizontalAlignment.Center
			};
			ownerLabel.AddThemeFontSizeOverride("font_size", 11);
			ownerLabel.AddThemeColorOverride("font_color", new Color(0.3f, 0.3f, 0.5f));

			vbox.AddChild(suitLabel);
			vbox.AddChild(rankLabel);
			vbox.AddChild(ownerLabel);

			_cardPreviewLayer.AddChild(card);
			_cardWidgets.Add(card);

			// Slide up from bottom
			Tween t = CreateTween();
			t.TweenProperty(card, "position:y", startY, 0.3f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
				.SetDelay(i * 0.05f);
		}
	}

	private static Color SuitColor(CardSuit suit) => suit switch
	{
		CardSuit.Hearts   => new Color(0.9f, 0.1f, 0.1f),
		CardSuit.Diamonds => new Color(0.9f, 0.1f, 0.1f),
		CardSuit.Clubs    => new Color(0.1f, 0.1f, 0.1f),
		CardSuit.Spades   => new Color(0.1f, 0.1f, 0.1f),
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
}
