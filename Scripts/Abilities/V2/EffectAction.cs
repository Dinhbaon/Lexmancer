using System.Collections.Generic;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Represents a single scripted action in an effect
/// </summary>
public class EffectAction
{
    /// <summary>
    /// Action type: spawn_projectile, damage, apply_status, etc.
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Arguments for this action (flexible dictionary)
    /// </summary>
    public Dictionary<string, object> Args { get; set; } = new();

    /// <summary>
    /// Actions to execute when this effect hits a target (legacy, for non-area effects)
    /// </summary>
    public List<EffectAction> OnHit { get; set; } = new();

    /// <summary>
    /// Actions to execute when an enemy enters an area effect (area effects only)
    /// </summary>
    public List<EffectAction> OnEnter { get; set; } = new();

    /// <summary>
    /// Actions to execute on tick intervals for area effects (area effects only)
    /// </summary>
    public List<EffectAction> OnTick { get; set; } = new();

    /// <summary>
    /// Actions to execute when this effect expires
    /// </summary>
    public List<EffectAction> OnExpire { get; set; } = new();

    /// <summary>
    /// Optional condition for execution
    /// </summary>
    public EffectCondition Condition { get; set; }
}

/// <summary>
/// Conditional execution for effects
/// </summary>
public class EffectCondition
{
    /// <summary>
    /// Condition expression: "target.is_burning", "caster.health &lt; 0.5"
    /// </summary>
    public string If { get; set; }

    /// <summary>
    /// Modifications to apply if condition is true
    /// </summary>
    public Dictionary<string, object> Then { get; set; } = new();
}
