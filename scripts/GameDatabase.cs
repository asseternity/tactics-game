using System.Collections.Generic;

public static class GameDatabase
{
	public static Dictionary<string, UnitProfile> Units { get; private set; } = new();
	public static Dictionary<string, Equipment> Items { get; private set; } = new();

	public static void Initialize()
	{
		Units.Clear();
		Items.Clear();

		// === UNITS ===
		Units["Ambrose"] = new UnitProfile("Ambrose", "res://assets/HighRes3.png", 25, 15, 1, 3, 0, UnitFacing.Right);
		Units["Dougal"] = new UnitProfile("Dougal", "res://assets/HighRes5.png", 18, 18, 1, 3, 0, UnitFacing.Right);
		Units["Guard"] = new UnitProfile("Goblin", "res://assets/HighRes4.png", 10, 3, 1, 3, 140, UnitFacing.Right);
		Units["Orc"]   = new UnitProfile("Ogre", "res://assets/HR_ORC2.png", 25, 8, 1, 2, 120, UnitFacing.Center);

		// === EQUIPMENT ===
		Items["IronSword"] = new Equipment("IronSword", "Iron Sword", "res://icons/sword.png", EquipSlot.Weapon, bonusDmg: 1);
		Items["FineHelmet"] = new Equipment("FineHelmet", "Fine Helmet", "res://icons/helmet.png", EquipSlot.Armor, bonusHp: 3);
	}
}
