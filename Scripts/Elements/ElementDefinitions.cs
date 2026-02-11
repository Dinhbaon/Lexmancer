using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Core;
using Lexmancer.Services;

namespace Lexmancer.Elements;

/// <summary>
/// Hardcoded element definitions for vertical slice
/// Contains all 8 base primitives with abilities
/// Combinations are now dynamic (LLM-generated)
/// </summary>
public static class ElementDefinitions
{
	/// <summary>
	/// Initialize base elements in the database and return their IDs
	/// Should be called once on game startup
	/// </summary>
	public static Dictionary<PrimitiveType, int> InitializeBaseElements()
	{
		var baseElementIds = new Dictionary<PrimitiveType, int>();

		// Create base elements (without IDs - database will assign them)
		var baseElements = CreateBaseElementsList();

		// Initialize element properties for procedural generation
		InitializeElementProperties(baseElements);

		// Check for existing base elements to avoid duplicates
		var existingBaseElements = ServiceLocator.Instance.Elements.GetElementsByTier(1);
		var existingByPrimitive = new Dictionary<PrimitiveType, Element>();
		foreach (var existing in existingBaseElements)
		{
			if (existing.Primitive.HasValue && !existingByPrimitive.ContainsKey(existing.Primitive.Value))
			{
				existingByPrimitive[existing.Primitive.Value] = existing;
			}
		}

		foreach (var element in baseElements)
		{
			if (!element.Primitive.HasValue)
				continue;

			// If a base element already exists, update it instead of inserting a duplicate
			if (existingByPrimitive.TryGetValue(element.Primitive.Value, out var existing))
			{
				element.Id = existing.Id;
				ServiceLocator.Instance.Elements.CacheElement(element);
				baseElementIds[element.Primitive.Value] = existing.Id;
				GD.Print($"Updated base element: {element.Name} (ID: {existing.Id})");
				continue;
			}

			// Cache element in database (this assigns the ID)
			int id = ServiceLocator.Instance.Elements.CacheElement(element);

			// Store the mapping from primitive type to database ID
			baseElementIds[element.Primitive.Value] = id;
			GD.Print($"Initialized base element: {element.Name} (ID: {id})");
		}

		return baseElementIds;
	}

	/// <summary>
	/// Initialize element properties for procedural generation
	/// Called after base elements are created
	/// </summary>
	private static void InitializeElementProperties(List<Element> baseElements)
	{
		foreach (var element in baseElements)
		{
			if (!element.Primitive.HasValue)
				continue;

			element.Properties = element.Primitive.Value switch
			{
				// PURE STATUS EFFECT IDENTITY - Elements differ ONLY by status effects
				// Everything else is equal:
				// - Damage: 17-21 (all combat elements)
				// - Delivery: 33% projectile/melee/area (pure RNG)
				// - Complexity: 50% chance for all features (pure RNG)
				// - NO preferences - elements don't favor any behaviors

				// Fire: Burning status only
				PrimitiveType.Fire => new ElementProperties
				{
					MinDamage = 17,
					MaxDamage = 21,
					PrimaryStatus = "burning",
					StatusDuration = 3.0f
				},

				// Water: Slowing status only
				PrimitiveType.Ice => new ElementProperties
				{
					MinDamage = 17,
					MaxDamage = 21,
					PrimaryStatus = "slowed",
					StatusDuration = 4.0f
				},

				// Earth: Stunning status only
				PrimitiveType.Earth => new ElementProperties
				{
					MinDamage = 17,
					MaxDamage = 21,
					PrimaryStatus = "stunned",
					StatusDuration = 2.0f
				},

				// Lightning: Shocking status only
				PrimitiveType.Lightning => new ElementProperties
				{
					MinDamage = 17,
					MaxDamage = 21,
					PrimaryStatus = "shocked",
					StatusDuration = 3.0f
				},

				// Poison: Poisoning status only
				PrimitiveType.Poison => new ElementProperties
				{
					MinDamage = 17,
					MaxDamage = 21,
					PrimaryStatus = "poisoned",
					StatusDuration = 5.0f
				},

				// Wind: Pure damage, no status
				PrimitiveType.Wind => new ElementProperties
				{
					MinDamage = 17,
					MaxDamage = 21,
					PrimaryStatus = null,
					StatusDuration = 0f
				},

				// Shadow: Pure damage, no status
				PrimitiveType.Shadow => new ElementProperties
				{
					MinDamage = 17,
					MaxDamage = 21,
					PrimaryStatus = null,
					StatusDuration = 0f
				},

				// Light: Healing element (special case - no damage)
				PrimitiveType.Light => new ElementProperties
				{
					MinDamage = 0,
					MaxDamage = 0,
					PrimaryStatus = null,
					StatusDuration = 0f
				},

				_ => ElementProperties.CreateDefault()
			};

			GD.Print($"Initialized properties for {element.Name}: {element.Properties}");
		}
	}

