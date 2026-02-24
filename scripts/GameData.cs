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
}

public enum EventType { Dialogue, Battle, AddPartyMember } // <-- NEW: Party Add Event

public class ScriptEvent
{
	public EventType Type;
	public string TimelinePath;
	public BattleSetup BattleData;
	public string ProfileId;

	public static ScriptEvent Dialogue(string path) => new ScriptEvent { Type = EventType.Dialogue, TimelinePath = path };
	public static ScriptEvent Battle(BattleSetup battle) => new ScriptEvent { Type = EventType.Battle, BattleData = battle };
	// Helper to write cleaner scripts:
	public static ScriptEvent AddPartyMember(string profileId) => new ScriptEvent { Type = EventType.AddPartyMember, ProfileId = profileId };
}

public static class GameScript
{
	public static List<ScriptEvent> GetMainScript()
	{
		return new List<ScriptEvent>
		{
			// Add our starting party via the script!
			ScriptEvent.AddPartyMember("Knight"),
			ScriptEvent.AddPartyMember("Archer"),

			ScriptEvent.Dialogue("res://dialogic_timelines/Intro.dtl"),
			
			ScriptEvent.Battle(new BattleSetup 
			{
				FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
				Enemies = { new UnitSpawn("Goblin", new Vector3(4,0,4)) }
			}),
			
			ScriptEvent.Dialogue("res://dialogic_timelines/PostFirstBattle.dtl"),
			
			ScriptEvent.Battle(new BattleSetup 
			{
				FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
				Enemies = { new UnitSpawn("Ogre", new Vector3(6,0,6)), new UnitSpawn("Goblin", new Vector3(4,0,4)) }
			})
		};
	}
}
