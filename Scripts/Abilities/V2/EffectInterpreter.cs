using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Lexmancer.Abilities.Execution;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Safe interpreter for effect scripts
/// Executes actions without arbitrary code execution
/// </summary>
public class EffectInterpreter
{
    private readonly Node worldNode;
    private int nestingDepth = 0;
    private const int MaxNestingDepth = 5;

    public EffectInterpreter(Node worldNode)
    {
        this.worldNode = worldNode;
    }

    /// <summary>
    /// Execute an effect action with the given context
    /// </summary>
    public void Execute(EffectAction action, EffectContext context)
    {
        // Prevent infinite recursion
        if (nestingDepth >= MaxNestingDepth)
        {
            GD.PrintErr($"Max nesting depth ({MaxNestingDepth}) exceeded!");
            return;
        }

        // Check condition if present
        if (action.Condition != null && !EvaluateCondition(action.Condition, context))
            return;

        nestingDepth++;
        try
        {
            ExecuteAction(action, context);
        }
        finally
        {
            nestingDepth--;
        }
    }

    private void ExecuteAction(EffectAction action, EffectContext context)
    {
        switch (action.Action.ToLower())
        {
            case "spawn_projectile":
                SpawnProjectile(action.Args, context, action.OnHit);
                break;

            case "spawn_area":
                SpawnArea(action.Args, context, action.OnHit, action.OnExpire);
                break;

            case "spawn_beam":
                SpawnBeam(action.Args, context, action.OnHit);
                break;

            case "damage":
                ApplyDamage(action.Args, context);
                break;

            case "apply_status":
                ApplyStatus(action.Args, context);
                break;

            case "knockback":
                ApplyKnockback(action.Args, context);
                break;

            case "heal":
                ApplyHeal(action.Args, context);
                break;

            case "chain_to_nearby":
                ChainToNearby(action.Args, context, action.OnHit);
                break;

            case "repeat":
                RepeatAction(action.Args, context, action.OnHit);
                break;

            case "on_hit":
                // on_hit is a container, execute its nested actions
                foreach (var nestedAction in action.OnHit)
                {
                    Execute(nestedAction, context);
                }
                break;

            case "on_expire":
                // on_expire is a container, execute its nested actions
                foreach (var nestedAction in action.OnExpire)
                {
                    Execute(nestedAction, context);
                }
                break;

            default:
                // LLM may generate creative actions that aren't implemented yet
                // Log as warning but don't error - just skip the action
                GD.Print($"⚠️  Skipping unsupported action: '{action.Action}' (not yet implemented)");
                GD.Print($"    Supported actions: spawn_projectile, spawn_area, spawn_beam, damage, heal, apply_status, knockback, chain_to_nearby, repeat");
                break;
        }
    }

    // ==================== SPAWNING ACTIONS ====================

    private void SpawnProjectile(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> onHit)
    {
        int count = GetArg(args, "count", 1);
        string pattern = GetArg(args, "pattern", "single");
        float speed = GetArg(args, "speed", 400f);
        float acceleration = GetArg(args, "acceleration", 0f);

        // Clamp values
        count = Math.Clamp(count, 1, 5);
        speed = Math.Clamp(speed, 50f, 1200f);
        acceleration = Math.Clamp(acceleration, -500f, 500f);

        for (int i = 0; i < count; i++)
        {
            Vector2 direction = CalculateDirection(pattern, i, count, ctx.Direction);

            // Create actual projectile node
            var projectile = new Abilities.Execution.ProjectileNodeV2();
            projectile.GlobalPosition = ctx.Position;
            projectile.Direction = direction;
            projectile.Speed = speed;
            projectile.Acceleration = acceleration;
            projectile.OnHitActions = onHit;
            projectile.Context = ctx;

            worldNode.AddChild(projectile);

            string accelInfo = acceleration != 0 ? $" (accel: {acceleration})" : "";
            GD.Print($"Spawned projectile #{i+1}/{count} at speed {speed}{accelInfo}");
        }
    }

    private void SpawnArea(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> onHit, List<EffectAction> onExpire)
    {
        float radius = GetArg(args, "radius", 100f);
        float duration = GetArg(args, "duration", 2f);
        int tickDamage = GetArg(args, "lingering_damage", 0);
        int damage = GetArg(args, "damage", 0);
        float growthTime = GetArg(args, "growth_time", 0f);

        // Clamp values
        radius = Math.Clamp(radius, 50f, 300f);
        duration = Math.Clamp(duration, 1f, 10f);
        tickDamage = Math.Clamp(tickDamage, 0, 20);
        growthTime = Math.Clamp(growthTime, 0f, 3f);

        // Create area effect node
        var areaNode = new Abilities.Execution.AreaEffectNode();
        areaNode.GlobalPosition = ctx.Position;
        areaNode.Radius = radius;
        areaNode.Duration = duration;
        areaNode.LingeringDamage = tickDamage;
        areaNode.GrowthTime = growthTime;
        areaNode.OnHitActions = onHit ?? new();
        areaNode.OnExpireActions = onExpire ?? new();
        areaNode.Context = ctx;

        worldNode.AddChild(areaNode);

        // If immediate damage specified, apply it now (only if instant, not growing)
        if (damage > 0 && growthTime <= 0)
        {
            var nearbyEnemies = FindNearbyEnemies(ctx.Position, radius, 20);
            foreach (var enemy in nearbyEnemies)
            {
                Combat.DamageSystem.ApplyDamage(enemy, damage);
            }
        }

        string growthInfo = growthTime > 0 ? $", growth={growthTime}s" : " (instant)";
        GD.Print($"Spawned area effect: radius={radius}, duration={duration}, tick={tickDamage}/s{growthInfo}");
    }

