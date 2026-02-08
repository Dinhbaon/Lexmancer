using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OllamaSharp;
using OllamaSharp.Models;
using Lexmancer.Abilities.V2;

/// <summary>
/// V2 LLM Client with creative prompting for effect scripts
/// </summary>
public class LLMClientV2
{
    private readonly OllamaApiClient ollama;
    private readonly string baseUrl;
    private readonly string model;

    public LLMClientV2(string baseUrl = "http://localhost:11434", string model = "qwen2.5:7b")
    {
        this.baseUrl = baseUrl;
        this.model = model;

        // Create custom HttpClient with infinite timeout for LLM calls
        var httpClient = new System.Net.Http.HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            BaseAddress = new Uri(baseUrl)
        };

        // OllamaApiClient constructor with just HttpClient
        this.ollama = new OllamaApiClient(httpClient);
        this.ollama.SelectedModel = model;

        GD.Print("LLMClientV2 initialized with infinite timeout");
    }

    /// <summary>
    /// Generate ability from primitives using creative effect scripting
    /// </summary>
    public async Task<AbilityV2> GenerateAbilityAsync(string[] primitives)
    {
        try
        {
            var prompt = BuildCreativePrompt(primitives);

            GD.Print($"Sending request to LLM: {baseUrl} (model: {model})");
            GD.Print($"Primitives: {string.Join(" + ", primitives)}");

            // Create request with JSON format
            var request = new GenerateRequest
            {
                Prompt = prompt,
                Format = "json",
                Stream = false,
                Model = model
            };

            // Generate response
            var responseBuilder = new StringBuilder();
            await foreach (var chunk in ollama.Generate(request))
            {
                if (chunk?.Response != null)
                {
                    responseBuilder.Append(chunk.Response);
                }
            }

            var jsonResponse = responseBuilder.ToString();
            GD.Print($"Received response from LLM ({jsonResponse.Length} chars)");

            // Parse using AbilityBuilder
            var ability = AbilityBuilder.FromLLMResponse(jsonResponse);

            GD.Print("✓ Generated ability");
            return ability;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"LLM request failed: {ex.Message}");
            return CreateFallbackAbility(primitives);
        }
    }

    private string BuildCreativePrompt(string[] primitives)
    {
        var primitivesStr = string.Join(" + ", primitives);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return $@"You are a creative game designer AI for a roguelike action game.

Create a UNIQUE ability from: {primitivesStr}

ELEMENT CHARACTERISTICS:
- Fire: Burns, spreads, explosive, aggressive (DoT, area denial)
- Ice: Slows, freezes, defensive, control (CC, shields, walls)
- Lightning: Fast, chains, shocking, multi-target
- Poison: Stacking DoT, corrupting, lingering
- Earth: Heavy, knockback, shields, solid
- Wind: Fast, pushing, mobility, light
- Shadow: Life drain, debuffs, fear, dark
- Light: Healing, buffs, purification, radiant

THINK CREATIVELY about how these elements INTERACT:
- How do they enhance or conflict with each other?
- What unique mechanics emerge from the combination?
- What would this look like visually?

EFFECT SCRIPTING - Use these ACTIONS:

SPAWNING (with speed/timing variety):
- spawn_projectile: {{""count"": 1-5, ""pattern"": ""single|spiral|spread|circle"", ""speed"": 50-1200, ""acceleration"": -500 to 500 (optional)}}
  * acceleration: Makes projectile speed up (positive) or slow down (negative) over time
  * Examples: Lightning should be fast + accelerate, Earth should start fast but decelerate (heavy)
- spawn_area: {{""radius"": 50-300, ""duration"": 1-10, ""lingering_damage"": 0-20, ""growth_time"": 0-3 (optional)}}
  * growth_time: 0 = instant area, >0 = grows from center over time (e.g., lava spreads slowly, lightning explosion is instant)
- spawn_beam: {{""length"": 200-800, ""width"": 10-50, ""duration"": 0.5-3, ""travel_time"": 0-2 (optional)}}
  * travel_time: 0 = instant beam (light), >0 = beam extends over time (slower elements)

DAMAGE:
- damage: {{""amount"": 10-50, ""element"": ""{primitives[0]}""}}
  OR {{""formula"": ""20 * (1 + target.status_stacks * 0.15)""}}
- apply_status: {{""status"": ""burning|frozen|poisoned|shocked|slowed|stunned|weakened|feared"", ""duration"": 1-10, ""stacks"": true|false}}
  * StatusEffectType ids: burning, frozen, poisoned, shocked, slowed, stunned, weakened, feared

MODIFIERS:
- chain_to_nearby: {{""max_chains"": 2-5, ""range"": 100-300}}
- knockback: {{""force"": 100-500, ""direction"": ""away|towards|up""}}
- heal: {{""amount"": 10-30}} (for light element)
- repeat: {{""count"": 2-5, ""interval"": 0.5-2}}

NESTING - Actions can have ""on_hit"" or ""on_expire"":
{{
  ""action"": ""spawn_projectile"",
  ""args"": {{""count"": 3, ""pattern"": ""spread""}},
  ""on_hit"": [
    {{""action"": ""damage"", ""args"": {{""amount"": 25}}}},
    {{""action"": ""apply_status"", ""args"": {{""status"": ""burning""}}}}
  ]
}}

RETURN FORMAT:
{{
  ""description"": ""Vivid description of visuals and mechanics"",
  ""primitives"": [{string.Join(", ", primitives.Select(p => $"\"{p}\""))}],
  ""effects"": [
    {{
      ""script"": [
        {{""action"": ""action_name"", ""args"": {{}}, ""on_hit"": []}}
      ]
    }}
  ],
  ""cooldown"": 2.5,
  ""version"": 2,
  ""generated_at"": {timestamp}
}}

EXAMPLES:

Fire alone - PROJECTILE with burning (medium speed):
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_projectile"", ""args"": {{""count"": 1, ""speed"": 400}},
        ""on_hit"": [
          {{""action"": ""damage"", ""args"": {{""amount"": 30, ""element"": ""fire""}}}},
          {{""action"": ""apply_status"", ""args"": {{""status"": ""burning"", ""duration"": 4}}}}
        ]
      }}
    ]
  }}]
}}

