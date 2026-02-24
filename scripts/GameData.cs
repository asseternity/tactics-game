// GameData.cs
using Godot;
using System.Collections.Generic;

// === THE DATA LAYER ===

public struct UnitProfile
{
	public string Name;
	public string SpritePath;
	public int MaxHP;
	public int AttackDamage;
	public int AttackRange; 

	public UnitProfile(string name, string spritePath, int maxHp, int attackDmg, int attackRange)
	{
		Name = name; SpritePath = spritePath; MaxHP = maxHp; 
		AttackDamage = attackDmg; AttackRange = attackRange;
	}
}

// Our new Persistent Unit container!
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

	public PersistentUnit(UnitProfile profile)
	{
		Profile = profile;
		MaxHP = profile.MaxHP;
		CurrentHP = profile.MaxHP;
		AttackDamage = profile.AttackDamage;
		AttackRange = profile.AttackRange;
	}

	// A tiny heal between battles
	public void HealBetweenBattles()
	{
		// Heal for 30% of MaxHP
		CurrentHP += Mathf.RoundToInt(MaxHP * 0.3f);
		
		// Revive dead units with at least 1 HP so you don't lose them permanently in a linear script
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
	public List<Vector3> FriendlySpawns = new(); // Just positions now! The Party fills these slots.
	public List<UnitSpawn> Enemies = new();
}

public enum EventType { Dialogue, Battle }

public class ScriptEvent
{
	public EventType Type;
	public string TimelinePath;
	public BattleSetup BattleData;

	public static ScriptEvent Dialogue(string path) => new ScriptEvent { Type = EventType.Dialogue, TimelinePath = path };
	public static ScriptEvent Battle(BattleSetup battle) => new ScriptEvent { Type = EventType.Battle, BattleData = battle };
}

// === THE GAME SCRIPT ===
public static class GameScript
{
	public static List<ScriptEvent> GetMainScript()
	{
		return new List<ScriptEvent>
		{
			ScriptEvent.Dialogue("res://dialogic_timelines/Intro.dtl"),
			
			ScriptEvent.Battle(new BattleSetup 
			{
				// We just provide the spawn tiles for the party!
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
