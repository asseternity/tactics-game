using Godot;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Text;

[Tool]
public partial class StoryImporter : Node
{
	[Export]
	public bool GenerateDialogicFiles
	{
		get => false;
		set { if (value) RunPipeline(); }
	}

	private void RunPipeline()
	{
		GD.Print("\n[PIPELINE] Generating Dialogic Files...");

		string jsonPath = ProjectSettings.GlobalizePath("res://story/story_data.json");
		if (!File.Exists(jsonPath))
		{
			GD.PrintErr($"[PIPELINE] ERROR: Could not find story_data.json at {jsonPath}!");
			return;
		}

		string jsonString = File.ReadAllText(jsonPath);
		var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
		StoryData data = JsonSerializer.Deserialize<StoryData>(jsonString, options);

		Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://dialogic_timelines/"));
		Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://dialogic_characters/"));

		GenerateFiles(data);

		GD.Print("[PIPELINE] Dialogic Generation Complete! Reloading resources...");
		EditorInterface.Singleton.GetResourceFilesystem().Scan();
	}

	private void GenerateFiles(StoryData data)
	{
		HashSet<string> uniqueCharacters = new HashSet<string>();

		foreach (var section in data.Sections.Values)
		{
			foreach (var ev in section.Events)
			{
				// 1. Generate Standard Cutscenes
				if (ev.Type == "Dialogue" && ev.Steps != null)
				{
					string timelinePath = ProjectSettings.GlobalizePath($"res://dialogic_timelines/{ev.TimelineName}.dtl");
					StringBuilder dtl = new StringBuilder();
					ParseSteps(ev.Steps, dtl, uniqueCharacters, "");
					File.WriteAllText(timelinePath, dtl.ToString());
					GD.Print($"[PIPELINE] Created Timeline: {ev.TimelineName}.dtl");
				}
				
				// === NEW: 2. Generate Mid-Battle Cutscenes ===
				if (ev.Type == "Battle" && ev.MidBattleEvents != null)
				{
					foreach (var midEvent in ev.MidBattleEvents)
					{
						if (midEvent.Steps != null && midEvent.Steps.Count > 0)
						{
							string timelinePath = ProjectSettings.GlobalizePath($"res://dialogic_timelines/{midEvent.TimelineName}.dtl");
							StringBuilder dtl = new StringBuilder();
							ParseSteps(midEvent.Steps, dtl, uniqueCharacters, "");
							File.WriteAllText(timelinePath, dtl.ToString());
							GD.Print($"[PIPELINE] Created Mid-Battle Timeline: {midEvent.TimelineName}.dtl");
						}
					}
				}
			}
		}

		// Generate Character Stubs
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

	// === NEW: Recursive Parser to handle indents inside choices ===
	private void ParseSteps(List<DialogueStep> steps, StringBuilder dtl, HashSet<string> uniqueCharacters, string indent)
	{
		if (steps == null) return;

		foreach (var step in steps)
		{
			// SIGNAL MUST BE FIRST (prevents fall-through to text lines)
			if (step.Action == "Signal" && !string.IsNullOrEmpty(step.Value))
			{
				dtl.AppendLine($"{indent}signal {step.Value}");
				dtl.AppendLine();               // extra blank line = Dialogic loves this
				continue;
			}

			if (step.Action == "Join")
			{
				uniqueCharacters.Add(step.Character);
				dtl.AppendLine($"{indent}join {step.Character} {step.Position}");
				dtl.AppendLine();
			}
			else if (step.Action == "Leave")
			{
				dtl.AppendLine($"{indent}leave {step.Character}");
				dtl.AppendLine();
			}
			else if (step.Choices != null && step.Choices.Count > 0)
			{
				dtl.AppendLine(); // space before choices
				foreach (var choice in step.Choices)
				{
					dtl.AppendLine($"{indent}- {choice.Text}");
					ParseSteps(choice.Steps, dtl, uniqueCharacters, indent + "\t");
				}
				dtl.AppendLine(); // space after choice block
			}
			else if (!string.IsNullOrEmpty(step.Speaker) && !string.IsNullOrEmpty(step.Text))
			{
				uniqueCharacters.Add(step.Speaker);
				dtl.AppendLine($"{indent}{step.Speaker}: {step.Text}");
			}
		}
	}
}
