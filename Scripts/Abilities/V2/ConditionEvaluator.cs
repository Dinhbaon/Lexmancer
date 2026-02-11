using Godot;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Lexmancer.Abilities.Execution;
using Lexmancer.Combat;
using Lexmancer.Core;
using Lexmancer.Services;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Safe condition evaluator using pattern matching (no eval or reflection)
/// </summary>
public static class ConditionEvaluator
{
	/// <summary>
	/// Evaluate a condition string using safe pattern matching
	/// </summary>
	public static bool Evaluate(string condition, EffectContext ctx)
	{
		if (string.IsNullOrWhiteSpace(condition))
			return true;

		condition = condition.Trim().ToLower();

		// Health percentage checks: "target.health < 0.5" or "caster.health > 0.8"
		if (condition.Contains(".health") && (condition.Contains("<") || condition.Contains(">")))
		{
			return EvaluateHealthCondition(condition, ctx);
		}

		// Status checks: "target.has_status('burning')" or "caster.has_status('burning')"
		if (condition.Contains(".has_status"))
		{
			return EvaluateStatusCondition(condition, ctx);
		}

		// Status stack checks: "target.status_stacks('poisoned') > 2" or "caster.status_stacks('poisoned') > 2"
		if (condition.Contains(".status_stacks"))
		{
			return EvaluateStatusStacksCondition(condition, ctx);
		}

		// Distance checks: "distance < 200" (relationship between entities)
		if (condition.Contains("distance"))
		{
			return EvaluateDistanceCondition(condition, ctx);
		}

		// Default: unrecognized condition, log warning and return true (execute anyway)
		GD.PrintErr($"Unrecognized condition: '{condition}'. Executing action anyway.");
		return true;
	}

	/// <summary>
	/// Evaluate health-based conditions
	/// </summary>
	private static bool EvaluateHealthCondition(string condition, EffectContext ctx)
	{
		// Extract entity, operator, and threshold
		// Patterns: "target.health < 0.5", "caster.health > 0.8"
		var match = Regex.Match(condition, @"(target|caster)\.health\s*([<>]=?)\s*([\d.]+)");
		if (!match.Success)
			return false;

		string entityRef = match.Groups[1].Value;
		string op = match.Groups[2].Value;
		float threshold = float.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

		// Get the referenced entity
		Node entity = entityRef == "target" ? ctx.Target : ctx.Caster;
		if (entity == null)
		{
			GD.PrintErr($"Condition references {entityRef} but it is null in context");
			return false;
		}

		var health = GetHealthComponent(entity);
		if (health == null)
		{
			GD.PrintErr($"Health component not found on {entity.Name} (looking for child named 'HealthComponent')");
			return false;
		}

		float healthPercent = health.Current / health.Max;
		GD.Print($"[Condition] {entityRef}.health = {health.Current}/{health.Max} = {healthPercent:0.00} {op} {threshold}?");

		return op switch
		{
			"<" => healthPercent < threshold,
			"<=" => healthPercent <= threshold,
			">" => healthPercent > threshold,
			">=" => healthPercent >= threshold,
			_ => false
		};
	}

	/// <summary>
	/// Evaluate status presence conditions
	/// </summary>
	private static bool EvaluateStatusCondition(string condition, EffectContext ctx)
	{
		// Extract entity and status name: "target.has_status('burning')" or "caster.has_status('burning')"
		var match = Regex.Match(condition, @"(target|caster)\.has_status\s*\(\s*[""'](\w+)[""']\s*\)");
		if (!match.Success)
			return false;

		string entityRef = match.Groups[1].Value;
		string status = match.Groups[2].Value;

		// Get the referenced entity
		Node entity = entityRef == "target" ? ctx.Target : ctx.Caster;
		if (entity == null)
		{
			GD.PrintErr($"Condition references {entityRef} but it is null in context");
			return false;
		}

		var statusManager = ServiceLocator.Instance.Combat.StatusEffects;
		if (statusManager == null)
			return false;

		return statusManager.HasStatus(entity, status);
	}

	/// <summary>
	/// Evaluate status stack count conditions
	/// </summary>
	private static bool EvaluateStatusStacksCondition(string condition, EffectContext ctx)
	{
		// Extract entity, status name, and threshold: "target.status_stacks('poisoned') > 2" or "caster.status_stacks('poisoned') > 2"
		var match = Regex.Match(condition, @"(target|caster)\.status_stacks\s*\(\s*[""'](\w+)[""']\s*\)\s*([<>]=?)\s*(\d+)");
		if (!match.Success)
			return false;

		string entityRef = match.Groups[1].Value;
		string status = match.Groups[2].Value;
		string op = match.Groups[3].Value;
		int threshold = int.Parse(match.Groups[4].Value);

		// Get the referenced entity
		Node entity = entityRef == "target" ? ctx.Target : ctx.Caster;
		if (entity == null)
		{
			GD.PrintErr($"Condition references {entityRef} but it is null in context");
			return false;
		}

		var statusManager = ServiceLocator.Instance.Combat.StatusEffects;
		if (statusManager == null)
			return false;

		int stacks = statusManager.GetStatusStacks(entity, status);

		return op switch
		{
			"<" => stacks < threshold,
			"<=" => stacks <= threshold,
			">" => stacks > threshold,
			">=" => stacks >= threshold,
			_ => false
		};
	}

	/// <summary>
	/// Evaluate distance conditions
	/// </summary>
	private static bool EvaluateDistanceCondition(string condition, EffectContext ctx)
	{
		// Extract threshold: "distance < 200"
		var match = Regex.Match(condition, @"distance\s*([<>]=?)\s*([\d.]+)");
		if (!match.Success)
			return false;

		string op = match.Groups[1].Value;
		float threshold = float.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

		var targetPos = (ctx.Target as Node2D)?.GlobalPosition ?? ctx.Position;
		var casterPos = (ctx.Caster as Node2D)?.GlobalPosition ?? Vector2.Zero;
		float distance = targetPos.DistanceTo(casterPos);

		return op switch
		{
			"<" => distance < threshold,
			"<=" => distance <= threshold,
			">" => distance > threshold,
			">=" => distance >= threshold,
			_ => false
		};
	}

	/// <summary>
	/// Get HealthComponent from a node
	/// </summary>
	private static HealthComponent GetHealthComponent(Node target)
	{
		// Check if node has HealthComponent as child
		return target.GetNodeOrNull<HealthComponent>("HealthComponent");
	}
}
