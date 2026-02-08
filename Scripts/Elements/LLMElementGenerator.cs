using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Lexmancer.Abilities.V2;

namespace Lexmancer.Elements;

/// <summary>
/// Generates element abilities using LLM
/// Bridges the gap between element system and primitive-based LLM generation
/// </summary>
public class LLMElementGenerator
{
	private readonly AbilityGeneratorV2 abilityGenerator;
	private readonly bool useLLM;
	private readonly LLMClientV2 llmClient;

	public LLMElementGenerator(
		string playerId = "player_001",
		bool useLLM = true,
		string llmBaseUrl = "http://localhost:11434",
		string llmModel = "qwen2.5:7b")
	{
		this.useLLM = useLLM;

		if (useLLM)
		{
			abilityGenerator = new AbilityGeneratorV2(playerId, llmBaseUrl, llmModel);
			llmClient = new LLMClientV2(llmBaseUrl, llmModel);
			GD.Print($"LLMElementGenerator initialized with LLM enabled ({llmModel})");
		}
		else
		{
			GD.Print("LLMElementGenerator initialized with LLM disabled (using fallbacks)");
		}
	}

	/// <summary>
	/// Generate a completely new element from combining two elements (name + ability)
	/// This is the main method for dynamic element creation!
	/// </summary>
	public async Task<Element> GenerateElementFromCombinationAsync(string element1Id, string element2Id, bool forceNew = false)
	{
		if (!useLLM)
		{
			GD.PrintErr("LLM is disabled - cannot generate dynamic elements");
			return null;
		}

		try
		{
			// Get element info (check registry first, then base elements)
			Element elem1 = ElementRegistry.GetElement(element1Id)
				?? ElementDefinitions.BaseElements.GetValueOrDefault(element1Id);
			Element elem2 = ElementRegistry.GetElement(element2Id)
				?? ElementDefinitions.BaseElements.GetValueOrDefault(element2Id);

			if (elem1 == null || elem2 == null)
			{
				GD.PrintErr($"Cannot find elements: {element1Id}, {element2Id}");
				return null;
			}

			GD.Print($"🔮 Generating NEW element from {elem1.Name} + {elem2.Name}...");

			// Generate the element and ability using LLM
			var result = await GenerateElementWithLLMAsync(elem1, elem2);

			GD.Print($"✨ Created element: {result.Name}");

			return result;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to generate element combination: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Generate or retrieve an ability for an element
	/// </summary>
	public async Task<AbilityV2> GenerateAbilityForElementAsync(Element element, bool forceNew = false)
	{
		if (!useLLM)
		{
			// Fallback to hardcoded abilities
			return GetFallbackAbility(element);
		}

		// Convert element to primitives
		var primitives = GetPrimitivesForElement(element);

		if (primitives.Count == 0)
		{
			GD.PrintErr($"No primitives mapped for element: {element.Name}");
			return GetFallbackAbility(element);
		}

		try
		{
			GD.Print($"Generating LLM ability for {element.Name} ({string.Join("+", primitives)})");

			// Generate using LLM
			var result = await abilityGenerator.GenerateAbilityAsync(primitives, forceNew);

			if (result.WasCached)
			{
				GD.Print("✓ Using cached ability");
			}
			else
			{
				GD.Print("✨ Generated NEW ability");
			}

			// Customize description with element flavor
			result.Ability.Description = CustomizeDescription(element, result.Ability.Description);

			return result.Ability;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"LLM generation failed for {element.Name}: {ex.Message}");
			return GetFallbackAbility(element);
		}
	}

	/// <summary>
	/// Get primitives for an element based on its recipe
	/// </summary>
	private List<PrimitiveType> GetPrimitivesForElement(Element element)
	{
		var primitives = new List<PrimitiveType>();

		if (element.Recipe == null || element.Recipe.Count == 0)
		{
			// Base element - use primitive directly
			if (element.Primitive.HasValue)
			{
				primitives.Add(element.Primitive.Value);
			}
		}
		else
		{
			// Combined element - look up recipe ingredients and get their primitives
			foreach (var ingredientId in element.Recipe)
			{
				var ingredient = ElementDefinitions.BaseElements.GetValueOrDefault(ingredientId);
				if (ingredient?.Primitive != null)
				{
					primitives.Add(ingredient.Primitive.Value);
				}
			}
		}

		return primitives;
	}

	/// <summary>
	/// Customize description with element context
	/// </summary>
	private string CustomizeDescription(Element element, string llmDescription)
	{
		// Add element context if not already present
		if (llmDescription.ToLower().Contains(element.Name.ToLower()))
			return llmDescription;

		return $"[{element.Name}] {llmDescription}";
	}

	/// <summary>
	/// Get fallback ability when LLM is disabled or fails
	/// Uses the hardcoded abilities from ElementDefinitions
	/// </summary>
	private AbilityV2 GetFallbackAbility(Element element)
	{
		// Try to get from ElementDefinitions (use ID to look up)
		if (ElementDefinitions.BaseElements.TryGetValue(element.Id, out var baseElement))
		{
			return baseElement.Ability;
		}

		// Last resort: create a basic projectile ability
		GD.PrintErr($"No fallback ability found for {element.Name}, creating basic projectile");

		return new AbilityV2
		{
			Description = $"A basic {element.Name} projectile",
			Primitives = new() { element.Name },
			Cooldown = 1.0f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_projectile",
							Args = new() { ["count"] = 1, ["speed"] = 400 },
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new() { ["amount"] = 20, ["element"] = element.Name }
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Use LLM to generate element name, description, color, and ability
	/// </summary>
	private async Task<Element> GenerateElementWithLLMAsync(Element elem1, Element elem2)
	{
		var prompt = BuildElementGenerationPrompt(elem1, elem2);

		GD.Print($"Sending element generation request to LLM...");

		// Call LLM with full element information
		var response = await llmClient.GenerateElementAsync(
			elem1.Name,
			elem2.Name,
			elem1.Description,
			elem2.Description);

		// Serialize to JSON for debugging
		var options = new JsonSerializerOptions { WriteIndented = true };
		var jsonString = JsonSerializer.Serialize(response, options);
		GD.Print($"LLM JSON Response:\n{jsonString}");

		// Parse response into Element (dynamic element, no primitive type)
		var newElement = new Element
		{
			Id = response.Id.ToLower().Replace(" ", "_"),
			Primitive = null,  // Dynamic elements don't have a primitive type
			Name = response.Name,
			Description = response.Description,
			ColorHex = response.ColorHex,
			Tier = 2,
			Recipe = new List<string> { elem1.Id, elem2.Id },
			Ability = response.Ability
		};

		return newElement;
	}

	/// <summary>
	/// Build creative prompt for element generation
	/// </summary>
	private string BuildElementGenerationPrompt(Element elem1, Element elem2)
	{
		return $@"You are a creative game designer creating magical elements for a roguelike game.

Two base elements are being combined:
- {elem1.Name}: {elem1.Description}
- {elem2.Name}: {elem2.Description}

Generate a NEW, UNIQUE element that emerges from this combination.

IMPORTANT GUIDELINES:
- Be CREATIVE! Think beyond obvious combinations
- {elem1.Name} + {elem2.Name} could create MANY different results
- Consider physical, magical, conceptual, and thematic combinations
- Each generation should feel unique and interesting

Examples of creative thinking:
- Fire + Earth could be: Lava, Magma, Hellstone, Obsidian, Ashrock, Cinders, Molten Core
- Water + Fire could be: Steam, Mist, Boiling Water, Thermal Vent, Geysir, Scalding Fog
- Earth + Water could be: Mud, Clay, Quicksand, Swamp, Silt, Fertile Soil

Generate the element with these properties:
1. Name (1-2 words, evocative and unique)
2. Description (1-2 sentences describing what it is)
3. Color (hex code like #FF5500)
4. An ability (using the effect scripting system)

Return as JSON.";
	}

	public void Dispose()
	{
		abilityGenerator?.Dispose();
	}
}
