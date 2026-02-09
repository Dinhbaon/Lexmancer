using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.Visuals;
using Lexmancer.Core;

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

    [Export] public string MovementType { get; set; } = "stationary"; // stationary, dash, lunge, jump_smash, backstep, blink, teleport_strike
    [Export] public float MoveDistance { get; set; } = 0f; // Tiles (1 tile = 64px)
    [Export] public float MoveDuration { get; set; } = 0f; // Seconds

    public Vector2 Direction { get; set; } = Vector2.Right;
    public List<EffectAction> OnHitActions { get; set; } = new();
    public EffectContext Context { get; set; }
    public Node Caster { get; set; }

    private float timeAlive = 0f;
    private bool isActive = false;
    private HashSet<Node> hitTargets = new(); // Track what we've already hit
    private Color elementColor;
    private GpuParticles2D slashParticles;
    private Polygon2D slashVisual;
    private Node2D casterNode;
    private Tween movementTween;
    private float activationDelay = 0f;
    private bool followCaster = false;

    public override void _Ready()
    {
        ResolveCasterNode();
        TryStartMovement();

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

        // Don't rotate the collision shape itself - we'll rotate the entire Area2D
        // (Rotating a positioned CollisionShape2D moves it to the wrong location)

        AddChild(collision);

        // Rotate the entire Area2D to match direction (except for circle)
        if (Shape.ToLower() != "circle")
        {
            Rotation = Direction.Angle();
        }
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
        // Don't include Direction.Angle() here - the Area2D itself is rotated
        float startAngle = -angleRad / 2f;
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

        // Create rectangle extending forward (Area2D is rotated, so don't rotate points)
        var points = new Vector2[]
        {
            new Vector2(0, -width/2),
            new Vector2(length, -width/2),
            new Vector2(length, width/2),
            new Vector2(0, width/2)
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

        if (followCaster && casterNode != null)
        {
            GlobalPosition = casterNode.GlobalPosition;
        }

        // Enable collision after windup
        if (!isActive && timeAlive >= WindupTime + activationDelay)
        {
            isActive = true;
            Monitoring = true;
            GD.Print("Melee attack active!");
        }

        // Destroy after active time expires
        if (isActive && timeAlive >= WindupTime + activationDelay + ActiveTime)
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

    private void ResolveCasterNode()
    {
        if (Caster is Node2D caster2D)
        {
            casterNode = caster2D;
            return;
        }

        if (Caster is IMoveable moveable)
        {
            casterNode = moveable.GetBody();
            return;
        }

        if (Caster is Node casterNodeRef && casterNodeRef.GetParent() is Node2D parent2D)
        {
            casterNode = parent2D;
        }
    }

    private void TryStartMovement()
    {
        if (casterNode == null)
            return;

        var movement = (MovementType ?? "stationary").ToLowerInvariant();
        if (movement == "stationary" || MoveDistance <= 0f || MoveDuration <= 0f)
            return;

        Vector2 moveDir = Direction.Length() > 0 ? Direction.Normalized() : Vector2.Right;
        switch (movement)
        {
            case "dash":
            case "lunge":
            case "jump_smash":
                break;
            case "backstep":
                moveDir = -moveDir;
                break;
            case "blink":
            case "teleport_strike":
                break;
            default:
                return;
        }

        activationDelay = movement == "jump_smash" ? MoveDuration : 0f;
        followCaster = true;

        var target = casterNode.GlobalPosition + moveDir * MoveDistance * 64f;
        if (movement == "teleport_strike")
        {
            var enemy = FindNearestEnemy(casterNode.GlobalPosition);
            if (enemy == null)
                return;

            var enemyPos = enemy.GlobalPosition;
            var toEnemy = enemyPos - casterNode.GlobalPosition;
            if (toEnemy.Length() <= 0.001f)
                return;

            moveDir = toEnemy.Normalized();
            Direction = moveDir;
            float maxTravel = Mathf.Max(0f, toEnemy.Length() - 32f);
            float travel = Mathf.Min(MoveDistance * 64f, maxTravel);
            target = casterNode.GlobalPosition + moveDir * travel;
        }

        if (movement == "blink" || movement == "teleport_strike")
        {
            casterNode.GlobalPosition = target;
            GlobalPosition = casterNode.GlobalPosition;
        }
        else
        {
            movementTween = CreateTween();
            movementTween.TweenProperty(casterNode, "global_position", target, MoveDuration)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(movement == "jump_smash" ? Tween.EaseType.InOut : Tween.EaseType.Out);
        }
    }

    private Node2D FindNearestEnemy(Vector2 from)
    {
        var searchRoot = Context?.WorldNode ?? GetTree().Root;
        if (searchRoot == null)
            return null;

        Node2D nearest = null;
        float bestDist = float.MaxValue;
        foreach (Node node in searchRoot.GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Node2D enemy)
                continue;

            float dist = enemy.GlobalPosition.DistanceTo(from);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = enemy;
            }
        }

        return nearest;
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
