using Godot;
using System;
using Lexmancer.Core;

public partial class PlayerController : Node, IMoveable
{
    [Export] public float Speed = 300.0f;
    [Export] public float DashSpeed = 600.0f;
    [Export] public float DashDuration = 0.2f;

    private CharacterBody2D body;
    private float dashTimeRemaining = 0;
    private bool canDash = true;
    private float dashCooldown = 0;
    private const float DashCooldownTime = 1.0f;
    private float currentMoveSpeed; // Tracks current speed (normal or dash)

    public override void _Ready()
    {
        body = GetParent<CharacterBody2D>();
        currentMoveSpeed = Speed; // Initialize to normal speed
    }

    public override void _Process(double delta)
    {
        HandleMovement(delta);
        UpdateCooldowns(delta);
    }

    private void HandleMovement(double delta)
    {
        Vector2 inputDirection = Vector2.Zero;

        // Get input direction
        if (Input.IsActionPressed("ui_right") || Input.IsKeyPressed(Key.D))
            inputDirection.X += 1;
        if (Input.IsActionPressed("ui_left") || Input.IsKeyPressed(Key.A))
            inputDirection.X -= 1;
        if (Input.IsActionPressed("ui_down") || Input.IsKeyPressed(Key.S))
            inputDirection.Y += 1;
        if (Input.IsActionPressed("ui_up") || Input.IsKeyPressed(Key.W))
            inputDirection.Y -= 1;

        // Normalize diagonal movement
        inputDirection = inputDirection.Normalized();

        // Handle dash
        if (Input.IsKeyPressed(Key.Space) && canDash && inputDirection.Length() > 0)
        {
            dashTimeRemaining = DashDuration;
            canDash = false;
            dashCooldown = DashCooldownTime;
            GD.Print("Dash!");
        }

        // Update current speed (dash or normal)
        if (dashTimeRemaining > 0)
        {
            currentMoveSpeed = DashSpeed;
            dashTimeRemaining -= (float)delta;
        }
        else
        {
            currentMoveSpeed = Speed;
        }

        // If no input, just stop moving
        if (inputDirection.Length() == 0)
        {
            body.Velocity = Vector2.Zero;
            body.MoveAndSlide();
            return;
        }

        // Let StatusEffectManager handle all movement-based status effects
        var statusManager = Lexmancer.Abilities.Execution.StatusEffectManager.Instance;
        if (statusManager != null)
        {
            statusManager.ApplyMovementEffects(this, inputDirection);
        }
        else
        {
            // Fallback if status manager not ready
            body.Velocity = inputDirection * currentMoveSpeed;
        }

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

    // IMoveable interface implementation
    public CharacterBody2D GetBody() => body;
    public float GetBaseMoveSpeed() => currentMoveSpeed;
}
