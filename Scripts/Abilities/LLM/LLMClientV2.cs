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
using Lexmancer.Abilities.LLM;
using Lexmancer.Elements;
using Lexmancer.Config;

/// <summary>
/// V2 LLM Client with creative prompting for effect scripts.
/// Supports both LLamaSharp direct inference and OllamaSharp HTTP fallback.
/// </summary>
public class LLMClientV2
{
    private readonly OllamaApiClient ollama;
    private readonly string baseUrl;
    private readonly string model;
    private readonly bool _useDirect;

    public LLMClientV2(string baseUrl = "http://localhost:11434", string model = "qwen2.5:7b")
    {
        this.baseUrl = baseUrl;
        this.model = model;

        // Check if LLamaSharp direct inference is available
        _useDirect = GameConfig.UseLLamaSharpDirect && ModelManager.Instance?.IsLoaded == true;

        if (_useDirect)
        {
            GD.Print("LLMClientV2 initialized with LLamaSharp direct inference");
        }
        else
        {
            // Fall back to OllamaSharp HTTP client
            var httpClient = new System.Net.Http.HttpClient
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan,
                BaseAddress = new Uri(baseUrl)
            };

            this.ollama = new OllamaApiClient(httpClient);
            this.ollama.SelectedModel = model;

            GD.Print("LLMClientV2 initialized with OllamaSharp HTTP fallback");
        }
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
        string jsonResponse = null;
        try
        {
            var prompt = BuildElementCreationPrompt(element1Name, element2Name, element1Description, element2Description);

            if (_useDirect)
            {
                GD.Print($"Generating element via LLamaSharp: {element1Name} + {element2Name}");
                // Grammar is always enforced - guarantees valid JSON structure and enum values
                jsonResponse = await ModelManager.Instance.InferAsync(prompt);
            }
            else
            {
                GD.Print($"Sending element creation request to LLM: {element1Name} + {element2Name}");

                var request = new GenerateRequest
                {
                    Prompt = prompt,
                    Format = "json",
                    Stream = false,
                    Model = model
                };

                var responseBuilder = new StringBuilder();
                await foreach (var chunk in ollama.Generate(request))
                {
                    if (chunk?.Response != null)
                    {
                        responseBuilder.Append(chunk.Response);
                    }
                }
                jsonResponse = responseBuilder.ToString();
            }

            GD.Print($"Received element response ({jsonResponse.Length} chars)");

            jsonResponse = SanitizeJsonResponse(jsonResponse);

            if (!ElementJsonParser.TryParseElement(
                jsonResponse,
                out var elemName,
                out var elemDescription,
                out var elemColorHex,
                out var elemAbility,
                out var parseError))
            {
                throw new Exception($"Failed to parse element JSON: {parseError}");
            }

            if (string.IsNullOrWhiteSpace(elemName))
                elemName = $"{element1Name}-{element2Name}";
            if (string.IsNullOrWhiteSpace(elemDescription))
                elemDescription = $"A fusion of {element1Name} and {element2Name}";
            if (string.IsNullOrWhiteSpace(elemColorHex))
                elemColorHex = "#808080";

            // Safety: Truncate overly long descriptions (max 200 chars)
            if (elemDescription.Length > 200)
            {
                GD.PrintErr($"Description too long ({elemDescription.Length} chars), truncating to 200");
                elemDescription = elemDescription.Substring(0, 197) + "...";
            }

            if (elemAbility == null)
            {
                elemAbility = CreateFallbackAbilityForElement(elemName);
            }

            GD.Print($"Generated element: {elemName}");
            return new ElementGenerationResponse
            {
                Name = elemName,
                Description = elemDescription,
                ColorHex = elemColorHex,
                Ability = elemAbility
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Element generation failed: {ex.Message}");
            if (jsonResponse != null && jsonResponse.Length > 0)
            {
                GD.PrintErr("LLM JSON Response:");
                GD.PrintErr(jsonResponse.Length > 500 ? jsonResponse.Substring(0, 500) + "..." : jsonResponse);
            }
            return new ElementGenerationResponse
            {
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

SUPPORTED ACTIONS (use a variety of these):
1. ""spawn_melee"" - Melee attack with shaped hitbox ⚔️
   args: shape (""arc""/""circle""/""rectangle""), range (0.5-3 tiles), arc_angle (30-360, for arc), width (0.2-2 tiles, for rectangle), windup_time (0-0.3), active_time (0.1-0.5),
         movement (""stationary""/""dash""/""lunge""/""jump_smash""/""backstep""/""blink""/""teleport_strike""), move_distance (0-4 tiles), move_duration (0-0.6s)
   SHAPES: arc (cone/wedge), circle (360° AOE), rectangle (thrust/stab)

2. ""spawn_projectile"" - Shoot projectile(s)
   args: count (1-5), speed (200-800), pattern (""single""/""spread""/""spiral""), acceleration (-500 to 500, optional)

3. ""spawn_area"" - Create area effect
   args: radius (50-300), duration (1-10), lingering_damage (0-20), growth_time (0-3, optional)

4. ""spawn_beam"" - Fire a beam
   args: length (200-800), width (10-50), duration (0.5-3), travel_time (0-2, optional)

5. ""damage"" - Deal damage
   args: amount (1-100), element (""fire""/""water""/""earth""/""lightning""/""poison""/""wind""/""shadow""/""light"")

6. ""heal"" - Restore health
   args: amount (10-50)

7. ""apply_status"" - Apply status effect
   args: status (""burning""/""frozen""/""poisoned""/""shocked""/""slowed""/""stunned""/""weakened""/""feared""), duration (1-10), stacks (true/false, optional)
   StatusEffectType ids: burning, frozen, poisoned, shocked, slowed, stunned, weakened, feared

8. ""knockback"" - Push target
   args: force (100-500), direction (""away""/""towards"")

9. ""chain_to_nearby"" - Chain to other enemies
   args: max_chains (2-5), range (100-300)

10. ""repeat"" - Repeat an action multiple times
   args: count (2-5), interval (0.5-2)

CRITICAL RULES - ABILITIES MUST BE INTERESTING:
⚠️  NEVER create abilities that ONLY do damage without any additional effects
⚠️  EVERY ability MUST include AT LEAST ONE of these interesting mechanics:
    - Status effects (burning, frozen, poisoned, shocked, slowed, stunned, weakened, feared)
    - Area effects (spawn_area with lingering_damage)
    - Multiple projectiles (count > 1 or pattern spread/spiral)
    - Melee attacks (spawn_melee with different shapes: arc, circle, rectangle)
    - Chaining (chain_to_nearby)
    - Knockback
    - Healing
    - Repeated casts (repeat)
    - Beams
    - Combination of multiple mechanics

✅ GOOD EXAMPLES (use variety of attack types):
- Arc melee slash + knockback + damage (90° cone in front)
- Circle melee slam + stunned status (360° AOE around player)
- Rectangle melee stab + poisoned status (thrust forward)
- Wide arc melee sweep + damage + status (160° cleave)
- Projectile + burning status
- Area with lingering damage + slowed status
- Multiple projectiles in spread pattern
- Beam + knockback + frozen status
- Projectile that chains to nearby enemies
- Repeated projectile waves

ATTACK TYPE VARIETY: Try to mix up attack types! Don't always default to projectiles.
- Use melee (arc/circle/rectangle) for close-range, physical-themed elements
- Use projectiles for ranged, magical-themed elements
- Use areas for persistent, zone-control elements
- Use beams for focused, piercing elements

❌ BAD EXAMPLES (boring, damage only - DO NOT GENERATE):
- Single projectile that only deals damage
- Beam that only deals damage
- Area that only deals instant damage without lingering effects

CRITICAL STRUCTURE RULES:
1. TOP-LEVEL actions (in ""script"" array): ONLY spawn_projectile, spawn_area, spawn_beam, spawn_melee, chain_to_nearby, or repeat
2. ALL spawn_* actions MUST have ""on_hit"" array with actions inside (damage, apply_status, heal, knockback)
3. NEVER put damage/apply_status/heal/knockback at top level - they go INSIDE on_hit arrays
4. Effects array must have at least 1 effect with a non-empty script array

CONCRETE EXAMPLES - Study these and output the same structure:

EXAMPLE 1 - Melee (Fire + Shadow):
{{
  ""name"": ""Shadowflame Reaper"",
  ""description"": ""A dark flame scythe that cleaves through enemies in a wide arc."",
  ""color"": ""#8B0000"",
  ""ability"": {{
    ""description"": ""Swing a scythe of dark flames in a 160° arc"",
    ""primitives"": [""fire"", ""shadow""],
    ""effects"": [
      {{
        ""script"": [
          {{
            ""action"": ""spawn_melee"",
            ""args"": {{""shape"": ""arc"", ""range"": 2.5, ""arc_angle"": 160, ""movement"": ""teleport_strike"", ""move_distance"": 2.5, ""move_duration"": 0.08}},
            ""on_hit"": [
              {{""action"": ""damage"", ""args"": {{""amount"": 35, ""element"": ""fire""}}}},
              {{""action"": ""apply_status"", ""args"": {{""status"": ""burning"", ""duration"": 5}}}}
            ]
          }}
        ]
      }}
    ],
    ""cooldown"": 2.0
  }}
}}

EXAMPLE 2 - Projectiles (Water + Lightning):
{{
  ""name"": ""Stormwave"",
  ""description"": ""Electrified water bolts that spread and chain to enemies."",
  ""color"": ""#1E90FF"",
  ""ability"": {{
    ""description"": ""Fire 3 electrified bolts that shock and chain"",
    ""primitives"": [""water"", ""lightning""],
    ""effects"": [
      {{
        ""script"": [
          {{
            ""action"": ""spawn_projectile"",
            ""args"": {{""count"": 3, ""speed"": 600, ""pattern"": ""spread""}},
            ""on_hit"": [
              {{""action"": ""damage"", ""args"": {{""amount"": 20}}}},
              {{""action"": ""apply_status"", ""args"": {{""status"": ""shocked"", ""duration"": 4}}}},
              {{
                ""action"": ""chain_to_nearby"",
                ""args"": {{""max_chains"": 3, ""range"": 200}},
                ""on_hit"": [{{""action"": ""damage"", ""args"": {{""amount"": 15}}}}]
              }}
            ]
          }}
        ]
      }}
    ],
    ""cooldown"": 1.5
  }}
}}

EXAMPLE 3 - Area (Earth + Poison):
{{
  ""name"": ""Toxic Swamp"",
  ""description"": ""A growing pool of poisonous mud that slows enemies."",
  ""color"": ""#556B2F"",
  ""ability"": {{
    ""description"": ""Create a toxic swamp that grows and poisons"",
    ""primitives"": [""earth"", ""poison""],
    ""effects"": [
      {{
        ""script"": [
          {{
            ""action"": ""spawn_area"",
            ""args"": {{""radius"": 150, ""duration"": 6, ""lingering_damage"": 8, ""growth_time"": 1.5}},
            ""on_hit"": [
              {{""action"": ""apply_status"", ""args"": {{""status"": ""poisoned"", ""duration"": 6}}}},
              {{""action"": ""apply_status"", ""args"": {{""status"": ""slowed"", ""duration"": 4}}}}
            ]
          }}
        ]
      }}
    ],
    ""cooldown"": 2.5
  }}
}}

EXAMPLE 4 - Beam (Light + Lightning):
{{
  ""name"": ""Divine Lance"",
  ""description"": ""A piercing beam of holy lightning that travels slowly and pushes enemies back."",
  ""color"": ""#FFD700"",
  ""ability"": {{
    ""description"": ""Fire a piercing beam of light that deals damage, shocks, and knocks back"",
    ""primitives"": [""light"", ""lightning""],
    ""effects"": [
      {{
        ""script"": [
          {{
            ""action"": ""spawn_beam"",
            ""args"": {{""length"": 600, ""width"": 30, ""duration"": 1.5, ""travel_time"": 0.8}},
            ""on_hit"": [
              {{""action"": ""damage"", ""args"": {{""amount"": 28, ""element"": ""lightning""}}}},
              {{""action"": ""apply_status"", ""args"": {{""status"": ""shocked"", ""duration"": 3}}}},
              {{""action"": ""knockback"", ""args"": {{""force"": 400, ""direction"": ""away""}}}}
            ]
          }}
        ]
      }}
    ],
    ""cooldown"": 2.0
  }}
}}

NOW CREATE YOUR ELEMENT using primitives [""{element1.ToLower()}"", ""{element2.ToLower()}""]:

REMEMBER: Make it INTERESTING! Include status effects, area damage, chains, knockback, beams, or other mechanics!
Be CREATIVE with the element name and make sure the ability matches the element's theme!";
    }

    private static string SanitizeJsonResponse(string jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse))
        {
            return jsonResponse;
        }

        string trimmed = jsonResponse.Trim();

        var extracted = ExtractFirstJsonValue(trimmed);
        if (!string.IsNullOrEmpty(extracted))
        {
            trimmed = extracted;
        }

        if (!IsValidJson(trimmed))
        {
            if (TryRepairTruncatedJson(trimmed, out var repaired) && IsValidJson(repaired))
            {
                trimmed = repaired;
            }
        }

        return trimmed;
    }