Lightning - FAST PROJECTILE with acceleration:
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_projectile"", ""args"": {{""count"": 1, ""speed"": 500, ""acceleration"": 200}},
        ""on_hit"": [
          {{""action"": ""damage"", ""args"": {{""amount"": 25, ""element"": ""lightning""}}}},
          {{""action"": ""apply_status"", ""args"": {{""status"": ""shocked"", ""duration"": 3}}}}
        ]
      }}
    ]
  }}]
}}

Light - INSTANT BEAM (fast):
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_beam"", ""args"": {{""length"": 600, ""width"": 20, ""duration"": 0.8, ""travel_time"": 0}},
        ""on_hit"": [
          {{""action"": ""damage"", ""args"": {{""amount"": 35, ""element"": ""light""}}}}
        ]
      }}
    ]
  }}]
}}

Ice + Earth - SLOW GROWING BEAM:
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_beam"", ""args"": {{""length"": 400, ""width"": 40, ""duration"": 1.5, ""travel_time"": 0.6}},
        ""on_hit"": [
          {{""action"": ""damage"", ""args"": {{""amount"": 28, ""element"": ""ice""}}}},
          {{""action"": ""knockback"", ""args"": {{""force"": 400, ""direction"": ""away""}}}},
          {{""action"": ""apply_status"", ""args"": {{""status"": ""slowed"", ""duration"": 3}}}}
        ]
      }}
    ]
  }}]
}}

Fire + Earth - GROWING LAVA POOL (spreads over time):
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_area"", ""args"": {{""radius"": 120, ""duration"": 5, ""lingering_damage"": 8, ""growth_time"": 1.2}},
        ""on_expire"": [
          {{""action"": ""damage"", ""args"": {{""amount"": 40, ""area_radius"": 150, ""element"": ""fire""}}}},
          {{""action"": ""apply_status"", ""args"": {{""status"": ""burning"", ""duration"": 3}}}}
        ]
      }}
    ]
  }}]
}}

Shadow - INSTANT AOE (sudden darkness):
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_area"", ""args"": {{""radius"": 100, ""duration"": 2, ""lingering_damage"": 10, ""growth_time"": 0}}}}
    ]
  }}]
}}

Shadow + Poison - BEAM with life drain:
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_beam"", ""args"": {{""length"": 400, ""width"": 30, ""duration"": 1.5}},
        ""on_hit"": [
          {{""action"": ""damage"", ""args"": {{""amount"": 25, ""element"": ""shadow""}}}},
          {{""action"": ""heal"", ""args"": {{""amount"": 15}}}},
          {{""action"": ""apply_status"", ""args"": {{""status"": ""weakened"", ""duration"": 5, ""stacks"": true}}}}
        ]
      }}
    ]
  }}]
}}

