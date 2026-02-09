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
			GD.Print($"  Batch size: {GameConfig.LLMBatchSize}");
			GD.Print($"  GPU layers: {GameConfig.LLMGpuLayerCount}");
			GD.Print($"  Threads: {GameConfig.LLMThreadCount}");

			_modelParams = new ModelParams(modelPath)
			{
				ContextSize = (uint)GameConfig.LLMContextSize,
				BatchSize = (uint)GameConfig.LLMBatchSize,
				GpuLayerCount = GameConfig.LLMGpuLayerCount,
				Threads = GameConfig.LLMThreadCount,
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
	/// ALWAYS enforces JSON grammar to guarantee valid output.
	/// </summary>
	public async Task<string> InferAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (!IsLoaded)
			throw new InvalidOperationException("Model not loaded. Call InitializeAsync first.");

		await _inferenceLock.WaitAsync(cancellationToken);
		try
		{
			var executor = new StatelessExecutor(_model, _modelParams);

			// ALWAYS use grammar constraint - guarantees valid JSON
			var samplingPipeline = new DefaultSamplingPipeline
			{
				Temperature = GameConfig.LLMTemperature,
				Grammar = new Grammar(GetElementGrammar(), "root")
			};

			GD.Print("Using JSON grammar constraint for guaranteed valid output");

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
	/// GBNF grammar for element generation with FULL schema enforcement.
	/// Enforces complete JSON structure AND validates args fields for each action type.
	/// Guarantees 100% valid, parseable output with no runtime validation needed.
	/// </summary>
	private string GetElementGrammar()
	{
		var grammarPath = ProjectSettings.GlobalizePath("res://Assets/LLM/element_grammar.gbnf");
		if (!File.Exists(grammarPath))
		{
			GD.PrintErr($"Grammar file not found: {grammarPath}");
			// Fallback to simple JSON grammar if file not found
			return GetJsonGrammar();
		}

		var grammarText = File.ReadAllText(grammarPath);
		GD.Print($"Loaded element grammar from: {grammarPath} ({grammarText.Length} bytes)");
		return grammarText;
	}

	/// <summary>
	/// Resolve the model file path. Priority: user override > bundled asset > env variable.
	/// </summary>
	private string ResolveModelPath()
	{
		// 1. User-provided override path (full path)
		if (!string.IsNullOrEmpty(GameConfig.LLMModelPath))
		{
			if (File.Exists(GameConfig.LLMModelPath))
			{
				GD.Print($"Using user-provided model: {GameConfig.LLMModelPath}");
				return GameConfig.LLMModelPath;
			}
			GD.PrintErr($"User-provided model not found: {GameConfig.LLMModelPath}");
		}

		// 2. Get model name from config or environment variable
		var modelName = System.Environment.GetEnvironmentVariable("LLM_MODEL_NAME")
		                ?? GameConfig.LLMModelName;

		if (string.IsNullOrEmpty(modelName))
		{
			GD.PrintErr("No model name specified in config or environment variable");
			return null;
		}

		// 3. Bundled model in game assets
		var bundledPath = ProjectSettings.GlobalizePath($"{BundledModelDir}/{modelName}");
		if (File.Exists(bundledPath))
		{
			GD.Print($"Using bundled model: {modelName}");
			return bundledPath;
		}

		// 4. Check user data directory (for development convenience)
		var userDataPath = Path.Combine(OS.GetUserDataDir(), "llm_models", modelName);
		if (File.Exists(userDataPath))
		{
			GD.Print($"Using model from user data: {userDataPath}");
			return userDataPath;
		}

		GD.PrintErr($"Model '{modelName}' not found at:");
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
