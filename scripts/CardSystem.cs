// CardSystem.cs
// Core poker hand evaluation, flanking detection, and combo resolution.
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// ENUMS
// ============================================================

public enum CardSuit { Hearts, Diamonds, Clubs, Spades }

/// <summary>
/// Card rank encoding. None = no relationship yet.
/// Tier prefix: 0=Base, 1=Bronze, 2=Silver, 3=Gold, 4=Platinum
/// </summary>
public enum CardRank
{
	None = 0,
	// Base tier
	Two = 2, Three = 3, Four = 4, Five = 5,
	Jack = 11, Queen = 12, King = 13, Ace = 14,
	// Bronze tier (same face values, higher tier prefix)
	BronzeTwo = 102, BronzeThree = 103, BronzeFour = 104, BronzeFive = 105,
	BronzeJack = 111, BronzeQueen = 112, BronzeKing = 113, BronzeAce = 114,
	// Silver tier
	SilverTwo = 202, SilverThree = 203, SilverFour = 204, SilverFive = 205,
	SilverJack = 211, SilverQueen = 212, SilverKing = 213, SilverAce = 214,
	// Gold tier
	GoldTwo = 302, GoldThree = 303, GoldFour = 304, GoldFive = 305,
	GoldJack = 311, GoldQueen = 312, GoldKing = 313, GoldAce = 314,
}

/// <summary>Extracts the base face value (2-14) from a CardRank for hand comparison.</summary>
public static class CardRankExtensions
{
	public static int FaceValue(this CardRank rank) => (int)rank % 100;
	public static int Tier(this CardRank rank) => (int)rank / 100;

	public static string DisplayName(this CardRank rank)
	{
		int face = rank.FaceValue();
		int tier = rank.Tier();
		string prefix = tier switch { 1 => "Bronze ", 2 => "Silver ", 3 => "Gold ", 4 => "Platinum ", _ => "" };
		string faceName = face switch
		{
			11 => "Jack", 12 => "Queen", 13 => "King", 14 => "Ace",
			_ => face.ToString()
		};
		return prefix + faceName;
	}

	/// <summary>Advance one step up the rank ladder.</summary>
	public static CardRank NextRank(this CardRank current)
	{
		int[] faceOrder = { 2, 3, 4, 5, 11, 12, 13, 14 };
		int tier = current.Tier();
		int face = current.FaceValue();
		int idx = Array.IndexOf(faceOrder, face);

		if (idx < 0) return CardRank.Two; // None → Two

		if (idx + 1 < faceOrder.Length)
		{
			// Same tier, next face
			return (CardRank)(tier * 100 + faceOrder[idx + 1]);
		}
		else
		{
			// Wrap to next tier at Two
			return (CardRank)((tier + 1) * 100 + 2);
		}
	}
}

// ============================================================
// HAND TYPES
// ============================================================

public enum PokerHand
{
	HighCard = 0,
	OnePair = 1,
	TwoPair = 2,
	ThreeOfAKind = 3,
	Straight = 4,
	Flush = 5,
	FullHouse = 6,
	FourOfAKind = 7,
	StraightFlush = 8,
	RoyalFlush = 9
}

public class HandResult
{
	public PokerHand Hand;
	public float DamageMultiplier;   // Applied to EACH unit in the combo
	public Color ComboColor;
	public string DisplayName;
	public List<CardEntry> ContributingCards; // The actual 5 cards used
	public List<Unit> ContributingUnits;      // Which units are in the combo

	public static HandResult None() => new HandResult
	{
		Hand = PokerHand.HighCard,
		DamageMultiplier = 1.0f,
		ComboColor = new Color(0.7f, 0.7f, 0.7f),
		DisplayName = "No Combo",
		ContributingCards = new List<CardEntry>(),
		ContributingUnits = new List<Unit>()
	};
}

public class CardEntry
{
	public Unit Owner;
	public CardRank Rank;
	public CardSuit Suit;
	public int FaceValue => Rank.FaceValue();
}

