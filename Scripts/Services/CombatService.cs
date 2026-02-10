using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.Execution;
using Lexmancer.Abilities.Visuals;
using Lexmancer.Core;
using Lexmancer.UI;
using Lexmancer.Combat;

namespace Lexmancer.Services;

/// <summary>
/// Unified combat service - manages damage, healing, status effects, and knockback.
/// Replaces static DamageSystem and singleton StatusEffectManager.
/// This is a Godot autoload service accessible via ServiceLocator.
/// </summary>
public partial class CombatService : Node
{
	// ==================== CHILD SYSTEMS ====================

	/// <summary>
	/// Status effect manager (no longer singleton)
	/// Also handles knockback internally
	/// </summary>
	public StatusEffectManager StatusEffects { get; private set; }

	// ==================== CONFIGURATION ====================

	/// <summary>
	/// Global damage multiplier (for difficulty scaling)
	/// </summary>
	public float DamageMultiplier { get; set; } = 1.0f;

	/// <summary>
	/// Global knockback force multiplier
	/// </summary>
	public float KnockbackMultiplier { get; set; } = 1.0f;

	// ==================== INITIALIZATION ====================

	public override void _Ready()
	{
		GD.Print("CombatService initializing...");

		// Create status effect manager as child
		StatusEffects = new StatusEffectManager
		{
			Name = "StatusEffectManager"
		};
		AddChild(StatusEffects);

		GD.Print("CombatService initialized");
	}

	// StatusEffectManager handles its own _Process for status effects and knockback

	// ==================== DAMAGE SYSTEM ====================

	/// <summary>
	/// Apply damage to a node with HealthComponent
	/// </summary>
	public bool ApplyDamage(Node target, float amount, string element = "neutral", Node source = null)
	{
		if (target == null || amount <= 0)
			return false;

		// Check for invulnerability (e.g., during dash)
		if (target.HasMeta("invulnerable") && (bool)target.GetMeta("invulnerable"))
		{
			GD.Print($"{target.Name} is invulnerable, damage blocked!");
			return false;
		}

		// Try to find HealthComponent in target or its children
		HealthComponent health = FindHealthComponent(target);

		if (health == null)
		{
			GD.PrintErr($"No HealthComponent found on {target.Name}");
			return false;
		}

		// Apply elemental modifiers and global multiplier
		float finalDamage = CalculateDamage(amount, element, target) * DamageMultiplier;

		health.TakeDamage(finalDamage);

		// Get position for event
		Vector2 position = (target is Node2D target2D) ? target2D.GlobalPosition : Vector2.Zero;

		// Emit damage event
		EventBus.Instance?.EmitSignal(EventBus.SignalName.EntityDamaged, target, finalDamage, element, position);

		// Spawn floating damage number
		SpawnDamageNumber(target, finalDamage, DamageNumberType.Normal);

		return true;
	}

	/// <summary>
	/// Apply healing to a node with HealthComponent
	/// </summary>
	public bool ApplyHealing(Node target, float amount, Node source = null)
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

		// Get position for event
		Vector2 position = (target is Node2D target2D) ? target2D.GlobalPosition : Vector2.Zero;

		// TODO: Add EntityHealed signal to EventBus if needed
		// EventBus.Instance?.EmitSignal(EventBus.SignalName.EntityHealed, target, amount, position);

		// Spawn floating heal number
		SpawnDamageNumber(target, amount, DamageNumberType.Healing);

