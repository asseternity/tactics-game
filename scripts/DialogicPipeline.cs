using Godot;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

[Tool]
public partial class DialogicPipeline : Node
{
	[Export]
	public bool GenerateDialogicFiles
	{
		get => false;
		set { if (value) RunPipeline(); }
	}

	private Dictionary<string, Dictionary<string, string>> _inheritedCharacters = new();

	private void RunPipeline()
	{
		GD.Print("\n[PIPELINE] Generating Dialogic Files...");

		CampaignData campaign = StoryLoader.GetCampaignData(ProjectSettings.GlobalizePath("res://story/campaign.json"));
		if (campaign == null || campaign.Missions == null)
		{
			GD.PrintErr("[PIPELINE] ERROR: Could not parse campaign data.");
			return;
		}

		Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://dialogic_timelines/"));
		Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://dialogic_characters/"));

		// === THE FIX: Process each mission one by one so "Start" sections don't overwrite each other! ===
		foreach (string missionPath in campaign.Missions)
		{
			StoryData missionData = StoryLoader.GetStoryData(ProjectSettings.GlobalizePath(missionPath));
			if (missionData == null) continue;

			BuildInheritanceMap(missionData);
			GenerateFiles(missionData);
		}

		GD.Print("[PIPELINE] Dialogic Generation Complete! Reloading resources...");
		EditorInterface.Singleton.GetResourceFilesystem().Scan();
	}

	private Dictionary<string, string> CloneMap(Dictionary<string, string> source) => new Dictionary<string, string>(source);

