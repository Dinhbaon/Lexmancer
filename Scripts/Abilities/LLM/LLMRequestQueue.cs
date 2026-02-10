using Godot;
using System;
using System.Collections.Concurrent;
using System.Threading;
using Lexmancer.Elements;
using Lexmancer.Abilities.V2;
using Lexmancer.Core;

namespace Lexmancer.Abilities.LLM;

/// <summary>
/// Queue-based LLM request processor.
/// Single background thread processes requests one at a time.
/// Results are marshaled back to main thread via CallDeferred.
/// NO async/await, NO SemaphoreSlim, NO thread chaos!
/// </summary>
public partial class LLMRequestQueue : Node
{
	// Thread-safe queue (built into .NET)
	private readonly ConcurrentQueue<LLMRequest> _requestQueue = new();

	// Event to wake up worker thread
	private readonly ManualResetEventSlim _requestPending = new(false);

	// Background worker thread
	private Thread _workerThread;

	// Cancellation support
	private readonly CancellationTokenSource _cancellation = new();

	// Reference to model manager (for inference)
	private ModelManager _modelManager;

	// Reference to LLM client (for HTTP fallback)
	private LLMClientV2 _llmClient;

	public bool IsWorkerProcessing { get; private set; }
	public int QueuedRequests => _requestQueue.Count;

	public override void _Ready()
	{
		GD.Print("LLMRequestQueue initializing...");

		// Start background worker thread
		_workerThread = new Thread(ProcessRequestLoop)
		{
			Name = "LLM-Worker",
			IsBackground = true, // Dies when main thread exits
			Priority = ThreadPriority.BelowNormal // Don't starve game thread
		};
		_workerThread.Start();

		GD.Print("LLM background worker thread started");
	}

	public override void _ExitTree()
	{
		GD.Print("LLMRequestQueue shutting down...");

		// Signal cancellation
		_cancellation.Cancel();
		_requestPending.Set(); // Wake up worker thread

		// Wait for thread to finish (with timeout)
		if (_workerThread != null && _workerThread.IsAlive)
		{
			if (!_workerThread.Join(TimeSpan.FromSeconds(2)))
			{
				GD.PrintErr("LLM worker thread did not exit gracefully");
				// Note: Cannot force abort in modern .NET, thread will die with process
			}
		}

		_requestPending.Dispose();
		_cancellation.Dispose();

		GD.Print("LLMRequestQueue shut down");
		base._ExitTree();
	}

	/// <summary>
	/// Set model manager reference (called by LLMService after model loads)
	/// </summary>
	public void SetModelManager(ModelManager modelManager)
	{
		_modelManager = modelManager;
	}

	/// <summary>
	/// Set LLM client reference (for HTTP fallback)
	/// </summary>
	public void SetLLMClient(LLMClientV2 client)
	{
		_llmClient = client;
	}

