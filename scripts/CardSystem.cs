// CardSystem.cs
// === KEY CHANGE: Attacker's card is NO LONGER in the poker hand.
// The combo represents the RELATIONSHIPS the attacker has forged with allies.
// Only flanking allies contribute cards. The attacker enables the combo but isn't a card.
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public enum CardSuit { Hearts, Diamonds, Clubs, Spades }

public enum CardRank
{
	None = 0,
	Two = 2, Three = 3, Four = 4, Five = 5,
	Jack = 11, Queen = 12, King = 13, Ace = 14,
	BronzeTwo = 102, BronzeThree = 103, BronzeFour = 104, BronzeFive = 105,
	BronzeJack = 111, BronzeQueen = 112, BronzeKing = 113, BronzeAce = 114,
	SilverTwo = 202, SilverThree = 203, SilverFour = 204, SilverFive = 205,
	SilverJack = 211, SilverQueen = 212, SilverKing = 213, SilverAce = 214,
	GoldTwo = 302, GoldThree = 303, GoldFour = 304, GoldFive = 305,
	GoldJack = 311, GoldQueen = 312, GoldKing = 313, GoldAce = 314,
}

public static class CardRankExtensions
{
	public static int FaceValue(this CardRank rank) => (int)rank % 100;
	public static int Tier(this CardRank rank) => (int)rank / 100;
	public static string DisplayName(this CardRank rank)
	{
		int face = rank.FaceValue(); int tier = rank.Tier();
		string prefix = tier switch { 1 => "Bronze ", 2 => "Silver ", 3 => "Gold ", 4 => "Platinum ", _ => "" };
		string faceName = face switch { 11 => "Jack", 12 => "Queen", 13 => "King", 14 => "Ace", _ => face.ToString() };
		return prefix + faceName;
	}
	public static CardRank NextRank(this CardRank current)
	{
		int[] order = { 2, 3, 4, 5, 11, 12, 13, 14 };
		int tier = current.Tier(); int face = current.FaceValue();
		int idx = Array.IndexOf(order, face);
		if (idx < 0) return CardRank.Two;
		return idx + 1 < order.Length ? (CardRank)(tier * 100 + order[idx + 1]) : (CardRank)((tier + 1) * 100 + 2);
	}
}

// ============================================================
// CARD IMAGE HELPER
// ============================================================

public static class CardImageHelper
{
	private static readonly Dictionary<CardSuit, string> SuitFolders = new()
	{ { CardSuit.Hearts, "Hearts" }, { CardSuit.Diamonds, "Diamonds" }, { CardSuit.Clubs, "Clubs" }, { CardSuit.Spades, "Spades" } };

	public static string GetCardImagePath(CardSuit suit, CardRank rank)
	{
		if (rank == CardRank.None) return null;
		string s = SuitFolders.GetValueOrDefault(suit, "Spades");
		int n = rank.FaceValue() == 14 ? 1 : rank.FaceValue();
		return $"res://cards/{s}/{s}_card_{n:D2}.png";
	}
	public static string GetCardBackPath(int index = 0) => $"res://cards/Backs/back_{Mathf.Clamp(index, 0, 8)}.png";
	public static string GetRandomCardBackPath() => $"res://cards/Backs/back_{GD.RandRange(0, 8)}.png";
	public static Color GetSuitColor(CardSuit suit) => suit switch
	{
		CardSuit.Hearts or CardSuit.Diamonds => new Color(0.9f, 0.1f, 0.1f),
		_ => new Color(0.15f, 0.15f, 0.15f)
	};
	public static string GetSuitSymbol(CardSuit suit) => suit switch
	{ CardSuit.Hearts => "♥", CardSuit.Diamonds => "♦", CardSuit.Clubs => "♣", CardSuit.Spades => "♠", _ => "?" };
}

// ============================================================
// HAND TYPES + EVALUATOR
// ============================================================

