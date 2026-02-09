using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.Visuals;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Melee attack with different shapes (arc, circle, rectangle)
/// Very short lifetime, spawns at player position
/// </summary>
public partial class MeleeAttackNode : Area2D
{
    [Export] public string Shape { get; set; } = "arc"; // arc, circle, rectangle
    [Export] public float Range { get; set; } = 1.5f; // Distance from player (tiles, 1 tile = 64px)
    [Export] public float ArcAngle { get; set; } = 120f; // Degrees (for arc/cone)
    [Export] public float Width { get; set; } = 0.5f; // Width in tiles (for rectangle)

    [Export] public float WindupTime { get; set; } = 0.05f; // Delay before hitbox appears
    [Export] public float ActiveTime { get; set; } = 0.2f; // How long hitbox stays

    public Vector2 Direction { get; set; } = Vector2.Right;
    public List<EffectAction> OnHitActions { get; set; } = new();
    public EffectContext Context { get; set; }

    private float timeAlive = 0f;
    private bool isActive = false;
    private HashSet<Node> hitTargets = new(); // Track what we've already hit
    private Color elementColor;
    private GpuParticles2D slashParticles;
    private Polygon2D slashVisual;

    public override void _Ready()
    {
        // Get element color
        elementColor = GetElementColor();

        // Create visual slash effect
        CreateVisual();

        // Create particle trail
        CreateParticleEffect();

        // Set up collision shape based on attack shape
        CreateCollisionShape();

        // Initially disable collision (during windup)
        Monitoring = false;

        // Connect signals
        BodyEntered += OnBodyEntered;
        AreaEntered += OnAreaEntered;

        GD.Print($"Melee attack spawned: shape={Shape}, range={Range}, arc={ArcAngle}°, color={elementColor}");
    }

    private void CreateVisual()
    {
        // Create a polygon shape for the slash visual
        slashVisual = new Polygon2D();
        slashVisual.Color = new Color(elementColor.R, elementColor.G, elementColor.B, 0.6f);

        // Generate polygon based on shape
        slashVisual.Polygon = GenerateVisualPolygon();

        AddChild(slashVisual);

        // Add glow effect
        VisualSystem.AddGlow(this, elementColor, 0.7f);
    }

    private void CreateParticleEffect()
    {
        slashParticles = new GpuParticles2D();
        slashParticles.Amount = 30;
        slashParticles.Lifetime = 0.4f;
        slashParticles.Emitting = true;
        slashParticles.OneShot = true;
        slashParticles.ProcessMaterial = VisualSystem.CreateParticleMaterial(elementColor, ParticleType.Burst);

        // Configure emission shape based on melee shape
        var material = slashParticles.ProcessMaterial as ParticleProcessMaterial;
        if (material != null)
        {
            material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
            material.EmissionSphereRadius = Range * 64f * 0.5f; // Half range
        }

        AddChild(slashParticles);
    }

    private void CreateCollisionShape()
    {
        var collision = new CollisionShape2D();

        switch (Shape.ToLower())
        {
            case "arc":
                // Use a circle segment (approximate with polygon)
                collision.Shape = CreateArcShape();
                break;

            case "circle":
                // Full circle around player
                var circleShape = new CircleShape2D();
                circleShape.Radius = Range * 64f;
                collision.Shape = circleShape;
                break;

            case "rectangle":
                // Thrust/stab forward
                var rectShape = new RectangleShape2D();
                rectShape.Size = new Vector2(Range * 64f, Width * 64f);
                collision.Shape = rectShape;
                collision.Position = new Vector2(Range * 64f / 2, 0); // Offset to extend forward
                break;

            default:
                GD.PrintErr($"Unknown melee shape: {Shape}, defaulting to arc");
                collision.Shape = CreateArcShape();
                break;
        }

        // Rotate collision to match direction (except for circle)
        if (Shape.ToLower() != "circle")
        {
            collision.Rotation = Direction.Angle();
        }

        AddChild(collision);
    }

    /// <summary>
    /// Create an arc-shaped collision (approximated with a polygon/convex shape)
    /// </summary>
    private Shape2D CreateArcShape()
    {
        // Create a wedge/pie slice shape for the arc
        var points = new List<Vector2>();

        // Start at origin (player position)
        points.Add(Vector2.Zero);

        // Calculate arc points
        int segments = 12;
        float angleRad = Mathf.DegToRad(ArcAngle);
        float startAngle = -angleRad / 2f;
        float radius = Range * 64f;

        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + (angleRad * i / segments);
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            points.Add(new Vector2(x, y));
        }

