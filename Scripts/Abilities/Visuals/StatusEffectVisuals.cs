using Godot;
using System.Collections.Generic;

namespace Lexmancer.Abilities.Visuals;

/// <summary>
/// Enhanced visual feedback for status effects
/// Creates particles, icons, and pulsing effects on affected entities
/// </summary>
public partial class StatusEffectVisuals : Node2D
{
	private Node2D attachedEntity;
	private Dictionary<string, StatusVisualData> activeVisuals = new();

	// Visual elements
	private GpuParticles2D particleEffect;
	private Node2D iconContainer;
	private ColorRect entityVisual;

	// Pulsing effect
	private float pulseTimer = 0f;
	private const float PulseSpeed = 3f;

	public override void _Ready()
	{
		// Create icon container above entity
		iconContainer = new Node2D();
		iconContainer.Position = new Vector2(0, -40); // Float above entity
		AddChild(iconContainer);
	}

	/// <summary>
	/// Attach visuals to an entity
	/// </summary>
	public void AttachToEntity(Node2D entity)
	{
		attachedEntity = entity;

		// Find the entity's visual node
		entityVisual = entity.GetNodeOrNull<ColorRect>("Visual");

		GD.Print($"StatusEffectVisuals attached to {entity.Name}");
	}

	/// <summary>
	/// Add visual feedback for a status effect
	/// </summary>
	public void AddStatusVisual(string status, float duration)
	{
		status = status.ToLower();

		// If already has this status visual, just refresh it
		if (activeVisuals.ContainsKey(status))
		{
			activeVisuals[status].TimeRemaining = duration;
			return;
		}

		var visualData = new StatusVisualData
		{
			Status = status,
			TimeRemaining = duration,
			Color = GetStatusColor(status),
			ParticleType = GetStatusParticleType(status)
		};

		activeVisuals[status] = visualData;

		// Create particle effect
		CreateParticleEffect(visualData);

		// Create status icon
		CreateStatusIcon(visualData);

		GD.Print($"Added status visual: {status}");
	}

	/// <summary>
	/// Remove visual feedback for a status effect
	/// </summary>
	public void RemoveStatusVisual(string status)
	{
		status = status.ToLower();

		if (activeVisuals.ContainsKey(status))
		{
			var visualData = activeVisuals[status];

			// Clean up particle effect
			if (visualData.Particles != null && IsInstanceValid(visualData.Particles))
			{
				visualData.Particles.Emitting = false;
				visualData.Particles.QueueFree();
			}

			// Clean up icon
			if (visualData.Icon != null && IsInstanceValid(visualData.Icon))
			{
				visualData.Icon.QueueFree();
			}

			activeVisuals.Remove(status);
			GD.Print($"Removed status visual: {status}");
		}
	}

	/// <summary>
	/// Clear all status visuals
	/// </summary>
	public void ClearAll()
	{
		foreach (var kvp in activeVisuals)
		{
			RemoveStatusVisual(kvp.Key);
		}
		activeVisuals.Clear();
	}

	public override void _Process(double delta)
	{
		// Update pulse effect
		pulseTimer += (float)delta * PulseSpeed;

		// Apply pulsing color modulation to entity visual
		ApplyPulsingEffect();

		// Update status visual durations
		var toRemove = new List<string>();
		foreach (var kvp in activeVisuals)
		{
			kvp.Value.TimeRemaining -= (float)delta;
			if (kvp.Value.TimeRemaining <= 0)
			{
				toRemove.Add(kvp.Key);
			}
		}

		foreach (var status in toRemove)
		{
			RemoveStatusVisual(status);
		}
	}

	/// <summary>
	/// Apply pulsing effect to entity based on active statuses
	/// </summary>
	private void ApplyPulsingEffect()
	{
		if (entityVisual == null || !IsInstanceValid(entityVisual))
			return;

		if (activeVisuals.Count == 0)
		{
			// Reset to default if no status effects
			entityVisual.Modulate = new Color(1, 1, 1);
			return;
		}

		// Get highest priority status color
		var priorityColor = GetHighestPriorityColor();

		// Apply pulsing effect
		float pulse = (Mathf.Sin(pulseTimer) + 1) / 2; // 0 to 1
		float intensity = 0.3f + (pulse * 0.4f); // Pulse between 0.3 and 0.7

		var modulatedColor = new Color(
			Mathf.Lerp(1, priorityColor.R, intensity),
			Mathf.Lerp(1, priorityColor.G, intensity),
			Mathf.Lerp(1, priorityColor.B, intensity)
		);

		entityVisual.Modulate = modulatedColor;
	}

	/// <summary>
	/// Get the highest priority status effect color
	/// </summary>
	private Color GetHighestPriorityColor()
	{
		// Priority order: burning > frozen > stunned > poisoned > shocked > slowed
		string[] priorityOrder = { "burning", "frozen", "stunned", "poisoned", "shocked", "slowed", "feared", "weakened" };

		foreach (var status in priorityOrder)
		{
			if (activeVisuals.ContainsKey(status))
			{
				return activeVisuals[status].Color;
			}
		}

		return new Color(1, 1, 1);
	}

