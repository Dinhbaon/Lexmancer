using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.Visuals;
using Lexmancer.Core;
using Lexmancer.Services;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Manages status effects on entities
/// Now emits EventBus events for status application/expiration
/// NO LONGER A SINGLETON - Managed by CombatService
/// Access via ServiceLocator.Combat.StatusEffects
/// </summary>
public partial class StatusEffectManager : Node
{
    // Entity -> Status -> Stack count
    private Dictionary<Node, Dictionary<StatusEffectType, StatusEffect>> activeEffects = new();

    // Entity -> Visual component for status effects
    private Dictionary<Node, StatusEffectVisuals> entityVisuals = new();

    // Entity -> Knockback velocity (decays over time)
    private Dictionary<Node, KnockbackData> activeKnockbacks = new();

    private class KnockbackData
    {
        public Vector2 Velocity { get; set; }
        public float TimeRemaining { get; set; }
    }

    public class StatusEffect
    {
        public StatusEffectType Status { get; set; }
        public float TimeRemaining { get; set; }
        public int Stacks { get; set; } = 1;
        public bool CanStack { get; set; }
    }

    public override void _Process(double delta)
    {
        // Update knockback velocities
        UpdateKnockbacks((float)delta);

        // Update all active effects
        var toRemove = new List<(Node, StatusEffectType)>();
        var invalidEntities = new List<Node>();

        foreach (var entityKv in activeEffects)
        {
            var entity = entityKv.Key;
            var statuses = entityKv.Value;

            // Check if entity is still valid (not freed/disposed)
            if (!GodotObject.IsInstanceValid(entity) || entity.IsQueuedForDeletion())
            {
                invalidEntities.Add(entity);
                continue;
            }

            // Apply status effects before updating timers
            ApplyStatusEffects(entity, statuses, (float)delta);

            foreach (var statusKv in statuses)
            {
                var status = statusKv.Value;
                status.TimeRemaining -= (float)delta;

                if (status.TimeRemaining <= 0)
                {
                    toRemove.Add((entity, statusKv.Key));
                    string statusId = StatusEffectTypeUtil.ToId(statusKv.Key);
                    GD.Print($"Status expired: {statusId} on {entity.Name}");
                    // Emit expiration event
                    EventBus.Instance?.EmitSignal(EventBus.SignalName.StatusEffectExpired, entity, statusId);
                }
            }
        }

        // Clean up invalid/freed entities
        foreach (var entity in invalidEntities)
        {
            activeEffects.Remove(entity);

            // Clean up visuals
            if (entityVisuals.ContainsKey(entity))
            {
                if (IsInstanceValid(entityVisuals[entity]))
                {
                    entityVisuals[entity].QueueFree();
                }
                entityVisuals.Remove(entity);
            }

            GD.Print($"Removed status effects from freed entity");
        }

        // Clean up expired effects
        foreach (var (entity, status) in toRemove)
        {
            activeEffects[entity].Remove(status);

            // Remove visual for this status
            if (entityVisuals.ContainsKey(entity) && IsInstanceValid(entityVisuals[entity]))
            {
                entityVisuals[entity].RemoveStatusVisual(StatusEffectTypeUtil.ToId(status));
            }

            if (activeEffects[entity].Count == 0)
            {
                activeEffects.Remove(entity);
                // Reset visual modulation when all effects cleared
                ResetVisualModulation(entity);

                // Remove visual component
                if (entityVisuals.ContainsKey(entity))
                {
                    if (IsInstanceValid(entityVisuals[entity]))
                    {
                        entityVisuals[entity].QueueFree();
                    }
                    entityVisuals.Remove(entity);
                }
            }
        }
    }