	private static readonly HashSet<string> ValidEmotions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "fear", "bravery", "sadness", "anger", "confusion" };

	private static string NormalizeEmotion(string emotion)
	{
		if (string.IsNullOrWhiteSpace(emotion)) return null;
		string e = emotion.Trim();
		return ValidEmotions.Contains(e) ? e.ToLowerInvariant() : null;
	}

	private static bool ShouldMirrorPortrait(string characterName, string joinFacing, StoryData data)
	{
		string resolved = null;
		if (!string.IsNullOrEmpty(joinFacing))
			resolved = joinFacing.Trim();
		else if (data.DefaultFacings != null && data.DefaultFacings.TryGetValue(characterName, out string defaultFacing) && !string.IsNullOrEmpty(defaultFacing))
			resolved = defaultFacing.Trim();
		if (string.IsNullOrEmpty(resolved)) return false;
		return string.Equals(resolved, "Left", System.StringComparison.OrdinalIgnoreCase);
	}

	private void BuildInheritanceMap(StoryData data)
	{
		_inheritedCharacters.Clear();

		var timelineSteps = new Dictionary<string, List<DialogueStep>>();
		var firstTimelineOfSection = new Dictionary<string, string>();

		if (data.Sections == null) return;

		foreach (var kvp in data.Sections)
		{
			string sectionName = kvp.Key;
			var events = kvp.Value.Events;
			
			if (events == null || events.Count == 0) continue;

			for (int i = 0; i < events.Count; i++)
			{
				var ev = events[i];
				if (ev.Type == "Dialogue" && ev.Steps != null)
				{
					timelineSteps[ev.TimelineName] = ev.Steps;
					if (i == 0) firstTimelineOfSection[sectionName] = ev.TimelineName;
				}
				else if (ev.Type == "Battle" && ev.MidBattleEvents != null)
				{
					foreach (var mid in ev.MidBattleEvents)
					{
						if (mid.Steps != null) timelineSteps[mid.TimelineName] = mid.Steps;
					}
				}
			}
		}

		var visited = new HashSet<string>();

		foreach (var kvp in timelineSteps)
		{
			SimulateStepsForJumps(kvp.Value, new Dictionary<string, string>(), timelineSteps, firstTimelineOfSection, visited);
		}
	}

	private void SimulateStepsForJumps(List<DialogueStep> steps, Dictionary<string, string> activeChars, Dictionary<string, List<DialogueStep>> allSteps, Dictionary<string, string> firstTimelines, HashSet<string> visited)
	{
		foreach (var step in steps)
		{
			if (step.Action == "Join") activeChars[step.Character] = step.Position;
			else if (step.Action == "Leave") activeChars.Remove(step.Character);
			else if (step.Action == "Signal" && !string.IsNullOrEmpty(step.Value) && step.Value.StartsWith("JumpTo:"))
			{
				string targetSection = step.Value.Split(':')[1];
				if (firstTimelines.TryGetValue(targetSection, out string targetTimeline))
				{
					if (!visited.Contains(targetTimeline))
					{
						visited.Add(targetTimeline);
						_inheritedCharacters[targetTimeline] = CloneMap(activeChars);
						
						if (allSteps.ContainsKey(targetTimeline))
						{
							SimulateStepsForJumps(allSteps[targetTimeline], CloneMap(activeChars), allSteps, firstTimelines, visited);
						}
					}
				}
			}
			else if (step.Choices != null)
			{
				foreach (var choice in step.Choices)
				{
					SimulateStepsForJumps(choice.Steps, CloneMap(activeChars), allSteps, firstTimelines, visited);
				}
			}
		}
	}

	private void GenerateFiles(StoryData data)
	{
		if (data.Sections == null) return;
		HashSet<string> uniqueCharacters = new HashSet<string>();

		foreach (var section in data.Sections.Values)
		{
			if (section.Events == null) continue;
			foreach (var ev in section.Events)
			{
				if (ev.Type == "Dialogue" && ev.Steps != null)
					WriteTimelineFile(ev.TimelineName, ev.Steps, uniqueCharacters, data);
				
				if (ev.Type == "Battle" && ev.MidBattleEvents != null)
				{
					foreach (var midEvent in ev.MidBattleEvents)
					{
						if (midEvent.Steps != null && midEvent.Steps.Count > 0)
							WriteTimelineFile(midEvent.TimelineName, midEvent.Steps, uniqueCharacters, data);
					}
				}
			}
		}

		foreach (string charName in uniqueCharacters)
		{
			string charPath = ProjectSettings.GlobalizePath($"res://dialogic_characters/{charName}.dch");
			if (!File.Exists(charPath))
			{
				string dchContent = $@"[gd_resource type=""Resource"" script_class=""DialogicCharacter"" load_steps=2 format=3]
[ext_resource type=""Script"" path=""res://addons/dialogic/Resources/character.gd"" id=""1_char""]
[resource]
script = ExtResource(""1_char"")
display_name = ""{charName}""
nicknames = [""""]
color = Color(1, 1, 1, 1)
description = """"
scale = 1.0
offset = Vector2(0, 0)
mirror = false
default_portrait = """"
portraits = {{}}
custom_info = {{}}
";
				File.WriteAllText(charPath, dchContent);
				GD.Print($"[PIPELINE] Created Character Stub: {charName}.dch");
			}
		}
	}

	private void WriteTimelineFile(string name, List<DialogueStep> steps, HashSet<string> uniqueCharacters, StoryData data)
	{
		string timelinePath = ProjectSettings.GlobalizePath($"res://dialogic_timelines/{name}.dtl");
		StringBuilder dtl = new StringBuilder();
		
		var startingChars = _inheritedCharacters.ContainsKey(name) ? CloneMap(_inheritedCharacters[name]) : new Dictionary<string, string>();
		
		ParseSteps(steps, dtl, uniqueCharacters, "", data, startingChars);
		File.WriteAllText(timelinePath, dtl.ToString());
	}

	private void ParseSteps(List<DialogueStep> steps, StringBuilder dtl, HashSet<string> uniqueCharacters, string indent, StoryData data, Dictionary<string, string> currentActiveChars)
	{
		if (steps == null) return;

		foreach (var step in steps)
		{
			// === THE FIX: Restored Signal, Join, and Leave Logic ===
			if (step.Action == "Signal" && !string.IsNullOrEmpty(step.Value))
			{
				if (step.Value.StartsWith("JumpTo:"))
				{
					string targetSection = step.Value.Split(':')[1];
					bool canSeamlessJump = false;
					string targetTimeline = "";
					
					if (data.Sections.TryGetValue(targetSection, out StorySection section) && section.Events.Count > 0)
					{
						if (section.Events[0].Type == "Dialogue") 
						{
							canSeamlessJump = true;
							targetTimeline = section.Events[0].TimelineName;
						}
					}

					if (canSeamlessJump)
					{
						dtl.AppendLine($"{indent}[signal arg=\"SetSectionQuietly:{targetSection}\"]");
						dtl.AppendLine($"{indent}jump {targetTimeline}/");
					}
					else
					{
						dtl.AppendLine($"{indent}[signal arg=\"{step.Value}\"]");
					}
				}
				else 
				{
					dtl.AppendLine($"{indent}[signal arg=\"{step.Value}\"]"); 
				}
				continue;
			}
			else if (step.Action == "Join")
			{
				uniqueCharacters.Add(step.Character);
				
				if (currentActiveChars.TryGetValue(step.Character, out string existingPos) && existingPos == step.Position)
				{
					// Do nothing, they are already here!
				}
				else
				{
					currentActiveChars[step.Character] = step.Position;
					
					// THE FIX: Translate our JSON words into Dialogic's default numbered anchors!
					string posId = step.Position;
					
					if (string.Equals(posId, "FarLeft", System.StringComparison.OrdinalIgnoreCase)) posId = "0";
					else if (string.Equals(posId, "Left", System.StringComparison.OrdinalIgnoreCase)) posId = "1";
					else if (string.Equals(posId, "Center", System.StringComparison.OrdinalIgnoreCase)) posId = "2";
					else if (string.Equals(posId, "Right", System.StringComparison.OrdinalIgnoreCase)) posId = "3";
					else if (string.Equals(posId, "FarRight", System.StringComparison.OrdinalIgnoreCase)) posId = "4";

					bool mirror = ShouldMirrorPortrait(step.Character, step.Facing, data);
					
					// Restored to your EXACT original syntax
					string joinLine = $"join {step.Character} {posId}";
					if (mirror) joinLine += " [mirrored=\"true\"]";
					
					dtl.AppendLine($"{indent}{joinLine}");
					dtl.AppendLine();
				}
				continue;
			}
			else if (step.Action == "Leave")
			{
				currentActiveChars.Remove(step.Character);
				dtl.AppendLine($"{indent}leave {step.Character}");
				dtl.AppendLine();
				continue;
			}

			// === If / ElseIf / Else Logic ===
			if (step.Action == "If")
			{
				dtl.AppendLine($"{indent}if {TranslateCondition(step.Condition)}:");
				ParseSteps(step.Steps, dtl, uniqueCharacters, indent + "\t", data, CloneMap(currentActiveChars));
			}
			else if (step.Action == "ElseIf")
			{
				dtl.AppendLine($"{indent}elif {TranslateCondition(step.Condition)}:");
				ParseSteps(step.Steps, dtl, uniqueCharacters, indent + "\t", data, CloneMap(currentActiveChars));
			}
			else if (step.Action == "Else")
			{
				dtl.AppendLine($"{indent}else:");
				ParseSteps(step.Steps, dtl, uniqueCharacters, indent + "\t", data, CloneMap(currentActiveChars));
			}
			else if (step.Action == "SetFlag")
			{
				dtl.AppendLine($"{indent}[signal arg=\"SetFlag:{step.Value}\"]");
			}
			else if (step.Choices != null && step.Choices.Count > 0)
			{
				dtl.AppendLine(); 
				foreach (var choice in step.Choices)
				{
					dtl.AppendLine($"{indent}- {choice.Text}");
					
					// If the choice sets a flag, inject the signal immediately!
					if (!string.IsNullOrEmpty(choice.SetFlag))
					{
						dtl.AppendLine($"{indent}\t[signal arg=\"SetFlag:{choice.SetFlag}\"]");
					}
					
					ParseSteps(choice.Steps, dtl, uniqueCharacters, indent + "\t", data, CloneMap(currentActiveChars)); 
				}
				dtl.AppendLine();
			}
			else if (!string.IsNullOrEmpty(step.Speaker) && !string.IsNullOrEmpty(step.Text))
			{
				uniqueCharacters.Add(step.Speaker);
				string emotion = NormalizeEmotion(step.Emotion);
				if (!string.IsNullOrEmpty(emotion))
				{
					dtl.AppendLine($"{indent}update {step.Speaker} [animation=\"Bounce\" length=\"0.4\" wait=\"true\"]");
					dtl.AppendLine($"{indent}[signal arg=\"Emotion:{step.Speaker}:{emotion}\"]");
				}
				dtl.AppendLine($"{indent}{step.Speaker}: {step.Text}");
			}
		}
	}
	
	private string TranslateCondition(string cond)
	{
		if (string.IsNullOrWhiteSpace(cond)) return "true";
		cond = cond.Trim();

		// THE FIX: Changed 'GameManager' to 'StoryBridge' in all three checks!
		if (cond.StartsWith("Rel:"))
		{
			string[] tokens = cond.Split(new char[] { ' ' }, 3, System.StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 3)
			{
				string[] idParts = tokens[0].Split(':');
				if (idParts.Length == 3)
					return $"StoryBridge.GetRelationship(\"{idParts[1]}\", \"{idParts[2]}\") {tokens[1]} {tokens[2]}";
			}
		}
		else if (cond.StartsWith("!Flag:"))
		{
			return $"StoryBridge.HasFlag(\"{cond.Substring(6)}\") == false";
		}
		else if (cond.StartsWith("Flag:"))
		{
			return $"StoryBridge.HasFlag(\"{cond.Substring(5)}\") == true";
		}

		return cond; 
	}
}