    private static bool IsValidJson(string json)
    {
        try
        {
            using var _ = JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ExtractFirstJsonValue(string input)
    {
        int start = -1;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '{' || c == '[')
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            return null;
        }

        var stack = new Stack<char>();
        bool inString = false;
        bool escape = false;

        for (int i = start; i < input.Length; i++)
        {
            char c = input[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if (c == '\\')
                {
                    escape = true;
                    continue;
                }
                if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{' || c == '[')
            {
                stack.Push(c);
                continue;
            }

            if (c == '}' || c == ']')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
                if (stack.Count == 0)
                {
                    return input.Substring(start, i - start + 1);
                }
            }
        }

        return null;
    }

    private static bool TryRepairTruncatedJson(string input, out string repaired)
    {
        repaired = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return false;
        }

        var stack = new Stack<char>();
        bool inString = false;
        bool escape = false;

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if (c == '\\')
                {
                    escape = true;
                    continue;
                }
                if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{' || c == '[')
            {
                stack.Push(c);
                continue;
            }

            if (c == '}' || c == ']')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
            }
        }

        var sb = new StringBuilder(trimmed);

        if (inString)
        {
            if (escape)
            {
                sb.Append("\\\\");
            }
            sb.Append('"');
        }

        while (stack.Count > 0)
        {
            char open = stack.Pop();
            sb.Append(open == '{' ? '}' : ']');
        }

        repaired = sb.ToString();
        return repaired != trimmed;
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

}

/// <summary>
/// Response from LLM element generation
/// ID is assigned by database, not by LLM
/// </summary>
public class ElementGenerationResponse
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string ColorHex { get; set; }
    public AbilityV2 Ability { get; set; }
}
