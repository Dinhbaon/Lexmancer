using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;
using System.IO;

/// <summary>
/// Player-specific cache for generated abilities using SQLite
/// </summary>
public class AbilityCache : IDisposable
{
    private readonly string dbPath;
    private readonly SqliteConnection connection;

    public AbilityCache(string playerId)
    {
        // Store cache in user data directory
        var userDataPath = OS.GetUserDataDir();
        var cacheDir = Path.Combine(userDataPath, "ability_cache");

        if (!Directory.Exists(cacheDir))
        {
            Directory.CreateDirectory(cacheDir);
        }

        dbPath = Path.Combine(cacheDir, $"player_{playerId}.db");
        connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        InitializeDatabase();
        GD.Print($"Ability cache initialized for player {playerId} at {dbPath}");
    }

    private void InitializeDatabase()
    {
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS abilities (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                combo_key TEXT NOT NULL UNIQUE,
                ability_json TEXT NOT NULL,
                version INTEGER NOT NULL DEFAULT 1,
                created_at INTEGER NOT NULL,
                last_used INTEGER NOT NULL,
                use_count INTEGER NOT NULL DEFAULT 1
            );

            CREATE INDEX IF NOT EXISTS idx_combo_key ON abilities(combo_key);
            CREATE INDEX IF NOT EXISTS idx_last_used ON abilities(last_used);
        ";

        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Get cached ability for a combination of primitives
    /// Returns null if not found in cache
    /// </summary>
    public CachedAbility GetCachedAbility(string comboKey)
    {
        var sql = "SELECT ability_json, version, use_count FROM abilities WHERE combo_key = @comboKey";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@comboKey", comboKey);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new CachedAbility
            {
                AbilityJson = reader.GetString(0),
                Version = reader.GetInt32(1),
                UseCount = reader.GetInt32(2)
            };
        }

        return null;
    }

    /// <summary>
    /// Get all cached variants of a combo
    /// </summary>
    public List<CachedAbility> GetAllVariants(string comboKey)
    {
        // For future expansion: store multiple variants per combo
        var variants = new List<CachedAbility>();
        var cached = GetCachedAbility(comboKey);
        if (cached != null)
        {
            variants.Add(cached);
        }
        return variants;
    }

    /// <summary>
    /// Save a newly generated ability to cache
    /// </summary>
    public void CacheAbility(string comboKey, string abilityJson, int version = 1)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var sql = @"
            INSERT INTO abilities (combo_key, ability_json, version, created_at, last_used, use_count)
            VALUES (@comboKey, @abilityJson, @version, @timestamp, @timestamp, 1)
            ON CONFLICT(combo_key)
            DO UPDATE SET
                ability_json = @abilityJson,
                version = @version,
                last_used = @timestamp,
                use_count = use_count + 1
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@comboKey", comboKey);
        command.Parameters.AddWithValue("@abilityJson", abilityJson);
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@timestamp", timestamp);
        command.ExecuteNonQuery();

        GD.Print($"Cached ability: {comboKey}");
    }

    /// <summary>
    /// Update usage statistics for an ability
    /// </summary>
    public void RecordUsage(string comboKey)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var sql = @"
            UPDATE abilities
            SET last_used = @timestamp, use_count = use_count + 1
            WHERE combo_key = @comboKey
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@comboKey", comboKey);
        command.Parameters.AddWithValue("@timestamp", timestamp);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Get statistics about cached abilities
    /// </summary>
    public CacheStats GetStats()
    {
        var sql = "SELECT COUNT(*), SUM(use_count) FROM abilities";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();

        if (reader.Read())
        {
            return new CacheStats
            {
                TotalAbilities = reader.GetInt32(0),
                TotalUses = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
            };
        }

        return new CacheStats();
    }

    /// <summary>
    /// Clear all cached abilities (for testing or reset)
    /// </summary>
    public void ClearCache()
    {
        var sql = "DELETE FROM abilities";
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
        GD.Print("Cache cleared");
    }

    public void Dispose()
    {
        connection?.Close();
        connection?.Dispose();
    }
}

public class CachedAbility
{
    public string AbilityJson { get; set; }
    public int Version { get; set; }
    public int UseCount { get; set; }
}

public class CacheStats
{
    public int TotalAbilities { get; set; }
    public int TotalUses { get; set; }
}
