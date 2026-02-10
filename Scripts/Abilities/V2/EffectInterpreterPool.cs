using Godot;
using System.Collections.Concurrent;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Simple pool to reuse EffectInterpreter instances per-world to reduce GC churn
/// during multi-hit abilities.
/// </summary>
public static class EffectInterpreterPool
{
    private static readonly ConcurrentDictionary<ulong, EffectInterpreter> cache = new();

    public static EffectInterpreter Get(Node worldNode)
    {
        if (worldNode == null)
            return new EffectInterpreter(null);

        var id = worldNode.GetInstanceId();
        return cache.GetOrAdd(id, _ => new EffectInterpreter(worldNode));
    }

    /// <summary>
    /// Optional cleanup if worlds are unloaded.
    /// </summary>
    public static void Clear(Node worldNode)
    {
        if (worldNode == null)
            return;

        cache.TryRemove(worldNode.GetInstanceId(), out _);
    }

    /// <summary>
    /// Clear all cached interpreters (useful on shutdown).
    /// </summary>
    public static void ClearAll() => cache.Clear();
}
