using System.Collections.Generic;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Represents a scriptable effect containing a sequence of actions
/// </summary>
public class EffectScript
{
    /// <summary>
    /// List of actions to execute in order
    /// </summary>
    public List<EffectAction> Script { get; set; } = new();
}
