using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Builder for creating AbilityV2 from flexible LLM JSON responses
/// </summary>
public static class AbilityBuilder
{
    /// <summary>
    /// Parse LLM JSON response into AbilityV2
    /// </summary>
    public static AbilityV2 FromLLMResponse(string llmJson)
    {
        try
        {
            var json = JsonDocument.Parse(llmJson);
            var root = json.RootElement;

            var ability = new AbilityV2
            {
                Description = GetStringProperty(root, "description", ""),
                Primitives = ParsePrimitives(root),
                Effects = ParseEffects(root),
                Cooldown = GetFloatProperty(root, "cooldown", 1.0f),
                Version = GetIntProperty(root, "version", 2),
                GeneratedAt = GetLongProperty(root, "generated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Validate
            ValidateAbility(ability);

            GD.Print("✓ Parsed ability");
            return ability;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Failed to parse LLM response: {ex.Message}");
            GD.PrintErr($"JSON: {llmJson}");
            throw;
        }
    }

    private static List<string> ParsePrimitives(JsonElement root)
    {
        var primitives = new List<string>();

        if (TryGetPropertyIgnoreCase(root, "primitives", out var primsArray))
        {
            foreach (var prim in primsArray.EnumerateArray())
            {
                primitives.Add(prim.GetString().ToLower());
            }
        }

        return primitives;
    }

    private static List<EffectScript> ParseEffects(JsonElement root)
    {
        var effects = new List<EffectScript>();

        if (!TryGetPropertyIgnoreCase(root, "effects", out var effectsArray))
        {
            GD.PrintErr("No effects found in ability JSON");
            return effects;
        }

        foreach (var effectElement in effectsArray.EnumerateArray())
        {
            var effect = new EffectScript();

            if (TryGetPropertyIgnoreCase(effectElement, "script", out var scriptArray))
            {
                foreach (var actionElement in scriptArray.EnumerateArray())
                {
                    effect.Script.Add(ParseAction(actionElement));
                }
            }
            else
            {
                GD.PrintErr("Effect missing 'script' array");
            }

            effects.Add(effect);
        }

        return effects;
    }

    private static EffectAction ParseAction(JsonElement actionElement)
    {
        var action = new EffectAction();

        // Parse action type
        if (!TryGetPropertyIgnoreCase(actionElement, "action", out var actionType))
        {
            GD.PrintErr("Action missing 'action' field");
            action.Action = "unknown";
        }
        else
        {
            action.Action = actionType.GetString();
        }

        // Parse args
        if (TryGetPropertyIgnoreCase(actionElement, "args", out var argsElement))
        {
            action.Args = ParseDictionary(argsElement);
        }

        // Parse nested on_hit actions
        if (TryGetPropertyIgnoreCase(actionElement, "on_hit", out var onHitArray))
        {
            foreach (var hitAction in onHitArray.EnumerateArray())
            {
                action.OnHit.Add(ParseAction(hitAction));
            }
        }

        // Parse on_expire actions
        if (TryGetPropertyIgnoreCase(actionElement, "on_expire", out var onExpireArray))
        {
            foreach (var expireAction in onExpireArray.EnumerateArray())
            {
                action.OnExpire.Add(ParseAction(expireAction));
            }
        }

        // Parse condition
        if (TryGetPropertyIgnoreCase(actionElement, "condition", out var conditionElement))
        {
            action.Condition = ParseCondition(conditionElement);
        }

        return action;
    }

    private static Dictionary<string, object> ParseDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object>();

        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = ParseValue(prop.Value);
        }

        return dict;
    }

    private static object ParseValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            // Always parse numbers as double to avoid int/float type conversion issues
            // The EffectInterpreter will convert to the appropriate type (int/float) as needed
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Array => ParseArray(value),
            JsonValueKind.Object => ParseDictionary(value),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static List<object> ParseArray(JsonElement arrayElement)
    {
        var list = new List<object>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            list.Add(ParseValue(item));
        }
        return list;
    }

    private static EffectCondition ParseCondition(JsonElement condElement)
    {
        return new EffectCondition
        {
            If = TryGetPropertyIgnoreCase(condElement, "if", out var ifProp) ? ifProp.GetString() : "",
            Then = TryGetPropertyIgnoreCase(condElement, "then", out var thenElement)
                ? ParseDictionary(thenElement)
                : new Dictionary<string, object>()
        };
    }

    private static void ValidateAbility(AbilityV2 ability)
    {
        if (ability.Effects.Count == 0)
        {
            throw new Exception("Ability has no effects");
        }

        // Clamp cooldown to safe range
        ability.Cooldown = Math.Clamp(ability.Cooldown, 0.1f, 10f);

        // Ensure metadata is set
        if (ability.Version == 0) ability.Version = 2;
        if (ability.GeneratedAt == 0)
            ability.GeneratedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (ability.Primitives.Count == 0)
            GD.PrintErr("Warning: Ability has no primitives");
    }

    // Property getters with defaults
    private static string GetStringProperty(JsonElement element, string name, string defaultValue)
    {
        return TryGetPropertyIgnoreCase(element, name, out var prop) ? prop.GetString() : defaultValue;
    }

    private static int GetIntProperty(JsonElement element, string name, int defaultValue)
    {
        if (TryGetPropertyIgnoreCase(element, name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt32();
        }
        return defaultValue;
    }

    private static long GetLongProperty(JsonElement element, string name, long defaultValue)
    {
        if (TryGetPropertyIgnoreCase(element, name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt64();
        }
        return defaultValue;
    }

    private static float GetFloatProperty(JsonElement element, string name, float defaultValue)
    {
        if (TryGetPropertyIgnoreCase(element, name, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return (float)prop.GetDouble();
        }
        return defaultValue;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
