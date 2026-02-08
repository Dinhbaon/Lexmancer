using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.Visuals;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Enhanced projectile with support for effect scripts
/// </summary>
public partial class ProjectileNodeV2 : Area2D
{
    [Export] public float Speed { get; set; } = 400f;
    [Export] public float Lifetime { get; set; } = 2.0f;
    [Export] public float Acceleration { get; set; } = 0f; // Pixels per second^2 (0 = constant speed)

    public Vector2 Direction { get; set; }
    public List<EffectAction> OnHitActions { get; set; } = new();
    public EffectContext Context { get; set; }

    private float timeAlive = 0f;
    private float currentSpeed = 0f; // Current speed (for acceleration)
    private bool hasHit = false;
    private GpuParticles2D trailParticles;

    public override void _Ready()
    {
        // Initialize speed
        currentSpeed = Speed;

        // Determine element color from context
        var elementColor = GetElementColor();
        var projectileSize = 12f;

        // Create colored visual
        var visual = VisualSystem.CreateProjectileVisual(elementColor, projectileSize);
        AddChild(visual);

        // Add particle trail
        trailParticles = new GpuParticles2D();
        trailParticles.Amount = 20;
        trailParticles.Lifetime = 0.5f;
        trailParticles.Emitting = true;
        trailParticles.ProcessMaterial = VisualSystem.CreateParticleMaterial(elementColor, ParticleType.Trail);
        trailParticles.DrawOrder = GpuParticles2D.DrawOrderEnum.Index;
        AddChild(trailParticles);

        // Add glow effect for extra punch
        VisualSystem.AddGlow(this, elementColor, 0.5f);

        // Create collision shape
        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = projectileSize / 2;
        collision.Shape = shape;
        AddChild(collision);

        // Connect signals
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;

        GD.Print($"Projectile spawned with color: {elementColor}, speed: {Speed}, accel: {Acceleration}");
    }

    /// <summary>
    /// Get element color from context or ability data
    /// </summary>
    private Color GetElementColor()
    {
        // Try to get element from on-hit damage action
        if (OnHitActions != null)
        {
            foreach (var action in OnHitActions)
            {
                if (action.Action.ToLower() == "damage" && action.Args != null && action.Args.ContainsKey("element"))
                {
                    var element = action.Args["element"].ToString();
                    GD.Print($"[Projectile] Found element from damage action: {element}");
                    return VisualSystem.GetElementColor(element);
                }
            }
        }

        // Try to get from context ability primitives
        if (Context?.Ability?.Primitives != null && Context.Ability.Primitives.Count > 0)
        {
            var primitives = Context.Ability.Primitives;
            GD.Print($"[Projectile] Using primitives for color: {string.Join(", ", primitives)}");
            return VisualSystem.GetElementColor(primitives);
        }

        // Fallback to neutral
        GD.Print("[Projectile] No element found, using neutral (white)");
        return VisualSystem.GetElementColor("neutral");
    }

    public override void _Process(double delta)
    {
        // Apply acceleration
        if (Acceleration != 0f)
        {
            currentSpeed += Acceleration * (float)delta;
            // Clamp speed to reasonable bounds
            currentSpeed = Math.Clamp(currentSpeed, 50f, 1200f);
        }

        // Move projectile with current speed
        Position += Direction * currentSpeed * (float)delta;

        // Lifetime check
        timeAlive += (float)delta;
        if (timeAlive >= Lifetime)
        {
            QueueFree();
        }
    }

    /// <summary>
    /// Called when projectile hits a body (CharacterBody2D, StaticBody2D, etc.)
    /// </summary>
    private void OnBodyEntered(Node2D body)
    {
        if (hasHit)
            return;

        // Check if it's an enemy or wall
        if (body.IsInGroup("enemies"))
        {
            OnCollision(body);
        }
        else if (body is StaticBody2D)
        {
            // Hit a wall, just destroy
            QueueFree();
        }
    }

    /// <summary>
    /// Called when projectile hits an area (Area2D enemies)
    /// </summary>
    private void OnAreaEntered(Area2D area)
    {
        if (hasHit)
            return;

        // Check if it's an enemy
        if (area.IsInGroup("enemies"))
        {
            OnCollision(area);
        }
    }

    /// <summary>
    /// Called when projectile hits something
    /// </summary>
    private void OnCollision(Node2D target)
    {
        if (hasHit)
            return;

        hasHit = true;
        GD.Print($"Projectile hit: {target.Name}");

        // Create impact particle burst
        SpawnImpactEffect();

        // Execute on-hit actions
        if (OnHitActions.Count > 0 && Context != null)
        {
            var interpreter = new EffectInterpreter(Context.WorldNode);
            var hitContext = Context.With(
                position: GlobalPosition,
                target: target
            );

            foreach (var action in OnHitActions)
            {
                interpreter.Execute(action, hitContext);
            }
        }

        QueueFree();
    }

    /// <summary>
    /// Spawn particle burst on impact
    /// </summary>
    private void SpawnImpactEffect()
    {
        var elementColor = GetElementColor();

        // Create one-shot particle burst
        var burstParticles = new GpuParticles2D();
        burstParticles.GlobalPosition = GlobalPosition;
        burstParticles.Amount = 15;
        burstParticles.Lifetime = 0.3f;
        burstParticles.OneShot = true;
        burstParticles.Emitting = true;
        burstParticles.ProcessMaterial = VisualSystem.CreateParticleMaterial(elementColor, ParticleType.Burst);

        // Add to world (not as child, since we're about to be freed)
        if (Context?.WorldNode != null)
        {
            Context.WorldNode.AddChild(burstParticles);

            // Auto-cleanup after particle lifetime
            var timer = new Timer();
            timer.WaitTime = burstParticles.Lifetime + 0.1;
            timer.Autostart = true;
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                burstParticles.QueueFree();
                timer.QueueFree();
            };
            Context.WorldNode.AddChild(timer);
        }
    }
}
