using Godot;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.Visuals;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Area effect with duration and lingering damage
/// </summary>
public partial class AreaEffectNode : Node2D
{
    [Export] public float Radius { get; set; } = 100f;
    [Export] public float Duration { get; set; } = 3f;
    [Export] public int LingeringDamage { get; set; } = 0;
    [Export] public float GrowthTime { get; set; } = 0f; // 0 = instant, >0 = grows over time

    public List<EffectAction> OnHitActions { get; set; } = new();
    public List<EffectAction> OnExpireActions { get; set; } = new();
    public EffectContext Context { get; set; }

    private float timeAlive = 0f;
    private float tickTimer = 0f;
    private const float TickInterval = 1.0f;
    private Color elementColor;
    private GpuParticles2D lingeringParticles;
    private float currentRadius = 0f; // Current radius (for growth animation)

    public override void _Ready()
    {
        // Determine element color
        elementColor = GetElementColor();

        // Initialize radius (instant or growing)
        currentRadius = GrowthTime > 0 ? 0f : Radius;

        // Create visual circle for area
        CreateVisual();

        // Add lingering particles
        CreateParticleEffect();

        GD.Print($"Area effect spawned: radius={Radius}, duration={Duration}, growthTime={GrowthTime}, color={elementColor}");

        // Apply on-hit effects immediately when the area spawns (no tick damage yet)
        if (OnHitActions.Count > 0 && Context != null)
        {
            ApplyTickEffects(includeDamage: false);
        }
    }

    private void CreateVisual()
    {
        // Draw will be called automatically
        QueueRedraw();
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

    public override void _Draw()
    {
        // Calculate alpha based on lifetime (fade out near end)
        float alpha = timeAlive < Duration * 0.8f ? 0.3f : 0.3f * (1f - (timeAlive - Duration * 0.8f) / (Duration * 0.2f));

        // Draw the area effect circle with element color (use currentRadius for growth animation)
        var fillColor = new Color(elementColor.R, elementColor.G, elementColor.B, alpha);
        var outlineColor = new Color(elementColor.R, elementColor.G, elementColor.B, alpha + 0.3f);

        DrawCircle(Vector2.Zero, currentRadius, fillColor);
        DrawArc(Vector2.Zero, currentRadius, 0, Mathf.Tau, 32, outlineColor, 3);
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
        QueueRedraw();

        // Apply lingering damage and/or on-hit effects at intervals
        if (LingeringDamage > 0 || OnHitActions.Count > 0)
        {
            tickTimer += (float)delta;
            if (tickTimer >= TickInterval)
            {
                tickTimer = 0f;
                ApplyTickEffects(includeDamage: true);
            }
        }

        // Check expiration
        if (timeAlive >= Duration)
        {
            OnExpire();
            QueueFree();
        }
    }

    private void ApplyTickEffects(bool includeDamage)
    {
        // Find all enemies in current radius (respects growth)
        var enemies = GetTree().GetNodesInGroup("enemies");
        EffectInterpreter interpreter = null;

        foreach (Node enemy in enemies)
        {
            if (enemy is Node2D enemy2D)
            {
                float distance = enemy2D.GlobalPosition.DistanceTo(GlobalPosition);
                if (distance <= currentRadius)
                {
                    if (includeDamage && LingeringDamage > 0)
                    {
                        Combat.DamageSystem.ApplyDamage(enemy, LingeringDamage);
                    }

                    if (OnHitActions.Count > 0 && Context != null)
                    {
                        interpreter ??= new EffectInterpreter(Context.WorldNode);
                        var hitContext = Context.With(
                            position: GlobalPosition,
                            target: enemy
                        );

                        foreach (var action in OnHitActions)
                        {
                            interpreter.Execute(action, hitContext);
                        }
                    }
                }
            }
        }

        if (includeDamage && LingeringDamage > 0)
        {
            GD.Print($"Tick damage: {LingeringDamage} to enemies in radius {currentRadius}/{Radius}");
        }
    }

    private void OnExpire()
    {
        GD.Print("Area effect expired");

        // Execute on-expire actions
        if (OnExpireActions.Count > 0 && Context != null)
        {
            var interpreter = new EffectInterpreter(Context.WorldNode);

            foreach (var action in OnExpireActions)
            {
                interpreter.Execute(action, Context);
            }
        }
    }
}