    private void SpawnBeam(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> onHit)
    {
        float length = GetArg(args, "length", 400f);
        float width = GetArg(args, "width", 20f);
        float duration = GetArg(args, "duration", 1f);
        float travelTime = GetArg(args, "travel_time", 0f);

        // Clamp values
        length = Math.Clamp(length, 200f, 800f);
        width = Math.Clamp(width, 10f, 50f);
        duration = Math.Clamp(duration, 0.5f, 3f);
        travelTime = Math.Clamp(travelTime, 0f, 2f);

        // Create beam node
        var beam = new Execution.BeamNode();
        beam.GlobalPosition = ctx.Position;
        beam.Direction = ctx.Direction;
        beam.Length = length;
        beam.Width = width;
        beam.Duration = duration;
        beam.TravelTime = travelTime;
        beam.OnHitActions = onHit;
        beam.Context = ctx;

        worldNode.AddChild(beam);

        string travelInfo = travelTime > 0 ? $", travel={travelTime}s" : " (instant)";
        GD.Print($"Spawned beam: length={length}, width={width}, duration={duration}{travelInfo}");
    }

    // ==================== DAMAGE ACTIONS ====================

    private void ApplyDamage(Dictionary<string, object> args, EffectContext ctx)
    {
        if (ctx.Target == null)
        {
            // Area damage if radius specified
            if (args.ContainsKey("area_radius"))
            {
                float radius = GetArg(args, "area_radius", 100f);
                var nearbyEnemies = FindNearbyEnemies(ctx.Position, radius, 20);

                foreach (var enemy in nearbyEnemies)
                {
                    float damage = EvaluateDamageFormula(args, ctx);
                    string element = GetArg(args, "element", "neutral");
                    damage = Math.Clamp(damage, 1f, 100f);

                    Combat.DamageSystem.ApplyDamage(enemy, damage, element, ctx.Caster);
                }

                GD.Print($"Applied AOE damage to {nearbyEnemies.Count} enemies in radius {radius}");
                return;
            }

            GD.PrintErr("No target for damage action");
            return;
        }

        // Calculate damage
        float finalDamage = EvaluateDamageFormula(args, ctx);
        string damageElement = GetArg(args, "element", "neutral");

        // Clamp damage
        finalDamage = Math.Clamp(finalDamage, 1f, 100f);

        // Apply damage to target
        Combat.DamageSystem.ApplyDamage(ctx.Target, finalDamage, damageElement, ctx.Caster);
    }

    private void ApplyStatus(Dictionary<string, object> args, EffectContext ctx)
    {
        if (ctx.Target == null)
        {
            GD.PrintErr("No target for status effect");
            return;
        }

        string status = GetArg(args, "status", "");
        float duration = GetArg(args, "duration", 3f);
        bool stacks = GetArg(args, "stacks", false);

        // Clamp duration
        duration = Math.Clamp(duration, 1f, 10f);

        // Validate status
        if (!StatusEffectTypeUtil.TryParse(status, out var statusType))
        {
            GD.PrintErr($"Invalid status: {status}");
            return;
        }

        // Get or create StatusEffectManager instance
        var statusManager = Execution.StatusEffectManager.Instance;
        if (statusManager == null)
        {
            // Create StatusEffectManager if it doesn't exist
            statusManager = new Execution.StatusEffectManager();
            statusManager.Name = "StatusEffectManager";
            worldNode.AddChild(statusManager);
            GD.Print("Created StatusEffectManager singleton");
        }

        // Apply status effect to target
        statusManager.ApplyStatus(ctx.Target, statusType, duration, stacks);
        GD.Print($"Applied {StatusEffectTypeUtil.ToId(statusType)} for {duration}s (stacks: {stacks}) to {ctx.Target.Name}");
    }

    // ==================== MODIFIER ACTIONS ====================

    private void ChainToNearby(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> chainedActions)
    {
        int maxChains = GetArg(args, "max_chains", 3);
        float range = GetArg(args, "range", 200f);

        // Clamp values
        maxChains = Math.Clamp(maxChains, 2, 5);
        range = Math.Clamp(range, 100f, 300f);

        // Find nearby enemies
        var nearbyEnemies = FindNearbyEnemies(ctx.Position, range, maxChains);

        GD.Print($"Chaining to {nearbyEnemies.Count} enemies within {range}px");

        // Execute chained actions on each enemy
        foreach (var enemy in nearbyEnemies)
        {
            // Cast to Node2D if needed for position
            var enemyPos = enemy is Node2D enemy2D ? enemy2D.Position : Vector2.Zero;
            var chainCtx = ctx.With(
                position: enemyPos,
                target: enemy
            );

            foreach (var action in chainedActions)
            {
                Execute(action, chainCtx);
            }
        }
    }