public enum PokerHand { HighCard=0, OnePair=1, TwoPair=2, ThreeOfAKind=3, Straight=4, Flush=5, FullHouse=6, FourOfAKind=7, StraightFlush=8, RoyalFlush=9 }

public class HandResult
{
	public PokerHand Hand; public float DamageMultiplier; public Color ComboColor;
	public string DisplayName; public List<CardEntry> ContributingCards; public List<Unit> ContributingUnits;
	public static HandResult None() => new() { Hand = PokerHand.HighCard, DamageMultiplier = 1f,
		ComboColor = new Color(0.7f, 0.7f, 0.7f), DisplayName = "No Combo",
		ContributingCards = new(), ContributingUnits = new() };
}

public class CardEntry { public Unit Owner; public CardRank Rank; public CardSuit Suit; public int FaceValue => Rank.FaceValue(); }

[Flags] public enum JokerEffect
{ None=0, PairsCountDouble=1<<0, StraightAt3=1<<1, FlushAt3=1<<2, FaceCardsBonus=1<<3, TierMultiplier=1<<4, AcesWild=1<<5 }

public static class PokerHandEvaluator
{
	static readonly Dictionary<PokerHand, float> Mult = new()
	{ {PokerHand.HighCard,1f},{PokerHand.OnePair,1.25f},{PokerHand.TwoPair,1.5f},{PokerHand.ThreeOfAKind,1.75f},
	  {PokerHand.Straight,2f},{PokerHand.Flush,2.25f},{PokerHand.FullHouse,2.5f},{PokerHand.FourOfAKind,3f},
	  {PokerHand.StraightFlush,3.5f},{PokerHand.RoyalFlush,4f} };
	static readonly Dictionary<PokerHand, Color> Colors = new()
	{ {PokerHand.HighCard,new(.6f,.6f,.6f)},{PokerHand.OnePair,new(.4f,.8f,.4f)},{PokerHand.TwoPair,new(.3f,.9f,.7f)},
	  {PokerHand.ThreeOfAKind,new(.3f,.6f,1f)},{PokerHand.Straight,new(.8f,.5f,1f)},{PokerHand.Flush,new(1f,.5f,.2f)},
	  {PokerHand.FullHouse,new(1f,.7f,.1f)},{PokerHand.FourOfAKind,new(1f,.3f,.3f)},{PokerHand.StraightFlush,new(1f,.1f,.8f)},
	  {PokerHand.RoyalFlush,new(1f,.9f,0f)} };
	static readonly Dictionary<PokerHand, string> Names = new()
	{ {PokerHand.HighCard,"High Card"},{PokerHand.OnePair,"One Pair!"},{PokerHand.TwoPair,"Two Pair!"},
	  {PokerHand.ThreeOfAKind,"Three of a Kind!"},{PokerHand.Straight,"Straight!!"},{PokerHand.Flush,"Flush!!"},
	  {PokerHand.FullHouse,"Full House!!"},{PokerHand.FourOfAKind,"Four of a Kind!!!"},
	  {PokerHand.StraightFlush,"Straight Flush!!!!"},{PokerHand.RoyalFlush,"ROYAL FLUSH!!!!!"} };

	public static HandResult Evaluate(List<CardEntry> cards, JokerEffect j = JokerEffect.None)
	{
		if (cards == null || cards.Count == 0) return HandResult.None();
		var best = cards.Count <= 5 ? new List<CardEntry>(cards) : FindBest(cards, j);
		var hand = Classify(best, j);
		float m = Mult[hand];
		if (j.HasFlag(JokerEffect.TierMultiplier)) m += best.Sum(c => c.Rank.Tier()) * 0.1f;
		if (j.HasFlag(JokerEffect.PairsCountDouble) && (hand == PokerHand.OnePair || hand == PokerHand.TwoPair)) m = 1f + (m - 1f) * 2f;
		return new() { Hand=hand, DamageMultiplier=m, ComboColor=Colors[hand], DisplayName=Names[hand],
			ContributingCards=best, ContributingUnits=best.Select(c=>c.Owner).Distinct().ToList() };
	}

