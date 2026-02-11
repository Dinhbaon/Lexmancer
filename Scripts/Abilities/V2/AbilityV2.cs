using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

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
    /// Ensure description is present; generate a concise summary from effects if missing.
    /// </summary>
    public void EnsureDescription()
    {
        if (!string.IsNullOrWhiteSpace(Description))
            return;

        Description = BuildDescriptionFromEffects(this);
    }

    /// <summary>
    /// Execute this ability at a position
    /// </summary>
    public void Execute(Vector2 position, Vector2 direction, Node caster, Node worldNode)
    {
        var interpreter = EffectInterpreterPool.Get(worldNode);
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
        var node = JsonSerializer.SerializeToNode(this, options);
        if (node == null)
            return "{}";

        PruneEmptyArrays(node);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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
        var ability = JsonSerializer.Deserialize<AbilityV2>(json, options);
        ability?.EnsureDescription();
        return ability;
    }

    private static void PruneEmptyArrays(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var toRemove = new List<string>();
            foreach (var kvp in obj)
            {
                if (kvp.Value == null)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                PruneEmptyArrays(kvp.Value);

                if (kvp.Value is JsonArray arr && arr.Count == 0)
                {
                    toRemove.Add(kvp.Key);
                }
                else if (kvp.Value is JsonObject childObj && childObj.Count == 0)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
                obj.Remove(key);
        }
        else if (node is JsonArray array)
        {
            for (int i = array.Count - 1; i >= 0; i--)
            {
                var child = array[i];
                if (child == null)
                {
                    array.RemoveAt(i);
                    continue;
                }

                PruneEmptyArrays(child);
                if (child is JsonObject childObj && childObj.Count == 0)
                {
                    array.RemoveAt(i);
                }
            }
        }
    }

    private static string BuildDescriptionFromEffects(AbilityV2 ability)
    {
        var intro = ability?.Primitives != null && ability.Primitives.Count > 0
            ? $"A {string.Join("/", ability.Primitives)} ability"
            : "An ability";

        if (ability?.Effects == null || ability.Effects.Count == 0)
            return $"{intro}.";

        var phrases = new List<string>();

        foreach (var effect in ability.Effects)
        {
            if (effect?.Script == null)
                continue;

            foreach (var action in effect.Script)
            {
                var top = DescribeTopLevelAction(action);
                if (!string.IsNullOrEmpty(top))
                    phrases.Add(top);

                var onHit = DescribeNestedActions("On hit", action?.OnHit);
                if (!string.IsNullOrEmpty(onHit))
                    phrases.Add(onHit);

                var onExpire = DescribeNestedActions("On expire", action?.OnExpire);
                if (!string.IsNullOrEmpty(onExpire))
                    phrases.Add(onExpire);

                if (phrases.Count >= 3)
                    break;
            }

            if (phrases.Count >= 3)
                break;
        }

        if (phrases.Count == 0)
            return $"{intro}.";

        var summary = $"{intro} {string.Join("; ", phrases)}.";
        return TrimToMax(summary, 200);
    }

    private static string DescribeTopLevelAction(EffectAction action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.Action))
            return "";

        var name = action.Action.Trim().ToLowerInvariant();
        return name switch
        {
            "spawn_area" => DescribeSpawnArea(action.Args),
            "spawn_projectile" => DescribeSpawnProjectile(action.Args),
            "spawn_beam" => DescribeSpawnBeam(action.Args),
            "spawn_melee" => DescribeSpawnMelee(action.Args),
            "chain_to_nearby" => "chains to nearby targets",
            "repeat" => DescribeRepeat(action.Args),
            _ => name.Replace('_', ' ')
        };
    }

    private static string DescribeNestedActions(string prefix, List<EffectAction> actions)
    {
        if (actions == null || actions.Count == 0)
            return "";

        var parts = new List<string>();
        foreach (var act in actions)
        {
            var desc = DescribeTerminalAction(act);
            if (!string.IsNullOrEmpty(desc))
                parts.Add(desc);
            if (parts.Count >= 2)
                break;
        }

        if (parts.Count == 0)
            return "";

        return $"{prefix}, {JoinWithAnd(parts)}";
    }

    private static string DescribeTerminalAction(EffectAction action)
    {
        if (action == null || string.IsNullOrWhiteSpace(action.Action))
            return "";

        var name = action.Action.Trim().ToLowerInvariant();
        return name switch
        {
            "damage" => DescribeDamage(action.Args),
            "apply_status" => DescribeApplyStatus(action.Args),
            "heal" => DescribeHeal(action.Args),
            "knockback" => DescribeKnockback(action.Args),
            "spawn_area" => $"spawns an area {DescribeSpawnArea(action.Args)}",
            "spawn_projectile" => $"spawns a projectile {DescribeSpawnProjectile(action.Args)}",
            "spawn_beam" => $"spawns a beam {DescribeSpawnBeam(action.Args)}",
            "spawn_melee" => $"spawns a melee strike {DescribeSpawnMelee(action.Args)}",
            "chain_to_nearby" => "chains to nearby targets",
            "repeat" => DescribeRepeat(action.Args),
            _ => name.Replace('_', ' ')
        };
    }

    private static string DescribeSpawnArea(Dictionary<string, object> args)
    {
        var sb = new StringBuilder("creates an area");
        if (TryGetNumber(args, "radius", out var radius))
            sb.Append($" (radius {FormatNumber(radius)})");
        if (TryGetNumber(args, "duration", out var duration))
            sb.Append($" for {FormatNumber(duration)}s");
        if (TryGetNumber(args, "lingering_damage", out var linger))
            sb.Append($", lingering damage {FormatNumber(linger)}");
        if (TryGetNumber(args, "damage", out var damage))
            sb.Append($", damage {FormatNumber(damage)}");
        return sb.ToString();
    }

    private static string DescribeSpawnProjectile(Dictionary<string, object> args)
    {
        var sb = new StringBuilder("fires a projectile");
        if (TryGetNumber(args, "count", out var count) && count > 1)
            sb.Append($" x{FormatNumber(count)}");
        if (TryGetNumber(args, "speed", out var speed))
            sb.Append($" at speed {FormatNumber(speed)}");
        if (TryGetNumber(args, "range", out var range))
            sb.Append($" (range {FormatNumber(range)})");
        return sb.ToString();
    }

    private static string DescribeSpawnBeam(Dictionary<string, object> args)
    {
        var sb = new StringBuilder("fires a beam");
        if (TryGetNumber(args, "duration", out var duration))
            sb.Append($" for {FormatNumber(duration)}s");
        if (TryGetNumber(args, "length", out var length))
            sb.Append($" (length {FormatNumber(length)})");
        return sb.ToString();
    }

    private static string DescribeSpawnMelee(Dictionary<string, object> args)
    {
        var sb = new StringBuilder("performs a melee strike");
        if (TryGetString(args, "shape", out var shape))
            sb.Append($" ({shape})");
        if (TryGetNumber(args, "range", out var range))
            sb.Append($" range {FormatNumber(range)}");
        return sb.ToString();
    }

    private static string DescribeRepeat(Dictionary<string, object> args)
    {
        if (TryGetNumber(args, "times", out var times))
            return $"repeats {FormatNumber(times)} times";
        return "repeats effects";
    }

    private static string DescribeDamage(Dictionary<string, object> args)
    {
        if (TryGetNumber(args, "amount", out var amount))
        {
            if (TryGetString(args, "element", out var element))
                return $"deals {FormatNumber(amount)} {element} damage";
            return $"deals {FormatNumber(amount)} damage";
        }
        return "deals damage";
    }

    private static string DescribeApplyStatus(Dictionary<string, object> args)
    {
        if (TryGetString(args, "status", out var status))
        {
            if (TryGetNumber(args, "duration", out var duration))
                return $"applies {status} for {FormatNumber(duration)}s";
            return $"applies {status}";
        }
        return "applies a status effect";
    }

    private static string DescribeHeal(Dictionary<string, object> args)
    {
        if (TryGetNumber(args, "amount", out var amount))
            return $"heals {FormatNumber(amount)}";
        return "heals";
    }

    private static string DescribeKnockback(Dictionary<string, object> args)
    {
        var sb = new StringBuilder("knocks back");
        if (TryGetString(args, "direction", out var direction))
            sb.Append($" {direction}");
        if (TryGetNumber(args, "force", out var force))
            sb.Append($" (force {FormatNumber(force)})");
        return sb.ToString();
    }

    private static bool TryGetNumber(Dictionary<string, object> args, string key, out double value)
    {
        value = 0;
        if (args == null || !args.TryGetValue(key, out var obj) || obj == null)
            return false;

        switch (obj)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            case string s when double.TryParse(s, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetString(Dictionary<string, object> args, string key, out string value)
    {
        value = null;
        if (args == null || !args.TryGetValue(key, out var obj) || obj == null)
            return false;

        value = obj.ToString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string JoinWithAnd(List<string> parts)
    {
        if (parts.Count == 1)
            return parts[0];
        return $"{parts[0]} and {parts[1]}";
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.01)
            return Math.Round(value).ToString("0");
        return value.ToString("0.##");
    }

    private static string TrimToMax(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= max)
            return text;
        return text.Substring(0, max - 3) + "...";
    }
}
