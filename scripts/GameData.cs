using Godot;
using System.Collections.Generic;

public struct UnitProfile
{
	public string Name;
	public string SpritePath;
	public int MaxHP;
	public int AttackDamage;
	public int AttackRange; 
	public int Movement;
	public int XPReward;

	public UnitProfile(string name, string spritePath, int maxHp, int attackDmg, int attackRange, int movement, int xpReward)
	{
		Name = name; SpritePath = spritePath; MaxHP = maxHp; 
		AttackDamage = attackDmg; AttackRange = attackRange; Movement = movement; XPReward = xpReward;
	}
}

public class PersistentUnit
{
	public UnitProfile Profile;
	public int Level = 1;
	public int CurrentXP = 0;
	public int MaxXP = 100;
	
	public int MaxHP;
	public int CurrentHP;
	public int AttackDamage;
	public int AttackRange;
	public int Movement;
	public int XPReward; 

	public PersistentUnit(UnitProfile profile)
	{
		Profile = profile;
		MaxHP = profile.MaxHP;
		CurrentHP = profile.MaxHP;
		AttackDamage = profile.AttackDamage;
		AttackRange = profile.AttackRange;
		Movement = profile.Movement;
		XPReward = profile.XPReward;
	}

	public void HealBetweenBattles()
	{
		CurrentHP += Mathf.RoundToInt(MaxHP * 0.3f);
		if (CurrentHP <= 0) CurrentHP = 1; 
		if (CurrentHP > MaxHP) CurrentHP = MaxHP;
	}
}

public struct UnitSpawn
{
	public string ProfileId;
	public Vector3 Position;
	public UnitSpawn(string profileId, Vector3 position) { ProfileId = profileId; Position = position; }
}

public class BattleSetup
{
	public List<Vector3> FriendlySpawns = new(); 
	public List<UnitSpawn> Enemies = new();
	public List<MidBattleEvent> MidBattleEvents = new();
}

public enum EventType { Dialogue, Battle, AddPartyMember, JumpToSection } // <-- NEW: Party Add Event

public class ScriptEvent
{
	public EventType Type;
	public string TimelinePath;
	public BattleSetup BattleData;
	public string ProfileId;
	public string TargetSection;

	public static ScriptEvent Dialogue(string path) => new ScriptEvent { Type = EventType.Dialogue, TimelinePath = path };
	public static ScriptEvent Battle(BattleSetup battle) => new ScriptEvent { Type = EventType.Battle, BattleData = battle };
	// Helper to write cleaner scripts:
	public static ScriptEvent AddPartyMember(string profileId) => new ScriptEvent { Type = EventType.AddPartyMember, ProfileId = profileId };
	// === NEW: Helper to instantly redirect the script! ===
	public static ScriptEvent JumpToSection(string target) => new ScriptEvent { Type = EventType.JumpToSection, TargetSection = target };
}

public static class GameScript
{
	public static Dictionary<string, List<ScriptEvent>> GetMainScript()
	{
		return new Dictionary<string, List<ScriptEvent>>
		{
			{
				"Intro", new List<ScriptEvent>
				{
					ScriptEvent.AddPartyMember("Knight"),
					ScriptEvent.AddPartyMember("Archer"),
					ScriptEvent.Battle(new BattleSetup 
					{
						FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
						Enemies = { new UnitSpawn("Goblin", new Vector3(4,0,4)) },
						MidBattleEvents = { new MidBattleEvent(2, "res://dialogic_timelines/TauntTurn1.dtl") }
					}),
					// The Dialogic choice here will fire a signal to interrupt the flow.
					ScriptEvent.Dialogue("res://dialogic_timelines/Intro.dtl") 
				}
			},
			{
				"Path_Goblins", new List<ScriptEvent>
				{
					ScriptEvent.Battle(new BattleSetup 
					{
						FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
						Enemies = { 
							new UnitSpawn("Goblin", new Vector3(4,0,4)), 
							new UnitSpawn("Goblin", new Vector3(6,0,4)), 
							new UnitSpawn("Goblin", new Vector3(6,0,6)) 
						}
					}),
					// === NEW: Converge back to the main story! ===
					ScriptEvent.JumpToSection("PostFight")
				}
			},
			{
				"Path_Ogre", new List<ScriptEvent>
				{
					ScriptEvent.Battle(new BattleSetup 
					{
						FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
						Enemies = { new UnitSpawn("Ogre", new Vector3(6,0,6)) }
					}),
					// === NEW: Converge back to the main story! ===
					ScriptEvent.JumpToSection("PostFight")
				}
			},
			{
				"PostFight", new List<ScriptEvent>
				{
					ScriptEvent.Dialogue("res://dialogic_timelines/PostFirstBattle.dtl") 
					// When this finishes, there are no more events and no JumpToSection,
					// so the game will safely display the "YOU WIN" GAME OVER text!
				}
			},
		};
	}
}

public struct MidBattleEvent
{
	public int Turn;
	public string TimelinePath;
	
	public MidBattleEvent(int turn, string path) 
	{ 
		Turn = turn; 
		TimelinePath = path; 
	}
}
