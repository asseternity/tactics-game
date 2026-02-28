using Godot;
using System.Collections.Generic;

/// <summary>
/// Juicy cartoon particle burst for dialogue emotions. 
/// Features one MASSIVE core emotion and smaller accent bursts.
/// </summary>
public partial class EmotionEffect : Node2D
{
	private static readonly Dictionary<string, string> EmotionSymbols = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
	{
		{ "fear", "!!" },
		{ "bravery", "★" },
		{ "sadness", "…" },
		{ "anger", "!!" },
		{ "confusion", "?" }
	};

	private static readonly Dictionary<string, Color> EmotionColors = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase)
	{
		{ "fear", new Color(0.6f, 0.6f, 1f, 1f) },
		{ "bravery", new Color(1f, 0.9f, 0.3f, 1f) },
		{ "sadness", new Color(0.5f, 0.6f, 0.9f, 1f) },
		{ "anger", new Color(1f, 0.25f, 0.2f, 1f) },
		{ "confusion", new Color(1f, 0.85f, 0.4f, 1f) }
	};

	private const float Duration = 2.0f;
	private const int AccentParticleCount = 3;

	public void Play(string emotionType, Vector2? portraitScreenPosition = null)
	{
		if (string.IsNullOrEmpty(emotionType)) { QueueFree(); return; }
		Vector2 viewSize = GetViewport().GetVisibleRect().Size;

		// 1. Create a temporary CanvasLayer that sits above EVERYTHING (Dialogic is usually Layer 1 or 2)
		CanvasLayer topLayer = new CanvasLayer { Layer = 100 };
		AddChild(topLayer);

		// 2. Create a container Node2D inside that CanvasLayer so our local positioning math still works
		Node2D container = new Node2D 
		{ 
			Position = portraitScreenPosition ?? new Vector2(viewSize.X * 0.5f, viewSize.Y * 0.4f) 
		};
		topLayer.AddChild(container);

		string key = emotionType.Trim().ToLowerInvariant();
		if (!EmotionSymbols.TryGetValue(key, out string symbol)) symbol = "!";
		if (!EmotionColors.TryGetValue(key, out Color color)) color = Colors.White;

		Font font = GD.Load<Font>("res://fonts/yoster.ttf");
		var rng = new System.Random();

		// 3. Spawn the BIG MAIN emotion and add it to the CONTAINER, not 'this'
		Label mainLabel = CreateCartoonyLabel(symbol, color, font, 110);
		container.AddChild(mainLabel);
		AnimateMainEmotion(mainLabel, color, key, rng);

		// 4. Spawn the smaller accent particles
		for (int i = 0; i < AccentParticleCount; i++)
		{
			Label accent = CreateCartoonyLabel(symbol, color, font, 30 + rng.Next(0, 15));
			container.AddChild(accent);
			AnimateAccentParticle(accent, color, rng);
		}

		GetTree().CreateTimer(Duration).Timeout += QueueFree;
	}

	/// <summary>Helper to create bold, outlined labels centered on their origin.</summary>
	private Label CreateCartoonyLabel(string text, Color color, Font font, int fontSize)
	{
		var label = new Label
		{
			Text = text,
			Modulate = color,
			Scale = Vector2.Zero,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			// Center the anchor so it grows from the middle
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
			// Approximate center pivot for juicy scaling/rotation
			PivotOffset = new Vector2(fontSize * 0.4f, fontSize * 0.5f) 
		};

		label.AddThemeFontSizeOverride("font_size", fontSize);
		if (font != null) label.AddThemeFontOverride("font", font);
		
		// Game Juice: Thick black outline
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", Mathf.Max(6, fontSize / 8));

		return label;
	}

	private void AnimateMainEmotion(Label label, Color color, string emotion, System.Random rng)
	{
		Tween t = CreateTween();
		
		switch (emotion)
		{
			case "anger":
				// HUGE pop, intense shake
				t.TweenProperty(label, "scale", new Vector2(1.8f, 1.8f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				for(int i = 0; i < 5; i++) {
					t.Chain().TweenProperty(label, "position", new Vector2((float)(rng.NextDouble()-0.5)*15f, (float)(rng.NextDouble()-0.5)*15f), 0.05f);
				}
				t.Chain().TweenProperty(label, "position", Vector2.Zero, 0.05f);
				break;

			case "confusion":
				// Extreme elastic wobble
				t.SetParallel(true);
				t.TweenProperty(label, "scale", new Vector2(1.4f, 1.4f), 0.6f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
				t.TweenProperty(label, "rotation", 0.4f, 0.5f).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
				t.SetParallel(false);
				break;

			case "bravery":
				// Heroic over-scale and shine (stretch up)
				t.TweenProperty(label, "scale", new Vector2(1.2f, 1.8f), 0.1f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
				t.Chain().TweenProperty(label, "scale", new Vector2(1.6f, 1.6f), 0.3f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
				break;

			case "sadness":
				// Slow droop and squash
				t.TweenProperty(label, "scale", new Vector2(0.8f, 1.5f), 0.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
				t.Chain().TweenProperty(label, "scale", new Vector2(1.4f, 0.9f), 0.4f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
				t.Parallel().TweenProperty(label, "position", new Vector2(0, 25f), 0.8f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
				break;

			case "fear":
				// Fast shivering shrink
				t.TweenProperty(label, "scale", new Vector2(1.5f, 1.5f), 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
				for(int i = 0; i < 6; i++) {
					t.Chain().TweenProperty(label, "position", new Vector2((float)(rng.NextDouble()-0.5)*10f, 0), 0.04f);
				}
				t.Chain().TweenProperty(label, "scale", new Vector2(1.1f, 1.3f), 0.2f);
				break;
		}

		// Universal follow-through: Hang for a bit, then fade out while drifting up
		Tween fade = CreateTween().SetParallel(true);
		fade.TweenProperty(label, "modulate", new Color(color.R, color.G, color.B, 0f), 0.4f).SetDelay(1.2f);
		fade.TweenProperty(label, "position", label.Position + new Vector2(0, -20f), 0.6f).SetDelay(1.2f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
	}

	private void AnimateAccentParticle(Label label, Color color, System.Random rng)
	{
		// Shoot outward from the center quickly, then hang and fade
		float angle = (float)(rng.NextDouble() * Mathf.Pi * 2);
		float distance = 60f + (float)rng.NextDouble() * 40f;
		Vector2 targetPos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
		
		Tween t = CreateTween().SetParallel(true);
		t.TweenProperty(label, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		t.TweenProperty(label, "position", targetPos, 0.3f).SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.Out);
		t.TweenProperty(label, "rotation", (float)(rng.NextDouble() - 0.5) * 2f, 0.4f);
		
		t.Chain().TweenInterval(0.6f + (float)rng.NextDouble() * 0.2f);
		t.Chain().TweenProperty(label, "modulate", new Color(color.R, color.G, color.B, 0f), 0.3f);
	}
}
