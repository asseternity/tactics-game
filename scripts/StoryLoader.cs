// StoryLoader.cs

using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class StoryLoader
{
	static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
	public static CampaignData GetCampaignData(string jsonPath = null) => DeserializeFile<CampaignData>(string.IsNullOrEmpty(jsonPath) ? StoryConstants.DefaultCampaignPath : jsonPath);
	public static StoryData GetStoryData(string jsonPath) => string.IsNullOrEmpty(jsonPath) ? null : DeserializeFile<StoryData>(jsonPath);
	public static Dictionary<string, List<ScriptEvent>> LoadScriptDatabase(string path) { var d = GetStoryData(path); return d?.Sections == null ? new() : StoryToScriptMapper.ToScriptDatabase(d); }
	static T DeserializeFile<T>(string path) where T : class { if (!FileAccess.FileExists(path)) { if (typeof(T) == typeof(StoryData)) GD.PrintErr($"[StoryLoader] Missing: {path}"); return null; } using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read); return JsonSerializer.Deserialize<T>(f.GetAsText(), JsonOptions); }
}

public static class StoryToScriptMapper
{
	public static Dictionary<string, List<ScriptEvent>> ToScriptDatabase(StoryData data)
	{
		var db = new Dictionary<string, List<ScriptEvent>>();
		if (data?.Sections == null) return db;
		foreach (var kvp in data.Sections) { var evts = new List<ScriptEvent>(); if (kvp.Value?.Events != null) foreach (var ev in kvp.Value.Events) { var se = Convert(ev); if (se != null) evts.Add(se); } db[kvp.Key] = evts; }
		return db;
	}
	static ScriptEvent Convert(StoryEvent ev) => ev?.Type switch
	{
		StoryConstants.EventAddPartyMember => ScriptEvent.AddPartyMember(ev.ProfileId ?? "", ev.IsPlayer),
		StoryConstants.EventDialogue => ScriptEvent.Dialogue(TPath(ev.TimelineName), ev.Background),
		StoryConstants.EventJump => ScriptEvent.JumpToSection(ev.TargetSection ?? ""),
		StoryConstants.EventBattle => ScriptEvent.Battle(ToBattle(ev)),
		_ => null
	};
	static string TPath(string n) => string.IsNullOrEmpty(n) ? null : StoryConstants.TimelinePathPrefix + n + StoryConstants.TimelineExtension;
	static BattleSetup ToBattle(StoryEvent ev)
	{
		var b = new BattleSetup { FriendlySpawns = new List<Vector3>(StoryConstants.DefaultFriendlySpawns), ElevationEnabled = ev.ElevationEnabled };
		if (!string.IsNullOrEmpty(ev.Ground) && Enum.TryParse(ev.Ground, true, out GroundType g)) b.Ground = g;
		if (!string.IsNullOrEmpty(ev.Light) && Enum.TryParse(ev.Light, true, out LightingMood l)) b.Light = l;
		if (ev.Enemies != null)
		{
			for (int i = 0; i < ev.Enemies.Count; i++)
			{
				int col = i % StoryConstants.EnemyColumnsMax;
				int row = i / StoryConstants.EnemyColumnsMax;
				b.Enemies.Add(new UnitSpawn(ev.Enemies[i], new Vector3(StoryConstants.EnemyStartX + col * StoryConstants.EnemySpacingX, 0, StoryConstants.EnemyStartZ + row * StoryConstants.EnemySpacingZ)));
			}
		}
		if (ev.MidBattleEvents != null) foreach (var m in ev.MidBattleEvents) b.MidBattleEvents.Add(new MidBattleEvent(m.Turn, TPath(m.TimelineName), m.Background));
		return b;
	}
}
