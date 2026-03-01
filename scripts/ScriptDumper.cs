using Godot;
using System.Text;

public partial class ScriptDumper : Node
{
	/// <summary>
	/// Folder to scan for .cs files (change in Inspector if needed)
	/// </summary>
	[Export]
	public string ScriptsFolder = "res://scripts/";

	/// <summary>
	/// Where the combined text file will be saved
	/// </summary>
	[Export]
	public string OutputPath = "res://all_csharp_scripts.txt";

	public override void _Ready()
	{
		// Run on the next frame so the node is fully initialized
		CallDeferred(nameof(DumpAllScripts));
	}

	private void DumpAllScripts()
	{
		// Get all files in the folder (no need to open DirAccess manually)
		string[] files = DirAccess.GetFilesAt(ScriptsFolder);

		if (files == null || files.Length == 0)
		{
			GD.PrintErr("No files found in " + ScriptsFolder);
			return;
		}

		var output = FileAccess.Open(OutputPath, FileAccess.ModeFlags.Write);
		if (output == null)
		{
			GD.PrintErr("Failed to create output file: " + OutputPath);
			return;
		}

		var sb = new StringBuilder();
		int dumpedCount = 0;

		// Simple header
		sb.AppendLine("=== ALL C# SCRIPTS COMBINED ===");
		sb.AppendLine($"Generated on: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
		sb.AppendLine($"Source folder: {ScriptsFolder}");
		sb.AppendLine(new string('=', 90));
		sb.AppendLine();

		foreach (string fileName in files)
		{
			if (fileName.ToLower().EndsWith(".cs"))
			{
				string fullPath = ScriptsFolder.PathJoin(fileName);
				string content = FileAccess.GetFileAsString(fullPath);

				if (!string.IsNullOrEmpty(content))
				{
					sb.AppendLine($"[FILENAME: {fileName}]");
					sb.AppendLine();
					sb.AppendLine(content);
					sb.AppendLine();
					sb.AppendLine(new string('=', 90));
					sb.AppendLine("\n");

					dumpedCount++;
				}
			}
		}

		output.StoreString(sb.ToString());
		output.Close();

		GD.Print($"✅ SUCCESS! {dumpedCount} C# script(s) combined into:");
		GD.Print(ProjectSettings.GlobalizePath(OutputPath));
		GD.Print("You can now delete this node/scene.");
	}
}