    /// <summary>
    /// Apply gameplay effects based on active statuses
    /// </summary>
    private void ApplyStatusEffects(Node entity, Dictionary<StatusEffectType, StatusEffect> statuses, float delta)
    {
        // Visual effects are now handled by StatusEffectVisuals component
        // No need to manually update modulation here

        foreach (var statusKv in statuses)
        {
            var status = statusKv.Value;

            switch (statusKv.Key)
            {
                case StatusEffectType.Burning:
                    // 3 damage per second
                    ServiceLocator.Instance.Combat.ApplyDamage(entity, 3f * delta, "fire");
                    break;

                case StatusEffectType.Poisoned:
                    // 2 damage per second
                    ServiceLocator.Instance.Combat.ApplyDamage(entity, 2f * delta, "poison");
                    break;

                case StatusEffectType.Shocked:
                    // 1 damage per second + small random knockback
                    ServiceLocator.Instance.Combat.ApplyDamage(entity, 1f * delta, "lightning");
                    break;

                // Movement-based effects are now handled via ApplyMovementEffects()
                case StatusEffectType.Frozen:
                case StatusEffectType.Slowed:
                case StatusEffectType.Stunned:
                case StatusEffectType.Feared:
                case StatusEffectType.Weakened:
                    // These are applied when entity calls ApplyMovementEffects()
                    break;
            }
        }
    }

    /// <summary>
    /// Update and decay knockback velocities
    /// </summary>
    private void UpdateKnockbacks(float delta)
    {
        var toRemove = new List<Node>();

        foreach (var kv in activeKnockbacks)
        {
            var entity = kv.Key;
            var knockback = kv.Value;

            // Check if entity is still valid
            if (!GodotObject.IsInstanceValid(entity) || entity.IsQueuedForDeletion())
            {
                toRemove.Add(entity);
                continue;
            }

            // Decay knockback over time
            knockback.TimeRemaining -= delta;

            if (knockback.TimeRemaining <= 0)
            {
                toRemove.Add(entity);
            }
        }

        // Clean up expired knockbacks
        foreach (var entity in toRemove)
        {
            activeKnockbacks.Remove(entity);
        }
    }

    /// <summary>
    /// Apply knockback to an entity
    /// This creates a sustained knockback that decays over time
    /// </summary>
    public void ApplyKnockback(Node entity, Vector2 direction, float force, float duration = 0.3f)
    {
        if (!GodotObject.IsInstanceValid(entity))
            return;

        var velocity = direction.Normalized() * force;

        if (activeKnockbacks.ContainsKey(entity))
        {
            // Add to existing knockback
            var existing = activeKnockbacks[entity];
            existing.Velocity += velocity;
            existing.TimeRemaining = Mathf.Max(existing.TimeRemaining, duration);
        }
        else
        {
            // Create new knockback
            activeKnockbacks[entity] = new KnockbackData
            {
                Velocity = velocity,
                TimeRemaining = duration
            };
        }

        GD.Print($"Applied knockback: {velocity} for {duration}s to {entity.Name}");

        // Emit event
        EventBus.Instance?.EmitSignal(EventBus.SignalName.KnockbackApplied, entity, direction, force);
    }

    /// <summary>
    /// Get current knockback velocity for an entity
    /// </summary>
    public Vector2 GetKnockbackVelocity(Node entity)
    {
        if (activeKnockbacks.ContainsKey(entity))
        {
            var knockback = activeKnockbacks[entity];
            // Decay velocity over time for smooth deceleration
            float t = knockback.TimeRemaining / 0.3f; // Assume 0.3s default duration
            return knockback.Velocity * t;
        }
        return Vector2.Zero;
    }

    /// <summary>
    /// Apply movement-based status effects to an entity
    /// Call this from the entity's _PhysicsProcess after calculating intended direction
    /// </summary>
    /// <param name="entity">The moveable entity (must implement IMoveable)</param>
    /// <param name="intendedDirection">The normalized direction the entity wants to move</param>
    public void ApplyMovementEffects(Core.IMoveable entity, Vector2 intendedDirection)
    {
        var body = entity.GetBody();
        if (body == null || !GodotObject.IsInstanceValid(body))
            return;

        // Always apply knockback velocity first (even if stunned/frozen)
        Vector2 knockbackVel = GetKnockbackVelocity(body);

        // Check for complete movement lockdown
        if (HasStatus(body, StatusEffectType.Frozen) || HasStatus(body, StatusEffectType.Stunned))
        {
            // Still apply knockback, but no AI movement
            body.Velocity = knockbackVel;
            return;
        }

        // Calculate movement modifiers
        float speedMultiplier = 1.0f;
        Vector2 direction = intendedDirection;

        // Slowed - reduce speed by 50%
        if (HasStatus(body, StatusEffectType.Slowed))
        {
            speedMultiplier *= 0.5f;
        }

        // Feared - reverse direction
        if (HasStatus(body, StatusEffectType.Feared))
        {
            direction = -direction;
        }

        // Weakened - reduce speed by 25% (can stack with slowed)
        if (HasStatus(body, StatusEffectType.Weakened))
        {
            speedMultiplier *= 0.75f;
        }

        // Apply final velocity (AI movement + knockback)
        Vector2 aiVelocity = direction * entity.GetBaseMoveSpeed() * speedMultiplier;
        body.Velocity = aiVelocity + knockbackVel;
    }

