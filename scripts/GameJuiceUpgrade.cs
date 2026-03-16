// ============================================================================
// GameJuiceUpgrade.cs — Drop-in polish system for your Godot 4.5 tactics game
// 
// INSTRUCTIONS: Create this as a new file in your scripts/ folder.
// Then add ONE LINE to GameManager._Ready(), after SetupUnifiedUI():
//
//     GameJuiceUpgrade.Install(this);
//
// That's it. Everything below auto-wires into your existing systems.
// ============================================================================

using Godot;
using System;

public partial class GameJuiceUpgrade : Node
{
	private GameManager _gm;
	private Camera3D _cam;
	private Godot.Environment _env;
	private float _time;
	
	// Configurable parameters
	private const float CamBreathAmount = 0.06f;   // Subtle idle breathing
	private const float CamBreathSpeed = 0.8f;

	/// <summary>
	/// Call from GameManager._Ready() after SetupUnifiedUI():
	///     GameJuiceUpgrade.Install(this);
	/// </summary>
	public static GameJuiceUpgrade Install(GameManager gm)
	{
		var juice = new GameJuiceUpgrade();
		gm.AddChild(juice);
		juice._gm = gm;
		juice._cam = gm.Cam;
		juice.Name = "GameJuiceUpgrade";
		
		juice.SetupPostProcessing();
		juice.SetupVignetteShader();
		juice.SetupCameraBreathing();
		
		GD.Print("[JUICE] Game juice system installed!");
		return juice;
	}

	// ================================================================
	// 1. POST-PROCESSING — Enhance the built-in Environment
	// ================================================================
	// Your ApplyEnvironment already sets up glow and SSAO, but the 
	// values are conservative. This punches them up significantly.

	private void SetupPostProcessing()
	{
		if (_gm.WorldEnv?.Environment == null) return;
		_env = _gm.WorldEnv.Environment;
		if (_cam != null) _cam.Environment = _env;

		// --- GLOW: Warm, soft bloom that makes the game feel alive ---
		_env.GlowEnabled = true;
		_env.GlowIntensity = 0.6f;        // Down from 0.8 — softer
		_env.GlowStrength = 1.1f;          // Up from 0.9 — wider spread
		_env.GlowBloom = 0.08f;            // Tiny bit of bloom on everything
		_env.GlowHdrThreshold = 0.8f;      // Slightly lower threshold
		_env.GlowHdrScale = 2.0f;          // Brighter glow on bright objects
		_env.GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Softlight;
		// Softlight blends more naturally than Additive — less washed out
		
		// Enable multiple glow levels for rich, layered bloom
		_env.SetGlowLevel(0, 1.0f);
		_env.SetGlowLevel(1, 1.0f);
		_env.SetGlowLevel(2, 1.0f);
		_env.SetGlowLevel(3, 1.0f);
		_env.SetGlowLevel(4, 0.0f);
		_env.SetGlowLevel(5, 0.0f);
		_env.SetGlowLevel(6, 0.0f);

		// --- SSAO: Deeper, richer ambient occlusion ---
		_env.SsaoEnabled = true;
		_env.SsaoRadius = 1.5f;            // Up from 1.0 — wider darkening
		_env.SsaoIntensity = 2.5f;         // Up from 1.5 — more pronounced
		_env.SsaoLightAffect = 0.15f;      // Slight AO even in direct light
		_env.SsaoSharpness = 0.7f;         // Sharper edges around objects

		// --- SSIL: Screen-Space Indirect Lighting (Godot 4's gem) ---
		// This makes light "bounce" off bright surfaces onto nearby dark ones.
		// Enormously improves the feeling of the scene for almost free.
		_env.SsilEnabled = true;
		_env.SsilRadius = 5.0f;
		_env.SsilIntensity = 0.6f;
		_env.SsilSharpness = 0.9f;
		_env.SsilNormalRejection = 1.2f;

		// --- TONEMAPPING: Cinematic color response ---
		_env.TonemapMode = Godot.Environment.ToneMapper.Aces;
		_env.TonemapExposure = 1.0f;       // Slightly lower — less blown out
		_env.TonemapWhite = 6.0f;          // Higher white point — richer highlights

		// --- COLOR ADJUSTMENTS: Warm, slightly saturated storybook look ---
		_env.AdjustmentEnabled = true;
		_env.AdjustmentContrast = 1.08f;   // Slightly more contrast
		_env.AdjustmentSaturation = 1.15f; // Richer colors (storybook feel)
		_env.AdjustmentBrightness = 1.0f;

		// --- FOG: Subtle atmospheric depth ---
		_env.FogEnabled = true;
		_env.FogLightColor = new Color(0.75f, 0.65f, 0.55f); // Warm haze
		_env.FogLightEnergy = 0.15f;
		_env.FogDensity = 0.0008f;         // Very subtle
		_env.FogAerialPerspective = 0.3f;  // Objects fade slightly with distance
		_env.FogSkyAffect = 0.0f;          // Don't fog the background color

		// --- VOLUMETRIC FOG: Atmospheric light shafts ---
		_env.VolumetricFogEnabled = true;
		_env.VolumetricFogDensity = 0.003f;
		_env.VolumetricFogAlbedo = new Color(0.85f, 0.75f, 0.65f);
		_env.VolumetricFogEmission = new Color(0.0f, 0.0f, 0.0f);
		_env.VolumetricFogEmissionEnergy = 0.0f;
		_env.VolumetricFogAnisotropy = 0.6f;  // Light scatters forward (god rays!)
	}

