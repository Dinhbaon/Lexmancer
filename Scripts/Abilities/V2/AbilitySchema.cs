using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// JSON Schema for ability validation.
/// Prevents LLM from generating broken abilities like orphaned apply_status actions.
/// </summary>
public static class AbilitySchema
{
	/// <summary>
	/// Get the JSON Schema for ability generation.
	/// This schema enforces correct structure (e.g., apply_status must be in OnHit, not top-level).
	/// </summary>
	public static string GetSchema()
	{
		return @"{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""type"": ""object"",
  ""required"": [""description"", ""primitives"", ""effects"", ""cooldown""],
  ""properties"": {
    ""description"": {
      ""type"": ""string"",
      ""description"": ""Vivid description of what this ability does""
    },
    ""primitives"": {
      ""type"": ""array"",
      ""items"": {""type"": ""string""},
      ""minItems"": 1,
      ""description"": ""List of primitive elements (e.g., fire, water)""
    },
    ""effects"": {
      ""type"": ""array"",
      ""items"": {""$ref"": ""#/definitions/effect""},
      ""minItems"": 1
    },
    ""cooldown"": {
      ""type"": ""number"",
      ""minimum"": 0.1,
      ""maximum"": 10
    }
  },
  ""definitions"": {
    ""effect"": {
      ""type"": ""object"",
      ""required"": [""script""],
      ""properties"": {
        ""script"": {
          ""type"": ""array"",
          ""items"": {""$ref"": ""#/definitions/topLevelAction""},
          ""minItems"": 1,
          ""description"": ""Top-level actions - ONLY spawn_* or chain_to_nearby or repeat""
        }
      }
    },
    ""topLevelAction"": {
      ""type"": ""object"",
      ""required"": [""action"", ""args""],
      ""properties"": {
        ""action"": {
          ""type"": ""string"",
          ""enum"": [""spawn_projectile"", ""spawn_area"", ""spawn_beam"", ""spawn_melee"", ""chain_to_nearby"", ""repeat""],
          ""description"": ""CRITICAL: Only spawners, chain, or repeat allowed at top level. NO damage/apply_status/heal/knockback here!""
        },
        ""args"": {""type"": ""object""},
        ""on_hit"": {
          ""type"": ""array"",
          ""items"": {""$ref"": ""#/definitions/targetAction""},
          ""description"": ""Actions to execute when this hits a target (REQUIRED for spawn_* actions)""
        },
        ""on_expire"": {
          ""type"": ""array"",
          ""items"": {""$ref"": ""#/definitions/targetAction""}
        }
      },
      ""allOf"": [
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""spawn_projectile""}}
          },
          ""then"": {
            ""required"": [""on_hit""],
            ""properties"": {
              ""on_hit"": {""minItems"": 1},
              ""args"": {
                ""required"": [""count"", ""speed""],
                ""properties"": {
                  ""count"": {""type"": ""number"", ""minimum"": 1, ""maximum"": 5},
                  ""speed"": {""type"": ""number"", ""minimum"": 50, ""maximum"": 1200},
                  ""pattern"": {""enum"": [""single"", ""spread"", ""spiral"", ""circle""]},
                  ""acceleration"": {""type"": ""number"", ""minimum"": -500, ""maximum"": 500}
                }
              }
            }
          }
        },
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""spawn_area""}}
          },
          ""then"": {
            ""required"": [""on_hit""],
            ""properties"": {
              ""on_hit"": {""minItems"": 1},
              ""args"": {
                ""required"": [""radius"", ""duration""],
                ""properties"": {
                  ""radius"": {""type"": ""number"", ""minimum"": 50, ""maximum"": 300},
                  ""duration"": {""type"": ""number"", ""minimum"": 1, ""maximum"": 10},
                  ""lingering_damage"": {""type"": ""number"", ""minimum"": 0, ""maximum"": 20},
                  ""growth_time"": {""type"": ""number"", ""minimum"": 0, ""maximum"": 3}
                }
              }
            }
          }
        },
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""spawn_beam""}}
          },
          ""then"": {
            ""required"": [""on_hit""],
            ""properties"": {
              ""on_hit"": {""minItems"": 1},
              ""args"": {
                ""required"": [""length"", ""width"", ""duration""],
                ""properties"": {
                  ""length"": {""type"": ""number"", ""minimum"": 200, ""maximum"": 800},
                  ""width"": {""type"": ""number"", ""minimum"": 10, ""maximum"": 50},
                  ""duration"": {""type"": ""number"", ""minimum"": 0.5, ""maximum"": 3},
                  ""travel_time"": {""type"": ""number"", ""minimum"": 0, ""maximum"": 2}
                }
              }
            }
          }
        },
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""spawn_melee""}}
          },
          ""then"": {
            ""required"": [""on_hit""],
            ""properties"": {
              ""on_hit"": {""minItems"": 1},
              ""args"": {
                ""required"": [""shape"", ""range""],
                ""properties"": {
                  ""shape"": {""enum"": [""arc"", ""circle"", ""rectangle""]},
                  ""range"": {""type"": ""number"", ""minimum"": 0.5, ""maximum"": 3},
                  ""arc_angle"": {""type"": ""number"", ""minimum"": 30, ""maximum"": 360},
                  ""width"": {""type"": ""number"", ""minimum"": 0.2, ""maximum"": 2}
                }
              }
            }
          }
        },
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""chain_to_nearby""}}
          },
          ""then"": {
            ""required"": [""on_hit""],
            ""properties"": {
              ""on_hit"": {""minItems"": 1},
              ""args"": {
                ""required"": [""max_chains"", ""range""],
                ""properties"": {
                  ""max_chains"": {""type"": ""number"", ""minimum"": 2, ""maximum"": 5},
                  ""range"": {""type"": ""number"", ""minimum"": 100, ""maximum"": 300}
                }
              }
            }
          }
        }
      ]
    },
    ""targetAction"": {
      ""type"": ""object"",
      ""required"": [""action"", ""args""],
      ""properties"": {
        ""action"": {
          ""type"": ""string"",
          ""enum"": [""damage"", ""apply_status"", ""heal"", ""knockback"", ""spawn_projectile"", ""spawn_area"", ""spawn_beam"", ""spawn_melee"", ""chain_to_nearby""],
          ""description"": ""Actions in on_hit/on_expire: terminal actions OR nested spawners for complex effects""
        },
        ""args"": {""type"": ""object""},
        ""on_hit"": {
          ""type"": ""array"",
          ""items"": {""$ref"": ""#/definitions/targetAction""},
          ""description"": ""Recursive nesting: spawn actions can have nested on_hit""
        },
        ""on_expire"": {
          ""type"": ""array"",
          ""items"": {""$ref"": ""#/definitions/targetAction""}
        }
      },
      ""allOf"": [
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""damage""}}
          },
          ""then"": {
            ""properties"": {
              ""args"": {
                ""required"": [""amount""],
                ""properties"": {
                  ""amount"": {""type"": ""number"", ""minimum"": 1, ""maximum"": 100},
                  ""element"": {""enum"": [""fire"", ""water"", ""earth"", ""lightning"", ""poison"", ""wind"", ""shadow"", ""light"", ""neutral""]}
                }
              }
            }
          }
        },
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""apply_status""}}
          },
          ""then"": {
            ""properties"": {
              ""args"": {
                ""required"": [""status"", ""duration""],
                ""properties"": {
                  ""status"": {""enum"": [""burning"", ""frozen"", ""poisoned"", ""shocked"", ""slowed"", ""stunned"", ""weakened"", ""feared""]},
                  ""duration"": {""type"": ""number"", ""minimum"": 1, ""maximum"": 10}
                }
              }
            }
          }
        },
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""heal""}}
          },
          ""then"": {
            ""properties"": {
              ""args"": {
                ""required"": [""amount""],
                ""properties"": {
                  ""amount"": {""type"": ""number"", ""minimum"": 10, ""maximum"": 50}
                }
              }
            }
          }
        },
        {
          ""if"": {
            ""properties"": {""action"": {""const"": ""knockback""}}
          },
          ""then"": {
            ""properties"": {
              ""args"": {
                ""required"": [""force"", ""direction""],
                ""properties"": {
                  ""force"": {""type"": ""number"", ""minimum"": 100, ""maximum"": 500},
                  ""direction"": {""enum"": [""away"", ""towards"", ""up""]}
                }
              }
            }
          }
        }
      ]
    }
  }
}";
	}

	/// <summary>
	/// Get a human-readable prompt version of the schema for LLM instruction.
	/// </summary>
	public static string GetSchemaPrompt()
	{
		return @"
ABILITY STRUCTURE REQUIREMENTS (JSON Schema):

ROOT OBJECT (required fields):
- description: string (what the ability does)
- primitives: array of strings (e.g., [""fire"", ""water""])
- effects: array with at least 1 effect
- cooldown: number (0.1-10)

EFFECT STRUCTURE:
- script: array of TOP-LEVEL ACTIONS (at least 1)

TOP-LEVEL ACTIONS (allowed at root level):
✅ spawn_projectile - Shoot projectiles
✅ spawn_area - Create area effect
✅ spawn_beam - Fire a beam
✅ spawn_melee - Melee attack
✅ chain_to_nearby - Chain to nearby enemies
✅ repeat - Repeat actions multiple times

❌ NEVER use these at top level: damage, apply_status, heal, knockback
   These MUST be inside on_hit/on_expire arrays!

SPAWN ACTIONS REQUIREMENTS:
- ALL spawn_* actions MUST have ""on_hit"" array with at least 1 action
- OPTIONAL: Can also have ""on_expire"" array for actions when effect ends
- Example CORRECT structure:
  {
    ""action"": ""spawn_projectile"",
    ""args"": {""count"": 3, ""speed"": 500},
    ""on_hit"": [
      {""action"": ""damage"", ""args"": {""amount"": 25}},
      {""action"": ""apply_status"", ""args"": {""status"": ""burning"", ""duration"": 3}}
    ]
  }

- Example WRONG structure (DO NOT DO THIS):
  {""action"": ""spawn_area"", ""args"": {...}},
  {""action"": ""apply_status"", ""args"": {...}}  ❌ ORPHANED! NO TARGET!

TARGET ACTIONS (ONLY in on_hit/on_expire):
- damage: requires {""amount"": 1-100, ""element"": ""fire""/etc}
- apply_status: requires {""status"": ""burning""/""slowed""/etc, ""duration"": 1-10}
- heal: requires {""amount"": 10-50}
- knockback: requires {""force"": 100-500, ""direction"": ""away""/""towards""}

ON_HIT vs ON_EXPIRE:
- on_hit: Actions executed when the effect hits/touches a target (projectile hits, area touches enemy, etc.)
- on_expire: Actions executed when the effect ends/expires (area disappears, beam ends, etc.)
- Both are optional containers that can hold target actions OR nested spawn actions

RECURSIVE NESTING (for complex abilities):
✅ You CAN nest spawn_* actions inside on_hit/on_expire for creative effects:

  Example 1: Projectile that spawns an area on impact:
  {
    ""action"": ""spawn_projectile"",
    ""on_hit"": [
      {
        ""action"": ""spawn_area"",
        ""args"": {""radius"": 150, ""duration"": 3, ""lingering_damage"": 5},
        ""on_hit"": [
          {""action"": ""apply_status"", ""args"": {""status"": ""burning"", ""duration"": 4}}
        ]
      }
    ]
  }

  Example 2: Area that explodes when it expires:
  {
    ""action"": ""spawn_area"",
    ""args"": {""radius"": 100, ""duration"": 2},
    ""on_hit"": [
      {""action"": ""apply_status"", ""args"": {""status"": ""slowed"", ""duration"": 3}}
    ],
    ""on_expire"": [
      {
        ""action"": ""spawn_projectile"",
        ""args"": {""count"": 8, ""speed"": 400, ""pattern"": ""circle""},
        ""on_hit"": [
          {""action"": ""damage"", ""args"": {""amount"": 15}}
        ]
      }
    ]
  }
";
	}

	/// <summary>
	/// Validate ability JSON against schema (basic structural validation).
	/// Returns true if valid, false otherwise with error messages.
	/// </summary>
	public static bool ValidateAbilityJson(string abilityJson, out List<string> errors)
	{
		errors = new List<string>();

		try
		{
			var doc = JsonDocument.Parse(abilityJson);
			var root = doc.RootElement;

			// Check required fields
			if (!root.TryGetProperty("description", out _))
				errors.Add("Missing required field: description");
			if (!root.TryGetProperty("primitives", out _))
				errors.Add("Missing required field: primitives");
			if (!root.TryGetProperty("effects", out _))
				errors.Add("Missing required field: effects");
			if (!root.TryGetProperty("cooldown", out _))
				errors.Add("Missing required field: cooldown");

			if (errors.Count > 0)
				return false;

			// Validate effects structure
			var effects = root.GetProperty("effects");
			if (effects.GetArrayLength() == 0)
			{
				errors.Add("Effects array is empty");
				return false;
			}

			foreach (var effect in effects.EnumerateArray())
			{
				if (!effect.TryGetProperty("script", out var script))
				{
					errors.Add("Effect missing 'script' array");
					continue;
				}

				if (script.GetArrayLength() == 0)
				{
					errors.Add("Effect script array is empty");
					continue;
				}

				// Validate top-level actions recursively
				foreach (var action in script.EnumerateArray())
				{
					ValidateActionRecursive(action, isTopLevel: true, errors);
				}
			}

			return errors.Count == 0;
		}
		catch (Exception ex)
		{
			errors.Add($"JSON parsing error: {ex.Message}");
			return false;
		}
	}

	/// <summary>
	/// Recursively validate an action and its nested on_hit/on_expire actions.
	/// </summary>
	private static void ValidateActionRecursive(JsonElement action, bool isTopLevel, List<string> errors)
	{
		if (!action.TryGetProperty("action", out var actionType))
		{
			errors.Add("Action missing 'action' field");
			return;
		}

		string actionName = actionType.GetString().ToLower();

		// Check for orphaned terminal actions at top level
		if (isTopLevel)
		{
			if (actionName == "damage" || actionName == "apply_status" ||
			    actionName == "heal" || actionName == "knockback")
			{
				errors.Add($"❌ CRITICAL: '{actionName}' is at top level with no target!");
				errors.Add($"   FIX: Move '{actionName}' inside the 'on_hit' array of a spawn_* action");
			}
		}

		// Check that spawn_* actions have on_hit
		if (actionName.StartsWith("spawn_"))
		{
			if (!action.TryGetProperty("on_hit", out var onHit) || onHit.GetArrayLength() == 0)
			{
				errors.Add($"⚠️ '{actionName}' has no on_hit actions - will do nothing when it hits");
			}
			else
			{
				// Recursively validate nested actions
				foreach (var nestedAction in onHit.EnumerateArray())
				{
					ValidateActionRecursive(nestedAction, isTopLevel: false, errors);
				}
			}

			// Also check on_expire if present
			if (action.TryGetProperty("on_expire", out var onExpire) && onExpire.GetArrayLength() > 0)
			{
				foreach (var nestedAction in onExpire.EnumerateArray())
				{
					ValidateActionRecursive(nestedAction, isTopLevel: false, errors);
				}
			}
		}

		// Check that chain_to_nearby has on_hit
		if (actionName == "chain_to_nearby")
		{
			if (!action.TryGetProperty("on_hit", out var onHit) || onHit.GetArrayLength() == 0)
			{
				errors.Add($"⚠️ 'chain_to_nearby' has no on_hit actions - will chain but do nothing");
			}
			else
			{
				// Recursively validate chained actions
				foreach (var nestedAction in onHit.EnumerateArray())
				{
					ValidateActionRecursive(nestedAction, isTopLevel: false, errors);
				}
			}
		}

		// Validate repeat action
		if (actionName == "repeat")
		{
			if (!action.TryGetProperty("on_hit", out var onHit) || onHit.GetArrayLength() == 0)
			{
				errors.Add($"⚠️ 'repeat' has no on_hit actions - will repeat nothing");
			}
			else
			{
				// Recursively validate repeated actions
				foreach (var nestedAction in onHit.EnumerateArray())
				{
					ValidateActionRecursive(nestedAction, isTopLevel: false, errors);
				}
			}
		}
	}
}
