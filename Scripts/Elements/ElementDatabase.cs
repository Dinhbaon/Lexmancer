using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Lexmancer.Elements;

/// <summary>
/// SQLite persistence layer for elements
/// Stores element definitions and recipes per player
/// </summary>
public class ElementDatabase : IDisposable
{
	private readonly string dbPath;
	private readonly SqliteConnection connection;
	private readonly string playerId;

	/// <summary>
	/// Create or open element database for a player
	/// </summary>
	public ElementDatabase(string playerId)
	{
		this.playerId = playerId;

		// Get user data directory (platform-specific)
		var userDataPath = OS.GetUserDataDir();
		var cacheDir = Path.Combine(userDataPath, "element_cache");

		// Create directory if it doesn't exist
		if (!Directory.Exists(cacheDir))
		{
			Directory.CreateDirectory(cacheDir);
			GD.Print($"Created element cache directory: {cacheDir}");
		}

		// Database file per player
		dbPath = Path.Combine(cacheDir, $"player_{playerId}.db");
		GD.Print($"Opening element database: {dbPath}");

		// Open connection
		connection = new SqliteConnection($"Data Source={dbPath}");
		connection.Open();

		// Initialize schema
		InitializeDatabase();
	}

	/// <summary>
	/// Create tables and indexes if they don't exist
	/// </summary>
	private void InitializeDatabase()
	{
		var createTablesSql = @"
			-- Main elements table
			CREATE TABLE IF NOT EXISTS elements (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				element_json TEXT NOT NULL,
				tier INTEGER NOT NULL,
				version INTEGER NOT NULL DEFAULT 1,
				created_at INTEGER NOT NULL,
				last_accessed INTEGER NOT NULL
			);

			-- Indexes for performance
			CREATE INDEX IF NOT EXISTS idx_tier ON elements(tier);

			-- Recipe table for querying what elements create what
			CREATE TABLE IF NOT EXISTS element_recipes (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				result_element_id INTEGER NOT NULL,
				ingredient_element_id INTEGER NOT NULL,
				created_at INTEGER NOT NULL,
				FOREIGN KEY(result_element_id) REFERENCES elements(id),
				FOREIGN KEY(ingredient_element_id) REFERENCES elements(id)
			);

			-- Index for recipe queries
			CREATE INDEX IF NOT EXISTS idx_recipe_result ON element_recipes(result_element_id);
			CREATE INDEX IF NOT EXISTS idx_recipe_ingredient ON element_recipes(ingredient_element_id);
		";

		using var command = connection.CreateCommand();
		command.CommandText = createTablesSql;
		command.ExecuteNonQuery();

		GD.Print($"Element database initialized for player: {playerId}");
	}

	/// <summary>
	/// Cache an element (insert or update)
	/// Returns the element ID (auto-generated on insert, existing on update)
	/// </summary>
	public int CacheElement(Element element)
	{
		if (element == null)
			throw new ArgumentNullException(nameof(element));

		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		// Check if element already exists (update) or is new (insert)
		if (element.Id > 0)
		{
			// Update existing element
			var updateSql = @"
				UPDATE elements
				SET element_json = @json,
					tier = @tier,
					version = @version,
					last_accessed = @accessed
				WHERE id = @id
			";

			using var updateCmd = connection.CreateCommand();
			updateCmd.CommandText = updateSql;
			updateCmd.Parameters.AddWithValue("@id", element.Id);
			updateCmd.Parameters.AddWithValue("@json", element.ToJson());
			updateCmd.Parameters.AddWithValue("@tier", element.Tier);
			updateCmd.Parameters.AddWithValue("@version", element.Version);
			updateCmd.Parameters.AddWithValue("@accessed", timestamp);
			updateCmd.ExecuteNonQuery();

			GD.Print($"Updated element ID {element.Id}: {element.Name}");
		}
		else
		{
			// Insert new element
			var insertSql = @"
				INSERT INTO elements (element_json, tier, version, created_at, last_accessed)
				VALUES (@json, @tier, @version, @created, @accessed)
			";

			using var insertCmd = connection.CreateCommand();
			insertCmd.CommandText = insertSql;
			insertCmd.Parameters.AddWithValue("@json", element.ToJson());
			insertCmd.Parameters.AddWithValue("@tier", element.Tier);
			insertCmd.Parameters.AddWithValue("@version", element.Version);
			insertCmd.Parameters.AddWithValue("@created", element.CreatedAt);
			insertCmd.Parameters.AddWithValue("@accessed", timestamp);
			insertCmd.ExecuteNonQuery();

			// Get the auto-generated ID
			using var idCmd = connection.CreateCommand();
			idCmd.CommandText = "SELECT last_insert_rowid()";
			element.Id = Convert.ToInt32(idCmd.ExecuteScalar());

			GD.Print($"Inserted new element ID {element.Id}: {element.Name}");
		}

		// Update recipe table
		UpdateRecipeTable(element);

		return element.Id;
	}

