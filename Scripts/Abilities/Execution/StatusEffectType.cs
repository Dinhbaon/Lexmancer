using System;
using System.Collections.Generic;
using System.Linq;

namespace Lexmancer.Abilities.Execution;

public enum StatusEffectType
{
    Burning,
    Frozen,
    Poisoned,
    Shocked,
    Slowed,
    Stunned,
    Weakened,
    Feared
}

public static class StatusEffectTypeUtil
{
    private static readonly Dictionary<string, StatusEffectType> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["burning"] = StatusEffectType.Burning,
        ["frozen"] = StatusEffectType.Frozen,
        ["poisoned"] = StatusEffectType.Poisoned,
        ["shocked"] = StatusEffectType.Shocked,
        ["slowed"] = StatusEffectType.Slowed,
        ["stunned"] = StatusEffectType.Stunned,
        ["weakened"] = StatusEffectType.Weakened,
        ["feared"] = StatusEffectType.Feared,
        ["poison"] = StatusEffectType.Poisoned,
        ["freeze"] = StatusEffectType.Frozen,
        ["shock"] = StatusEffectType.Shocked
    };

    private static readonly string[] OrderedIds = Map
        .Where(kvp => kvp.Key == kvp.Value.ToString().ToLowerInvariant())
        .Select(kvp => kvp.Key)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static bool TryParse(string raw, out StatusEffectType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return Map.TryGetValue(raw.Trim(), out type);
    }

    public static string ToId(StatusEffectType type)
    {
        return type.ToString().ToLowerInvariant();
    }

    public static IReadOnlyList<string> GetIds()
    {
        return OrderedIds;
    }
}
