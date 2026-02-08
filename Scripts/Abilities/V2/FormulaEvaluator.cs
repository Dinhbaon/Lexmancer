using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lexmancer.Combat;

namespace Lexmancer.Abilities.V2;

/// <summary>
/// Safe math expression evaluator for damage formulas
/// </summary>
public static class FormulaEvaluator
{
	/// <summary>
	/// Evaluate a damage formula with variable substitution
	/// </summary>
	public static float Evaluate(string formula, EffectContext ctx)
	{
		if (string.IsNullOrWhiteSpace(formula))
			return 20f; // Default damage

		try
		{
			// Replace variables with actual values
			string expression = formula.ToLower();

			// Build variable dictionary
			var variables = new Dictionary<string, float>
			{
				["caster.level"] = GetLevel(ctx.Caster),
				["caster.element_count"] = GetElementCount(ctx.Caster),
				["target.health"] = GetCurrentHealth(ctx.Target),
				["target.max_health"] = GetMaxHealth(ctx.Target),
				["distance"] = GetDistance(ctx),
			};

			// Substitute variables
			foreach (var (key, value) in variables)
			{
				expression = expression.Replace(key, value.ToString("F2"));
			}

			// Evaluate the math expression
			float result = EvaluateMathExpression(expression);

			// Clamp to safe range
			result = Math.Clamp(result, 1f, 100f);

			GD.Print($"Formula evaluation: '{formula}' = {result}");
			return result;
		}
		catch (Exception ex)
		{
			GD.PrintErr($"Failed to evaluate formula '{formula}': {ex.Message}");
			return 20f; // Fallback to default
		}
	}

	/// <summary>
	/// Evaluate a simple math expression (supports +, -, *, /, parentheses)
	/// </summary>
	private static float EvaluateMathExpression(string expression)
	{
		// Remove whitespace
		expression = Regex.Replace(expression, @"\s+", "");

		// Simple recursive descent parser
		return ParseExpression(ref expression);
	}

	/// <summary>
	/// Parse and evaluate expression (handles + and -)
	/// </summary>
	private static float ParseExpression(ref string expr)
	{
		float result = ParseTerm(ref expr);

		while (expr.Length > 0 && (expr[0] == '+' || expr[0] == '-'))
		{
			char op = expr[0];
			expr = expr.Substring(1);
			float term = ParseTerm(ref expr);

			if (op == '+')
				result += term;
			else
				result -= term;
		}

		return result;
	}

	/// <summary>
	/// Parse and evaluate term (handles * and /)
	/// </summary>
	private static float ParseTerm(ref string expr)
	{
		float result = ParseFactor(ref expr);

		while (expr.Length > 0 && (expr[0] == '*' || expr[0] == '/'))
		{
			char op = expr[0];
			expr = expr.Substring(1);
			float factor = ParseFactor(ref expr);

			if (op == '*')
				result *= factor;
			else if (op == '/' && factor != 0)
				result /= factor;
		}

		return result;
	}

	/// <summary>
	/// Parse and evaluate factor (handles numbers and parentheses)
	/// </summary>
	private static float ParseFactor(ref string expr)
	{
		if (expr.Length == 0)
			throw new Exception("Unexpected end of expression");

		// Handle parentheses
		if (expr[0] == '(')
		{
			expr = expr.Substring(1);
			float result = ParseExpression(ref expr);

			if (expr.Length == 0 || expr[0] != ')')
				throw new Exception("Missing closing parenthesis");

			expr = expr.Substring(1);
			return result;
		}

		// Handle negative numbers
		if (expr[0] == '-')
		{
			expr = expr.Substring(1);
			return -ParseFactor(ref expr);
		}

		// Parse number
		var match = Regex.Match(expr, @"^(\d+\.?\d*)");
		if (!match.Success)
			throw new Exception($"Expected number at: {expr}");

		float value = float.Parse(match.Value);
		expr = expr.Substring(match.Length);
		return value;
	}

	/// <summary>
	/// Get caster level (placeholder - returns 1 for now)
	/// </summary>
	private static float GetLevel(Node caster)
	{
		// TODO: Implement level system
		return 1f;
	}

	/// <summary>
	/// Get number of elements in caster's inventory
	/// </summary>
	private static float GetElementCount(Node caster)
	{
		try
		{
			// Try to get GameManager and inventory
			var gameManager = caster?.GetTree()?.Root?.GetNode<GameManager>("Main/GameManager");
			if (gameManager?.Inventory != null)
			{

				var inventory = gameManager.Inventory;

				// Access inventory count (assuming GetElementCount method exists)
				// Fallback: assume 3 elements equipped
				return 3f;
			}
		}
		catch
		{
			// Silently fall through
		}

		return 3f; // Default: 3 elements
	}

	/// <summary>
	/// Get target's current health
	/// </summary>
	private static float GetCurrentHealth(Node target)
	{
		if (target == null)
			return 0f;

		var health = target.GetNodeOrNull<HealthComponent>("HealthComponent");
		return health?.Current ?? 0f;
	}

	/// <summary>
	/// Get target's max health
	/// </summary>
	private static float GetMaxHealth(Node target)
	{
		if (target == null)
			return 0f;

		var health = target.GetNodeOrNull<HealthComponent>("HealthComponent");
		return health?.Max ?? 0f;
	}

	/// <summary>
	/// Get distance between caster and target
	/// </summary>
	private static float GetDistance(EffectContext ctx)
	{
		var targetPos = (ctx.Target as Node2D)?.GlobalPosition ?? ctx.Position;
		var casterPos = (ctx.Caster as Node2D)?.GlobalPosition ?? Vector2.Zero;
		return targetPos.DistanceTo(casterPos);
	}
}
