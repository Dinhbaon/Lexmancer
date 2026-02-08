using Godot;
using System.Collections.Generic;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Context passed to effect execution containing position, targets, and runtime data
/// </summary>
public class EffectContext
{
    /// <summary>
    /// Position where effect originates
    /// </summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Direction the effect is aimed
    /// </summary>
    public Vector2 Direction { get; set; }

    /// <summary>
    /// Current target (for damage, status effects, etc.)
    /// </summary>
    public Node Target { get; set; }

    /// <summary>
    /// Entity that cast the ability
    /// </summary>
    public Node Caster { get; set; }

    /// <summary>
    /// Runtime variables for formulas and conditions
    /// </summary>
    public Dictionary<string, object> Variables { get; set; } = new();

    /// <summary>
    /// World node for spawning effects
    /// </summary>
    public Node WorldNode { get; set; }

    /// <summary>
    /// The ability being executed (for visual/element info)
    /// </summary>
    public AbilityV2 Ability { get; set; }

    /// <summary>
    /// Create a copy of this context with modified values
    /// </summary>
    public EffectContext With(
        Vector2? position = null,
        Vector2? direction = null,
        Node target = null,
        Node caster = null)
    {
        return new EffectContext
        {
            Position = position ?? Position,
            Direction = direction ?? Direction,
            Target = target ?? Target,
            Caster = caster ?? Caster,
            WorldNode = WorldNode,
            Variables = new Dictionary<string, object>(Variables),
            Ability = Ability // Preserve ability reference
        };
    }
}