// ============================================================
// JOKER EFFECT FLAGS (on Equipment)
// ============================================================

[Flags]
public enum JokerEffect
{
	None             = 0,
	PairsCountDouble = 1 << 0,   // Pair damage bonus is doubled
	StraightAt3      = 1 << 1,   // Straights valid with only 3 cards
	FlushAt3         = 1 << 2,   // Flushes valid with only 3 of same suit
	FaceCardsBonus   = 1 << 3,   // J/Q/K/A count as +1 face value in straights
	TierMultiplier   = 1 << 4,   // Each tier above 0 adds +10% damage
	AcesWild         = 1 << 5,   // Aces count as any suit
}

// ============================================================
// HAND EVALUATOR
// ============================================================

public static class PokerHandEvaluator
{
	// --- Multipliers per hand type (base, before joker modifications) ---
	private static readonly Dictionary<PokerHand, float> BaseMultipliers = new()
	{
		{ PokerHand.HighCard,      1.00f },
		{ PokerHand.OnePair,       1.25f },
		{ PokerHand.TwoPair,       1.50f },
		{ PokerHand.ThreeOfAKind,  1.75f },
		{ PokerHand.Straight,      2.00f },
		{ PokerHand.Flush,         2.25f },
		{ PokerHand.FullHouse,     2.50f },
		{ PokerHand.FourOfAKind,   3.00f },
		{ PokerHand.StraightFlush, 3.50f },
		{ PokerHand.RoyalFlush,    4.00f },
	};

	private static readonly Dictionary<PokerHand, Color> HandColors = new()
	{
		{ PokerHand.HighCard,      new Color(0.6f, 0.6f, 0.6f) },
		{ PokerHand.OnePair,       new Color(0.4f, 0.8f, 0.4f) },
		{ PokerHand.TwoPair,       new Color(0.3f, 0.9f, 0.7f) },
		{ PokerHand.ThreeOfAKind,  new Color(0.3f, 0.6f, 1.0f) },
		{ PokerHand.Straight,      new Color(0.8f, 0.5f, 1.0f) },
		{ PokerHand.Flush,         new Color(1.0f, 0.5f, 0.2f) },
		{ PokerHand.FullHouse,     new Color(1.0f, 0.7f, 0.1f) },
		{ PokerHand.FourOfAKind,   new Color(1.0f, 0.3f, 0.3f) },
		{ PokerHand.StraightFlush, new Color(1.0f, 0.1f, 0.8f) },
		{ PokerHand.RoyalFlush,    new Color(1.0f, 0.9f, 0.0f) },
	};

	private static readonly Dictionary<PokerHand, string> HandNames = new()
	{
		{ PokerHand.HighCard,      "High Card" },
		{ PokerHand.OnePair,       "One Pair!" },
		{ PokerHand.TwoPair,       "Two Pair!" },
		{ PokerHand.ThreeOfAKind,  "Three of a Kind!" },
		{ PokerHand.Straight,      "Straight!!" },
		{ PokerHand.Flush,         "Flush!!" },
		{ PokerHand.FullHouse,     "Full House!!" },
		{ PokerHand.FourOfAKind,   "Four of a Kind!!!" },
		{ PokerHand.StraightFlush, "Straight Flush!!!!" },
		{ PokerHand.RoyalFlush,    "ROYAL FLUSH!!!!!" },
	};

