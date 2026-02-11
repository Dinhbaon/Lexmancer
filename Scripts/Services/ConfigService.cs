using Godot;
using System;
using System.Collections.Generic;

namespace Lexmancer.Services;

/// <summary>
/// Game configuration service - replaces static GameConfig.
/// Provides runtime configuration with change notifications via EventBus.
/// This is a Godot autoload service accessible via ServiceLocator.
/// </summary>
public partial class ConfigService : Node
{
	// ==================== LLM SETTINGS ====================

	public bool UseLLM { get; private set; } = true;
	public bool UseLLMFlavor { get; private set; } = false;
	public string LLMBaseUrl { get; private set; } = "http://localhost:11434";
	public string LLMModel { get; private set; } = "qwen2.5:7b";

	// LLamaSharp Direct Inference Settings
	public bool UseLLamaSharpDirect { get; private set; } = true;
	public string LLMModelPath { get; private set; } = "";
	public string LLMModelName { get; private set; } = "granite-3.1-3b-a800m-instruct-Q4_K_M.gguf";
	public int LLMContextSize { get; private set; } = 4096;
	public int LLMBatchSize { get; private set; } = 1024;
	public int LLMGpuLayerCount { get; private set; } = 0;
	public int LLMThreadCount { get; private set; } = 6;
	public bool GpuAutoDetected { get; private set; } = false;
	public float LLMTemperature { get; private set; } = 0.7f;
	public float LLMRepeatPenalty { get; private set; } = 1.15f;
	public int LLMMaxTokens { get; private set; } = 2400;

	// ==================== ELEMENT SETTINGS ====================

	public bool CacheGeneratedAbilities { get; private set; } = true;

	// ==================== GENERATION SETTINGS ====================

	public GenerationMode CurrentGenerationMode { get; private set; } = GenerationMode.FullLLMMode;

	// ==================== DEBUG SETTINGS ====================

	public bool VerboseLogging { get; private set; } = true;

	public override void _Ready()
	{
		GD.Print("ConfigService initialized with default settings");

		// Auto-detect NVIDIA GPU and optimize settings
		AutoDetectGpu();

		PrintConfig();
	}

	// ==================== GPU AUTO-DETECTION ====================

	/// <summary>
	/// Automatically detect NVIDIA GPU and apply optimal settings.
	/// Falls back to CPU-only mode if no GPU detected.
	/// </summary>
	public void AutoDetectGpu()
	{
		GD.Print("=== GPU Auto-Detection ===");

		var gpuInfo = GpuDetector.DetectNvidiaGpu();

		if (gpuInfo != null)
		{
			// Apply recommended GPU settings
			SetLLMGpuLayers(gpuInfo.RecommendedGpuLayers);
			SetLLMThreads(gpuInfo.RecommendedThreads);
			GpuAutoDetected = true;

			GD.Print($"✓ GPU acceleration enabled: {gpuInfo.Name}");
			GD.Print($"  Using {gpuInfo.RecommendedGpuLayers} GPU layers with {gpuInfo.RecommendedThreads} CPU threads");
		}
		else
		{
			// Fallback to CPU-only mode with optimal thread count
			SetLLMGpuLayers(0);
			SetLLMThreads(System.Environment.ProcessorCount - 2); // Leave 2 cores free
			GpuAutoDetected = false;

			GD.Print("✓ Using CPU-only mode");
			GD.Print($"  Using {LLMThreadCount} CPU threads");
		}

		GD.Print("==========================");
	}

	// ==================== SETTERS (with validation) ====================

	public void SetUseLLM(bool enabled)
	{
		if (UseLLM != enabled)
		{
			UseLLM = enabled;
			GD.Print($"LLM usage: {enabled}");
		}
	}

	public void SetUseLLMFlavor(bool enabled)
	{
		if (UseLLMFlavor != enabled)
		{
			UseLLMFlavor = enabled;
			GD.Print($"LLM flavor usage: {enabled}");
		}
	}

	public void SetLLamaSharpDirect(bool enabled)
	{
		if (UseLLamaSharpDirect != enabled)
		{
			UseLLamaSharpDirect = enabled;
			GD.Print($"LLamaSharp direct inference: {enabled}");
		}
	}

	public void SetLLMModelName(string modelName)
	{
		if (!string.IsNullOrEmpty(modelName) && LLMModelName != modelName)
		{
			LLMModelName = modelName;
			GD.Print($"LLM model name: {modelName}");
		}
	}

	public void SetLLMModelPath(string path)
	{
		if (LLMModelPath != path)
		{
			LLMModelPath = path ?? "";
			GD.Print($"LLM model path: {(string.IsNullOrEmpty(path) ? "(default)" : path)}");
		}
	}

	public void SetLLMContextSize(int size)
	{
		size = Mathf.Clamp(size, 512, 32768);
		if (LLMContextSize != size)
		{
			LLMContextSize = size;
			GD.Print($"LLM context size: {size}");
		}
	}

	public void SetLLMBatchSize(int size)
	{
		size = Mathf.Clamp(size, 128, 4096);
		if (LLMBatchSize != size)
		{
			LLMBatchSize = size;
			GD.Print($"LLM batch size: {size}");
		}
	}

	public void SetLLMGpuLayers(int layers)
	{
		layers = Mathf.Max(0, layers);
		if (LLMGpuLayerCount != layers)
		{
			LLMGpuLayerCount = layers;
			GD.Print($"LLM GPU layers: {layers}");
		}
	}

	public void SetLLMThreads(int threads)
	{
		threads = Mathf.Clamp(threads, 1, 32);
		if (LLMThreadCount != threads)
		{
			LLMThreadCount = threads;
			GD.Print($"LLM threads: {threads}");
		}
	}

