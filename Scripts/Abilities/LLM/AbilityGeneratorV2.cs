using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lexmancer.Abilities.V2;

/// <summary>
/// V2 Ability Generator with effect scripting support
/// </summary>
public class AbilityGeneratorV2
{
    private readonly LLMClientV2 llmClient;
    private readonly AbilityCache cache;
    private readonly string playerId;

    /// <summary>
    /// Create an AbilityGeneratorV2
    /// </summary>
    /// <param name="playerId">Unique player identifier</param>
    /// <param name="llmBaseUrl">URL for Ollama server</param>
    /// <param name="llmModel">Model name (default: qwen2.5:7b)</param>
    public AbilityGeneratorV2(
        string playerId,
        string llmBaseUrl = "http://localhost:11434",
        string llmModel = "qwen2.5:7b")
    {
        this.playerId = playerId;
        this.cache = new AbilityCache(playerId);
        this.llmClient = new LLMClientV2(llmBaseUrl, llmModel);

        GD.Print($"AbilityGeneratorV2 initialized for player: {playerId} [Using {llmModel}]");
    }

    /// <summary>
    /// Generate or retrieve an ability from primitives
    /// Returns both the ability and whether it was cached
    /// </summary>
    public async Task<GenerationResultV2> GenerateAbilityAsync(
        List<PrimitiveType> primitives,
        bool forceNew = false)
    {
        var comboKey = PrimitiveInfo.GetComboKey(primitives);
        GD.Print($"Generating V2 ability for combo: {comboKey}");

        // Check cache first (unless forcing new generation)
        if (!forceNew)
        {
            var cached = cache.GetCachedAbility(comboKey);
            if (cached != null)
            {
                GD.Print($"Found cached V2 ability (version {cached.Version}, used {cached.UseCount} times)");

                // Try to parse as V2
                try
                {
                    var cachedAbility = AbilityV2.FromJson(cached.AbilityJson);
                    return new GenerationResultV2
                    {
                        Ability = cachedAbility,
                        WasCached = true,
                        Version = cached.Version
                    };
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"Failed to parse cached ability as V2: {ex.Message}");
                    GD.Print("Regenerating...");
                }
            }
        }

        // Generate new ability using LLM
        GD.Print("Generating new V2 ability with LLM...");
        var primitiveStrings = primitives.Select(p => p.ToString().ToLower()).ToArray();

        var ability = await llmClient.GenerateAbilityAsync(primitiveStrings);

        // Cache the result as JSON
        var abilityJson = ability.ToJson();
        cache.CacheAbility(comboKey, abilityJson, version: 2);

        return new GenerationResultV2
        {
            Ability = ability,
            WasCached = false,
            Version = 2
        };
    }

    /// <summary>
    /// Get the cached ability if it exists, otherwise null
    /// </summary>
    public AbilityV2 GetCachedAbility(List<PrimitiveType> primitives)
    {
        var comboKey = PrimitiveInfo.GetComboKey(primitives);
        var cached = cache.GetCachedAbility(comboKey);

        if (cached != null)
        {
            try
            {
                return AbilityV2.FromJson(cached.AbilityJson);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Check if a combo has been generated before
    /// </summary>
    public bool HasCachedAbility(List<PrimitiveType> primitives)
    {
        var comboKey = PrimitiveInfo.GetComboKey(primitives);
        return cache.GetCachedAbility(comboKey) != null;
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStats GetCacheStats()
    {
        return cache.GetStats();
    }

    /// <summary>
    /// Record that an ability was used
    /// </summary>
    public void RecordAbilityUsage(List<PrimitiveType> primitives)
    {
        var comboKey = PrimitiveInfo.GetComboKey(primitives);
        cache.RecordUsage(comboKey);
    }

    public void Dispose()
    {
        cache?.Dispose();
    }
}

/// <summary>
/// Result of V2 ability generation
/// </summary>
public class GenerationResultV2
{
    public AbilityV2 Ability { get; set; }
    public bool WasCached { get; set; }
    public int Version { get; set; }
}