	/// <summary>
	/// Given a list of CardEntries (1-N units), find the best 5-card hand 
	/// and return a HandResult with multiplier, color, and contributing units.
	/// Joker effects come from all active party equipment.
	/// </summary>
	public static HandResult Evaluate(List<CardEntry> cards, JokerEffect jokers = JokerEffect.None)
	{
		if (cards == null || cards.Count == 0) return HandResult.None();

		// Cap at 5 cards, pick best combo
		List<CardEntry> best = FindBestHand(cards, jokers);
		PokerHand hand = ClassifyHand(best, jokers);

		float multiplier = BaseMultipliers[hand];

		// --- Joker: TierMultiplier ---
		if (jokers.HasFlag(JokerEffect.TierMultiplier))
		{
			float tierBonus = best.Sum(c => c.Rank.Tier()) * 0.10f;
			multiplier += tierBonus;
		}

		// --- Joker: PairsCountDouble ---
		if (jokers.HasFlag(JokerEffect.PairsCountDouble) && (hand == PokerHand.OnePair || hand == PokerHand.TwoPair))
		{
			multiplier = 1.0f + (multiplier - 1.0f) * 2f;
		}

		return new HandResult
		{
			Hand = hand,
			DamageMultiplier = multiplier,
			ComboColor = HandColors[hand],
			DisplayName = HandNames[hand],
			ContributingCards = best,
			ContributingUnits = best.Select(c => c.Owner).Distinct().ToList()
		};
	}

	// --------------------------------------------------------
	// PRIVATE HELPERS
	// --------------------------------------------------------

	private static List<CardEntry> FindBestHand(List<CardEntry> cards, JokerEffect jokers)
	{
		if (cards.Count <= 5) return new List<CardEntry>(cards);

		// Try all C(n,5) combinations and pick the highest scoring hand
		List<CardEntry> best = null;
		PokerHand bestRank = PokerHand.HighCard;

		foreach (var combo in Combinations(cards, 5))
		{
			PokerHand rank = ClassifyHand(combo, jokers);
			if (best == null || rank > bestRank)
			{
				best = combo;
				bestRank = rank;
			}
		}
		return best ?? cards.Take(5).ToList();
	}

	private static PokerHand ClassifyHand(List<CardEntry> cards, JokerEffect jokers)
	{
		bool acesWild = jokers.HasFlag(JokerEffect.AcesWild);

		var suits = cards.Select(c =>
			(acesWild && c.FaceValue == 14) ? (CardSuit?)null : (CardSuit?)c.Suit
		).ToList();

		var values = cards.Select(c => c.FaceValue).OrderBy(v => v).ToList();
		var groups = values.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();

		int minFlushCards = jokers.HasFlag(JokerEffect.FlushAt3) ? 3 : 5;
		int minStraightCards = jokers.HasFlag(JokerEffect.StraightAt3) ? 3 : 5;

		// Check flush (all same suit, ignoring aces-wild wildcards)
		bool isFlush = cards.Count >= minFlushCards &&
			suits.Where(s => s.HasValue).GroupBy(s => s.Value).Any(g => g.Count() >= minFlushCards);

		// Check straight
		bool isStraight = IsStraight(values, minStraightCards, jokers.HasFlag(JokerEffect.FaceCardsBonus));

		if (isFlush && isStraight)
		{
			bool isRoyal = values.Contains(14) && values.Contains(13) && values.Contains(12)
						   && values.Contains(11) && values.Contains(10);
			return isRoyal ? PokerHand.RoyalFlush : PokerHand.StraightFlush;
		}

		int topGroupCount = groups[0].Count();
		int secondGroupCount = groups.Count > 1 ? groups[1].Count() : 0;

		if (topGroupCount == 4) return PokerHand.FourOfAKind;
		if (topGroupCount == 3 && secondGroupCount == 2) return PokerHand.FullHouse;
		if (isFlush) return PokerHand.Flush;
		if (isStraight) return PokerHand.Straight;
		if (topGroupCount == 3) return PokerHand.ThreeOfAKind;
		if (topGroupCount == 2 && secondGroupCount == 2) return PokerHand.TwoPair;
		if (topGroupCount == 2) return PokerHand.OnePair;
		return PokerHand.HighCard;
	}

