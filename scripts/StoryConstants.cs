// StoryConstants.cs
using Godot;

public static class StoryConstants
{
	public const string EventDialogue = "Dialogue";
	public const string EventBattle = "Battle";
	public const string EventAddPartyMember = "AddPartyMember";
	public const string EventJump = "Jump";
	public const string DefaultCampaignPath = "res://story/campaign.json";
	public const string TimelinePathPrefix = "res://dialogic_timelines/";
	public const string TimelineExtension = ".dtl";
 
	// === MOVED CLOSER TO CENTER ===
	// 30x30 grid, TileSize=2. World center ≈ (29, 0, 29).
	// Allies at tiles 8-10 (X=16-20), enemies at tiles 18-20 (X=36-40).
	// ~8-10 tiles apart → combat starts in 2-3 rounds.
	public static readonly Vector3[] DefaultFriendlySpawns = {
		new(16, 0, 24), new(16, 0, 28), new(16, 0, 32),
		new(20, 0, 22), new(20, 0, 26), new(20, 0, 30), new(20, 0, 34),
	};
 
	public const float EnemyStartX = 36f;
	public const float EnemySpacingX = 4f;
	public const float EnemySpacingZ = 4f;
	public const float EnemyStartZ = 22f;
	public const int EnemyColumnsMax = 3;
}
