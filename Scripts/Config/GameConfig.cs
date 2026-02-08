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

	// LLamaSharp Direct Inference Settings
	public static bool UseLLamaSharpDirect { get; set; } = true;  // Use bundled model instead of Ollama HTTP
	public static string LLMModelPath { get; set; } = "";  // Override path; empty = use bundled model
	public static int LLMContextSize { get; set; } = 2048;
	public static int LLMGpuLayerCount { get; set; } = 0;  // 0=CPU, -1=auto-detect GPU
	public static float LLMTemperature { get; set; } = 0.7f;
	public static int LLMMaxTokens { get; set; } = 512;

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
			GD.Print($"  LLamaSharp Direct: {UseLLamaSharpDirect}");
			if (UseLLamaSharpDirect)
			{
				GD.Print($"  Model Path: {(string.IsNullOrEmpty(LLMModelPath) ? "(bundled)" : LLMModelPath)}");
				GD.Print($"  Context Size: {LLMContextSize}");
				GD.Print($"  GPU Layers: {LLMGpuLayerCount}");
				GD.Print($"  Temperature: {LLMTemperature}");
				GD.Print($"  Max Tokens: {LLMMaxTokens}");
			}
			else
			{
				GD.Print($"  LLM URL: {LLMBaseUrl}");
				GD.Print($"  LLM Model: {LLMModel}");
			}
			GD.Print($"  Generate on Startup: {GenerateElementAbilitiesOnStartup}");
			GD.Print($"  Cache Abilities: {CacheGeneratedAbilities}");
		}
		GD.Print($"Verbose Logging: {VerboseLogging}");
		GD.Print("==========================");
	}
}
