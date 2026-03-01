using Godot;

/// <summary>
/// Central constants for story JSON and script execution.
/// Keeps event types, paths, and default battle layout in one place.
/// </summary>
public static class StoryConstants
{
	// --- JSON event type strings (must match story/*.json "Type" field) ---
	public const string EventDialogue = "Dialogue";
	public const string EventBattle = "Battle";
	public const string EventAddPartyMember = "AddPartyMember";
	public const string EventJump = "Jump";

	// --- Paths ---
	public const string DefaultCampaignPath = "res://story/campaign.json";
	public const string TimelinePathPrefix = "res://dialogic_timelines/";
	public const string TimelineExtension = ".dtl";

	// --- Default battle layout (used when JSON doesn't specify) ---
	public static readonly Vector3[] DefaultFriendlySpawns = { new Vector3(0, 0, 4), new Vector3(2, 0, 4) };
	public const float EnemyRowX = 6f;
	public const float EnemySpacing = 2f;
}
