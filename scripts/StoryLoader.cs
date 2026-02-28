using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class StoryLoader
{
	// Helper to deserialize the JSON cleanly for both the runtime and the pipeline tool
	public static StoryData GetRawStoryData(string jsonPath = "res://story/story_data.json")
	{
		if (!FileAccess.FileExists(jsonPath))
		{
			GD.PrintErr($"[ERROR] Could not find {jsonPath}");
			return null;
		}

		using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
		string jsonString = file.GetAsText();

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		return JsonSerializer.Deserialize<StoryData>(jsonString, options);
	}

	public static Dictionary<string, List<ScriptEvent>> LoadFromJSON()
	{
		var scriptDatabase = new Dictionary<string, List<ScriptEvent>>();
		StoryData data = GetRawStoryData();
		
		if (data == null) return scriptDatabase;

		foreach (var kvp in data.Sections)
		{
			string sectionName = kvp.Key;
			var events = new List<ScriptEvent>();

			foreach (var ev in kvp.Value.Events)
			{
				if (ev.Type == "AddPartyMember")
				{
					events.Add(ScriptEvent.AddPartyMember(ev.ProfileId, ev.IsPlayer));
				}
				else if (ev.Type == "Dialogue")
				{
					events.Add(ScriptEvent.Dialogue($"res://dialogic_timelines/{ev.TimelineName}.dtl"));
				}
				else if (ev.Type == "Jump")
				{
					events.Add(ScriptEvent.JumpToSection(ev.TargetSection));
				}
				else if (ev.Type == "Battle")
				{
					var battle = new BattleSetup 
					{
						FriendlySpawns = new List<Vector3> { new Vector3(0,0,4), new Vector3(2,0,4) },
						ElevationEnabled = ev.ElevationEnabled
					};
					
					if (Enum.TryParse(ev.Ground, true, out GroundType parsedGround)) 
						battle.Ground = parsedGround;
					
					if (Enum.TryParse(ev.Light, true, out LightingMood parsedLight)) 
						battle.Light = parsedLight;
					
					if (ev.Enemies != null)
					{
						for (int i = 0; i < ev.Enemies.Count; i++) 
						{
							battle.Enemies.Add(new UnitSpawn(ev.Enemies[i], new Vector3(6 + (i * 2), 0, 4)));
						}
					}
					
					if (ev.MidBattleEvents != null)
					{
						foreach (var midEvent in ev.MidBattleEvents)
						{
							battle.MidBattleEvents.Add(new MidBattleEvent(midEvent.Turn, $"res://dialogic_timelines/{midEvent.TimelineName}.dtl"));
						}
					}
					
					events.Add(ScriptEvent.Battle(battle));
				}
			}
			scriptDatabase[sectionName] = events;
		}

		return scriptDatabase;
	}
}
