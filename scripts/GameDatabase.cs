using System.Collections.Generic;

public static class GameDatabase
{
	public static Dictionary<string, UnitProfile> Units { get; private set; } = new();
	public static Dictionary<string, Equipment> Items { get; private set; } = new();

	public static void Initialize()
	{
		Units.Clear();
		Items.Clear();

		// === UNITS  (name, sprite, hp, dmg, range, move, xp, facing, speed, suit) ===
		Units["Ambrose"] = new UnitProfile("Ambrose", "res://assets/HighRes3.png",
			25, 15, 1, 3, 0, UnitFacing.Right,  speed: 7,  cardSuit: CardSuit.Hearts);
		Units["Dougal"]  = new UnitProfile("Dougal",  "res://assets/HighRes5.png",
			18, 18, 1, 3, 0, UnitFacing.Right,  speed: 9,  cardSuit: CardSuit.Spades);
		Units["Guard"]   = new UnitProfile("Goblin",  "res://assets/HighRes4.png",
			10,  3, 1, 3, 140, UnitFacing.Right, speed: 4, cardSuit: CardSuit.Clubs);
		Units["Orc"]     = new UnitProfile("Ogre",    "res://assets/HR_ORC2.png",
			25,  8, 1, 2, 120, UnitFacing.Center, speed: 3, cardSuit: CardSuit.Diamonds);

		// === ARMOR ===
		Items["RustyMail"]          = new Equipment("RustyMail",          "Rusty Mail",          "res://icons/Rusty Mail.png",          EquipSlot.Armor,  bonusHp: 1);
		Items["AgilityCloak"]       = new Equipment("AgilityCloak",       "Agility Cloak",       "res://icons/Agility Cloak.png",       EquipSlot.Armor,  bonusHp: 1, bonusMov: 1);
		Items["FineAgilityCloak"]   = new Equipment("FineAgilityCloak",   "Fine Agility Cloak",  "res://icons/Fine Agility Cloak.png",  EquipSlot.Armor,  bonusHp: 2, bonusMov: 1);
		Items["GrandAgilityCloak"]  = new Equipment("GrandAgilityCloak",  "Grand Agility Cloak", "res://icons/Grand Agility Cloak.png", EquipSlot.Armor,  bonusHp: 3, bonusMov: 2);
		Items["HeavyLeatherArmor"]  = new Equipment("HeavyLeatherArmor",  "Heavy Leather Armor", "res://icons/Heavy Leather Armor.png", EquipSlot.Armor,  bonusHp: 4);
		Items["GoldenShellmail"]    = new Equipment("GoldenShellmail",    "Golden Shellmail",    "res://icons/Golden Shellmail.png",    EquipSlot.Armor,  bonusHp: 6);
		Items["BluePlatemail"]      = new Equipment("BluePlatemail",      "Blue Platemail",      "res://icons/Blue Platemail.png",      EquipSlot.Armor,  bonusHp: 8);
		Items["FineHelmet"]         = new Equipment("FineHelmet",         "Fine Helmet",         "res://icons/helmet.png",              EquipSlot.Armor,  bonusHp: 3);
		Items["SteelHelmet"]        = new Equipment("SteelHelmet",        "Steel Helmet",        "res://icons/Steel Helmet.png",        EquipSlot.Armor,  bonusHp: 4);
		Items["SuperbHelmet"]       = new Equipment("SuperbHelmet",       "Superb Helmet",       "res://icons/Superb Helmet.png",       EquipSlot.Armor,  bonusHp: 5);

		// === WEAPONS (TIER 1) ===
		Items["IronSword"]    = new Equipment("IronSword",    "Iron Sword",    "res://icons/sword.png",       EquipSlot.Weapon, bonusDmg: 1);
		Items["RustyKnife"]   = new Equipment("RustyKnife",   "Rusty Knife",   "res://icons/Rusty Knife.png", EquipSlot.Weapon, bonusDmg: 1);
		Items["RustyHatchet"] = new Equipment("RustyHatchet", "Rusty Hatchet", "res://icons/Rusty Hatchet.png", EquipSlot.Weapon, bonusDmg: 1);
		Items["QuickKnife"]   = new Equipment("QuickKnife",   "Quick Knife",   "res://icons/Quick Knife.png", EquipSlot.Weapon, bonusDmg: 1, bonusMov: 1);

		// === WEAPONS (TIER 2) ===
		Items["DuelingRapier"] = new Equipment("DuelingRapier", "Dueling Rapier", "res://icons/Dueling Rapier.png", EquipSlot.Weapon, bonusDmg: 2, bonusMov: 1);
		Items["Emberknife"]    = new Equipment("Emberknife",    "Emberknife",     "res://icons/Emberknife.png",     EquipSlot.Weapon, bonusDmg: 2);
		Items["Hooksword"]     = new Equipment("Hooksword",     "Hooksword",      "res://icons/Hooksword.png",      EquipSlot.Weapon, bonusDmg: 2);
		Items["RazorBlade"]    = new Equipment("RazorBlade",    "Razor Blade",    "res://icons/Razor Blade.png",    EquipSlot.Weapon, bonusDmg: 2);
		Items["Scimitar"]      = new Equipment("Scimitar",      "Scimitar",       "res://icons/Scimitar.png",       EquipSlot.Weapon, bonusDmg: 2);
		Items["WandOfAgility"] = new Equipment("WandOfAgility", "Wand of Agility","res://icons/Wand of Agility.png",EquipSlot.Weapon, bonusDmg: 1, bonusMov: 2);

		// === WEAPONS (TIER 3) ===
		Items["CaptainsSabre"] = new Equipment("CaptainsSabre", "Captain's Sabre", "res://icons/Captain's Sabre.png", EquipSlot.Weapon, bonusDmg: 3);
		Items["DualSwords"]    = new Equipment("DualSwords",    "Dual Swords",     "res://icons/Dual Swords.png",     EquipSlot.Weapon, bonusDmg: 3);
		Items["EbonyRazor"]    = new Equipment("EbonyRazor",    "Ebony Razor",     "res://icons/Ebony Razor.png",     EquipSlot.Weapon, bonusDmg: 3);
		Items["Hammer"]        = new Equipment("Hammer",        "Hammer",          "res://icons/Hammer.png",          EquipSlot.Weapon, bonusDmg: 3);
		Items["KingsKnife"]    = new Equipment("KingsKnife",    "King's Knife",    "res://icons/King's Knife.png",    EquipSlot.Weapon, bonusDmg: 3);
		Items["Yatagan"]       = new Equipment("Yatagan",       "Yatagan",         "res://icons/Yatagan.png",         EquipSlot.Weapon, bonusDmg: 3);
		Items["DaggerOfSpeed"] = new Equipment("DaggerOfSpeed", "Dagger of Speed", "res://icons/Dagger of Speed.png", EquipSlot.Weapon, bonusDmg: 2, bonusMov: 2);

		// === WEAPONS (TIER 4) ===
		Items["Bloodaxe"]           = new Equipment("Bloodaxe",           "Bloodaxe",            "res://icons/Bloodaxe.png",            EquipSlot.Weapon, bonusHp: 2, bonusDmg: 5);
		Items["BlueLightningSword"] = new Equipment("BlueLightningSword", "Blue Lightning Sword", "res://icons/Blue Lightning Sword.png",EquipSlot.Weapon, bonusDmg: 4, bonusMov: 1);
		Items["BurningEdge"]        = new Equipment("BurningEdge",        "Burning Edge",         "res://icons/Burning Edge.png",        EquipSlot.Weapon, bonusDmg: 4);
		Items["DualScimitars"]      = new Equipment("DualScimitars",      "Dual Scimitars",       "res://icons/Dual Scimitars.png",      EquipSlot.Weapon, bonusDmg: 4);
		Items["GoldenFlail"]        = new Equipment("GoldenFlail",        "Golden Flail",         "res://icons/Golden Flail.png",        EquipSlot.Weapon, bonusDmg: 4);
		Items["MorningStar"]        = new Equipment("MorningStar",        "Morning Star",         "res://icons/Morning Star.png",        EquipSlot.Weapon, bonusDmg: 4);
		Items["Trident"]            = new Equipment("Trident",            "Trident",              "res://icons/Trident.png",             EquipSlot.Weapon, bonusDmg: 4);
		Items["GoldenScythe"]       = new Equipment("GoldenScythe",       "Golden Scythe",        "res://icons/Golden Scythe.png",       EquipSlot.Weapon, bonusDmg: 5);

		// === WEAPONS (TIER 5) ===
		Items["AssassinsDeathknife"] = new Equipment("AssassinsDeathknife", "Assassin's Deathknife","res://icons/Assassin's Deathknife.png", EquipSlot.Weapon, bonusDmg: 6, bonusMov: 1);
		Items["SapphireGreatsword"]  = new Equipment("SapphireGreatsword",  "Sapphire Greatsword",  "res://icons/Sapphire Greatsword.png",  EquipSlot.Weapon, bonusDmg: 7);
		Items["VillainsDeathsword"]  = new Equipment("VillainsDeathsword",  "Villain's Deathsword", "res://icons/Villain's Deathsword.png", EquipSlot.Weapon, bonusDmg: 7);
		Items["HeroicMastersword"]   = new Equipment("HeroicMastersword",   "Heroic Mastersword",   "res://icons/Heroic Mastersword.png",   EquipSlot.Weapon, bonusDmg: 8);

		// =========================================================
		// === JOKER ITEMS (new) ===
		// =========================================================

		// Pair Jokers
		Items["LoversBracelet"] = new Equipment("LoversBracelet", "Lover's Bracelet",
			"res://icons/Rusty Mail.png",  // placeholder icon path
			EquipSlot.Armor, bonusHp: 2,
			jokerEffects: JokerEffect.PairsCountDouble);

		// Straight Jokers
		Items["WanderersBoots"] = new Equipment("WanderersBoots", "Wanderer's Boots",
			"res://icons/Agility Cloak.png",
			EquipSlot.Armor, bonusMov: 1,
			jokerEffects: JokerEffect.StraightAt3);

		// Flush Jokers
		Items["ClanCrest"] = new Equipment("ClanCrest", "Clan Crest",
			"res://icons/helmet.png",
			EquipSlot.Armor, bonusHp: 1,
			jokerEffects: JokerEffect.FlushAt3);

		// Face Card Jokers
		Items["NobilityRing"] = new Equipment("NobilityRing", "Nobility Ring",
			"res://icons/sword.png",
			EquipSlot.Weapon, bonusDmg: 1,
			jokerEffects: JokerEffect.FaceCardsBonus);

		// Tier Multiplier Joker
		Items["AncientMedallion"] = new Equipment("AncientMedallion", "Ancient Medallion",
			"res://icons/Golden Shellmail.png",
			EquipSlot.Armor, bonusHp: 3,
			jokerEffects: JokerEffect.TierMultiplier);

		// Ace Wild Joker
		Items["JokersCard"] = new Equipment("JokersCard", "Joker's Card",
			"res://icons/Scimitar.png",
			EquipSlot.Weapon, bonusDmg: 2,
			jokerEffects: JokerEffect.AcesWild);

		// Combined Joker (rare!)
		Items["FoolsGold"] = new Equipment("FoolsGold", "Fool's Gold",
			"res://icons/Golden Scythe.png",
			EquipSlot.Weapon, bonusDmg: 3, bonusMov: 1,
			jokerEffects: JokerEffect.PairsCountDouble | JokerEffect.FaceCardsBonus);
	}
}
