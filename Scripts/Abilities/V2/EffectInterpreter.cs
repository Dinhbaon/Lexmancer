using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Lexmancer.Abilities.Execution;
using Lexmancer.Abilities.Visuals;
using Lexmancer.Core;
using Lexmancer.Services;

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
    private ConfigService Config => ServiceLocator.Instance.Config;

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
        if (action.Action?.ToLower() == "spawn_area"
            && context.Variables.TryGetValue("jump_smash_delay_pending", out var pendingObj)
            && pendingObj is bool pending
            && pending
            && TryGetContextFloat(context, "jump_smash_delay_seconds", out float delaySeconds)
            && delaySeconds > 0f)
        {
            ScheduleDelayedAction(action, context, delaySeconds, spawnAtCaster: true);
            context.Variables["jump_smash_delay_pending"] = false;
            return;
        }

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

            case "spawn_melee":
                SpawnMelee(action.Args, context, action.OnHit);
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
                GD.Print($"    Supported actions: spawn_projectile, spawn_area, spawn_beam, spawn_melee, damage, heal, apply_status, knockback, chain_to_nearby, repeat");
                break;
        }
    }

    // ==================== SPAWNING ACTIONS ====================

    private void SpawnProjectile(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> onHit)
    {
        GD.Print($"[SpawnProjectile] Starting...");
        GD.Print($"   Position: {ctx.Position}, Direction: {ctx.Direction}");

        int count = GetArg(args, "count", 1);
        string pattern = GetArg(args, "pattern", "single");
        float speed = GetArg(args, "speed", 400f);
        float acceleration = GetArg(args, "acceleration", 0f);
        bool piercing = GetArg(args, "piercing", false);
        int maxPierceHits = GetArg(args, "max_pierce_hits", 3);

        GD.Print($"   Args: count={count}, pattern={pattern}, speed={speed}, accel={acceleration}, piercing={piercing}");

        // Clamp values
        count = Math.Clamp(count, 1, 5);
        speed = Math.Clamp(speed, 50f, 1200f);
        acceleration = Math.Clamp(acceleration, -500f, 500f);
        maxPierceHits = Math.Clamp(maxPierceHits, 1, 10);

        GD.Print($"   OnHit actions: {onHit?.Count ?? 0}");

        for (int i = 0; i < count; i++)
        {
            Vector2 direction = CalculateDirection(pattern, i, count, ctx.Direction);
            GD.Print($"   Projectile #{i+1} direction: {direction}");

            // Create actual projectile node
            var projectile = new Abilities.Execution.ProjectileNodeV2();
            projectile.GlobalPosition = ctx.Position;
            projectile.Direction = direction;
            projectile.Speed = speed;
            projectile.Acceleration = acceleration;
            projectile.Piercing = piercing;
            projectile.MaxPierceHits = maxPierceHits;
            projectile.OnHitActions = onHit;
            projectile.Context = ctx;

            GD.Print($"   Created ProjectileNodeV2 at {ctx.Position}");

            worldNode.AddChild(projectile);
            GD.Print($"   ✓ Added projectile to worldNode ({worldNode.Name})");

            string accelInfo = acceleration != 0 ? $" (accel: {acceleration})" : "";
            string pierceInfo = piercing ? $", piercing (max {maxPierceHits})" : "";
            GD.Print($"✓ Spawned projectile #{i+1}/{count} at speed {speed}{accelInfo}{pierceInfo}");
        }
    }

    private void SpawnArea(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> onHit, List<EffectAction> onExpire)
    {
        float radius = GetArg(args, "radius", 100f);
        float duration = GetArg(args, "duration", 2f);
        int tickDamage = GetArg(args, "lingering_damage", 0);
        int damage = GetArg(args, "damage", 0);
        float growthTime = GetArg(args, "growth_time", 0f);
        bool spawnAtCaster = GetArg(args, "spawn_at_caster", false);

        // Clamp values
        radius = Math.Clamp(radius, 50f, 300f);
        duration = Math.Clamp(duration, 1f, 10f);
        tickDamage = Math.Clamp(tickDamage, 0, 20);
        growthTime = Math.Clamp(growthTime, 0f, 3f);

        Vector2 spawnPosition = ctx.Position;
        if (spawnAtCaster)
        {
            if (ctx.Caster is Node2D caster2D)
            {
                spawnPosition = caster2D.GlobalPosition;
            }
            else if (ctx.Caster is IMoveable moveable)
            {
                spawnPosition = moveable.GetBody().GlobalPosition;
            }
        }

        // Create area effect node
        var areaNode = new Abilities.Execution.AreaEffectNode();
        areaNode.GlobalPosition = spawnPosition;
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
            var nearbyEnemies = FindNearbyEnemies(spawnPosition, radius, 20);
            foreach (var enemy in nearbyEnemies)
            {
                ServiceLocator.Instance.Combat.ApplyDamage(enemy, damage);
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

    private void SpawnMelee(Dictionary<string, object> args, EffectContext ctx, List<EffectAction> onHit)
    {
        string shape = GetArg(args, "shape", "arc");
        float range = GetArg(args, "range", 1.5f);
        float arcAngle = GetArg(args, "arc_angle", 120f);
        float width = GetArg(args, "width", 0.5f);
        float windupTime = GetArg(args, "windup_time", 0.05f);
        float activeTime = GetArg(args, "active_time", 0.2f);
        string movement = GetArg(args, "movement", "stationary");
        movement = movement?.ToLowerInvariant() ?? "stationary";
        float moveDistance = GetArg(args, "move_distance", 0f);
        float moveDuration = GetArg(args, "move_duration", 0f);

        // Clamp values
        range = Math.Clamp(range, 0.5f, 3f);
        arcAngle = Math.Clamp(arcAngle, 30f, 360f);
        width = Math.Clamp(width, 0.2f, 2f);
        windupTime = Math.Clamp(windupTime, 0f, 0.3f);
        activeTime = Math.Clamp(activeTime, 0.1f, 0.5f);
        ApplyMeleeMovementDefaults(movement, ref moveDistance, ref moveDuration);
        moveDistance = Math.Clamp(moveDistance, 0f, 4f);
        moveDuration = Math.Clamp(moveDuration, 0f, 0.6f);

        if (movement == "jump_smash" && moveDuration > 0f)
        {
            ctx.Variables["jump_smash_delay_pending"] = true;
            ctx.Variables["jump_smash_delay_seconds"] = moveDuration;
        }

        // Create melee attack node
        var melee = new Execution.MeleeAttackNode();
        melee.GlobalPosition = ctx.Position;
        melee.Direction = ctx.Direction;
        melee.Shape = shape;
        melee.Range = range;
        melee.ArcAngle = arcAngle;
        melee.Width = width;
        melee.WindupTime = windupTime;
        melee.ActiveTime = activeTime;
        melee.MovementType = movement;
        melee.MoveDistance = moveDistance;
        melee.MoveDuration = moveDuration;
        melee.OnHitActions = onHit;
        melee.Context = ctx;
        melee.Caster = ctx.Caster;

        worldNode.AddChild(melee);

        var moveInfo = movement?.ToLower() == "stationary"
            ? "stationary"
            : $"{movement} {moveDistance:0.##} tiles / {moveDuration:0.##}s";
        GD.Print($"Spawned melee: shape={shape}, range={range} tiles, arc={arcAngle}°, windup={windupTime}s, active={activeTime}s, move={moveInfo}");
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
                    GD.Print($"[DEBUG] Raw damage from formula: {damage}");
                    damage = Math.Clamp(damage, 1f, 100f);
                    GD.Print($"[DEBUG] Clamped damage: {damage}");

                    ServiceLocator.Instance.Combat.ApplyDamage(enemy, damage, element, ctx.Caster);
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

        if (Config.VerboseLogging)
        {
            GD.Print($"[DEBUG] Raw damage from formula: {finalDamage}");
            GD.Print($"[DEBUG] Args contents: {System.Text.Json.JsonSerializer.Serialize(args)}");
        }

        // Clamp damage
        finalDamage = Math.Clamp(finalDamage, 1f, 100f);
        if (Config.VerboseLogging)
        {
            GD.Print($"[DEBUG] Final clamped damage: {finalDamage}");
        }

        // Apply damage to target
        ServiceLocator.Instance.Combat.ApplyDamage(ctx.Target, finalDamage, damageElement, ctx.Caster);
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
        var statusManager = ServiceLocator.Instance.Combat.StatusEffects;
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

        // Determine chain visual color
        var chainColor = GetChainColor(chainedActions, ctx);

        // Execute chained actions on each enemy
        foreach (var enemy in nearbyEnemies)
        {
            // Cast to Node2D if needed for position
            var enemyPos = enemy is Node2D enemy2D ? enemy2D.GlobalPosition : Vector2.Zero;
            var chainCtx = ctx.With(
                position: enemyPos,
                target: enemy
            );

            SpawnChainVisual(ctx.Position, enemyPos, chainColor);

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
            // Use the effect context position as the pull center (area center)
            "towards_center" => (ctx.Position - targetPos).Normalized(),
            "up" => Vector2.Up,
            _ => Vector2.Zero
        };

        // Apply sustained knockback via StatusEffectManager
        var statusManager = ServiceLocator.Instance.Combat.StatusEffects;
        if (statusManager != null)
        {
            statusManager.ApplyKnockback(ctx.Target, knockbackDir, force, duration: 0.3f);
        }
        else
        {
            // Fallback to instant velocity if StatusEffectManager not available
            if (ctx.Target is CharacterBody2D body)
            {
                body.Velocity = knockbackDir * force;
                GD.Print($"Applied knockback (instant): force={force}, direction={direction} to {ctx.Target.Name}");
            }
            else
            {
                GD.PrintErr($"Target {ctx.Target.Name} is not a CharacterBody2D, cannot apply knockback");
            }
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
        if (!IsHealAllowed(ctx.Caster, healTarget))
        {
            var casterName = ctx.Caster?.Name ?? "null";
            var targetName = healTarget?.Name ?? "null";
            GD.Print($"Blocked heal: caster={casterName}, target={targetName} (not self/ally)");
            return;
        }

        // Apply healing
        ServiceLocator.Instance.Combat.ApplyHealing(healTarget, amount);
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
    private void ScheduleDelayedAction(EffectAction action, EffectContext ctx, float delaySeconds, bool spawnAtCaster)
    {
        var executor = worldNode.GetNodeOrNull<Execution.DelayedActionExecutor>("DelayedActionExecutor");
        if (executor == null)
        {
            executor = new Execution.DelayedActionExecutor();
            executor.Name = "DelayedActionExecutor";
            worldNode.AddChild(executor);
            GD.Print("Created DelayedActionExecutor");
        }

        var delayedAction = new EffectAction
        {
            Action = action.Action,
            Args = new Dictionary<string, object>(action.Args ?? new Dictionary<string, object>()),
            OnHit = action.OnHit,
            OnExpire = action.OnExpire,
            Condition = action.Condition
        };

        if (spawnAtCaster)
            delayedAction.Args["spawn_at_caster"] = true;

        executor.ScheduleActions(new List<EffectAction> { delayedAction }, ctx, 1, delaySeconds);
        GD.Print($"Scheduled delayed action '{action.Action}' after {delaySeconds:0.##}s");
    }

    private bool TryGetContextFloat(EffectContext ctx, string key, out float value)
    {
        value = 0f;
        if (ctx?.Variables == null || !ctx.Variables.TryGetValue(key, out var raw))
            return false;

        try
        {
            switch (raw)
            {
                case float f:
                    value = f;
                    return true;
                case double d:
                    value = (float)d;
                    return true;
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case string s when float.TryParse(s, out var parsed):
                    value = parsed;
                    return true;
                default:
                    value = Convert.ToSingle(raw);
                    return true;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHealAllowed(Node caster, Node target)
    {
        if (target == null)
            return false;

        if (caster == null)
        {
            // Without a caster, only allow healing the player side.
            return target.IsInGroup("player") && !target.IsInGroup("enemies");
        }

        if (ReferenceEquals(caster, target))
            return true;

        bool casterIsPlayer = caster.IsInGroup("player");
        bool casterIsEnemy = caster.IsInGroup("enemies");
        bool targetIsPlayer = target.IsInGroup("player");
        bool targetIsEnemy = target.IsInGroup("enemies");

        // If neither has a faction, be conservative and block.
        if (!casterIsPlayer && !casterIsEnemy)
            return false;

        return (casterIsPlayer && targetIsPlayer) || (casterIsEnemy && targetIsEnemy);
    }

    private float EvaluateDamageFormula(Dictionary<string, object> args, EffectContext ctx)
    {
        // If formula is specified, evaluate it
        if (args.TryGetValue("formula", out var formulaObj))
        {
            string formula = formulaObj.ToString();
            return EvaluateFormula(formula, ctx);
        }

        // Otherwise use amount
        float amount = GetArg(args, "amount", 20f);
        if (Config.VerboseLogging)
        {
            GD.Print($"[DEBUG] GetArg returned damage amount: {amount}");
        }
        return amount;
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
        if (args != null && args.TryGetValue(key, out var value))
        {
            try
            {
                if (value is JsonElement jsonValue)
                {
                    return ConvertJsonElement(jsonValue, defaultValue);
                }

                if (value is T typedValue)
                    return typedValue;

                // Special handling for numeric conversions (double → float, double → int)
                if (typeof(T) == typeof(float) && value is double doubleVal)
                    return (T)(object)(float)doubleVal;

                if (typeof(T) == typeof(int) && value is double doubleIntVal)
                    return (T)(object)(int)doubleIntVal;

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GetArg] Failed to convert '{key}' value '{value}' (type: {value?.GetType().Name}) to {typeof(T).Name}: {ex.Message}");
                if (Config.VerboseLogging)
                {
                    GD.PrintErr($"[GetArg] Failed to convert '{key}' value '{value}' (type: {value?.GetType().Name}) to {typeof(T).Name}: {ex.Message}");
                }
                return defaultValue;
            }
        }
        return defaultValue;
    }

    private T ConvertJsonElement<T>(JsonElement element, T defaultValue)
    {
        try
        {
            if (typeof(T) == typeof(string))
            {
                var str = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
                return (T)(object)(str ?? "");
            }

            if (typeof(T) == typeof(int))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var i))
                    return (T)(object)i;
            }

            if (typeof(T) == typeof(long))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var l))
                    return (T)(object)l;
            }

            if (typeof(T) == typeof(float))
            {
                if (element.ValueKind == JsonValueKind.Number)
                    return (T)(object)(float)element.GetDouble();
            }

            if (typeof(T) == typeof(double))
            {
                if (element.ValueKind == JsonValueKind.Number)
                    return (T)(object)element.GetDouble();
            }

            if (typeof(T) == typeof(bool))
            {
                if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
                    return (T)(object)element.GetBoolean();
            }
        }
        catch
        {
            // Fall through to default value
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

    private void ApplyMeleeMovementDefaults(string movement, ref float distance, ref float duration)
    {
        var move = (movement ?? "stationary").ToLowerInvariant();
        if (move == "stationary")
        {
            distance = 0f;
            duration = 0f;
            return;
        }

        if (distance <= 0f)
        {
            distance = move switch
            {
                "dash" => 2.0f,
                "lunge" => 1.0f,
                "jump_smash" => 1.5f,
                "backstep" => 1.0f,
                "blink" => 2.0f,
                "teleport_strike" => 2.5f,
                _ => 0f
            };
        }

        if (duration <= 0f)
        {
            duration = move switch
            {
                "dash" => 0.15f,
                "lunge" => 0.1f,
                "jump_smash" => 0.25f,
                "backstep" => 0.12f,
                "blink" => 0.05f,
                "teleport_strike" => 0.08f,
                _ => 0f
            };
        }
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

    private Color GetChainColor(List<EffectAction> chainedActions, EffectContext ctx)
    {
        if (chainedActions != null)
        {
            foreach (var action in chainedActions)
            {
                if (action.Action.ToLower() == "damage" && action.Args != null && action.Args.ContainsKey("element"))
                {
                    var element = action.Args["element"].ToString();
                    return VisualSystem.GetElementColor(element);
                }
            }
        }

        if (ctx?.Ability?.Primitives != null && ctx.Ability.Primitives.Count > 0)
        {
            return VisualSystem.GetElementColor(ctx.Ability.Primitives);
        }

        return VisualSystem.GetElementColor("neutral");
    }

    private void SpawnChainVisual(Vector2 from, Vector2 to, Color color)
    {
        if (worldNode == null)
            return;

        var chain = new ChainVisual
        {
            From = from,
            To = to,
            ChainColor = color
        };

        worldNode.AddChild(chain);
    }
}