	/// <summary>
	/// Create particle effect for a status
	/// </summary>
	private void CreateParticleEffect(StatusVisualData visualData)
	{
		var particles = new GpuParticles2D();
		particles.Amount = 8;
		particles.Lifetime = 0.8f;
		particles.Emitting = true;
		particles.LocalCoords = false; // Emit in world space
		particles.Position = new Vector2(0, -20); // Above entity

		// Create particle material
		var material = new ParticleProcessMaterial();
		material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
		material.EmissionSphereRadius = 15f;
		material.Direction = new Vector3(0, -1, 0); // Float upward
		material.Spread = 30f;
		material.InitialVelocityMin = 15f;
		material.InitialVelocityMax = 30f;
		material.Gravity = new Vector3(0, -10, 0); // Float up
		material.ScaleMin = 2f;
		material.ScaleMax = 3f;
		material.Color = visualData.Color;

		// Gradient for fade-out
		var gradient = new Gradient();
		gradient.AddPoint(0f, visualData.Color);
		gradient.AddPoint(1f, new Color(visualData.Color.R, visualData.Color.G, visualData.Color.B, 0f));
		var gradientTexture = new GradientTexture1D();
		gradientTexture.Gradient = gradient;
		material.ColorRamp = gradientTexture;

		particles.ProcessMaterial = material;

		// Store reference
		visualData.Particles = particles;
		AddChild(particles);
	}

	/// <summary>
	/// Create a floating status icon
	/// </summary>
	private void CreateStatusIcon(StatusVisualData visualData)
	{
		// Create a small colored circle as the icon
		var icon = new ColorRect();
		icon.Size = new Vector2(8, 8);
		icon.Color = visualData.Color;

		// Position icons in a row
		int iconCount = activeVisuals.Count;
		float spacing = 10f;
		icon.Position = new Vector2((iconCount - 1) * spacing - (iconCount * spacing) / 2, 0);

		// Add a subtle glow border
		var border = new ColorRect();
		border.Size = new Vector2(10, 10);
		border.Position = new Vector2(-1, -1);
		border.Color = new Color(visualData.Color.R, visualData.Color.G, visualData.Color.B, 0.3f);
		icon.AddChild(border);
		icon.MoveChild(border, 0); // Move border behind main rect

		// Store reference
		visualData.Icon = icon;
		iconContainer.AddChild(icon);

		// Reposition all icons to keep them centered
		RepositionIcons();
	}

	/// <summary>
	/// Reposition all status icons in a centered row
	/// </summary>
	private void RepositionIcons()
	{
		int index = 0;
		float spacing = 10f;
		int count = activeVisuals.Count;

		foreach (var kvp in activeVisuals)
		{
			if (kvp.Value.Icon != null && IsInstanceValid(kvp.Value.Icon))
			{
				float totalWidth = (count - 1) * spacing;
				kvp.Value.Icon.Position = new Vector2(
					index * spacing - totalWidth / 2,
					0
				);
				index++;
			}
		}
	}

	/// <summary>
	/// Get color for a status effect
	/// </summary>
	private Color GetStatusColor(string status)
	{
		return status.ToLower() switch
		{
			"burning" => new Color(1.0f, 0.4f, 0.1f),     // Bright orange-red
			"frozen" => new Color(0.3f, 0.5f, 1.0f),      // Bright blue
			"poisoned" => new Color(0.3f, 1.0f, 0.3f),    // Bright green
			"shocked" => new Color(0.9f, 0.9f, 1.0f),     // Bright electric blue
			"slowed" => new Color(0.5f, 0.5f, 0.8f),      // Purple-blue
			"stunned" => new Color(1.0f, 1.0f, 0.3f),     // Yellow
			"feared" => new Color(0.6f, 0.3f, 0.8f),      // Purple
			"weakened" => new Color(0.7f, 0.7f, 0.7f),    // Gray
			_ => new Color(1, 1, 1)
		};
	}

	/// <summary>
	/// Get particle type for a status effect
	/// </summary>
	private ParticleType GetStatusParticleType(string status)
	{
		return status.ToLower() switch
		{
			"burning" => ParticleType.Lingering,
			"frozen" => ParticleType.Default,
			"poisoned" => ParticleType.Lingering,
			"shocked" => ParticleType.Burst,
			_ => ParticleType.Default
		};
	}
}

/// <summary>
/// Data for tracking active status visuals
/// </summary>
public class StatusVisualData
{
	public string Status { get; set; }
	public float TimeRemaining { get; set; }
	public Color Color { get; set; }
	public ParticleType ParticleType { get; set; }
	public GpuParticles2D Particles { get; set; }
	public ColorRect Icon { get; set; }
}
