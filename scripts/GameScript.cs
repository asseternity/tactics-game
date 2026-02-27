// GameScript.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class GameScript
{
	public static Dictionary<string, List<ScriptEvent>> LoadFromJSON()
	{
		string jsonPath = "res://story/story_data.json";
		
		if (!FileAccess.FileExists(jsonPath))
		{
			GD.PrintErr($"[ERROR] Could not find {jsonPath}");
			return new Dictionary<string, List<ScriptEvent>>();
		}

		using var file = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Read);
		string jsonString = file.GetAsText();

		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		StoryData data = JsonSerializer.Deserialize<StoryData>(jsonString, options);

		var scriptDatabase = new Dictionary<string, List<ScriptEvent>>();

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
						Enemies = new List<UnitSpawn>(),
						ElevationEnabled = ev.ElevationEnabled // <-- NEW: Grabs elevation!
					};
					
					// === NEW: Safely parse Enums from the JSON strings ===
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
					
					// === NEW: Map Mid-Battle Events into the Setup! ===
					if (ev.MidBattleEvents != null)
					{
						foreach (var midEvent in ev.MidBattleEvents)
						{
							battle.MidBattleEvents.Add(new MidBattleEvent(
								midEvent.Turn, 
								$"res://dialogic_timelines/{midEvent.TimelineName}.dtl"
							));
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

// === JSON DESERIALIZATION MODELS ===
public class StoryData { public Dictionary<string, StorySection> Sections { get; set; } }
public class StorySection { public List<StoryEvent> Events { get; set; } }
public class StoryEvent
{
	public string Type { get; set; }
	public string ProfileId { get; set; }
	public string TimelineName { get; set; }
	public string TargetSection { get; set; }
	public List<string> Enemies { get; set; }
	public bool IsPlayer { get; set; }
	
	// === NEW: Battle Setup Properties ===
	public string Ground { get; set; }
	public string Light { get; set; }
	public bool ElevationEnabled { get; set; }
	public List<StoryMidBattleEvent> MidBattleEvents { get; set; }
	
	// We include Steps so the Deserializer doesn't throw errors when reading the JSON, 
	// even though the runtime game just ignores them!
	public List<DialogueStep> Steps { get; set; } 
}

// === NEW: Classes for the nested JSON structures ===
public class StoryMidBattleEvent
{
	public int Turn { get; set; }
	public string TimelineName { get; set; }
	public List<DialogueStep> Steps { get; set; }
}

public class DialogueStep 
{
	public string Action { get; set; } 
	public string Character { get; set; }
	public string Position { get; set; } 
	public string Speaker { get; set; } 
	public string Text { get; set; }
	public string Value { get; set; } 
	public List<DialogueChoice> Choices { get; set; }
}

public class DialogueChoice 
{
	public string Text { get; set; }
	public List<DialogueStep> Steps { get; set; } 
}
