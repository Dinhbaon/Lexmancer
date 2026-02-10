using Godot;
using System;
using System.Threading;
using System.Threading.Tasks;
using Lexmancer.Elements;
using Lexmancer.Core;
using Lexmancer.Services;

namespace Lexmancer.Abilities.LLM;

/// <summary>
/// Centralized LLM service with queue-based request processing.
/// NO async void, NO SemaphoreSlim, NO thread chaos!
/// Uses single background thread + event-driven completion.
/// </summary>
public partial class LLMService : Node
{
    private ConfigService Config => ServiceLocator.Instance.Config;
	public ModelManager ModelManager { get; private set; }
	public LLMClientV2 Client { get; private set; }
	public LLMRequestQueue RequestQueue { get; private set; }
	public LLMElementGenerator ElementGenerator { get; private set; }

	public bool IsInitialized { get; private set; }
	public bool IsModelLoading { get; private set; }

	private CancellationTokenSource _initCancellation;

	public override void _Ready()
	{
		GD.Print("LLMService initializing...");

		// Create request queue immediately
		RequestQueue = new LLMRequestQueue
		{
			Name = "LLMRequestQueue"
		};
		AddChild(RequestQueue);

        // Start model loading in background (non-blocking)
        if (Config.UseLLM && Config.UseLLamaSharpDirect)
        {
            StartModelLoading();
        }
		else
		{
			// Just initialize HTTP client
			InitializeHttpClient();
			IsInitialized = true;
		}
	}

	public override void _ExitTree()
	{
		_initCancellation?.Cancel();
		_initCancellation?.Dispose();
		base._ExitTree();
	}

	/// <summary>
	/// Start loading model in background (non-blocking)
	/// </summary>
	private void StartModelLoading()
	{
		IsModelLoading = true;
		_initCancellation = new CancellationTokenSource();

		GD.Print("Starting model load in background...");
		EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMServiceStatusChanged, false, "Loading model...");

		// Load on thread pool (Godot's async is OK for initialization)
		Task.Run(async () =>
		{
			try
			{
				// Create model manager
				var modelManager = new ModelManager
				{
					Name = "ModelManager"
				};

				// Must add to tree on main thread
				CallDeferred(nameof(AddModelManager), modelManager);

				// Wait a frame for it to be added
				await Task.Delay(100);

				// Load model (this is slow, 20-30 seconds)
				bool loaded = await modelManager.InitializeAsync();

				// Complete on main thread
				CallDeferred(nameof(OnModelLoadComplete), loaded);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Model loading failed: {ex.Message}");
				CallDeferred(nameof(OnModelLoadComplete), false);
			}
		}, _initCancellation.Token);
	}

	/// <summary>
	/// Add model manager to tree (called on main thread via CallDeferred)
	/// </summary>
	private void AddModelManager(ModelManager modelManager)
	{
		ModelManager = modelManager;
		AddChild(ModelManager);
		GD.Print("ModelManager added to tree");
	}

	/// <summary>
	/// Called when model loading completes (on main thread)
	/// </summary>
	private void OnModelLoadComplete(bool success)
	{
		IsModelLoading = false;

		if (success)
		{
			GD.Print("✓ Model loaded successfully!");
			EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMServiceStatusChanged, true, "Ready");

			// Configure request queue
			RequestQueue.SetModelManager(ModelManager);

                // Initialize element generator (for CombinationPanel compatibility)
                Client = new LLMClientV2(); // Create client wrapper
                ElementGenerator = new LLMElementGenerator(
                    playerId: "player_001",
                    useLLM: Config.UseLLM,
                    llmClient: Client
                );
            }
            else
            {
			GD.PrintErr("✗ Model loading failed, using HTTP fallback");
			EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMServiceStatusChanged, true, "Using HTTP");

                // Switch to HTTP
                Config.SetLLamaSharpDirect(false);
                InitializeHttpClient();
            }

		IsInitialized = true;
	}

	/// <summary>
	/// Initialize HTTP client (fallback)
	/// </summary>
	private void InitializeHttpClient()
	{
        Client = new LLMClientV2(Config.LLMBaseUrl, Config.LLMModel);
        RequestQueue.SetLLMClient(Client);

        // Initialize element generator (for CombinationPanel compatibility)
        ElementGenerator = new LLMElementGenerator(
            playerId: "player_001",
            useLLM: Config.UseLLM,
            llmClient: Client
        );

		GD.Print("LLM HTTP client initialized");
	}

	/// <summary>
	/// Request element generation (queue-based, returns immediately)
	/// Result delivered via EventBus events
	/// </summary>
	public void RequestElementGeneration(int element1Id, int element2Id)
	{
		if (!IsInitialized)
		{
			GD.PrintErr("LLMService not initialized yet!");
			EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMGenerationFailed, "Service not ready");
			return;
		}

        if (!Config.UseLLM)
        {
            GD.PrintErr("LLM is disabled!");
            EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMGenerationFailed, "LLM disabled");
            return;
        }

		// Queue the request (non-blocking!)
		RequestQueue.QueueElementGeneration(
			element1Id,
			element2Id,
			onSuccess: null, // Will use events instead
			onError: null
		);
	}

	/// <summary>
	/// Get service status message
	/// </summary>
	public string GetStatusMessage()
	{
		if (!IsInitialized)
			return IsModelLoading ? "Loading model..." : "Initializing...";

		if (ModelManager != null && ModelManager.IsLoaded)
			return $"Ready (Direct inference, {RequestQueue.QueuedRequests} queued)";

		if (Client != null)
			return $"Ready (HTTP fallback, {RequestQueue.QueuedRequests} queued)";

		return "Not available";
	}

	/// <summary>
	/// Get queue status for UI
	/// </summary>
	public (bool isProcessing, int queued) GetQueueStatus()
	{
		return (RequestQueue.IsWorkerProcessing, RequestQueue.QueuedRequests);
	}

	/// <summary>
	/// Clear pending requests (e.g., user closes panel)
	/// </summary>
	public void ClearPendingRequests()
	{
		RequestQueue.ClearQueue();
	}
}
