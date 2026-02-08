using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Lexmancer.Abilities.Visuals;

/// <summary>
/// Centralized visual system for ability effects
/// Maps elements to colors, particles, and visual properties
/// </summary>
public static class VisualSystem
{
	/// <summary>
	/// Element to color mapping
	/// </summary>
	private static readonly Dictionary<string, Color> ElementColors = new()
	{
		["fire"] = new Color("#FF4500"),      // Orange-red
		["water"] = new Color("#1E90FF"),     // Dodger blue
		["ice"] = new Color("#87CEEB"),       // Sky blue
		["earth"] = new Color("#8B4513"),     // Saddle brown
		["lightning"] = new Color("#FFD700"), // Gold
		["poison"] = new Color("#9932CC"),    // Dark orchid
		["wind"] = new Color("#87CEEB"),      // Sky blue
		["shadow"] = new Color("#2F4F4F"),    // Dark slate gray
		["light"] = new Color("#FFFACD"),     // Lemon chiffon

		// Combined elements (can be overridden by LLM later)
		["steam"] = new Color("#E0FFFF"),     // Light cyan
		["lava"] = new Color("#FF6347"),      // Tomato red
		["mud"] = new Color("#654321"),       // Dark brown
		["magma"] = new Color("#DC143C"),     // Crimson
		["frost"] = new Color("#B0E0E6"),     // Powder blue
		["storm"] = new Color("#4682B4"),     // Steel blue

		// Neutral fallback
		["neutral"] = new Color("#FFFFFF")    // White
	};

	/// <summary>
	/// Get color for an element (from primitives or element name)
	/// </summary>
	public static Color GetElementColor(List<string> primitives)
	{
		if (primitives == null || primitives.Count == 0)
			return ElementColors["neutral"];

		// Try to find exact match first
		var primitiveLower = primitives[0].ToLower();
		if (ElementColors.ContainsKey(primitiveLower))
			return ElementColors[primitiveLower];

		// If multiple primitives, blend colors
		if (primitives.Count > 1)
		{
			return BlendColors(primitives);
		}

		return ElementColors["neutral"];
	}

	/// <summary>
	/// Get color from single element string
	/// </summary>
	public static Color GetElementColor(string element)
	{
		if (string.IsNullOrEmpty(element))
			return ElementColors["neutral"];

		var elementLower = element.ToLower();
		return ElementColors.ContainsKey(elementLower)
			? ElementColors[elementLower]
			: ElementColors["neutral"];
	}

	/// <summary>
	/// Blend multiple element colors together
	/// </summary>
	private static Color BlendColors(List<string> primitives)
	{
		var colors = primitives
			.Select(p => ElementColors.ContainsKey(p.ToLower())
				? ElementColors[p.ToLower()]
				: ElementColors["neutral"])
			.ToList();

		if (colors.Count == 0)
			return ElementColors["neutral"];

		// Average the colors
		float r = colors.Average(c => c.R);
		float g = colors.Average(c => c.G);
		float b = colors.Average(c => c.B);

		return new Color(r, g, b);
	}

	/// <summary>
	/// Create a particle material for an element
	/// </summary>
	public static ParticleProcessMaterial CreateParticleMaterial(Color baseColor, ParticleType type = ParticleType.Default)
	{
		var material = new ParticleProcessMaterial();

		// Base properties
		material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
		material.EmissionSphereRadius = 2f;

		// Color
		material.Color = baseColor;

		// Gradient for fade-out
		var gradient = new Gradient();
		gradient.AddPoint(0f, baseColor);
		gradient.AddPoint(1f, new Color(baseColor.R, baseColor.G, baseColor.B, 0f)); // Fade to transparent

		var gradientTexture = new GradientTexture1D();
		gradientTexture.Gradient = gradient;
		material.ColorRamp = gradientTexture;

		// Type-specific properties
		switch (type)
		{
			case ParticleType.Trail:
				material.Direction = new Vector3(0, 0, 0); // Emit in all directions
				material.Spread = 20f;
				material.InitialVelocityMin = 20f;
				material.InitialVelocityMax = 50f;
				material.Gravity = new Vector3(0, 0, 0);
				material.ScaleMin = 1.5f;
				material.ScaleMax = 1.5f;
				break;

			case ParticleType.Burst:
				material.Direction = new Vector3(0, 0, 0);
				material.Spread = 180f;
				material.InitialVelocityMin = 100f;
				material.InitialVelocityMax = 200f;
				material.Gravity = new Vector3(0, 50, 0); // Slight downward gravity
				material.ScaleMin = 2f;
				material.ScaleMax = 2f;
				break;

			case ParticleType.Lingering:
				material.Direction = new Vector3(0, -1, 0); // Float upward
				material.Spread = 45f;
				material.InitialVelocityMin = 10f;
				material.InitialVelocityMax = 30f;
				material.Gravity = new Vector3(0, -20, 0); // Float up
				material.ScaleMin = 2.5f;
				material.ScaleMax = 2.5f;
				break;

			default: // ParticleType.Default
				material.Direction = new Vector3(0, 0, 0);
				material.Spread = 30f;
				material.InitialVelocityMin = 30f;
				material.InitialVelocityMax = 60f;
				material.Gravity = new Vector3(0, 0, 0);
				material.ScaleMin = 2f;
				material.ScaleMax = 2f;
				break;
		}

		return material;
	}

	/// <summary>
	/// Create a simple colored circle texture for projectiles
	/// Can be replaced with actual sprites later
	/// </summary>
	public static ColorRect CreateProjectileVisual(Color color, float size = 12)
	{
		var visual = new ColorRect();
		visual.Color = color;
		visual.Size = new Vector2(size, size);
		visual.Position = new Vector2(-size / 2, -size / 2);

		return visual;
	}

	/// <summary>
	/// Add a glow effect to a node
	/// </summary>
	public static void AddGlow(Node2D node, Color color, float intensity = 1.0f)
	{
		// Create a PointLight2D for glow effect
		var light = new PointLight2D();
		light.Color = color;
		light.Energy = intensity;
		light.Texture = GD.Load<Texture2D>("res://icon.svg"); // Fallback texture, replace with proper glow texture
		light.TextureScale = 0.5f;

		node.AddChild(light);
	}
}

/// <summary>
/// Particle behavior types
/// </summary>
public enum ParticleType
{
	Default,   // General purpose
	Trail,     // Follows projectile
	Burst,     // Explosion on impact
	Lingering  // Floats in area effects
}
