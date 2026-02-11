using Godot;
using System;

namespace Lexmancer.Elements;

/// <summary>
/// Metadata that guides procedural ability generation
/// Defines elemental characteristics that influence delivery method, damage, and behavior
/// </summary>
public class ElementProperties
{
	// ==================== DAMAGE CHARACTERISTICS ====================

	/// <summary>Minimum damage value</summary>
	public int MinDamage { get; set; }

	/// <summary>Maximum damage value</summary>
	public int MaxDamage { get; set; }

	// ==================== STATUS EFFECTS ====================

	/// <summary>Primary status effect (burning, slowed, stunned, etc.)</summary>
	public string PrimaryStatus { get; set; }

	/// <summary>Duration of primary status effect in seconds</summary>
	public float StatusDuration { get; set; }

	/// <summary>Secondary status effect (optional)</summary>
	public string SecondaryStatus { get; set; }

	/// <summary>Duration of secondary status effect in seconds</summary>
	public float SecondaryStatusDuration { get; set; }

	/// <summary>
	/// Create default properties (for dynamic elements without defined properties)
	/// </summary>
	public static ElementProperties CreateDefault()
	{
		return new ElementProperties
		{
			MinDamage = 17,
			MaxDamage = 21,
			PrimaryStatus = null,
			StatusDuration = 0f
		};
	}

	/// <summary>
	/// Merge two element properties (for combination generation)
	/// </summary>
	public static ElementProperties Merge(ElementProperties props1, ElementProperties props2)
	{
		var merged = new ElementProperties
		{
			// Average damage ranges
			MinDamage = (props1.MinDamage + props2.MinDamage) / 2,
			MaxDamage = (props1.MaxDamage + props2.MaxDamage) / 2,

			// Combine status effects (primary from first, secondary from second)
			PrimaryStatus = props1.PrimaryStatus,
			StatusDuration = props1.StatusDuration,
			SecondaryStatus = props2.PrimaryStatus,
			SecondaryStatusDuration = props2.StatusDuration
		};

		return merged;
	}

	public override string ToString()
	{
		return $"ElementProperties[Damage:{MinDamage}-{MaxDamage} " +
		       $"Primary:{PrimaryStatus ?? "none"}({StatusDuration:F1}s) " +
		       $"Secondary:{SecondaryStatus ?? "none"}({SecondaryStatusDuration:F1}s)]";
	}
}