	public void SetLLMTemperature(float temperature)
	{
		temperature = Mathf.Clamp(temperature, 0.0f, 2.0f);
		if (!Mathf.IsEqualApprox(LLMTemperature, temperature))
		{
			LLMTemperature = temperature;
			GD.Print($"LLM temperature: {temperature}");
		}
	}

	public void SetLLMRepeatPenalty(float penalty)
	{
		penalty = Mathf.Clamp(penalty, 1.0f, 2.0f);
		if (!Mathf.IsEqualApprox(LLMRepeatPenalty, penalty))
		{
			LLMRepeatPenalty = penalty;
			GD.Print($"LLM repeat penalty: {penalty}");
		}
	}

	public void SetLLMMaxTokens(int tokens)
	{
		tokens = Mathf.Clamp(tokens, 128, 8192);
		if (LLMMaxTokens != tokens)
		{
			LLMMaxTokens = tokens;
			GD.Print($"LLM max tokens: {tokens}");
		}
	}

	public void SetCacheAbilities(bool enabled)
	{
		if (CacheGeneratedAbilities != enabled)
		{
			CacheGeneratedAbilities = enabled;
			GD.Print($"Cache abilities: {enabled}");
		}
	}

	public void SetVerboseLogging(bool enabled)
	{
		if (VerboseLogging != enabled)
		{
			VerboseLogging = enabled;
			GD.Print($"Verbose logging: {enabled}");
		}
	}

	public void SetGenerationMode(GenerationMode mode)
	{
		if (CurrentGenerationMode != mode)
		{
			CurrentGenerationMode = mode;
			GD.Print($"Generation mode: {mode}");
		}
	}

	// ==================== BULK CONFIGURATION ====================

	/// <summary>
	/// Load configuration from project settings or file
	/// </summary>
	public void LoadFromProjectSettings()
	{
		// TODO: Load from ProjectSettings or config file
		GD.Print("Loading config from project settings (not implemented yet)");
	}


	/// <summary>
	/// Apply quick preset configurations
	/// </summary>
	public void ApplyPreset(ConfigPreset preset)
	{
		switch (preset)
		{
			case ConfigPreset.Development:
				SetUseLLM(true);
				SetVerboseLogging(true);
				GD.Print("Applied Development preset");
				break;

			case ConfigPreset.Testing:
				SetUseLLM(true);
				SetLLamaSharpDirect(false); // Use HTTP for testing
				SetVerboseLogging(true);
				GD.Print("Applied Testing preset");
				break;

			case ConfigPreset.Production:
				SetUseLLM(true);
				SetLLamaSharpDirect(true); // Use direct inference
				SetVerboseLogging(false);
				GD.Print("Applied Production preset");
				break;

			case ConfigPreset.LowEnd:
				SetUseLLM(true);
				SetVerboseLogging(false);
				GD.Print("Applied LowEnd preset");
				break;
		}

		PrintConfig();
	}

	// ==================== UTILITIES ====================

	/// <summary>
	/// Print current configuration to console
	/// </summary>
	public void PrintConfig()
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
				GD.Print($"  GPU Layers: {LLMGpuLayerCount} {(GpuAutoDetected ? "(auto-detected)" : "(manual/default)")}");
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
		GD.Print($"Generation Mode: {CurrentGenerationMode}");
		GD.Print($"Verbose Logging: {VerboseLogging}");
		GD.Print("==========================");
	}

	/// <summary>
	/// Get configuration as dictionary (for serialization)
	/// </summary>
	public Dictionary<string, Variant> ToDictionary()
	{
		return new Dictionary<string, Variant>
		{
			{ "UseLLM", UseLLM },
			{ "LLMBaseUrl", LLMBaseUrl },
			{ "LLMModel", LLMModel },
			{ "UseLLamaSharpDirect", UseLLamaSharpDirect },
			{ "LLMModelPath", LLMModelPath },
			{ "LLMModelName", LLMModelName },
			{ "LLMContextSize", LLMContextSize },
			{ "LLMBatchSize", LLMBatchSize },
			{ "LLMGpuLayerCount", LLMGpuLayerCount },
			{ "LLMThreadCount", LLMThreadCount },
			{ "GpuAutoDetected", GpuAutoDetected },
			{ "LLMTemperature", LLMTemperature },
			{ "LLMRepeatPenalty", LLMRepeatPenalty },
			{ "LLMMaxTokens", LLMMaxTokens },
			{ "CacheGeneratedAbilities", CacheGeneratedAbilities },
			{ "VerboseLogging", VerboseLogging }
		};
	}

	/// <summary>
	/// Validate current configuration
	/// </summary>
	public bool ValidateConfig(out string error)
	{
		error = null;

		if (UseLLM && UseLLamaSharpDirect)
		{
			if (string.IsNullOrEmpty(LLMModelName) && string.IsNullOrEmpty(LLMModelPath))
			{
				error = "LLM enabled but no model specified";
				return false;
			}

			if (LLMContextSize < LLMBatchSize)
			{
				error = "Context size must be >= batch size";
				return false;
			}
		}

		return true;
	}
}

/// <summary>
/// Configuration presets for quick setup
/// </summary>
public enum ConfigPreset
{
	Development,  // Fast iteration, no LLM
	Testing,      // LLM enabled via HTTP
	Production,   // LLM enabled via direct inference
	LowEnd        // Minimal features for low-end systems
}

/// <summary>
/// Generation modes for element abilities
/// </summary>
public enum GenerationMode
{
	FullLLMMode          // Complete LLM generation
}
