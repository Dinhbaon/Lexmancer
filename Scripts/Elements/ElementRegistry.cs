using Godot;
using System;
using System.Collections.Generic;

namespace Lexmancer.Elements;

/// <summary>
/// Static global accessor for the element system
/// Provides two-level caching: runtime Dictionary + database
/// </summary>
public static class ElementRegistry
{
	private static ElementDatabase database;
	private static Dictionary<string, Element> runtimeCache = new();
	private static bool isInitialized = false;
	private static string currentPlayerId;

	// ==================== INITIALIZATION ====================

	/// <summary>
	/// Initialize the element registry for a player
	/// Must be called before any other operations
	/// </summary>
	public static void Initialize(string playerId)
	{
		if (string.IsNullOrEmpty(playerId))
			throw new ArgumentException("Player ID cannot be null or empty", nameof(playerId));

		// Shutdown existing if already initialized
		if (isInitialized)
		{
			GD.Print("ElementRegistry already initialized, shutting down previous instance");
			Shutdown();
		}

		currentPlayerId = playerId;
		database = new ElementDatabase(playerId);
		runtimeCache = new Dictionary<string, Element>();
		isInitialized = true;

		GD.Print($"ElementRegistry initialized for player: {playerId}");
	}

	/// <summary>
	/// Shutdown the registry and cleanup resources
	/// </summary>
	public static void Shutdown()
	{
		if (!isInitialized)
			return;

		database?.Dispose();
		database = null;
		runtimeCache.Clear();
		isInitialized = false;
		currentPlayerId = null;

		GD.Print("ElementRegistry shut down");
	}

	/// <summary>
	/// Check if the registry is initialized
	/// </summary>
	public static bool IsInitialized => isInitialized;

	// ==================== ELEMENT ACCESS ====================

	/// <summary>
	/// Get an element by ID (with caching) - unified lookup for all elements
	/// </summary>
	public static Element GetElement(string elementId)
	{
		ThrowIfNotInitialized();

		if (string.IsNullOrEmpty(elementId))
			return null;

		// Check runtime cache first
		if (runtimeCache.TryGetValue(elementId, out var cachedElement))
		{
			return cachedElement;
		}

		// Load from database
		var element = database.GetElement(elementId);
		if (element != null)
		{
			// Add to runtime cache
			runtimeCache[elementId] = element;

			// Update last accessed timestamp
			database.UpdateLastAccessed(elementId);

			GD.Print($"Loaded element from database: {elementId}");
		}

		return element;
	}

	/// <summary>
	/// Get all elements of a specific tier
	/// </summary>
	public static List<Element> GetElementsByTier(int tier)
	{
		ThrowIfNotInitialized();

		var elements = database.GetElementsByTier(tier);

		// Add to runtime cache
		foreach (var element in elements)
		{
			if (!runtimeCache.ContainsKey(element.Id))
			{
				runtimeCache[element.Id] = element;
			}
		}

		return elements;
	}

	/// <summary>
	/// Get all cached elements
	/// </summary>
	public static List<Element> GetAllElements()
	{
		ThrowIfNotInitialized();

		var elements = database.GetAllElements();

		// Add to runtime cache
		foreach (var element in elements)
		{
			if (!runtimeCache.ContainsKey(element.Id))
			{
				runtimeCache[element.Id] = element;
			}
		}

		return elements;
	}

	/// <summary>
	/// Check if an element exists
	/// </summary>
	public static bool HasElement(string elementId)
	{
		ThrowIfNotInitialized();

		if (string.IsNullOrEmpty(elementId))
			return false;

		// Check runtime cache first
		if (runtimeCache.ContainsKey(elementId))
			return true;

		// Check database
		return database.HasElement(elementId);
	}

	/// <summary>
	/// Get recipe ingredients for an element
	/// </summary>
	public static List<string> GetRecipeIngredients(string elementId)
	{
		ThrowIfNotInitialized();
		return database.GetRecipeIngredients(elementId);
	}

	// ==================== CACHING ====================

	/// <summary>
	/// Cache an element (updates both runtime cache and database)
	/// </summary>
	public static void CacheElement(Element element)
	{
		ThrowIfNotInitialized();

		if (element == null)
			throw new ArgumentNullException(nameof(element));

		// Cache in database
		database.CacheElement(element);

		// Update runtime cache
		runtimeCache[element.Id] = element;

		GD.Print($"Cached element: {element.Id}");
	}

	/// <summary>
	/// Preload elements into runtime cache (for startup optimization)
	/// </summary>
	public static void PreloadElements(List<string> elementIds)
	{
		ThrowIfNotInitialized();

		if (elementIds == null || elementIds.Count == 0)
			return;

		GD.Print($"Preloading {elementIds.Count} elements...");

		int loaded = 0;
		foreach (var elementId in elementIds)
		{
			if (!runtimeCache.ContainsKey(elementId))
			{
				var element = database.GetElement(elementId);
				if (element != null)
				{
					runtimeCache[elementId] = element;
					loaded++;
				}
			}
		}

		GD.Print($"Preloaded {loaded}/{elementIds.Count} elements into runtime cache");
	}

	/// <summary>
	/// Preload all base elements (Tier 1)
	/// </summary>
	public static void PreloadBaseElements()
	{
		ThrowIfNotInitialized();

		var baseElements = database.GetElementsByTier(1);
		foreach (var element in baseElements)
		{
			runtimeCache[element.Id] = element;
		}

		GD.Print($"Preloaded {baseElements.Count} base elements");
	}

	// ==================== CACHE MANAGEMENT ====================

	/// <summary>
	/// Clear runtime cache (keeps database intact)
	/// </summary>
	public static void ClearRuntimeCache()
	{
		ThrowIfNotInitialized();

		runtimeCache.Clear();
		GD.Print("Runtime cache cleared");
	}

	/// <summary>
	/// Clear all cached elements (both runtime and database)
	/// </summary>
	public static void ClearAllCache()
	{
		ThrowIfNotInitialized();

		runtimeCache.Clear();
		database.ClearCache();
		GD.Print("All caches cleared");
	}

	/// <summary>
	/// Get runtime cache size
	/// </summary>
	public static int GetRuntimeCacheSize()
	{
		return runtimeCache.Count;
	}

	/// <summary>
	/// Get database statistics
	/// </summary>
	public static (int total, int tier1, int tier2, int tier3, int tier4) GetStats()
	{
		ThrowIfNotInitialized();
		return database.GetStats();
	}

	/// <summary>
	/// Print cache statistics to console
	/// </summary>
	public static void PrintStats()
	{
		ThrowIfNotInitialized();

		var (total, tier1, tier2, tier3, tier4) = database.GetStats();

		GD.Print("=== Element Registry Statistics ===");
		GD.Print($"Player ID: {currentPlayerId}");
		GD.Print($"Runtime Cache: {runtimeCache.Count} elements");
		GD.Print($"Database Total: {total} elements");
		GD.Print($"  Tier 1: {tier1}");
		GD.Print($"  Tier 2: {tier2}");
		GD.Print($"  Tier 3: {tier3}");
		GD.Print($"  Tier 4: {tier4}");
		GD.Print("===================================");
	}

	// ==================== HELPERS ====================

	/// <summary>
	/// Throw exception if not initialized
	/// </summary>
	private static void ThrowIfNotInitialized()
	{
		if (!isInitialized)
		{
			throw new InvalidOperationException(
				"ElementRegistry not initialized. Call ElementRegistry.Initialize(playerId) first."
			);
		}
	}
}
