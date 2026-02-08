using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Defines the elemental primitives that can be combined to create abilities
/// </summary>
public enum PrimitiveType
{
    // Core Elements
    Fire,       // Burns, spreads, explosive
    Ice,        // Slows, freezes, defensive
    Lightning,  // Fast, chains, high damage
    Poison,     // Stacking DoT, lingering
    Earth,      // Heavy, knockback, shields
    Wind,       // Fast, pushing, mobility
    Shadow,     // Life drain, debuffs, fear
    Light       // Healing, buffs, purification
}

/// <summary>
/// Defines how an ability is delivered to targets
/// </summary>
public enum DeliveryMethod
{
    Projectile, // Fires a projectile at target
    Area,       // Creates an area effect
    Beam,       // Shoots a continuous beam
    Touch       // Melee or touch range attack
}

/// <summary>
/// Behavior modifiers that affect how abilities work
/// </summary>
public enum BehaviorModifier
{
    Chain,      // Chains to multiple targets
    Pierce,     // Pierces through targets
    Explode,    // Explodes on impact
    Bounce      // Bounces off surfaces
}

/// <summary>
/// Represents a complete ability composition with elements, delivery, and modifiers
/// </summary>
public class AbilityComposition
{
    public List<PrimitiveType> Elements { get; set; } = new();
    public DeliveryMethod? Delivery { get; set; }
    public List<BehaviorModifier> Modifiers { get; set; } = new();

    public string GetComboKey()
    {
        var parts = new List<string>();

        var sortedElements = Elements.OrderBy(e => e).Select(e => e.ToString());
        parts.AddRange(sortedElements);

        if (Delivery.HasValue)
            parts.Add(Delivery.Value.ToString());

        var sortedModifiers = Modifiers.OrderBy(m => m).Select(m => m.ToString());
        parts.AddRange(sortedModifiers);

        return string.Join("+", parts);
    }
}

/// <summary>
/// Static helper for primitive information
/// </summary>
public static class PrimitiveInfo
{
    public static readonly Dictionary<PrimitiveType, string> Descriptions = new()
    {
        { PrimitiveType.Fire, "Burns targets with spreading flames and explosive power" },
        { PrimitiveType.Ice, "Freezes and slows enemies with chilling cold" },
        { PrimitiveType.Lightning, "Strikes with electrifying speed and chaining energy" },
        { PrimitiveType.Poison, "Corrupts with stacking venom and lingering toxins" },
        { PrimitiveType.Earth, "Crushes with heavy rocks and protective barriers" },
        { PrimitiveType.Wind, "Strikes with swift gusts and forceful currents" },
        { PrimitiveType.Shadow, "Drains life force and spreads darkness and fear" },
        { PrimitiveType.Light, "Heals allies and purifies with radiant energy" }
    };

    public static readonly Dictionary<DeliveryMethod, string> DeliveryDescriptions = new()
    {
        { DeliveryMethod.Projectile, "Fires a projectile at target" },
        { DeliveryMethod.Area, "Creates an area effect" },
        { DeliveryMethod.Beam, "Shoots a continuous beam" },
        { DeliveryMethod.Touch, "Melee or touch range attack" }
    };

    public static readonly Dictionary<BehaviorModifier, string> ModifierDescriptions = new()
    {
        { BehaviorModifier.Chain, "Chains to multiple targets" },
        { BehaviorModifier.Pierce, "Pierces through targets" },
        { BehaviorModifier.Explode, "Explodes on impact" },
        { BehaviorModifier.Bounce, "Bounces off surfaces" }
    };

    public static string GetComboKey(List<PrimitiveType> primitives)
    {
        // Sort to make order-agnostic (Fire+Ice = Ice+Fire)
        var sorted = new List<PrimitiveType>(primitives);
        sorted.Sort();
        return string.Join("+", sorted);
    }
}