        // Create convex polygon from points
        var shape = new ConvexPolygonShape2D();
        shape.Points = points.ToArray();

        return shape;
    }

    /// <summary>
    /// Generate visual polygon for the slash effect
    /// </summary>
    private Vector2[] GenerateVisualPolygon()
    {
        switch (Shape.ToLower())
        {
            case "arc":
                return GenerateArcPolygon();

            case "circle":
                // Draw a ring outline (outer circle - inner circle)
                return GenerateCirclePolygon();

            case "rectangle":
                // Draw a rectangle extending forward
                return GenerateRectanglePolygon();

            default:
                return GenerateArcPolygon();
        }
    }

    private Vector2[] GenerateArcPolygon()
    {
        var points = new List<Vector2>();

        // Arc from center
        points.Add(Vector2.Zero);

        int segments = 20;
        float angleRad = Mathf.DegToRad(ArcAngle);
        float startAngle = Direction.Angle() - angleRad / 2f;
        float radius = Range * 64f;

        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + (angleRad * i / segments);
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            points.Add(new Vector2(x, y));
        }

        return points.ToArray();
    }

    private Vector2[] GenerateCirclePolygon()
    {
        var points = new List<Vector2>();

        int segments = 32;
        float radius = Range * 64f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.Tau / segments;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            points.Add(new Vector2(x, y));
        }

        return points.ToArray();
    }

    private Vector2[] GenerateRectanglePolygon()
    {
        float length = Range * 64f;
        float width = Width * 64f;
        float angle = Direction.Angle();

        // Create rectangle extending in direction
        var points = new Vector2[]
        {
            Vector2.Zero,
            new Vector2(length, -width/2).Rotated(angle),
            new Vector2(length, width/2).Rotated(angle),
            new Vector2(0, width/2).Rotated(angle)
        };

        return points;
    }

    /// <summary>
    /// Get element color from context
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
                    return VisualSystem.GetElementColor(element);
                }
            }
        }

        // Try to get from context ability primitives
        if (Context?.Ability?.Primitives != null && Context.Ability.Primitives.Count > 0)
        {
            return VisualSystem.GetElementColor(Context.Ability.Primitives);
        }

        return VisualSystem.GetElementColor("neutral");
    }

    public override void _Process(double delta)
    {
        timeAlive += (float)delta;

        // Enable collision after windup
        if (!isActive && timeAlive >= WindupTime)
        {
            isActive = true;
            Monitoring = true;
            GD.Print("Melee attack active!");
        }

        // Destroy after active time expires
        if (isActive && timeAlive >= WindupTime + ActiveTime)
        {
            QueueFree();
        }

        // Scale visual during windup (grow effect)
        if (timeAlive < WindupTime && slashVisual != null)
        {
            float progress = timeAlive / WindupTime;
            slashVisual.Scale = Vector2.One * progress;
        }
        else if (slashVisual != null)
        {
            slashVisual.Scale = Vector2.One;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (!isActive || hitTargets.Contains(body))
            return;

        if (body.IsInGroup("enemies"))
        {
            OnHit(body);
        }
    }

    private void OnAreaEntered(Area2D area)
    {
        if (!isActive || hitTargets.Contains(area))
            return;

        if (area.IsInGroup("enemies"))
        {
            OnHit(area);
        }
    }

    private void OnHit(Node target)
    {
        // Mark as hit (prevent double-hitting)
        hitTargets.Add(target);

        GD.Print($"Melee hit: {target.Name}");

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

        // Spawn hit particles at target location
        if (target is Node2D target2D)
        {
            SpawnHitEffect(target2D.GlobalPosition);
        }
    }

    private void SpawnHitEffect(Vector2 position)
    {
        var hitParticles = new GpuParticles2D();
        hitParticles.GlobalPosition = position;
        hitParticles.Amount = 10;
        hitParticles.Lifetime = 0.2f;
        hitParticles.OneShot = true;
        hitParticles.Emitting = true;
        hitParticles.ProcessMaterial = VisualSystem.CreateParticleMaterial(elementColor, ParticleType.Burst);

        if (Context?.WorldNode != null)
        {
            Context.WorldNode.AddChild(hitParticles);

            // Auto-cleanup
            var timer = new Timer();
            timer.WaitTime = 0.3;
            timer.Autostart = true;
            timer.OneShot = true;
            timer.Timeout += () =>
            {
                hitParticles.QueueFree();
                timer.QueueFree();
            };
            Context.WorldNode.AddChild(timer);
        }
    }
}
