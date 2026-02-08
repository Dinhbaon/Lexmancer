using Godot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Lexmancer.Config;

namespace Lexmancer.Abilities.LLM;

/// <summary>
/// Singleton that manages the lifecycle of the bundled LLamaSharp model.
/// Loads GGUF model from game assets, provides thread-safe inference access.
/// </summary>
public partial class ModelManager : Node
{
	private static ModelManager _instance;
	public static ModelManager Instance => _instance;

	private LLamaWeights _model;
	private ModelParams _modelParams;
	private readonly SemaphoreSlim _inferenceLock = new(1, 1);

	public bool IsLoaded => _model != null;

	private const string BundledModelName = "qwen2.5-7b-instruct-q4_k_m.gguf";
	private const string BundledModelDir = "res://Assets/LLM";

	public override void _Ready()
	{
		if (_instance != null && _instance != this)
		{
			QueueFree();
			return;
		}
		_instance = this;
	}

	/// <summary>
	/// Initialize the model asynchronously. Returns true if model loaded successfully.
	/// </summary>
	public async Task<bool> InitializeAsync()
	{
		try
		{
			var modelPath = ResolveModelPath();
			if (string.IsNullOrEmpty(modelPath))
			{
				GD.PrintErr("No model file found. Cannot initialize LLamaSharp.");
				return false;
			}

			GD.Print($"Loading GGUF model from: {modelPath}");
			GD.Print($"  Context size: {GameConfig.LLMContextSize}");
			GD.Print($"  GPU layers: {GameConfig.LLMGpuLayerCount}");

			_modelParams = new ModelParams(modelPath)
			{
				ContextSize = (uint)GameConfig.LLMContextSize,
				GpuLayerCount = GameConfig.LLMGpuLayerCount,
			};

			_model = await LLamaWeights.LoadFromFileAsync(_modelParams);

			GD.Print("LLamaSharp model loaded successfully!");
			GD.Print($"  Model size: {new FileInfo(modelPath).Length / (1024.0 * 1024.0):F1} MB");
			return true;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to load LLamaSharp model: {ex.Message}");
			GD.PrintErr(ex.StackTrace);
			_model = null;
			return false;
		}
	}

	/// <summary>
	/// Run inference with the loaded model. Thread-safe via semaphore.
	/// Optionally enforces JSON grammar for guaranteed valid output.
	/// </summary>
	public async Task<string> InferAsync(string prompt, bool enforceJson = false, CancellationToken cancellationToken = default)
	{
		if (!IsLoaded)
			throw new InvalidOperationException("Model not loaded. Call InitializeAsync first.");

		await _inferenceLock.WaitAsync(cancellationToken);
		try
		{
			var executor = new StatelessExecutor(_model, _modelParams);

			// Create sampling pipeline with optional JSON grammar
			var samplingPipeline = enforceJson
				? new DefaultSamplingPipeline
				{
					Temperature = GameConfig.LLMTemperature,
					Grammar = new Grammar(GetJsonGrammar(), "root")
				}
				: new DefaultSamplingPipeline
				{
					Temperature = GameConfig.LLMTemperature,
				};

			if (enforceJson)
			{
				GD.Print("Using JSON grammar constraint for guaranteed valid output");
			}

			var inferenceParams = new InferenceParams
			{
				SamplingPipeline = samplingPipeline,
				MaxTokens = GameConfig.LLMMaxTokens,
				AntiPrompts = new[] { "\n\n\n" },
			};

			var result = new System.Text.StringBuilder();
			await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
			{
				result.Append(token);
			}

			return result.ToString();
		}
		finally
		{
			_inferenceLock.Release();
		}
	}

	/// <summary>
	/// GBNF grammar for JSON objects (from llama.cpp examples)
	/// </summary>
	private string GetJsonGrammar()
	{
		// Standard JSON grammar from llama.cpp - use simplified version without complex escaping
		return "root ::= object\n" +
		       "object ::= \"{\" ws members ws \"}\"\n" +
		       "members ::= pair (\",\" ws pair)*\n" +
		       "pair ::= string \":\" ws value\n" +
		       "value ::= object | array | string | number | \"true\" | \"false\" | \"null\"\n" +
		       "array ::= \"[\" ws (value (\",\" ws value)*)? ws \"]\"\n" +
		       "string ::= \"\\\"\" ([^\"\\\\\\x00-\\x1F] | \"\\\\\" [\"\\\\/bfnrt])* \"\\\"\"\n" +
		       "number ::= \"-\"? ([0-9] | [1-9] [0-9]*) (\".\" [0-9]+)? ([eE] [-+]? [0-9]+)?\n" +
		       "ws ::= [ \\t\\n\\r]*\n";
	}

	/// <summary>
	/// Resolve the model file path. Priority: user override > bundled asset.
	/// </summary>
	private string ResolveModelPath()
	{
		// 1. User-provided override path
		if (!string.IsNullOrEmpty(GameConfig.LLMModelPath))
		{
			if (File.Exists(GameConfig.LLMModelPath))
			{
				GD.Print($"Using user-provided model: {GameConfig.LLMModelPath}");
				return GameConfig.LLMModelPath;
			}
			GD.PrintErr($"User-provided model not found: {GameConfig.LLMModelPath}");
		}

		// 2. Bundled model in game assets
		var bundledPath = ProjectSettings.GlobalizePath($"{BundledModelDir}/{BundledModelName}");
		if (File.Exists(bundledPath))
		{
			GD.Print($"Using bundled model: {bundledPath}");
			return bundledPath;
		}

		// 3. Check user data directory (for development convenience)
		var userDataPath = Path.Combine(OS.GetUserDataDir(), "llm_models", BundledModelName);
		if (File.Exists(userDataPath))
		{
			GD.Print($"Using model from user data: {userDataPath}");
			return userDataPath;
		}

		GD.PrintErr($"Model not found at:");
		GD.PrintErr($"  Bundled: {bundledPath}");
		GD.PrintErr($"  User data: {userDataPath}");
		return null;
	}

	public override void _ExitTree()
	{
		DisposeModel();
		if (_instance == this)
			_instance = null;
		base._ExitTree();
	}

	private void DisposeModel()
	{
		_model?.Dispose();
		_model = null;
		GD.Print("LLamaSharp model disposed.");
	}
}
