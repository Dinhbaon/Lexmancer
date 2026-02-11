using System;
using System.Text.Json.Serialization;

namespace Lexmancer.Elements;

/// <summary>
/// Procedural tuning data for elements (damage ranges, status effects, etc.)
/// </summary>
public class ElementProperties
{
	[JsonPropertyName("minDamage")]
	public int MinDamage { get; set; } = 12;

	[JsonPropertyName("maxDamage")]
	public int MaxDamage { get; set; } = 24;

	[JsonPropertyName("primaryStatus")]
	public string PrimaryStatus { get; set; }

	[JsonPropertyName("statusDuration")]
	public float StatusDuration { get; set; } = 0f;

	[JsonPropertyName("secondaryStatus")]
	public string SecondaryStatus { get; set; }

	[JsonPropertyName("secondaryStatusDuration")]
	public float SecondaryStatusDuration { get; set; } = 0f;

	public static ElementProperties CreateDefault()
	{
		return new ElementProperties();
	}

	/// <summary>
	/// Merge two property sets into a single blended result.
	/// Damage ranges are averaged; statuses prefer primary then secondary.
	/// </summary>
	public static ElementProperties Merge(ElementProperties a, ElementProperties b)
	{
		if (a == null && b == null)
			return CreateDefault();
		if (a == null)
			return b;
		if (b == null)
			return a;

		var merged = new ElementProperties
		{
			MinDamage = (int)Math.Round((a.MinDamage + b.MinDamage) / 2.0),
			MaxDamage = (int)Math.Round((a.MaxDamage + b.MaxDamage) / 2.0),
			PrimaryStatus = !string.IsNullOrEmpty(a.PrimaryStatus) ? a.PrimaryStatus : b.PrimaryStatus,
			StatusDuration = Math.Max(a.StatusDuration, b.StatusDuration),
			SecondaryStatus = !string.IsNullOrEmpty(a.SecondaryStatus) ? a.SecondaryStatus : b.SecondaryStatus,
			SecondaryStatusDuration = Math.Max(a.SecondaryStatusDuration, b.SecondaryStatusDuration)
		};

		// Ensure sane ranges
		if (merged.MinDamage < 1)
			merged.MinDamage = 1;
		if (merged.MaxDamage < merged.MinDamage)
			merged.MaxDamage = merged.MinDamage;

		return merged;
	}

	public override string ToString()
	{
		return $"DMG {MinDamage}-{MaxDamage}, Status {PrimaryStatus}({StatusDuration:0.##})";
	}
}