	// ================================================================
	// 2. VIGNETTE — Soft darkened corners via full-screen shader
	// ================================================================
	// Godot 4 has no built-in vignette, so we create a lightweight
	// shader on a ColorRect in a CanvasLayer. Costs almost nothing.

	private ShaderMaterial _vignetteMat;
	
	private void SetupVignetteShader()
	{
		// Shader source — subtle vignette + very light chromatic aberration
string shaderCode = @"
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_linear_mipmap;
uniform float vignette_intensity : hint_range(0.0, 1.0) = 0.35;
uniform float vignette_softness : hint_range(0.0, 1.0) = 0.45;
uniform float aberration_amount : hint_range(0.0, 0.01) = 0.001;

void fragment() {
    vec2 uv = SCREEN_UV;
    
    // Vignette
    float dist = distance(uv, vec2(0.5));
    float vig = smoothstep(vignette_softness, vignette_softness - 0.25, dist);
    
    // Very subtle chromatic aberration at edges only
    float edge = smoothstep(0.2, 0.7, dist);
    vec2 offset = (uv - 0.5) * aberration_amount * edge;
    
    vec4 col;
    col.r = texture(screen_texture, uv + offset).r;
    col.g = texture(screen_texture, uv).g;
    col.b = texture(screen_texture, uv - offset).b;
    col.a = 1.0;
    
    // Apply vignette as darkening
    col.rgb *= mix(1.0 - vignette_intensity, 1.0, vig);
    
    COLOR = col;
}
";
		Shader vignetteShader = new Shader();
		vignetteShader.Code = shaderCode;

		_vignetteMat = new ShaderMaterial();
		_vignetteMat.Shader = vignetteShader;
		_vignetteMat.SetShaderParameter("vignette_intensity", 0.3f);
		_vignetteMat.SetShaderParameter("vignette_softness", 0.45f);
		_vignetteMat.SetShaderParameter("aberration_amount", 0.0008f);

		// Put it on a full-screen ColorRect inside a CanvasLayer
		CanvasLayer layer = new CanvasLayer { Layer = 80 }; // Below Dialogic but above game
		_gm.AddChild(layer);

		ColorRect screenQuad = new ColorRect();
		screenQuad.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		screenQuad.MouseFilter = Control.MouseFilterEnum.Ignore;
		screenQuad.Material = _vignetteMat;
		layer.AddChild(screenQuad);
	}

	/// <summary>
	/// Call during combat hits to pulse the vignette darker momentarily.
	/// Usage: GameJuiceUpgrade juice = GetNode<GameJuiceUpgrade>("GameJuiceUpgrade");
	///        juice.PulseVignette();
	/// </summary>
	public void PulseVignette(float intensity = 0.7f, float duration = 0.3f)
	{
		if (_vignetteMat == null) return;
		Tween t = CreateTween();
		t.TweenMethod(Callable.From<float>(v => _vignetteMat.SetShaderParameter("vignette_intensity", v)),
			intensity, 0.3f, duration)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}

	// ================================================================
	// 3. CAMERA BREATHING — Subtle idle sway that makes the world alive
	// ================================================================
	// A perfectly still camera feels dead. This adds a very gentle
	// sine-wave drift that's almost imperceptible but makes the
	// scene feel like it's being viewed through a living eye.

