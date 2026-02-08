using Godot;
using System;

public partial class PlayerController : Node
{
    [Export] public float Speed = 300.0f;
    [Export] public float DashSpeed = 600.0f;
    [Export] public float DashDuration = 0.2f;

    private CharacterBody2D body;
    private float dashTimeRemaining = 0;
    private bool canDash = true;
    private float dashCooldown = 0;
    private const float DashCooldownTime = 1.0f;

    public override void _Ready()
    {
        body = GetParent<CharacterBody2D>();
    }

    public override void _Process(double delta)
    {
        HandleMovement(delta);
        UpdateCooldowns(delta);
    }

    private void HandleMovement(double delta)
    {
        // Check for movement-impairing status effects
        var statusManager = Lexmancer.Abilities.Execution.StatusEffectManager.Instance;
        if (statusManager != null)
        {
            // Stunned or frozen - cannot move at all
            if (statusManager.HasStatus(body, "stunned") || statusManager.HasStatus(body, "frozen"))
            {
                body.Velocity = Vector2.Zero;
                body.MoveAndSlide();
                return;
            }
        }

        Vector2 velocity = Vector2.Zero;

        // Get input direction
        if (Input.IsActionPressed("ui_right") || Input.IsKeyPressed(Key.D))
            velocity.X += 1;
        if (Input.IsActionPressed("ui_left") || Input.IsKeyPressed(Key.A))
            velocity.X -= 1;
        if (Input.IsActionPressed("ui_down") || Input.IsKeyPressed(Key.S))
            velocity.Y += 1;
        if (Input.IsActionPressed("ui_up") || Input.IsKeyPressed(Key.W))
            velocity.Y -= 1;

        // Normalize diagonal movement
        velocity = velocity.Normalized();

        // Handle dash
        if (Input.IsKeyPressed(Key.Space) && canDash && velocity.Length() > 0)
        {
            dashTimeRemaining = DashDuration;
            canDash = false;
            dashCooldown = DashCooldownTime;
            GD.Print("Dash!");
        }

        // Apply speed with status effect modifiers
        float currentSpeed = Speed;

        // Apply status effect movement modifiers
        float speedMultiplier = 1.0f;
        if (statusManager != null && statusManager.HasStatus(body, "slowed"))
        {
            speedMultiplier *= 0.5f;
        }

        if (dashTimeRemaining > 0)
        {
            currentSpeed = DashSpeed;
            dashTimeRemaining -= (float)delta;
        }

        velocity *= currentSpeed * speedMultiplier;
        body.Velocity = velocity;
        body.MoveAndSlide();
    }

    private void UpdateCooldowns(double delta)
    {
        if (dashCooldown > 0)
        {
            dashCooldown -= (float)delta;
            if (dashCooldown <= 0)
                canDash = true;
        }
    }
}
