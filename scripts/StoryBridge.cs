using Godot;

public partial class StoryBridge : Node
{
	public bool HasFlag(string flagName)
	{
		return GameManager.Instance != null && GameManager.Instance.HasFlag(flagName);
	}

	public int GetBondXP(string charName)
	{
		if (GameManager.Instance != null)
			return GameManager.Instance.GetBondXP(charName);
		return 0;
	}

	// Backwards compat — old Dialogic conditions may still call this
	public int GetRelationship(string charName, string relType)
	{
		return GetBondXP(charName);
	}
}
