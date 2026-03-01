using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Loads story JSON and converts it to the runtime script database.
/// Single entry point for campaign + mission data and ScriptEvent execution.
/// </summary>
public static class StoryLoader
{
	static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true
	};

	// --- Public API ---

	/// <summary>Load campaign (list of mission paths). Uses default path if not specified.</summary>
	public static CampaignData GetCampaignData(string jsonPath = null)
	{
		string path = string.IsNullOrEmpty(jsonPath) ? StoryConstants.DefaultCampaignPath : jsonPath;
		return DeserializeFile<CampaignData>(path);
	}

	/// <summary>Load raw mission story data (sections + events). Used by pipeline and loader.</summary>
	public static StoryData GetStoryData(string jsonPath)
	{
		if (string.IsNullOrEmpty(jsonPath)) return null;
		return DeserializeFile<StoryData>(jsonPath);
	}

	/// <summary>Load a mission and convert to script database (section name → list of ScriptEvents).</summary>
	public static Dictionary<string, List<ScriptEvent>> LoadScriptDatabase(string missionJsonPath)
	{
		StoryData data = GetStoryData(missionJsonPath);
		if (data?.Sections == null) return new Dictionary<string, List<ScriptEvent>>();
		return StoryToScriptMapper.ToScriptDatabase(data);
	}

	// --- File + JSON ---

	static T DeserializeFile<T>(string path) where T : class
	{
		if (!FileAccess.FileExists(path))
		{
			if (typeof(T) == typeof(StoryData))
				GD.PrintErr($"[StoryLoader] Missing file: {path}");
			return null;
		}
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		return JsonSerializer.Deserialize<T>(file.GetAsText(), JsonOptions);
	}
}

/// <summary>
/// Maps StoryData (JSON) to runtime ScriptEvent structure.
/// Keeps conversion logic in one place and uses StoryConstants.
/// </summary>
public static class StoryToScriptMapper
{
	public static Dictionary<string, List<ScriptEvent>> ToScriptDatabase(StoryData data)
	{
		var db = new Dictionary<string, List<ScriptEvent>>();
		if (data?.Sections == null) return db;

		foreach (var kvp in data.Sections)
		{
			string sectionName = kvp.Key;
			var events = new List<ScriptEvent>();
			if (kvp.Value?.Events != null)
			{
				foreach (StoryEvent ev in kvp.Value.Events)
				{
					ScriptEvent scriptEv = ToScriptEvent(ev);
					if (scriptEv != null)
						events.Add(scriptEv);
				}
			}
			db[sectionName] = events;
		}
		return db;
	}

	static ScriptEvent ToScriptEvent(StoryEvent ev)
	{
		if (ev?.Type == null) return null;

		switch (ev.Type)
		{
			case StoryConstants.EventAddPartyMember:
				return ScriptEvent.AddPartyMember(ev.ProfileId ?? "", ev.IsPlayer);

			case StoryConstants.EventDialogue:
				string timelinePath = ToTimelinePath(ev.TimelineName);
				return ScriptEvent.Dialogue(timelinePath, ev.Background);

			case StoryConstants.EventJump:
				return ScriptEvent.JumpToSection(ev.TargetSection ?? "");

			case StoryConstants.EventBattle:
				return ScriptEvent.Battle(ToBattleSetup(ev));
			default:
				return null;
		}
	}

	static string ToTimelinePath(string timelineName)
	{
		if (string.IsNullOrEmpty(timelineName)) return null;
		return StoryConstants.TimelinePathPrefix + timelineName + StoryConstants.TimelineExtension;
	}

	static BattleSetup ToBattleSetup(StoryEvent ev)
	{
		var battle = new BattleSetup
		{
			FriendlySpawns = new List<Vector3>(StoryConstants.DefaultFriendlySpawns),
			ElevationEnabled = ev.ElevationEnabled
		};

		if (!string.IsNullOrEmpty(ev.Ground) && Enum.TryParse(ev.Ground, true, out GroundType ground))
			battle.Ground = ground;
		if (!string.IsNullOrEmpty(ev.Light) && Enum.TryParse(ev.Light, true, out LightingMood light))
			battle.Light = light;

		if (ev.Enemies != null)
		{
			for (int i = 0; i < ev.Enemies.Count; i++)
			{
				float x = StoryConstants.EnemyRowX + i * StoryConstants.EnemySpacing;
				battle.Enemies.Add(new UnitSpawn(ev.Enemies[i], new Vector3(x, 0, 4)));
			}
		}

		if (ev.MidBattleEvents != null)
		{
			foreach (var mid in ev.MidBattleEvents)
			{
				string path = ToTimelinePath(mid.TimelineName);
				battle.MidBattleEvents.Add(new MidBattleEvent(mid.Turn, path, mid.Background));
			}
		}

		return battle;
	}
}
