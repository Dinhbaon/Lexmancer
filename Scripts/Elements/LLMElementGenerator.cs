using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.Procedural;
using Lexmancer.Core;
using Lexmancer.Services;

namespace Lexmancer.Elements;

/// <summary>
/// Generates element abilities using hybrid procedural + LLM approach
/// Instant procedural mechanics + async LLM flavor text
/// </summary>
public class LLMElementGenerator
{
    private bool useLLM;
    private readonly LLMClientV2 llmClient;
    private readonly ProceduralAbilityComposer proceduralComposer;
    private readonly ProceduralNameGenerator nameGenerator;

    public LLMElementGenerator(
        string playerId = "player_001",
        bool useLLM = true,
        string llmBaseUrl = "http://localhost:11434",
        string llmModel = "qwen2.5:7b",
        LLMClientV2 llmClient = null)
    {
        this.useLLM = useLLM;

        // Initialize procedural generators (always available)
        this.proceduralComposer = new ProceduralAbilityComposer();
        this.nameGenerator = new ProceduralNameGenerator();

        // Allow DI of a shared client so transport selection is centralized.
        if (llmClient != null)
        {
            this.llmClient = llmClient;
            if (useLLM)
                GD.Print("LLMElementGenerator using injected client");
        }
        else if (useLLM)
        {
            this.llmClient = new LLMClientV2(llmBaseUrl, llmModel);
            GD.Print($"LLMElementGenerator initialized with LLM enabled ({llmModel})");
        }

        if (!useLLM)
        {
            GD.Print("LLMElementGenerator initialized with LLM disabled (using fallbacks)");
        }

        GD.Print("Procedural generators initialized (always available)");
    }

    /// <summary>
    /// Enable/disable LLM usage at runtime.
    /// </summary>
    public void SetUseLLM(bool enabled) => useLLM = enabled;