	static List<CardEntry> FindBest(List<CardEntry> cards, JokerEffect j)
	{
		List<CardEntry> best=null; PokerHand br=PokerHand.HighCard;
		foreach(var c in Combos(cards,5)){var r=Classify(c,j);if(best==null||r>br){best=c;br=r;}} return best??cards.Take(5).ToList();
	}
	static PokerHand Classify(List<CardEntry> cards, JokerEffect j)
	{
		bool aw=j.HasFlag(JokerEffect.AcesWild);
		var suits=cards.Select(c=>(aw&&c.FaceValue==14)?(CardSuit?)null:(CardSuit?)c.Suit).ToList();
		var vals=cards.Select(c=>c.FaceValue).OrderBy(v=>v).ToList();
		var grps=vals.GroupBy(v=>v).OrderByDescending(g=>g.Count()).ThenByDescending(g=>g.Key).ToList();
		int mf=j.HasFlag(JokerEffect.FlushAt3)?3:5, ms=j.HasFlag(JokerEffect.StraightAt3)?3:5;
		bool flush=cards.Count>=mf&&suits.Where(s=>s.HasValue).GroupBy(s=>s.Value).Any(g=>g.Count()>=mf);
		bool straight=IsStraight(vals,ms,j.HasFlag(JokerEffect.FaceCardsBonus));
		if(flush&&straight){bool royal=vals.Contains(14)&&vals.Contains(13)&&vals.Contains(12)&&vals.Contains(11)&&vals.Contains(10);return royal?PokerHand.RoyalFlush:PokerHand.StraightFlush;}
		int t=grps[0].Count(),s2=grps.Count>1?grps[1].Count():0;
		if(t==4)return PokerHand.FourOfAKind;if(t==3&&s2==2)return PokerHand.FullHouse;if(flush)return PokerHand.Flush;
		if(straight)return PokerHand.Straight;if(t==3)return PokerHand.ThreeOfAKind;if(t==2&&s2==2)return PokerHand.TwoPair;
		if(t==2)return PokerHand.OnePair;return PokerHand.HighCard;
	}
	static bool IsStraight(List<int> v, int min, bool fb)
	{
		if(v.Count<min)return false;var d=v.Distinct().OrderBy(x=>x).ToList();if(d.Count<min)return false;
		int streak=1,mx=1;for(int i=1;i<d.Count;i++){if(d[i]-d[i-1]==1)streak++;else streak=1;mx=Math.Max(mx,streak);}return mx>=min;
	}
	static IEnumerable<List<T>> Combos<T>(List<T> l, int k)
	{if(k==0){yield return new();yield break;}for(int i=0;i<l.Count;i++)foreach(var r in Combos(l.Skip(i+1).ToList(),k-1)){var c=new List<T>{l[i]};c.AddRange(r);yield return c;}}
}

// ============================================================
// FLANKING DETECTOR — CHAIN FLANKING
// ============================================================

public static class FlankingDetector
{
	const float TileSize = 2f;
	public static List<(Unit ally, Unit flankedEnemy)> GetFlankingPairs(Unit attacker, Unit target, List<Unit> all)
	{
		var result = new List<(Unit, Unit)>();
		var allies = all.Where(u => u.IsFriendly && u != attacker && GodotObject.IsInstanceValid(u)).ToList();
		var enemies = all.Where(u => !u.IsFriendly && GodotObject.IsInstanceValid(u)).ToList();
		var chained = new HashSet<Unit> { attacker }; var frontier = new Queue<Unit>();
		foreach (var a in allies) if (IsFlank(attacker, a, target)) { result.Add((a, target)); if (chained.Add(a)) frontier.Enqueue(a); }
		while (frontier.Count > 0) { var cur = frontier.Dequeue(); foreach (var e in enemies) foreach (var a in allies)
			if (!chained.Contains(a) && IsFlank(cur, a, e)) { result.Add((a, e)); chained.Add(a); frontier.Enqueue(a); } }
		return result;
	}
	static bool IsFlank(Unit a, Unit b, Unit e)
	{
		float dot = (e.GlobalPosition - a.GlobalPosition).Normalized().Dot((e.GlobalPosition - b.GlobalPosition).Normalized());
		return dot <= -0.3f && Dist(a.GlobalPosition, e.GlobalPosition) <= a.Data.AttackRange && Dist(b.GlobalPosition, e.GlobalPosition) <= b.Data.AttackRange;
	}
	public static int Dist(Vector3 a, Vector3 b)
	{ int ax=Mathf.RoundToInt(a.X/TileSize),az=Mathf.RoundToInt(a.Z/TileSize),bx=Mathf.RoundToInt(b.X/TileSize),bz=Mathf.RoundToInt(b.Z/TileSize);return Mathf.Max(Mathf.Abs(ax-bx),Mathf.Abs(az-bz)); }
}