	private Vector3 _camBaseOffset;
	private bool _breathingEnabled = true;

	private void SetupCameraBreathing()
	{
		_camBaseOffset = Vector3.Zero;
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;

		// Camera breathing
		if (_breathingEnabled && _cam != null && IsInstanceValid(_cam))
		{
			// Two overlapping sine waves at different speeds for organic motion
			float breathY = Mathf.Sin(_time * CamBreathSpeed) * CamBreathAmount;
			float breathX = Mathf.Sin(_time * CamBreathSpeed * 0.7f) * CamBreathAmount * 0.5f;
			_cam.HOffset = breathX;
			_cam.VOffset = breathY;
		}
	}

	/// <summary>Disable breathing during attack sequences (it's handled by screenshake)</summary>
	public void SetBreathing(bool enabled) => _breathingEnabled = enabled;

	// ================================================================
	// 4. HIT FREEZE — Micro-pause on impact for satisfying hits
	// ================================================================
	// The single highest-impact juice technique in game development.
	// Freeze time for 40-80ms on a hit. Makes attacks feel MASSIVE.
	// 
	// Usage: Add to ExecuteSingleAttack right after TakeDamage:
	//     await GameJuiceUpgrade.HitFreeze(GetTree(), 0.06f);

	public static async System.Threading.Tasks.Task HitFreeze(SceneTree tree, float duration = 0.06f)
	{
		tree.Paused = true;
		
		// Use a real OS timer, not the game timer (which is paused!)
		ulong startMs = Time.GetTicksMsec();
		ulong durationMs = (ulong)(duration * 1000);
		
		while (Time.GetTicksMsec() - startMs < durationMs)
		{
			await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		
		tree.Paused = false;
	}

	// ================================================================
	// 5. SCREEN FLASH — White/red flash on damage for visceral feedback
	// ================================================================
	// Usage: GameJuiceUpgrade.ScreenFlash(this, Colors.White, 0.1f);

	public static void ScreenFlash(Node parent, Color color, float duration = 0.15f)
	{
		CanvasLayer flashLayer = new CanvasLayer { Layer = 85 };
		parent.AddChild(flashLayer);

		ColorRect flash = new ColorRect();
		flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		flash.MouseFilter = Control.MouseFilterEnum.Ignore;
		flash.Color = new Color(color.R, color.G, color.B, 0.3f);

		flashLayer.AddChild(flash);

		Tween t = parent.CreateTween();
		t.TweenProperty(flash, "color:a", 0.0f, duration)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		t.Finished += () => flashLayer.QueueFree();
	}

	// ================================================================
	// 6. DUST PARTICLES ON MOVEMENT — Sprite3D leaves a trail
	// ================================================================
	// Call from Unit.MoveAlongPath at each step:
	//     GameJuiceUpgrade.SpawnDustPuff(this, GlobalPosition);

	public static void SpawnDustPuff(Node parent, Vector3 position)
	{
		CpuParticles3D dust = new CpuParticles3D
		{
			Emitting = true,
			OneShot = true,
			Explosiveness = 0.8f,
			Amount = 6,
			Lifetime = 0.5f,
			Direction = Vector3.Up,
			Spread = 60f,
			InitialVelocityMin = 0.5f,
			InitialVelocityMax = 1.5f,
			Gravity = new Vector3(0, -2f, 0),
			ScaleAmountMin = 0.5f,
			ScaleAmountMax = 1.2f,
		};

		dust.Mesh = new SphereMesh { Radius = 0.08f, Height = 0.16f };
		dust.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.6f, 0.5f, 0.4f, 0.6f),
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled
		};

		Curve scaleCurve = new Curve();
		scaleCurve.AddPoint(new Vector2(0, 0.5f));
		scaleCurve.AddPoint(new Vector2(0.3f, 1.0f));
		scaleCurve.AddPoint(new Vector2(1, 0));
		dust.ScaleAmountCurve = scaleCurve;

		parent.GetParent().AddChild(dust);
		dust.GlobalPosition = position + new Vector3(0, 0.1f, 0);