    /// <summary>
    /// Reset visual modulation to default
    /// </summary>
    private void ResetVisualModulation(Node entity)
    {
        // Validate entity before accessing
        if (!GodotObject.IsInstanceValid(entity) || entity.IsQueuedForDeletion())
            return;

        var visual = entity.GetNodeOrNull<ColorRect>("Visual");
        if (visual != null && GodotObject.IsInstanceValid(visual))
        {
            visual.Modulate = new Color(1, 1, 1);
        }
    }

    /// <summary>
    /// Get or create status effect visuals for an entity
    /// </summary>
    private StatusEffectVisuals GetOrCreateVisuals(Node entity)
    {
        // Return existing visuals if already created
        if (entityVisuals.ContainsKey(entity) && IsInstanceValid(entityVisuals[entity]))
        {
            return entityVisuals[entity];
        }

        // Create new visual component
        var visuals = new StatusEffectVisuals();
        visuals.Name = "StatusEffectVisuals";

        // Attach to entity if it's a Node2D
        if (entity is Node2D entity2D)
        {
            entity2D.AddChild(visuals);
            visuals.AttachToEntity(entity2D);
            entityVisuals[entity] = visuals;

            GD.Print($"Created StatusEffectVisuals for {entity.Name}");
            return visuals;
        }

        GD.PrintErr($"Cannot create visuals for non-Node2D entity: {entity.Name}");
        return null;
    }

    /// <summary>
    /// Apply a status effect to an entity
    /// </summary>
    public void ApplyStatus(Node entity, string status, float duration, bool canStack = false)
    {
        if (!StatusEffectTypeUtil.TryParse(status, out var parsedStatus))
        {
            GD.PrintErr($"Invalid status: {status}");
            return;
        }

        ApplyStatus(entity, parsedStatus, duration, canStack);
    }

    public void ApplyStatus(Node entity, StatusEffectType status, float duration, bool canStack = false)
    {

        if (!activeEffects.ContainsKey(entity))
        {
            activeEffects[entity] = new Dictionary<StatusEffectType, StatusEffect>();
        }

        var statuses = activeEffects[entity];

        if (statuses.ContainsKey(status))
        {
            var existing = statuses[status];

            if (canStack)
            {
                existing.Stacks++;
                existing.TimeRemaining = duration; // Refresh duration
                GD.Print($"Stacked {StatusEffectTypeUtil.ToId(status)} on {entity.Name} (stacks: {existing.Stacks})");
            }
            else
            {
                // Just refresh duration
                existing.TimeRemaining = duration;
                GD.Print($"Refreshed {StatusEffectTypeUtil.ToId(status)} on {entity.Name}");
            }

            // Refresh visuals
            var visuals = GetOrCreateVisuals(entity);
            if (visuals != null)
            {
                visuals.AddStatusVisual(StatusEffectTypeUtil.ToId(status), duration);
            }
        }
        else
        {
            // New status
            statuses[status] = new StatusEffect
            {
                Status = status,
                TimeRemaining = duration,
                Stacks = 1,
                CanStack = canStack
            };
            string statusId = StatusEffectTypeUtil.ToId(status);
            GD.Print($"Applied {statusId} to {entity.Name}");

            // Emit event
            EventBus.Instance?.EmitSignal(EventBus.SignalName.StatusEffectApplied, entity, statusId, duration);

            // Create visual effect
            var visuals = GetOrCreateVisuals(entity);
            if (visuals != null)
            {
                visuals.AddStatusVisual(statusId, duration);
            }
        }
    }

    /// <summary>
    /// Check if entity has a status
    /// </summary>
    public bool HasStatus(Node entity, string status)
    {
        return StatusEffectTypeUtil.TryParse(status, out var parsedStatus) &&
               HasStatus(entity, parsedStatus);
    }