	/// <summary>
	/// Check if a combination has been generated before (cached in ElementService)
	/// Returns the cached element if found, otherwise null
	/// </summary>
	public Element GetCachedCombination(int element1Id, int element2Id)
	{
		// Check ElementService for all previously generated combinations
		var allElements = ServiceLocator.Instance.Elements.GetAllElements();

		foreach (var element in allElements)
		{
			// Check if this element was created from these two ingredients (in either order)
			if (element.Recipe != null && element.Recipe.Count == 2)
			{
				bool isMatch = (element.Recipe[0] == element1Id && element.Recipe[1] == element2Id) ||
				               (element.Recipe[0] == element2Id && element.Recipe[1] == element1Id);

				if (isMatch)
				{
					GD.Print($"Found cached combination: {element.Name}");
					return element;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Generate a completely new element from combining two elements (name + ability)
	/// This is the main method for dynamic element creation!
	///
	/// HYBRID MODE: Generates mechanics procedurally (instant) + LLM flavor (async)
	/// FULL LLM MODE: Uses legacy full LLM generation (slow)
	/// PURE PROCEDURAL MODE: Only procedural, no LLM
	/// </summary>
	public async Task<Element> GenerateElementFromCombinationAsync(int element1Id, int element2Id, bool forceNew = false)
	{
		try
		{
			// Get elements from service
			Element elem1 = ServiceLocator.Instance.Elements.GetElement(element1Id);
			Element elem2 = ServiceLocator.Instance.Elements.GetElement(element2Id);

			if (elem1 == null || elem2 == null)
			{
				GD.PrintErr($"Cannot find elements: {element1Id}, {element2Id}");
				return null;
			}

			// Check generation mode
			var mode = ServiceLocator.Instance.Config.CurrentGenerationMode;

			// LEGACY MODE: Full LLM generation (original behavior)
			if (mode == Services.GenerationMode.FullLLMMode)
			{
				if (!useLLM)
				{
					GD.PrintErr("LLM is disabled - cannot use FullLLMMode");
					return null;
				}

				GD.Print("Using Full LLM generation mode (legacy)");
				GD.Print($"🔮 Generating NEW element from {elem1.Name} + {elem2.Name}...");
				var result = await GenerateElementWithLLMAsync(elem1, elem2);
				GD.Print($"✨ Created element: {result.Name}");
				return result;
			}

			// PROCEDURAL/HYBRID MODE: Generate mechanics procedurally
			GD.Print($"⚡ Generating element from {elem1.Name} + {elem2.Name} (mode: {mode})");

			int seed = HashElements(element1Id, element2Id);

			// STEP 1: Generate ability mechanically (INSTANT)
			AbilityV2 ability = proceduralComposer.ComposeAbility(elem1, elem2, seed);

			// STEP 2: Generate procedural name/description (fallback)
			string procName = nameGenerator.GenerateName(elem1, elem2, seed);
			string procDesc = nameGenerator.GenerateDescription(ability);
			string procColor = nameGenerator.BlendColors(elem1.ColorHex, elem2.ColorHex);

			GD.Print($"✅ Procedural generation complete:");
			GD.Print($"   Name: {procName}");
			GD.Print($"   Color: {procColor}");
			GD.Print($"   Ability: {ability.Description}");

			// STEP 3: Create element immediately with procedural values
			var newElement = new Element
			{
				Name = procName,
				Description = procDesc,
				ColorHex = procColor,
				Tier = Math.Max(elem1.Tier, elem2.Tier) + 1,
				Recipe = new List<int> { elem1.Id, elem2.Id },
				Ability = ability,
				Properties = ElementProperties.Merge(
					elem1.Properties ?? ElementProperties.CreateDefault(),
					elem2.Properties ?? ElementProperties.CreateDefault()
				)
			};

			// STEP 4: Cache immediately (for database persistence)
			int elementId = ServiceLocator.Instance.Elements.CacheElement(newElement);
			GD.Print($"✅ Element cached with ID: {elementId}");

			// STEP 5: LLM flavor request (HYBRID mode - WAIT for it)
			if (mode == Services.GenerationMode.HybridMode && useLLM)
			{
				GD.Print("🎨 Requesting LLM flavor text (waiting)...");

				// WAIT for LLM to complete before returning
				var enhancedElement = await RequestLLMFlavorAsync(newElement, elem1, elem2, ability);

				// Return the LLM-enhanced version
				return enhancedElement ?? newElement; // Fallback to procedural if LLM fails
			}
			else if (mode == Services.GenerationMode.PureProceduralMode)
			{
				GD.Print("✓ Pure procedural mode - no LLM flavor");
			}

			return newElement;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to generate element combination: {ex.Message}");
			GD.PrintErr($"Stack trace: {ex.StackTrace}");
			return null;
		}
	}

	/// <summary>
	/// Request LLM flavor text and return the enhanced element
	/// Returns null if LLM fails (caller should use original element)
	/// </summary>
	private async Task<Element> RequestLLMFlavorAsync(Element element, Element elem1, Element elem2, AbilityV2 ability)
	{
		try
		{
			GD.Print($"🔮 Generating LLM flavor for {element.Name}...");

			// Use LLMClientV2.GenerateFlavorTextAsync (from Phase 5)
			var flavor = await llmClient.GenerateFlavorTextAsync(
				elem1.Name, elem2.Name, ability.Description);

			// Update only name/desc/color, keep ability unchanged
			element.Name = flavor.Name;
			element.Description = flavor.Description; // Technical description with stats
			element.ColorHex = flavor.ColorHex;

			// Element is already cached with ID, no need to update again
			// UpdateElement and ElementFlavorUpdated signal not available in this branch

			GD.Print($"✨ Updated with LLM flavor: {flavor.Name}");
			GD.Print($"   New description: {flavor.Description}");
			GD.Print("   Ability JSON:");
			GD.Print(ability?.ToJson() ?? "   <null ability>");

			return element; // Return the enhanced element
		}
		catch (Exception ex)
		{
			GD.Print($"⚠️ LLM flavor failed, keeping procedural name: {ex.Message}");
			return null; // Signal failure - caller should use original procedural element
		}
	}

	/// <summary>
	/// Hash two element IDs to create a deterministic seed
	/// </summary>
	private int HashElements(int id1, int id2)
	{
		// Sort IDs to ensure same seed regardless of order
		int min = Math.Min(id1, id2);
		int max = Math.Max(id1, id2);
		return (min * 1000) + max;
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
		// ID will be auto-assigned by database when cached
		var newElement = new Element
		{
			// Id is auto-generated by database - don't set it
			Primitive = null,  // Dynamic elements don't have a primitive type
			Name = response.Name,
			Description = response.Description,
			ColorHex = response.ColorHex,
			Tier = 2,
			Recipe = new List<int> { elem1.Id, elem2.Id },
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
}
