using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lexmancer.Abilities.V2;

namespace Lexmancer.Elements;

/// <summary>
/// Represents a combinable element (Fire, Steam, Lava, etc.)
/// Elements are both useable (have abilities) and combinable (can create new elements)
/// </summary>
public class Element
{
	/// <summary>
	/// Unique identifier for storage and lookup: "fire", "steam", "lava_pool"
	/// For base elements, this is the primitive name in lowercase
	/// For dynamic elements, this is generated or from LLM
	/// </summary>
	[JsonPropertyName("id")]
	public string Id { get; set; }

	/// <summary>
	/// Primitive type enum value (for semantic categorization)
	/// Set for base elements, null for dynamic/combined elements
	/// </summary>
	[JsonPropertyName("primitive")]
	[JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
	public PrimitiveType? Primitive { get; set; }

	/// <summary>
	/// Display name: "Fire", "Steam", "Molten Lava"
	/// </summary>
	[JsonPropertyName("name")]
	public string Name { get; set; }

	/// <summary>
	/// Flavor text description
	/// </summary>
	[JsonPropertyName("description")]
	public string Description { get; set; }

	/// <summary>
	/// Tier: 1 (base) → 2 (two-way) → 3 (three-way) → 4 (legendary)
	/// </summary>
	[JsonPropertyName("tier")]
	public int Tier { get; set; }

	/// <summary>
	/// Recipe: List of element IDs that combine to create this element
	/// Empty for base elements, ["fire", "ice"] for Steam, etc.
	/// </summary>
	[JsonPropertyName("recipe")]
	public List<string> Recipe { get; set; } = new();

	/// <summary>
	/// Color as hex string for JSON serialization: "#FF4500"
	/// </summary>
	[JsonPropertyName("colorHex")]
	public string ColorHex { get; set; } = "#FFFFFF";

	/// <summary>
	/// Serialized AbilityV2 JSON string
	/// </summary>
	[JsonPropertyName("abilityJson")]
	public string AbilityJson { get; set; }

	/// <summary>
	/// Schema version for future migrations
	/// </summary>
	[JsonPropertyName("version")]
	public int Version { get; set; } = 1;

	/// <summary>
	/// Unix timestamp when element was created
	/// </summary>
	[JsonPropertyName("createdAt")]
	public long CreatedAt { get; set; }

	/// <summary>
	/// Whether this element has been discovered by the player
	/// Note: This is overridden by PlayerElementCollection for per-player tracking
	/// </summary>
	[JsonPropertyName("isDiscovered")]
	public bool IsDiscovered { get; set; } = false;

	// ==================== RUNTIME PROPERTIES ====================

	/// <summary>
	/// Godot Color parsed from ColorHex
	/// </summary>
	[JsonIgnore]
	public Color Color
	{
		get
		{
			if (string.IsNullOrEmpty(ColorHex))
				return new Color(1, 1, 1); // White default

			try
			{
				return Color.FromHtml(ColorHex);
			}
			catch
			{
				GD.PrintErr($"Invalid color hex: {ColorHex}");
				return new Color(1, 1, 1);
			}
		}
	}

	/// <summary>
	/// Lazy-loaded ability from AbilityJson
	/// </summary>
	[JsonIgnore]
	private AbilityV2 ability;

	[JsonIgnore]
	public AbilityV2 Ability
	{
		get
		{
			if (ability == null && !string.IsNullOrEmpty(AbilityJson))
			{
				try
				{
					GD.Print($"[Element.Ability] Lazy-loading ability for {Name} from JSON ({AbilityJson.Length} chars)");
					ability = AbilityV2.FromJson(AbilityJson);
					GD.Print($"[Element.Ability] ✓ Successfully loaded ability");
				}
				catch (Exception ex)
				{
					GD.PrintErr($"❌ Failed to load ability for element {Name}: {ex.Message}");
					GD.PrintErr($"   Stack trace: {ex.StackTrace}");
					GD.PrintErr($"   AbilityJson preview: {AbilityJson.Substring(0, Math.Min(300, AbilityJson.Length))}");
				}
			}
			else if (ability == null)
			{
				GD.PrintErr($"❌ Element {Name} has no AbilityJson to load!");
			}
			return ability;
		}
		set
		{
			ability = value;
			AbilityJson = value?.ToJson();
		}
	}

	// ==================== METHODS ====================

	/// <summary>
	/// Default constructor
	/// </summary>
	public Element()
	{
		CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	/// <summary>
	/// Serialize to JSON string
	/// </summary>
	public string ToJson()
	{
		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};
		return JsonSerializer.Serialize(this, options);
	}

	/// <summary>
	/// Deserialize from JSON string
	/// </summary>
	public static Element FromJson(string json)
	{
		try
		{
			var options = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase
			};
			var element = JsonSerializer.Deserialize<Element>(json, options);

			if (element == null)
				throw new InvalidOperationException("Deserialization returned null");

			element.Validate();
			return element;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to deserialize element from JSON: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Validate element properties
	/// </summary>
	private void Validate()
	{
		// Ensure required fields
		if (string.IsNullOrEmpty(Id))
		{
			GD.PrintErr("Element ID is required");
			Id = "unknown";
		}

		if (string.IsNullOrEmpty(Name))
		{
			GD.PrintErr($"Element name is empty for ID: {Id}");
			Name = "Unnamed Element";
		}

		// Clamp tier to valid range
		if (Tier < 1 || Tier > 4)
		{
			GD.PrintErr($"Invalid tier {Tier} for element {Id}, clamping to 1-4");
			Tier = Math.Clamp(Tier, 1, 4);
		}

		// Ensure recipe list exists
		if (Recipe == null)
			Recipe = new List<string>();

		// Validate recipe count matches tier (loosely)
		// Tier 1: empty recipe
		// Tier 2: typically 2 elements
		// Tier 3: typically 3 elements
		// Tier 4: typically 4 elements
		if (Tier == 1 && Recipe.Count > 0)
		{
			GD.PrintErr($"Base element {Id} should have empty recipe");
		}

		// Set default CreatedAt if not set
		if (CreatedAt == 0)
			CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	/// <summary>
	/// Get recipe as sorted string key for caching
	/// </summary>
	public string GetRecipeKey()
	{
		if (Recipe == null || Recipe.Count == 0)
			return Id;

		var sorted = new List<string>(Recipe);
		sorted.Sort();
		return string.Join("_", sorted);
	}

	public override string ToString()
	{
		return $"Element[{Id}] {Name} (Tier {Tier})";
	}
}