	/// <summary>
	/// Update the recipe table for this element
	/// </summary>
	private void UpdateRecipeTable(Element element)
	{
		// Delete existing recipes for this element
		var deleteSql = "DELETE FROM element_recipes WHERE result_element_id = @id";
		using (var deleteCmd = connection.CreateCommand())
		{
			deleteCmd.CommandText = deleteSql;
			deleteCmd.Parameters.AddWithValue("@id", element.Id);
			deleteCmd.ExecuteNonQuery();
		}

		// Insert new recipes
		if (element.Recipe != null && element.Recipe.Count > 0)
		{
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
			var insertSql = @"
				INSERT INTO element_recipes (result_element_id, ingredient_element_id, created_at)
				VALUES (@result, @ingredient, @created)
			";

			foreach (var ingredientId in element.Recipe)
			{
				using var insertCmd = connection.CreateCommand();
				insertCmd.CommandText = insertSql;
				insertCmd.Parameters.AddWithValue("@result", element.Id);
				insertCmd.Parameters.AddWithValue("@ingredient", ingredientId);
				insertCmd.Parameters.AddWithValue("@created", timestamp);
				insertCmd.ExecuteNonQuery();
			}
		}
	}

	/// <summary>
	/// Get an element by ID
	/// </summary>
	public Element GetElement(int elementId)
	{
		if (elementId <= 0)
			return null;

		var sql = "SELECT id, element_json FROM elements WHERE id = @id LIMIT 1";

		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.AddWithValue("@id", elementId);

		using var reader = command.ExecuteReader();
		if (reader.Read())
		{
			var id = reader.GetInt32(0);
			var json = reader.GetString(1);
			var element = Element.FromJson(json);
			element.Id = id; // Set the ID from database
			return element;
		}

		return null;
	}

	/// <summary>
	/// Get all elements of a specific tier
	/// </summary>
	public List<Element> GetElementsByTier(int tier)
	{
		var elements = new List<Element>();

		var sql = "SELECT id, element_json FROM elements WHERE tier = @tier ORDER BY id";

		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.AddWithValue("@tier", tier);

		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			var id = reader.GetInt32(0);
			var json = reader.GetString(1);
			try
			{
				var element = Element.FromJson(json);
				element.Id = id; // Set the ID from database
				elements.Add(element);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Failed to parse element: {ex.Message}");
			}
		}

		return elements;
	}

	/// <summary>
	/// Get all cached elements
	/// </summary>
	public List<Element> GetAllElements()
	{
		var elements = new List<Element>();

		var sql = "SELECT id, element_json FROM elements ORDER BY tier, id";

		using var command = connection.CreateCommand();
		command.CommandText = sql;

		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			var id = reader.GetInt32(0);
			var json = reader.GetString(1);
			try
			{
				var element = Element.FromJson(json);
				element.Id = id; // Set the ID from database
				elements.Add(element);
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Failed to parse element: {ex.Message}");
			}
		}

		GD.Print($"Loaded {elements.Count} elements from database");
		return elements;
	}

	/// <summary>
	/// Get the ingredient element IDs that create this element
	/// </summary>
	public List<int> GetRecipeIngredients(int elementId)
	{
		var ingredients = new List<int>();

		var sql = "SELECT ingredient_element_id FROM element_recipes WHERE result_element_id = @id";

		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.AddWithValue("@id", elementId);

		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			ingredients.Add(reader.GetInt32(0));
		}

		return ingredients;
	}

	/// <summary>
	/// Check if an element exists in the database
	/// </summary>
	public bool HasElement(int elementId)
	{
		if (elementId <= 0)
			return false;

		var sql = "SELECT COUNT(*) FROM elements WHERE id = @id";

		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.AddWithValue("@id", elementId);

		var count = (long)command.ExecuteScalar();
		return count > 0;
	}

	/// <summary>
	/// Update the last accessed timestamp for an element
	/// </summary>
	public void UpdateLastAccessed(int elementId)
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		var sql = "UPDATE elements SET last_accessed = @timestamp WHERE id = @id";

		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.Parameters.AddWithValue("@timestamp", timestamp);
		command.Parameters.AddWithValue("@id", elementId);
		command.ExecuteNonQuery();
	}

	/// <summary>
	/// Clear all cached elements
	/// </summary>
	public void ClearCache()
	{
		// Delete recipes first to avoid foreign key constraint violations
		var sql = "DELETE FROM element_recipes; DELETE FROM elements;";

		using var command = connection.CreateCommand();
		command.CommandText = sql;
		command.ExecuteNonQuery();

		GD.Print("Element cache cleared");
	}

	/// <summary>
	/// Get database statistics
	/// </summary>
	public (int totalElements, int tier1, int tier2, int tier3, int tier4) GetStats()
	{
		var sql = @"
			SELECT
				COUNT(*) as total,
				SUM(CASE WHEN tier = 1 THEN 1 ELSE 0 END) as tier1,
				SUM(CASE WHEN tier = 2 THEN 1 ELSE 0 END) as tier2,
				SUM(CASE WHEN tier = 3 THEN 1 ELSE 0 END) as tier3,
				SUM(CASE WHEN tier = 4 THEN 1 ELSE 0 END) as tier4
			FROM elements
		";

		using var command = connection.CreateCommand();
		command.CommandText = sql;

		using var reader = command.ExecuteReader();
		if (reader.Read())
		{
			return (
				totalElements: Convert.ToInt32(reader.GetInt64(0)),
				tier1: Convert.ToInt32(reader.GetInt64(1)),
				tier2: Convert.ToInt32(reader.GetInt64(2)),
				tier3: Convert.ToInt32(reader.GetInt64(3)),
				tier4: Convert.ToInt32(reader.GetInt64(4))
			);
		}

		return (0, 0, 0, 0, 0);
	}

	/// <summary>
	/// Cleanup resources
	/// </summary>
	public void Dispose()
	{
		connection?.Close();
		connection?.Dispose();
		GD.Print($"Element database closed for player: {playerId}");
	}
}