Wind + Lightning - PROJECTILE spread with chain:
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""spawn_projectile"", ""args"": {{""count"": 3, ""speed"": 700, ""pattern"": ""spread""}},
        ""on_hit"": [
          {{""action"": ""damage"", ""args"": {{""amount"": 18, ""element"": ""lightning""}}}},
          {{""action"": ""chain_to_nearby"", ""args"": {{""max_chains"": 4, ""range"": 200}},
            ""on_hit"": [
              {{""action"": ""damage"", ""args"": {{""amount"": 12}}}},
              {{""action"": ""apply_status"", ""args"": {{""status"": ""stunned"", ""duration"": 1}}}}
            ]
          }}
        ]
      }}
    ]
  }}]
}}

Light alone - AREA with healing:
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""heal"", ""args"": {{""amount"": 25}}}},
      {{""action"": ""spawn_area"", ""args"": {{""radius"": 200, ""duration"": 3}},
        ""on_hit"": [
          {{""action"": ""heal"", ""args"": {{""amount"": 5}}}}
        ]
      }}
    ]
  }}]
}}

Poison + Fire - PROJECTILE spiral with repeat:
{{
  ""effects"": [{{
    ""script"": [
      {{""action"": ""repeat"", ""args"": {{""count"": 3, ""interval"": 0.6}},
        ""on_hit"": [
          {{""action"": ""spawn_projectile"", ""args"": {{""count"": 5, ""speed"": 400, ""pattern"": ""spiral""}},
            ""on_hit"": [
              {{""action"": ""damage"", ""args"": {{""amount"": 15}}}},
              {{""action"": ""apply_status"", ""args"": {{""status"": ""poisoned"", ""duration"": 5, ""stacks"": true}}}}
            ]
          }}
        ]
      }}
    ]
  }}]
}}

