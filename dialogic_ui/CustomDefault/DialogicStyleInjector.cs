using Godot;
using System.Collections.Generic;

public partial class DialogicStyleInjector : Node
{
	private Control _mainDialogPanel;
	private Control _nameBoxPanel;
	private RichTextLabel _dialogText;
	private Label _nameLabel;
	private VBoxContainer _choiceContainer;
	
	private string _lastName = "";
	private string _lastDialogText = ""; 
	
	private HashSet<Node> _processedNodes = new HashSet<Node>();
	private Dictionary<Control, Tween> _activeTweens = new Dictionary<Control, Tween>();

	public override void _Process(double delta)
	{
		// === THE FIX: Purge dead nodes BEFORE doing anything! ===
		ValidateNodes();

		HuntForDialogicNodes(GetParent());
		
		bool shouldPop = false;
		
		if (_nameLabel != null && _nameLabel.Text != _lastName)
		{
			_lastName = _nameLabel.Text;
			shouldPop = true;
		}
		
		if (_dialogText != null && _dialogText.Text != _lastDialogText)
		{
			_lastDialogText = _dialogText.Text;
			shouldPop = true;
		}

		if (shouldPop && !string.IsNullOrEmpty(_lastName)) 
		{
			TriggerSpeakerJuice();
		}
	}

	private void ValidateNodes()
	{
		// If Dialogic jumps timelines, it destroys UI nodes. We must release them instantly!
		if (_dialogText != null && !IsInstanceValid(_dialogText)) _dialogText = null;
		if (_nameLabel != null && !IsInstanceValid(_nameLabel)) _nameLabel = null;
		if (_choiceContainer != null && !IsInstanceValid(_choiceContainer)) _choiceContainer = null;
		if (_mainDialogPanel != null && !IsInstanceValid(_mainDialogPanel)) _mainDialogPanel = null;
		if (_nameBoxPanel != null && !IsInstanceValid(_nameBoxPanel)) _nameBoxPanel = null;
		
		// Remove dead nodes from our processed list so we don't leak memory
		_processedNodes.RemoveWhere(n => !IsInstanceValid(n));
	}

	private void HuntForDialogicNodes(Node current)
	{
		if (!IsInstanceValid(current)) return;

		if (!_processedNodes.Contains(current) && current is Control control)
		{
			ApplyJuiceToNode(control);
			_processedNodes.Add(current);
		}

		foreach (Node child in current.GetChildren())
		{
			HuntForDialogicNodes(child);
		}
	}

