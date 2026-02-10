using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Lexmancer.Core;
using Lexmancer.Services;
using OllamaSharp;
using OllamaSharp.Models;
using Lexmancer.Abilities.V2;
using Lexmancer.Abilities.LLM;
using Lexmancer.Elements;
using System.Threading;

/// <summary>
/// V2 LLM Client with creative prompting for effect scripts.
/// Supports both LLamaSharp direct inference and OllamaSharp HTTP fallback.
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

        // HTTP client is always prepared; selection happens per-call
        var httpClient = new System.Net.Http.HttpClient
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            BaseAddress = new Uri(baseUrl)
        };

        this.ollama = new OllamaApiClient(httpClient);
        this.ollama.SelectedModel = model;
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

            if (CanUseDirect())
            {
                GD.Print($"Generating element via LLamaSharp: {element1Name} + {element2Name}");
                jsonResponse = await ModelManager.Instance.InferAsync(prompt, CancellationToken.None);
            }
            else
            {
                GD.Print($"Sending element creation request to LLM (HTTP fallback): {element1Name} + {element2Name}");

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

Examples: Fire + Earth → Lava/Magma/Obsidian, Water + Fire → Steam/Geysir, Earth + Water → Mud/Quicksand

SUPPORTED ACTIONS:
1. ""spawn_melee"" - Melee attack with shaped hitbox
   args: shape (""arc""/""circle""/""rectangle""), range (0.5-3), arc_angle (30-360, for arc), width (0.2-2, for rectangle), windup_time (0-0.3), active_time (0.1-0.5),
         movement (""stationary""/""dash""/""lunge""/""jump_smash""/""backstep""/""blink""/""teleport_strike""), move_distance (0-4), move_duration (0-0.6)

   MOVEMENT TYPES:
   - ""stationary"": No movement, attack at current position
   - ""dash"": Invulnerable dash forward, leaves slash trail that damages enemies along path (best for aggressive gap-closing)
   - ""lunge"": Leap to location, attack on landing (gap closer + strike)
   - ""jump_smash"": Leap to location, attack on landing (typically with circle/AOE for ground slam effect)
   - ""backstep"": Move backward while attacking (defensive retreat)
   - ""blink"": Instant teleport to location
   - ""teleport_strike"": Teleport to nearest enemy

2. ""spawn_projectile"" - Shoot projectile(s)
   args: count (1-5), speed (200-800), pattern (""single""/""spread""/""spiral""/""circle""), acceleration (-500 to 500, optional), piercing (true/false), max_pierce_hits (1-10)

3. ""spawn_area"" - Create area effect
   args: radius (50-300), duration (1-10), lingering_damage (0-20), growth_time (0-3, optional), damage (optional)

4. ""spawn_beam"" - Fire a beam
   args: length (200-800), width (10-50), duration (0.5-3), travel_time (0-2, optional)

5. ""damage"" - Deal damage (ONLY in on_hit/on_expire)
   args: amount (1-100), element (""fire""/""water""/""earth""/""lightning""/""poison""/""wind""/""shadow""/""light""/""neutral""), area_radius (optional)

6. ""heal"" - Restore health (ONLY in on_hit/on_expire)
   args: amount (10-50)

7. ""apply_status"" - Apply status effect (ONLY in on_hit/on_expire)
   args: status (""burning""/""frozen""/""poisoned""/""shocked""/""slowed""/""stunned""/""weakened""/""feared""), duration (1-10), stacks (true/false)

8. ""knockback"" - Push target (ONLY in on_hit/on_expire)
   args: force (100-500), direction (""away""/""towards""/""towards_center"")
   NOTE: ""towards_center"" only for area effects

9. ""chain_to_nearby"" - Chain to other enemies
   args: max_chains (2-5), range (100-300)

10. ""repeat"" - Repeat an action multiple times
   args: count (2-5), interval (0.5-2)

CONDITIONAL EXECUTION (Optional - use sparingly for interesting synergies):
Add a ""condition"" field to any action to execute it conditionally.

Examples:
- {{""action"": ""damage"", ""args"": {{""amount"": 40}}, ""condition"": {{""if"": ""target.health < 0.5""}}}} - Execute damage
- {{""action"": ""chain_to_nearby"", ""args"": {{...}}, ""condition"": {{""if"": ""target.has_status('shocked')""}}}} - Chain if shocked
- {{""action"": ""spawn_area"", ""args"": {{...}}, ""condition"": {{""if"": ""target.status_stacks('poisoned') >= 3""}}}} - Trigger at 3+ stacks

Supported conditions:
- ""target.health < 0.5"" - Health percentage checks (<, <=, >, >=)
- ""target.has_status('burning')"" - Check if target has a status
- ""target.status_stacks('poisoned') >= 3"" - Check status stack count
- ""distance < 200"" - Distance between caster and target

WHEN TO USE CONDITIONALS:
✅ Execute mechanics (bonus damage to low HP enemies)
✅ Status synergies (explode if target has 3+ poison stacks)
✅ Combo abilities (chain lightning if target is shocked)
❌ Don't overuse - only 20-30% of abilities should have conditionals

CRITICAL RULES:
⚠️  NEVER create abilities that ONLY do damage
⚠️  EVERY ability MUST include interesting mechanics: status effects, area damage, multiple projectiles, piercing, melee shapes, chaining, knockback, healing, repeat, beams

✅ GOOD: Arc/circle/rectangle melee + status, Projectiles + chains/status, Areas + lingering damage, Beams + knockback
❌ BAD: Single projectile with only damage, Beam with only damage

ATTACK TYPE VARIETY: Mix it up!
- Melee (arc/circle/rectangle) for close-range, physical elements
- Projectiles for ranged, magical elements
- Areas for zone control
- Beams for piercing attacks

STRUCTURE:
1. TOP-LEVEL: spawn_projectile, spawn_area, spawn_beam, spawn_melee, chain_to_nearby, repeat
2. NESTED in on_hit/on_expire: damage, apply_status, heal, knockback

DESCRIPTION RULES:
Generate actions FIRST, then write ability ""description"" to match what you created.
- Mention every action type present (melee/projectile/area/beam).
- spawn_melee → mention strike/slash/cleave/slam/thrust and the shape (arc/circle/rectangle) when used.
- movement != ""stationary"" → mention dash/lunge/blink/teleport/backstep/jump.
- spawn_projectile count=1 → ""fire a projectile"" or ""shoot a bolt"".
- spawn_projectile count>1 → mention count/""multiple"" and the pattern if not ""single"".
- projectile piercing=true → mention piercing or ""passes through enemies"".
- chain_to_nearby → mention chaining to nearby enemies.
- repeat → mention repeated casts/waves (count if possible).
- spawn_area → mention zone/area/pool; if lingering_damage>0 mention lingering damage; if growth_time>0 mention expanding/growing.
- spawn_beam → mention beam/lance/ray; if travel_time>0 mention it travels forward.
- on_hit apply_status → mention EACH status effect.
- on_hit knockback → mention push/pull/knock back.
- on_hit heal → mention healing.

EXAMPLES:

Melee (Fire + Shadow):
{{
  ""name"": ""Shadowflame Reaper"",
  ""description"": ""A dark flame scythe that cleaves through enemies in a wide arc."",
  ""color"": ""#8B0000"",
  ""ability"": {{
    ""primitives"": [""fire"", ""shadow""],
    ""effects"": [{{""script"": [{{
      ""action"": ""spawn_melee"",
      ""args"": {{""shape"": ""arc"", ""range"": 2.5, ""arc_angle"": 160, ""movement"": ""teleport_strike"", ""move_distance"": 2.5, ""move_duration"": 0.08}},
      ""on_hit"": [
        {{""action"": ""damage"", ""args"": {{""amount"": 35, ""element"": ""fire""}}}},
        {{""action"": ""apply_status"", ""args"": {{""status"": ""burning"", ""duration"": 5}}}}
      ]}}]}}],
    ""cooldown"": 2.0,
    ""description"": ""Teleport and cleave in a 160° arc, burning enemies""
  }}
}}

Projectiles (Water + Lightning):
{{
  ""name"": ""Stormwave"",
  ""description"": ""Electrified water bolts that spread and chain viciously to shocked enemies."",
  ""color"": ""#1E90FF"",
  ""ability"": {{
    ""primitives"": [""water"", ""lightning""],
    ""effects"": [{{""script"": [{{
      ""action"": ""spawn_projectile"",
      ""args"": {{""count"": 3, ""speed"": 600, ""pattern"": ""spread""}},
      ""on_hit"": [
        {{""action"": ""damage"", ""args"": {{""amount"": 20}}}},
        {{""action"": ""apply_status"", ""args"": {{""status"": ""shocked"", ""duration"": 4}}}},
        {{""action"": ""chain_to_nearby"", ""args"": {{""max_chains"": 3, ""range"": 200}}, ""condition"": {{""if"": ""target.has_status('shocked')""}}, ""on_hit"": [{{""action"": ""damage"", ""args"": {{""amount"": 25}}}}]}}
      ]}}]}}],
    ""cooldown"": 1.5,
    ""description"": ""Fire 3 electrified bolts in a spread that shock enemies and chain to shocked targets for bonus damage""
  }}
}}

Conditional Execute (Poison + Shadow):
{{
  ""name"": ""Venom Reaper"",
  ""description"": ""A toxic strike that reaps heavily poisoned enemies."",
  ""color"": ""#8B008B"",
  ""ability"": {{
    ""primitives"": [""poison"", ""shadow""],
    ""effects"": [{{""script"": [{{
      ""action"": ""spawn_melee"",
      ""args"": {{""shape"": ""arc"", ""range"": 2.2, ""arc_angle"": 140}},
      ""on_hit"": [
        {{""action"": ""damage"", ""args"": {{""amount"": 18, ""element"": ""poison""}}}},
        {{""action"": ""apply_status"", ""args"": {{""status"": ""poisoned"", ""duration"": 5}}}},
        {{""action"": ""damage"", ""args"": {{""amount"": 45, ""element"": ""shadow""}}, ""condition"": {{""if"": ""target.status_stacks('poisoned') >= 3""}}}}
      ]}}]}}],
    ""cooldown"": 2.5,
    ""description"": ""Slash in an arc applying poison, dealing massive damage if target has 3+ poison stacks""
  }}
}}

NOW CREATE YOUR ELEMENT using primitives [""{element1.ToLower()}"", ""{element2.ToLower()}""].
Make it INTERESTING with status effects, area damage, chains, knockback, or other mechanics!";
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

    private static bool CanUseDirect()
    {
        return ServiceLocator.Instance.Config.UseLLamaSharpDirect && ModelManager.Instance?.IsLoaded == true;
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
