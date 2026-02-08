using Godot;
using System;
using System.Text.RegularExpressions;
using Lexmancer.Combat;
using Lexmancer.Abilities.Execution;

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

		// Health percentage checks: "target.health < 0.5"
		if (condition.Contains("target.health") && (condition.Contains("<") || condition.Contains(">")))
		{
			return EvaluateHealthCondition(condition, ctx);
		}

		// Status checks: "target.has_status(\"burning\")" or "target.has_status('burning')"
		if (condition.Contains("has_status"))
		{
			return EvaluateStatusCondition(condition, ctx);
		}

		// Status stack checks: "target.status_stacks(\"poisoned\") > 2"
		if (condition.Contains("status_stacks"))
		{
			return EvaluateStatusStacksCondition(condition, ctx);
		}

		// Distance checks: "distance < 200" or "distance(target, caster) < 200"
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
		if (ctx.Target == null)
			return false;

		var health = GetHealthComponent(ctx.Target);
		if (health == null)
			return false;

		float healthPercent = health.Current / health.Max;

		// Extract threshold and operator
		// Patterns: "target.health < 0.5", "target.health > 0.3"
		var match = Regex.Match(condition, @"target\.health\s*([<>]=?)\s*([\d.]+)");
		if (!match.Success)
			return false;

		string op = match.Groups[1].Value;
		float threshold = float.Parse(match.Groups[2].Value);

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
		if (ctx.Target == null)
			return false;

		// Extract status name: has_status("burning") or has_status('burning')
		var match = Regex.Match(condition, @"has_status\s*\(\s*[""'](\w+)[""']\s*\)");
		if (!match.Success)
			return false;

		string status = match.Groups[1].Value;

		var statusManager = StatusEffectManager.Instance;
		if (statusManager == null)
			return false;

		return statusManager.HasStatus(ctx.Target, status);
	}

	/// <summary>
	/// Evaluate status stack count conditions
	/// </summary>
	private static bool EvaluateStatusStacksCondition(string condition, EffectContext ctx)
	{
		if (ctx.Target == null)
			return false;

		// Extract status name and threshold: status_stacks("poisoned") > 2
		var match = Regex.Match(condition, @"status_stacks\s*\(\s*[""'](\w+)[""']\s*\)\s*([<>]=?)\s*(\d+)");
		if (!match.Success)
			return false;

		string status = match.Groups[1].Value;
		string op = match.Groups[2].Value;
		int threshold = int.Parse(match.Groups[3].Value);

		var statusManager = StatusEffectManager.Instance;
		if (statusManager == null)
			return false;

		int stacks = statusManager.GetStatusStacks(ctx.Target, status);

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
		float threshold = float.Parse(match.Groups[2].Value);

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