    private void ApplyKnockback(Dictionary<string, object> args, EffectContext ctx)
    {
        if (ctx.Target == null)
        {
            GD.PrintErr("No target for knockback");
            return;
        }

        float force = GetArg(args, "force", 300f);
        string direction = GetArg(args, "direction", "away");

        // Clamp force
        force = Math.Clamp(force, 100f, 500f);

        // Calculate knockback direction
        var targetPos = ctx.Target is Node2D t ? t.GlobalPosition : Vector2.Zero;
        var casterPos = ctx.Caster is Node2D c ? c.GlobalPosition : Vector2.Zero;

        Vector2 knockbackDir = direction.ToLower() switch
        {
            "away" => (targetPos - casterPos).Normalized(),
            "towards" => (casterPos - targetPos).Normalized(),
            "up" => Vector2.Up,
            _ => Vector2.Zero
        };

        // Apply physics impulse
        if (ctx.Target is CharacterBody2D body)
        {
            body.Velocity = knockbackDir * force;
            GD.Print($"Applied knockback: force={force}, direction={direction} to {ctx.Target.Name}");
        }
        else
        {
            GD.PrintErr($"Target {ctx.Target.Name} is not a CharacterBody2D, cannot apply knockback");
        }
    }

    private void ApplyHeal(Dictionary<string, object> args, EffectContext ctx)
    {
        if (ctx.Target == null && ctx.Caster == null)
        {
            GD.PrintErr("No target or caster for heal");
            return;
        }

        int amount = GetArg(args, "amount", 20);
        amount = Math.Clamp(amount, 10, 50);

        var healTarget = ctx.Target ?? ctx.Caster;

        // Apply healing
        Combat.DamageSystem.ApplyHealing(healTarget, amount);
    }

    private void RepeatAction(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> actions)
    {
        int count = GetArg(args, "count", 3);
        float interval = GetArg(args, "interval", 1f);

        count = Math.Clamp(count, 2, 5);
        interval = Math.Clamp(interval, 0.5f, 2f);

        GD.Print($"Repeat action {count} times with {interval}s interval");

        // Get or create DelayedActionExecutor
        var executor = worldNode.GetNodeOrNull<Execution.DelayedActionExecutor>("DelayedActionExecutor");
        if (executor == null)
        {
            executor = new Execution.DelayedActionExecutor();
            executor.Name = "DelayedActionExecutor";
            worldNode.AddChild(executor);
            GD.Print("Created DelayedActionExecutor");
        }

        // Schedule actions for delayed execution
        executor.ScheduleActions(actions, ctx, count, interval);
    }

    // ==================== HELPER METHODS ====================

    private float EvaluateDamageFormula(Dictionary<string, object> args, EffectContext ctx)
    {
        // If formula is specified, evaluate it
        if (args.TryGetValue("formula", out var formulaObj))
        {
            string formula = formulaObj.ToString();
            return EvaluateFormula(formula, ctx);
        }

        // Otherwise use amount
        return GetArg(args, "amount", 20f);
    }

    private float EvaluateFormula(string formula, EffectContext ctx)
    {
        // Use safe formula evaluator
        return FormulaEvaluator.Evaluate(formula, ctx);
    }

    private bool EvaluateCondition(EffectCondition condition, EffectContext ctx)
    {
        if (condition == null || string.IsNullOrWhiteSpace(condition.If))
            return true;

        // Use safe condition evaluator
        bool result = ConditionEvaluator.Evaluate(condition.If, ctx);
        GD.Print($"Condition check: '{condition.If}' = {result}");
        return result;
    }

    private T GetArg<T>(Dictionary<string, object> args, string key, T defaultValue)
    {
        if (args.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                    return typedValue;

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    private Vector2 CalculateDirection(string pattern, int index, int total, Vector2 baseDirection)
    {
        return pattern.ToLower() switch
        {
            "spiral" => baseDirection.Rotated(index * Mathf.Tau / total),
            "spread" => baseDirection.Rotated((index - total / 2f) * 0.3f),
            "circle" => Vector2.Right.Rotated(index * Mathf.Tau / total),
            _ => baseDirection
        };
    }

    private List<Node> FindNearbyEnemies(Vector2 position, float range, int maxCount)
    {
        var enemies = new List<Node>();
        var enemyGroup = worldNode.GetTree().GetNodesInGroup("enemies");

        foreach (Node node in enemyGroup)
        {
            if (node is Node2D enemy2D)
            {
                float distance = enemy2D.GlobalPosition.DistanceTo(position);
                if (distance <= range)
                {
                    enemies.Add(node);

                    if (enemies.Count >= maxCount)
                        break;
                }
            }
        }

        return enemies;
    }
}
