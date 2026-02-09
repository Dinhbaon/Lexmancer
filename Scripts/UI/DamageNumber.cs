using Godot;
using System;

namespace Lexmancer.UI;

/// <summary>
/// Floating damage number that appears above entities when they take damage
/// Automatically floats upward and fades out
/// </summary>
public partial class DamageNumber : Node2D
{
	[Export] public float FloatSpeed { get; set; } = 50f;
	[Export] public float Lifetime { get; set; } = 1.0f;
	[Export] public float FadeStartTime { get; set; } = 0.5f;

	private Label label;
	private float timeAlive = 0f;
	private Color originalColor;

	public override void _Ready()
	{
		// Label should be created before adding to tree
		if (label == null)
		{
			GD.PrintErr("DamageNumber: Label not set before _Ready!");
			QueueFree();
		}
	}

	/// <summary>
	/// Initialize the damage number with a value and color
	/// Call this before adding to the scene tree
	/// </summary>
	public void Initialize(float damageAmount, Color color, bool isCritical = false)
	{
		// Create label
		label = new Label();
		label.Text = Mathf.RoundToInt(damageAmount).ToString();
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeFontSizeOverride("font_size", isCritical ? 24 : 18);

		// Center the label
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.Position = new Vector2(-20, -10); // Offset to center text

		// Add outline for visibility
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 2);

		AddChild(label);
		originalColor = color;

		// Randomize horizontal offset slightly for visual variety
		var randomOffset = new Vector2((float)GD.RandRange(-10, 10), 0);
		Position += randomOffset;
	}

	public override void _Process(double delta)
	{
		// Float upward
		Position += Vector2.Up * FloatSpeed * (float)delta;

		// Track lifetime
		timeAlive += (float)delta;

		// Fade out near end of lifetime
		if (timeAlive >= FadeStartTime)
		{
			float fadeProgress = (timeAlive - FadeStartTime) / (Lifetime - FadeStartTime);
			float alpha = 1.0f - fadeProgress;

			if (label != null)
			{
				var color = originalColor;
				color.A = alpha;
				label.AddThemeColorOverride("font_color", color);
			}
		}

		// Cleanup after lifetime
		if (timeAlive >= Lifetime)
		{
			QueueFree();
		}
	}

	/// <summary>
	/// Create a damage number at a specific position
	/// Automatically adds to the world node
	/// </summary>
	public static void Spawn(Node worldNode, Vector2 position, float damage, DamageNumberType type = DamageNumberType.Normal)
	{
		if (worldNode == null)
		{
			GD.PrintErr("DamageNumber.Spawn: worldNode is null!");
			return;
		}

		// Don't show numbers for very small damage (< 1)
		// This prevents showing "0" for tick damage
		if (damage < 1.0f && type != DamageNumberType.Healing)
		{
			return;
		}

		var damageNumber = new DamageNumber();
		damageNumber.GlobalPosition = position;

		// Color based on type
		Color color = type switch
		{
			DamageNumberType.Normal => new Color(1.0f, 0.9f, 0.9f), // White-ish
			DamageNumberType.Critical => new Color(1.0f, 0.3f, 0.0f), // Orange-red
			DamageNumberType.Healing => new Color(0.3f, 1.0f, 0.3f), // Green
			DamageNumberType.Status => new Color(0.8f, 0.5f, 1.0f), // Purple
			_ => Colors.White
		};

		bool isCritical = type == DamageNumberType.Critical;
		damageNumber.Initialize(damage, color, isCritical);

		worldNode.AddChild(damageNumber);
	}
}

/// <summary>
/// Type of damage number to display
/// </summary>
public enum DamageNumberType
{
	Normal,    // White - regular damage
	Critical,  // Orange-red - critical hits (future)
	Healing,   // Green - healing
	Status     // Purple - status effect damage (poison, burn, etc.)
}