	private void ApplyJuiceToNode(Control control)
	{
		string nodeName = control.Name.ToString().ToLower();

		if (control is VBoxContainer vbox && nodeName.Contains("choice"))
		{
			_choiceContainer = vbox;
			_choiceContainer.AddThemeConstantOverride("separation", 20); 
			_choiceContainer.ChildEnteredTree += OnChoiceAdded;
			foreach (Node child in _choiceContainer.GetChildren()) OnChoiceAdded(child);
		}
		else if (control is RichTextLabel rtl && nodeName.Contains("text"))
		{
			_dialogText = rtl;
			rtl.Theme = GameManager.Instance.MasterTheme;
			rtl.AddThemeFontSizeOverride("normal_font_size", 30); rtl.AddThemeFontSizeOverride("bold_font_size", 32);
			rtl.AddThemeColorOverride("default_color", new Color(0.95f, 0.95f, 0.95f, 1f));
			
			if (GameManager.Instance.MasterTheme?.DefaultFont != null)
			{
				rtl.AddThemeFontOverride("normal_font", GameManager.Instance.MasterTheme.DefaultFont);
				rtl.AddThemeFontOverride("bold_font", GameManager.Instance.MasterTheme.DefaultFont);
			}

			Control bg = FindParentBackground(rtl);
			if (bg != null && _mainDialogPanel == null)
			{
				_mainDialogPanel = bg;
				_mainDialogPanel.CustomMinimumSize = new Vector2(1100, 240); 
				_mainDialogPanel.SelfModulate = Colors.White; 

				StyleBoxFlat dialogStyle = GameManager.Instance.BaseUIStyle != null ? (StyleBoxFlat)GameManager.Instance.BaseUIStyle.Duplicate() : new StyleBoxFlat { CornerRadiusTopLeft = 16, CornerRadiusTopRight = 16, CornerRadiusBottomLeft = 16, CornerRadiusBottomRight = 16 };
				dialogStyle.BgColor = new Color(0.06f, 0.06f, 0.09f, 0.98f); dialogStyle.BorderColor = new Color(1f, 0.85f, 0.3f, 1f); 
				dialogStyle.BorderWidthTop = 6; dialogStyle.BorderWidthBottom = 6; dialogStyle.BorderWidthLeft = 6; dialogStyle.BorderWidthRight = 6;
				dialogStyle.ShadowSize = 40; dialogStyle.ShadowOffset = new Vector2(0, 15);
				dialogStyle.ContentMarginTop = 90; dialogStyle.ContentMarginLeft = 40; dialogStyle.ContentMarginRight = 40; dialogStyle.ContentMarginBottom = 30;

				_mainDialogPanel.AddThemeStyleboxOverride("panel", dialogStyle);
				_mainDialogPanel.PivotOffset = new Vector2(550, 240);
				_mainDialogPanel.Scale = new Vector2(0.01f, 0.01f);
				CreateTween().TweenProperty(_mainDialogPanel, "scale", Vector2.One, 0.5f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}
		}
		else if (control is Label lbl && nodeName.Contains("name"))
		{
			_nameLabel = lbl;
			lbl.Theme = GameManager.Instance.MasterTheme;
			lbl.AddThemeFontSizeOverride("font_size", 28); lbl.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.7f));
			lbl.HorizontalAlignment = HorizontalAlignment.Center; lbl.VerticalAlignment = VerticalAlignment.Center;
			if (GameManager.Instance.MasterTheme?.DefaultFont != null) lbl.AddThemeFontOverride("font", GameManager.Instance.MasterTheme.DefaultFont);

			Control bg = FindParentBackground(lbl);
			if (bg != null && _nameBoxPanel == null)
			{
				_nameBoxPanel = bg;
				_nameBoxPanel.SelfModulate = Colors.White; 
				
				if (GameManager.Instance.BadgeStyle != null)
				{
					StyleBoxFlat badge = (StyleBoxFlat)GameManager.Instance.BadgeStyle.Duplicate();
					badge.ContentMarginLeft = 35; badge.ContentMarginRight = 35; badge.ContentMarginTop = 15; badge.ContentMarginBottom = 15;
					_nameBoxPanel.AddThemeStyleboxOverride("panel", badge);
				}
				
				_nameBoxPanel.RotationDegrees = -2f;
				_nameBoxPanel.Position = new Vector2(_nameBoxPanel.Position.X, _nameBoxPanel.Position.Y - 30f);
			}
		}
	}

	private Control FindParentBackground(Control child)
	{
		Node current = child.GetParent();
		while (current != null && current is Control parentCtrl)
		{
			if (parentCtrl is PanelContainer || parentCtrl is Panel) return parentCtrl;
			current = current.GetParent();
		}
		return null;
	}

	private void OnChoiceAdded(Node node)
	{
		if (node is Button btn && !_processedNodes.Contains(btn))
		{
			_processedNodes.Add(btn);
			btn.CustomMinimumSize = new Vector2(600, 80); btn.AddThemeFontSizeOverride("font_size", 28);
			
			if (GameManager.Instance.MasterTheme != null)
			{
				btn.AddThemeStyleboxOverride("normal", GameManager.Instance.MasterTheme.GetStylebox("normal", "Button"));
				btn.AddThemeStyleboxOverride("hover", GameManager.Instance.MasterTheme.GetStylebox("hover", "Button"));
				btn.AddThemeStyleboxOverride("pressed", GameManager.Instance.MasterTheme.GetStylebox("pressed", "Button"));
				btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
				if (GameManager.Instance.MasterTheme.DefaultFont != null) btn.AddThemeFontOverride("font", GameManager.Instance.MasterTheme.DefaultFont);
			}
			
			btn.PivotOffset = btn.CustomMinimumSize / 2; btn.Scale = Vector2.Zero;
			float delay = btn.GetIndex() * 0.15f; 
			CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.4f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(delay);
			
			btn.MouseEntered += () => { btn.PivotOffset = btn.Size / 2; CreateTween().TweenProperty(btn, "scale", new Vector2(1.08f, 1.08f), 0.15f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out); btn.RotationDegrees = 1f; };
			btn.MouseExited += () => { CreateTween().TweenProperty(btn, "scale", Vector2.One, 0.2f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out); btn.RotationDegrees = 0f; };
			btn.ButtonDown += () => { CreateTween().TweenProperty(btn, "scale", new Vector2(0.9f, 0.9f), 0.1f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out); };
		}
	}

	private void PlayBounceTween(Control target, float hopHeight)
	{
		if (!IsInstanceValid(target)) return;

		if (!target.HasMeta("BasePosY"))
		{
			target.SetMeta("BasePosY", target.Position.Y);
			target.SetMeta("BaseScale", target.Scale);
		}

		float baseY = target.GetMeta("BasePosY").AsSingle();
		Vector2 baseScale = target.GetMeta("BaseScale").AsVector2();
		target.PivotOffset = new Vector2(target.Size.X / 2, target.Size.Y);

		if (_activeTweens.ContainsKey(target))
		{
			Tween oldTween = _activeTweens[target];
			if (oldTween != null && oldTween.IsValid()) oldTween.Kill();
		}

		Tween hopTween = CreateTween();
		
		// === THE FIX: Bind the tween to the UI node. 
		// If Dialogic deletes the UI, the tween instantly self-destructs safely! ===
		hopTween.BindNode(target);
		
		_activeTweens[target] = hopTween;

		hopTween.TweenProperty(target, "position:y", baseY - hopHeight, 0.15f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		hopTween.Parallel().TweenProperty(target, "scale", new Vector2(baseScale.X * 0.85f, baseScale.Y * 1.25f), 0.15f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		hopTween.Chain().TweenProperty(target, "position:y", baseY, 0.25f).SetTrans(Tween.TransitionType.Bounce).SetEase(Tween.EaseType.Out);
		hopTween.Parallel().TweenProperty(target, "scale", new Vector2(baseScale.X * 1.15f, baseScale.Y * 0.85f), 0.15f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
		hopTween.Chain().TweenProperty(target, "scale", baseScale, 0.15f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
	}

	private async void TriggerSpeakerJuice()
	{
		if (_nameBoxPanel != null && IsInstanceValid(_nameBoxPanel))
		{
			PlayBounceTween(_nameBoxPanel, 20f); 
			Tween wiggle = CreateTween();
			wiggle.BindNode(_nameBoxPanel); // Bind this tween too!
			wiggle.TweenProperty(_nameBoxPanel, "rotation_degrees", 3f, 0.05f);
			wiggle.TweenProperty(_nameBoxPanel, "rotation_degrees", -3f, 0.1f);
			wiggle.TweenProperty(_nameBoxPanel, "rotation_degrees", -2f, 0.05f); 
		}
		
		await ToSignal(GetTree().CreateTimer(0.1f), "timeout");
		
		// === THE FIX: Stop execution if the Injector was destroyed during the await pause ===
		if (!IsInsideTree()) return;
		
		Control activePortrait = FindPortraitNodeByName(GetParent(), _lastName);
		if (activePortrait != null && IsInstanceValid(activePortrait))
		{
			PlayBounceTween(activePortrait, 60f); 
		}
		
		if (GameManager.Instance != null && IsInstanceValid(GameManager.Instance))
		{
			foreach (Node child in GameManager.Instance.GetChildren())
			{
				if (child is Unit u && u.Data != null)
				{
					u.SetHovered(u.Data.Profile.Name == _lastName);
				}
			}
		}
	}

	private Control FindPortraitNodeByName(Node current, string speakerName)
	{
		if (!IsInstanceValid(current)) return null;

		string nodeName = current.Name.ToString().ToLower();
		string targetName = speakerName.ToLower();

		if (current is Control ctrl && !(current is Label) && !(current is RichTextLabel))
		{
			if (nodeName.Contains(targetName) && !nodeName.Contains("name")) return ctrl;
		}

		foreach (Node child in current.GetChildren())
		{
			Control found = FindPortraitNodeByName(child, speakerName);
			if (found != null) return found;
		}
		return null;
	}

	public override void _ExitTree()
	{
		if (_choiceContainer != null && IsInstanceValid(_choiceContainer))
		{
			_choiceContainer.ChildEnteredTree -= OnChoiceAdded;
		}
		
		foreach (var tween in _activeTweens.Values)
		{
			if (tween != null && tween.IsValid()) tween.Kill();
		}
		
		_activeTweens.Clear();
		_processedNodes.Clear();
	}
}
