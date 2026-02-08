using Godot;
using System;
using Lexmancer.Combat;
using Lexmancer.UI;

namespace Lexmancer.Enemies;

/// <summary>
/// Basic melee chaser enemy for vertical slice
/// </summary>
public partial class BasicEnemy : CharacterBody2D
{
	[Export] public float MaxHealth { get; set; } = 50f;
	[Export] public float MoveSpeed { get; set; } = 100f;
	[Export] public float ContactDamage { get; set; } = 10f;
	[Export] public float DamageInterval { get; set; } = 1.0f;

	private HealthComponent health;
	private Node2D player;
	private float damageTimer = 0f;

	public override void _Ready()
	{
		// Initialize health
		health = new HealthComponent();
		health.Initialize(MaxHealth);
		health.Name = "HealthComponent";
		health.OnDeath += OnDeath;
		health.OnDamaged += OnDamaged;

		// Add health component as child for DamageSystem to find
		AddChild(health);

		// Add a small health bar above the enemy
		var healthBar = new HealthBar();
		healthBar.IsPlayerHealthBar = false;
		healthBar.Name = "EnemyHealthBar";
		AddChild(healthBar);

		// Add to enemies group
		AddToGroup("enemies");

		// Create visual
		CreateVisual();

		// Create collision
		CreateCollision();

		// Find player
		player = GetTree().GetFirstNodeInGroup("player") as Node2D;

		GD.Print($"BasicEnemy spawned with {MaxHealth} HP");
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!health.IsAlive)
			return;

		// Check for movement-impairing status effects
		var statusManager = Abilities.Execution.StatusEffectManager.Instance;
		if (statusManager != null)
		{
			// Stunned or frozen - cannot move at all
			if (statusManager.HasStatus(this, "stunned") || statusManager.HasStatus(this, "frozen"))
			{
				Velocity = Vector2.Zero;
				MoveAndSlide();
				return;
			}
		}

		// Move toward player
		if (player != null)
		{
			var direction = (player.GlobalPosition - GlobalPosition).Normalized();

			// Apply status effect movement modifiers
			float speedMultiplier = 1.0f;

			if (statusManager != null)
			{
				// Slowed - 50% speed
				if (statusManager.HasStatus(this, "slowed"))
					speedMultiplier *= 0.5f;

				// Feared - move away from player instead
				if (statusManager.HasStatus(this, "feared"))
					direction = -direction;
			}

			Velocity = direction * MoveSpeed * speedMultiplier;
			MoveAndSlide();

			// Check for collision with player
			CheckPlayerCollision(delta);
		}
	}

	private void CheckPlayerCollision(double delta)
	{
		damageTimer -= (float)delta;

		// Check if overlapping with player
		if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) < 32)
		{
			if (damageTimer <= 0)
			{
				// Deal contact damage to player
				DamageSystem.ApplyDamage(player, ContactDamage, "physical", this);
				damageTimer = DamageInterval;
			}
		}
	}

	private void CreateVisual()
	{
		var sprite = new ColorRect();
		sprite.Color = new Color(1, 0, 0); // Red
		sprite.Size = new Vector2(28, 28);
		sprite.Position = new Vector2(-14, -14);
		sprite.Name = "Visual";
		AddChild(sprite);
	}

	private void CreateCollision()
	{
		var collision = new CollisionShape2D();
		var shape = new RectangleShape2D();
		shape.Size = new Vector2(28, 28);
		collision.Shape = shape;
		collision.Name = "CollisionShape";
		AddChild(collision);
	}

	private void OnDamaged(float amount)
	{
		// Flash red when damaged
		var visual = GetNodeOrNull<ColorRect>("Visual");
		if (visual != null)
		{
			visual.Modulate = new Color(1.5f, 0.5f, 0.5f);
			// Reset color after a short delay
			GetTree().CreateTimer(0.1).Timeout += () => {
				if (IsInstanceValid(visual))
					visual.Modulate = new Color(1, 1, 1);
			};
		}

		GD.Print($"Enemy took {amount} damage. Health: {health.Current}/{health.Max}");
	}

	private void OnDeath()
	{
		GD.Print("Enemy died!");

		// TODO: Drop element pickup
		// TODO: Play death animation/effect

		// Emit signal for game manager
		GetTree().Root.GetNode<Node>("Main")?.GetNode<GameManager>("GameManager")?.OnEnemyDied(this);

		QueueFree();
	}

	/// <summary>
	/// Get current health (for UI/debugging)
	/// </summary>
	public float GetHealthPercentage()
	{
		return health?.HealthPercentage ?? 0;
	}
}
