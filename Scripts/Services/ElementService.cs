using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Elements;

namespace Lexmancer.Services;

/// <summary>
/// Element management service - replaces static ElementRegistry.
/// Manages element database, runtime cache, and player inventory.
/// This is a Godot autoload service accessible via ServiceLocator.
/// </summary>
public partial class ElementService : Node
{
	private ElementDatabase _database;
	private Dictionary<int, Element> _runtimeCache = new();
	private string _currentPlayerId;
	private bool _isInitialized = false;

	// Player inventories (one per player, but typically just one)
	private Dictionary<string, PlayerElementInventory> _playerInventories = new();

	public bool IsInitialized => _isInitialized;

	public override void _Ready()
	{
		GD.Print("ElementService initializing...");
		// Will be initialized with player ID when game starts
	}

	public override void _ExitTree()
	{
		Shutdown();
		base._ExitTree();
	}

	/// <summary>
	/// Initialize the service for a player
	/// </summary>
	public void Initialize(string playerId)
	{
		if (string.IsNullOrEmpty(playerId))
			throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));

		if (_isInitialized && _currentPlayerId == playerId)
		{
			GD.Print($"ElementService already initialized for player: {playerId}");
			return;
		}

		// Shutdown existing if switching players
		if (_isInitialized)
		{
			GD.Print($"Switching player from {_currentPlayerId} to {playerId}");
			Shutdown();
		}

		_currentPlayerId = playerId;
		_database = new ElementDatabase(playerId);
		_runtimeCache = new Dictionary<int, Element>();
		_isInitialized = true;

		GD.Print($"ElementService initialized for player: {playerId}");
	}

	/// <summary>
	/// Shutdown the service and cleanup resources
	/// </summary>
	public void Shutdown()
	{
		if (!_isInitialized)
			return;

		_database?.Dispose();
		_database = null;
		_runtimeCache.Clear();
		_isInitialized = false;
		_currentPlayerId = null;

		GD.Print("ElementService shut down");
	}

	// ==================== ELEMENT ACCESS ====================

	/// <summary>
	/// Get an element by ID (with caching)
	/// </summary>
	public Element GetElement(int elementId)
	{
		ThrowIfNotInitialized();

		if (elementId <= 0)
			return null;

		// Check runtime cache first
		if (_runtimeCache.TryGetValue(elementId, out var cachedElement))
		{
			return cachedElement;
		}

		// Load from database
		var element = _database.GetElement(elementId);
		if (element != null)
		{
			// Add to runtime cache
			_runtimeCache[elementId] = element;

			// Update last accessed timestamp
			_database.UpdateLastAccessed(elementId);
		}

		return element;
	}

	/// <summary>
	/// Get all elements of a specific tier
	/// </summary>
	public List<Element> GetElementsByTier(int tier)
	{
		ThrowIfNotInitialized();

		var elements = _database.GetElementsByTier(tier);

		// Add to runtime cache
		foreach (var element in elements)
		{
			if (!_runtimeCache.ContainsKey(element.Id))
			{
				_runtimeCache[element.Id] = element;
			}
		}

		return elements;
	}

	/// <summary>
	/// Get all cached elements
	/// </summary>
	public List<Element> GetAllElements()
	{
		ThrowIfNotInitialized();

		var elements = _database.GetAllElements();

		// Add to runtime cache
		foreach (var element in elements)
		{
			if (!_runtimeCache.ContainsKey(element.Id))
			{
				_runtimeCache[element.Id] = element;
			}
		}

		return elements;
	}

	/// <summary>
	/// Check if an element exists
	/// </summary>
	public bool HasElement(int elementId)
	{
		ThrowIfNotInitialized();

		if (elementId <= 0)
			return false;

		// Check runtime cache first
		if (_runtimeCache.ContainsKey(elementId))
			return true;

		// Check database
		return _database.HasElement(elementId);
	}

	/// <summary>
	/// Get recipe ingredients for an element
	/// </summary>
	public List<int> GetRecipeIngredients(int elementId)
	{
		ThrowIfNotInitialized();
		return _database.GetRecipeIngredients(elementId);
	}

	// ==================== CACHING ====================

	/// <summary>
	/// Cache an element (updates both runtime cache and database)
	/// Returns the element ID (auto-generated on insert)
	/// </summary>
	public int CacheElement(Element element)
	{
		ThrowIfNotInitialized();

		if (element == null)
			throw new ArgumentNullException(nameof(element));

		// Cache in database (this sets element.Id if it's a new element)
		int id = _database.CacheElement(element);

		// Update runtime cache
		_runtimeCache[id] = element;

		GD.Print($"Cached element: ID {id} ({element.Name})");

		return id;
	}

	/// <summary>
	/// Update an existing element (both runtime cache and database)
	/// Element must have a valid ID already
	/// </summary>
	public void UpdateElement(Element element)
	{
		ThrowIfNotInitialized();

		if (element == null)
			throw new ArgumentNullException(nameof(element));

		if (element.Id <= 0)
			throw new ArgumentException("Element must have a valid ID to update", nameof(element));

		// Update in database
		_database.CacheElement(element);

		// Update runtime cache
		_runtimeCache[element.Id] = element;

		GD.Print($"Updated element: ID {element.Id} ({element.Name})");
	}

	/// <summary>
	/// Preload elements into runtime cache
	/// </summary>
	public void PreloadElements(List<int> elementIds)
	{
		ThrowIfNotInitialized();

		if (elementIds == null || elementIds.Count == 0)
			return;

		GD.Print($"Preloading {elementIds.Count} elements...");

		int loaded = 0;
		foreach (var elementId in elementIds)
		{
			if (!_runtimeCache.ContainsKey(elementId))
			{
				var element = _database.GetElement(elementId);
				if (element != null)
				{
					_runtimeCache[elementId] = element;
					loaded++;
				}
			}
		}

		GD.Print($"Preloaded {loaded}/{elementIds.Count} elements into runtime cache");
	}

	/// <summary>
	/// Preload all base elements (Tier 1)
	/// </summary>
	public void PreloadBaseElements()
	{
		ThrowIfNotInitialized();

		var baseElements = _database.GetElementsByTier(1);
		foreach (var element in baseElements)
		{
			_runtimeCache[element.Id] = element;
		}

		GD.Print($"Preloaded {baseElements.Count} base elements");
	}

	// ==================== CACHE MANAGEMENT ====================

	/// <summary>
	/// Clear runtime cache (keeps database intact)
	/// </summary>
	public void ClearRuntimeCache()
	{
		ThrowIfNotInitialized();

		_runtimeCache.Clear();
		GD.Print("Runtime cache cleared");
	}

	/// <summary>
	/// Clear all cached elements (both runtime and database)
	/// </summary>
	public void ClearAllCache()
	{
		ThrowIfNotInitialized();

		_runtimeCache.Clear();
		_database.ClearCache();
		foreach (var inventory in _playerInventories.Values)
		{
			inventory.Clear();
		}
		GD.Print("All caches cleared");
	}

	/// <summary>
	/// Get runtime cache size
	/// </summary>
	public int GetRuntimeCacheSize()
	{
		return _runtimeCache.Count;
	}

	/// <summary>
	/// Get database statistics
	/// </summary>
	public (int total, int tier1, int tier2, int tier3, int tier4) GetStats()
	{
		ThrowIfNotInitialized();
		return _database.GetStats();
	}

	/// <summary>
	/// Print cache statistics to console
	/// </summary>
	public void PrintStats()
	{
		ThrowIfNotInitialized();

		var (total, tier1, tier2, tier3, tier4) = _database.GetStats();

		GD.Print("=== Element Service Statistics ===");
		GD.Print($"Player ID: {_currentPlayerId}");
		GD.Print($"Runtime Cache: {_runtimeCache.Count} elements");
		GD.Print($"Database Total: {total} elements");
		GD.Print($"  Tier 1: {tier1}");
		GD.Print($"  Tier 2: {tier2}");
		GD.Print($"  Tier 3: {tier3}");
		GD.Print($"  Tier 4: {tier4}");
		GD.Print("===================================");
	}

	// ==================== PLAYER INVENTORY ====================

	/// <summary>
	/// Get or create player inventory
	/// </summary>
	public PlayerElementInventory GetInventory(string playerId)
	{
		if (string.IsNullOrEmpty(playerId))
			playerId = _currentPlayerId;

		if (!_playerInventories.TryGetValue(playerId, out var inventory))
		{
			inventory = new PlayerElementInventory();
			_playerInventories[playerId] = inventory;
			GD.Print($"Created inventory for player: {playerId}");
		}

		return inventory;
	}

	/// <summary>
	/// Get current player's inventory
	/// </summary>
	public PlayerElementInventory GetCurrentInventory()
	{
		ThrowIfNotInitialized();
		return GetInventory(_currentPlayerId);
	}

	// ==================== HELPERS ====================

	/// <summary>
	/// Throw exception if not initialized
	/// </summary>
	private void ThrowIfNotInitialized()
	{
		if (!_isInitialized)
		{
			throw new InvalidOperationException(
				"ElementService not initialized. Call Initialize(playerId) first."
			);
		}
	}
}
