using Godot;
using System;

namespace Lexmancer.Config;

/// <summary>
/// Global game configuration
/// </summary>
public static class GameConfig
{
	// LLM Settings
	public static bool UseLLM { get; set; } = false;  // Set to true to enable LLM generation
	public static string LLMBaseUrl { get; set; } = "http://localhost:11434";
	public static string LLMModel { get; set; } = "qwen2.5:7b";

	// Element Settings
	public static bool GenerateElementAbilitiesOnStartup { get; set; } = false;
	public static bool CacheGeneratedAbilities { get; set; } = true;

	// Debug Settings
	public static bool VerboseLogging { get; set; } = true;

	/// <summary>
	/// Print current configuration to console
	/// </summary>
	public static void PrintConfig()
	{
		GD.Print("=== Game Configuration ===");
		GD.Print($"LLM Enabled: {UseLLM}");
		if (UseLLM)
		{
			GD.Print($"  LLM URL: {LLMBaseUrl}");
			GD.Print($"  LLM Model: {LLMModel}");
			GD.Print($"  Generate on Startup: {GenerateElementAbilitiesOnStartup}");
			GD.Print($"  Cache Abilities: {CacheGeneratedAbilities}");
		}
		GD.Print($"Verbose Logging: {VerboseLogging}");
		GD.Print("==========================");
	}
}
