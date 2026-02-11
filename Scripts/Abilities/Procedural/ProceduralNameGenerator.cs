using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Elements;

namespace Lexmancer.Abilities.Procedural;

/// <summary>
/// Template-based name generator for procedurally created elements
/// Serves as fallback when LLM is unavailable or fails
/// </summary>
public class ProceduralNameGenerator
{
	// Word banks per element type (adjectives, nouns)
	private static readonly Dictionary<PrimitiveType, (List<string> Adj, List<string> Noun)> WordBanks = new()
	{
		{
			PrimitiveType.Fire,
			(
				new List<string> { "Blazing", "Scorching", "Infernal", "Volcanic", "Searing", "Molten", "Burning", "Fiery" },
				new List<string> { "Inferno", "Pyre", "Blaze", "Flame", "Cinder", "Ember", "Conflagration", "Eruption" }
			)
		},
		{
			PrimitiveType.Ice,
			(
				new List<string> { "Frozen", "Icy", "Glacial", "Frigid", "Chilling", "Arctic", "Crystalline", "Frost" },
				new List<string> { "Ice", "Frost", "Glacier", "Crystal", "Hail", "Freeze", "Blizzard", "Shard" }
			)
		},
		{
			PrimitiveType.Earth,
			(
				new List<string> { "Rocky", "Solid", "Crushing", "Mighty", "Stone", "Granite", "Boulder", "Earthen" },
				new List<string> { "Stone", "Rock", "Boulder", "Quake", "Slam", "Crag", "Granite", "Basalt" }
			)
		},
		{
			PrimitiveType.Lightning,
			(
				new List<string> { "Shocking", "Thundering", "Voltaic", "Electric", "Crackling", "Charged", "Storming", "Sparking" },
				new List<string> { "Bolt", "Strike", "Discharge", "Thunder", "Voltage", "Arc", "Shock", "Storm" }
			)
		},
		{
			PrimitiveType.Poison,
			(
				new List<string> { "Toxic", "Venomous", "Noxious", "Corrosive", "Putrid", "Virulent", "Festering", "Vile" },
				new List<string> { "Venom", "Toxin", "Poison", "Miasma", "Blight", "Plague", "Corruption", "Acid" }
			)
		},
		{
			PrimitiveType.Wind,
			(
				new List<string> { "Rushing", "Howling", "Swirling", "Gale", "Tempest", "Breezy", "Whirling", "Gusty" },
				new List<string> { "Wind", "Gale", "Tempest", "Breeze", "Cyclone", "Zephyr", "Gust", "Whirlwind" }
			)
		},
		{
			PrimitiveType.Shadow,
			(
				new List<string> { "Dark", "Shadowy", "Umbral", "Tenebrous", "Murky", "Obscure", "Dim", "Gloom" },
				new List<string> { "Shadow", "Darkness", "Gloom", "Shade", "Eclipse", "Void", "Umbra", "Dusk" }
			)
		},
		{
			PrimitiveType.Light,
			(
				new List<string> { "Radiant", "Brilliant", "Gleaming", "Luminous", "Shining", "Bright", "Glowing", "Celestial" },
				new List<string> { "Light", "Radiance", "Brilliance", "Glow", "Shine", "Luster", "Beam", "Ray" }
			)
		}
	};

	private Random rng;

	/// <summary>
	/// Generate a procedural name for a combined element
	/// </summary>
	public string GenerateName(Element elem1, Element elem2, int seed)
	{
		rng = new Random(seed);

		// Get word banks for both elements
		var (adj1, noun1) = GetWordBank(elem1);
		var (adj2, noun2) = GetWordBank(elem2);

		// Choose format randomly
		return rng.Next(0, 5) switch
		{
			0 => $"{PickFrom(adj1)} {PickFrom(noun2)}", // "Blazing Ice"
			1 => $"{PickFrom(noun1)} of {PickFrom(adj2)} {PickFrom(noun2)}", // "Flame of Frozen Crystal"
			2 => $"{PickFrom(adj1)} {PickFrom(adj2)} {PickFrom(noun1)}", // "Blazing Icy Inferno"
			3 => $"{PickFrom(noun1)}-{PickFrom(noun2)}", // "Flame-Ice"
			_ => $"{elem1.Name}-{elem2.Name} Fusion" // "Fire-Water Fusion"
		};
	}

	/// <summary>
	/// Generate a technical description from ability mechanics
	/// </summary>
	public string GenerateDescription(AbilityV2 ability)
	{
		// Reuse existing AbilityV2.BuildDescriptionFromEffects()
		ability.EnsureDescription();
		return ability.Description;
	}

	/// <summary>
	/// Blend two element colors
	/// </summary>
	public string BlendColors(string hex1, string hex2)
	{
		try
		{
			Color c1 = Color.FromHtml(hex1);
			Color c2 = Color.FromHtml(hex2);
			Color blended = c1.Lerp(c2, 0.5f);
			return blended.ToHtml(false);
		}
		catch
		{
			GD.PrintErr($"Failed to blend colors: {hex1}, {hex2}");
			return "#808080"; // Gray fallback
		}
	}

	/// <summary>
	/// Get word bank for an element (handles primitives and non-primitives)
	/// </summary>
	private (List<string> Adj, List<string> Noun) GetWordBank(Element elem)
	{
		if (elem.Primitive.HasValue && WordBanks.TryGetValue(elem.Primitive.Value, out var bank))
		{
			return bank;
		}

		// Fallback for non-primitive elements
		var genericAdj = new List<string> { "Mystic", "Arcane", "Ethereal", "Primal", "Ancient", "Potent" };
		var genericNoun = new List<string> { "Force", "Power", "Energy", "Essence", "Spirit", "Manifestation" };
		return (genericAdj, genericNoun);
	}

	/// <summary>
	/// Pick a random word from a list
	/// </summary>
	private string PickFrom(List<string> words)
	{
		if (words == null || words.Count == 0)
			return "Unknown";

		return words[rng.Next(0, words.Count)];
	}
}
