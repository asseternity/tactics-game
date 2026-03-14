// InitiativeSystem.cs
// Replaces the PlayerTurn/EnemyTurn two-phase system with a per-unit initiative queue.
// Each unit acts once per round; order is determined by Speed stat.
using Godot;
using System.Collections.Generic;
using System.Linq;

public class InitiativeQueue
{
	private List<Unit> _order = new();
	private int _currentIndex = 0;
	public int Round { get; private set; } = 1;

	// Called at the start of each battle or when units die/spawn
	public void Rebuild(IEnumerable<Unit> allUnits)
	{
		_order = allUnits
			.Where(u => GodotObject.IsInstanceValid(u))
			.OrderByDescending(u => u.Data.Speed)
			.ThenBy(u => u.IsFriendly ? 0 : 1) // Friendly breaks ties first
			.ToList();
		_currentIndex = 0;
	}

	public Unit Current => (_order.Count > 0 && _currentIndex < _order.Count)
		? _order[_currentIndex] : null;

	/// <summary>Advance to the next unit. Wraps to new round. Returns new current unit.</summary>
	public Unit Advance()
	{
		// Remove dead units
		_order.RemoveAll(u => !GodotObject.IsInstanceValid(u));

		if (_order.Count == 0) return null;

		_currentIndex++;
		if (_currentIndex >= _order.Count)
		{
			_currentIndex = 0;
			Round++;
		}
		return Current;
	}

	/// <summary>Remove a unit (on death) without disturbing the queue order.</summary>
	public void Remove(Unit u)
	{
		int idx = _order.IndexOf(u);
		if (idx < 0) return;
		_order.RemoveAt(idx);
		if (idx < _currentIndex) _currentIndex--;
		_currentIndex = Mathf.Max(0, _currentIndex);
	}

	public List<Unit> PeekNext(int count)
	{
		var result = new List<Unit>();
		int i = _currentIndex;
		while (result.Count < count)
		{
			i = (i + 1) % Mathf.Max(1, _order.Count);
			if (_order.Count == 0) break;
			result.Add(_order[i]);
			if (result.Count >= _order.Count) break;
		}
		return result;
	}

	public List<Unit> AllInOrder() => new List<Unit>(_order);
}
