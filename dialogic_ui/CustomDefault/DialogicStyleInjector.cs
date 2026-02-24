using Godot;

public partial class DialogicStyleInjector : Node
{
	public override void _Ready()
	{
		CallDeferred(MethodName.ApplyMasterTheme);
	}

	private void ApplyMasterTheme()
	{
		if (GameManager.Instance == null || GameManager.Instance.MasterTheme == null) return;
		ApplyThemeRecursive(GetParent(), GameManager.Instance.MasterTheme);
	}

	private void ApplyThemeRecursive(Node node, Theme theme)
	{
		if (node is Control control)
		{
			// Did we find the NPC Name Box? Give it the sleek red badge!
			if (control is PanelContainer && control.Name.ToString().Contains("Name"))
			{
				control.AddThemeStyleboxOverride("panel", GameManager.Instance.BadgeStyle);
			}
			// Did we find the main Dialog Box? Bounce it in!
			else if (control is PanelContainer && control.Name.ToString().Contains("DialogTextPanel"))
			{
				control.Theme = theme;
				control.RemoveThemeStyleboxOverride("panel");
				
				// === THE JUICE ===
				control.PivotOffset = new Vector2(control.Size.X / 2, control.Size.Y); // Anchor to bottom center
				control.Scale = new Vector2(0.01f, 0.01f);
				Tween bounce = CreateTween();
				bounce.TweenProperty(control, "scale", Vector2.One, 0.35f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}
			// Everything else (like Choice Buttons) gets the Master Theme
			else
			{
				control.Theme = theme;
				control.RemoveThemeStyleboxOverride("panel");
				control.RemoveThemeStyleboxOverride("normal");
				control.RemoveThemeStyleboxOverride("hover");
				control.RemoveThemeStyleboxOverride("pressed");
			}
		}

		foreach (Node child in node.GetChildren())
		{
			ApplyThemeRecursive(child, theme);
		}
	}
}
