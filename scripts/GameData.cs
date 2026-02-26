// GameData.cs
using Godot;
using System.Collections.Generic;

// === NEW: Facing Enum ===
public enum UnitFacing { Left, Right, Center }

public struct UnitProfile
{
	public string Name;
	public string SpritePath;
	public int MaxHP;
	public int AttackDamage;
	public int AttackRange; 
	public int Movement;
	public int XPReward;
	
	// === NEW: Default Facing ===
	public UnitFacing DefaultFacing;

	// Note the new parameter at the end!
	public UnitProfile(string name, string spritePath, int maxHp, int attackDmg, int attackRange, int movement, int xpReward, UnitFacing defaultFacing = UnitFacing.Center)
	{
		Name = name; SpritePath = spritePath; MaxHP = maxHp; 
		AttackDamage = attackDmg; AttackRange = attackRange; Movement = movement; XPReward = xpReward;
		DefaultFacing = defaultFacing;
	}
}

public class PersistentUnit
{
	public UnitProfile Profile;
	public int Level = 1;
	public int CurrentXP = 0;
	public int MaxXP = 100;
	
	// Base Stats
	public int MaxHP;
	public int CurrentHP;
	public int AttackDamage;
	public int AttackRange;
	public int Movement;
	public int XPReward; 

	public bool IsPlayerCharacter = false;
	public Dictionary<string, int> Relationships = new();

	// === NEW: Equipment Slots & Dynamic Stat Getters ===
	public Equipment EquippedWeapon;
	public Equipment EquippedArmor;

	public int GetTotalMaxHP() => MaxHP + (EquippedWeapon?.BonusMaxHP ?? 0) + (EquippedArmor?.BonusMaxHP ?? 0);
	public int GetTotalDamage() => AttackDamage + (EquippedWeapon?.BonusDamage ?? 0) + (EquippedArmor?.BonusDamage ?? 0);
	public int GetTotalMovement() => Movement + (EquippedWeapon?.BonusMovement ?? 0) + (EquippedArmor?.BonusMovement ?? 0);

	public PersistentUnit(UnitProfile profile, bool isPlayer = false)
	{
		Profile = profile;
		MaxHP = profile.MaxHP;
		CurrentHP = profile.MaxHP;
		AttackDamage = profile.AttackDamage;
		AttackRange = profile.AttackRange;
		Movement = profile.Movement;
		XPReward = profile.XPReward;
		IsPlayerCharacter = isPlayer;

		if (!IsPlayerCharacter)
		{
			Relationships.Add("Respect", 50);
			Relationships.Add("Agreement", 50);
			Relationships.Add("Fear", 10);
		}
	}

	public void HealBetweenBattles()
	{
		CurrentHP += Mathf.RoundToInt(GetTotalMaxHP() * 0.3f);
		if (CurrentHP <= 0) CurrentHP = 1; 
		if (CurrentHP > GetTotalMaxHP()) CurrentHP = GetTotalMaxHP();
	}
}

public struct UnitSpawn
{
	public string ProfileId;
	public Vector3 Position;
	public UnitSpawn(string profileId, Vector3 position) { ProfileId = profileId; Position = position; }
}

// === NEW: Environment Enums ===
public enum GroundType { Grass, Dirt, Marble }
public enum LightingMood { Noon, Morning, Night, Indoors }

public class BattleSetup
{
	public List<Vector3> FriendlySpawns = new(); 
	public List<UnitSpawn> Enemies = new();
	public List<MidBattleEvent> MidBattleEvents = new();
	
	public GroundType Ground = GroundType.Grass;
	public LightingMood Light = LightingMood.Noon;
	
	// === NEW: Tactical Elevation! ===
	public bool ElevationEnabled = false;
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
					ScriptEvent.AddPartyMember("Ambrose"),
					ScriptEvent.AddPartyMember("Dougal"),
					ScriptEvent.Battle(new BattleSetup 
					{
						FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
						Enemies = { new UnitSpawn("Guard", new Vector3(4,0,4)) },
						MidBattleEvents = { new MidBattleEvent(2, "res://dialogic_timelines/TauntTurn1.dtl") },
						Ground = GroundType.Dirt,
						Light = LightingMood.Morning,
						ElevationEnabled = true
					}),
					// The Dialogic choice here will fire a signal to interrupt the flow.
					ScriptEvent.Dialogue("res://dialogic_timelines/Intro.dtl") 
				}
			},
			{
				"Path_Guards", new List<ScriptEvent>
				{
					ScriptEvent.Battle(new BattleSetup 
					{
						FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
						Enemies = { 
							new UnitSpawn("Guard", new Vector3(4,0,4)), 
							new UnitSpawn("Guard", new Vector3(6,0,4)), 
							new UnitSpawn("Guard", new Vector3(6,0,6))
						}, // <--- THE FIX: This bracket closes the Enemies list!
						Ground = GroundType.Grass,
						Light = LightingMood.Night,
						ElevationEnabled = true
					}),
					// === NEW: Converge back to the main story! ===
					ScriptEvent.JumpToSection("PostFight")
				}
			},
			{
				"Path_Orc", new List<ScriptEvent>
				{
					ScriptEvent.Battle(new BattleSetup 
					{
						FriendlySpawns = { new Vector3(0,0,0), new Vector3(2,0,0) },
						Enemies = { new UnitSpawn("Orc", new Vector3(6,0,6)) },
						Ground = GroundType.Dirt,
						Light = LightingMood.Night,
						ElevationEnabled = true
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

// === NEW: Clean Item & Equipment Architecture ===
public enum EquipSlot { Weapon, Armor }

public class GameItem
{
	public string Id;
	public string Name;
	public string IconPath;
	public string Description;
}

public class Equipment : GameItem
{
	public EquipSlot Slot;
	public int BonusMaxHP;
	public int BonusDamage;
	public int BonusMovement;
	
	public Equipment(string id, string name, string iconPath, EquipSlot slot, int bonusHp = 0, int bonusDmg = 0, int bonusMov = 0)
	{
		Id = id; Name = name; IconPath = iconPath; Slot = slot;
		BonusMaxHP = bonusHp; BonusDamage = bonusDmg; BonusMovement = bonusMov;
		
		// Generate the description dynamically for the tooltips!
		List<string> stats = new();
		if (BonusDamage > 0) stats.Add($"+{BonusDamage} DMG");
		if (BonusMaxHP > 0) stats.Add($"+{BonusMaxHP} HP");
		if (BonusMovement > 0) stats.Add($"+{BonusMovement} Move");
		Description = string.Join(" | ", stats);
	}

	// === NEW: Safely clone the archetype for the inventory ===
	public Equipment Clone() => (Equipment)this.MemberwiseClone();
}
