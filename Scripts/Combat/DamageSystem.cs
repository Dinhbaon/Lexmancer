using Godot;
using System;

namespace Lexmancer.Combat;

/// <summary>
/// Centralized damage calculation and application
/// </summary>
public static class DamageSystem
{
	/// <summary>
	/// Apply damage to a node with HealthComponent
	/// </summary>
	public static bool ApplyDamage(Node target, float amount, string element = "neutral", Node source = null)
	{
		if (target == null || amount <= 0)
			return false;

		// Try to find HealthComponent in target or its children
		HealthComponent health = FindHealthComponent(target);

		if (health == null)
		{
			GD.PrintErr($"No HealthComponent found on {target.Name}");
			return false;
		}

		// Apply elemental modifiers (for future expansion)
		float finalDamage = CalculateDamage(amount, element, target);

		health.TakeDamage(finalDamage);
		return true;
	}

	/// <summary>
	/// Apply healing to a node with HealthComponent
	/// </summary>
	public static bool ApplyHealing(Node target, float amount)
	{
		if (target == null || amount <= 0)
			return false;

		HealthComponent health = FindHealthComponent(target);

		if (health == null)
		{
			GD.PrintErr($"No HealthComponent found on {target.Name}");
			return false;
		}

		health.Heal(amount);
		return true;
	}

	/// <summary>
	/// Find HealthComponent in node
	/// </summary>
	private static HealthComponent FindHealthComponent(Node node)
	{
		// Look for HealthComponent as a child node
		return node.GetNodeOrNull<HealthComponent>("HealthComponent");
	}

	/// <summary>
	/// Calculate final damage with elemental modifiers
	/// For vertical slice, just return base damage
	/// </summary>
	private static float CalculateDamage(float baseDamage, string element, Node target)
	{
		// TODO: Add elemental resistances/weaknesses
		// For now, just return base damage
		return baseDamage;
	}

	/// <summary>
	/// Check if node has health component
	/// </summary>
	public static bool HasHealthComponent(Node node)
	{
		return FindHealthComponent(node) != null;
	}

	/// <summary>
	/// Get current health of a node
	/// </summary>
	public static float GetCurrentHealth(Node node)
	{
		var health = FindHealthComponent(node);
		return health?.Current ?? 0;
	}

	/// <summary>
	/// Check if node is alive
	/// </summary>
	public static bool IsAlive(Node node)
	{
		var health = FindHealthComponent(node);
		return health?.IsAlive ?? false;
	}
}