Be CREATIVE! Combine actions in unique ways!";
    }

    /// <summary>
    /// Generate a complete element (name + description + ability) from two base elements
    /// </summary>
    public async Task<ElementGenerationResponse> GenerateElementAsync(
        string element1Name,
        string element2Name,
        string element1Description = null,
        string element2Description = null)
    {
        try
        {
            var prompt = BuildElementCreationPrompt(element1Name, element2Name, element1Description, element2Description);

            GD.Print($"Sending element creation request to LLM: {element1Name} + {element2Name}");

            // Create request with JSON format
            var request = new GenerateRequest
            {
                Prompt = prompt,
                Format = "json",
                Stream = false,
                Model = model
            };

            // Generate response
            var responseBuilder = new StringBuilder();
            await foreach (var chunk in ollama.Generate(request))
            {
                if (chunk?.Response != null)
                {
                    responseBuilder.Append(chunk.Response);
                }
            }

            var jsonResponse = responseBuilder.ToString();
            GD.Print($"Received element response from LLM ({jsonResponse.Length} chars)");

            // Parse JSON response
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonResponse, options);

            // Extract element info
            string name = parsed["name"].GetString();
            string description = parsed["description"].GetString();
            string colorHex = parsed.ContainsKey("color") ? parsed["color"].GetString() : "#808080";

            // Parse ability from the effects section
            AbilityV2 ability;
            if (parsed.ContainsKey("ability"))
            {
                var abilityJson = parsed["ability"].GetRawText();
                ability = AbilityBuilder.FromLLMResponse(abilityJson);
            }
            else
            {
                // Fallback: create basic ability
                ability = CreateFallbackAbilityForElement(name);
            }

            GD.Print($"✓ Generated element: {name}");

            return new ElementGenerationResponse
            {
                Id = name.ToLower().Replace(" ", "_"),
                Name = name,
                Description = description,
                ColorHex = colorHex,
                Ability = ability
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Element generation failed: {ex.Message}");
            // Return fallback
            return new ElementGenerationResponse
            {
                Id = $"{element1Name}_{element2Name}".ToLower(),
                Name = $"{element1Name}-{element2Name}",
                Description = $"A fusion of {element1Name} and {element2Name}",
                ColorHex = "#808080",
                Ability = CreateFallbackAbilityForElement($"{element1Name}-{element2Name}")
            };
        }
    }

    private string BuildElementCreationPrompt(
        string element1,
        string element2,
        string element1Desc = null,
        string element2Desc = null)
    {
        var element1Info = string.IsNullOrEmpty(element1Desc)
            ? element1
            : $"{element1} ({element1Desc})";
        var element2Info = string.IsNullOrEmpty(element2Desc)
            ? element2
            : $"{element2} ({element2Desc})";

        return $@"You are a creative game designer creating magical elements.

Combining: {element1Info} + {element2Info}

Generate a UNIQUE, CREATIVE element. Think beyond the obvious!

Examples of creative combinations:
- Fire + Earth: Lava, Magma, Hellstone, Obsidian, Ashrock, Cinders
- Water + Fire: Steam, Mist, Geysir, Thermal Vent, Scalding Fog
- Earth + Water: Mud, Clay, Quicksand, Swamp, Silt, Fertile Soil

SUPPORTED ACTIONS (use ONLY these):
1. ""spawn_projectile"" - Shoot projectile(s)
   args: count (1-5), speed (200-800), pattern (""single""/""spread""/""spiral"")

2. ""spawn_area"" - Create area effect
   args: radius (50-300), duration (1-10), lingering_damage (0-20)

3. ""spawn_beam"" - Fire a beam
   args: length (200-800), width (10-50), duration (0.5-3)

4. ""damage"" - Deal damage
   args: amount (1-100), element (""fire""/""water""/""earth"")

5. ""heal"" - Restore health
   args: amount (10-50)

6. ""apply_status"" - Apply status effect
   args: status (""burning""/""frozen""/""poisoned""/""shocked""/""slowed""/""stunned""/""weakened""/""feared""), duration (1-10)
   StatusEffectType ids: burning, frozen, poisoned, shocked, slowed, stunned, weakened, feared

7. ""knockback"" - Push target
   args: force (100-500), direction (""away""/""towards"")

8. ""chain_to_nearby"" - Chain to other enemies
   args: max_chains (2-5), range (100-300)

RETURN JSON FORMAT:
{{
  ""name"": ""Creative Element Name"",
  ""description"": ""1-2 sentences describing this element"",
  ""color"": ""#HEXCODE"",
  ""ability"": {{
  ""description"": ""What this ability does"",
  ""primitives"": [""{element1.ToLower()}"", ""{element2.ToLower()}""],
    ""effects"": [
      {{
        ""script"": [
          {{
            ""action"": ""spawn_projectile"",
            ""args"": {{""count"": 1, ""speed"": 400}},
            ""on_hit"": [
              {{""action"": ""damage"", ""args"": {{""amount"": 25}}}}
            ]
          }}
        ]
      }}
    ],
    ""cooldown"": 1.5
  }}
}}

IMPORTANT: Only use the supported actions listed above! Be creative with combinations and values!
Be CREATIVE with the element name! Make it interesting and unique!";
    }

    private AbilityV2 CreateFallbackAbilityForElement(string elementName)
    {
        return new AbilityV2
        {
            Description = $"A basic {elementName} attack",
            Primitives = new List<string> { elementName.ToLower() },
            Effects = new List<EffectScript>
            {
                new EffectScript
                {
                    Script = new List<EffectAction>
                    {
                        new EffectAction
                        {
                            Action = "spawn_projectile",
                            Args = new Dictionary<string, object>
                            {
                                { "count", 1 },
                                { "speed", 400 }
                            },
                            OnHit = new List<EffectAction>
                            {
                                new EffectAction
                                {
                                    Action = "damage",
                                    Args = new Dictionary<string, object>
                                    {
                                        { "amount", 20 }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Cooldown = 1.0f,
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    private AbilityV2 CreateFallbackAbility(string[] primitives)
    {
        var primitivesStr = string.Join(" ", primitives);

        return new AbilityV2
        {
            Description = $"A basic ability combining {primitivesStr}",
            Primitives = new List<string>(primitives.Select(p => p.ToLower())),
            Effects = new List<EffectScript>
            {
                new EffectScript
                {
                    Script = new List<EffectAction>
                    {
                        new EffectAction
                        {
                            Action = "spawn_projectile",
                            Args = new Dictionary<string, object>
                            {
                                { "count", 1 },
                                { "speed", 400 }
                            },
                            OnHit = new List<EffectAction>
                            {
                                new EffectAction
                                {
                                    Action = "damage",
                                    Args = new Dictionary<string, object>
                                    {
                                        { "amount", 20 },
                                        { "element", primitives.Length > 0 ? primitives[0] : "neutral" }
                                    }
                                }
                            }
                        }
                    }
                }
            },
            Cooldown = 1.0f,
            Version = 2,
            GeneratedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }
}

/// <summary>
/// Response from LLM element generation
/// </summary>
public class ElementGenerationResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string ColorHex { get; set; }
    public AbilityV2 Ability { get; set; }
}