    public bool HasStatus(Node entity, StatusEffectType status)
    {
        return activeEffects.ContainsKey(entity) && activeEffects[entity].ContainsKey(status);
    }

    /// <summary>
    /// Get number of stacks for a status
    /// </summary>
    public int GetStatusStacks(Node entity, string status)
    {
        return StatusEffectTypeUtil.TryParse(status, out var parsedStatus)
            ? GetStatusStacks(entity, parsedStatus)
            : 0;
    }

    public int GetStatusStacks(Node entity, StatusEffectType status)
    {
        if (activeEffects.ContainsKey(entity) && activeEffects[entity].ContainsKey(status))
        {
            return activeEffects[entity][status].Stacks;
        }
        return 0;
    }

    /// <summary>
    /// Clear a specific status from entity
    /// </summary>
    public void ClearStatus(Node entity, string status)
    {
        if (!StatusEffectTypeUtil.TryParse(status, out var parsedStatus))
        {
            return;
        }

        ClearStatus(entity, parsedStatus);
    }

    public void ClearStatus(Node entity, StatusEffectType status)
    {
        if (activeEffects.ContainsKey(entity))
        {
            activeEffects[entity].Remove(status);

            // Remove visual
            if (entityVisuals.ContainsKey(entity) && IsInstanceValid(entityVisuals[entity]))
            {
                entityVisuals[entity].RemoveStatusVisual(StatusEffectTypeUtil.ToId(status));
            }

            GD.Print($"Cleared {StatusEffectTypeUtil.ToId(status)} from {entity.Name}");
        }
    }
    /// <summary>
    /// Clear all statuses from entity
    /// </summary>
    public void ClearAllStatuses(Node entity)
    {
        activeEffects.Remove(entity);

        // Clear all visuals
        if (entityVisuals.ContainsKey(entity))
        {
            if (IsInstanceValid(entityVisuals[entity]))
            {
                entityVisuals[entity].ClearAll();
                entityVisuals[entity].QueueFree();
            }
            entityVisuals.Remove(entity);
        }

        GD.Print($"Cleared all statuses from {entity.Name}");
    }

    // ==================== COMBATSERVICE COMPATIBILITY ====================

    /// <summary>
    /// Apply effect (alias for ApplyStatus for CombatService compatibility)
    /// </summary>
    public void ApplyEffect(Node target, StatusEffectType type, float duration, int stacks = 1, Node source = null)
    {
        ApplyStatus(target, type, duration, canStack: stacks > 1);
        // TODO: Handle stacks parameter if needed
    }

    /// <summary>
    /// Remove effect (alias for ClearStatus for CombatService compatibility)
    /// </summary>
    public void RemoveEffect(Node target, StatusEffectType type)
    {
        ClearStatus(target, type);
    }

    /// <summary>
    /// Has effect (alias for HasStatus for CombatService compatibility)
    /// </summary>
    public bool HasEffect(Node target, StatusEffectType type)
    {
        return HasStatus(target, type);
    }

    /// <summary>
    /// Clear all effects (alias for ClearAllStatuses for CombatService compatibility)
    /// </summary>
    public void ClearAllEffects(Node target)
    {
        ClearAllStatuses(target);
    }

    /// <summary>
    /// Get movement speed multiplier based on active status effects
    /// </summary>
    public float GetMovementMultiplier(Node target)
    {
        float multiplier = 1.0f;

        // Frozen or Stunned = no movement
        if (HasStatus(target, StatusEffectType.Frozen) || HasStatus(target, StatusEffectType.Stunned))
        {
            return 0.0f;
        }

        // Slowed - reduce speed by 50%
        if (HasStatus(target, StatusEffectType.Slowed))
        {
            multiplier *= 0.5f;
        }

        // Weakened - reduce speed by 25% (can stack with slowed)
        if (HasStatus(target, StatusEffectType.Weakened))
        {
            multiplier *= 0.75f;
        }

        return multiplier;
    }

    /// <summary>
    /// Get count of all active status effects across all entities
    /// </summary>
    public int GetActiveEffectCount()
    {
        int count = 0;
        foreach (var entityEffects in activeEffects.Values)
        {
            count += entityEffects.Count;
        }
        return count;
    }
}
