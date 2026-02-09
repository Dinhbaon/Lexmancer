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
	public static string LLMModelName { get; set; } = "granite-3.1-3b-a800m-instruct-Q4_K_M.gguf";  // IBM Granite 3.1 MoE (3.3B total, 800M active)
	public static int LLMContextSize { get; set; } = 4096;  // Increased to prevent KV cache overflow
	public static int LLMBatchSize { get; set; } = 1024;  // Must be >= largest prompt size to avoid NoKvSlot errors
	public static int LLMGpuLayerCount { get; set; } = 0;  // CPU-only: no GPU layers (AMD iGPU not supported)
	public static int LLMThreadCount { get; set; } = 6;  // Use 6 physical cores (12 threads / 2)
	public static float LLMTemperature { get; set; } = 0.7f;
	public static float LLMRepeatPenalty { get; set; } = 1.15f;  // Penalize repetition (1.0 = no penalty, higher = more penalty)
	public static int LLMMaxTokens { get; set; } = 2400;  // Increased to reduce truncation for large JSON outputs

	// Element Settings
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
				GD.Print($"  Model Name: {LLMModelName}");
				GD.Print($"  Model Path Override: {(string.IsNullOrEmpty(LLMModelPath) ? "(none)" : LLMModelPath)}");
				GD.Print($"  Context Size: {LLMContextSize}");
				GD.Print($"  Batch Size: {LLMBatchSize}");
				GD.Print($"  GPU Layers: {LLMGpuLayerCount}");
				GD.Print($"  Threads: {LLMThreadCount}");
				GD.Print($"  Temperature: {LLMTemperature}");
				GD.Print($"  Repeat Penalty: {LLMRepeatPenalty}");
				GD.Print($"  Max Tokens: {LLMMaxTokens}");
			}
			else
			{
				GD.Print($"  LLM URL: {LLMBaseUrl}");
				GD.Print($"  LLM Model: {LLMModel}");
			}
			GD.Print($"  Cache Abilities: {CacheGeneratedAbilities}");
		}
		GD.Print($"Verbose Logging: {VerboseLogging}");
		GD.Print("==========================");
	}
}
