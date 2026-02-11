using Godot;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.Visuals;
using Lexmancer.Core;
using Lexmancer.Services;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Area effect with duration and lingering damage
/// </summary>
public partial class AreaEffectNode : Area2D
{
    [Export] public float Radius { get; set; } = 100f;
    [Export] public float Duration { get; set; } = 3f;
    [Export] public int LingeringDamage { get; set; } = 0;
    [Export] public float GrowthTime { get; set; } = 0f; // 0 = instant, >0 = grows over time

    public List<EffectAction> OnEnterActions { get; set; } = new();
    public List<EffectAction> OnTickActions { get; set; } = new();
    public List<EffectAction> OnExpireActions { get; set; } = new();
    public EffectContext Context { get; set; }

    private float timeAlive = 0f;
    private float tickTimer = 0f;
    private const float TickInterval = 1.0f;
    private Color elementColor;
    private GpuParticles2D lingeringParticles;
    private float currentRadius = 0f; // Current radius (for growth animation)
    private HashSet<Node> enteredEnemies = new(); // Track enemies that already entered
    private CollisionShape2D collisionShape;
    private Node2D visualNode; // For drawing (Area2D can't use _Draw directly)

    public override void _Ready()
    {
        // Create visual node for drawing (Area2D can't _Draw directly)
        visualNode = new Node2D();
        visualNode.Draw += DrawVisual;
        AddChild(visualNode);

        // Set up collision detection for on_enter
        collisionShape = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = Radius;
        collisionShape.Shape = shape;
        AddChild(collisionShape);

        // Connect to area signals for entry detection
        AreaEntered += OnAreaEntered;
        BodyEntered += OnBodyEntered;

        // Set collision layers (detect enemies)
        CollisionLayer = 0; // Don't be detected by others
        CollisionMask = 1; // Detect layer 1 (assumes enemies are on layer 1)

        // Determine element color
        elementColor = GetElementColor();

        // Initialize radius (instant or growing)
        currentRadius = GrowthTime > 0 ? 0f : Radius;

        // Add lingering particles
        CreateParticleEffect();

        GD.Print($"Area effect spawned: radius={Radius}, duration={Duration}, growthTime={GrowthTime}, color={elementColor}");
        GD.Print($"  on_enter actions: {OnEnterActions.Count}, on_tick actions: {OnTickActions.Count}");
    }

    private void DrawVisual()
    {
        // Calculate alpha based on lifetime (fade out near end)
        float alpha = timeAlive < Duration * 0.8f ? 0.3f : 0.3f * (1f - (timeAlive - Duration * 0.8f) / (Duration * 0.2f));

        // Draw the area effect circle with element color (use currentRadius for growth animation)
        var fillColor = new Color(elementColor.R, elementColor.G, elementColor.B, alpha);
        var outlineColor = new Color(elementColor.R, elementColor.G, elementColor.B, alpha + 0.3f);

        visualNode.DrawCircle(Vector2.Zero, currentRadius, fillColor);
        visualNode.DrawArc(Vector2.Zero, currentRadius, 0, Mathf.Tau, 32, outlineColor, 3);
    }

    private void UpdateVisual()
    {
        // Update visual node's drawing
        if (visualNode != null)
        {
            visualNode.QueueRedraw();
        }
    }

    private void CreateParticleEffect()
    {
        // Create lingering particles that float around the area
        lingeringParticles = new GpuParticles2D();
        lingeringParticles.Amount = (int)(Radius / 5); // Scale particle count with radius
        lingeringParticles.Lifetime = 1.0f;
        lingeringParticles.Emitting = true;
        lingeringParticles.ProcessMaterial = VisualSystem.CreateParticleMaterial(elementColor, ParticleType.Lingering);

        // Set emission shape to circle matching area radius
        var material = lingeringParticles.ProcessMaterial as ParticleProcessMaterial;
        if (material != null)
        {
            material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            material.EmissionSphereRadius = Radius * 0.8f; // Slightly smaller than visible area
        }

        AddChild(lingeringParticles);
    }

    /// <summary>
    /// Get element color from context or ability data
    /// </summary>
    private Color GetElementColor()
    {
        // Try to get from context ability primitives
        if (Context?.Ability?.Primitives != null && Context.Ability.Primitives.Count > 0)
        {
            var primitives = Context.Ability.Primitives;
            GD.Print($"[Area] Using primitives for color: {string.Join(", ", primitives)}");
            return VisualSystem.GetElementColor(primitives);
        }

        // Fallback to neutral
        GD.Print("[Area] No element found, using neutral (white)");
        return VisualSystem.GetElementColor("neutral");
    }

    public override void _Process(double delta)
    {
        timeAlive += (float)delta;

        // Update radius for growth animation
        if (GrowthTime > 0 && currentRadius < Radius)
        {
            float growthProgress = Mathf.Min(timeAlive / GrowthTime, 1f);
            currentRadius = Radius * growthProgress;

            // Update collision shape to match growth
            if (collisionShape?.Shape is CircleShape2D circle)
            {
                circle.Radius = currentRadius;
            }
        }
        else if (GrowthTime <= 0)
        {
            currentRadius = Radius;
        }

        // Update particle emission radius to match current radius
        if (lingeringParticles != null)
        {
            var material = lingeringParticles.ProcessMaterial as ParticleProcessMaterial;
            if (material != null)
            {
                material.EmissionSphereRadius = currentRadius * 0.8f;
            }
        }

        // Redraw for fade animation and growth
        UpdateVisual();

        // Apply on_tick effects at intervals
        if (LingeringDamage > 0 || OnTickActions.Count > 0)
        {
            tickTimer += (float)delta;
            if (tickTimer >= TickInterval)
            {
                tickTimer = 0f;
                ApplyTickEffects();
            }
        }

        // Check expiration
        if (timeAlive >= Duration)
        {
            OnExpire();
            QueueFree();
        }
    }

