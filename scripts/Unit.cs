// Unit.cs — Selection ring flat at feet, spin tween tracked
using Godot;
using System.Threading.Tasks;
using System.Collections.Generic;

public partial class Unit : Node3D
{
	public bool IsFriendly = true;
	public event System.Action<Unit> OnDied;
	public PersistentUnit Data { get; private set; }
	public bool HasMoved = false;
	public bool HasAttacked = false;
	public bool IsSelected { get; private set; } = false;
	public UnitFacing CurrentFacing { get; private set; }

	private Sprite3D _sprite; private Label3D _targetIcon;
	private SubViewport _uiViewport; private Sprite3D _uiSprite;
	private ProgressBar _hpBar, _hpPreviewBar; private Label _hpLabel;
	private ProgressBar _xpBar; private Label _xpLabel;
	private Tween _previewTween; private bool _isPreviewing = false;
	private bool _isHovered = false; private Tween _hoverTween;
	private MeshInstance3D _selectionRing; private Tween _ringPulseTween, _ringSpinTween;

	public override void _Ready()
	{
		_sprite = GetNode<Sprite3D>("Sprite3D");
		_targetIcon = new Label3D { Text = "▼", FontSize = 120, Modulate = new Color(1, 0.2f, 0.2f), OutlineModulate = Colors.Black, OutlineSize = 10, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, Position = new Vector3(0, 2.9f, 0), Visible = false, RenderPriority = 20 };
		AddChild(_targetIcon);
		Tween bob = CreateTween().SetLoops();
		bob.TweenProperty(_targetIcon, "position:y", 3.2f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		bob.TweenProperty(_targetIcon, "position:y", 2.9f, 0.4f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		_uiViewport = new SubViewport { TransparentBg = true, Size = new Vector2I(200, 100), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
		AddChild(_uiViewport);
		VBoxContainer vb = new() { Alignment = BoxContainer.AlignmentMode.Center }; vb.SetAnchorsPreset(Control.LayoutPreset.FullRect); vb.AddThemeConstantOverride("separation", 6); _uiViewport.AddChild(vb);

		MarginContainer hpC = new() { CustomMinimumSize = new Vector2(180, 35) };
		_hpPreviewBar = new ProgressBar { CustomMinimumSize = new Vector2(180, 35), ShowPercentage = false, Step = 0.1 };
		_hpPreviewBar.AddThemeStyleboxOverride("background", MS(new Color(0.1f, 0.1f, 0.1f, 0.9f)));
		_hpPreviewBar.AddThemeStyleboxOverride("fill", MS(new Color(1f, 0f, 0f)));
		_hpBar = new ProgressBar { CustomMinimumSize = new Vector2(180, 35), ShowPercentage = false, Step = 0.1 };
		_hpBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
		_hpBar.AddThemeStyleboxOverride("fill", MS(new Color(0.2f, 0.9f, 0.2f)));
		_hpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_hpLabel.AddThemeFontSizeOverride("font_size", 22); _hpLabel.AddThemeConstantOverride("outline_size", 6);
		hpC.AddChild(_hpPreviewBar); hpC.AddChild(_hpBar); hpC.AddChild(_hpLabel); vb.AddChild(hpC);

		MarginContainer xpC = new() { CustomMinimumSize = new Vector2(180, 35) };
		_xpBar = new ProgressBar { CustomMinimumSize = new Vector2(180, 25), ShowPercentage = false, Step = 0.1f };
		_xpBar.AddThemeStyleboxOverride("background", MS(new Color(0.1f, 0.1f, 0.1f, 0.9f)));
		_xpBar.AddThemeStyleboxOverride("fill", MS(new Color(1f, 0.8f, 0f)));
		_xpLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_xpLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_xpLabel.AddThemeFontSizeOverride("font_size", 16); _xpLabel.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.6f)); _xpLabel.AddThemeConstantOverride("outline_size", 5);
		_xpBar.AddChild(_xpLabel); xpC.AddChild(_xpBar); vb.AddChild(xpC);

		_uiSprite = new Sprite3D { Texture = _uiViewport.GetTexture(), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 10, PixelSize = 0.008f, Position = new Vector3(0, 2.1f, 0) };
		AddChild(_uiSprite);

		// Selection ring — TorusMesh is ALREADY horizontal (XZ plane) in Godot 4. No rotation needed.
		_selectionRing = new MeshInstance3D
		{
			Mesh = new TorusMesh { InnerRadius = 0.55f, OuterRadius = 0.75f, Rings = 24, RingSegments = 24 },
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
			Position = new Vector3(0, 0.05f, 0),
			Visible = false
		};
		_selectionRing.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.3f, 1f, 0.5f, 0.7f), EmissionEnabled = true,
			Emission = new Color(0.3f, 1f, 0.5f), EmissionEnergyMultiplier = 2f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		AddChild(_selectionRing);
	}