	/// <summary>
	/// Create base element definitions (without database IDs)
	/// </summary>
	private static List<Element> CreateBaseElementsList() => new()
	{
		new Element
		{
			Primitive = PrimitiveType.Fire,
			Name = "Fire",
			Description = "Crackling flames and searing heat",
			Tier = 1,
			ColorHex = "#FF4500",
			Recipe = new(),
			Ability = CreateFireballAbility()
		},
		new Element
		{
			Primitive = PrimitiveType.Ice,
			Name = "Water",  // Display as "Water" for UI
			Description = "Flowing liquid, cool and adaptable",
			Tier = 1,
			ColorHex = "#1E90FF",
			Recipe = new(),
			Ability = CreateWaterJetAbility()
		},
		new Element
		{
			Primitive = PrimitiveType.Earth,
			Name = "Earth",
			Description = "Solid stone and ancient ground",
			Tier = 1,
			ColorHex = "#8B4513",
			Recipe = new(),
			Ability = CreateRockThrowAbility()
		},
		new Element
		{
			Primitive = PrimitiveType.Lightning,
			Name = "Lightning",
			Description = "Electric energy, rapid and chaotic",
			Tier = 1,
			ColorHex = "#FFD700",
			Recipe = new(),
			Ability = CreateLightningBoltAbility()
		},
		new Element
		{
			Primitive = PrimitiveType.Poison,
			Name = "Poison",
			Description = "Corrosive toxin, insidious and persistent",
			Tier = 1,
			ColorHex = "#9932CC",
			Recipe = new(),
			Ability = CreatePoisonDartAbility()
		},
		new Element
		{
			Primitive = PrimitiveType.Wind,
			Name = "Wind",
			Description = "Rushing air currents, ever-moving",
			Tier = 1,
			ColorHex = "#87CEEB",
			Recipe = new(),
			Ability = CreateWindGustAbility()
		},
		new Element
		{
			Primitive = PrimitiveType.Shadow,
			Name = "Shadow",
			Description = "Darkness that consumes light",
			Tier = 1,
			ColorHex = "#2F4F4F",
			Recipe = new(),
			Ability = CreateShadowDrainAbility()
		},
		new Element
		{
			Primitive = PrimitiveType.Light,
			Name = "Light",
			Description = "Brilliant radiance and illumination",
			Tier = 1,
			ColorHex = "#FFFACD",
			Recipe = new(),
			Ability = CreateLightBurstAbility()
		}
	};

	/// <summary>
	/// Get all base elements (without IDs - for reference only)
	/// Use ElementRegistry.GetElementsByTier(1) to get elements with database IDs
	/// </summary>
	public static List<Element> GetAllBaseElements()
	{
		return CreateBaseElementsList();
	}

	// ==================== ABILITY DEFINITIONS ====================

