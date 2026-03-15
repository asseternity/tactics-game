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

	// 30x30 grid: allies left, enemies right
	public static readonly Vector3[] DefaultFriendlySpawns = {
		new(4, 0, 24), new(4, 0, 28), new(4, 0, 32),
		new(8, 0, 22), new(8, 0, 26), new(8, 0, 30), new(8, 0, 34),
	};

	public const float EnemyStartX = 48f;
	public const float EnemySpacingX = 4f;
	public const float EnemySpacingZ = 4f;
	public const float EnemyStartZ = 22f;
	public const int EnemyColumnsMax = 3;
}