	StyleBoxFlat MS(Color c) => new() { BgColor = c, CornerRadiusTopLeft=8, CornerRadiusTopRight=8, CornerRadiusBottomLeft=8, CornerRadiusBottomRight=8, BorderWidthBottom=3, BorderWidthTop=3, BorderWidthLeft=3, BorderWidthRight=3, BorderColor=Colors.Black };

	public void Setup(PersistentUnit data, bool isFriendly)
	{
		Data = data; IsFriendly = isFriendly; CurrentFacing = data.Profile.DefaultFacing;
		if (GameManager.Instance?.MasterTheme != null) { _hpLabel.Theme = GameManager.Instance.MasterTheme; _xpLabel.Theme = GameManager.Instance.MasterTheme; }
		_hpBar.MaxValue = Data.MaxHP; _hpPreviewBar.MaxValue = Data.MaxHP;
		if (IsFriendly) { _xpBar.Visible = true; _xpBar.MaxValue = Data.MaxXP; _xpBar.Value = Data.CurrentXP; }
		else { _xpBar.Visible = false; _hpBar.AddThemeStyleboxOverride("fill", MS(new Color(0.9f, 0.2f, 0.2f))); }

		Texture2D tex = GD.Load<Texture2D>(data.Profile.SpritePath);
		if (tex != null)
		{
			_sprite.Texture = tex; float sf = 1.8f / (tex.GetHeight() * _sprite.PixelSize);
			_sprite.Scale = new Vector3(sf, sf, sf); _sprite.SetMeta("BaseScale", _sprite.Scale);
			foreach (Node c in _sprite.GetChildren()) c.QueueFree();
			_sprite.MaterialOverride = null; _sprite.AlphaCut = SpriteBase3D.AlphaCutMode.Discard;
			_sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.On; _sprite.Shaded = true; _sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
			_sprite.AddChild(new Sprite3D { Texture = tex, PixelSize = _sprite.PixelSize, Modulate = Colors.Black,
				AlphaCut = SpriteBase3D.AlphaCutMode.Discard, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				Shaded = false, Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
				Scale = new Vector3(1.08f, 1.08f, 1.08f), Position = new Vector3(0, 0, -0.02f), RenderPriority = -1 });
		}
		_sprite.SetMeta("BasePos", _sprite.Position); UpdateVisuals();
	}