    /// <summary>
    /// Handle enemy entering area (collision detection)
    /// </summary>
    private void OnBodyEntered(Node2D body)
    {
        if (body.IsInGroup("enemies") && !enteredEnemies.Contains(body))
        {
            enteredEnemies.Add(body);
            GD.Print($"Enemy {body.Name} entered area");

            if (OnEnterActions.Count > 0 && Context != null)
            {
                var interpreter = new EffectInterpreter(Context.WorldNode);
                var hitContext = Context.With(
                    position: GlobalPosition,
                    target: body
                );

                foreach (var action in OnEnterActions)
                {
                    interpreter.Execute(action, hitContext);
                }
            }
        }
    }

    /// <summary>
    /// Handle Area2D entering (if enemies have Area2D)
    /// </summary>
    private void OnAreaEntered(Area2D area)
    {
        if (area.IsInGroup("enemies") && !enteredEnemies.Contains(area))
        {
            enteredEnemies.Add(area);
            GD.Print($"Enemy area {area.Name} entered");

            if (OnEnterActions.Count > 0 && Context != null)
            {
                var interpreter = new EffectInterpreter(Context.WorldNode);
                var hitContext = Context.With(
                    position: GlobalPosition,
                    target: area
                );

                foreach (var action in OnEnterActions)
                {
                    interpreter.Execute(action, hitContext);
                }
            }
        }
    }

    /// <summary>
    /// Apply tick effects to enemies currently in the area
    /// </summary>
    private void ApplyTickEffects()
    {
        // Find all enemies currently overlapping the area
        var enemies = GetTree().GetNodesInGroup("enemies");
        EffectInterpreter interpreter = null;
        int enemiesHit = 0;

        foreach (Node enemy in enemies)
        {
            if (enemy is Node2D enemy2D)
            {
                float distance = enemy2D.GlobalPosition.DistanceTo(GlobalPosition);
                if (distance <= currentRadius)
                {
                    enemiesHit++;

                    if (LingeringDamage > 0)
                    {
                        ServiceLocator.Instance.Combat.ApplyDamage(enemy, LingeringDamage);
                    }

                    if (OnTickActions.Count > 0 && Context != null)
                    {
                        interpreter ??= new EffectInterpreter(Context.WorldNode);
                        var hitContext = Context.With(
                            position: GlobalPosition,
                            target: enemy
                        );

                        foreach (var action in OnTickActions)
                        {
                            interpreter.Execute(action, hitContext);
                        }
                    }
                }
            }
        }

        GD.Print($"Area tick at t={timeAlive:0.00}s: {enemiesHit} enemies in radius {currentRadius:0.0}/{Radius}");
    }

    private void OnExpire()
    {
        GD.Print("Area effect expired");

        // Execute on-expire actions
        if (OnExpireActions.Count > 0 && Context != null)
        {
            var interpreter = EffectInterpreterPool.Get(Context.WorldNode);

            var targetActions = new List<EffectAction>();
            var areaRadiusDamageActions = new List<EffectAction>();
            var nonTargetActions = new List<EffectAction>();

            foreach (var action in OnExpireActions)
            {
                var actionName = action.Action?.ToLower() ?? "";
                if (actionName == "damage" || actionName == "apply_status" || actionName == "knockback"
                    || actionName == "heal" || actionName == "chain_to_nearby")
                {
                    if (actionName == "damage" && action.Args != null && action.Args.ContainsKey("area_radius"))
                    {
                        areaRadiusDamageActions.Add(action);
                    }
                    else
                    {
                        targetActions.Add(action);
                    }
                }
                else
                {
                    nonTargetActions.Add(action);
                }
            }

            // Non-target actions run once at area position
            if (nonTargetActions.Count > 0)
            {
                var expireContext = Context.With(position: GlobalPosition);
                foreach (var action in nonTargetActions)
                {
                    interpreter.Execute(action, expireContext);
                }
            }

            // Targeted actions apply to enemies currently in the area
            if (targetActions.Count > 0)
            {
                var enemies = GetTree().GetNodesInGroup("enemies");
                foreach (Node enemy in enemies)
                {
                    if (enemy is Node2D enemy2D)
                    {
                        float distance = enemy2D.GlobalPosition.DistanceTo(GlobalPosition);
                        if (distance <= currentRadius)
                        {
                            var hitContext = Context.With(
                                position: GlobalPosition,
                                target: enemy
                            );

                            foreach (var action in targetActions)
                            {
                                interpreter.Execute(action, hitContext);
                            }
                        }
                    }
                }
            }

            // AOE damage actions with area_radius run once and let interpreter handle enemy lookup
            if (areaRadiusDamageActions.Count > 0)
            {
                var expireContext = Context.With(position: GlobalPosition, target: null);
                foreach (var action in areaRadiusDamageActions)
                {
                    interpreter.Execute(action, expireContext);
                }
            }
        }
    }
}
