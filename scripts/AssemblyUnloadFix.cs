using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;

/// <summary>
/// Registers cleanup so Godot can unload C# assemblies when stopping Play or rebuilding.
/// Prevents "Failed to unload assemblies" / "Giving up on assembly reloading" (see
/// https://github.com/godotengine/godot/issues/78513 and
/// https://github.com/dotnet/runtime/issues/65323).
/// System.Text.Json caches type info and blocks unloading; we clear that cache on unload.
/// </summary>
internal static class AssemblyUnloadFix
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly()).Unloading += OnUnloading;
	}

	static void OnUnloading(AssemblyLoadContext context)
	{
		ClearSystemTextJsonCache();
	}

	/// <summary>
	/// Clears System.Text.Json's internal type cache so the assembly can be unloaded.
	/// From: https://github.com/dotnet/runtime/issues/65323#issuecomment-1320949911
	/// </summary>
	static void ClearSystemTextJsonCache()
	{
		try
		{
			var assembly = typeof(JsonSerializerOptions).Assembly;
			var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
			var clearCacheMethod = updateHandlerType?.GetMethod("ClearCache", BindingFlags.Static | BindingFlags.Public);
			clearCacheMethod?.Invoke(null, new object[] { null });
		}
		catch
		{
			// Best-effort; avoid throwing during unload
		}
	}
}
