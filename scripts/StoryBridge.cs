using Godot;

public partial class StoryBridge : Node
{
	// Dialogic will call this globally!
	public bool HasFlag(string flagName)
	{
		return GameManager.Instance != null && GameManager.Instance.HasFlag(flagName);
	}

	// Dialogic will call this globally!
	public int GetRelationship(string charName, string relType)
	{
		if (GameManager.Instance != null)
			return GameManager.Instance.GetRelationship(charName, relType);
		
		return 0;
	}
}
