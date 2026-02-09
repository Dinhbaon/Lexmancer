using Godot;
using System.Collections.Generic;
using Lexmancer.Abilities.Visuals;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Manages status effects on entities
/// </summary>
public partial class StatusEffectManager : Node
{
    public static StatusEffectManager Instance { get; private set; }

    // Entity -> Status -> Stack count
    private Dictionary<Node, Dictionary<StatusEffectType, StatusEffect>> activeEffects = new();

    // Entity -> Visual component for status effects
    private Dictionary<Node, StatusEffectVisuals> entityVisuals = new();

    public override void _EnterTree()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            GD.PrintErr("Multiple StatusEffectManager instances detected!");
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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
                    GD.Print($"Status expired: {StatusEffectTypeUtil.ToId(statusKv.Key)} on {entity.Name}");
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
                    Combat.DamageSystem.ApplyDamage(entity, 3f * delta, "fire");
                    break;

                case StatusEffectType.Poisoned:
                    // 2 damage per second
                    Combat.DamageSystem.ApplyDamage(entity, 2f * delta, "poison");
                    break;

                case StatusEffectType.Shocked:
                    // 1 damage per second + small random knockback
                    Combat.DamageSystem.ApplyDamage(entity, 1f * delta, "lightning");
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

        // Check for complete movement lockdown
        if (HasStatus(body, StatusEffectType.Frozen) || HasStatus(body, StatusEffectType.Stunned))
        {
            body.Velocity = Vector2.Zero;
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

        // Apply final velocity
        body.Velocity = direction * entity.GetBaseMoveSpeed() * speedMultiplier;
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
            GD.Print($"Applied {StatusEffectTypeUtil.ToId(status)} to {entity.Name}");

            // Create visual effect
            var visuals = GetOrCreateVisuals(entity);
            if (visuals != null)
            {
                visuals.AddStatusVisual(StatusEffectTypeUtil.ToId(status), duration);
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
}