	/// <summary>
	/// Queue a request for element generation.
	/// Returns immediately, result will be delivered via callback on main thread.
	/// </summary>
	public void QueueElementGeneration(
		int element1Id,
		int element2Id,
		Action<Element> onSuccess,
		Action<string> onError)
	{
		var request = new LLMRequest
		{
			RequestId = Guid.NewGuid().ToString(),
			Type = LLMRequestType.ElementGeneration,
			Element1Id = element1Id,
			Element2Id = element2Id,
			OnSuccess = onSuccess,
			OnError = onError,
			QueuedAt = DateTime.UtcNow
		};

		_requestQueue.Enqueue(request);
		_requestPending.Set(); // Wake up worker thread

		GD.Print($"Queued LLM request {request.RequestId} ({_requestQueue.Count} in queue)");

		// Emit event
		EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMGenerationStarted, element1Id, element2Id);
	}

	/// <summary>
	/// Clear all pending requests (e.g., when closing combination panel)
	/// </summary>
	public void ClearQueue()
	{
		int cleared = 0;
		while (_requestQueue.TryDequeue(out _))
		{
			cleared++;
		}

		if (cleared > 0)
		{
			GD.Print($"Cleared {cleared} pending LLM requests");
		}
	}

	/// <summary>
	/// Background worker loop - processes requests one at a time
	/// Runs on separate thread, NOT main thread!
	/// </summary>
	private void ProcessRequestLoop()
	{
		GD.Print("[LLM-Worker] Worker thread started");

		while (!_cancellation.IsCancellationRequested)
		{
			try
			{
				// Wait for request or cancellation
				_requestPending.Wait(_cancellation.Token);
				_requestPending.Reset();

				// Process all queued requests
				while (_requestQueue.TryDequeue(out var request))
				{
					if (_cancellation.IsCancellationRequested)
						break;

					ProcessRequest(request);
				}
			}
			catch (OperationCanceledException)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception ex)
			{
				GD.PrintErr($"[LLM-Worker] Unexpected error: {ex.Message}");
			}
		}

		GD.Print("[LLM-Worker] Worker thread exiting");
	}

	/// <summary>
	/// Process a single request (runs on background thread)
	/// </summary>
	private void ProcessRequest(LLMRequest request)
	{
		IsWorkerProcessing = true;

		try
		{
			var elapsed = DateTime.UtcNow - request.QueuedAt;
			GD.Print($"[LLM-Worker] Processing request {request.RequestId} (waited {elapsed.TotalSeconds:F1}s)");

			switch (request.Type)
			{
				case LLMRequestType.ElementGeneration:
					ProcessElementGeneration(request);
					break;

				default:
					CompleteRequestWithError(request, $"Unknown request type: {request.Type}");
					break;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[LLM-Worker] Error processing request: {ex.Message}");
			CompleteRequestWithError(request, ex.Message);
		}
		finally
		{
			IsWorkerProcessing = false;
		}
	}

	/// <summary>
	/// Process element generation request (background thread)
	/// </summary>
	private void ProcessElementGeneration(LLMRequest request)
	{
		// Get elements from registry (thread-safe read)
		Element elem1 = ServiceLocator.Instance.Elements.GetElement(request.Element1Id);
		Element elem2 = ServiceLocator.Instance.Elements.GetElement(request.Element2Id);

		if (elem1 == null || elem2 == null)
		{
			CompleteRequestWithError(request, $"Elements not found: {request.Element1Id}, {request.Element2Id}");
			return;
		}

		GD.Print($"[LLM-Worker] Generating {elem1.Name} + {elem2.Name}...");

		try
		{
			// Build prompt
			string prompt = BuildElementPrompt(elem1, elem2);

			// Call LLM (BLOCKING call on background thread - this is OK!)
			string jsonResponse = CallLLMBlocking(prompt);

			// Parse response
			if (!ElementJsonParser.TryParseElement(
				jsonResponse,
				out var name,
				out var description,
				out var colorHex,
				out var ability,
				out var parseError))
			{
				CompleteRequestWithError(request, $"Failed to parse LLM response: {parseError}");
				return;
			}

			// Create element
			var newElement = new Element
			{
				Name = name ?? $"{elem1.Name}-{elem2.Name}",
				Description = description ?? $"A fusion of {elem1.Name} and {elem2.Name}",
				ColorHex = colorHex ?? "#808080",
				Primitive = null,
				Tier = Math.Max(elem1.Tier, elem2.Tier) + 1,
				Recipe = new() { elem1.Id, elem2.Id }
			};

			// Set ability
			if (ability != null)
			{
				newElement.Ability = ability;
			}
			else
			{
				// Fallback ability
				newElement.Ability = CreateFallbackAbility(newElement.Name);
			}

			// Cache in database (thread-safe)
			newElement.Id = ServiceLocator.Instance.Elements.CacheElement(newElement);

			GD.Print($"[LLM-Worker] Created element: {newElement.Name} (ID: {newElement.Id})");

			// Complete request on main thread
			CompleteRequestWithSuccess(request, newElement);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[LLM-Worker] Generation failed: {ex.Message}");
			CompleteRequestWithError(request, ex.Message);
		}
	}

	/// <summary>
	/// Call LLM synchronously (BLOCKING - only call from background thread!)
	/// </summary>
	private string CallLLMBlocking(string prompt)
	{
		// Try direct inference first
		if (_modelManager != null && _modelManager.IsLoaded)
		{
			GD.Print("[LLM-Worker] Using direct inference...");
			// NOTE: InferAsync is actually synchronous when awaited
			// We're on background thread so blocking is OK
			var task = _modelManager.InferAsync(prompt, _cancellation.Token);
			task.Wait(_cancellation.Token); // Blocking wait
			return task.Result;
		}

		// Fallback to HTTP
		if (_llmClient != null)
		{
			GD.Print("[LLM-Worker] Using HTTP fallback...");
			// This is also async but we can block on it
			var task = _llmClient.GenerateElementAsync("elem1", "elem2", null, null);
			task.Wait(_cancellation.Token);
			var response = task.Result;
			// Convert to JSON string
			return System.Text.Json.JsonSerializer.Serialize(new
			{
				name = response.Name,
				description = response.Description,
				color = response.ColorHex,
				ability = response.Ability
			});
		}

		throw new InvalidOperationException("No LLM backend available!");
	}

	/// <summary>
	/// Complete request with success (marshals to main thread)
	/// </summary>
	private void CompleteRequestWithSuccess(LLMRequest request, Element result)
	{
		// Marshal back to main thread using Godot's CallDeferred
		// Pass element ID since Element objects can't be passed through Variant
		CallDeferred(nameof(InvokeSuccessCallback), request.RequestId, result.Id);
	}

	/// <summary>
	/// Complete request with error (marshals to main thread)
	/// </summary>
	private void CompleteRequestWithError(LLMRequest request, string error)
	{
		// Marshal back to main thread
		CallDeferred(nameof(InvokeErrorCallback), request.RequestId, error);
	}

	/// <summary>
	/// Invoke success callback on main thread (called via CallDeferred)
	/// </summary>
	private void InvokeSuccessCallback(string requestId, int elementId)
	{
		// Fetch the element from registry since we can't pass complex objects through CallDeferred
		var result = ServiceLocator.Instance.Elements.GetElement(elementId);
		if (result == null)
		{
			GD.PrintErr($"Failed to fetch element {elementId} after LLM generation");
			return;
		}

		GD.Print($"LLM request {requestId} completed successfully: {result.Name}");

		// Emit event for UI to listen to
		EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMGenerationCompleted, result.Id);

		// NOTE: Callbacks are handled via EventBus signals now
		// CombinationPanel should listen to LLMGenerationCompleted event
	}

	/// <summary>
	/// Invoke error callback on main thread (called via CallDeferred)
	/// </summary>
	private void InvokeErrorCallback(string requestId, string error)
	{
		GD.PrintErr($"LLM request {requestId} failed: {error}");
		EventBus.Instance?.EmitSignal(EventBus.SignalName.LLMGenerationFailed, error);
	}

	private string BuildElementPrompt(Element elem1, Element elem2)
	{
		// Same prompt as LLMClientV2
		return $"Generate a creative element by combining {elem1.Name} and {elem2.Name}";
	}

	private AbilityV2 CreateFallbackAbility(string elementName)
	{
		// Minimal fallback
		return new AbilityV2
		{
			Description = $"A basic {elementName} attack",
			Primitives = new() { elementName.ToLower() },
			Effects = new(),
			Cooldown = 1.0f
		};
	}
}

/// <summary>
/// LLM request data
/// </summary>
internal class LLMRequest
{
	public string RequestId { get; set; }
	public LLMRequestType Type { get; set; }
	public int Element1Id { get; set; }
	public int Element2Id { get; set; }
	public Action<Element> OnSuccess { get; set; }
	public Action<string> OnError { get; set; }
	public DateTime QueuedAt { get; set; }
}

/// <summary>
/// Types of LLM requests
/// </summary>
internal enum LLMRequestType
{
	ElementGeneration
	// Future: AbilityGeneration, DescriptionGeneration, etc.
}