		// Auto-cleanup
		parent.GetTree().CreateTimer(0.8f).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(dust)) dust.QueueFree();
		};
	}

	// ================================================================
	// 7. IMPROVED DIRECTIONAL LIGHT — Warm key + cool fill
	// ================================================================
	// Call from ApplyEnvironment to add a secondary fill light.
	// This creates the classic "warm sun + cool sky" dual-light setup
	// that makes 3D scenes look dramatically better.
	//
	// Usage in ApplyEnvironment, after setting MainLight:
	//     GameJuiceUpgrade.AddFillLight(this, data.Light);

	public static void AddFillLight(Node3D parent, LightingMood mood)
	{
		// Remove any previous fill light
		var old = parent.GetNodeOrNull<DirectionalLight3D>("FillLight");
		if (old != null) old.QueueFree();

		DirectionalLight3D fill = new DirectionalLight3D { Name = "FillLight" };
		fill.ShadowEnabled = false; // Fill lights don't need shadows

		switch (mood)
		{
			case LightingMood.Noon:
				fill.LightColor = new Color(0.6f, 0.7f, 1.0f); // Cool sky blue
				fill.LightEnergy = 0.25f;
				fill.RotationDegrees = new Vector3(-30, -135, 0); // Opposite to sun
				break;
			case LightingMood.Morning:
				fill.LightColor = new Color(0.5f, 0.6f, 0.9f);
				fill.LightEnergy = 0.2f;
				fill.RotationDegrees = new Vector3(-40, -120, 0);
				break;
			case LightingMood.Night:
				fill.LightColor = new Color(0.3f, 0.35f, 0.6f);
				fill.LightEnergy = 0.15f;
				fill.RotationDegrees = new Vector3(-50, 150, 0);
				break;
			case LightingMood.Indoors:
				fill.LightColor = new Color(0.7f, 0.6f, 0.5f);
				fill.LightEnergy = 0.3f;
				fill.RotationDegrees = new Vector3(-70, 90, 0);
				break;
		}

		parent.AddChild(fill);
	}

	// ================================================================
	// 8. BOUNCE SCALE ON TURN START — Unit pops when it's their turn
	// ================================================================
	// Call this on the active unit when their turn begins.
	// Usage: GameJuiceUpgrade.BounceUnit(activeUnit);

	public static void BounceUnit(Unit unit)
	{
		if (unit == null || !GodotObject.IsInstanceValid(unit)) return;
		
		Node3D sprite = unit.GetNodeOrNull<Node3D>("Sprite3D");
		if (sprite == null) return;

		Vector3 baseScale = sprite.HasMeta("BaseScale") 
			? sprite.GetMeta("BaseScale").AsVector3() 
			: sprite.Scale;

		Tween t = unit.CreateTween();
		// Squash down
		t.TweenProperty(sprite, "scale", 
			new Vector3(baseScale.X * 1.2f, baseScale.Y * 0.7f, baseScale.Z * 1.2f), 0.08f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		// Stretch up (overshoot)
		t.TweenProperty(sprite, "scale", 
			new Vector3(baseScale.X * 0.85f, baseScale.Y * 1.25f, baseScale.Z * 0.85f), 0.12f)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		// Settle
		t.TweenProperty(sprite, "scale", baseScale, 0.15f)
			.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
	}

	// ================================================================
	// 9. TILE RIPPLE ON CLICK — Tiles around click point bounce
	// ================================================================
	// Call when the player clicks to move or attack:
	//     GameJuiceUpgrade.TileRipple(clickPosition, _grid, TileSize);

	public static void TileRipple(Vector3 center, Godot.Collections.Dictionary<Vector2I, Tile> grid, float tileSize)
	{
		Vector2I centerGrid = new Vector2I(
			Mathf.RoundToInt(center.X / tileSize),
			Mathf.RoundToInt(center.Z / tileSize));

		int radius = 3;
		for (int dx = -radius; dx <= radius; dx++)
		{
			for (int dz = -radius; dz <= radius; dz++)
			{
				Vector2I pos = centerGrid + new Vector2I(dx, dz);
				if (!grid.ContainsKey(pos)) continue;

				Tile tile = grid[pos];
				float dist = Mathf.Sqrt(dx * dx + dz * dz);
				if (dist > radius) continue;

				float delay = dist * 0.06f;
				float strength = (1f - dist / radius) * 0.15f;

				Tween t = tile.CreateTween();
				t.TweenProperty(tile, "position:y", tile.Position.Y + strength, 0.1f)
					.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out)
					.SetDelay(delay);
				t.TweenProperty(tile, "position:y", tile.Position.Y, 0.25f)
					.SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
			}
		}
	}
}
