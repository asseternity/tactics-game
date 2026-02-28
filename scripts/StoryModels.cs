using System.Collections.Generic;

public class CampaignData
{
	public System.Collections.Generic.List<string> Missions { get; set; }
}

public class StoryData
{
	public Dictionary<string, StorySection> Sections { get; set; }
	/// <summary>Optional: character name -> "Left"|"Right"|"Center" for portrait mirroring (match game unit DefaultFacing).</summary>
	public Dictionary<string, string> DefaultFacings { get; set; }
}
public class StorySection { public List<StoryEvent> Events { get; set; } }

public class StoryEvent
{
	public string Type { get; set; }
	public string ProfileId { get; set; }
	public string TimelineName { get; set; }
	public string TargetSection { get; set; }
	public List<string> Enemies { get; set; }
	public string Background { get; set; }
	public bool IsPlayer { get; set; }
	public string Ground { get; set; }
	public string Light { get; set; }
	public bool ElevationEnabled { get; set; }
	public List<StoryMidBattleEvent> MidBattleEvents { get; set; }
	public List<DialogueStep> Steps { get; set; } 
}

public class StoryMidBattleEvent
{
	public int Turn { get; set; }
	public string TimelineName { get; set; }
	public string Background { get; set; }
	public List<DialogueStep> Steps { get; set; }
}

public class DialogueStep 
{
	public string Action { get; set; } 
	public string Character { get; set; }
	public string Position { get; set; }
	/// <summary>Optional: "Left"|"Right"|"Center" for this join (overrides DefaultFacings for portrait mirroring).</summary>
	public string Facing { get; set; }
	public string Speaker { get; set; } 
	public string Text { get; set; }
	/// <summary>Optional: "fear"|"bravery"|"sadness"|"anger"|"confusion" — portrait bounce + cartoon particle effect when line is shown.</summary>
	public string Emotion { get; set; }
	public string Value { get; set; } 
	public List<DialogueChoice> Choices { get; set; }
}

public class DialogueChoice 
{
	public string Text { get; set; }
	public List<DialogueStep> Steps { get; set; } 
}