	/// <summary>
	/// Fire - Fireball: Single projectile, 20 damage + burning status
	/// </summary>
	private static AbilityV2 CreateFireballAbility()
	{
		return new AbilityV2
		{
			Description = "Shoots a burning projectile that ignites enemies",
			Primitives = new() { "fire" },
			Cooldown = 0.5f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_projectile",
							Args = new()
							{
								["count"] = 1,
								["speed"] = 400,
								["pattern"] = "single"
							},
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new()
									{
										["amount"] = 20,
										["element"] = "fire"
									}
								},
								new EffectAction
								{
									Action = "apply_status",
									Args = new()
									{
										["status"] = "burning",
										["duration"] = 3.0,
										["stacks"] = false
									}
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Water/Ice - Ice Shard: Projectile that freezes enemies
	/// </summary>
	private static AbilityV2 CreateWaterJetAbility()
	{
		return new AbilityV2
		{
			Description = "Shoots a freezing projectile that slows enemies",
			Primitives = new() { "water" },
			Cooldown = 0.8f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_projectile",
							Args = new()
							{
								["count"] = 1,
								["speed"] = 450,
								["pattern"] = "single"
							},
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new()
									{
										["amount"] = 15,
										["element"] = "ice"
									}
								},
								new EffectAction
								{
									Action = "apply_status",
									Args = new()
									{
										["status"] = "slowed",
										["duration"] = 4.0,
										["stacks"] = false
									}
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Earth - Ground Slam: Circular melee attack around player, 30 damage + stun
	/// </summary>
	private static AbilityV2 CreateRockThrowAbility()
	{
		return new AbilityV2
		{
			Description = "Slams the ground, damaging nearby enemies in a circle",
			Primitives = new() { "earth" },
			Cooldown = 1.2f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_melee",
							Args = new()
							{
								["shape"] = "circle",
								["range"] = 2.0, // 2 tiles radius
								["windup_time"] = 0.1,
								["active_time"] = 0.3
							},
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new()
									{
										["amount"] = 30,
										["element"] = "earth"
									}
								},
								new EffectAction
								{
									Action = "apply_status",
									Args = new()
									{
										["status"] = "stunned",
										["duration"] = 2.0,
										["stacks"] = false
									}
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Lightning - Lightning Bolt: Fast projectile with acceleration, 25 damage + shocked status
	/// </summary>
	private static AbilityV2 CreateLightningBoltAbility()
	{
		return new AbilityV2
		{
			Description = "Strikes with electrifying speed and shocks enemies",
			Primitives = new() { "lightning" },
			Cooldown = 0.6f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_projectile",
							Args = new()
							{
								["count"] = 1,
								["speed"] = 500,
								["acceleration"] = 200, // Accelerates (electric discharge)
								["pattern"] = "single"
							},
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new()
									{
										["amount"] = 25,
										["element"] = "lightning"
									}
								},
								new EffectAction
								{
									Action = "apply_status",
									Args = new()
									{
										["status"] = "shocked",
										["duration"] = 3.0,
										["stacks"] = false
									}
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Poison - Poison Stab: Thrust melee attack, 15 damage + poison status (DoT)
	/// </summary>
	private static AbilityV2 CreatePoisonDartAbility()
	{
		return new AbilityV2
		{
			Description = "Thrust forward with toxic energy that poisons enemies",
			Primitives = new() { "poison" },
			Cooldown = 0.7f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_melee",
							Args = new()
							{
								["shape"] = "rectangle",
								["range"] = 2.2, // Long thrust
								["width"] = 0.4, // Narrow
								["windup_time"] = 0.06,
								["active_time"] = 0.18
							},
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new()
									{
										["amount"] = 15,
										["element"] = "poison"
									}
								},
								new EffectAction
								{
									Action = "apply_status",
									Args = new()
									{
										["status"] = "poisoned",
										["duration"] = 5.0,
										["stacks"] = false
									}
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Wind - Wind Slash: Arc melee attack in front of player, 18 damage
	/// </summary>
	private static AbilityV2 CreateWindGustAbility()
	{
		return new AbilityV2
		{
			Description = "Slashes the air in a sharp arc",
			Primitives = new() { "wind" },
			Cooldown = 0.6f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_melee",
							Args = new()
							{
								["shape"] = "arc",
								["range"] = 2.0,
								["arc_angle"] = 90, // 90° arc
								["windup_time"] = 0.05, // Quick slash
								["active_time"] = 0.15
							},
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new()
									{
										["amount"] = 18,
										["element"] = "wind"
									}
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Shadow - Reap: Wide arc melee attack, 22 damage
	/// </summary>
	private static AbilityV2 CreateShadowDrainAbility()
	{
		return new AbilityV2
		{
			Description = "Sweeps darkness in a wide arc",
			Primitives = new() { "shadow" },
			Cooldown = 0.8f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "spawn_melee",
							Args = new()
							{
								["shape"] = "arc",
								["range"] = 2.5, // Longer reach
								["arc_angle"] = 160, // Very wide arc
								["windup_time"] = 0.08,
								["active_time"] = 0.25
							},
							OnHit = new()
							{
								new EffectAction
								{
									Action = "damage",
									Args = new()
									{
										["amount"] = 22,
										["element"] = "shadow"
									}
								}
							}
						}
					}
				}
			}
		};
	}

	/// <summary>
	/// Light - Light Burst: Area heal, 20 HP to player/allies
	/// </summary>
	private static AbilityV2 CreateLightBurstAbility()
	{
		return new AbilityV2
		{
			Description = "Radiates healing energy",
			Primitives = new() { "light" },
			Cooldown = 1.2f,
			Effects = new()
			{
				new EffectScript
				{
					Script = new()
					{
						new EffectAction
						{
							Action = "heal",
							Args = new()
							{
								["amount"] = 20
							}
						}
					}
				}
			}
		};
	}
}
