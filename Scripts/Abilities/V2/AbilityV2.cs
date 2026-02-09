using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// V2 Ability system with scriptable effects
/// </summary>
public class AbilityV2
{
    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("primitives")]
    public List<string> Primitives { get; set; } = new();

    [JsonPropertyName("effects")]
    public List<EffectScript> Effects { get; set; } = new();

    [JsonPropertyName("cooldown")]
    public float Cooldown { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("generated_at")]
    public long GeneratedAt { get; set; }

    /// <summary>
    /// Execute this ability at a position
    /// </summary>
    public void Execute(Vector2 position, Vector2 direction, Node caster, Node worldNode)
    {
        var interpreter = new EffectInterpreter(worldNode);
        var context = new EffectContext
        {
            Position = position,
            Direction = direction,
            Caster = caster,
            WorldNode = worldNode,
            Ability = this // Pass ability reference for visual/element info
        };

        var prims = Primitives != null && Primitives.Count > 0 ? string.Join(", ", Primitives) : "none";
        GD.Print($"✨ Executing ability (primitives: {prims})");
        GD.Print($"   Description: {Description}");
        GD.Print($"   Effects count: {Effects?.Count ?? 0}");

        if (Effects == null || Effects.Count == 0)
        {
            GD.PrintErr("❌ No effects to execute!");
            return;
        }

        foreach (var effect in Effects)
        {
            GD.Print($"   Processing effect with {effect.Script?.Count ?? 0} actions");
            if (effect.Script == null || effect.Script.Count == 0)
            {
                GD.PrintErr("   ❌ Effect has no script actions!");
                continue;
            }

            foreach (var action in effect.Script)
            {
                GD.Print($"   → Executing action: {action.Action}");
                interpreter.Execute(action, context);
            }
        }
        GD.Print($"✓ Ability execution finished");
    }

    /// <summary>
    /// Serialize to JSON for caching
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserialize from JSON
    /// </summary>
    public static AbilityV2 FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        return JsonSerializer.Deserialize<AbilityV2>(json, options);
    }
}
