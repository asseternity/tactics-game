// ComboVisualizer.cs — Green chains, big floating cards, polished bottom display
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class ComboVisualizer : Node3D
{
	private List<Node3D> _activeVisuals = new();
	private List<Node3D> _cardSprites3D = new();
	private List<Tween> _activeTweens = new();
	private Label3D _handLabel;
	private CanvasLayer _cardPreviewLayer;
	private List<Control> _cardWidgets = new();
	private Font _font;

	public override void _Ready()
	{
		_font = GD.Load<Font>("res://fonts/yoster.ttf");
		_handLabel = new Label3D
		{
			FontSize = 80, OutlineSize = 14, OutlineModulate = Colors.Black,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true, RenderPriority = 50, Visible = false
		};
		if (_font != null) _handLabel.Font = _font;
		AddChild(_handLabel);
		_cardPreviewLayer = new CanvasLayer { Layer = 90 };
		AddChild(_cardPreviewLayer);
	}

	public void ShowComboPreview(ComboResult combo, Camera3D cam, Unit attacker = null)
	{
		ClearVisuals();
		if (combo == null) return;
		if (combo.AllFlankingAllies.Count == 0 && !combo.HasCombo) return;

		Color chainColor = new Color(0.2f, 1.0f, 0.4f, 0.6f); // Green translucent
		Color comboColor = combo.HasCombo ? combo.HandResult.ComboColor : chainColor;

		// 1. Green chains between attacker↔allies and allies↔enemies
		if (attacker != null && GodotObject.IsInstanceValid(attacker))
		{
			foreach (var ally in combo.AllFlankingAllies)
			{
				if (!GodotObject.IsInstanceValid(ally)) continue;
				DrawChainLink(attacker.GlobalPosition + Vector3.Up * 0.8f, ally.GlobalPosition + Vector3.Up * 0.8f, chainColor);
			}
		}
		foreach (var kvp in combo.AttackMap)
		{
			Unit enemy = kvp.Key;
			foreach (var (atk, _) in kvp.Value)
			{
				if (!GodotObject.IsInstanceValid(atk) || !GodotObject.IsInstanceValid(enemy)) continue;
				if (atk == attacker) continue; // Attacker→enemy line drawn separately
				DrawChainLink(atk.GlobalPosition + Vector3.Up * 0.8f, enemy.GlobalPosition + Vector3.Up * 0.8f, chainColor);
			}
		}

		// Attacker → primary enemy: draw a golden attack line
		Unit primaryEnemy = combo.AttackMap.Keys.FirstOrDefault();
		if (attacker != null && primaryEnemy != null && GodotObject.IsInstanceValid(attacker) && GodotObject.IsInstanceValid(primaryEnemy))
			DrawChainLink(attacker.GlobalPosition + Vector3.Up * 0.8f, primaryEnemy.GlobalPosition + Vector3.Up * 0.8f,
				new Color(1f, 0.85f, 0.3f, 0.7f));

		// 2. Hand label over primary enemy
		if (primaryEnemy != null && GodotObject.IsInstanceValid(primaryEnemy) && combo.HasCombo)
		{
			_handLabel.Text = $"{combo.HandResult.DisplayName}\n×{combo.HandResult.DamageMultiplier:F2}";
			_handLabel.Modulate = comboColor;
			_handLabel.GlobalPosition = primaryEnemy.GlobalPosition + Vector3.Up * 3.8f;
			_handLabel.Visible = true;
			_handLabel.Scale = Vector3.Zero;
			CreateTween().TweenProperty(_handLabel, "scale", Vector3.One, 0.3f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}

		// 3. Big floating card images over flanking allies
		ShowCardSprites3D(combo, comboColor);

		// 4. Bottom card display
		ShowCardWidgets(combo, cam);
	}

	public void ClearVisuals()
	{
		foreach (var tw in _activeTweens) if (tw != null && tw.IsValid()) tw.Kill();
		_activeTweens.Clear();
		foreach (var n in _activeVisuals) if (GodotObject.IsInstanceValid(n)) n.QueueFree();
		_activeVisuals.Clear();
		_handLabel.Visible = false;
		foreach (var w in _cardWidgets) if (GodotObject.IsInstanceValid(w)) w.QueueFree();
		_cardWidgets.Clear();
		foreach (var s in _cardSprites3D) if (GodotObject.IsInstanceValid(s)) s.QueueFree();
		_cardSprites3D.Clear();
	}

	// --------------------------------------------------------
	// GREEN CHAIN LINKS
	// --------------------------------------------------------

	private void DrawChainLink(Vector3 from, Vector3 to, Color color)
	{
		float dist = (to - from).Length();
		int segCount = Mathf.Max(3, (int)(dist * 1.5f));

		Node3D chainParent = new Node3D();
		AddChild(chainParent);
		_activeVisuals.Add(chainParent);

		var segMaterials = new List<StandardMaterial3D>();

		for (int i = 0; i <= segCount; i++)
		{
			float t = i / (float)segCount;
			float arc = Mathf.Sin(t * Mathf.Pi) * 0.3f;
			Vector3 pos = from.Lerp(to, t) + Vector3.Up * arc;

			// Chain link = small stretched box
			var mat = new StandardMaterial3D
			{
				AlbedoColor = color,
				EmissionEnabled = true,
				Emission = new Color(color.R, color.G, color.B),
				EmissionEnergyMultiplier = 1.5f,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha
			};
			segMaterials.Add(mat);

			// Alternate between horizontal and vertical "links"
			bool horizontal = i % 2 == 0;
			BoxMesh linkMesh = new BoxMesh
			{
				Size = horizontal
					? new Vector3(dist / segCount * 0.8f, 0.06f, 0.15f)
					: new Vector3(0.06f, 0.15f, dist / segCount * 0.8f)
			};

			MeshInstance3D link = new MeshInstance3D
			{
				Mesh = linkMesh,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				MaterialOverride = mat
			};

			chainParent.AddChild(link);
			link.GlobalPosition = pos;

			// Orient link along the chain direction
			Vector3 dir = (to - from).Normalized();
			link.LookAt(pos + dir, Vector3.Up);

			// Pop in
			link.Scale = Vector3.Zero;
			CreateTween().TweenProperty(link, "scale", Vector3.One, 0.12f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
				.SetDelay(t * 0.15f);
		}

		// Pulse glow — tracked
		if (segMaterials.Count > 0)
		{
			Tween pulse = CreateTween().SetLoops();
			pulse.TweenProperty(segMaterials[0], "emission_energy_multiplier", 0.6f, 0.5f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			for (int i = 1; i < segMaterials.Count; i++)
				pulse.Parallel().TweenProperty(segMaterials[i], "emission_energy_multiplier", 0.6f, 0.5f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			pulse.TweenProperty(segMaterials[0], "emission_energy_multiplier", 2.5f, 0.5f)
				.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			for (int i = 1; i < segMaterials.Count; i++)
				pulse.Parallel().TweenProperty(segMaterials[i], "emission_energy_multiplier", 2.5f, 0.5f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			_activeTweens.Add(pulse);
		}
	}

	// --------------------------------------------------------
	// BIG FLOATING CARD SPRITES OVER FLANKING ALLIES
	// --------------------------------------------------------

	private void ShowCardSprites3D(ComboResult combo, Color comboColor)
	{
		if (combo.HandResult?.ContributingCards == null) return;

		var cardsByOwner = combo.HandResult.ContributingCards
			.GroupBy(c => c.Owner)
			.Where(g => g.Key != null && GodotObject.IsInstanceValid(g.Key));

		int index = 0;
		foreach (var group in cardsByOwner)
		{
			Unit owner = group.Key;
			CardEntry card = group.First();

			string imgPath = CardImageHelper.GetCardImagePath(card.Suit, card.Rank);
			Texture2D tex = !string.IsNullOrEmpty(imgPath) ? GD.Load<Texture2D>(imgPath) : null;

			if (tex != null)
			{
				// BIG card — roughly half the sprite height (sprite is ~1.8 units tall, so card is ~0.9)
				Sprite3D cardSprite = new Sprite3D
				{
					Texture = tex,
					Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
					NoDepthTest = true, RenderPriority = 45,
					PixelSize = 0.013f,
					Shaded = false,
					AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
					CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
				};
				AddChild(cardSprite);
				cardSprite.GlobalPosition = owner.GlobalPosition + Vector3.Up * 3.2f;
				_cardSprites3D.Add(cardSprite);

				// Bouncy entrance
				cardSprite.Scale = Vector3.Zero;
				Tween entrance = CreateTween();
				entrance.TweenProperty(cardSprite, "scale", Vector3.One * 1.15f, 0.2f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
					.SetDelay(index * 0.12f);
				entrance.TweenProperty(cardSprite, "scale", Vector3.One, 0.15f)
					.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);

				// Float bob — tracked
				float baseY = owner.GlobalPosition.Y + 3.2f;
				Tween bob = CreateTween().SetLoops();
				bob.TweenProperty(cardSprite, "global_position:y", baseY + 0.2f, 0.8f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				bob.TweenProperty(cardSprite, "global_position:y", baseY - 0.08f, 0.8f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				_activeTweens.Add(bob);

				// Gentle tilt — tracked
				Tween tilt = CreateTween().SetLoops();
				tilt.TweenProperty(cardSprite, "rotation_degrees:z", 6f, 1.4f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				tilt.TweenProperty(cardSprite, "rotation_degrees:z", -6f, 1.4f)
					.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				_activeTweens.Add(tilt);
			}
			else
			{
				Label3D fb = new Label3D
				{
					Text = $"{CardImageHelper.GetSuitSymbol(card.Suit)} {card.Rank.DisplayName()}",
					FontSize = 80, OutlineSize = 14, OutlineModulate = Colors.Black,
					Modulate = CardImageHelper.GetSuitColor(card.Suit),
					Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
					NoDepthTest = true, RenderPriority = 45
				};
				if (_font != null) fb.Font = _font;
				AddChild(fb);
				fb.GlobalPosition = owner.GlobalPosition + Vector3.Up * 3.2f;
				_cardSprites3D.Add(fb);
				fb.Scale = Vector3.Zero;
				CreateTween().TweenProperty(fb, "scale", Vector3.One, 0.25f)
					.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
					.SetDelay(index * 0.1f);
			}
			index++;
		}
	}

	// --------------------------------------------------------
	// BOTTOM CARD DISPLAY — dark panels, no ugly overlay
	// --------------------------------------------------------

	private void ShowCardWidgets(ComboResult combo, Camera3D cam)
	{
		if (cam == null) return;
		var cards = combo.HandResult.ContributingCards;
		if (cards == null || cards.Count == 0) return;

		float cardW = 90f, cardH = 130f, spacing = 14f;
		float totalW = cards.Count * (cardW + spacing) - spacing;
		Vector2 screen = GetViewport().GetVisibleRect().Size;
		float startX = (screen.X - totalW) / 2f;
		float landY = screen.Y - cardH - 50f;

		for (int i = 0; i < cards.Count; i++)
		{
			var entry = cards[i];

			PanelContainer panel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(cardW, cardH + 24f),
				Position = new Vector2(startX + i * (cardW + spacing), screen.Y + 30f)
			};
			panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
			{
				BgColor = new Color(0.04f, 0.04f, 0.06f, 0.95f),
				CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
				CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
				BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
				BorderColor = combo.HasCombo ? new Color(combo.HandResult.ComboColor, 0.6f) : new Color(0.3f, 0.7f, 0.4f, 0.6f),
				ShadowSize = 6, ShadowColor = new Color(0, 0, 0, 0.7f),
				ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4
			});

			VBoxContainer vbox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
			vbox.AddThemeConstantOverride("separation", 2);
			panel.AddChild(vbox);

			string imgPath = CardImageHelper.GetCardImagePath(entry.Suit, entry.Rank);
			Texture2D tex = !string.IsNullOrEmpty(imgPath) ? GD.Load<Texture2D>(imgPath) : null;
			if (tex != null)
			{
				vbox.AddChild(new TextureRect
				{
					Texture = tex,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					CustomMinimumSize = new Vector2(cardW - 10, cardH - 10),
					SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
				});
			}

			Label name = new Label { Text = entry.Owner?.Data?.Profile.Name ?? "?", HorizontalAlignment = HorizontalAlignment.Center };
			name.AddThemeFontSizeOverride("font_size", 12);
			name.AddThemeColorOverride("font_color", new Color(0.85f, 0.8f, 0.65f));
			name.AddThemeColorOverride("font_outline_color", Colors.Black);
			name.AddThemeConstantOverride("outline_size", 3);
			if (_font != null) name.AddThemeFontOverride("font", _font);
			vbox.AddChild(name);

			_cardPreviewLayer.AddChild(panel);
			_cardWidgets.Add(panel);

			// Bounce up from below
			panel.PivotOffset = new Vector2(cardW / 2f, (cardH + 24f) / 2f);
			panel.Scale = new Vector2(0.6f, 0.6f);
			Tween slide = CreateTween();
			slide.TweenProperty(panel, "position:y", landY, 0.4f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(i * 0.08f);
			slide.Parallel().TweenProperty(panel, "scale", Vector2.One, 0.35f)
				.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(i * 0.08f);
		}
	}
}