	private static bool IsStraight(List<int> sortedValues, int minCards, bool faceBonus)
	{
		if (sortedValues.Count < minCards) return false;
		var distinct = sortedValues.Distinct().OrderBy(v => v).ToList();
		if (distinct.Count < minCards) return false;

		int streak = 1, maxStreak = 1;
		for (int i = 1; i < distinct.Count; i++)
		{
			int diff = distinct[i] - distinct[i - 1];
			// FaceBonus: J(11) Q(12) K(13) A(14) are consecutive with no bonus needed,
			// but also allow 10→J gap to count as 1 when FaceBonus is on
			if (diff == 1 || (faceBonus && diff == 1)) streak++;
			else streak = 1;
			maxStreak = Math.Max(maxStreak, streak);
		}
		return maxStreak >= minCards;
	}

	private static IEnumerable<List<T>> Combinations<T>(List<T> list, int k)
	{
		if (k == 0) { yield return new List<T>(); yield break; }
		for (int i = 0; i < list.Count; i++)
			foreach (var rest in Combinations(list.Skip(i + 1).ToList(), k - 1))
			{
				var combo = new List<T> { list[i] };
				combo.AddRange(rest);
				yield return combo;
			}
	}
}

// ============================================================
// FLANKING DETECTOR
// ============================================================

public static class FlankingDetector
{
	private const float TileSize = 2f; // Must match GameManager.TileSize

	/// <summary>
	/// Returns all (ally, flankedEnemy) pairs where the ally flanks the given enemy
	/// from the attacker's perspective. Flanking = attacker and ally are on OPPOSITE
	/// sides of the enemy within AttackRange of both.
	/// </summary>
	public static List<(Unit ally, Unit flankedEnemy)> GetFlankingPairs(
		Unit attacker,
		Unit targetEnemy,
		List<Unit> allUnits)
	{
		var result = new List<(Unit, Unit)>();

		// All living enemies on the board
		var enemies = allUnits.Where(u => !u.IsFriendly && u != targetEnemy && IsInstanceValid(u)).ToList();
		// All other friendly units
		var allies = allUnits.Where(u => u.IsFriendly && u != attacker && IsInstanceValid(u)).ToList();

		// We check flanking for the target enemy AND nearby enemies (chain)
		var enemiesToCheck = new List<Unit> { targetEnemy };
		enemiesToCheck.AddRange(enemies.Where(e =>
			GridDist(e.GlobalPosition, targetEnemy.GlobalPosition) <= 2));

		foreach (var enemy in enemiesToCheck)
		{
			foreach (var ally in allies)
			{
				if (IsFlanking(attacker, ally, enemy))
					result.Add((ally, enemy));
			}
		}

		return result;
	}

	/// <summary>
	/// An ally flanks an enemy if:
	///  - The enemy lies roughly between the attacker and the ally
	///  - Both the attacker and ally are within attack range of the enemy
	/// </summary>
	private static bool IsFlanking(Unit attacker, Unit ally, Unit enemy)
	{
		Vector3 aPos = attacker.GlobalPosition;
		Vector3 allyPos = ally.GlobalPosition;
		Vector3 ePos = enemy.GlobalPosition;

		// Direction from attacker to enemy
		Vector3 toEnemy = (ePos - aPos);
		// Direction from ally to enemy
		Vector3 allyToEnemy = (ePos - allyPos);

		// They flank if the dot product of their "to-enemy" vectors is negative
		// (pointing toward each other through the enemy)
		float dot = toEnemy.Normalized().Dot(allyToEnemy.Normalized());
		if (dot > -0.3f) return false; // Not on opposite sides

		// Both must be able to reach the enemy
		bool attackerInRange = GridDist(aPos, ePos) <= attacker.Data.AttackRange;
		bool allyInRange = GridDist(allyPos, ePos) <= ally.Data.AttackRange;

		return attackerInRange && allyInRange;
	}