		return true;
	}

	/// <summary>
	/// Calculate final damage with elemental modifiers
	/// </summary>
	private float CalculateDamage(float baseDamage, string element, Node target)
	{
		// TODO: Add elemental weakness/resistance system
		// For now, just return base damage
		return baseDamage;
	}

	/// <summary>
	/// Find HealthComponent in target or its children
	/// </summary>
	private HealthComponent FindHealthComponent(Node target)
	{
		// Check metadata first (most common path)
		if (target.HasMeta("health_component"))
		{
			var health = target.GetMeta("health_component").As<HealthComponent>();
			if (health != null)
				return health;
		}

		// Try getting directly
		if (target is HealthComponent healthDirect)
			return healthDirect;

		// Search children
		foreach (Node child in target.GetChildren())
		{
			if (child is HealthComponent healthChild)
				return healthChild;
		}

		return null;
	}

	/// <summary>
	/// Spawn floating damage number
	/// </summary>
	private void SpawnDamageNumber(Node target, float amount, DamageNumberType type)
	{
		if (target is not Node2D target2D)
			return;

		// Find world node (root of scene tree)
		var worldNode = GetTree()?.Root?.GetNode("Main");
		if (worldNode == null)
		{
			// Fallback: use current scene root
			worldNode = GetTree()?.CurrentScene;
		}

		if (worldNode != null)
		{
			// Spawn slightly above the target
			var spawnPosition = target2D.GlobalPosition + new Vector2(0, -20);
			DamageNumber.Spawn(worldNode, spawnPosition, amount, type);
		}
	}

	// ==================== KNOCKBACK SYSTEM (Delegates) ====================

	/// <summary>
	/// Apply knockback force to a target (delegates to StatusEffects)
	/// </summary>
	public void ApplyKnockback(Node target, Vector2 direction, float force, float duration = 0.3f)
	{
		// Apply global multiplier
		float modifiedForce = force * KnockbackMultiplier;
		StatusEffects?.ApplyKnockback(target, direction, modifiedForce, duration);
	}

	/// <summary>
	/// Get current knockback velocity for a target (delegates to StatusEffects)
	/// </summary>
	public Vector2 GetKnockbackVelocity(Node target)
	{
		return StatusEffects?.GetKnockbackVelocity(target) ?? Vector2.Zero;
	}

	// ==================== STATUS EFFECTS (Delegates) ====================

	/// <summary>
	/// Apply status effect to target (delegates to StatusEffects manager)
	/// </summary>
	public void ApplyStatusEffect(Node target, StatusEffectType type, float duration, int stacks = 1, Node source = null)
	{
		StatusEffects?.ApplyEffect(target, type, duration, stacks, source);
	}

	/// <summary>
	/// Remove status effect from target
	/// </summary>
	public void RemoveStatusEffect(Node target, StatusEffectType type)
	{
		StatusEffects?.RemoveEffect(target, type);
	}

	/// <summary>
	/// Check if target has a status effect
	/// </summary>
	public bool HasStatusEffect(Node target, StatusEffectType type)
	{
		return StatusEffects?.HasEffect(target, type) ?? false;
	}

	/// <summary>
	/// Get movement speed multiplier for target (accounting for status effects)
	/// </summary>
	public float GetMovementMultiplier(Node target)
	{
		return StatusEffects?.GetMovementMultiplier(target) ?? 1.0f;
	}

	// ==================== UTILITIES ====================

	/// <summary>
	/// Clear all combat state for a target (on death)
	/// </summary>
	public void ClearCombatState(Node target)
	{
		if (target == null)
			return;

		// Clear all status effects and knockback (StatusEffects handles both)
		StatusEffects?.ClearAllStatuses(target);

		GD.Print($"Cleared combat state for {target.Name}");
	}

	/// <summary>
	/// Get combat statistics (for debugging)
	/// </summary>
	public int GetActiveEffectCount()
	{
		// Count all active status effects across all entities
		return StatusEffects?.GetActiveEffectCount() ?? 0;
	}

	/// <summary>
	/// Print combat service status to console
	/// </summary>
	public void PrintStatus()
	{
		GD.Print("=== CombatService Status ===");
		GD.Print($"Damage Multiplier: {DamageMultiplier}");
		GD.Print($"Knockback Multiplier: {KnockbackMultiplier}");
		GD.Print($"Status Effects Manager: {(StatusEffects != null ? "✓" : "✗")}");
		if (StatusEffects != null)
		{
			GD.Print($"Active Status Effects: {StatusEffects.GetActiveEffectCount()}");
		}
		GD.Print("===========================");
	}
}
