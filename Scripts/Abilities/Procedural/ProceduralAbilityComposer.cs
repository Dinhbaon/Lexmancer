using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;
using Lexmancer.Elements;

namespace Lexmancer.Abilities.Procedural;

/// <summary>
/// Procedural ability composer - generates balanced abilities from element properties
/// Instant, deterministic, and always produces valid mechanics
/// Uses ProceduralGenerationConfig for all tunable parameters
/// </summary>
public class ProceduralAbilityComposer
{
	private Random rng;
	private ProceduralGenerationConfig config => ProceduralGenerationConfig.Instance;

	/// <summary>
	/// Compose an ability from two element properties
	/// </summary>
	/// <param name="elem1">First element</param>
	/// <param name="elem2">Second element</param>
	/// <param name="seed">Seed for deterministic generation (use element ID hash)</param>
	/// <returns>Fully functional AbilityV2</returns>
	public AbilityV2 ComposeAbility(Element elem1, Element elem2, int seed)
	{
		// Use element IDs as deterministic seed
		if (seed == 0)
			seed = HashElements(elem1.Id, elem2.Id);

		rng = new Random(seed);

		// Merge element properties
		var props1 = elem1.Properties ?? ElementProperties.CreateDefault();
		var props2 = elem2.Properties ?? ElementProperties.CreateDefault();
		var merged = ElementProperties.Merge(props1, props2);

		GD.Print($"Composing ability from {elem1.Name} + {elem2.Name}");
		GD.Print($"  Merged properties: {merged}");

		// Choose delivery method (weighted random)
		string delivery = ChooseDeliveryMethod(merged);
		GD.Print($"  Chosen delivery: {delivery}");

		// Generate ability based on delivery
		AbilityV2 ability = delivery switch
		{
			"projectile" => ComposeProjectileAbility(merged, elem1, elem2),
			"melee" => ComposeMeleeAbility(merged, elem1, elem2),
			"area" => ComposeAreaAbility(merged, elem1, elem2),
			_ => ComposeFallbackAbility(merged, elem1, elem2)
		};

		// Add complexity layers (chains, explosions, conditionals)
		AddComplexityLayers(ability, merged);

		// Set primitives for visual system
		ability.Primitives = new List<string> { elem1.Name.ToLower(), elem2.Name.ToLower() };

		// Calculate cooldown
		ability.Cooldown = CalculateCooldown(ability, merged, delivery);

		// Generate description
		ability.EnsureDescription();

		GD.Print($"  Generated ability: {ability.Description}");
		GD.Print($"  Cooldown: {ability.Cooldown}s");

		return ability;
	}

	/// <summary>
	/// Choose delivery method randomly (equal chance for all)
	/// </summary>
	private string ChooseDeliveryMethod(ElementProperties props)
	{
		// Equal chance for projectile, melee, or area
		return rng.Next(0, 3) switch
		{
			0 => "projectile",
			1 => "melee",
			_ => "area"
		};
	}