// ============================================================
// COMBO RESOLVER — ATTACKER EXCLUDED FROM CARD POOL
// ============================================================

public class ComboResult
{
	public HandResult HandResult;
	public Dictionary<Unit, List<(Unit attacker, float multiplier)>> AttackMap = new();
	public bool HasCombo => HandResult != null && HandResult.Hand > PokerHand.HighCard;
	public List<Unit> AllFlankingAllies = new(); // For visuals
}

public static class ComboResolver
{
	public static ComboResult Resolve(Unit attacker, Unit primaryTarget, List<Unit> allUnits, List<GameItem> inventory)
	{
		JokerEffect jokers = JokerEffect.None;
		foreach (var item in inventory) if (item is Equipment eq) jokers |= eq.JokerEffects;
		if (attacker.Data.EquippedWeapon != null) jokers |= attacker.Data.EquippedWeapon.JokerEffects;
		if (attacker.Data.EquippedArmor != null) jokers |= attacker.Data.EquippedArmor.JokerEffects;

		var flankingPairs = FlankingDetector.GetFlankingPairs(attacker, primaryTarget, allUnits);

		// === KEY CHANGE: Only FLANKING ALLIES contribute cards. Attacker does NOT. ===
		var cardPool = new List<CardEntry>();
		var unitsInCombo = new HashSet<Unit>();
		var allFlankAllies = new List<Unit>();

		foreach (var (ally, _) in flankingPairs)
		{
			if (unitsInCombo.Contains(ally)) continue;
			if (cardPool.Count >= 5) break;
			unitsInCombo.Add(ally);
			allFlankAllies.Add(ally);
			if (ally.Data.CardRank != CardRank.None)
				cardPool.Add(new CardEntry { Owner = ally, Rank = ally.Data.CardRank, Suit = ally.Data.CardSuit });
		}

		HandResult handResult = cardPool.Count > 0
			? PokerHandEvaluator.Evaluate(cardPool, jokers)
			: HandResult.None();

		// Ensure contributing units are populated
		if (handResult.ContributingUnits.Count == 0 && cardPool.Count > 0)
		{
			handResult.ContributingCards = cardPool;
			handResult.ContributingUnits = cardPool.Select(c => c.Owner).Distinct().ToList();
		}

		var result = new ComboResult { HandResult = handResult, AllFlankingAllies = allFlankAllies };
		result.AttackMap[primaryTarget] = new List<(Unit, float)> { (attacker, handResult.DamageMultiplier) };

		foreach (var (ally, flankedEnemy) in flankingPairs)
		{
			if (!handResult.ContributingUnits.Contains(ally)) continue;
			if (!result.AttackMap.ContainsKey(flankedEnemy))
				result.AttackMap[flankedEnemy] = new List<(Unit, float)>();
			if (!result.AttackMap[flankedEnemy].Any(x => x.Item1 == ally))
				result.AttackMap[flankedEnemy].Add((ally, handResult.DamageMultiplier));
		}

		return result;
	}
}
