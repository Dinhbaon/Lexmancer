using Godot;

namespace Lexmancer.Core;

/// <summary>
/// Interface for entities that can be affected by movement-based status effects
/// </summary>
public interface IMoveable
{
    /// <summary>
    /// Get the CharacterBody2D for this entity
    /// </summary>
    CharacterBody2D GetBody();

    /// <summary>
    /// Get the base movement speed (before status effect modifiers)
    /// </summary>
    float GetBaseMoveSpeed();
}