	/// <summary>
	/// Compose a projectile ability
	/// </summary>
	private AbilityV2 ComposeProjectileAbility(ElementProperties merged, Element elem1, Element elem2)
	{
		// Projectile count (configured via ProceduralGenerationConfig)
		int count = rng.Next(config.ProjectileCountMin, config.ProjectileCountMax);

		// Speed variance (configured)
		int baseSpeed = rng.Next(config.ProjectileSpeedMin, config.ProjectileSpeedMax);

		// Pattern selection
		string pattern = count > 1 ? (rng.Next(0, 2) == 0 ? "spread" : "burst") : "single";

		// Piercing chance (pure RNG - no preferences)
		bool piercing = rng.Next(0, 100) < config.PiercingChance;

		var ability = new AbilityV2
		{
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
								["count"] = count,
								["speed"] = baseSpeed,
								["pattern"] = pattern,
								["piercing"] = piercing
							},
							OnHit = GenerateOnHitEffects(merged, elem1, elem2)
						}
					}
				}
			}
		};

		return ability;
	}

	/// <summary>
	/// Compose a melee ability
	/// </summary>
	private AbilityV2 ComposeMeleeAbility(ElementProperties merged, Element elem1, Element elem2)
	{
		// Choose shape (arc, circle, rectangle)
		string shape = rng.Next(0, 3) switch
		{
			0 => "arc",
			1 => "circle",
			_ => "rectangle"
		};

		// Determine range (configured)
		float range = config.MeleeRangeMin + (float)rng.NextDouble() * (config.MeleeRangeMax - config.MeleeRangeMin);

		// Determine arc angle for arc shapes (configured)
		int arcAngle = rng.Next(config.ArcAngleMin, config.ArcAngleMax + 1);

		// Determine width for rectangle shapes (0.3-0.8 tiles)
		float width = 0.3f + (float)rng.NextDouble() * 0.5f;

		// Movement type (configured mobility chance)
		string movementType = null;
		if (rng.Next(0, 100) < config.MobilityChance)
		{
			movementType = rng.Next(0, 3) switch
			{
				0 => "dash",
				1 => "lunge",
				_ => null
			};
		}

		var args = new Dictionary<string, object>
		{
			["shape"] = shape,
			["range"] = range,
			["windup_time"] = 0.05 + rng.NextDouble() * 0.05,
			["active_time"] = 0.15 + rng.NextDouble() * 0.15
		};

		if (shape == "arc")
			args["arc_angle"] = arcAngle;
		if (shape == "rectangle")
			args["width"] = width;
		if (movementType != null)
			args["movement_type"] = movementType;

		var ability = new AbilityV2
		{
			Effects = new List<EffectScript>
			{
				new EffectScript
				{
					Script = new List<EffectAction>
					{
						new EffectAction
						{
							Action = "spawn_melee",
							Args = args,
							OnHit = GenerateOnHitEffects(merged, elem1, elem2)
						}
					}
				}
			}
		};

		return ability;
	}

	/// <summary>
	/// Compose an area effect ability
	/// </summary>
	private AbilityV2 ComposeAreaAbility(ElementProperties merged, Element elem1, Element elem2)
	{
		// Determine radius (configured, converted to pixels: *64)
		float radius = (config.AreaRadiusMin + (float)rng.NextDouble() * (config.AreaRadiusMax - config.AreaRadiusMin)) * 64f;

		// Determine duration (configured)
		float duration = config.AreaDurationMin + (float)rng.NextDouble() * (config.AreaDurationMax - config.AreaDurationMin);

		// Lingering damage (configured chance and multiplier)
		float lingeringDamage = 0f;
		if (rng.Next(0, 100) < config.LingeringDamageChance)
		{
			int avgDamage = (merged.MinDamage + merged.MaxDamage) / 2;
			lingeringDamage = avgDamage * config.LingeringDamageMultiplier;
		}

		var ability = new AbilityV2
		{
			Effects = new List<EffectScript>
			{
				new EffectScript
				{
					Script = new List<EffectAction>
					{
						new EffectAction
						{
							Action = "spawn_area",
							Args = new Dictionary<string, object>
							{
								["radius"] = radius,
								["duration"] = duration,
								["lingering_damage"] = lingeringDamage
							},
							OnHit = GenerateOnHitEffects(merged, elem1, elem2, includeInitialDamage: lingeringDamage <= 0)
						}
					}
				}
			}
		};

		return ability;
	}

	/// <summary>
	/// Generate fallback ability (simple projectile)
	/// </summary>
	private AbilityV2 ComposeFallbackAbility(ElementProperties merged, Element elem1, Element elem2)
	{
		GD.Print("  Using fallback ability (simple projectile)");
		return ComposeProjectileAbility(merged, elem1, elem2);
	}

	/// <summary>
	/// Generate OnHit effects (damage + status effects)
	/// </summary>
	private List<EffectAction> GenerateOnHitEffects(ElementProperties merged, Element elem1, Element elem2, bool includeInitialDamage = true)
	{
		var effects = new List<EffectAction>();

		// Always add damage (unless area with lingering damage)
		if (includeInitialDamage)
		{
			int damage = rng.Next(merged.MinDamage, merged.MaxDamage + 1);
			effects.Add(new EffectAction
			{
				Action = "damage",
				Args = new Dictionary<string, object>
				{
					["amount"] = damage,
					["element"] = elem1.Name.ToLower()
				}
			});
		}

		// Add primary status effect
		if (!string.IsNullOrEmpty(merged.PrimaryStatus) && merged.StatusDuration > 0f)
		{
			effects.Add(new EffectAction
			{
				Action = "apply_status",
				Args = new Dictionary<string, object>
				{
					["status"] = merged.PrimaryStatus,
					["duration"] = merged.StatusDuration,
					["stacks"] = false
				}
			});
		}

		// Add secondary status effect (configured chance)
		if (!string.IsNullOrEmpty(merged.SecondaryStatus) && merged.SecondaryStatusDuration > 0f && rng.Next(0, 100) < config.SecondaryStatusChance)
		{
			effects.Add(new EffectAction
			{
				Action = "apply_status",
				Args = new Dictionary<string, object>
				{
					["status"] = merged.SecondaryStatus,
					["duration"] = merged.SecondaryStatusDuration,
					["stacks"] = false
				}
			});
		}

		return effects;
	}

	/// <summary>
	/// Add complexity layers (chains, explosions, conditionals)
	/// All parameters configured via ProceduralGenerationConfig
	/// </summary>
	private void AddComplexityLayers(AbilityV2 ability, ElementProperties merged)
	{
		// Get main action
		var mainAction = ability.Effects[0].Script[0];

		// Add chaining (configured chance, only for projectiles)
		if (mainAction.Action == "spawn_projectile" && rng.Next(0, 100) < config.ChainingChance)
		{
			if (mainAction.OnHit == null)
				mainAction.OnHit = new List<EffectAction>();

			mainAction.OnHit.Add(new EffectAction
			{
				Action = "chain_to_nearby",
				Args = new Dictionary<string, object>
				{
					["max_targets"] = rng.Next(config.ChainTargetsMin, config.ChainTargetsMax + 1),
					["max_distance"] = config.ChainDistanceMin + rng.Next(0, config.ChainDistanceVariance),
					["damage_multiplier"] = config.ChainDamageMultiplierMin + rng.NextDouble() * config.ChainDamageMultiplierVariance
				},
				Condition = BuildOptionalCondition(merged)
			});

			GD.Print("  Added chaining");
		}

		// Add explosion on hit (configured chance, only for projectiles/melee)
		if ((mainAction.Action == "spawn_projectile" || mainAction.Action == "spawn_melee") && rng.Next(0, 100) < config.ExplosionChance)
		{
			if (mainAction.OnHit == null)
				mainAction.OnHit = new List<EffectAction>();

			int avgDamage = (merged.MinDamage + merged.MaxDamage) / 2;
			mainAction.OnHit.Add(new EffectAction
			{
				Action = "spawn_area",
				Args = new Dictionary<string, object>
				{
					["radius"] = config.ExplosionRadiusMin + rng.Next(0, config.ExplosionRadiusVariance),
					["duration"] = config.ExplosionDurationMin + rng.NextDouble() * config.ExplosionDurationVariance,
					["lingering_damage"] = avgDamage * config.ExplosionDamageMultiplier
				},
				Condition = BuildOptionalCondition(merged)
			});

			GD.Print("  Added explosion on hit");
		}

		// Add on_expire effects (configured chance, only for areas)
		if (mainAction.Action == "spawn_area" && rng.Next(0, 100) < config.OnExpireChance)
		{
			int avgDamage = (merged.MinDamage + merged.MaxDamage) / 2;
			mainAction.OnExpire = new List<EffectAction>
			{
				new EffectAction
				{
					Action = "damage",
					Args = new Dictionary<string, object>
					{
						["amount"] = (int)(avgDamage * config.OnExpireDamageMultiplier),
						["element"] = "generic"
					},
					Condition = BuildOptionalCondition(merged)
				}
			};

			GD.Print("  Added on_expire damage");
		}
	}

	/// <summary>
	/// Optionally attach a condition to an action (low chance).
	/// Prefers status-based conditions if available, otherwise falls back to health threshold.
	/// </summary>
	private EffectCondition BuildOptionalCondition(ElementProperties merged)
	{
		if (rng.Next(0, 100) >= config.ConditionalChance)
			return null;

		string condition = null;

		if (!string.IsNullOrEmpty(merged.PrimaryStatus))
		{
			condition = $"target.has_status('{merged.PrimaryStatus}')";
		}
		else if (!string.IsNullOrEmpty(merged.SecondaryStatus))
		{
			condition = $"target.has_status('{merged.SecondaryStatus}')";
		}
		else
		{
			condition = $"target.health < {config.ConditionalHealthThreshold:0.##}";
		}

		return new EffectCondition { If = condition };
	}

	/// <summary>
	/// Calculate cooldown based on ability complexity and damage
	/// </summary>
	private float CalculateCooldown(AbilityV2 ability, ElementProperties merged, string delivery)
	{
		// Base cooldown by delivery method
		float baseCooldown = delivery switch
		{
			"projectile" => 0.8f,
			"melee" => 1.0f,
			"area" => 1.5f,
			_ => 1.0f
		};

		// Adjust for damage
		int avgDamage = (merged.MinDamage + merged.MaxDamage) / 2;
		float damageMultiplier = 1.0f + (avgDamage - 20f) / 50f; // +0.2 per 10 damage over 20

		// Adjust for complexity
		float complexityMultiplier = 1.0f;
		var mainAction = ability.Effects[0].Script[0];

		if (mainAction.OnHit != null && mainAction.OnHit.Count > 2)
			complexityMultiplier += 0.3f;
		if (mainAction.OnExpire != null && mainAction.OnExpire.Count > 0)
			complexityMultiplier += 0.2f;

		// Has chaining?
		if (mainAction.OnHit != null && mainAction.OnHit.Exists(a => a.Action == "chain_to_nearby"))
			complexityMultiplier += 0.5f;

		// Has explosion?
		if (mainAction.OnHit != null && mainAction.OnHit.Exists(a => a.Action == "spawn_area"))
			complexityMultiplier += 0.4f;

		float cooldown = baseCooldown * damageMultiplier * complexityMultiplier * config.CooldownComplexityMultiplier;

		// Clamp to configured range
		return Mathf.Clamp(cooldown, config.CooldownMin, config.CooldownMax);
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
}