	public void UpdateVisuals()
	{
		if (_sprite == null || Data == null) return;
		int thp = Data.GetTotalMaxHP(); if (Data.CurrentHP > thp) Data.CurrentHP = thp;
		if (!_isPreviewing) { _hpLabel.Text = $"{Data.CurrentHP}/{thp}"; _hpBar.MaxValue = thp; _hpPreviewBar.MaxValue = thp;
			CreateTween().TweenProperty(_hpBar, "value", (double)Data.CurrentHP, 0.15f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out); _hpPreviewBar.Value = Data.CurrentHP; }
		if (IsFriendly) _xpLabel.Text = $"Level {Data.Level}";

		Color sc = IsSelected ? new Color(1.6f, 1.5f, 0.7f) : (IsFriendly && HasMoved && HasAttacked) ? new Color(0.4f, 0.4f, 0.4f) : IsFriendly ? new Color(1.2f, 1.2f, 1.2f) : new Color(1.3f, 0.9f, 0.9f);
		if (!_isPreviewing) _sprite.Modulate = sc;

		// Ring
		if (_selectionRing != null)
		{
			if (IsSelected && IsFriendly)
			{
				_selectionRing.Visible = true;
				if (_ringPulseTween == null || !_ringPulseTween.IsValid())
				{
					_selectionRing.Scale = Vector3.Zero;
					CreateTween().TweenProperty(_selectionRing, "scale", Vector3.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
					var mat = (StandardMaterial3D)_selectionRing.MaterialOverride;
					_ringPulseTween = CreateTween().SetLoops();
					_ringPulseTween.TweenProperty(mat, "emission_energy_multiplier", 1.0f, 0.6f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
					_ringPulseTween.TweenProperty(mat, "emission_energy_multiplier", 3.0f, 0.6f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
					_ringSpinTween = CreateTween().SetLoops();
					_ringSpinTween.TweenProperty(_selectionRing, "rotation_degrees:y", _selectionRing.RotationDegrees.Y + 360f, 4f);
				}
			}
			else
			{
				if (_ringPulseTween != null && _ringPulseTween.IsValid()) { _ringPulseTween.Kill(); _ringPulseTween = null; }
				if (_ringSpinTween != null && _ringSpinTween.IsValid()) { _ringSpinTween.Kill(); _ringSpinTween = null; }
				if (_selectionRing.Visible) { var sh = CreateTween(); sh.TweenProperty(_selectionRing, "scale", Vector3.Zero, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In); sh.Finished += () => _selectionRing.Visible = false; }
			}
		}
	}

	public int GetMinDamage() => Mathf.FloorToInt(Data.AttackDamage * 0.8f);
	public int GetMaxDamage() => Mathf.CeilToInt(Data.AttackDamage * 1.2f);
	public void PreviewDamage(int dmg) { if (_isPreviewing) return; _isPreviewing = true; int nh = Mathf.Max(0, Data.CurrentHP - dmg); _hpBar.Value = nh; _hpLabel.Text = $"{nh}/{Data.MaxHP} (-{Mathf.Min(dmg, Data.CurrentHP)})"; if (_previewTween != null && _previewTween.IsValid()) _previewTween.Kill(); _previewTween = CreateTween().SetLoops(); var ps = (StyleBoxFlat)_hpPreviewBar.GetThemeStylebox("fill"); _previewTween.TweenProperty(ps, "bg_color", new Color(1f, 1f, 1f), 0.15f); _previewTween.TweenProperty(ps, "bg_color", new Color(1f, 0f, 0f), 0.15f); if (nh <= 0) { _previewTween.Parallel().TweenProperty(_sprite, "modulate", new Color(5f, 5f, 5f), 0.15f); _previewTween.Parallel().TweenProperty(_sprite, "modulate", new Color(1f, 0f, 0f), 0.15f).SetDelay(0.15f); } }
	public void ClearPreview() { if (!_isPreviewing) return; _isPreviewing = false; if (_previewTween != null && _previewTween.IsValid()) _previewTween.Kill(); ((StyleBoxFlat)_hpPreviewBar.GetThemeStylebox("fill")).BgColor = new Color(1f, 0f, 0f); UpdateVisuals(); }

	public async Task TakeDamage(int dmg, Unit attacker = null)
	{
		ClearPreview(); Data.CurrentHP -= dmg; if (Data.CurrentHP < 0) Data.CurrentHP = 0; UpdateVisuals();
		if (dmg > 0) SpawnHitParticles();
		if (Data.CurrentHP <= 0)
		{
			var bs = _sprite.HasMeta("BaseScale") ? _sprite.GetMeta("BaseScale").AsVector3() : _sprite.Scale;
			Tween dt = CreateTween();
			dt.TweenProperty(_sprite, "scale", new Vector3(bs.X * 1.5f, bs.Y * 0.2f, bs.Z), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			dt.TweenProperty(this, "scale", new Vector3(0.001f, 0.001f, 0.001f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
			await ToSignal(dt, Tween.SignalName.Finished);
			SpawnDeathParticles(); Visible = false;
			if (attacker != null && !IsFriendly) { await GameManager.Instance.RollForLoot(); if (GodotObject.IsInstanceValid(attacker)) await attacker.GainXP(Data.XPReward); }
			OnDied?.Invoke(this); QueueFree();
		}
	}

	public async Task GainXP(int amt)
	{
		if (!IsFriendly) return; Data.CurrentXP += amt;
		CreateTween().TweenProperty(_xpBar, "value", (double)Data.CurrentXP, 0.5f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		await ToSignal(GetTree().CreateTimer(0.6f), "timeout");
		if (Data.CurrentXP >= Data.MaxXP) { Data.Level++; Data.CurrentXP -= Data.MaxXP; Data.MaxXP = Mathf.RoundToInt(Data.MaxXP * 1.5f);
			Label3D lv = new() { Text = "LEVEL UP!", Modulate = new Color(1, 0.8f, 0), FontSize = 80, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, OutlineModulate = Colors.Black, OutlineSize = 10, Position = GlobalPosition + Vector3.Up * 3f };
			GetParent().AddChild(lv); var lt = CreateTween(); lt.TweenProperty(lv, "position:y", lv.Position.Y + 1.5f, 1.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); lt.Parallel().TweenProperty(lv, "modulate:a", 0f, 1.5f); lt.Finished += () => lv.QueueFree();
			_xpBar.MaxValue = Data.MaxXP; _xpBar.Value = 0; UpdateVisuals(); await GameManager.Instance.ShowLevelUpScreen(this); await GainXP(0); }
	}

	public async Task MoveAlongPath(List<Vector3> path)
	{
		HasMoved = true; UpdateVisuals();
		foreach (var sp in path) { await FaceDirection(sp); var mt = CreateTween(); mt.Parallel().TweenProperty(this, "position:x", sp.X, 0.18f); mt.Parallel().TweenProperty(this, "position:z", sp.Z, 0.18f);
			float hp2 = Mathf.Max(Position.Y, sp.Y) + 0.8f; var ht = CreateTween(); ht.TweenProperty(this, "position:y", hp2, 0.09f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out); ht.TweenProperty(this, "position:y", sp.Y, 0.09f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			await ToSignal(mt, "finished"); }
	}

	public void SetHovered(bool h, bool tgt = false) { if (_isHovered == h) return; if (h && !IsFriendly && !tgt) { _isHovered = h; return; } _isHovered = h;
		if (_hoverTween != null && _hoverTween.IsValid()) _hoverTween.Kill(); _hoverTween = CreateTween();
		var bs = _sprite.HasMeta("BaseScale") ? _sprite.GetMeta("BaseScale").AsVector3() : _sprite.Scale;
		var bp = _sprite.HasMeta("BasePos") ? _sprite.GetMeta("BasePos").AsVector3() : _sprite.Position;
		if (h) { if (IsFriendly) { _hoverTween.TweenProperty(_sprite, "scale", new Vector3(bs.X*1.05f, bs.Y*1.15f, bs.Z*1.05f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); _hoverTween.Parallel().TweenProperty(_sprite, "position:y", bp.Y+0.15f, 0.15f); }
			else if (tgt) { _hoverTween.TweenProperty(_sprite, "scale", new Vector3(bs.X*1.25f, bs.Y*0.9f, bs.Z*1.25f), 0.1f).SetTrans(Tween.TransitionType.Bounce); _hoverTween.TweenProperty(_sprite, "rotation_degrees:z", 8f, 0.05f); _hoverTween.TweenProperty(_sprite, "rotation_degrees:z", -8f, 0.05f); _hoverTween.TweenProperty(_sprite, "rotation_degrees:z", 0f, 0.05f); } }
		else { _hoverTween.TweenProperty(_sprite, "scale", bs, 0.2f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); _hoverTween.Parallel().TweenProperty(_sprite, "position:y", bp.Y, 0.2f).SetTrans(Tween.TransitionType.Bounce); _hoverTween.Parallel().TweenProperty(_sprite, "rotation_degrees:z", 0f, 0.1f); } }

	public async Task FaceDirection(Vector3 tgt) { if (Data.Profile.DefaultFacing == UnitFacing.Center) return; var cam = GetViewport().GetCamera3D(); if (cam == null) return;
		float srd = (tgt - GlobalPosition).Dot(cam.GlobalTransform.Basis.X); if (Mathf.Abs(srd) < 0.1f) return;
		var des = srd > 0 ? UnitFacing.Right : UnitFacing.Left;
		if (CurrentFacing != des) { CurrentFacing = des; var bp = _sprite.HasMeta("BasePos") ? _sprite.GetMeta("BasePos").AsVector3() : _sprite.Position; var bs = _sprite.HasMeta("BaseScale") ? _sprite.GetMeta("BaseScale").AsVector3() : _sprite.Scale;
			var ft = CreateTween(); ft.Parallel().TweenProperty(_sprite, "position:y", bp.Y+0.6f, 0.12f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out); ft.Parallel().TweenProperty(_sprite, "scale:x", 0.01f, 0.12f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
			await ToSignal(ft, Tween.SignalName.Finished);
			bool fl = CurrentFacing != Data.Profile.DefaultFacing; _sprite.FlipH = fl; if (_sprite.GetChildCount() > 0 && _sprite.GetChild(0) is Sprite3D ol) ol.FlipH = fl;
			var lt = CreateTween(); lt.Parallel().TweenProperty(_sprite, "position:y", bp.Y, 0.15f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out); lt.Parallel().TweenProperty(_sprite, "scale:x", bs.X, 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			await ToSignal(lt, Tween.SignalName.Finished); } }

	void SpawnHitParticles() { var p = new CpuParticles3D { Emitting=false, OneShot=true, Explosiveness=0.9f, Amount=12, Lifetime=0.5f, Position=Vector3.Up*1.2f }; p.Mesh=new BoxMesh{Size=new(.15f,.15f,.15f)}; p.MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color(1,.9f,.2f),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,NoDepthTest=true}; p.Direction=Vector3.Up;p.Spread=90f;p.InitialVelocityMin=4f;p.InitialVelocityMax=8f;p.Gravity=new(0,-15f,0); var sc=new Curve();sc.AddPoint(new(0,1));sc.AddPoint(new(1,0));p.ScaleAmountCurve=sc; AddChild(p);p.Emitting=true; GetTree().CreateTimer(0.6f).Timeout+=()=>{if(GodotObject.IsInstanceValid(p))p.QueueFree();}; }
	void SpawnDeathParticles() { var p = new CpuParticles3D{Emitting=false,OneShot=true,Explosiveness=1f,Amount=25,Lifetime=1f}; p.Mesh=new SphereMesh{Radius=0.25f,Height=0.5f}; p.MaterialOverride=new StandardMaterial3D{AlbedoColor=IsFriendly?new Color(0.2f,0.5f,1f):new Color(0.8f,0.2f,0.2f),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded}; p.Direction=Vector3.Up;p.Spread=180f;p.InitialVelocityMin=5f;p.InitialVelocityMax=10f;p.Gravity=new(0,-12f,0); var sc=new Curve();sc.AddPoint(new(0,1));sc.AddPoint(new(0.7f,0.8f));sc.AddPoint(new(1,0));p.ScaleAmountCurve=sc; GetParent().AddChild(p);p.GlobalPosition=GlobalPosition+Vector3.Up;p.Emitting=true; GetTree().CreateTimer(1.2f).Timeout+=()=>{if(GodotObject.IsInstanceValid(p))p.QueueFree();}; }

	public void SetSelected(bool s) { IsSelected = s; UpdateVisuals(); }
	public void NewTurn() { HasMoved = false; HasAttacked = false; IsSelected = false; UpdateVisuals(); }
	public void SetTargetable(bool t) { if (_targetIcon != null) _targetIcon.Visible = t; }
}