	public static int GridDist(Vector3 a, Vector3 b)
	{
		int ax = Mathf.RoundToInt(a.X / TileSize), az = Mathf.RoundToInt(a.Z / TileSize);
		int bx = Mathf.RoundToInt(b.X / TileSize), bz = Mathf.RoundToInt(b.Z / TileSize);
		return Mathf.Max(Mathf.Abs(ax - bx), Mathf.Abs(az - bz));
	}

	private static bool IsInstanceValid(Unit u) => GodotObject.IsInstanceValid(u);
}

// ============================================================
// COMBO RESOLVER  (called before each attack)
// ============================================================

public class ComboResult
{
	public HandResult HandResult;
	/// <summary>Map of enemy → list of (ally, multiplier) that will hit it this attack</summary>
	public Dictionary<Unit, List<(Unit attacker, float multiplier)>> AttackMap = new();

	public bool HasCombo => HandResult != null && HandResult.Hand > PokerHand.HighCard;
}

public static class ComboResolver
{
	/// <summary>
	/// Given the attacking unit and all units on the board, resolve the full combo:
	///  - find all flanking pairs
	///  - build card list from attacker + allies
	///  - evaluate hand
	///  - build the AttackMap
	/// Returns a ComboResult with everything needed for damage + visuals.
	/// </summary>
	public static ComboResult Resolve(
		Unit attacker,
		Unit primaryTarget,
		List<Unit> allUnits,
		List<GameItem> inventory)
	{
		// 1. Gather joker effects from equipped items
		JokerEffect jokers = JokerEffect.None;
		foreach (var item in inventory)
		{
			if (item is Equipment eq && eq.JokerEffects != JokerEffect.None)
				jokers |= eq.JokerEffects;
		}

		// Also check attacker's own equipment
		if (attacker.Data.EquippedWeapon != null) jokers |= attacker.Data.EquippedWeapon.JokerEffects;
		if (attacker.Data.EquippedArmor != null) jokers |= attacker.Data.EquippedArmor.JokerEffects;

		// 2. Get flanking pairs
		var flankingPairs = FlankingDetector.GetFlankingPairs(attacker, primaryTarget, allUnits);

		// 3. Build card pool: attacker + unique flanking allies (cap 5 total)
		var cardPool = new List<CardEntry>();
		var unitsInCombo = new HashSet<Unit> { attacker };

		// Attacker's card
		if (attacker.Data.CardRank != CardRank.None)
			cardPool.Add(new CardEntry { Owner = attacker, Rank = attacker.Data.CardRank, Suit = attacker.Data.CardSuit });

		foreach (var (ally, _) in flankingPairs)
		{
			if (unitsInCombo.Contains(ally)) continue;
			if (cardPool.Count >= 5) break;
			unitsInCombo.Add(ally);
			if (ally.Data.CardRank != CardRank.None)
				cardPool.Add(new CardEntry { Owner = ally, Rank = ally.Data.CardRank, Suit = ally.Data.CardSuit });
		}

		// 4. Evaluate hand
		HandResult handResult = cardPool.Count > 0
			? PokerHandEvaluator.Evaluate(cardPool, jokers)
			: HandResult.None();

		// 5. Build attack map
		var comboResult = new ComboResult { HandResult = handResult };

		// Attacker always hits primary target
		comboResult.AttackMap[primaryTarget] = new List<(Unit, float)>
		{
			(attacker, handResult.DamageMultiplier)
		};

		// Each flanking ally hits their flanked enemy
		foreach (var (ally, flankedEnemy) in flankingPairs)
		{
			if (!handResult.ContributingUnits.Contains(ally)) continue;

			if (!comboResult.AttackMap.ContainsKey(flankedEnemy))
				comboResult.AttackMap[flankedEnemy] = new List<(Unit, float)>();

			// Avoid duplicate entries
			bool alreadyAdded = comboResult.AttackMap[flankedEnemy].Any(x => x.Item1 == ally);
			if (!alreadyAdded)
				comboResult.AttackMap[flankedEnemy].Add((ally, handResult.DamageMultiplier));
		}

		return comboResult;
	}
}
